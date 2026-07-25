// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

internal class Av1SmoothHorizontalPredictor : IAv1Predictor
{
    private readonly nuint blockWidth;
    private readonly nuint blockHeight;

    public Av1SmoothHorizontalPredictor(Size blockSize)
    {
        this.blockWidth = (nuint)blockSize.Width;
        this.blockHeight = (nuint)blockSize.Height;
    }

    public Av1SmoothHorizontalPredictor(Av1TransformSize transformSize)
    {
        this.blockWidth = (nuint)transformSize.GetWidth();
        this.blockHeight = (nuint)transformSize.GetHeight();
    }

    public static void PredictScalar(Av1TransformSize transformSize, Span<byte> destination, nuint stride, Span<byte> above, Span<byte> left)
        => new Av1SmoothHorizontalPredictor(transformSize).PredictScalar(destination, stride, above, left);

    /// <summary>
    /// SVT: highbd_smooth_h_predictor
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
        int rightPrediction = Unsafe.Add(ref aboveRef, this.blockWidth - 1); // estimated by top-right pixel
        ref int weights = ref Av1SmoothPredictor.Weights[(int)this.blockWidth];

        // scale = 2 * 2^sm_weight_log2_scale
        int log2Scale = 1 + Av1SmoothPredictor.WeightLog2Scale;
        int scale = 1 << Av1SmoothPredictor.WeightLog2Scale;

        // sm_weights_sanity_checks(sm_weights_w, sm_weights_h, scale, log2_scale + 2);
        for (nuint r = 0; r < this.blockHeight; ++r)
        {
            for (nuint c = 0; c < this.blockWidth; ++c)
            {
                int columnWeight = Unsafe.Add(ref weights, c);
                Guard.MustBeGreaterThanOrEqualTo(scale, columnWeight, nameof(scale));
                int thisPredition = Unsafe.Add(ref leftRef, r) * columnWeight;
                thisPredition += rightPrediction * (scale - columnWeight);
                Unsafe.Add(ref destinationRef, c) = (byte)Av1Math.DivideRound(thisPredition, log2Scale);
            }

            destinationRef = ref Unsafe.Add(ref destinationRef, stride);
        }
    }
}
