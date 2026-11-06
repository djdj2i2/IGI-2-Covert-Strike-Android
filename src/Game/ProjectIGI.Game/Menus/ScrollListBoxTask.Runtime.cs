
namespace ProjectIGI.Game.Menus;

/// <summary>
/// The vertical list: several entries visible at once, with the selection moving through them.
/// </summary>
/// <remarks>
/// <para>
/// Shares its item model with <see cref="ListBoxTask"/> — both are filled by a
/// <c>Collect Items Script</c> and both report the selected entry's id as <c>nActiveID</c> — but this
/// one shows the whole list and scrolls, where a list box shows one entry between two arrows.
/// </para>
/// <para>
/// Behaviour transcribed from <c>ScrollListBox_Update</c> (<c>0x41BDD0</c>). Two things there are easy
/// to miss and are what make the control usable: <b>clicking a row selects that row</b> rather than
/// activating it, and clicking the row that is <em>already</em> selected a second time within half the
/// double-click interval is what runs its action. That is how the original starts a mission from the
/// Select Mission screen.
/// </para>
/// <para>
/// A third, touch-only: the control draws no scrollbar — its <c>ScrollBar</c> belongs to the text
/// boxes, not to this list — so on a phone the list scrolls by dragging it. A press that started
/// inside keeps scrolling while the button is held, whole rows at a time; the selection stays
/// where the press put it, the way a scrollbar moves the view and not the row.
/// </para>
/// </remarks>
public partial class ScrollListBoxTask
{
    /// <summary>The row height used until the renderer reports the font's own.</summary>
    /// <remarks>
    /// The original reads a row height stored on the control (<c>+5104</c>) that its construction fills
    /// in from the font. Here the renderer owns the font, so it publishes <see cref="RowHeight"/> as it
    /// draws and this is what the first tick uses.
    /// </remarks>
    public const int DefaultRowHeight = 12;

    /// <summary>
    /// How many ticks after a click on the selected row a second click still counts as a double click.
    /// </summary>
    /// <remarks>
    /// The original compares against <em>half</em> the system double-click time (<c>0x402870</c>), which
    /// is 250 ms with the Windows default. At the menu's 30 Hz that is eight ticks.
    /// </remarks>
    public const int DoubleClickTicks = 8;

    private readonly List<MenuListItem> _items = [];
    private int _ticksSinceClick = int.MaxValue;
    private int _clickedRow = -1;

    /// <summary>Whether a press inside the list is holding a drag, rather than a tap.</summary>
    /// <remarks>Latched by the press, not by the position: the finger may leave the rectangle mid-drag.</remarks>
    private bool _dragging;

    /// <summary>The pointer's Y when the drag last moved a whole row, in authored pixels.</summary>
    /// <remarks>Only whole rows are consumed, so a slow drag accumulates the remainder.</remarks>
    private int _dragAnchorY;

    /// <summary>Gets the items currently in the list.</summary>
    public IReadOnlyList<MenuListItem> Items => _items;

    /// <summary>Gets or sets the selected row, as an absolute index into <see cref="Items"/>.</summary>
    public int Index { get; set; }

    /// <summary>Gets or sets the first visible row.</summary>
    /// <remarks>The original keeps this on its scroll bar; the value it reads back is the same thing.</remarks>
    public int ScrollOffset { get; set; }

    /// <summary>Gets or sets the height of one row in pixels.</summary>
    /// <remarks>Published by the renderer, which knows the font the box draws with.</remarks>
    public int RowHeight { get; set; } = DefaultRowHeight;

    /// <summary>Gets how many rows fit inside the box.</summary>
    /// <remarks>The original's <c>(height - 4) / rowHeight</c>, so the border is not counted.</remarks>
    public int VisibleRows => Math.Max(1, (LayoutHeight - 4) / Math.Max(1, RowHeight));

    /// <summary>Replaces the list's contents.</summary>
    /// <param name="items">The items, in order.</param>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> is <see langword="null"/>.</exception>
    public void SetItems(IEnumerable<MenuListItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        _items.Clear();
        _items.AddRange(items);
        Index = Math.Clamp(Index, 0, Math.Max(0, _items.Count - 1));
        KeepSelectionVisible();

        if (Index < _items.Count)
        {
            ActiveId = _items[Index].Id;
        }
    }

