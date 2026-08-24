// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

/// <summary>
/// Predicts an 8-bit AV1 block from the rounded average of its top and left neighboring samples.
/// </summary>
internal class Av1DcPredictor : IAv1Predictor
{
    /// <summary>
    /// The number of top samples averaged and samples written to each destination row.
    /// </summary>
    private readonly nuint blockWidth;

    /// <summary>
    /// The number of left samples averaged and destination rows written.
    /// </summary>
    private readonly nuint blockHeight;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1DcPredictor"/> class for explicit block dimensions.
    /// </summary>
    /// <param name="blockSize">The predicted block dimensions in samples.</param>
    public Av1DcPredictor(Size blockSize)
    {
        this.blockWidth = (nuint)blockSize.Width;
        this.blockHeight = (nuint)blockSize.Height;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1DcPredictor"/> class for a transform size.
    /// </summary>
    /// <param name="transformSize">The transform size whose dimensions define the predicted block.</param>
    public Av1DcPredictor(Av1TransformSize transformSize)
    {
        this.blockWidth = (nuint)transformSize.GetWidth();
        this.blockHeight = (nuint)transformSize.GetHeight();
    }

    /// <summary>
    /// Predicts a transform block from its top and left neighboring samples.
    /// </summary>
    /// <param name="transformSize">The predicted block dimensions.</param>
    /// <param name="destination">The destination block.</param>
    /// <param name="stride">The distance, in samples, between destination rows.</param>
    /// <param name="above">The top neighboring samples.</param>
    /// <param name="left">The left neighboring samples.</param>
    public static void PredictScalar(Av1TransformSize transformSize, Span<byte> destination, nuint stride, Span<byte> above, Span<byte> left)
        => new Av1DcPredictor(transformSize).PredictScalar(destination, stride, above, left);

    /// <inheritdoc/>
    public void PredictScalar(Span<byte> destination, nuint stride, Span<byte> above, Span<byte> left)
    {
        int sum = 0;
        Guard.MustBeGreaterThanOrEqualTo(stride, this.blockWidth, nameof(stride));
        Guard.MustBeSizedAtLeast(left, (int)this.blockHeight, nameof(left));
        Guard.MustBeSizedAtLeast(above, (int)this.blockWidth, nameof(above));
        Guard.MustBeSizedAtLeast(destination, (int)this.blockHeight * (int)stride, nameof(destination));
        ref byte leftRef = ref left[0];
        ref byte aboveRef = ref above[0];
        ref byte destinationRef = ref destination[0];
        uint count = (uint)(this.blockWidth + this.blockHeight);
        uint width = (uint)this.blockWidth;
        for (nuint i = 0; i < this.blockWidth; i++)
        {
            sum += Unsafe.Add(ref aboveRef, i);
        }

        for (nuint i = 0; i < this.blockHeight; i++)
        {
            sum += Unsafe.Add(ref leftRef, i);
        }

        // Adding half the combined edge count implements the normative nearest-integer DC average before division.
        byte expectedDc = (byte)((sum + (count >> 1)) / count);
        for (nuint r = 0; r < this.blockHeight; r++)
        {
            Unsafe.InitBlock(ref destinationRef, expectedDc, width);
            destinationRef = ref Unsafe.Add(ref destinationRef, stride);
        }
    }
}
