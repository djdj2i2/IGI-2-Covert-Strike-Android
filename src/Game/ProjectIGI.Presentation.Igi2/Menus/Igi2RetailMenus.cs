using ProjectIGI.Game.Menus;
using ProjectIGI.Presentation.Igi1.Menus;

namespace ProjectIGI.Presentation.Igi2.Menus;

/// <summary>I.G.I. 2's retail menus: <c>mainmenu.qvm</c> run in the loop and drawn by the menu renderer.</summary>
/// <remarks>
/// The original's <c>MenuSystem_Load(root, "LOCAL:menusystem\mainmenu.qsc", 1, 1, 1)</c>. The mission briefing images are
/// in their own archive beside the menu's. The manager runs under I.G.I. 2's numbering — the script's
/// <c>MENUMANAGER_STARTGAME</c> is 14, not Project I.G.I.'s 13 — so it takes the Igi2 dialect before it is initialised.
/// </remarks>
internal sealed class Igi2RetailMenus : IRetailMenus
{
    private readonly RetailMenuHost _shell;
    private readonly MenuSystem _menu;

    /// <summary>Initializes a new instance of the <see cref="Igi2RetailMenus"/> class, and starts the menu script.</summary>
    /// <param name="request">What the menus are started with.</param>
    public Igi2RetailMenus(in RetailMenusRequest request)
    {
        // The loop's registry already holds this title's menu classes under the shared names
        // ("MenuManager" is Igi2MenuManagerTask, and so on), and MenuSystem.Load registers the
        // shared classes under those same names -- without the swap it throws on the first of
        // them. Swap the colliding names to the shared classes for the load, then swap them back,
        // so the menu is built from the shared classes and later scripts still spawn this
        // title's. The load's own registration tolerates the names it just swapped (see
        // TaskTypeRegistry.RegisterOrKeep), since they already make the shared classes.
        IReadOnlyList<string> swapped = Igi2MenuTypeSwap.SwapToShared(request.Loop.Types);
        MenuSystem menu;
        try
        {
            menu = MenuSystem.Load(
                request.FileSystem.ReadAllBytes("LOCAL:menusystem/mainmenu.qvm"),
                request.Loop,
                request.Logger);
        }
        finally
        {
            Igi2MenuTypeSwap.SwapToIgi2(request.Loop.Types, swapped);
        }

        _menu = menu;

        // The script's top-level code only creates tasks, so nothing has read the navigator yet; the manager takes
        // I.G.I. 2's numbering now, before Initialize captures the navigator into the script context.
        menu.Manager.SetDialect(MenuManagerDialect.Igi2);

        Action<bool> leave = request.Leave;
        _shell = new RetailMenuHost(new RetailMenuPresentationRequest(
            request.Device,
            request.FileSystem,
            menu.Manager.ResourcePath,
            request.Strings() ?? throw new InvalidOperationException("The language's text is loaded before the menus start."),
            // The menu's text is drawn from font1/font3/font4.fnt, and I.G.I. 2 keeps its fonts in
            // common/fonts/fonts_med.res — not in the menu's own archive, as Project I.G.I.'s are.
            // Without it every MenuText resolves to a null font and draws nothing, which is exactly
            // what the first device test showed: backdrop, logo, frame and cursor, no entries.
            ["LOCAL:menusystem/missionsprites.res", "LOCAL:common/fonts/fonts_med.res"],
            request.Input,
            request.Batch,
            request.ScreenWidth,
            request.ScreenHeight,
            request.Scale,
            request.Sounds,
            // Leaving with StartGame starts the chosen mission; leaving any other way quits.
            leaving => leave(leaving == MenuManagerState.StartGame),
            request.Logger));

        // The script passes 904 as "Current menuscreen": the screen it actually opens on comes
        // from its default-screen expression, Flow_IsPlayCredits() ? 916 : 923, which the shared
        // manager never evaluates -- it starts on CurrentMenuScreen. The logo screen 923 would
        // need its 169-tick timeout to reach the main menu, and nothing in the tree implements a
        // screen timeout (nothing reads TicksToTimeout), so open directly on the main menu
        // instead. Phase 2 owns the default-screen native and the splash timeout.
        menu.Manager.CurrentMenuScreen = 900;

        // -screen is a development shortcut, not a game feature: the script decides where the menu
        // opens, and this overwrites that one parameter before the manager reads it.
        if (request.StartScreen > 0)
        {
            menu.Manager.CurrentMenuScreen = request.StartScreen;
        }

        // No configuration yet: I.G.I. 2's settings live in Igi2GameConfig/Igi2PlayerProfile, and there is no
        // IMenuConfiguration over them — the only one in the tree is Project I.G.I.'s ConfigNatives, which would read
        // the wrong profiles. Until Phase 2 wires an Igi2 one, the manager's Configuration stays unset, so config
        // expressions read zero and log a "No native named ..." warning; the main menu itself does not need them.
        menu.Manager.ExternalNatives = request.ExternalNatives;
        menu.Manager.Initialize(menu, _shell, request.Logger);
    }

    /// <inheritdoc/>
    public void CloseScreens() => _shell.CloseScreens();

    /// <inheritdoc/>
    public void RunAction(string expression) => _menu.Manager.RunAction(expression);

    /// <inheritdoc/>
    public void Dispose() => _shell.Dispose();
}
