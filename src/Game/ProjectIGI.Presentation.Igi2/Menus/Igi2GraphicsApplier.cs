using ProjectIGI.Game.Igi2.Config;
using ProjectIGI.Rendering;

namespace ProjectIGI.Presentation.Igi2.Menus;

/// <summary>Pushes I.G.I. 2's retail graphics options to the live stores, and reads them back.</summary>
/// <remarks>
/// <para>
/// The retail menu writes the options in force — the <c>GfxOptions_*</c> record — and the OK
/// button files them with the active profile. Neither of those is a live store, so applying is a
/// translation from the record to the variables the game actually reads, done here because this
/// project is the one that may name the renderer (<c>ProjectIGI.Game.Igi2</c> cannot; it holds
/// only the record and the menu natives).
/// </para>
/// <para>
/// What maps, and where it lands:
/// <list type="bullet">
/// <item>Resolution — the render-scale tier, the same four the pause screen's Resolution Quality row offers: 0.5, 2/3, 5/6, 1.0 (<c>r_scale</c>).</item>
/// <item>Texture detail — <c>r_texture_quality</c>: HIGH 0 to 2, LOW 1 to 1, ULTRA LOW 2 to 0.</item>
/// <item>Texture filtering — <c>r_anisotropy</c> and <c>r_mip_point</c>: bilinear to 1 with point mips, trilinear to 1 with blended mips, anisotropic to 8 with blended mips. The host already re-applies the samplers whenever either variable changes, so writing them is enough.</item>
/// <item>Antialiasing — <c>r_msaa</c>: DISABLED 0, X2 2, X4 4, X6 6. The value is written and archived, but nothing reads it on the Android Vanilla path: the only reader sizes the offscreen scene target, and that target is only built while <c>r_hdr</c> is on, which Vanilla never is. The option is kept and remembered; it does nothing visible here.</item>
/// <item>Terrain detail — <c>r_terrain_detail</c>: the slider's 0..1 becomes 96..163, the range retail runs for low/medium/high; 0 on the variable means "as retail derives it", which this port reads as full.</item>
/// </list>
/// </para>
/// <para>
/// What does not map, and why it is left as a record-only option: model detail is read at level
/// load, so it takes effect on the next mission, not this one; high-quality forests and lens
/// flares are read live straight out of the record, so they need no push; stencil shadows need
/// the deferred/HDR frame path Android Vanilla never builds; texture compression, wind on
/// forests, bullet hit effects and water quality have no reader anywhere; gamma is only read in
/// the Resolve pass Vanilla never runs; force software vertex processing is a retail renderer
/// choice with no equivalent here. Those stay stored in the record and the profile, and the PR
/// body says so, rather than being dressed up as working.
/// </para>
/// <para>
/// The record is seeded from the live stores when the menus start (<see
/// cref="SyncFromLive"/>): the mapped options have no writer for the retail file, so after a
/// relaunch the record would still say what <c>config.qvm</c> said while the archived variables
/// say what the player last chose — and the screen must show what the game is actually using.
/// </para>
/// </remarks>
public static class Igi2GraphicsApplier
{
    /// <summary>The render scales the resolution tiers stand for, in the pause screen's order.</summary>
    private static readonly float[] RenderScales = [0.5f, 2f / 3f, 5f / 6f, 1f];

    /// <summary>Gets or sets the resolution tier the render scale stands for, nought to three.</summary>
    public static int RenderScaleTier
    {
        get
        {
            float scale = ReconstructionCVars.RenderScale.Value;
            return scale < 0.583f ? 0 : scale < 0.75f ? 1 : scale < 0.917f ? 2 : 3;
        }

        set => ReconstructionCVars.RenderScale.Value = RenderScales[Math.Clamp(value, 0, RenderScales.Length - 1)];
    }

    /// <summary>Pushes the mapped options in force to the live stores.</summary>
    /// <param name="config">The configuration whose in-force options and active profile are pushed.</param>
    public static void Apply(Igi2GameConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        Igi2GfxOptions gfx = config.Gfx;
        MaterialCVars.TextureQuality.Value = gfx.TextureDetail switch
        {
            0 => 2,
            1 => 1,
            _ => 0,
        };

        (int anisotropy, bool pointMips) = (config.Active?.TextureFilteringMode ?? 0) switch
        {
            0 => (1, true),
            2 => (8, false),
            _ => (1, false),
        };
        MaterialCVars.Anisotropy.Value = anisotropy;
        MaterialCVars.PointMips.Value = pointMips;

        FrameCVars.MultiSampling.Value = (config.Active?.AntialiasingMode ?? 0) switch
        {
            1 => 2,
            2 => 4,
            3 => 6,
            _ => 0,
        };

        GeometryCVars.TerrainDetail.Value = (int)Math.Round(96f + Math.Clamp(gfx.TerrainDetail, 0f, 1f) * 67f);
    }

    /// <summary>Seeds the options in force from the live stores, so the menu shows what the game is using.</summary>
    /// <param name="config">The configuration to seed.</param>
    /// <remarks>
    /// Only the mapped options are seeded: the rest have no live store, so the retail file's
    /// values stand for them. The detail level is recomputed from the seeded fields, so a
    /// profile whose fields match no row reads Custom.
    /// </remarks>
    public static void SyncFromLive(Igi2GameConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        config.Gfx.TextureDetail = MaterialCVars.TextureQuality.Value switch
        {
            2 => 0,
            1 => 1,
            _ => 2,
        };
        config.Gfx.TerrainDetail = GeometryCVars.TerrainDetail.Value == 0
            ? 1f
            : Math.Clamp((GeometryCVars.TerrainDetail.Value - 96f) / 67f, 0f, 1f);

        if (config.Active is { } profile)
        {
            profile.AntialiasingMode = FrameCVars.MultiSampling.Value switch
            {
                2 => 1,
                4 => 2,
                6 => 3,
                _ => 0,
            };
            profile.TextureFilteringMode = MaterialCVars.PointMips.Value
                ? 0
                : MaterialCVars.Anisotropy.Value >= 2 ? 2 : 1;
        }

        config.GfxDetailLevel = Igi2GfxDetail.LevelFor(config.Gfx);
    }
}
