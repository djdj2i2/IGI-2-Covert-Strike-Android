using ProjectIGI.Core.Graphics;

namespace ProjectIGI.Rendering;

/// <summary>The colours a crosshair may be drawn in, as chosen by <see cref="HudCVars.CrosshairColor"/>.</summary>
/// <remarks>
/// Index 0 is the sight's own colour, so it resolves to nothing here and each sight keeps
/// what it already draws. The rest are opaque, vivid colours that read against dark ground
/// and bright sky alike; the names are what the settings screen shows for the choice.
/// </remarks>
public static class CrosshairPalette
{
    /// <summary>The name shown for each colour index, in index order.</summary>
    public static readonly string[] Names = ["Default", "White", "Yellow", "Green", "Cyan", "Red"];

    /// <summary>Gets the colour for an index, or nothing for the sight's own colour.</summary>
    /// <param name="index">The value of <see cref="HudCVars.CrosshairColor"/>.</param>
    /// <returns>The colour, or <see langword="null"/> where the sight keeps its own.</returns>
    public static Bgra32? Color(int index) => index switch
    {
        1 => Bgra32.FromRgb(255, 255, 255),
        2 => Bgra32.FromRgb(255, 235, 0),
        3 => Bgra32.FromRgb(0, 255, 120),
        4 => Bgra32.FromRgb(0, 225, 255),
        5 => Bgra32.FromRgb(255, 60, 60),
        _ => null,
    };
}
