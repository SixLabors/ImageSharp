// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

/// <summary>
/// Predicts an 8-bit AV1 block from the rounded average of its available left neighboring samples.
/// </summary>
internal readonly struct Av1DcLeftPredictor : IAv1Predictor
{
    /// <summary>
    /// The number of samples written to each destination row.
    /// </summary>
    private readonly uint blockWidth;

    /// <summary>
    /// The number of left samples averaged and destination rows written.
    /// </summary>
    private readonly uint blockHeight;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1DcLeftPredictor"/> struct for explicit block dimensions.
    /// </summary>
    /// <param name="blockSize">The predicted block dimensions in samples.</param>
    public Av1DcLeftPredictor(Size blockSize)
    {
        this.blockWidth = (uint)blockSize.Width;
        this.blockHeight = (uint)blockSize.Height;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1DcLeftPredictor"/> struct for a transform size.
    /// </summary>
    /// <param name="transformSize">The transform size whose dimensions define the predicted block.</param>
    public Av1DcLeftPredictor(Av1TransformSize transformSize)
    {
        this.blockWidth = (uint)transformSize.GetWidth();
        this.blockHeight = (uint)transformSize.GetHeight();
    }

    /// <summary>
    /// Predicts a transform block from its left neighboring samples.
    /// </summary>
    /// <param name="transformSize">The predicted block dimensions.</param>
    /// <param name="destination">The destination block.</param>
    /// <param name="stride">The distance, in samples, between destination rows.</param>
    /// <param name="above">The unused top-neighbor buffer required by the common predictor signature.</param>
    /// <param name="left">The left neighboring samples.</param>
    public static void PredictScalar(Av1TransformSize transformSize, Span<byte> destination, nuint stride, Span<byte> above, Span<byte> left)
        => new Av1DcLeftPredictor(transformSize).PredictScalar(destination, stride, above, left);

    /// <inheritdoc/>
    public void PredictScalar(Span<byte> destination, nuint stride, Span<byte> above, Span<byte> left)
    {
        int sum = 0;
        Guard.MustBeGreaterThanOrEqualTo(stride, this.blockWidth, nameof(stride));
        Guard.MustBeSizedAtLeast(left, (int)this.blockHeight, nameof(left));
        Guard.MustBeSizedAtLeast(destination, (int)this.blockHeight * (int)stride, nameof(destination));
        ref byte leftRef = ref left[0];
        ref byte destinationRef = ref destination[0];
        for (uint i = 0; i < this.blockHeight; i++)
        {
            sum += Unsafe.Add(ref leftRef, i);
        }

        // Adding half the sample count implements the normative nearest-integer DC average before division.
        byte expectedDc = (byte)((sum + (this.blockHeight >> 1)) / this.blockHeight);
        for (uint r = 0; r < this.blockHeight; r++)
        {
            Unsafe.InitBlock(ref destinationRef, expectedDc, this.blockWidth);
            destinationRef = ref Unsafe.Add(ref destinationRef, stride);
        }
    }
}
