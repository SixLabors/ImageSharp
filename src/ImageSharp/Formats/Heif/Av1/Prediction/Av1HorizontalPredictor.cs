// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

internal class Av1HorizontalPredictor : IAv1Predictor
{
    private readonly nuint blockWidth;
    private readonly nuint blockHeight;

    public Av1HorizontalPredictor(Size blockSize)
    {
        this.blockWidth = (nuint)blockSize.Width;
        this.blockHeight = (nuint)blockSize.Height;
    }

    public Av1HorizontalPredictor(Av1TransformSize transformSize)
    {
        this.blockWidth = (nuint)transformSize.GetWidth();
        this.blockHeight = (nuint)transformSize.GetHeight();
    }

    public static void PredictScalar(Av1TransformSize transformSize, Span<byte> destination, nuint stride, Span<byte> above, Span<byte> left)
        => new Av1HorizontalPredictor(transformSize).PredictScalar(destination, stride, above, left);

    /// <summary>
    /// SVT: highbd_h_predictor
    /// </summary>
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
