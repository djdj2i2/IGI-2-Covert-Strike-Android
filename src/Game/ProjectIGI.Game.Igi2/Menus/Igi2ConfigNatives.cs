using ProjectIGI.Core.Diagnostics;
using ProjectIGI.Engine.Titles;
using ProjectIGI.Game.Config;
using ProjectIGI.Game.Igi2.Config;
using ProjectIGI.Game.Igi2.Saves;
using ProjectIGI.Game.Igi2.World;
using ProjectIGI.Game.Menus;
using ProjectIGI.Game.Player;
using ProjectIGI.Game.World;
using ProjectIGI.Scripting.Expressions;
using ProjectIGI.Scripting.Runtime;

namespace ProjectIGI.Game.Igi2.Menus;

/// <summary>
/// The configuration functions I.G.I. 2's retail menu scripts call (its <c>Config_*</c> natives), over
/// <see cref="Igi2GameConfig"/> and the mission catalogue.
/// </summary>
/// <remarks>
/// <para>
/// Project I.G.I.'s equivalent is <c>ConfigNatives</c> over its own <c>GameConfig</c>, with
/// <c>MenuConfigHost</c> supplying what only the shell knows; this is the same shape over the sequel's
/// profiles, whose active mission is one per difficulty rather than one, and whose missions come from
/// the scanned catalogue rather than a chain.
/// </para>
/// <para>
/// This is an <see cref="IMenuConfiguration"/>, not a set of QVM registrations: the retail menu's
/// expressions are evaluated by the menu's own evaluator, which reaches natives only through
/// <c>MenuManagerTask.Invoke</c> — the configuration first, then the game's external natives — so
/// registering these with the script VM (as <see cref="Igi2MenuNatives"/> does for the in-game menu)
/// would leave the retail menu unable to call them.
/// </para>
/// </remarks>
public sealed class Igi2ConfigNatives : IMenuConfiguration, IMenuMissionStats
{
    private readonly Igi2GameConfig _config;
    private readonly GameConfig? _shared;
    private readonly Func<IMissionCatalogue<MissionDescriptor>?> _missions;
    private readonly Func<MenuStringTable?> _strings;
    private readonly Igi2SaveSession? _saves;
    private readonly IProjectIGILogger _logger;

    /// <summary>The mark a locked mission carries in the list.</summary>
    private const string LockedSuffix = " (Locked)";

    /// <summary>Initializes a new instance of the <see cref="Igi2ConfigNatives"/> class.</summary>
    /// <param name="config">The player profiles the settings screens read and write.</param>
    /// <param name="missions">The title's missions, which the mission screens list, or <see langword="null"/> before they are scanned.</param>
    /// <param name="strings">The language's text, or <see langword="null"/> before it is loaded.</param>
    /// <param name="saves">The session's save games, or <see langword="null"/> when the menu has none to count.</param>
    /// <param name="logger">Where the mission fill is reported.</param>
    /// <param name="sharedConfiguration">The shared profiles, whose active profile the mission-completion flow advances; the menu reads its frontier too, because a completion writes there and never into the sequel's own profiles.</param>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    public Igi2ConfigNatives(
        Igi2GameConfig config,
        Func<IMissionCatalogue<MissionDescriptor>?> missions,
        Func<MenuStringTable?> strings,
        Igi2SaveSession? saves = null,
        IProjectIGILogger? logger = null,
        GameConfig? sharedConfiguration = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(missions);
        ArgumentNullException.ThrowIfNull(strings);

