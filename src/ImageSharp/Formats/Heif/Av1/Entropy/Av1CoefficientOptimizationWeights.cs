// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;

/// <summary>
/// The encoder configuration that coefficient optimization reads for one transform block. It holds the sharpness, the shift of the rate multiplier,
/// and the quantization matrices.
/// </summary>
internal readonly ref struct Av1CoefficientOptimizationWeights
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Av1CoefficientOptimizationWeights"/> struct.
    /// </summary>
    /// <param name="sharpness">The encoder sharpness from zero through seven.</param>
    /// <param name="rateShift">The shift of the rate multiplier. It is 7 for the image tunes, 6 for a noise pattern, and 5 for all other blocks.</param>
    /// <param name="endOfBlockCutoff">The last scan position that sharpness protects. It is 8 for a noise pattern and 5 for all other blocks.</param>
    /// <param name="distortionWeights">The quantization matrix of the QM-PSNR metric, or empty for plain squared error.</param>
    /// <param name="inverseWeights">The inverse quantization matrix, or empty for a flat matrix.</param>
    public Av1CoefficientOptimizationWeights(
        int sharpness, int rateShift, int endOfBlockCutoff, ReadOnlySpan<byte> distortionWeights, ReadOnlySpan<byte> inverseWeights)
    {
        this.Sharpness = sharpness;
        this.RateShift = rateShift;
        this.EndOfBlockCutoff = endOfBlockCutoff;
        this.DistortionWeights = distortionWeights;
        this.InverseWeights = inverseWeights;
    }

    /// <summary>
    /// Gets the weights of the default configuration. The sharpness is zero, the shift and the cutoff are 5, and the matrices are flat.
    /// </summary>
    public static Av1CoefficientOptimizationWeights Default => new(0, 5, 5, default, default);

    /// <summary>
    /// Gets the last scan position that sharpness protects. Up to this position, sharpness keeps a level above two and keeps the end of block.
    /// </summary>
    public int EndOfBlockCutoff { get; }

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
