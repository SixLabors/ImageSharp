// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;

/// <summary>
/// The encoder configuration that coefficient optimization reads: the sharpness, the shift of the rate multiplier and
/// the quantization matrices of one transform block. Reference: the sharpness, rshift, qmatrix and iqmatrix of
/// av1_optimize_txb().
/// </summary>
internal readonly ref struct Av1CoefficientOptimizationWeights
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Av1CoefficientOptimizationWeights"/> struct.
    /// </summary>
    /// <param name="sharpness">The encoder sharpness from zero through seven.</param>
    /// <param name="rateShift">The shift of the rate multiplier; 7 for the image tunes, else 5.</param>
    /// <param name="distortionWeights">The quantization matrix of the QM-PSNR metric, or empty for plain squared error.</param>
    /// <param name="inverseWeights">The inverse quantization matrix, or empty for a flat matrix.</param>
    public Av1CoefficientOptimizationWeights(
        int sharpness,
        int rateShift,
        ReadOnlySpan<byte> distortionWeights,
        ReadOnlySpan<byte> inverseWeights)
    {
        this.Sharpness = sharpness;
        this.RateShift = rateShift;
        this.DistortionWeights = distortionWeights;
        this.InverseWeights = inverseWeights;
    }

    /// <summary>
    /// Gets the weights of the default configuration: no sharpness, the default shift and flat matrices.
    /// </summary>
    public static Av1CoefficientOptimizationWeights Default => new(0, 5, default, default);

    /// <summary>
    /// Gets the encoder sharpness from zero through seven.
    /// </summary>
    public int Sharpness { get; }

    /// <summary>
    /// Gets the shift of the rate multiplier.
    /// </summary>
    public int RateShift { get; }

    /// <summary>
    /// Gets the quantization matrix that weights the distortion, or an empty span for plain squared error.
    /// </summary>
    public ReadOnlySpan<byte> DistortionWeights { get; }

    /// <summary>
    /// Gets the inverse quantization matrix that weights the reconstruction steps, or an empty span for a flat matrix.
    /// </summary>
    public ReadOnlySpan<byte> InverseWeights { get; }
}
