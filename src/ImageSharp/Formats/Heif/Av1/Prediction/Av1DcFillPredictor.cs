// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

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

    public void PredictScalar(ref byte destination, nuint stride, ref byte above, ref byte left)
    {
        const byte expectedDc = 0x80;
        for (uint r = 0; r < this.blockHeight; r++)
        {
            Unsafe.InitBlock(ref destination, expectedDc, this.blockWidth);
            destination = ref Unsafe.Add(ref destination, stride);
        }
    }
}
