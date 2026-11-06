namespace ProjectIGI.Game.Menus;

/// <summary>The mission details a stats screen shows, from the menu's configuration.</summary>
/// <remarks>
/// <para>
/// The stats screen's task carries no data of its own: the id it shows comes from
/// <c>MenuStatsScreen_SetActiveMissionID</c>, which records it on the configuration, and the mission
/// itself lives in the title's catalogue, which only the configuration can reach. So the renderer
/// asks the configuration — through this, rather than naming a title's class — and a configuration
/// that does not answer it (Project I.G.I.'s has no stats screen) leaves the box empty.
/// </para>
/// <para>
/// The name and description come back already localised: a descriptor's two strings are language
/// keys, not text, and the configuration resolves them through the menu's string table.
/// </para>
/// </remarks>
public interface IMenuMissionStats
{
    /// <summary>Gets the name and description of the mission the stats screen is showing.</summary>
    /// <param name="name">Receives the mission's localised name.</param>
    /// <param name="description">Receives the mission's localised description.</param>
    /// <returns><see langword="true"/> when there is a mission to show.</returns>
    bool TryGetActiveMissionStats(out string name, out string description);
}
