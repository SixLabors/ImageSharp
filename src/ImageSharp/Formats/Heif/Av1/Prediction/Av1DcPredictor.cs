// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

internal class Av1DcPredictor : IAv1Predictor
{
    private readonly uint blockWidth;
    private readonly uint blockHeight;

    public Av1DcPredictor(Size blockSize)
    {
        this.blockWidth = (uint)blockSize.Width;
        this.blockHeight = (uint)blockSize.Height;
    }

    public void PredictScalar(ref byte destination, nuint stride, ref byte above, ref byte left)
    {
        int sum = 0;
        uint count = this.blockWidth + this.blockHeight;
        for (uint i = 0; i < this.blockWidth; i++)
        {
            sum += Unsafe.Add(ref above, i);
        }

        for (uint i = 0; i < this.blockHeight; i++)
        {
            sum += Unsafe.Add(ref left, i);
        }

        byte expectedDc = (byte)((sum + (count >> 1)) / count);
        for (uint r = 0; r < this.blockHeight; r++)
        {
            Unsafe.InitBlock(ref destination, expectedDc, this.blockWidth);
            destination = ref Unsafe.Add(ref destination, stride);
        }
    }
}
