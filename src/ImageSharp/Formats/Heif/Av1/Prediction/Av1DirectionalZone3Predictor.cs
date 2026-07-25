// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

internal class Av1DirectionalZone3Predictor
{
    private readonly nuint blockWidth;
    private readonly nuint blockHeight;

    public Av1DirectionalZone3Predictor(Size blockSize)
    {
        this.blockWidth = (nuint)blockSize.Width;
        this.blockHeight = (nuint)blockSize.Height;
    }

    public Av1DirectionalZone3Predictor(Av1TransformSize transformSize)
    {
        this.blockWidth = (nuint)transformSize.GetWidth();
        this.blockHeight = (nuint)transformSize.GetHeight();
    }

    public static void PredictScalar(Av1TransformSize transformSize, Span<byte> destination, nuint stride, Span<byte> left, bool upsampleAbove, int dx, int dy)
        => new Av1DirectionalZone3Predictor(transformSize).PredictScalar(destination, stride, left, upsampleAbove, dx, dy);

    /// <summary>
    /// SVT: svt_av1_dr_prediction_z3_c
    /// </summary>
    public void PredictScalar(Span<byte> destination, nuint stride, Span<byte> left, bool upsample, int dx, int dy)
    {
        Guard.MustBeGreaterThanOrEqualTo(stride, this.blockWidth, nameof(stride));
        Guard.MustBeSizedAtLeast(left, (int)this.blockHeight, nameof(left));
        Guard.MustBeSizedAtLeast(destination, (int)this.blockHeight * (int)stride, nameof(destination));
        int upsampleLeft = upsample ? 1 : 0;
        ref byte leftRef = ref left[0];
        ref byte destinationRef = ref destination[0];
        Guard.IsTrue(dx == 1, nameof(dx), "Dx expected to be always equal to 1 for directional Zone 3 prediction.");
        Guard.MustBeGreaterThan(dy, 0, nameof(dy));

        int maxBasisY = ((int)this.blockWidth + (int)this.blockHeight - 1) << upsampleLeft;
        int fractionBitCount = 6 - upsampleLeft;
        int basisIncrement = 1 << upsampleLeft;
        int y = dy;
        for (nuint c = 0; c < this.blockWidth; ++c)
        {
            int basis = y >> fractionBitCount;
            int shift = ((y << upsampleLeft) & 0x3F) >> 1;

            for (nuint r = 0; r < this.blockHeight; ++r)
            {
                if (basis < maxBasisY)
                {
                    int val;
                    val = (Unsafe.Add(ref leftRef, basis) * (32 - shift)) + (Unsafe.Add(ref leftRef, basis + 1) * shift);
                    val = Av1Math.RoundPowerOf2(val, 5);
                    Unsafe.Add(ref destinationRef, (r * stride) + c) = (byte)Av1Math.Clamp(val, 0, 255);
                }
                else
                {
                    for (; r < this.blockHeight; ++r)
                    {
                        Unsafe.Add(ref destinationRef, (r * stride) + c) = left[maxBasisY];
                    }

                    break;
                }

                basis += basisIncrement;
            }

            y += dy;
        }
    }
}
