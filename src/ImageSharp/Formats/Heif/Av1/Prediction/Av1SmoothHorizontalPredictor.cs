// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

/// <summary>
/// Predicts an 8-bit AV1 block by smoothly blending each left neighbor toward the top-right reference sample.
/// </summary>
internal class Av1SmoothHorizontalPredictor : IAv1Predictor
{
    /// <summary>
    /// The number of interpolation weights and samples written to each destination row.
    /// </summary>
    private readonly nuint blockWidth;

    /// <summary>
    /// The number of left samples consumed and destination rows written.
    /// </summary>
    private readonly nuint blockHeight;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1SmoothHorizontalPredictor"/> class for explicit block dimensions.
    /// </summary>
    /// <param name="blockSize">The predicted block dimensions in samples.</param>
    public Av1SmoothHorizontalPredictor(Size blockSize)
    {
        this.blockWidth = (nuint)blockSize.Width;
        this.blockHeight = (nuint)blockSize.Height;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1SmoothHorizontalPredictor"/> class for a transform size.
    /// </summary>
    /// <param name="transformSize">The transform size whose dimensions define the predicted block.</param>
    public Av1SmoothHorizontalPredictor(Av1TransformSize transformSize)
    {
        this.blockWidth = (nuint)transformSize.GetWidth();
        this.blockHeight = (nuint)transformSize.GetHeight();
    }

    /// <summary>
    /// Predicts a transform block using horizontal smooth interpolation.
    /// </summary>
    /// <param name="transformSize">The predicted block dimensions.</param>
    /// <param name="destination">The destination block.</param>
    /// <param name="stride">The distance, in samples, between destination rows.</param>
    /// <param name="above">The top neighboring samples whose final value supplies the right endpoint.</param>
    /// <param name="left">The left neighboring samples.</param>
    public static void PredictScalar(Av1TransformSize transformSize, Span<byte> destination, nuint stride, Span<byte> above, Span<byte> left)
        => new Av1SmoothHorizontalPredictor(transformSize).PredictScalar(destination, stride, above, left);

    /// <inheritdoc/>
    /// <remarks>SVT-AV1: <c>highbd_smooth_h_predictor</c>.</remarks>
    public void PredictScalar(Span<byte> destination, nuint stride, Span<byte> above, Span<byte> left)
    {
        Guard.MustBeGreaterThanOrEqualTo(stride, this.blockWidth, nameof(stride));
        Guard.MustBeSizedAtLeast(left, (int)this.blockHeight, nameof(left));
        Guard.MustBeSizedAtLeast(above, (int)this.blockWidth, nameof(above));
        Guard.MustBeSizedAtLeast(destination, (int)this.blockHeight * (int)stride, nameof(destination));
        ref byte leftRef = ref left[0];
        ref byte aboveRef = ref above[0];
        ref byte destinationRef = ref destination[0];
        int rightPrediction = Unsafe.Add(ref aboveRef, this.blockWidth - 1); // estimated by top-right pixel
        ref int weights = ref Av1SmoothPredictor.Weights[(int)this.blockWidth];

        int log2Scale = Av1SmoothPredictor.WeightLog2Scale;
        int scale = 1 << Av1SmoothPredictor.WeightLog2Scale;

        // sm_weights_sanity_checks(sm_weights_w, sm_weights_h, scale, log2_scale + 2);
        for (nuint r = 0; r < this.blockHeight; ++r)
        {
            for (nuint c = 0; c < this.blockWidth; ++c)
            {
                int columnWeight = Unsafe.Add(ref weights, c);
                Guard.MustBeGreaterThanOrEqualTo(scale, columnWeight, nameof(scale));

                // The Q8 weight decreases toward the right edge, shifting influence from left to top-right.
                int thisPredition = Unsafe.Add(ref leftRef, r) * columnWeight;
                thisPredition += rightPrediction * (scale - columnWeight);
                Unsafe.Add(ref destinationRef, c) = (byte)Av1Math.DivideRound(thisPredition, log2Scale);
            }

            destinationRef = ref Unsafe.Add(ref destinationRef, stride);
        }
    }
}
