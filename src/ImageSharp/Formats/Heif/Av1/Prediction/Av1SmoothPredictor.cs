// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

/// <summary>
/// Predicts an 8-bit AV1 block by blending top-to-bottom and left-to-right smooth interpolation surfaces.
/// </summary>
internal readonly struct Av1SmoothPredictor : IAv1Predictor
{
    // Weights are quadratic from '1' to '1 / BlockSize', scaled by
    // 2^sm_weight_log2_scale.

    /// <summary>
    /// The number of fractional bits in the normative smooth-prediction weights.
    /// </summary>
    internal static readonly int WeightLog2Scale = 8;

    /// <summary>
    /// The concatenated smooth-weight sequences, addressed by using the block dimension as the sequence offset.
    /// </summary>
    internal static readonly int[] Weights = [

        // Unused, because we always offset by bs, which is at least 2.
        0, 0,

        // bs = 2
        255, 128,

        // bs = 4
        255, 149, 85, 64,

        // bs = 8
        255, 197, 146, 105, 73, 50, 37, 32,

        // bs = 16
        255, 225, 196, 170, 145, 123, 102, 84, 68, 54, 43, 33, 26, 20, 17, 16,

        // bs = 32
        255, 240, 225, 210, 196, 182, 169, 157, 145, 133, 122, 111, 101, 92, 83, 74,
        66, 59, 52, 45, 39, 34, 29, 25, 21, 17, 14, 12, 10, 9, 8, 8,

        // bs = 64
        255, 248, 240, 233, 225, 218, 210, 203, 196, 189, 182, 176, 169, 163, 156,
        150, 144, 138, 133, 127, 121, 116, 111, 106, 101, 96, 91, 86, 82, 77, 73, 69,
        65, 61, 57, 54, 50, 47, 44, 41, 38, 35, 32, 29, 27, 25, 22, 20, 18, 16, 15,
        13, 12, 10, 9, 8, 7, 6, 6, 5, 5, 4, 4, 4,
    ];

    /// <summary>
    /// The number of top samples consumed and samples written to each destination row.
    /// </summary>
    private readonly nuint blockWidth;

    /// <summary>
    /// The number of left samples consumed and destination rows written.
    /// </summary>
    private readonly nuint blockHeight;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1SmoothPredictor"/> struct for explicit block dimensions.
    /// </summary>
    /// <param name="blockSize">The predicted block dimensions in samples.</param>
    public Av1SmoothPredictor(Size blockSize)
    {
        this.blockWidth = (nuint)blockSize.Width;
        this.blockHeight = (nuint)blockSize.Height;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1SmoothPredictor"/> struct for a transform size.
    /// </summary>
    /// <param name="transformSize">The transform size whose dimensions define the predicted block.</param>
    public Av1SmoothPredictor(Av1TransformSize transformSize)
    {
        this.blockWidth = (nuint)transformSize.GetWidth();
        this.blockHeight = (nuint)transformSize.GetHeight();
    }

    /// <summary>
    /// Predicts a transform block by combining horizontal and vertical smooth interpolation.
    /// </summary>
    /// <param name="transformSize">The predicted block dimensions.</param>
    /// <param name="destination">The destination block.</param>
    /// <param name="stride">The distance, in samples, between destination rows.</param>
    /// <param name="above">The top neighboring samples.</param>
    /// <param name="left">The left neighboring samples.</param>
    public static void PredictScalar(Av1TransformSize transformSize, Span<byte> destination, nuint stride, Span<byte> above, Span<byte> left)
        => new Av1SmoothPredictor(transformSize).PredictScalar(destination, stride, above, left);

    /// <inheritdoc/>
    /// <remarks>SVT-AV1: <c>highbd_smooth_predictor</c>.</remarks>
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
        int rightPrediction = Unsafe.Add(ref aboveRef, this.blockWidth - 1); // estimated by top-right pixel
        ref int heightWeights = ref Weights[(int)this.blockHeight];
        ref int widthWeights = ref Weights[(int)this.blockWidth];

        // The two independent interpolation surfaces each use Q8 weights, so their combined sum has one extra scale bit.
        int log2Scale = 1 + WeightLog2Scale;
        int scale = 1 << WeightLog2Scale;

        // sm_weights_sanity_checks(sm_weights_w, sm_weights_h, scale, log2_scale + 2);
        for (nuint r = 0; r < this.blockHeight; ++r)
        {
            int rowWeight = Unsafe.Add(ref heightWeights, r);
            Guard.MustBeGreaterThanOrEqualTo(scale, rowWeight, nameof(scale));
            for (nuint c = 0; c < this.blockWidth; ++c)
            {
                int columnWeight = Unsafe.Add(ref widthWeights, c);
                Guard.MustBeGreaterThanOrEqualTo(scale, columnWeight, nameof(scale));

                // Blend top toward bottom-left and left toward top-right, then normalize their combined Q8 contributions.
                int thisPredition = Unsafe.Add(ref aboveRef, c) * rowWeight;
                thisPredition += belowPrediction * (scale - rowWeight);
                thisPredition += Unsafe.Add(ref leftRef, r) * columnWeight;
                thisPredition += rightPrediction * (scale - columnWeight);
                Unsafe.Add(ref destinationRef, c) = (byte)Av1Math.DivideRound(thisPredition, log2Scale);
            }

            destinationRef = ref Unsafe.Add(ref destinationRef, stride);
        }
    }
}
