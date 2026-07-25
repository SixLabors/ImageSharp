// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

internal class Av1SmoothVerticalPredictor : IAv1Predictor
{
    private readonly nuint blockWidth;
    private readonly nuint blockHeight;

    public Av1SmoothVerticalPredictor(Size blockSize)
    {
        this.blockWidth = (nuint)blockSize.Width;
        this.blockHeight = (nuint)blockSize.Height;
    }

    public Av1SmoothVerticalPredictor(Av1TransformSize transformSize)
    {
        this.blockWidth = (nuint)transformSize.GetWidth();
        this.blockHeight = (nuint)transformSize.GetHeight();
    }

    public static void PredictScalar(Av1TransformSize transformSize, Span<byte> destination, nuint stride, Span<byte> above, Span<byte> left)
        => new Av1SmoothVerticalPredictor(transformSize).PredictScalar(destination, stride, above, left);

    /// <summary>
    /// SVT: highbd_smooth_v_predictor
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
        ref int weights = ref Av1SmoothPredictor.Weights[(int)this.blockHeight];

        // scale = 2 * 2^sm_weight_log2_scale
        int log2Scale = 1 + Av1SmoothPredictor.WeightLog2Scale;
        int scale = 1 << Av1SmoothPredictor.WeightLog2Scale;

        // sm_weights_sanity_checks(sm_weights_w, sm_weights_h, scale, log2_scale + 2);
        for (nuint r = 0; r < this.blockHeight; ++r)
        {
            int rowWeight = Unsafe.Add(ref weights, r);
            for (nuint c = 0; c < this.blockWidth; ++c)
            {
                int thisPredition = Unsafe.Add(ref aboveRef, c) * rowWeight;
                thisPredition += belowPrediction * (scale - rowWeight);
                Unsafe.Add(ref destinationRef, c) = (byte)Av1Math.DivideRound(thisPredition, log2Scale);
            }

            destinationRef = ref Unsafe.Add(ref destinationRef, stride);
        }
    }
}
