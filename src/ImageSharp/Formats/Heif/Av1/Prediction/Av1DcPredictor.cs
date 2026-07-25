// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

internal class Av1DcPredictor : IAv1Predictor
{
    private readonly nuint blockWidth;
    private readonly nuint blockHeight;

    public Av1DcPredictor(Size blockSize)
    {
        this.blockWidth = (nuint)blockSize.Width;
        this.blockHeight = (nuint)blockSize.Height;
    }

    public Av1DcPredictor(Av1TransformSize transformSize)
    {
        this.blockWidth = (nuint)transformSize.GetWidth();
        this.blockHeight = (nuint)transformSize.GetHeight();
    }

    public static void PredictScalar(Av1TransformSize transformSize, Span<byte> destination, nuint stride, Span<byte> above, Span<byte> left)
        => new Av1DcPredictor(transformSize).PredictScalar(destination, stride, above, left);

    public void PredictScalar(Span<byte> destination, nuint stride, Span<byte> above, Span<byte> left)
    {
        int sum = 0;
        Guard.MustBeGreaterThanOrEqualTo(stride, this.blockWidth, nameof(stride));
        Guard.MustBeSizedAtLeast(left, (int)this.blockHeight, nameof(left));
        Guard.MustBeSizedAtLeast(above, (int)this.blockWidth, nameof(above));
        Guard.MustBeSizedAtLeast(destination, (int)this.blockHeight * (int)stride, nameof(destination));
        ref byte leftRef = ref left[0];
        ref byte aboveRef = ref above[0];
        ref byte destinationRef = ref destination[0];
        uint count = (uint)(this.blockWidth + this.blockHeight);
        uint width = (uint)this.blockWidth;
        for (nuint i = 0; i < this.blockWidth; i++)
        {
            sum += Unsafe.Add(ref aboveRef, i);
        }

        for (nuint i = 0; i < this.blockHeight; i++)
        {
            sum += Unsafe.Add(ref leftRef, i);
        }

        byte expectedDc = (byte)((sum + (count >> 1)) / count);
        for (nuint r = 0; r < this.blockHeight; r++)
        {
            Unsafe.InitBlock(ref destinationRef, expectedDc, width);
            destinationRef = ref Unsafe.Add(ref destinationRef, stride);
        }
    }
}
