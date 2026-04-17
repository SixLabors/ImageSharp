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

        if (horizontalScale == 2 && verticalScale == 1)
        {
            this.CopyTo2x1Scale(ref areaOrigin, (uint)areaStride);
            return;
        }

        if (horizontalScale == 1 && verticalScale == 2)
        {
            this.CopyTo1x2Scale(ref areaOrigin, (uint)areaStride);
            return;
        }

        if (horizontalScale == 4 && verticalScale == 1)
        {
            this.CopyTo4x1Scale(ref areaOrigin, (uint)areaStride);
            return;
        }

        if (horizontalScale == 4 && verticalScale == 2)
        {
            this.CopyTo4x2Scale(ref areaOrigin, (uint)areaStride);
            return;
        }

        if (horizontalScale == 1 && verticalScale == 4)
        {
            this.CopyTo1x4Scale(ref areaOrigin, (uint)areaStride);
            return;
        }

        if (horizontalScale == 2 && verticalScale == 4)
        {
            this.CopyTo2x4Scale(ref areaOrigin, (uint)areaStride);
            return;
        }

        if (horizontalScale == 4 && verticalScale == 4)
        {
            this.CopyTo4x4Scale(ref areaOrigin, (uint)areaStride);
            return;
        }

        // The common 1x, 2x, and 4x integral scales are specialized above.
        // Uncommon legal factor-3 scales use the generic fallback.
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

    /// <summary>
    /// Copies the full 8x8 block into the destination buffer while doubling only the horizontal axis.
    /// </summary>
    [MethodImpl(InliningOptions.ShortMethod)]
    private void CopyTo2x1Scale(ref float areaOrigin, uint areaStride)
    {
        ref Vector4 sourceBase = ref this.V0L;

        WidenRow8(ref sourceBase, ref areaOrigin, 0u, 0u, areaStride);
        WidenRow8(ref sourceBase, ref areaOrigin, 1u, 1u, areaStride);
        WidenRow8(ref sourceBase, ref areaOrigin, 2u, 2u, areaStride);
        WidenRow8(ref sourceBase, ref areaOrigin, 3u, 3u, areaStride);
        WidenRow8(ref sourceBase, ref areaOrigin, 4u, 4u, areaStride);
        WidenRow8(ref sourceBase, ref areaOrigin, 5u, 5u, areaStride);
        WidenRow8(ref sourceBase, ref areaOrigin, 6u, 6u, areaStride);
        WidenRow8(ref sourceBase, ref areaOrigin, 7u, 7u, areaStride);
    }

    /// <summary>
    /// Copies the full 8x8 block into the destination buffer while doubling only the vertical axis.
    /// </summary>
    [MethodImpl(InliningOptions.ShortMethod)]
    private void CopyTo1x2Scale(ref float areaOrigin, uint areaStride)
    {
        ref Vector4 sourceBase = ref this.V0L;

        CopyRow8(ref sourceBase, ref areaOrigin, 0u, 0u, areaStride);
        CopyRow8(ref sourceBase, ref areaOrigin, 0u, 1u, areaStride);
        CopyRow8(ref sourceBase, ref areaOrigin, 1u, 2u, areaStride);
        CopyRow8(ref sourceBase, ref areaOrigin, 1u, 3u, areaStride);
        CopyRow8(ref sourceBase, ref areaOrigin, 2u, 4u, areaStride);
        CopyRow8(ref sourceBase, ref areaOrigin, 2u, 5u, areaStride);
        CopyRow8(ref sourceBase, ref areaOrigin, 3u, 6u, areaStride);
        CopyRow8(ref sourceBase, ref areaOrigin, 3u, 7u, areaStride);
        CopyRow8(ref sourceBase, ref areaOrigin, 4u, 8u, areaStride);
        CopyRow8(ref sourceBase, ref areaOrigin, 4u, 9u, areaStride);
        CopyRow8(ref sourceBase, ref areaOrigin, 5u, 10u, areaStride);
        CopyRow8(ref sourceBase, ref areaOrigin, 5u, 11u, areaStride);
        CopyRow8(ref sourceBase, ref areaOrigin, 6u, 12u, areaStride);
        CopyRow8(ref sourceBase, ref areaOrigin, 6u, 13u, areaStride);
        CopyRow8(ref sourceBase, ref areaOrigin, 7u, 14u, areaStride);
        CopyRow8(ref sourceBase, ref areaOrigin, 7u, 15u, areaStride);
    }

    /// <summary>
    /// Copies the full 8x8 block into the destination buffer while quadrupling only the horizontal axis.
    /// </summary>
    [MethodImpl(InliningOptions.ShortMethod)]
    private void CopyTo4x1Scale(ref float areaOrigin, uint areaStride)
    {
        ref Vector4 sourceBase = ref this.V0L;

        ExpandRow8(ref sourceBase, ref areaOrigin, 0u, 0u, areaStride);
        ExpandRow8(ref sourceBase, ref areaOrigin, 1u, 1u, areaStride);
        ExpandRow8(ref sourceBase, ref areaOrigin, 2u, 2u, areaStride);
        ExpandRow8(ref sourceBase, ref areaOrigin, 3u, 3u, areaStride);
        ExpandRow8(ref sourceBase, ref areaOrigin, 4u, 4u, areaStride);
        ExpandRow8(ref sourceBase, ref areaOrigin, 5u, 5u, areaStride);
        ExpandRow8(ref sourceBase, ref areaOrigin, 6u, 6u, areaStride);
        ExpandRow8(ref sourceBase, ref areaOrigin, 7u, 7u, areaStride);
    }

    /// <summary>
    /// Copies the full 8x8 block into the destination buffer while quadrupling horizontally and doubling vertically.
    /// </summary>
    [MethodImpl(InliningOptions.ShortMethod)]
    private void CopyTo4x2Scale(ref float areaOrigin, uint areaStride)
    {
        ref Vector4 sourceBase = ref this.V0L;

        ExpandRow8(ref sourceBase, ref areaOrigin, 0u, 0u, areaStride);
        ExpandRow8(ref sourceBase, ref areaOrigin, 0u, 1u, areaStride);
        ExpandRow8(ref sourceBase, ref areaOrigin, 1u, 2u, areaStride);
        ExpandRow8(ref sourceBase, ref areaOrigin, 1u, 3u, areaStride);
        ExpandRow8(ref sourceBase, ref areaOrigin, 2u, 4u, areaStride);
        ExpandRow8(ref sourceBase, ref areaOrigin, 2u, 5u, areaStride);
        ExpandRow8(ref sourceBase, ref areaOrigin, 3u, 6u, areaStride);
        ExpandRow8(ref sourceBase, ref areaOrigin, 3u, 7u, areaStride);
        ExpandRow8(ref sourceBase, ref areaOrigin, 4u, 8u, areaStride);
        ExpandRow8(ref sourceBase, ref areaOrigin, 4u, 9u, areaStride);
        ExpandRow8(ref sourceBase, ref areaOrigin, 5u, 10u, areaStride);
        ExpandRow8(ref sourceBase, ref areaOrigin, 5u, 11u, areaStride);
        ExpandRow8(ref sourceBase, ref areaOrigin, 6u, 12u, areaStride);
        ExpandRow8(ref sourceBase, ref areaOrigin, 6u, 13u, areaStride);
        ExpandRow8(ref sourceBase, ref areaOrigin, 7u, 14u, areaStride);
        ExpandRow8(ref sourceBase, ref areaOrigin, 7u, 15u, areaStride);
    }

    /// <summary>
    /// Copies the full 8x8 block into the destination buffer while quadrupling only the vertical axis.
    /// </summary>
    [MethodImpl(InliningOptions.ShortMethod)]
    private void CopyTo1x4Scale(ref float areaOrigin, uint areaStride)
    {
        ref Vector4 sourceBase = ref this.V0L;

        CopyRow8(ref sourceBase, ref areaOrigin, 0u, 0u, areaStride);
        CopyRow8(ref sourceBase, ref areaOrigin, 0u, 1u, areaStride);
        CopyRow8(ref sourceBase, ref areaOrigin, 0u, 2u, areaStride);
        CopyRow8(ref sourceBase, ref areaOrigin, 0u, 3u, areaStride);
        CopyRow8(ref sourceBase, ref areaOrigin, 1u, 4u, areaStride);
        CopyRow8(ref sourceBase, ref areaOrigin, 1u, 5u, areaStride);
        CopyRow8(ref sourceBase, ref areaOrigin, 1u, 6u, areaStride);
        CopyRow8(ref sourceBase, ref areaOrigin, 1u, 7u, areaStride);
        CopyRow8(ref sourceBase, ref areaOrigin, 2u, 8u, areaStride);
        CopyRow8(ref sourceBase, ref areaOrigin, 2u, 9u, areaStride);
        CopyRow8(ref sourceBase, ref areaOrigin, 2u, 10u, areaStride);
        CopyRow8(ref sourceBase, ref areaOrigin, 2u, 11u, areaStride);
        CopyRow8(ref sourceBase, ref areaOrigin, 3u, 12u, areaStride);
        CopyRow8(ref sourceBase, ref areaOrigin, 3u, 13u, areaStride);
        CopyRow8(ref sourceBase, ref areaOrigin, 3u, 14u, areaStride);
        CopyRow8(ref sourceBase, ref areaOrigin, 3u, 15u, areaStride);
        CopyRow8(ref sourceBase, ref areaOrigin, 4u, 16u, areaStride);
        CopyRow8(ref sourceBase, ref areaOrigin, 4u, 17u, areaStride);
        CopyRow8(ref sourceBase, ref areaOrigin, 4u, 18u, areaStride);
        CopyRow8(ref sourceBase, ref areaOrigin, 4u, 19u, areaStride);
        CopyRow8(ref sourceBase, ref areaOrigin, 5u, 20u, areaStride);
        CopyRow8(ref sourceBase, ref areaOrigin, 5u, 21u, areaStride);
        CopyRow8(ref sourceBase, ref areaOrigin, 5u, 22u, areaStride);
        CopyRow8(ref sourceBase, ref areaOrigin, 5u, 23u, areaStride);
        CopyRow8(ref sourceBase, ref areaOrigin, 6u, 24u, areaStride);
        CopyRow8(ref sourceBase, ref areaOrigin, 6u, 25u, areaStride);
        CopyRow8(ref sourceBase, ref areaOrigin, 6u, 26u, areaStride);
        CopyRow8(ref sourceBase, ref areaOrigin, 6u, 27u, areaStride);
        CopyRow8(ref sourceBase, ref areaOrigin, 7u, 28u, areaStride);
        CopyRow8(ref sourceBase, ref areaOrigin, 7u, 29u, areaStride);
        CopyRow8(ref sourceBase, ref areaOrigin, 7u, 30u, areaStride);
        CopyRow8(ref sourceBase, ref areaOrigin, 7u, 31u, areaStride);
    }

    /// <summary>
    /// Copies the full 8x8 block into the destination buffer while doubling horizontally and quadrupling vertically.
    /// </summary>
    [MethodImpl(InliningOptions.ShortMethod)]
    private void CopyTo2x4Scale(ref float areaOrigin, uint areaStride)
    {
        ref Vector4 sourceBase = ref this.V0L;

        WidenRow8(ref sourceBase, ref areaOrigin, 0u, 0u, areaStride);
        WidenRow8(ref sourceBase, ref areaOrigin, 0u, 1u, areaStride);
        WidenRow8(ref sourceBase, ref areaOrigin, 0u, 2u, areaStride);
        WidenRow8(ref sourceBase, ref areaOrigin, 0u, 3u, areaStride);
        WidenRow8(ref sourceBase, ref areaOrigin, 1u, 4u, areaStride);
        WidenRow8(ref sourceBase, ref areaOrigin, 1u, 5u, areaStride);
        WidenRow8(ref sourceBase, ref areaOrigin, 1u, 6u, areaStride);
        WidenRow8(ref sourceBase, ref areaOrigin, 1u, 7u, areaStride);
        WidenRow8(ref sourceBase, ref areaOrigin, 2u, 8u, areaStride);
        WidenRow8(ref sourceBase, ref areaOrigin, 2u, 9u, areaStride);
        WidenRow8(ref sourceBase, ref areaOrigin, 2u, 10u, areaStride);
        WidenRow8(ref sourceBase, ref areaOrigin, 2u, 11u, areaStride);
        WidenRow8(ref sourceBase, ref areaOrigin, 3u, 12u, areaStride);
        WidenRow8(ref sourceBase, ref areaOrigin, 3u, 13u, areaStride);
        WidenRow8(ref sourceBase, ref areaOrigin, 3u, 14u, areaStride);
        WidenRow8(ref sourceBase, ref areaOrigin, 3u, 15u, areaStride);
        WidenRow8(ref sourceBase, ref areaOrigin, 4u, 16u, areaStride);
        WidenRow8(ref sourceBase, ref areaOrigin, 4u, 17u, areaStride);
        WidenRow8(ref sourceBase, ref areaOrigin, 4u, 18u, areaStride);
        WidenRow8(ref sourceBase, ref areaOrigin, 4u, 19u, areaStride);
        WidenRow8(ref sourceBase, ref areaOrigin, 5u, 20u, areaStride);
        WidenRow8(ref sourceBase, ref areaOrigin, 5u, 21u, areaStride);
        WidenRow8(ref sourceBase, ref areaOrigin, 5u, 22u, areaStride);
        WidenRow8(ref sourceBase, ref areaOrigin, 5u, 23u, areaStride);
        WidenRow8(ref sourceBase, ref areaOrigin, 6u, 24u, areaStride);
        WidenRow8(ref sourceBase, ref areaOrigin, 6u, 25u, areaStride);
        WidenRow8(ref sourceBase, ref areaOrigin, 6u, 26u, areaStride);
        WidenRow8(ref sourceBase, ref areaOrigin, 6u, 27u, areaStride);
        WidenRow8(ref sourceBase, ref areaOrigin, 7u, 28u, areaStride);
        WidenRow8(ref sourceBase, ref areaOrigin, 7u, 29u, areaStride);
        WidenRow8(ref sourceBase, ref areaOrigin, 7u, 30u, areaStride);
        WidenRow8(ref sourceBase, ref areaOrigin, 7u, 31u, areaStride);
    }

    /// <summary>
    /// Copies the full 8x8 block into the destination buffer while quadrupling both axes.
    /// </summary>
    [MethodImpl(InliningOptions.ShortMethod)]
    private void CopyTo4x4Scale(ref float areaOrigin, uint areaStride)
    {
        ref Vector4 sourceBase = ref this.V0L;

        ExpandRow8(ref sourceBase, ref areaOrigin, 0u, 0u, areaStride);
        ExpandRow8(ref sourceBase, ref areaOrigin, 0u, 1u, areaStride);
        ExpandRow8(ref sourceBase, ref areaOrigin, 0u, 2u, areaStride);
        ExpandRow8(ref sourceBase, ref areaOrigin, 0u, 3u, areaStride);
        ExpandRow8(ref sourceBase, ref areaOrigin, 1u, 4u, areaStride);
        ExpandRow8(ref sourceBase, ref areaOrigin, 1u, 5u, areaStride);
        ExpandRow8(ref sourceBase, ref areaOrigin, 1u, 6u, areaStride);
        ExpandRow8(ref sourceBase, ref areaOrigin, 1u, 7u, areaStride);
        ExpandRow8(ref sourceBase, ref areaOrigin, 2u, 8u, areaStride);
        ExpandRow8(ref sourceBase, ref areaOrigin, 2u, 9u, areaStride);
        ExpandRow8(ref sourceBase, ref areaOrigin, 2u, 10u, areaStride);
        ExpandRow8(ref sourceBase, ref areaOrigin, 2u, 11u, areaStride);
        ExpandRow8(ref sourceBase, ref areaOrigin, 3u, 12u, areaStride);
        ExpandRow8(ref sourceBase, ref areaOrigin, 3u, 13u, areaStride);
        ExpandRow8(ref sourceBase, ref areaOrigin, 3u, 14u, areaStride);
        ExpandRow8(ref sourceBase, ref areaOrigin, 3u, 15u, areaStride);
        ExpandRow8(ref sourceBase, ref areaOrigin, 4u, 16u, areaStride);
        ExpandRow8(ref sourceBase, ref areaOrigin, 4u, 17u, areaStride);
        ExpandRow8(ref sourceBase, ref areaOrigin, 4u, 18u, areaStride);
        ExpandRow8(ref sourceBase, ref areaOrigin, 4u, 19u, areaStride);
        ExpandRow8(ref sourceBase, ref areaOrigin, 5u, 20u, areaStride);
        ExpandRow8(ref sourceBase, ref areaOrigin, 5u, 21u, areaStride);
        ExpandRow8(ref sourceBase, ref areaOrigin, 5u, 22u, areaStride);
        ExpandRow8(ref sourceBase, ref areaOrigin, 5u, 23u, areaStride);
        ExpandRow8(ref sourceBase, ref areaOrigin, 6u, 24u, areaStride);
        ExpandRow8(ref sourceBase, ref areaOrigin, 6u, 25u, areaStride);
        ExpandRow8(ref sourceBase, ref areaOrigin, 6u, 26u, areaStride);
        ExpandRow8(ref sourceBase, ref areaOrigin, 6u, 27u, areaStride);
        ExpandRow8(ref sourceBase, ref areaOrigin, 7u, 28u, areaStride);
        ExpandRow8(ref sourceBase, ref areaOrigin, 7u, 29u, areaStride);
        ExpandRow8(ref sourceBase, ref areaOrigin, 7u, 30u, areaStride);
        ExpandRow8(ref sourceBase, ref areaOrigin, 7u, 31u, areaStride);
    }

    /// <summary>
    /// Copies one eight-sample row from the full block to the destination row.
    /// </summary>
    [MethodImpl(InliningOptions.ShortMethod)]
    private static void CopyRow8(ref Vector4 sourceBase, ref float areaOrigin, nuint sourceRow, nuint destRow, uint areaStride)
    {
        ref Vector4 source = ref Unsafe.Add(ref sourceBase, sourceRow * 2u);
        ref Vector4 dest = ref Unsafe.As<float, Vector4>(ref Unsafe.Add(ref areaOrigin, destRow * areaStride));

        dest = source;
        Unsafe.Add(ref dest, 1u) = Unsafe.Add(ref source, 1u);
    }

    /// <summary>
    /// Expands one eight-sample row to sixteen samples by duplicating each source value horizontally.
    /// </summary>
    [MethodImpl(InliningOptions.ShortMethod)]
    private static void WidenRow8(ref Vector4 sourceBase, ref float areaOrigin, nuint sourceRow, nuint destRow, uint areaStride)
    {
        ref Vector4 sourceLeft = ref Unsafe.Add(ref sourceBase, sourceRow * 2u);
        ref Vector4 sourceRight = ref Unsafe.Add(ref sourceLeft, 1u);
        ref Vector4 dest = ref Unsafe.As<float, Vector4>(ref Unsafe.Add(ref areaOrigin, destRow * areaStride));

        Vector4 xyLeft = new(sourceLeft.X);
        xyLeft.Z = sourceLeft.Y;
        xyLeft.W = sourceLeft.Y;

        Vector4 zwLeft = new(sourceLeft.Z);
        zwLeft.Z = sourceLeft.W;
        zwLeft.W = sourceLeft.W;

        Vector4 xyRight = new(sourceRight.X);
        xyRight.Z = sourceRight.Y;
        xyRight.W = sourceRight.Y;

        Vector4 zwRight = new(sourceRight.Z);
        zwRight.Z = sourceRight.W;
        zwRight.W = sourceRight.W;

        dest = xyLeft;
        Unsafe.Add(ref dest, 1u) = zwLeft;
        Unsafe.Add(ref dest, 2u) = xyRight;
        Unsafe.Add(ref dest, 3u) = zwRight;
    }

    /// <summary>
    /// Expands one eight-sample row to thirty-two samples by duplicating each source value four times horizontally.
    /// </summary>
    [MethodImpl(InliningOptions.ShortMethod)]
    private static void ExpandRow8(ref Vector4 sourceBase, ref float areaOrigin, nuint sourceRow, nuint destRow, uint areaStride)
    {
        ref Vector4 sourceLeft = ref Unsafe.Add(ref sourceBase, sourceRow * 2u);
        ref Vector4 sourceRight = ref Unsafe.Add(ref sourceLeft, 1u);
        ref Vector4 dest = ref Unsafe.As<float, Vector4>(ref Unsafe.Add(ref areaOrigin, destRow * areaStride));

        dest = new Vector4(sourceLeft.X);
        Unsafe.Add(ref dest, 1u) = new Vector4(sourceLeft.Y);
        Unsafe.Add(ref dest, 2u) = new Vector4(sourceLeft.Z);
        Unsafe.Add(ref dest, 3u) = new Vector4(sourceLeft.W);
        Unsafe.Add(ref dest, 4u) = new Vector4(sourceRight.X);
        Unsafe.Add(ref dest, 5u) = new Vector4(sourceRight.Y);
        Unsafe.Add(ref dest, 6u) = new Vector4(sourceRight.Z);
        Unsafe.Add(ref dest, 7u) = new Vector4(sourceRight.W);
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
