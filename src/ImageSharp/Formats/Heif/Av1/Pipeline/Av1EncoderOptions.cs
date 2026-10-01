// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.FilmGrain;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// The encoder options that libavif configures on top of the speed: the part of libaom's encoder configuration
/// (av1_extracfg) that the port reads. Reference: handle_tuning() and the controls of aomCodecEncodeImage().
/// </summary>
internal sealed class Av1EncoderOptions
{
    /// <summary>
    /// The first quantization matrix level of the image and SSIMULACRA 2 tunes. Reference: QM_FIRST_IQ_SSIMULACRA2.
    /// </summary>
    public const int FirstIqQuantizationMatrix = 2;

    /// <summary>
    /// The last quantization matrix level of the image and SSIMULACRA 2 tunes. Reference: QM_LAST_IQ_SSIMULACRA2.
    /// </summary>
    public const int LastIqQuantizationMatrix = 10;

    /// <summary>
    /// The largest key frame distance when the caller sets no key frame interval. Reference: the kf_max_dist
    /// default, which libavif keeps when keyframeInterval is 0.
    /// </summary>
    public const int DefaultKeyFrameMaximumDistance = 9999;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1EncoderOptions"/> class.
    /// </summary>
    /// <param name="speed">The cpu-used tier.</param>
    /// <param name="tuning">The tune metric.</param>
    /// <param name="enableRestoration">Whether loop restoration may be used; libavif turns it off for 12-bit images.</param>
    /// <param name="allIntra">Whether the encoder runs in all-intra usage, which libavif selects for single images.</param>
    public Av1EncoderOptions(HeifEncodingSpeed speed, Av1Tuning tuning, bool enableRestoration, bool allIntra = true)
    {
        this.Speed = speed;
        this.Tuning = tuning;
        this.EnableRestoration = enableRestoration;
        this.IsAllIntra = allIntra;

        // All-intra usage turns CDEF off, uses anti-aliasing aware screen detection and widens the matrix levels.
        // Reference: the AOM_USAGE_ALL_INTRA defaults of encoder_init().
        this.QuantizationMatrixMinimum = allIntra ? 4 : 5;
        this.QuantizationMatrixMaximum = allIntra ? 10 : 9;
        this.CdefControl = allIntra ? Av1CdefControl.None : Av1CdefControl.All;
        this.ScreenDetectionMode = allIntra ? Av1ScreenDetectionMode.AntialiasingAware : Av1ScreenDetectionMode.Standard;
        this.DistortionMetric = Av1DistortionMetric.Psnr;

        // Sequences keep the objective delta-q mode, which acts only when the temporal dependency model has
        // statistics. Reference: the DELTA_Q_OBJECTIVE deltaq_mode of the good-quality defaults.
        this.DeltaQMode = allIntra ? Av1DeltaQMode.None : Av1DeltaQMode.Objective;

        // The image and SSIMULACRA 2 tunes enable the quantization matrices, sharpness 7, the QM-PSNR metric, adaptive
        // CDEF, chroma delta q, variance boost and anti-aliasing aware screen detection. Only the image tune also
        // enables adaptive sharpness. Every other tune keeps the defaults. Reference: handle_tuning().
        if (tuning.IsImageTuning())
        {
            this.EnableQuantizationMatrices = true;
            this.QuantizationMatrixMinimum = FirstIqQuantizationMatrix;
            this.QuantizationMatrixMaximum = LastIqQuantizationMatrix;
            this.Sharpness = GetDefaultSharpness(tuning);
            this.DistortionMetric = Av1DistortionMetric.QuantizationMatrixPsnr;
            this.CdefControl = Av1CdefControl.Adaptive;
            this.EnableChromaDeltaQ = true;
            this.DeltaQMode = Av1DeltaQMode.VarianceBoost;
            this.ScreenDetectionMode = Av1ScreenDetectionMode.AntialiasingAware;
            this.EnableAdaptiveSharpness = tuning == Av1Tuning.Iq;
        }
    }

    /// <summary>
    /// Gets the cpu-used tier.
    /// </summary>
    public HeifEncodingSpeed Speed { get; init; }

    /// <summary>
    /// Gets the tune metric.
    /// </summary>
    public Av1Tuning Tuning { get; init; }

    /// <summary>
    /// Gets a value indicating whether the encoder runs in all-intra usage.
    /// </summary>
    public bool IsAllIntra { get; }

    /// <summary>
    /// Gets a value indicating whether loop restoration may be used.
    /// </summary>
    public bool EnableRestoration { get; init; }

    /// <summary>
    /// Gets a value indicating whether the quantization matrices are enabled. Reference: enable_qm.
    /// </summary>
    public bool EnableQuantizationMatrices { get; init; }

    /// <summary>
    /// Gets the lowest quantization matrix level. Reference: qm_min.
    /// </summary>
    public int QuantizationMatrixMinimum { get; init; }

    /// <summary>
    /// Gets the highest quantization matrix level. Reference: qm_max.
    /// </summary>
    public int QuantizationMatrixMaximum { get; init; }

    /// <summary>
    /// Gets the encoder sharpness, 0 to 7. Reference: algo_cfg.sharpness.
    /// </summary>
    public int Sharpness { get; init; }

    /// <summary>
    /// Gets the distortion metric. Reference: dist_metric.
    /// </summary>
    public Av1DistortionMetric DistortionMetric { get; init; }

    /// <summary>
    /// Gets the CDEF control. Reference: enable_cdef.
    /// </summary>
    public Av1CdefControl CdefControl { get; init; }

    /// <summary>
    /// Gets a value indicating whether the chroma planes get their own delta quantizers. Reference: enable_chroma_deltaq.
    /// </summary>
    public bool EnableChromaDeltaQ { get; init; }

