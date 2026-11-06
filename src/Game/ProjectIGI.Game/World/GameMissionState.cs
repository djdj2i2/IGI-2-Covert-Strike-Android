using ProjectIGI.Scripting.Expressions;
using ProjectIGI.Scripting.Runtime;

namespace ProjectIGI.Game.World;

/// <summary>The mission the game will start when it next leaves the menus.</summary>
/// <remarks>
/// <para>
/// The original keeps this in a single global (<c>0x539560</c>). <c>Game_SetMission</c>
/// (<c>0x415890</c>) writes the id into it and does nothing else; <c>Mission_Load</c> reads the same
/// global back as the very first thing it does. The Select Mission screen's OK button is exactly this
/// pair: <c>Game_SetMission(this.nActiveID), MenuManager_LeaveMenus(-1, MENUMANAGER_STARTGAME)</c>.
/// </para>
/// <para>
/// Modelled as an object rather than a static so a test can drive two games without them sharing state.
/// </para>
/// </remarks>
public sealed class GameMissionState
{
    /// <summary>The native the menus call to choose a mission.</summary>
    public const string SetMissionNative = "Game_SetMission";

    /// <summary>The native the menus call to record the game mode.</summary>
    public const string SetGameModeNative = "Game_SetGameMode";

    /// <summary>Gets the mission id chosen, or zero when none has been.</summary>
    public int PendingMissionId { get; private set; }

    /// <summary>Gets the game mode chosen, or zero — single player — when none has been.</summary>
    /// <remarks>
    /// <c>GAME_GAMEMODE_SINGLEPLAYER</c> is 0 and <c>GAME_GAMEMODE_MULTIPLAYER</c> 1. The mission
    /// screen records it before it starts a mission; nothing reads it yet — v1 is single-player only —
    /// but the mode is the mission's, so it lives beside the pending id rather than in the menu.
    /// </remarks>
    public int PendingGameMode { get; private set; }

    /// <summary>Serves <c>Game_SetMission</c> and <c>Game_SetGameMode</c> to the menu's expression evaluator.</summary>
    /// <param name="name">The native being called.</param>
    /// <param name="arguments">Its arguments.</param>
    /// <returns>The result, or <see langword="null"/> when the name is not one of these.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="arguments"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// A menu expression is evaluated by the menu's own evaluator rather than by the script VM, so the
    /// registration below does not reach it and the menus need this second door.
    /// </remarks>
    public QvmValue? TryInvokeFromMenu(string name, ScriptArguments arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (string.Equals(name, SetMissionNative, StringComparison.Ordinal))
        {
            PendingMissionId = arguments.GetInt32(0);
            return QvmValue.FromInt32(1);
        }

        if (string.Equals(name, SetGameModeNative, StringComparison.Ordinal))
        {
            PendingGameMode = arguments.GetInt32(0);
            return QvmValue.FromInt32(1);
        }

        return null;
    }

    /// <summary>Registers <c>Game_SetMission</c> and <c>Game_SetGameMode</c>.</summary>
    /// <param name="natives">The script namespace the menu scripts resolve against.</param>
    /// <exception cref="ArgumentNullException"><paramref name="natives"/> is <see langword="null"/>.</exception>
    public void Register(NativeRegistry natives)
    {
        ArgumentNullException.ThrowIfNull(natives);

        natives.RegisterFunction(
            SetMissionNative,
            call =>
            {
                PendingMissionId = call.GetInt32(0);
                return QvmValue.FromInt32(1);
            },
            1);
        natives.RegisterFunction(
            SetGameModeNative,
            call =>
            {
                PendingGameMode = call.GetInt32(0);
                return QvmValue.FromInt32(1);
            },
            1);
    }

    /// <inheritdoc/>
    public override string ToString() => $"pending mission {PendingMissionId}";
}
