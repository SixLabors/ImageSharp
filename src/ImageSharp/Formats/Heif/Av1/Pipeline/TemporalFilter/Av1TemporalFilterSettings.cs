// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.TemporalFilter;

/// <summary>
/// Holds the encoder configuration and the speed features that the temporal filter reads.
/// </summary>
internal readonly struct Av1TemporalFilterSettings
{
    /// <summary>
    /// The default number of filter frames.
    /// </summary>
    public const int DefaultMaximumFrames = 7;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1TemporalFilterSettings"/> struct with the default values for good-quality
    /// encoding (seven filter frames, strength five, key frame filtering and overlays), the configured sharpness and the temporal
    /// filter speed features of <paramref name="speed"/>.
    /// </summary>
    /// <param name="speed">The encoding speed, 0 to 6.</param>
    /// <param name="sharpness">The configured sharpness.</param>
    /// <param name="frameWidth">The visible frame width.</param>
    /// <param name="frameHeight">The visible frame height.</param>
    /// <param name="allowScreenContentTools">Whether the frame allows the screen content tools, which disables the
    /// noise-based frame count adjustment.</param>
    /// <param name="motionSearch">The motion search speed features of the frame.</param>
    public Av1TemporalFilterSettings(
        HeifEncodingSpeed speed,
        int sharpness,
        int frameWidth,
        int frameHeight,
        bool allowScreenContentTools,
        Av1MotionSearchSettings motionSearch)
    {
        this.MaximumFrames = DefaultMaximumFrames;
        this.Strength = 5;
        this.KeyFrameFiltering = 1;
        this.EnableOverlay = true;
        this.Sharpness = sharpness;
        this.MotionSearch = motionSearch;

        // Speed 3 and faster use the approximated exponential for the weights.
        this.WeightCalculationLevel = speed >= HeifEncodingSpeed.Level3 ? 1 : 0;

        // The noise-based frame count adjustment uses level 1 from speed 1 and level 2 from speed 5. Screen content never uses it.
        this.FrameCountAdjustment = allowScreenContentTools ? 0
            : speed >= HeifEncodingSpeed.Level5 ? 2
            : speed >= HeifEncodingSpeed.Level1 ? 1 : 0;

        // Speed 6 and faster skip the sub-block motion search of flat blocks when the shorter frame side is 480 or more.
        this.AllowSubblockMotionSearchPruning = speed >= HeifEncodingSpeed.Level6 && Math.Min(frameWidth, frameHeight) >= 480;

        // Speed 6 and faster do not filter the second alternate reference.
        this.SecondAlternateReferenceFiltering = speed < HeifEncodingSpeed.Level6;
    }

    /// <summary>
    /// Gets the configured number of filter frames. A value of one disables filtering.
    /// </summary>
    public int MaximumFrames { get; init; }

    /// <summary>
    /// Gets the configured filter strength, 0 to 6.
    /// </summary>
    public int Strength { get; init; }

    /// <summary>
    /// Gets the key frame filtering mode. Zero disables key frame filtering. The strength limit for large mean differences applies
    /// only in mode one.
    /// </summary>
    public int KeyFrameFiltering { get; init; }

    /// <summary>
    /// Gets a value indicating whether an overlay can follow a filtered alternate reference.
    /// </summary>
    public bool EnableOverlay { get; init; }

    /// <summary>
    /// Gets the sharpness, 0 to 7. Any nonzero value measures the source variance of each block for the motion
    /// search, and three also limits the motion search to the frame and lowers the strength of low-contrast blocks.
    /// </summary>
    public int Sharpness { get; init; }

    /// <summary>
    /// Gets the weight calculation level. Level zero uses the exact exponential. Level one uses the approximated exponential.
    /// </summary>
    public int WeightCalculationLevel { get; init; }

    /// <summary>
    /// Gets the level of the noise-based frame count adjustment. Zero disables the adjustment.
    /// </summary>
    public int FrameCountAdjustment { get; init; }

    /// <summary>
    /// Gets a value indicating whether the sub-block motion search is skipped for flat blocks.
    /// </summary>
    public bool AllowSubblockMotionSearchPruning { get; init; }

    /// <summary>
    /// Gets a value indicating whether the second alternate reference is filtered.
    /// </summary>
    public bool SecondAlternateReferenceFiltering { get; init; }

    /// <summary>
    /// Gets the motion search speed features: the fractional method and iterations, the row-subsampled SAD level,
    /// and the mesh patterns and thresholds.
    /// </summary>
    public Av1MotionSearchSettings MotionSearch { get; init; }
}
