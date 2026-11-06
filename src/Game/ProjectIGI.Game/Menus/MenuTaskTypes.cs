using ProjectIGI.Engine.Tasks;
using ProjectIGI.Scripting.Runtime;

namespace ProjectIGI.Game.Menus;

/// <summary>
/// Registers the menu task types and the constants their scripts name.
/// </summary>
/// <remarks>
/// <para>
/// The original spreads this over fifteen registrars — <c>MenuManager</c> at <c>0x4170A0</c>,
/// <c>MenuScreen</c> at <c>0x421750</c>, <c>MenuText</c> at <c>0x420CC0</c> and so on. Each declares
/// its class, binds its methods and builds a property block; only the property blocks matter to a
/// reimplementation, and those are declared by the script rather than the engine.
/// </para>
/// <para>
/// <b>Parameter order is wire order.</b> Every list here was transcribed from the
/// <c>Task_DeclareParameters</c> calls in retail <c>mainmenu.qvm</c>, which is the authority on what
/// the data contains: <c>Task_New</c> parses positionally, so one wrong entry shifts every field after
/// it and silently swallows the following child tasks. <c>MenuTaskRegistrationTests</c> checks the
/// lists against the script itself rather than against this file.
/// </para>
/// </remarks>
public static class MenuTaskTypes
{
    /// <summary>The colour indices <c>MenuText</c> registers, and the values a script's <c>ColourIndex</c> takes.</summary>
    /// <remarks>Registered by the original inside the <c>MenuText</c> registrar (<c>0x420CC0</c>).</remarks>
    public static IReadOnlyList<(string Name, int Value)> TextColours { get; } =
    [
        ("TEXT_BLACK", 0),
        ("TEXT_WHITE", 1),
        ("TEXT_GREEN", 2),
        ("TEXT_ORANGE", 3),
    ];

    /// <summary>Registers every menu task type.</summary>
    /// <param name="types">The registry to add to.</param>
    /// <param name="parent">The base type the menu types derive from, if the host has one.</param>
    /// <exception cref="ArgumentNullException"><paramref name="types"/> is <see langword="null"/>.</exception>
    public static void RegisterAll(TaskTypeRegistry types, TaskType? parent = null)
    {
        ArgumentNullException.ThrowIfNull(types);

        types.RegisterOrKeep<MenuManagerTask>("MenuManager", parent);
        types.RegisterOrKeep<MenuFrameTask>("MenuFrame", parent);
        types.RegisterOrKeep<MenuScreenTask>("MenuScreen", parent);
        types.RegisterOrKeep<MenuTextTask>("MenuText", parent);
        types.RegisterOrKeep<MenuTextConditionalTask>("MenuTextConditional", parent);
        types.RegisterOrKeep<SlideBarTask>("SlideBar", parent);
        types.RegisterOrKeep<ListBoxTask>("ListBox", parent);
        types.RegisterOrKeep<ToggleBoxTask>("ToggleBox", parent);
        types.RegisterOrKeep<InputBoxTask>("InputBox", parent);
        types.RegisterOrKeep<AsciiBoxTask>("AsciiBox", parent);
        types.RegisterOrKeep<ScrollListBoxTask>("ScrollListBox", parent);
        types.RegisterOrKeep<PictureBoxTask>("PictureBox", parent);
        types.RegisterOrKeep<TextBoxTask>("TextBox", parent);
        types.RegisterOrKeep<TypeWriterBoxTask>("TypeWriterBox", parent);
        types.RegisterOrKeep<DialogWindowTask>("DialogWindow", parent);

        // Spawned without a declaration because it takes no parameters, like the structural types.
        types.RegisterOrKeep<ControlsMenuTask>("ControlsMenu", parent);

        // Two more the engine owns rather than the script: no menu file names either, but a SlideBar
        // builds a pair of MenuButtons in its constructor and they have to resolve to a type.
        types.RegisterOrKeep<MenuButtonTask>("MenuButton", parent);
    }

    /// <summary>Registers the constants a menu script names.</summary>
    /// <param name="natives">The script namespace to add to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="natives"/> is <see langword="null"/>.</exception>
    public static void RegisterConstants(NativeRegistry natives)
    {
        ArgumentNullException.ThrowIfNull(natives);

        foreach ((string name, int value) in TextColours)
        {
            natives.RegisterConstant(name, value);
        }
    }
}
