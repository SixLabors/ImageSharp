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
    private Av1EncoderOptions(HeifEncodingSpeed speed, Av1Tuning tuning, bool enableRestoration)
    {
        this.Speed = speed;
        this.Tuning = tuning;
        this.EnableRestoration = enableRestoration;

        // Reference: handle_tuning(). The image tune enables the quantization matrices, sharpness 7, the QM-PSNR
        // metric, adaptive CDEF, chroma delta q, variance boost, anti-aliasing aware screen detection and adaptive
        // sharpness. Every other tune keeps the libaom defaults.
        bool iq = tuning == Av1Tuning.Iq;
        this.EnableQuantizationMatrices = iq;
        this.QuantizationMatrixMinimum = iq ? FirstIqQuantizationMatrix : 5;
        this.QuantizationMatrixMaximum = iq ? LastIqQuantizationMatrix : 9;
        this.Sharpness = iq ? 7 : 0;
        this.DistortionMetric = iq ? Av1DistortionMetric.QuantizationMatrixPsnr : Av1DistortionMetric.Psnr;
        this.CdefControl = iq ? Av1CdefControl.Adaptive : Av1CdefControl.All;
        this.EnableChromaDeltaQ = iq;
        this.DeltaQMode = iq ? Av1DeltaQMode.VarianceBoost : Av1DeltaQMode.None;
        this.ScreenDetectionMode = iq ? Av1ScreenDetectionMode.AntialiasingAware : Av1ScreenDetectionMode.Standard;
        this.EnableAdaptiveSharpness = iq;
    }

    /// <summary>
    /// Gets the cpu-used tier.
    /// </summary>
    public HeifEncodingSpeed Speed { get; }

    /// <summary>
    /// Gets the tune metric.
    /// </summary>
    public Av1Tuning Tuning { get; }

    /// <summary>
    /// Gets a value indicating whether loop restoration may be used.
    /// </summary>
    public bool EnableRestoration { get; }

    /// <summary>
    /// Gets a value indicating whether the quantization matrices are enabled. Reference: enable_qm.
    /// </summary>
    public bool EnableQuantizationMatrices { get; }

    /// <summary>
    /// Gets the lowest quantization matrix level. Reference: qm_min.
    /// </summary>
    public int QuantizationMatrixMinimum { get; }

    /// <summary>
    /// Gets the highest quantization matrix level. Reference: qm_max.
    /// </summary>
    public int QuantizationMatrixMaximum { get; }

    /// <summary>
    /// Gets the loop filter sharpness. Reference: sharpness.
    /// </summary>
    public int Sharpness { get; }

    /// <summary>
    /// Gets the distortion metric. Reference: dist_metric.
    /// </summary>
    public Av1DistortionMetric DistortionMetric { get; }

    /// <summary>
    /// Gets the CDEF control. Reference: enable_cdef.
    /// </summary>
    public Av1CdefControl CdefControl { get; }

    /// <summary>
    /// Gets a value indicating whether the chroma planes get their own delta quantizers. Reference: enable_chroma_deltaq.
    /// </summary>
    public bool EnableChromaDeltaQ { get; }

    /// <summary>
    /// Gets the delta quantizer mode. Reference: deltaq_mode.
    /// </summary>
    public Av1DeltaQMode DeltaQMode { get; }

    /// <summary>
    /// Gets the screen content detection mode. Reference: screen_detection_mode.
    /// </summary>
    public Av1ScreenDetectionMode ScreenDetectionMode { get; }

    /// <summary>
    /// Gets a value indicating whether the loop filter sharpness adapts to the quantizer. Reference:
    /// enable_adaptive_sharpness.
    /// </summary>
    public bool EnableAdaptiveSharpness { get; }

    /// <summary>
    /// Creates the options of an encoding.
    /// </summary>
    /// <param name="speed">The cpu-used tier.</param>
    /// <param name="tuning">The tune metric.</param>
    /// <param name="enableRestoration">Whether loop restoration may be used.</param>
    /// <returns>The options.</returns>
    public static Av1EncoderOptions Create(HeifEncodingSpeed speed, Av1Tuning tuning = Av1Tuning.Psnr, bool enableRestoration = true)
        => new(speed, tuning, enableRestoration);
}
