// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.FilmGrain;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// The encoder options that apply on top of the speed. The tune and the caller options set these values.
/// </summary>
internal sealed class Av1EncoderOptions
{
    /// <summary>
    /// The first quantization matrix level of the image and SSIMULACRA 2 tunes.
    /// </summary>
    public const int FirstIqQuantizationMatrix = 2;

    /// <summary>
    /// The last quantization matrix level of the image and SSIMULACRA 2 tunes.
    /// </summary>
    public const int LastIqQuantizationMatrix = 10;

    /// <summary>
    /// The largest key frame distance when the caller sets no key frame interval.
    /// </summary>
    public const int DefaultKeyFrameMaximumDistance = 9999;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1EncoderOptions"/> class.
    /// </summary>
    /// <param name="speed">The encoding speed.</param>
    /// <param name="tuning">The tune metric.</param>
    /// <param name="enableRestoration">Whether loop restoration can be used. A caller can turn it off, for example for 12-bit images.</param>
    /// <param name="allIntra">Whether the encoder runs in all-intra usage. Single images use all-intra usage.</param>
    public Av1EncoderOptions(HeifEncodingSpeed speed, Av1Tuning tuning, bool enableRestoration, bool allIntra = true)
    {
        this.Speed = speed;
        this.Tuning = tuning;
        this.EnableRestoration = enableRestoration;
        this.IsAllIntra = allIntra;

        // All-intra usage turns CDEF off, uses anti-aliasing aware screen detection and widens the matrix levels.
        this.QuantizationMatrixMinimum = allIntra ? 4 : 5;
        this.QuantizationMatrixMaximum = allIntra ? 10 : 9;
        this.CdefControl = allIntra ? Av1CdefControl.None : Av1CdefControl.All;
        this.ScreenDetectionMode = allIntra ? Av1ScreenDetectionMode.AntialiasingAware : Av1ScreenDetectionMode.Standard;
        this.DistortionMetric = Av1DistortionMetric.Psnr;

        // Sequences use the objective delta-q mode. This mode acts only when the temporal dependency model has statistics.
        this.DeltaQMode = allIntra ? Av1DeltaQMode.None : Av1DeltaQMode.Objective;

        // The image and SSIMULACRA 2 tunes enable the quantization matrices, sharpness 7, the QM-PSNR metric, adaptive CDEF, chroma delta q,
        // variance boost and anti-aliasing aware screen detection. Only the image tune also enables adaptive sharpness.
        // Every other tune keeps the defaults.
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
    /// Gets the encoding speed.
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
    /// Gets a value indicating whether loop restoration can be used.
    /// </summary>
    public bool EnableRestoration { get; init; }

    /// <summary>
    /// Gets a value indicating whether the quantization matrices are enabled.
    /// </summary>
    public bool EnableQuantizationMatrices { get; init; }

    /// <summary>
    /// Gets the lowest quantization matrix level.
    /// </summary>
    public int QuantizationMatrixMinimum { get; init; }

    /// <summary>
    /// Gets the highest quantization matrix level.
    /// </summary>
    public int QuantizationMatrixMaximum { get; init; }

    /// <summary>
    /// Gets the encoder sharpness, 0 to 7.
    /// </summary>
    public int Sharpness { get; init; }

    /// <summary>
    /// Gets the token that stops the encode. The tile encoder reads it before each superblock row, so a large frame stops part way through.
    /// The token changes no coded output.
    /// </summary>
    public CancellationToken CancellationToken { get; init; }

    /// <summary>
    /// Gets the distortion metric.
    /// </summary>
    public Av1DistortionMetric DistortionMetric { get; init; }

    /// <summary>
    /// Gets the CDEF control.
    /// </summary>
    public Av1CdefControl CdefControl { get; init; }

    /// <summary>
    /// Gets a value indicating whether the chroma planes get their own delta quantizers.
    /// </summary>
    public bool EnableChromaDeltaQ { get; init; }

    /// <summary>
    /// Gets the delta quantizer mode.
    /// </summary>
    public Av1DeltaQMode DeltaQMode { get; init; }

    /// <summary>
    /// Gets the adaptive quantization mode. The default is <see cref="Av1AdaptiveQuantizationMode.None"/>.
    /// </summary>
    public Av1AdaptiveQuantizationMode AdaptiveQuantizationMode { get; init; }

    /// <summary>
    /// Gets the film grain preset from 1 to 16, or 0 for none.
    /// </summary>
    public int FilmGrainPreset { get; init; }

    /// <summary>
    /// Gets the film grain table, or <see langword="null"/> for none.
    /// </summary>
    public Av1FilmGrainTable? FilmGrainTable { get; init; }

    /// <summary>
    /// Gets a value indicating whether the sequence signals film grain. A preset or a table turns film grain on.
    /// </summary>
    public bool HasFilmGrain => this.FilmGrainPreset != 0 || this.FilmGrainTable is not null;

    /// <summary>
    /// Gets the screen content detection mode.
    /// </summary>
    public Av1ScreenDetectionMode ScreenDetectionMode { get; init; }

    /// <summary>
    /// Gets a value indicating whether the loop filter sharpness adapts to the quantizer.
    /// </summary>
    public bool EnableAdaptiveSharpness { get; init; }

    /// <summary>
    /// Gets the rate control mode. The HEIF encoder selects <see cref="Av1RateControlMode.ConstantBitRate"/> for real-time usage and
    /// <see cref="Av1RateControlMode.Quality"/> for other usages. An end-usage option from the caller replaces this default.
    /// </summary>
    public Av1RateControlMode RateControlMode { get; init; }

    /// <summary>
    /// Gets a value indicating whether the frames code at a constant bit rate.
    /// </summary>
    public bool UsesConstantBitRate => this.RateControlMode == Av1RateControlMode.ConstantBitRate;

    /// <summary>
    /// Gets a value indicating whether the frames code against a bit budget. This is true in every mode except <see cref="Av1RateControlMode.Quality"/>.
    /// </summary>
    public bool UsesBitBudget => this.RateControlMode != Av1RateControlMode.Quality;

    /// <summary>
    /// Gets a value indicating whether the frames follow the constant-quality level.
    /// This is true only in the <see cref="Av1RateControlMode.Quality"/> and <see cref="Av1RateControlMode.ConstrainedQuality"/> modes.
    /// </summary>
    public bool UsesConstantQualityLevel =>
        this.RateControlMode is Av1RateControlMode.Quality or Av1RateControlMode.ConstrainedQuality;

    /// <summary>
    /// Gets the lowest quantizer on the scale from 0 to 63.
    /// </summary>
    public int MinimumQuantizer { get; init; }

    /// <summary>
    /// Gets the highest quantizer on the scale from 0 to 63.
    /// </summary>
    public int MaximumQuantizer { get; init; } = 63;

    /// <summary>
    /// Gets the number of frames that a sequence looks ahead before it codes a frame. The value 0 codes each frame when it arrives.
    /// </summary>
    public int LagInFrames { get; init; }

    /// <summary>
    /// Gets a value indicating whether a lookahead sequence runs the temporal dependency model. The default is on.
    /// </summary>
    public bool EnableTemporalModel { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether a lookahead sequence filters its alternate references and key frames. The default is on.
    /// </summary>
    public bool EnableTemporalFilter { get; init; } = true;

    /// <summary>
    /// Gets the largest number of frames between key frames.
    /// </summary>
    public int KeyFrameMaximumDistance { get; init; } = DefaultKeyFrameMaximumDistance;

    /// <summary>
    /// Gets the number of spatial layers of a layered image, or 1 for an image without layers.
    /// Each layer is one frame of the sequence, so the count also limits the number of frames.
    /// The sequence header then lists one operating point per layer. Each frame carries its layer in an OBU extension header.
    /// The superblocks are 64x64.
    /// </summary>
    public int LayerCount { get; init; } = 1;

    /// <summary>
    /// Gets a value indicating whether every frame of a constant-quality sequence codes at the constant-quality index.
    /// Key frames and golden frames then get no quality boost. The HEIF encoder selects this for layered images.
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
    /// Returns the sharpness that a tune sets. The image and SSIMULACRA 2 tunes set 7. Every other tune sets 0.
    /// A sharpness that the caller sets replaces this value, because the caller options apply after the tune.
    /// </summary>
    /// <param name="tuning">The tune metric.</param>
    /// <returns>The sharpness.</returns>
    public static int GetDefaultSharpness(Av1Tuning tuning) => tuning.IsImageTuning() ? 7 : 0;

    /// <summary>
    /// Creates the options of an encoding.
    /// </summary>
    /// <param name="speed">The encoding speed.</param>
    /// <param name="tuning">The tune metric.</param>
    /// <param name="enableRestoration">Whether loop restoration can be used.</param>
    /// <param name="allIntra">Whether the encoder runs in all-intra usage.</param>
    /// <returns>The options.</returns>
    public static Av1EncoderOptions Create(
        HeifEncodingSpeed speed,
        Av1Tuning tuning = Av1Tuning.Psnr,
        bool enableRestoration = true,
        bool allIntra = true)
        => new(speed, tuning, enableRestoration, allIntra);
}