    /// <summary>
    /// Gets the delta quantizer mode. Reference: deltaq_mode.
    /// </summary>
    public Av1DeltaQMode DeltaQMode { get; init; }

    /// <summary>
    /// Gets the adaptive quantization mode. Reference: aq_mode, NO_AQ by default.
    /// </summary>
    public Av1AdaptiveQuantizationMode AdaptiveQuantizationMode { get; init; }

    /// <summary>
    /// Gets the film grain preset from 1 to 16, or 0 for none. Reference: film_grain_test_vector.
    /// </summary>
    public int FilmGrainPreset { get; init; }

    /// <summary>
    /// Gets the film grain table, or <see langword="null"/> for none. Reference: film_grain_table_filename.
    /// </summary>
    public Av1FilmGrainTable? FilmGrainTable { get; init; }

    /// <summary>
    /// Gets a value indicating whether the sequence signals film grain. Reference:
    /// av1_update_film_grain_parameters_seq().
    /// </summary>
    public bool HasFilmGrain => this.FilmGrainPreset != 0 || this.FilmGrainTable is not null;

    /// <summary>
    /// Gets the screen content detection mode. Reference: screen_detection_mode.
    /// </summary>
    public Av1ScreenDetectionMode ScreenDetectionMode { get; init; }

    /// <summary>
    /// Gets a value indicating whether the loop filter sharpness adapts to the quantizer. Reference:
    /// enable_adaptive_sharpness.
    /// </summary>
    public bool EnableAdaptiveSharpness { get; init; }

    /// <summary>
    /// Gets a value indicating whether a sequence codes at a constant bit rate. libavif selects it for real-time
    /// usage. Reference: the AOM_CBR rc_end_usage of aomCodecEncodeImage().
    /// </summary>
    public bool UsesConstantBitRate { get; init; }

    /// <summary>
    /// Gets the lowest quantizer on libaom's zero-through-63 scale. Reference: rc_min_quantizer.
    /// </summary>
    public int MinimumQuantizer { get; init; }

    /// <summary>
    /// Gets the highest quantizer on libaom's zero-through-63 scale. Reference: rc_max_quantizer.
    /// </summary>
    public int MaximumQuantizer { get; init; } = 63;

    /// <summary>
    /// Gets the number of frames a sequence looks ahead before it codes a frame, or 0 to code each frame as it
    /// arrives. Reference: g_lag_in_frames.
    /// </summary>
    public int LagInFrames { get; init; }

    /// <summary>
    /// Gets a value indicating whether a lookahead sequence runs the temporal dependency model. Reference:
    /// enable_tpl_model, on by default.
    /// </summary>
    public bool EnableTemporalModel { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether a lookahead sequence filters its alternate references and key frames.
    /// Reference: arnr_max_frames and enable_keyframe_filtering, on by default.
    /// </summary>
    public bool EnableTemporalFilter { get; init; } = true;

    /// <summary>
    /// Gets the largest number of frames between key frames. Reference: kf_max_dist.
    /// </summary>
    public int KeyFrameMaximumDistance { get; init; } = DefaultKeyFrameMaximumDistance;

    /// <summary>
    /// Gets the number of spatial layers of a layered image, or 1 for an image without layers. Each layer is one frame
    /// of the sequence, so the count also limits the number of frames. The sequence header then lists one operating
    /// point per layer, each frame carries its layer in an OBU extension header, and the superblocks are 64x64.
    /// Reference: AOME_SET_NUMBER_SPATIAL_LAYERS and the g_limit that libavif sets to the layer count.
    /// </summary>
    public int LayerCount { get; init; } = 1;

    /// <summary>
    /// Gets a value indicating whether every frame of a constant-quality sequence codes at the constant-quality index,
    /// with no key frame or golden frame boost. libavif selects it for layered images. Reference: use_fixed_qp_offsets
    /// of 2, which av1_set_size_dependent_vars() reads with the AOM_Q end usage.
    /// </summary>
    public bool UsesFixedQuantizer { get; init; }

    /// <summary>
    /// Gets the number of tile columns as a power of two: 0 is one column, 1 is two, 2 is four.
    /// </summary>
    public int TileColumnsLog2 { get; init; }

    /// <summary>
    /// Gets the number of tile rows as a power of two: 0 is one row, 1 is two, 2 is four.
    /// </summary>
    public int TileRowsLog2 { get; init; }

    /// <summary>
    /// Returns the sharpness a tune sets: 7 for the image and SSIMULACRA 2 tunes, 0 for every other tune. A sharpness
    /// the caller sets replaces it, because libavif applies its codec options after the tune. Reference:
    /// handle_tuning(), and the order of the AOME_SET_TUNING control and avifProcessAOMOptionsPostInit() in
    /// aomCodecEncodeImage().
    /// </summary>
    /// <param name="tuning">The tune metric.</param>
    /// <returns>The sharpness.</returns>
    public static int GetDefaultSharpness(Av1Tuning tuning) => tuning.IsImageTuning() ? 7 : 0;

    /// <summary>
    /// Creates the options of an encoding.
    /// </summary>
    /// <param name="speed">The cpu-used tier.</param>
    /// <param name="tuning">The tune metric.</param>
    /// <param name="enableRestoration">Whether loop restoration may be used.</param>
    /// <param name="allIntra">Whether the encoder runs in all-intra usage.</param>
    /// <returns>The options.</returns>
    public static Av1EncoderOptions Create(
        HeifEncodingSpeed speed,
        Av1Tuning tuning = Av1Tuning.Psnr,
        bool enableRestoration = true,
        bool allIntra = true)
        => new(speed, tuning, enableRestoration, allIntra);
}
