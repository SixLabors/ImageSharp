// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

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

    public Av1DcPredictor(Av1TransformSize transformSize)
    {
        this.blockWidth = (uint)transformSize.GetWidth();
        this.blockHeight = (uint)transformSize.GetHeight();
    }

    public static void PredictScalar(Av1TransformSize transformSize, ref byte destination, nuint stride, ref byte above, ref byte left)
        => new Av1DcPredictor(transformSize).PredictScalar(ref destination, stride, ref above, ref left);

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