        _config = config;
        _shared = sharedConfiguration;
        _missions = missions;
        _strings = strings;
        _saves = saves;
        _logger = logger ?? NullLogger.Instance;
    }

    /// <inheritdoc/>
    public MenuWindowTask? Target { get; set; }

    /// <summary>Gets the mission id the stats screen was last told to show, or zero when none has been.</summary>
    /// <remarks>
    /// What <c>MenuStatsScreen_SetActiveMissionID</c> records. The stats screen reads it back off
    /// the manager's configuration through <c>IMenuMissionStats</c> when it draws.
    /// </remarks>
    public int StatsActiveMissionId { get; private set; }

    /// <summary>
    /// The mission names and descriptions as Ray wrote them, keyed by mission id.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The retail <c>missions.res</c> texts these replace are the ones the mission screen showed
    /// before: the lookup was right — each mission's own <c>"Mission N Desc"</c> key, resolved through
    /// the mounted missions archive — but the archive's descriptions are not the accurate ones, so the
    /// screen showed the wrong words for the right mission. These are Ray's texts, verbatim.
    /// </para>
    /// <para>
    /// Keyed by mission id, not by list position: id 11 is mission 1, 12 is mission 2, and so on
    /// through 36 as mission 19, the order <see cref="Igi2MissionCatalogue.InProgressionOrder"/>
    /// yields. A mission not listed here — a multiplayer map, for example — still falls back to the
    /// string table, so nothing outside the campaign changes.
    /// </para>
    /// <para>
    /// The Russian / Libyan / Chinese act groupings Ray used to organise the list are deliberately
    /// not represented here: the menu shows one mission at a time, with no act headers.
    /// </para>
    /// </remarks>
    private static readonly IReadOnlyDictionary<int, (string Name, string Description)> CorrectedMissionTexts =
        new Dictionary<int, (string Name, string Description)>
        {
            [11] = ("Infiltration", "Infiltrate a Carpathian weather station turned mafia lab via mining tunnels using stealth."),
            [12] = ("Deep in the Mines", "Descend past communication dead zones, disable security locks, and ride the cargo railway to reach the research base."),
            [13] = ("The Weather Station", "Use a laser cutter to break into the vault for prototype EMP chips and launch a weather balloon for extraction."),
            [14] = ("Bridge Across the Dnestr", "Plant C4 on the Dnestr River bridge supports to stop a military convoy carrying stolen chips."),
            [15] = ("Ambush", "Eliminate armored escorts, retrieve anti-armor weapons, and recover the EMP chips from the convoy."),
            [16] = ("Production Facility", "Infiltrate the manufacturing center, disable tracking grids, and download engineering specs with radio silence."),
            [17] = ("Border Crossing", "Evade patrols, searchlights, and minefields to cross the border and reach an extraction chopper."),
            [21] = ("Libyan Rendezvous", "Meet a local intelligence asset at the docks, cause a market distraction, and breach a hotel compound."),
            [22] = ("Prison Escape", "Infiltrate a desert prison, neutralize guards quietly, and rescue Jach Priboi."),
            [23] = ("Priboi's Villa", "Clear out forces loyal to Major Zaleb Said to let Priboi recover private vault documents."),
            [24] = ("The Airfield", "Sneak past checkpoints, sabotage regional air defenses, and board a transport plane."),
            [25] = ("Zaleb's Stronghold", "Assault a fortified desert fortress, defeat Zaleb, and recover the stolen prototypes."),
            [26] = ("Showdown at the Docks", "Fight through an Egyptian maritime port and commandeer a Soviet Ekranoplan transport craft."),
            [31] = ("Island Assault", "Infiltrate a Spratly Islands testing compound controlled by General Wu Xing."),
            [32] = ("Air-Strike", "Use a laser designator from the ridges to call in precision airstrikes on armored valley targets."),
            [33] = ("The Ancient Temple", "Bypass heavy perimeter guards and override four ancient security gates into the jungle."),
            [34] = ("Secret Weapons Lab", "Navigate past tracking cameras to access the research server and copy rocket telemetry data."),
            [35] = ("Mission Control", "Breach the control building and override automated launch sequences to stop an EMP ballistic missile."),
            [36] = ("The Launch Pad", "Climb the service tower and manually trigger the emergency fuel venting system to destroy the rocket on the pad."),
        };

    /// <summary>Gets Ray's corrected name and description for a mission, when one is listed.</summary>
    private static bool TryGetCorrectedText(int missionId, out string name, out string description)
    {
        if (CorrectedMissionTexts.TryGetValue(missionId, out (string Name, string Description) text))
        {
            name = text.Name;
            description = text.Description;
            return true;
        }

        name = string.Empty;
        description = string.Empty;
        return false;
    }

    /// <inheritdoc/>
    public bool TryGetActiveMissionStats(out string name, out string description)
    {
        name = string.Empty;
        description = string.Empty;

        if (StatsActiveMissionId == 0 || _missions() is not { } catalogue)
        {
            return false;
        }

        if (!catalogue.TryGet(StatsActiveMissionId, out MissionDescriptor? mission))
        {
            return false;
        }

        if (TryGetCorrectedText(mission.Id, out string correctedName, out string correctedDescription))
        {
            name = correctedName;
            description = correctedDescription;
            return true;
        }

        name = Localise(mission.Name);
        description = Localise(mission.Description);
        return true;
    }

    /// <inheritdoc/>
    public bool TryInvoke(string name, ScriptArguments arguments, out QvmValue result)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        result = QvmValue.FromInt32(0);

        switch (name)
        {
            case "Config_FillMissionSelectionBox":
                int reached = ReachedMission();
                List<MenuListItem> items = GetMissions(reached).ToList();
                Fill(items);
                _logger.Information(
                    "Menu",
                    $"Config_FillMissionSelectionBox: listed {items.Count} mission(s) [{string.Join(", ", items.Select(item => item.Id))}], reached {reached}.");
                return true;

            case "Config_FillMissionPictureBox":
                FillMissionPictures(Target);
                return true;

            case "Config_PlayerGetActiveMission":
                // The active profile's answer is the merged frontier, so the list opens on the
                // mission the game has reached — a completion writes the shared profile, and the
                // sequel's own profile alone would still answer the mission before it.
                int profileIndex = arguments.GetInt32(0);
                result = QvmValue.FromInt32(
                    profileIndex == _config.ActiveIndex ? ReachedMission() : ActiveMissionOf(ProfileAt(profileIndex)));
                return true;

            case "Config_GetActivePlayerProfileIndex":
                result = QvmValue.FromInt32(_config.ActiveIndex);
                return true;

            case "MenuStatsScreen_SetActiveMissionID":
                // The id back, not zero: screen 914's Get Data is
                // Config_PlayerGetActiveMission(...), MenuStatsScreen_SetActiveMissionID(this.nActiveID),
                // and the comma yields this call's value, so the list's active id becomes what this
                // returns. Echoing the selection keeps it on the row the fill put it on; zero would
                // leave it pointing at no mission and OK would start mission 0.
                int id = arguments.GetInt32(0);
                StatsActiveMissionId = id;
                result = QvmValue.FromInt32(id);
                return true;

            case "Game_GetNumberOfValidSaveGames":
                result = QvmValue.FromInt32(
                    _saves?.Slots?.CountValid(_saves.Owner with { Mission = arguments.GetInt32(0) }) ?? 0);
                return true;

            default:
                return false;
        }
    }

    /// <summary>Puts items into the list box whose collect script is running.</summary>
    private void Fill(IEnumerable<MenuListItem> items)
    {
        if (Target is ListBoxTask list)
        {
            list.SetItems(items);
        }
        else if (Target is ScrollListBoxTask scroll)
        {
            scroll.SetItems(items);
        }
    }

    /// <summary>Walks the campaign, one entry per mission, marking the ones past the frontier locked.</summary>
    /// <remarks>
    /// Project I.G.I.'s <c>Config_FillMissionSelectionBox</c> (<c>0x405660</c>) walks the mission chain
    /// from the first id, adding each mission's name keyed by its id, and stops after the one the
    /// profile has reached — which is what keeps a fresh profile from starting mission 14. The whole
    /// campaign is listed here instead, with the ones past the frontier marked locked and unpickable,
    /// which says the same thing in the open: the missions are there, and the game decides which may
    /// be played. A profile that has reached nothing (active mission zero) counts as standing on the
    /// first mission — without that the frontier is no mission at all, and the whole campaign lists
    /// unlocked, which is how every mission came to be playable from a fresh install.
    /// </remarks>
    private IEnumerable<MenuListItem> GetMissions(int reached)
    {
        int frontier = IndexOf(reached);
        int index = 0;
        foreach (MissionDescriptor mission in Chain())
        {
            bool unlocked = PlayerCheatCVars.UnlockAllLevels.Value || index <= frontier;
            // The corrected name where one is listed, so the list and the stats box
            // never disagree about what a mission is called.
            string displayName = TryGetCorrectedText(mission.Id, out string correctedName, out _)
                ? correctedName
                : Localise(mission.Name);
            yield return new MenuListItem(mission.Id, unlocked ? displayName : displayName + LockedSuffix, unlocked);
            index++;
        }
    }

    /// <summary>Adds every campaign mission's briefing sprite, keyed by id, to the picture box whose collect script is running.</summary>
    /// <remarks>
    /// Project I.G.I.'s <c>Config_FillMissionPictureBox</c> (<c>0x4056F0</c>) walks the whole chain — no
    /// progress stop — adding each mission's briefing sprite keyed by id, and the box then picks one.
    /// The sprites are <c>mission{id}.spr</c> in <c>missionsprites.res</c>, which the menu's host
    /// already mounts as an extra archive, so the bare sprite name is all the box needs.
    /// </remarks>
    private void FillMissionPictures(MenuWindowTask? target)
    {
        if (target is not PictureBoxTask box)
        {
            return;
        }

        foreach (MissionDescriptor mission in Chain())
        {
            box.Entries[mission.Id] = mission.BriefingSprite;
        }
    }

    /// <summary>Walks the campaign in the order it is played.</summary>
    /// <remarks>
    /// The order is the campaign's own grouping — a mission's id is its location and its level read
    /// together — not the table's; see <see cref="Igi2MissionCatalogue.InProgressionOrder"/>. Left
    /// null the screen shows nothing, which is the honest rendering of an installation whose missions
    /// could not be read.
    /// </remarks>
    private IEnumerable<MissionDescriptor> Chain() =>
        _missions()?.InProgressionOrder() ?? [];

    /// <summary>Resolves a descriptor's name key, which is a language key rather than text.</summary>
    private string Localise(string key) => _strings() is { } table ? table.Get(key) : key;

    /// <summary>Gets the furthest mission the active profile has reached: the furthest of the sequel's own profile at its difficulty and the shared profile the mission-completion flow advances.</summary>
    /// <remarks>
    /// A completion writes the shared profile and never the sequel's own, so the sequel's profile
    /// alone would still answer the mission before the one just finished — and the game would forget
    /// the unlock on the next visit to the screen. The shared value only counts when it is a mission
    /// of this campaign: a fresh shared profile carries Project I.G.I.'s numbering, which is no
    /// mission of the sequel's.
    /// </remarks>
    private int ReachedMission()
    {
        int reached = ActiveMissionOf(_config.Active);
        int shared = _shared?.Active?.ActiveMission ?? 0;
        return IndexOf(shared) > IndexOf(reached) ? shared : reached;
    }

    /// <summary>Gets a mission's place in the campaign, or -1 when it is no mission of it.</summary>
    private int IndexOf(int missionId)
    {
        int index = 0;
        foreach (MissionDescriptor mission in Chain())
        {
            if (mission.Id == missionId)
            {
                return index;
            }

            index++;
        }

        return -1;
    }

    /// <summary>Gets the first mission of the campaign, or zero when the campaign holds none.</summary>
    private int FirstMissionId()
    {
        foreach (MissionDescriptor mission in Chain())
        {
            return mission.Id;
        }

        return 0;
    }

    /// <summary>Gets whether a mission may be played: on or before the frontier, or every mission when the unlock-all cheat is on.</summary>
    /// <remarks>A mission that is on no campaign of this title may not be played from this screen.</remarks>
    public bool IsUnlocked(int missionId)
    {
        if (PlayerCheatCVars.UnlockAllLevels.Value)
        {
            return true;
        }

        int wanted = IndexOf(missionId);
        int frontier = IndexOf(ReachedMission());
        return wanted >= 0 && frontier >= 0 && wanted <= frontier;
    }

    /// <summary>Gets a profile's furthest mission at its own difficulty, or the first mission when it has reached none.</summary>
    private int ActiveMissionOf(Igi2PlayerProfile? profile)
    {
        int reached = profile is null || (uint)profile.Difficulty >= (uint)profile.ActiveMission.Length
            ? 0
            : profile.ActiveMission[profile.Difficulty];
        return IndexOf(reached) >= 0 ? reached : FirstMissionId();
    }

    private Igi2PlayerProfile? ProfileAt(int index) =>
        index >= 0 && index < _config.Profiles.Count ? _config.Profiles[index] : null;
}
