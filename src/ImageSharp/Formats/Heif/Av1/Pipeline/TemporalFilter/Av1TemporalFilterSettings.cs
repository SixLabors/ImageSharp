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
    /// The default number of filter frames. Reference: the arnr_max_frames of default_extra_cfg.
    /// </summary>
    public const int DefaultMaximumFrames = 7;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1TemporalFilterSettings"/> struct with the default_extra_cfg
    /// values for good-quality encoding (seven filter frames, strength five, key frame filtering and overlays), the
    /// configured sharpness and the temporal filter speed features. Reference:
    /// set_good_speed_features_framesize_independent() and set_good_speed_feature_framesize_dependent().
    /// </summary>
    /// <param name="speed">The encoding speed, 0 to 6.</param>
    /// <param name="sharpness">The configured sharpness, algo_cfg.sharpness.</param>
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

        // hl_sf.weight_calc_level_in_tf: the approximated exponential from speed 3.
        this.WeightCalculationLevel = speed >= HeifEncodingSpeed.Level3 ? 1 : 0;

        // hl_sf.adjust_num_frames_for_arf_filtering: level 1 from speed 1 and level 2 from speed 5, never for
        // screen content.
        this.FrameCountAdjustment = allowScreenContentTools ? 0
            : speed >= HeifEncodingSpeed.Level5 ? 2
            : speed >= HeifEncodingSpeed.Level1 ? 1 : 0;

        // hl_sf.allow_sub_blk_me_in_tf: from speed 6 at 480p and larger.
        this.AllowSubblockMotionSearchPruning = speed >= HeifEncodingSpeed.Level6 && Math.Min(frameWidth, frameHeight) >= 480;

        // hl_sf.second_alt_ref_filtering: disabled from speed 6.
        this.SecondAlternateReferenceFiltering = speed < HeifEncodingSpeed.Level6;
    }

    /// <summary>
    /// Gets the configured number of filter frames, arnr_max_frames; one disables filtering.
    /// </summary>
    public int MaximumFrames { get; init; }

    /// <summary>
    /// Gets the configured filter strength, arnr_strength, 0 to 6.
    /// </summary>
    public int Strength { get; init; }

    /// <summary>
    /// Gets the key frame filtering mode, enable_keyframe_filtering; the strength limit of large mean differences
    /// applies only in mode one.
    /// </summary>
    public int KeyFrameFiltering { get; init; }

    /// <summary>
    /// Gets a value indicating whether a filtered alternate reference may be followed by an overlay, enable_overlay.
    /// </summary>
    public bool EnableOverlay { get; init; }

    /// <summary>
    /// Gets the sharpness, 0 to 7. Any nonzero value measures the source variance of each block for the motion
    /// search, and three also limits the motion search to the frame and lowers the strength of low-contrast blocks.
    /// </summary>
    public int Sharpness { get; init; }

    /// <summary>
    /// Gets the weight calculation level, hl_sf.weight_calc_level_in_tf.
    /// </summary>
    public int WeightCalculationLevel { get; init; }

    /// <summary>
    /// Gets the noise-based frame count adjustment level, hl_sf.adjust_num_frames_for_arf_filtering.
    /// </summary>
    public int FrameCountAdjustment { get; init; }

    /// <summary>
    /// Gets a value indicating whether sub-block motion search is skipped for flat blocks, hl_sf.allow_sub_blk_me_in_tf.
    /// </summary>
    public bool AllowSubblockMotionSearchPruning { get; init; }

    /// <summary>
    /// Gets a value indicating whether the second alternate reference is filtered, hl_sf.second_alt_ref_filtering.
    /// </summary>
    public bool SecondAlternateReferenceFiltering { get; init; }

    /// <summary>
    /// Gets the motion search speed features: the fractional method and iterations, the row-subsampled SAD level,
    /// and the mesh patterns and thresholds.
    /// </summary>
    public Av1MotionSearchSettings MotionSearch { get; init; }
}
