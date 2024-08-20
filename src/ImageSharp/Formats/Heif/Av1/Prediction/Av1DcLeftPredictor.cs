// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

internal class Av1DcLeftPredictor : IAv1Predictor
{
    private readonly uint blockWidth;
    private readonly uint blockHeight;

    public Av1DcLeftPredictor(Size blockSize)
    {
        this.blockWidth = (uint)blockSize.Width;
        this.blockHeight = (uint)blockSize.Height;
    }

    public void PredictScalar(ref byte destination, nuint stride, ref byte above, ref byte left)
    {
        int sum = 0;
        for (uint i = 0; i < this.blockHeight; i++)
        {
            sum += Unsafe.Add(ref left, i);
        }

        byte expectedDc = (byte)((sum + (this.blockHeight >> 1)) / this.blockHeight);
        for (uint r = 0; r < this.blockHeight; r++)
        {
            Unsafe.InitBlock(ref destination, expectedDc, this.blockWidth);
            destination = ref Unsafe.Add(ref destination, stride);
        }
    }
}