    /// <summary>Selects a row by its id, scrolling it into view.</summary>
    /// <param name="id">The id to look for. An id the list does not hold leaves the selection alone.</param>
    /// <returns><see langword="true"/> when the id was found.</returns>
    public bool SelectId(int id)
    {
        int row = _items.FindIndex(item => item.Id == id);
        if (row < 0)
        {
            return false;
        }

        Index = row;
        ActiveId = id;
        KeepSelectionVisible();
        return true;
    }

    /// <inheritdoc/>
    protected override void OnLoadData()
    {
        Run(CollectItemsScript);

        if (GetDataScript.Length > 0)
        {
            // The script names the id to sit on, so the row has to be looked up rather than assumed:
            // the mission list opens on whichever mission the profile reached, not on the first row.
            int id = (int)Evaluate(GetDataScript);
            if (!SelectId(id))
            {
                ActiveId = id;
            }
        }
    }

    /// <inheritdoc/>
    protected override void OnSaveData() => Run(SetDataScript);

    /// <summary>Advances the double-click timer. Called from the control's update.</summary>
    private void TickClickTimer()
    {
        if (_ticksSinceClick < int.MaxValue)
        {
            _ticksSinceClick++;
        }
    }

    /// <inheritdoc/>
    protected override void OnPointer(MenuPointer pointer, bool inside)
    {
        ArgumentNullException.ThrowIfNull(pointer);

        // A drag started inside the list keeps scrolling it while the button is held, even after
        // the finger leaves the rectangle. Whole rows only, the remainder kept so a slow drag
        // still accumulates; the selection stays where the press put it.
        if (_dragging)
        {
            if (!pointer.IsDown)
            {
                _dragging = false;
            }
            else
            {
                int rowHeight = Math.Max(1, RowHeight);
                int rows = (pointer.Y - _dragAnchorY) / rowHeight;
                if (rows != 0)
                {
                    ScrollOffset = Math.Clamp(ScrollOffset + rows, 0, Math.Max(0, _items.Count - 1));
                    _dragAnchorY += rows * rowHeight;

                    // A scroll is not the first half of a double click: without this, dragging
                    // off the selected row and tapping it again inside the double-click window
                    // would run its action.
                    _ticksSinceClick = int.MaxValue;
                    _clickedRow = -1;
                }
            }

            return;
        }

        if (!inside || !pointer.Pressed || _items.Count == 0)
        {
            return;
        }

        _dragging = true;
        _dragAnchorY = pointer.Y;

        int row = ScrollOffset + ((pointer.Y - LayoutY) / Math.Max(1, RowHeight));

        // Clicking the already-selected row, or past the last one, does not move the selection: it is
        // the first half of a double click, and a second one runs On Select Item.
        if (row == Index || row >= _items.Count)
        {
            if (_clickedRow == Index && _ticksSinceClick <= DoubleClickTicks)
            {
                _ticksSinceClick = int.MaxValue;
                Run(OnSelectItem);
            }
            else
            {
                _ticksSinceClick = 0;
                _clickedRow = Index;
            }

            return;
        }

        Index = row;
        ActiveId = _items[Index].Id;
        _ticksSinceClick = 0;
        _clickedRow = Index;
        KeepSelectionVisible();

        // Nothing else has to be told: the controls keyed off this list re-evaluate their own
        // expression every update, which is why the original can move a row and reload nothing.
        Manager?.PlaySound(MenuManagerTask.MoveSoundName);
    }

    /// <summary>Keeps the selected row inside the visible window, the way the original nudges its scroll bar.</summary>
    private void KeepSelectionVisible()
    {
        int rows = VisibleRows;
        if (Index < ScrollOffset)
        {
            ScrollOffset = Index;
        }
        else if (Index >= ScrollOffset + rows)
        {
            ScrollOffset = Index - rows + 1;
        }

        ScrollOffset = Math.Clamp(ScrollOffset, 0, Math.Max(0, _items.Count - 1));
    }
}
