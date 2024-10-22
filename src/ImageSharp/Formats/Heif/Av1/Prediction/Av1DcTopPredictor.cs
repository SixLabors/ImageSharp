// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

internal class Av1DcTopPredictor : IAv1Predictor
{
    private readonly uint blockWidth;
    private readonly uint blockHeight;

    public Av1DcTopPredictor(Size blockSize)
    {
        this.blockWidth = (uint)blockSize.Width;
        this.blockHeight = (uint)blockSize.Height;
    }

    public Av1DcTopPredictor(Av1TransformSize transformSize)
    {
        this.blockWidth = (uint)transformSize.GetWidth();
        this.blockHeight = (uint)transformSize.GetHeight();
    }

    public static void PredictScalar(Av1TransformSize transformSize, ref byte destination, nuint stride, ref byte above, ref byte left)
        => new Av1DcTopPredictor(transformSize).PredictScalar(ref destination, stride, ref above, ref left);

    public void PredictScalar(ref byte destination, nuint stride, ref byte above, ref byte left)
    {
        int sum = 0;
        for (uint i = 0; i < this.blockWidth; i++)
        {
            sum += Unsafe.Add(ref above, i);
        }

        byte expectedDc = (byte)((sum + (this.blockWidth >> 1)) / this.blockWidth);
        for (uint r = 0; r < this.blockHeight; r++)
        {
            Unsafe.InitBlock(ref destination, expectedDc, this.blockWidth);
            destination = ref Unsafe.Add(ref destination, stride);
        }
    }
}
