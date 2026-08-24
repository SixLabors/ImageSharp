// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

/// <summary>
/// Predicts an 8-bit AV1 block by extending each left neighboring sample across its destination row.
/// </summary>
internal class Av1HorizontalPredictor : IAv1Predictor
{
    /// <summary>
    /// The number of samples written to each destination row.
    /// </summary>
    private readonly nuint blockWidth;

    /// <summary>
    /// The number of left samples consumed and destination rows written.
    /// </summary>
    private readonly nuint blockHeight;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1HorizontalPredictor"/> class for explicit block dimensions.
    /// </summary>
    /// <param name="blockSize">The predicted block dimensions in samples.</param>
    public Av1HorizontalPredictor(Size blockSize)
    {
        this.blockWidth = (nuint)blockSize.Width;
        this.blockHeight = (nuint)blockSize.Height;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1HorizontalPredictor"/> class for a transform size.
    /// </summary>
    /// <param name="transformSize">The transform size whose dimensions define the predicted block.</param>
    public Av1HorizontalPredictor(Av1TransformSize transformSize)
    {
        this.blockWidth = (nuint)transformSize.GetWidth();
        this.blockHeight = (nuint)transformSize.GetHeight();
    }

    /// <summary>
    /// Predicts a transform block by extending its left edge horizontally.
    /// </summary>
    /// <param name="transformSize">The predicted block dimensions.</param>
    /// <param name="destination">The destination block.</param>
    /// <param name="stride">The distance, in samples, between destination rows.</param>
    /// <param name="above">The unused top-neighbor buffer required by the common predictor signature.</param>
    /// <param name="left">The left neighboring samples.</param>
    public static void PredictScalar(Av1TransformSize transformSize, Span<byte> destination, nuint stride, Span<byte> above, Span<byte> left)
        => new Av1HorizontalPredictor(transformSize).PredictScalar(destination, stride, above, left);

    /// <inheritdoc/>
    /// <remarks>SVT-AV1: <c>highbd_h_predictor</c>.</remarks>
    public void PredictScalar(Span<byte> destination, nuint stride, Span<byte> above, Span<byte> left)
    {
        Guard.MustBeGreaterThanOrEqualTo(stride, this.blockWidth, nameof(stride));
        Guard.MustBeSizedAtLeast(left, (int)this.blockHeight, nameof(left));
        Guard.MustBeSizedAtLeast(above, (int)this.blockWidth, nameof(above));
        Guard.MustBeSizedAtLeast(destination, (int)this.blockHeight * (int)stride, nameof(destination));
        ref byte leftRef = ref left[0];
        ref byte destinationRef = ref destination[0];
        uint width = (uint)this.blockWidth;
        for (nuint r = 0; r < this.blockHeight; ++r)
        {
            Unsafe.InitBlock(ref destinationRef, Unsafe.Add(ref leftRef, r), width);
            destinationRef = ref Unsafe.Add(ref destinationRef, stride);
        }
    }
}
