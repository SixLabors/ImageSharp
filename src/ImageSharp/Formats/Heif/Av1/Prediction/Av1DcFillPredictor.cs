// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

internal class Av1DcFillPredictor : IAv1Predictor
{
    private readonly uint blockWidth;
    private readonly uint blockHeight;

    public Av1DcFillPredictor(Size blockSize)
    {
        this.blockWidth = (uint)blockSize.Width;
        this.blockHeight = (uint)blockSize.Height;
    }

    public Av1DcFillPredictor(Av1TransformSize transformSize)
    {
        this.blockWidth = (uint)transformSize.GetWidth();
        this.blockHeight = (uint)transformSize.GetHeight();
    }

    public static void PredictScalar(Av1TransformSize transformSize, Span<byte> destination, nuint stride, Span<byte> above, Span<byte> left)
        => new Av1DcFillPredictor(transformSize).PredictScalar(destination, stride, above, left);

    public void PredictScalar(Span<byte> destination, nuint stride, Span<byte> above, Span<byte> left)
    {
        const byte expectedDc = 0x80;
        Guard.MustBeGreaterThanOrEqualTo(stride, this.blockWidth, nameof(stride));
        Guard.MustBeSizedAtLeast(destination, (int)this.blockHeight * (int)stride, nameof(destination));
        ref byte destinationRef = ref destination[0];
        for (uint r = 0; r < this.blockHeight; r++)
        {
            Unsafe.InitBlock(ref destinationRef, expectedDc, this.blockWidth);
            destinationRef = ref Unsafe.Add(ref destinationRef, stride);
        }
    }
}