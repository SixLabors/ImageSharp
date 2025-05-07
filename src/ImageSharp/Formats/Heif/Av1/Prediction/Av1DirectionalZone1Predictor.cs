// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

internal class Av1DirectionalZone1Predictor
{
    private readonly nuint blockWidth;
    private readonly nuint blockHeight;

    public Av1DirectionalZone1Predictor(Size blockSize)
    {
        this.blockWidth = (nuint)blockSize.Width;
        this.blockHeight = (nuint)blockSize.Height;
    }

    public Av1DirectionalZone1Predictor(Av1TransformSize transformSize)
    {
        this.blockWidth = (nuint)transformSize.GetWidth();
        this.blockHeight = (nuint)transformSize.GetHeight();
    }

    public static void PredictScalar(Av1TransformSize transformSize, Span<byte> destination, nuint stride, Span<byte> above, bool upsampleAbove, int dx)
        => new Av1DirectionalZone1Predictor(transformSize).PredictScalar(destination, stride, above, upsampleAbove, dx);

    /// <summary>
    /// SVT: svt_av1_dr_prediction_z1_c
    /// </summary>
    public void PredictScalar(Span<byte> destination, nuint stride, Span<byte> above, bool upsample, int dx)
    {
        Guard.MustBeGreaterThanOrEqualTo(stride, this.blockWidth, nameof(stride));
        Guard.MustBeSizedAtLeast(destination, (int)this.blockHeight * (int)stride, nameof(destination));
        int upsampleAbove = upsample ? 1 : 0;
        ref byte aboveRef = ref above[0];
        ref byte destinationRef = ref destination[0];
        int maxBasisX = (((int)this.blockWidth + (int)this.blockHeight) - 1) << upsampleAbove;
        int fractionBitCount = 6 - upsampleAbove;
        int basisIncrement = 1 << upsampleAbove;
        int x = dx;
        for (nuint r = 0; r < this.blockHeight; ++r)
        {
            int basis = x >> fractionBitCount, shift = ((x << upsampleAbove) & 0x3F) >> 1;

            if (basis >= maxBasisX)
            {
                for (nuint i = r; i < this.blockHeight; ++i)
                {
                    Unsafe.InitBlock(ref destinationRef, Unsafe.Add(ref aboveRef, maxBasisX), (uint)this.blockWidth);
                    destinationRef = ref Unsafe.Add(ref destinationRef, stride);
                }

                return;
            }

            for (nuint c = 0; c < this.blockWidth; ++c)
            {
                if (basis < maxBasisX)
                {
                    int val;
                    val = (Unsafe.Add(ref aboveRef, basis) * (32 - shift)) + (Unsafe.Add(ref aboveRef, basis + 1) * shift);
                    val = Av1Math.RoundPowerOf2(val, 5);
                    Unsafe.Add(ref destinationRef, c) = (byte)Av1Math.Clamp(val, 0, 255);
                }
                else
                {
                    Unsafe.Add(ref destinationRef, c) = Unsafe.Add(ref aboveRef, maxBasisX);
                }

                basis += basisIncrement;
            }

            x += dx;
            destinationRef = ref Unsafe.Add(ref destinationRef, stride);
        }
    }
}
