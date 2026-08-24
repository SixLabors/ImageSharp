// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

/// <summary>
/// Predicts an 8-bit AV1 block by smoothly blending each top neighbor toward the bottom-left reference sample.
/// </summary>
internal readonly struct Av1SmoothVerticalPredictor : IAv1Predictor
{
    /// <summary>
    /// The number of top samples consumed and samples written to each destination row.
    /// </summary>
    private readonly nuint blockWidth;

    /// <summary>
    /// The number of interpolation weights, left samples consumed, and destination rows written.
    /// </summary>
    private readonly nuint blockHeight;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1SmoothVerticalPredictor"/> struct for explicit block dimensions.
    /// </summary>
    /// <param name="blockSize">The predicted block dimensions in samples.</param>
    public Av1SmoothVerticalPredictor(Size blockSize)
    {
        this.blockWidth = (nuint)blockSize.Width;
        this.blockHeight = (nuint)blockSize.Height;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1SmoothVerticalPredictor"/> struct for a transform size.
    /// </summary>
    /// <param name="transformSize">The transform size whose dimensions define the predicted block.</param>
    public Av1SmoothVerticalPredictor(Av1TransformSize transformSize)
    {
        this.blockWidth = (nuint)transformSize.GetWidth();
        this.blockHeight = (nuint)transformSize.GetHeight();
    }

    /// <summary>
    /// Predicts a transform block using vertical smooth interpolation.
    /// </summary>
    /// <param name="transformSize">The predicted block dimensions.</param>
    /// <param name="destination">The destination block.</param>
    /// <param name="stride">The distance, in samples, between destination rows.</param>
    /// <param name="above">The top neighboring samples.</param>
    /// <param name="left">The left neighboring samples whose final value supplies the bottom endpoint.</param>
    public static void PredictScalar(Av1TransformSize transformSize, Span<byte> destination, nuint stride, Span<byte> above, Span<byte> left)
        => new Av1SmoothVerticalPredictor(transformSize).PredictScalar(destination, stride, above, left);

    /// <inheritdoc/>
    /// <remarks>SVT-AV1: <c>highbd_smooth_v_predictor</c>.</remarks>
    public void PredictScalar(Span<byte> destination, nuint stride, Span<byte> above, Span<byte> left)
    {
        Guard.MustBeGreaterThanOrEqualTo(stride, this.blockWidth, nameof(stride));
        Guard.MustBeSizedAtLeast(left, (int)this.blockHeight, nameof(left));
        Guard.MustBeSizedAtLeast(above, (int)this.blockWidth, nameof(above));
        Guard.MustBeSizedAtLeast(destination, (int)this.blockHeight * (int)stride, nameof(destination));
        ref byte leftRef = ref left[0];
        ref byte aboveRef = ref above[0];
        ref byte destinationRef = ref destination[0];
        int belowPrediction = Unsafe.Add(ref leftRef, this.blockHeight - 1); // estimated by bottom-left pixel
        ref int weights = ref Av1SmoothPredictor.Weights[(int)this.blockHeight];

        int log2Scale = Av1SmoothPredictor.WeightLog2Scale;
        int scale = 1 << Av1SmoothPredictor.WeightLog2Scale;

        // sm_weights_sanity_checks(sm_weights_w, sm_weights_h, scale, log2_scale + 2);
        for (nuint r = 0; r < this.blockHeight; ++r)
        {
            int rowWeight = Unsafe.Add(ref weights, r);
            for (nuint c = 0; c < this.blockWidth; ++c)
            {
                // The Q8 weight decreases toward the bottom edge, shifting influence from top to bottom-left.
                int thisPredition = Unsafe.Add(ref aboveRef, c) * rowWeight;
                thisPredition += belowPrediction * (scale - rowWeight);
                Unsafe.Add(ref destinationRef, c) = (byte)Av1Math.DivideRound(thisPredition, log2Scale);
            }

            destinationRef = ref Unsafe.Add(ref destinationRef, stride);
        }
    }
}
