// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

internal class Av1PaethPredictor : IAv1Predictor
{
    private readonly uint blockWidth;
    private readonly uint blockHeight;

    public Av1PaethPredictor(Size blockSize)
    {
        this.blockWidth = (uint)blockSize.Width;
        this.blockHeight = (uint)blockSize.Height;
    }

    public Av1PaethPredictor(Av1TransformSize transformSize)
    {
        this.blockWidth = (uint)transformSize.GetWidth();
        this.blockHeight = (uint)transformSize.GetHeight();
    }

    public static void PredictScalar(Av1TransformSize transformSize, Span<byte> destination, nuint stride, Span<byte> above, Span<byte> left)
        => new Av1DcPredictor(transformSize).PredictScalar(destination, stride, above, left);

    public void PredictScalar(Span<byte> destination, nuint stride, Span<byte> above, Span<byte> left)
    {
        Guard.MustBeGreaterThanOrEqualTo(stride, this.blockWidth, nameof(stride));
        Guard.MustBeSizedAtLeast(left, (int)this.blockHeight, nameof(left));
        Guard.MustBeSizedAtLeast(above, (int)this.blockWidth, nameof(above));
        Guard.MustBeSizedAtLeast(destination, (int)this.blockHeight * (int)stride, nameof(destination));
        ref byte leftRef = ref left[0];
        ref byte aboveRef = ref above[0];
        int yTopLeft = above[-1];
        ref byte destinationRef = ref destination[0];
        for (nuint r = 0; r < this.blockHeight; r++)
        {
            for (nuint c = 0; c < this.blockWidth; c++)
            {
                destinationRef = PredictSingle(Unsafe.Add(ref leftRef, r), Unsafe.Add(ref aboveRef, c), yTopLeft);
                destinationRef = ref Unsafe.Add(ref destinationRef, stride);
            }
        }
    }

    private static byte PredictSingle(byte left, byte top, int topLeft)
    {
        int basis = top + left - topLeft;
        int pLeft = Av1Math.AbsoluteDifference(basis, left);
        int pTop = Av1Math.AbsoluteDifference(basis, top);
        int pTopLeft = Av1Math.AbsoluteDifference(basis, topLeft);

        // Return nearest to base of left, top and top_left.
        return (byte)((pLeft <= pTop && pLeft <= pTopLeft) ? left : (pTop <= pTopLeft) ? top : topLeft);
    }
}
