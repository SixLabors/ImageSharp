// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

/// <summary>
/// Predicts an 8-bit AV1 block by selecting the top, left, or top-left neighbor with the smallest local gradient.
/// </summary>
internal readonly struct Av1PaethPredictor : IAv1Predictor
{
    /// <summary>
    /// The number of top samples consumed and samples written to each destination row.
    /// </summary>
    private readonly uint blockWidth;

    /// <summary>
    /// The number of left samples consumed and destination rows written.
    /// </summary>
    private readonly uint blockHeight;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1PaethPredictor"/> struct for explicit block dimensions.
    /// </summary>
    /// <param name="blockSize">The predicted block dimensions in samples.</param>
    public Av1PaethPredictor(Size blockSize)
    {
        this.blockWidth = (uint)blockSize.Width;
        this.blockHeight = (uint)blockSize.Height;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1PaethPredictor"/> struct for a transform size.
    /// </summary>
    /// <param name="transformSize">The transform size whose dimensions define the predicted block.</param>
    public Av1PaethPredictor(Av1TransformSize transformSize)
    {
        this.blockWidth = (uint)transformSize.GetWidth();
        this.blockHeight = (uint)transformSize.GetHeight();
    }

    /// <summary>
    /// Predicts a transform block using the Paeth gradient selector.
    /// </summary>
    /// <param name="transformSize">The predicted block dimensions.</param>
    /// <param name="destination">The destination block.</param>
    /// <param name="stride">The distance, in samples, between destination rows.</param>
    /// <param name="above">The top neighboring samples, preceded in memory by the top-left sample.</param>
    /// <param name="left">The left neighboring samples.</param>
    public static void PredictScalar(Av1TransformSize transformSize, Span<byte> destination, nuint stride, Span<byte> above, Span<byte> left)
        => new Av1PaethPredictor(transformSize).PredictScalar(destination, stride, above, left);

    /// <inheritdoc/>
    public void PredictScalar(Span<byte> destination, nuint stride, Span<byte> above, Span<byte> left)
    {
        Guard.MustBeGreaterThanOrEqualTo(stride, this.blockWidth, nameof(stride));
        Guard.MustBeSizedAtLeast(left, (int)this.blockHeight, nameof(left));
        Guard.MustBeSizedAtLeast(above, (int)this.blockWidth, nameof(above));
        Guard.MustBeSizedAtLeast(destination, (int)this.blockHeight * (int)stride, nameof(destination));
        ref byte leftRef = ref left[0];
        ref byte aboveRef = ref above[0];

        // The caller preserves the top-left sample immediately before the top row.
        int yTopLeft = Unsafe.Subtract(ref aboveRef, 1);
        ref byte destinationRef = ref destination[0];
        for (nuint r = 0; r < this.blockHeight; r++)
        {
            for (nuint c = 0; c < this.blockWidth; c++)
            {
                Unsafe.Add(ref destinationRef, c) = PredictSingle(
                    Unsafe.Add(ref leftRef, r),
                    Unsafe.Add(ref aboveRef, c),
                    yTopLeft);
            }

            destinationRef = ref Unsafe.Add(ref destinationRef, stride);
        }
    }

    /// <summary>
    /// Selects the reference sample nearest to the planar estimate <paramref name="top"/> + <paramref name="left"/> - <paramref name="topLeft"/>.
    /// </summary>
    /// <param name="left">The left reference sample.</param>
    /// <param name="top">The top reference sample.</param>
    /// <param name="topLeft">The shared top-left reference sample.</param>
    /// <returns>The reference sample with the smallest absolute distance from the planar estimate.</returns>
    private static byte PredictSingle(byte left, byte top, int topLeft)
    {
        int basis = top + left - topLeft;
        int pLeft = Av1Math.AbsoluteDifference(basis, left);
        int pTop = Av1Math.AbsoluteDifference(basis, top);
        int pTopLeft = Av1Math.AbsoluteDifference(basis, topLeft);

        // Ties prefer left, then top, matching AV1's normative Paeth selection order.
        return (byte)((pLeft <= pTop && pLeft <= pTopLeft) ? left : (pTop <= pTopLeft) ? top : topLeft);
    }
}
