using ProjectIGI.Core.Diagnostics;
using ProjectIGI.Engine.Titles;
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
    private readonly Func<IMissionCatalogue<MissionDescriptor>?> _missions;
    private readonly Func<MenuStringTable?> _strings;
    private readonly Igi2SaveSession? _saves;
    private readonly IProjectIGILogger _logger;

    /// <summary>Initializes a new instance of the <see cref="Igi2ConfigNatives"/> class.</summary>
    /// <param name="config">The player profiles the settings screens read and write.</param>
    /// <param name="missions">The title's missions, which the mission screens list, or <see langword="null"/> before they are scanned.</param>
    /// <param name="strings">The language's text, or <see langword="null"/> before it is loaded.</param>
    /// <param name="saves">The session's save games, or <see langword="null"/> when the menu has none to count.</param>
    /// <param name="logger">Where the mission fill is reported.</param>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    public Igi2ConfigNatives(
        Igi2GameConfig config,
        Func<IMissionCatalogue<MissionDescriptor>?> missions,
        Func<MenuStringTable?> strings,
        Igi2SaveSession? saves = null,
        IProjectIGILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(missions);
        ArgumentNullException.ThrowIfNull(strings);

        _config = config;
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
                result = QvmValue.FromInt32(ActiveMissionOf(ProfileAt(arguments.GetInt32(0))));
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

    /// <summary>Walks the campaign, one entry per mission, stopping after the one the profile has reached.</summary>
    /// <remarks>
    /// Project I.G.I.'s <c>Config_FillMissionSelectionBox</c> (<c>0x405660</c>) walks the mission chain
    /// from the first id, adding each mission's name keyed by its id, and stops after the one the
    /// profile has reached — which is what keeps a fresh profile from starting mission 14. The same
    /// stop here, over <see cref="IMissionCatalogue{MissionDescriptor}.InProgressionOrder"/>: a
    /// profile that has reached nothing (active mission zero) lists the whole campaign, since no
    /// mission carries id zero.
    /// </remarks>
    private IEnumerable<MenuListItem> GetMissions(int reached)
    {
        bool unlockAll = PlayerCheatCVars.UnlockAllLevels.Value;
        foreach (MissionDescriptor mission in Chain())
        {
            yield return new MenuListItem(mission.Id, Localise(mission.Name));
            if (!unlockAll && mission.Id == reached)
            {
                yield break;
            }
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

    /// <summary>Gets the furthest mission the active profile has reached at its own difficulty.</summary>
    private int ReachedMission() => ActiveMissionOf(_config.Active);

    /// <summary>Gets a profile's furthest mission at its own difficulty, or zero when there is none.</summary>
    private static int ActiveMissionOf(Igi2PlayerProfile? profile) =>
        profile is null || (uint)profile.Difficulty >= (uint)profile.ActiveMission.Length
            ? 0
            : profile.ActiveMission[profile.Difficulty];

    private Igi2PlayerProfile? ProfileAt(int index) =>
        index >= 0 && index < _config.Profiles.Count ? _config.Profiles[index] : null;
}
