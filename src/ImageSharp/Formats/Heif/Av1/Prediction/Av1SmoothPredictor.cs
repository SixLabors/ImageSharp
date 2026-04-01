// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

internal class Av1SmoothPredictor : IAv1Predictor
{
    // Weights are quadratic from '1' to '1 / BlockSize', scaled by
    // 2^sm_weight_log2_scale.
    internal static readonly int WeightLog2Scale = 8;

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

    private readonly nuint blockWidth;
    private readonly nuint blockHeight;

    public Av1SmoothPredictor(Size blockSize)
    {
        this.blockWidth = (nuint)blockSize.Width;
        this.blockHeight = (nuint)blockSize.Height;
    }

    public Av1SmoothPredictor(Av1TransformSize transformSize)
    {
        this.blockWidth = (nuint)transformSize.GetWidth();
        this.blockHeight = (nuint)transformSize.GetHeight();
    }

    public static void PredictScalar(Av1TransformSize transformSize, Span<byte> destination, nuint stride, Span<byte> above, Span<byte> left)
        => new Av1SmoothPredictor(transformSize).PredictScalar(destination, stride, above, left);

    /// <summary>
    /// SVT: highbd_smooth_predictor
    /// </summary>
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
        ref int heightWeights = ref Weights[(int)this.blockWidth];
        ref int widthWeights = ref Weights[(int)this.blockHeight];

        // scale = 2 * 2^sm_weight_log2_scale
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
