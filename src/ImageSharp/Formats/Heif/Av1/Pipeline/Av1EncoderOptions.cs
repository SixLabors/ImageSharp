// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// The encoder options that libavif configures on top of the speed: the part of libaom's encoder configuration
/// (av1_extracfg) that the port reads. Reference: handle_tuning() and the controls of aomCodecEncodeImage().
/// </summary>
internal sealed class Av1EncoderOptions
{
    /// <summary>
    /// The first quantization matrix level of tune=iq. Reference: QM_FIRST_IQ_SSIMULACRA2.
    /// </summary>
    public const int FirstIqQuantizationMatrix = 2;

    /// <summary>
    /// The last quantization matrix level of tune=iq. Reference: QM_LAST_IQ_SSIMULACRA2.
    /// </summary>
    public const int LastIqQuantizationMatrix = 10;

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
        this.DeltaQMode = Av1DeltaQMode.None;

        // The image tune enables the quantization matrices, sharpness 7, the QM-PSNR metric, adaptive CDEF, chroma
        // delta q, variance boost, anti-aliasing aware screen detection and adaptive sharpness. Every other tune keeps
        // the defaults. Reference: handle_tuning().
        if (tuning == Av1Tuning.Iq)
        {
            this.EnableQuantizationMatrices = true;
            this.QuantizationMatrixMinimum = FirstIqQuantizationMatrix;
            this.QuantizationMatrixMaximum = LastIqQuantizationMatrix;
            this.Sharpness = 7;
            this.DistortionMetric = Av1DistortionMetric.QuantizationMatrixPsnr;
            this.CdefControl = Av1CdefControl.Adaptive;
            this.EnableChromaDeltaQ = true;
            this.DeltaQMode = Av1DeltaQMode.VarianceBoost;
            this.ScreenDetectionMode = Av1ScreenDetectionMode.AntialiasingAware;
            this.EnableAdaptiveSharpness = true;
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
    /// Gets the loop filter sharpness. Reference: sharpness.
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
