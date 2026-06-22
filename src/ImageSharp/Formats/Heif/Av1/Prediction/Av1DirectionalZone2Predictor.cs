// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

internal class Av1DirectionalZone2Predictor
{
    private readonly nuint blockWidth;
    private readonly nuint blockHeight;

    public Av1DirectionalZone2Predictor(Size blockSize)
    {
        this.blockWidth = (nuint)blockSize.Width;
        this.blockHeight = (nuint)blockSize.Height;
    }

    public Av1DirectionalZone2Predictor(Av1TransformSize transformSize)
    {
        this.blockWidth = (nuint)transformSize.GetWidth();
        this.blockHeight = (nuint)transformSize.GetHeight();
    }

    public static void PredictScalar(Av1TransformSize transformSize, Span<byte> destination, nuint stride, Span<byte> above, Span<byte> left, bool upsampleAbove, bool upsampleLeft, int dx, int dy)
        => new Av1DirectionalZone2Predictor(transformSize).PredictScalar(destination, stride, above, left, upsampleAbove, upsampleAbove, dx, dy);

    /// <summary>
    /// SVT: svt_av1_dr_prediction_z1_c
    /// </summary>
    public void PredictScalar(Span<byte> destination, nuint stride, Span<byte> above, Span<byte> left, bool doUpsampleAbove, bool doUpsampleLeft, int dx, int dy)
    {
        Guard.MustBeGreaterThanOrEqualTo(stride, this.blockWidth, nameof(stride));
        Guard.MustBeSizedAtLeast(left, (int)this.blockHeight, nameof(left));
        Guard.MustBeSizedAtLeast(above, (int)this.blockWidth, nameof(above));
        Guard.MustBeSizedAtLeast(destination, (int)this.blockHeight * (int)stride, nameof(destination));
        int upsampleAbove = doUpsampleAbove ? 1 : 0;
        int upsampleLeft = doUpsampleLeft ? 1 : 0;
        ref byte aboveRef = ref above[0];
        ref byte leftRef = ref left[0];
        ref byte destinationRef = ref destination[0];
        int minBasisX = -(1 << upsampleAbove);
        int fractionBitCountX = 6 - upsampleAbove;
        int fractionBitCountY = 6 - upsampleLeft;
        int basisIncrementX = 1 << upsampleAbove;
        int x = -dx;
        for (nuint r = 0; r < this.blockHeight; ++r)
        {
            int val;
            int base1 = x >> fractionBitCountX;
            int y = ((int)r << 6) - dy;
            for (nuint c = 0; c < this.blockWidth; ++c, base1 += basisIncrementX, y -= dy)
            {
                if (base1 >= minBasisX)
                {
                    int shift1 = ((x * (1 << upsampleAbove)) & 0x3F) >> 1;
                    val = (Unsafe.Add(ref aboveRef, base1) * (32 - shift1)) + (Unsafe.Add(ref aboveRef, base1 + 1) * shift1);
                    val = Av1Math.RoundPowerOf2(val, 5);
                }
                else
                {
                    int base2 = y >> fractionBitCountY;
                    Guard.MustBeGreaterThanOrEqualTo(base2, -(1 << upsampleLeft), nameof(base2));
                    int shift2 = ((y * (1 << upsampleLeft)) & 0x3F) >> 1;
                    val = (Unsafe.Add(ref leftRef, base2) * (32 - shift2)) + (Unsafe.Add(ref leftRef, base2 + 1) * shift2);
                    val = Av1Math.RoundPowerOf2(val, 5);
                }

                Unsafe.Add(ref destinationRef, c) = (byte)Av1Math.Clamp(val, 0, 255);
            }

            x -= dx;
            destinationRef = ref Unsafe.Add(ref destinationRef, stride);
        }
    }
}
