namespace ProjectIGI.Game.Menus;

/// <summary>
/// The entry's per-tick behaviour: it is the type that consumes a choose.
/// </summary>
/// <remarks>
/// <para>
/// Transcribed from <c>MenuText_Update</c> (<c>0x4207E0</c>). A completed pointer click and an
/// <c>Enter</c> posted by the manager are the same event here — <c>if (wasClicked || pending == -7)</c>
/// — which is why the keyboard and the mouse cannot disagree about what "choosing" means.
/// </para>
/// <para>
/// This is also the one control that <b>clears</b> its pending command, and it clears it whether or
/// not it acted. A slider and a switch leave theirs set and rely on their own guards instead.
/// </para>
/// <para>
/// The enabled and visible expressions are evaluated the same way every other control evaluates its
/// data expressions — through <see cref="MenuWindowTask"/>'s float walk, with <c>this</c> bound — and
/// mirrored from I.G.I. 2's own <c>Igi2MenuTextTask.IsVisible(level)</c> /
/// <c>IsEnabled(level)</c>, which evaluate theirs through <c>Igi2MenuData.TryRun</c> and likewise
/// treat a missing expression as always true. An entry with no expression keeps its default, so a
/// script that never declares them — Project I.G.I.'s <c>MenuText</c> — is unaffected.
/// </para>
/// </remarks>
public partial class MenuTextTask
{
    /// <summary>Gets whether the entry is drawn, from its visible expression; true when there is none.</summary>
    /// <remarks>
    /// Evaluated every tick and on load. The renderer gates on this, so an entry the script hides —
    /// I.G.I. 2's screen-900 Read Me / Credits links carry <c>isVisible "0"</c> — draws nothing.
    /// </remarks>
    public bool IsVisible { get; private set; } = true;

    /// <summary>Gets whether the entry can be chosen, from its enabled expression; true when there is none.</summary>
    /// <remarks>
    /// The version text carries <c>isEnabled "0"</c> but <c>isVisible "1"</c>: it still draws, and
    /// only its click — which it has none of — would be refused. There is no dimming here: that needs
    /// the script's <c>Is Item Dimmed When Disabled</c> flag, which this class does not carry.
    /// </remarks>
    public bool IsEnabled { get; private set; } = true;

    /// <inheritdoc/>
    protected override void OnUpdate()
    {
        EvaluateVisibility();
        ApplyPendingCommand();
        base.OnUpdate();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The state is read from the first tick, so it is evaluated here too: <c>LoadData</c> runs as a
    /// screen is entered, before the tree's first update.
    /// </remarks>
    protected override void OnLoadData() => EvaluateVisibility();

    /// <inheritdoc/>
    /// <remarks>
    /// A hidden entry takes no pointer: it cannot be hovered onto the selection or clicked. The
    /// hit-test runs before the tree's update on a tick, so a hidden entry is still hit-testable for
    /// the one tick its expression flips on — it is already invisible by then, and a click needs a
    /// press and a release across two ticks.
    /// </remarks>
    protected override bool TakesPointer => IsInteractive && IsVisible;

    /// <summary>Runs the entry's action if it was chosen, then clears the pending command.</summary>
    /// <remarks>The body of <c>MenuText_Update</c>, without the base chain.</remarks>
    public void ApplyPendingCommand()
    {
        // A hidden or disabled entry never fires, even if the keys land on it: the selection still
        // collects it (it is gathered before the first evaluation), so the guard has to be here
        // rather than in the selection. The pending command is still cleared, as the original does.
        bool chosen = (WasClicked || PendingCommand == MenuWindowCommand.Choose) && IsVisible && IsEnabled;

        if (chosen && OnClickScript.Length > 0)
        {
            // Script first, then the sound: the original runs the expression and only then decides
            // between the entry's own sound name and menu_sel.
            Run(OnClickScript);
            Manager?.PlaySound(
                ClickSoundResource.Length > 0 ? ClickSoundResource : MenuManagerTask.ChooseSoundName);
        }

        // Cleared unconditionally, at the end, before the base chain — exactly where the original
        // puts it.
        PendingCommand = MenuWindowCommand.None;
    }

    // Reads the two expressions through the manager, with `this` bound. An empty expression is
    // true; a missing manager — before the menu is initialised, when the tree is never ticked —
    // leaves the defaults alone rather than reading a zero out of an evaluator that does not exist.
    private void EvaluateVisibility()
    {
        if (Manager is null)
        {
            return;
        }

        IsVisible = IsVisibleExpression.Length == 0 || Evaluate(IsVisibleExpression) != 0.0;
        IsEnabled = IsEnabledExpression.Length == 0 || Evaluate(IsEnabledExpression) != 0.0;
    }
}
