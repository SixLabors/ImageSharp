// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;

// ReSharper disable UseObjectOrCollectionInitializer
// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

internal partial struct Block8x8F
{
    [MethodImpl(InliningOptions.ShortMethod)]
    public void ScaledCopyFrom(ref float areaOrigin, int areaStride) =>
        CopyFrom1x1Scale(ref Unsafe.As<float, byte>(ref areaOrigin), ref Unsafe.As<Block8x8F, byte>(ref this), areaStride);

    [MethodImpl(InliningOptions.ShortMethod)]
    public void ScaledCopyTo(ref float areaOrigin, int areaStride, int horizontalScale, int verticalScale)
    {
        if (horizontalScale == 1 && verticalScale == 1)
        {
            CopyTo1x1Scale(ref Unsafe.As<Block8x8F, byte>(ref this), ref Unsafe.As<float, byte>(ref areaOrigin), areaStride);
            return;
        }

        if (horizontalScale == 2 && verticalScale == 2)
        {
            this.CopyTo2x2Scale(ref areaOrigin, areaStride);
            return;
        }

        // TODO: Optimize: implement all cases with scale-specific, loopless code!
        this.CopyArbitraryScale(ref areaOrigin, (uint)areaStride, (uint)horizontalScale, (uint)verticalScale);
    }

    private void CopyTo2x2Scale(ref float areaOrigin, int areaStride)
    {
        ref Vector2 destBase = ref Unsafe.As<float, Vector2>(ref areaOrigin);
        nuint destStride = (uint)areaStride / 2;

        WidenCopyRowImpl2x2(ref this.V0L, ref destBase, 0, destStride);
        WidenCopyRowImpl2x2(ref this.V0L, ref destBase, 1, destStride);
        WidenCopyRowImpl2x2(ref this.V0L, ref destBase, 2, destStride);
        WidenCopyRowImpl2x2(ref this.V0L, ref destBase, 3, destStride);
        WidenCopyRowImpl2x2(ref this.V0L, ref destBase, 4, destStride);
        WidenCopyRowImpl2x2(ref this.V0L, ref destBase, 5, destStride);
        WidenCopyRowImpl2x2(ref this.V0L, ref destBase, 6, destStride);
        WidenCopyRowImpl2x2(ref this.V0L, ref destBase, 7, destStride);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void WidenCopyRowImpl2x2(ref Vector4 selfBase, ref Vector2 destBase, nuint row, nuint destStride)
        {
            ref Vector4 sLeft = ref Unsafe.Add(ref selfBase, 2 * row);
            ref Vector4 sRight = ref Unsafe.Add(ref sLeft, 1);

            nuint offset = 2 * row * destStride;
            ref Vector4 dTopLeft = ref Unsafe.As<Vector2, Vector4>(ref Unsafe.Add(ref destBase, offset));
            ref Vector4 dBottomLeft = ref Unsafe.As<Vector2, Vector4>(ref Unsafe.Add(ref destBase, offset + destStride));

            Vector4 xyLeft = new(sLeft.X);
            xyLeft.Z = sLeft.Y;
            xyLeft.W = sLeft.Y;

            Vector4 zwLeft = new(sLeft.Z);
            zwLeft.Z = sLeft.W;
            zwLeft.W = sLeft.W;

            Vector4 xyRight = new(sRight.X);
            xyRight.Z = sRight.Y;
            xyRight.W = sRight.Y;

            Vector4 zwRight = new(sRight.Z);
            zwRight.Z = sRight.W;
            zwRight.W = sRight.W;

            dTopLeft = xyLeft;
            Unsafe.Add(ref dTopLeft, 1) = zwLeft;
            Unsafe.Add(ref dTopLeft, 2) = xyRight;
            Unsafe.Add(ref dTopLeft, 3) = zwRight;

            dBottomLeft = xyLeft;
            Unsafe.Add(ref dBottomLeft, 1) = zwLeft;
            Unsafe.Add(ref dBottomLeft, 2) = xyRight;
            Unsafe.Add(ref dBottomLeft, 3) = zwRight;
        }
    }

    [MethodImpl(InliningOptions.ColdPath)]
    private void CopyArbitraryScale(ref float areaOrigin, uint areaStride, uint horizontalScale, uint verticalScale)
    {
        for (nuint y = 0; y < 8; y++)
        {
            nuint yy = y * verticalScale;
            nuint y8 = y * 8;

            for (nuint x = 0; x < 8; x++)
            {
                nuint xx = x * horizontalScale;

                float value = this[(int)(y8 + x)];
                nuint baseIdx = (yy * areaStride) + xx;

                for (nuint i = 0; i < verticalScale; i++, baseIdx += areaStride)
                {
                    for (nuint j = 0; j < horizontalScale; j++)
                    {
                        // area[xx + j, yy + i] = value;
                        Unsafe.Add(ref areaOrigin, baseIdx + j) = value;
                    }
                }
            }
        }
    }

    private static void CopyTo1x1Scale(ref byte origin, ref byte dest, int areaStride)
    {
        int destStride = areaStride * sizeof(float);

        CopyRowImpl(ref origin, ref dest, destStride, 0);
        CopyRowImpl(ref origin, ref dest, destStride, 1);
        CopyRowImpl(ref origin, ref dest, destStride, 2);
        CopyRowImpl(ref origin, ref dest, destStride, 3);
        CopyRowImpl(ref origin, ref dest, destStride, 4);
        CopyRowImpl(ref origin, ref dest, destStride, 5);
        CopyRowImpl(ref origin, ref dest, destStride, 6);
        CopyRowImpl(ref origin, ref dest, destStride, 7);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void CopyRowImpl(ref byte origin, ref byte dest, int destStride, int row)
        {
            origin = ref Unsafe.Add(ref origin, (uint)row * 8 * sizeof(float));
            dest = ref Unsafe.Add(ref dest, (uint)(row * destStride));
            Unsafe.CopyBlock(ref dest, ref origin, 8 * sizeof(float));
        }
    }

    private static void CopyFrom1x1Scale(ref byte origin, ref byte dest, int areaStride)
    {
        int destStride = areaStride * sizeof(float);

        CopyRowImpl(ref origin, ref dest, destStride, 0);
        CopyRowImpl(ref origin, ref dest, destStride, 1);
        CopyRowImpl(ref origin, ref dest, destStride, 2);
        CopyRowImpl(ref origin, ref dest, destStride, 3);
        CopyRowImpl(ref origin, ref dest, destStride, 4);
        CopyRowImpl(ref origin, ref dest, destStride, 5);
        CopyRowImpl(ref origin, ref dest, destStride, 6);
        CopyRowImpl(ref origin, ref dest, destStride, 7);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void CopyRowImpl(ref byte origin, ref byte dest, int sourceStride, int row)
        {
            origin = ref Unsafe.Add(ref origin, (uint)(row * sourceStride));
            dest = ref Unsafe.Add(ref dest, (uint)row * 8 * sizeof(float));
            Unsafe.CopyBlock(ref dest, ref origin, 8 * sizeof(float));
        }
    }
}
