using ProjectIGI.Engine.Tasks;
using ProjectIGI.Game.Igi2.Menus;
using ProjectIGI.Game.Menus;

namespace ProjectIGI.Presentation.Igi2.Menus;

/// <summary>
/// Swaps the loop's menu task types between this title's classes and the shared menu classes
/// around the retail menu's load.
/// </summary>
/// <remarks>
/// <para>
/// The title's content modules register its own menu classes under the shared names
/// (<c>Igi2MenuManagerTask</c> as <c>"MenuManager"</c>, and so on), and
/// <c>MenuSystem.Load</c> registers the shared classes under those same names. Neither side can load after the other without the swap:
/// the registry throws on a name already held by another class, and skipping the registration
/// would leave the script spawning this title's classes, which the shared menu host cannot draw
/// (nor would <c>MenuSystem</c> find its <c>MenuManagerTask</c>).
/// </para>
/// <para>
/// So the presenter swaps the colliding names to the shared classes, loads the script, and swaps
/// them back: the instances the script built keep the shared classes, and every later spawn --
/// the in-game menu a level makes, whose <c>Load</c> looks its manager up as an
/// <c>Igi2MenuManagerTask</c> -- resolves to this title's classes again.
/// </para>
/// <para>
/// The swap is by name, through <see cref="ByNameParameterAdapter"/>: I.G.I. 2 declares several
/// of these types with parameters the shared classes do not carry (its manager's default-screen
/// and demo expressions, its screens' logo, timeout and justification), and a positional
/// registration would fail the declaration check outright for the manager, the screen and the
/// text. Set by name, every value the shared class declares is set from the script's values and
/// the rest is dropped; the declaration check is skipped, as it is for a level's own classes.
/// </para>
/// </remarks>
internal static class Igi2MenuTypeSwap
{
    private static readonly (string Name, Func<GameTask> Shared, Func<GameTask> Igi2, bool Igi2ByName)[] Swapped =
    [
        (Igi2MenuManagerTask.TypeName, static () => new MenuManagerTask(), static () => new Igi2MenuManagerTask(), false),
        (Igi2MenuScreenTask.TypeName, static () => new MenuScreenTask(), static () => new Igi2MenuScreenTask(), true),
        (Igi2MenuFrameTask.TypeName, static () => new MenuFrameTask(), static () => new Igi2MenuFrameTask(), true),
        (Igi2MenuTextTask.TypeName, static () => new MenuTextTask(), static () => new Igi2MenuTextTask(), true),
        (Igi2SlideBarTask.TypeName, static () => new SlideBarTask(), static () => new Igi2SlideBarTask(), true),
        (Igi2ListBoxTask.TypeName, static () => new ListBoxTask(), static () => new Igi2ListBoxTask(), true),
        (Igi2ToggleBoxTask.TypeName, static () => new ToggleBoxTask(), static () => new Igi2ToggleBoxTask(), true),
        (Igi2InputBoxTask.TypeName, static () => new InputBoxTask(), static () => new Igi2InputBoxTask(), true),
        (Igi2ScrollListBoxTask.TypeName, static () => new ScrollListBoxTask(), static () => new Igi2ScrollListBoxTask(), true),
        (Igi2PictureBoxTask.TypeName, static () => new PictureBoxTask(), static () => new Igi2PictureBoxTask(), true),
        (Igi2DialogWindowTask.TypeName, static () => new DialogWindowTask(), static () => new Igi2DialogWindowTask(), true),
        (Igi2ControlsMenuTask.TypeName, static () => new ControlsMenuTask(), static () => new Igi2ControlsMenuTask(), false),
    ];

    /// <summary>Swaps the colliding menu names to the shared classes, for the menu script's load.</summary>
    /// <param name="types">The loop's registry.</param>
    /// <returns>The names that were swapped, to hand back when restoring.</returns>
    public static IReadOnlyList<string> SwapToShared(TaskTypeRegistry types)
    {
        ArgumentNullException.ThrowIfNull(types);

        List<string> swapped = [];
        foreach ((string name, Func<GameTask> shared, _, _) in Swapped)
        {
            // A name this title never registered, or one only level data declared, is left alone:
            // the load registers the shared class under it fresh.
            if (!types.TryGet(name, out TaskType? existing) || existing.IsDeclarationOnly)
            {
                continue;
            }

            types.Replace(name, shared, ByNameParameterAdapter.Instance);
            swapped.Add(name);
        }

        return swapped;
    }

    /// <summary>Swaps the names back to this title's classes, after the menu script has loaded.</summary>
    /// <param name="types">The loop's registry.</param>
    /// <param name="swapped">The names <see cref="SwapToShared"/> swapped.</param>
    /// <remarks>
    /// Only the names the load swapped are restored, and each is restored exactly as its content
    /// module registered it: the ten set by name get the adapter back, and the manager and the
    /// controls menu get a plain replacement, which re-reads their compiled parameters. The
    /// manager's compiled list is the menu script's declared list, so that check passes; the
    /// controls menu was never declared by the script, so there is nothing to check against.
    /// </remarks>
    public static void SwapToIgi2(TaskTypeRegistry types, IReadOnlyList<string> swapped)
    {
        ArgumentNullException.ThrowIfNull(types);
        ArgumentNullException.ThrowIfNull(swapped);

        HashSet<string> restore = [.. swapped];
        foreach ((string name, _, Func<GameTask> igi2, bool byName) in Swapped)
        {
            if (!restore.Contains(name))
            {
                continue;
            }

            if (byName)
            {
                types.Replace(name, igi2, ByNameParameterAdapter.Instance);
            }
            else
            {
                types.Replace(name, igi2);
            }
        }
    }
}
