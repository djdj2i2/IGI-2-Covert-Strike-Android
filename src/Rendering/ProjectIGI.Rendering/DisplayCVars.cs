using ProjectIGI.Core.Configuration;

namespace ProjectIGI.Rendering;

/// <summary>How the window sits on the screen, as the settings name it.</summary>
public enum DisplayWindowMode
{
    /// <summary>A decorated window at the chosen resolution.</summary>
    Windowed,

    /// <summary>An undecorated window over the whole desktop, at the desktop's resolution.</summary>
    Borderless,

    /// <summary>The monitor taken over and switched to the chosen resolution.</summary>
    Fullscreen,
}

/// <summary>The display's console variables: which API draws, and the window it draws into.</summary>
/// <remarks>
/// <para>
/// Applied by the host -- to the window, and through the composition root's choice of device -- and declared
/// here, beside the renderer's own (<see cref="FrameCVars"/> and its siblings), so every layer that shows or
/// reads them has them registered.
/// </para>
/// <para>
/// The command line wins over all three for its run, without writing them: <c>ProjectIGI_RENDERER</c> over
/// <see cref="Renderer"/>, and <c>-window</c>, <c>-borderless</c>, <c>-fullscreen</c> and <c>-size</c> over
/// <see cref="Window"/> and <see cref="Resolution"/>.
/// </para>
/// </remarks>
[CVarContainer]
public static class DisplayCVars
{
    /// <summary>The graphics API the game draws over.</summary>
    /// <remarks>
    /// Read by the composition root as the game starts. Changed in a running game, it rebuilds the game over
    /// that API and puts the player back where they were, as <c>r_switch_renderer</c> does.
    /// </remarks>
    public static readonly EnumCVar<RendererBackend> Renderer = CVar.Choice(
        "r_renderer",
        RendererBackend.OpenGL,
        CVarOptions.Archive,
        "Graphics API: opengl (default), vulkan, or webgl. "
            + "Changing it rebuilds the renderer in place.");

    /// <summary>How the window sits on the screen.</summary>
    public static readonly EnumCVar<DisplayWindowMode> Window = CVar.Choice(
        "r_window_mode",
        DisplayWindowMode.Borderless,
        CVarOptions.Archive,
        "Window mode: windowed, borderless (covers the desktop at its resolution), or fullscreen (switches the monitor).");

    /// <summary>The window's size when windowed, or the monitor's mode when fullscreen, as <c>WIDTHxHEIGHT</c>.</summary>
    /// <remarks>
    /// Empty for the desktop's own. Borderless always covers the desktop and ignores it. Spelled as
    /// <see cref="ResolutionText"/> spells it.
    /// </remarks>
    public static readonly StringCVar Resolution = CVar.Text(
        "r_resolution",
        string.Empty,
        CVarOptions.Archive,
        "Resolution as WIDTHxHEIGHT, e.g. 1920x1080, or empty for the desktop's. Ignored while borderless.");

    /// <summary>Display a small FPS counter overlay at the top left corner of the screen.</summary>
    public static readonly BoolCVar ShowFps = CVar.Bool(
        "r_show_fps",
        false,
        CVarOptions.Archive,
        "Display a small FPS counter text overlay at the top left corner of the screen.");

    /// <summary>How large the shell's interface draws, as a multiplier on the authored sizes.</summary>
    /// <remarks>
    /// The Configuration screen's "UI Size" slider. Read at startup and on change by the host, which
    /// hands it to the layout as its user scale; the shell is authored at a 1920x1080 reference, so on
    /// a phone it renders small without it.
    /// </remarks>
    public static readonly NumericCVar<float> UiScale = CVar.Float(
        "r_ui_scale",
        1.2f,
        0.8f,
        2.0f,
        CVarOptions.Archive,
        "Shell interface size as a multiplier: 1.2 is the default that keeps the Configuration screen readable on a phone.");

    /// <summary>How large the shell's text draws, as a multiplier on the text sizes.</summary>
    /// <remarks>
    /// The Configuration screen's "Text Size" slider. The row widgets multiply the size they resolve
    /// by this as they draw, so it takes effect live; the shell's geometry is untouched.
    /// </remarks>
    public static readonly NumericCVar<float> TextScale = CVar.Float(
        "r_text_scale",
        1.0f,
        0.8f,
        1.5f,
        CVarOptions.Archive,
        "Shell text size as a multiplier on the row captions and values.");
}
