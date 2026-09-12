// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Resolves the libaom speed features used by one encoded picture.
/// </summary>
internal readonly struct Av1EncoderSpeedSettings
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Av1EncoderSpeedSettings"/> struct.
    /// </summary>
    /// <param name="speed">The native-valued cpu-used tier.</param>
    /// <param name="allIntra">Whether the sequence uses the all-intra profile.</param>
    /// <param name="intraFrame">Whether the current picture is intra-only.</param>
    /// <param name="qIndex">The current base quantizer index.</param>
    public Av1EncoderSpeedSettings(HeifEncodingSpeed speed, bool allIntra, bool intraFrame, int qIndex)
    {
        this.Speed = speed;
        // GOOD mode disables dual interpolation filtering in its baseline speed features. All-intra pictures do not
        // write inter filters, so keeping the sequence flag disabled avoids advertising an unused coding tool.
        this.EnableDualFilter = false;
        this.EnableRestoration = !allIntra || speed < HeifEncodingSpeed.Level5;
        this.AllowHighPrecisionMotionVector = !intraFrame && qIndex < 128;
        this.UseVarianceBasedPartition = allIntra && speed >= HeifEncodingSpeed.Level7;
        this.PaletteSearchLevel = speed == HeifEncodingSpeed.Level0 ? 0 : speed < HeifEncodingSpeed.Level3 ? 1 : 2;
        this.LumaPaletteHeaderPruneLevel = allIntra ? speed == HeifEncodingSpeed.Level0 ? 1 : 2 : 0;
        this.EarlyTerminateChromaPaletteSearch = allIntra || speed >= HeifEncodingSpeed.Level6;
    }

    /// <summary>
    /// Gets the native-valued cpu-used tier.
    /// </summary>
    public HeifEncodingSpeed Speed { get; }

    /// <summary>
    /// Gets a value indicating whether independent horizontal and vertical interpolation filters are enabled.
    /// </summary>
    public bool EnableDualFilter { get; }

    /// <summary>
    /// Gets a value indicating whether loop restoration remains enabled for the sequence.
    /// </summary>
    public bool EnableRestoration { get; }

    /// <summary>
    /// Gets a value indicating whether eighth-sample motion-vector syntax is enabled for the picture.
    /// </summary>
    public bool AllowHighPrecisionMotionVector { get; }

    /// <summary>
    /// Gets a value indicating whether all-intra variance-based partitioning replaces rate-distortion partition search.
    /// </summary>
    public bool UseVarianceBasedPartition { get; }

    /// <summary>
    /// Gets the dominant-color and k-means palette search pruning level.
    /// </summary>
    public int PaletteSearchLevel { get; }

    /// <summary>
    /// Gets the luma palette header-cost pruning level.
    /// </summary>
    public int LumaPaletteHeaderPruneLevel { get; }

    /// <summary>
    /// Gets a value indicating whether chroma palette-size search terminates when its header alone exceeds the best cost.
    /// </summary>
    public bool EarlyTerminateChromaPaletteSearch { get; }
}
