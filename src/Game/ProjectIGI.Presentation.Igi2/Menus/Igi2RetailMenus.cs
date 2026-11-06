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
        MenuSystem menu = MenuSystem.Load(
            request.FileSystem.ReadAllBytes("LOCAL:menusystem/mainmenu.qvm"),
            request.Loop,
            request.Logger);
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
            ["LOCAL:menusystem/missionsprites.res"],
            request.Input,
            request.Batch,
            request.ScreenWidth,
            request.ScreenHeight,
            request.Scale,
            request.Sounds,
            // Leaving with StartGame starts the chosen mission; leaving any other way quits.
            leaving => leave(leaving == MenuManagerState.StartGame),
            request.Logger));

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
