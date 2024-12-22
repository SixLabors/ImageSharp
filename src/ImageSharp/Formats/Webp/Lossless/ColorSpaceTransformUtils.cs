// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SixLabors.ImageSharp.Formats.Webp.Lossless;

internal static class ColorSpaceTransformUtils
{
    public static void CollectColorBlueTransforms(Span<uint> bgra, int stride, int tileWidth, int tileHeight, int greenToBlue, int redToBlue, Span<int> histo)
    {
        if (Avx2.IsSupported && tileWidth >= 16)
        {
            const int span = 16;
            Span<ushort> values = stackalloc ushort[span];
            Vector256<byte> collectColorBlueTransformsShuffleLowMask256 = Vector256.Create(255, 2, 255, 6, 255, 10, 255, 14, 255, 255, 255, 255, 255, 255, 255, 255, 255, 18, 255, 22, 255, 26, 255, 30, 255, 255, 255, 255, 255, 255, 255, 255);
            Vector256<byte> collectColorBlueTransformsShuffleHighMask256 = Vector256.Create(255, 255, 255, 255, 255, 255, 255, 255, 255, 2, 255, 6, 255, 10, 255, 14, 255, 255, 255, 255, 255, 255, 255, 255, 255, 18, 255, 22, 255, 26, 255, 30);
            Vector256<byte> collectColorBlueTransformsGreenBlueMask256 = Vector256.Create(255, 255, 0, 0, 255, 255, 0, 0, 255, 255, 0, 0, 255, 255, 0, 0, 255, 255, 0, 0, 255, 255, 0, 0, 255, 255, 0, 0, 255, 255, 0, 0);
            Vector256<byte> collectColorBlueTransformsGreenMask256 = Vector256.Create(0, 255, 0, 255, 0, 255, 0, 255, 0, 255, 0, 255, 0, 255, 0, 255, 0, 255, 0, 255, 0, 255, 0, 255, 0, 255, 0, 255, 0, 255, 0, 255);
            Vector256<byte> collectColorBlueTransformsBlueMask256 = Vector256.Create(255, 0, 255, 0, 255, 0, 255, 0, 255, 0, 255, 0, 255, 0, 255, 0, 255, 0, 255, 0, 255, 0, 255, 0, 255, 0, 255, 0, 255, 0, 255, 0);
            Vector256<short> multsr = Vector256.Create(LosslessUtils.Cst5b(redToBlue));
            Vector256<short> multsg = Vector256.Create(LosslessUtils.Cst5b(greenToBlue));
            for (int y = 0; y < tileHeight; y++)
            {
                Span<uint> srcSpan = bgra[(y * stride)..];
                ref uint inputRef = ref MemoryMarshal.GetReference(srcSpan);
                for (nuint x = 0; x <= (uint)tileWidth - span; x += span)
                {
                    nuint input0Idx = x;
                    nuint input1Idx = x + (span / 2);
                    Vector256<byte> input0 = Unsafe.As<uint, Vector256<uint>>(ref Unsafe.Add(ref inputRef, input0Idx)).AsByte();
                    Vector256<byte> input1 = Unsafe.As<uint, Vector256<uint>>(ref Unsafe.Add(ref inputRef, input1Idx)).AsByte();
                    Vector256<byte> r0 = Avx2.Shuffle(input0, collectColorBlueTransformsShuffleLowMask256);
                    Vector256<byte> r1 = Avx2.Shuffle(input1, collectColorBlueTransformsShuffleHighMask256);
                    Vector256<byte> r = Avx2.Or(r0, r1);
                    Vector256<byte> gb0 = Avx2.And(input0, collectColorBlueTransformsGreenBlueMask256);
                    Vector256<byte> gb1 = Avx2.And(input1, collectColorBlueTransformsGreenBlueMask256);
                    Vector256<ushort> gb = Avx2.PackUnsignedSaturate(gb0.AsInt32(), gb1.AsInt32());
                    Vector256<byte> g = Avx2.And(gb.AsByte(), collectColorBlueTransformsGreenMask256);
                    Vector256<short> a = Avx2.MultiplyHigh(r.AsInt16(), multsr);
                    Vector256<short> b = Avx2.MultiplyHigh(g.AsInt16(), multsg);
                    Vector256<byte> c = Avx2.Subtract(gb.AsByte(), b.AsByte());
                    Vector256<byte> d = Avx2.Subtract(c, a.AsByte());
                    Vector256<byte> e = Avx2.And(d, collectColorBlueTransformsBlueMask256);

                    ref ushort outputRef = ref MemoryMarshal.GetReference(values);
                    Unsafe.As<ushort, Vector256<ushort>>(ref outputRef) = e.AsUInt16();

                    for (int i = 0; i < span; i++)
                    {
                        ++histo[values[i]];
                    }
                }
            }

            int leftOver = tileWidth & (span - 1);
            if (leftOver > 0)
            {
                CollectColorBlueTransformsNoneVectorized(bgra[(tileWidth - leftOver)..], stride, leftOver, tileHeight, greenToBlue, redToBlue, histo);
            }
        }
        else if (Sse41.IsSupported)
        {
            const int span = 8;
            Span<ushort> values = stackalloc ushort[span];
            Vector128<byte> collectColorBlueTransformsShuffleLowMask = Vector128.Create(255, 2, 255, 6, 255, 10, 255, 14, 255, 255, 255, 255, 255, 255, 255, 255);
            Vector128<byte> collectColorBlueTransformsShuffleHighMask = Vector128.Create(255, 255, 255, 255, 255, 255, 255, 255, 255, 2, 255, 6, 255, 10, 255, 14);
            Vector128<byte> collectColorBlueTransformsGreenBlueMask = Vector128.Create(255, 255, 0, 0, 255, 255, 0, 0, 255, 255, 0, 0, 255, 255, 0, 0);
            Vector128<byte> collectColorBlueTransformsGreenMask = Vector128.Create(0, 255, 0, 255, 0, 255, 0, 255, 0, 255, 0, 255, 0, 255, 0, 255);
            Vector128<byte> collectColorBlueTransformsBlueMask = Vector128.Create(255, 0, 255, 0, 255, 0, 255, 0, 255, 0, 255, 0, 255, 0, 255, 0);
            Vector128<short> multsr = Vector128.Create(LosslessUtils.Cst5b(redToBlue));
            Vector128<short> multsg = Vector128.Create(LosslessUtils.Cst5b(greenToBlue));
            for (int y = 0; y < tileHeight; y++)
            {
                Span<uint> srcSpan = bgra[(y * stride)..];
                ref uint inputRef = ref MemoryMarshal.GetReference(srcSpan);
                for (nuint x = 0; (int)x <= tileWidth - span; x += span)
                {
                    nuint input0Idx = x;
                    nuint input1Idx = x + (span / 2);
                    Vector128<byte> input0 = Unsafe.As<uint, Vector128<uint>>(ref Unsafe.Add(ref inputRef, input0Idx)).AsByte();
                    Vector128<byte> input1 = Unsafe.As<uint, Vector128<uint>>(ref Unsafe.Add(ref inputRef, input1Idx)).AsByte();
                    Vector128<byte> r0 = Ssse3.Shuffle(input0, collectColorBlueTransformsShuffleLowMask);
                    Vector128<byte> r1 = Ssse3.Shuffle(input1, collectColorBlueTransformsShuffleHighMask);
                    Vector128<byte> r = Sse2.Or(r0, r1);
                    Vector128<byte> gb0 = Sse2.And(input0, collectColorBlueTransformsGreenBlueMask);
                    Vector128<byte> gb1 = Sse2.And(input1, collectColorBlueTransformsGreenBlueMask);
                    Vector128<ushort> gb = Sse41.PackUnsignedSaturate(gb0.AsInt32(), gb1.AsInt32());
                    Vector128<byte> g = Sse2.And(gb.AsByte(), collectColorBlueTransformsGreenMask);
                    Vector128<short> a = Sse2.MultiplyHigh(r.AsInt16(), multsr);
                    Vector128<short> b = Sse2.MultiplyHigh(g.AsInt16(), multsg);
                    Vector128<byte> c = Sse2.Subtract(gb.AsByte(), b.AsByte());
                    Vector128<byte> d = Sse2.Subtract(c, a.AsByte());
                    Vector128<byte> e = Sse2.And(d, collectColorBlueTransformsBlueMask);

                    ref ushort outputRef = ref MemoryMarshal.GetReference(values);
                    Unsafe.As<ushort, Vector128<ushort>>(ref outputRef) = e.AsUInt16();

                    for (int i = 0; i < span; i++)
                    {
                        ++histo[values[i]];
                    }
                }
            }

            int leftOver = tileWidth & (span - 1);
            if (leftOver > 0)
            {
                CollectColorBlueTransformsNoneVectorized(bgra[(tileWidth - leftOver)..], stride, leftOver, tileHeight, greenToBlue, redToBlue, histo);
            }
        }
        else
        {
            CollectColorBlueTransformsNoneVectorized(bgra, stride, tileWidth, tileHeight, greenToBlue, redToBlue, histo);
        }
    }

    private static void CollectColorBlueTransformsNoneVectorized(Span<uint> bgra, int stride, int tileWidth, int tileHeight, int greenToBlue, int redToBlue, Span<int> histo)
    {
        int pos = 0;
        while (tileHeight-- > 0)
        {
            for (int x = 0; x < tileWidth; x++)
            {
                int idx = LosslessUtils.TransformColorBlue((sbyte)greenToBlue, (sbyte)redToBlue, bgra[pos + x]);
                ++histo[idx];
            }

            pos += stride;
        }
    }

    public static void CollectColorRedTransforms(Span<uint> bgra, int stride, int tileWidth, int tileHeight, int greenToRed, Span<int> histo)
    {
        if (Avx2.IsSupported && tileWidth >= 16)
        {
            Vector256<byte> collectColorRedTransformsGreenMask256 = Vector256.Create(0x00ff00).AsByte();
            Vector256<byte> collectColorRedTransformsAndMask256 = Vector256.Create((short)0xff).AsByte();
            Vector256<short> multsg = Vector256.Create(LosslessUtils.Cst5b(greenToRed));
            const int span = 16;
            Span<ushort> values = stackalloc ushort[span];
            for (int y = 0; y < tileHeight; y++)
            {
                Span<uint> srcSpan = bgra[(y * stride)..];
                ref uint inputRef = ref MemoryMarshal.GetReference(srcSpan);
                for (nuint x = 0; x <= (uint)tileWidth - span; x += span)
                {
                    nuint input0Idx = x;
                    nuint input1Idx = x + (span / 2);
                    Vector256<byte> input0 = Unsafe.As<uint, Vector256<uint>>(ref Unsafe.Add(ref inputRef, input0Idx)).AsByte();
                    Vector256<byte> input1 = Unsafe.As<uint, Vector256<uint>>(ref Unsafe.Add(ref inputRef, input1Idx)).AsByte();
                    Vector256<byte> g0 = Avx2.And(input0, collectColorRedTransformsGreenMask256); // 0 0  | g 0
                    Vector256<byte> g1 = Avx2.And(input1, collectColorRedTransformsGreenMask256);
                    Vector256<ushort> g = Avx2.PackUnsignedSaturate(g0.AsInt32(), g1.AsInt32()); // g 0
                    Vector256<int> a0 = Avx2.ShiftRightLogical(input0.AsInt32(), 16); // 0 0  | x r
                    Vector256<int> a1 = Avx2.ShiftRightLogical(input1.AsInt32(), 16);
                    Vector256<ushort> a = Avx2.PackUnsignedSaturate(a0, a1); // x r
                    Vector256<short> b = Avx2.MultiplyHigh(g.AsInt16(), multsg); // x dr
                    Vector256<byte> c = Avx2.Subtract(a.AsByte(), b.AsByte()); // x r'
                    Vector256<byte> d = Avx2.And(c, collectColorRedTransformsAndMask256); // 0 r'

                    ref ushort outputRef = ref MemoryMarshal.GetReference(values);
                    Unsafe.As<ushort, Vector256<ushort>>(ref outputRef) = d.AsUInt16();

                    for (int i = 0; i < span; i++)
                    {
                        ++histo[values[i]];
                    }
                }
            }

            int leftOver = tileWidth & (span - 1);
            if (leftOver > 0)
            {
                CollectColorRedTransformsNoneVectorized(bgra[(tileWidth - leftOver)..], stride, leftOver, tileHeight, greenToRed, histo);
            }
        }
        else if (Sse41.IsSupported)
        {
            Vector128<byte> collectColorRedTransformsGreenMask = Vector128.Create(0x00ff00).AsByte();
            Vector128<byte> collectColorRedTransformsAndMask = Vector128.Create((short)0xff).AsByte();
            Vector128<short> multsg = Vector128.Create(LosslessUtils.Cst5b(greenToRed));
            const int span = 8;
            Span<ushort> values = stackalloc ushort[span];
            for (int y = 0; y < tileHeight; y++)
            {
                Span<uint> srcSpan = bgra[(y * stride)..];
                ref uint inputRef = ref MemoryMarshal.GetReference(srcSpan);
                for (nuint x = 0; (int)x <= tileWidth - span; x += span)
                {
                    nuint input0Idx = x;
                    nuint input1Idx = x + (span / 2);
                    Vector128<byte> input0 = Unsafe.As<uint, Vector128<uint>>(ref Unsafe.Add(ref inputRef, input0Idx)).AsByte();
                    Vector128<byte> input1 = Unsafe.As<uint, Vector128<uint>>(ref Unsafe.Add(ref inputRef, input1Idx)).AsByte();
                    Vector128<byte> g0 = Sse2.And(input0, collectColorRedTransformsGreenMask); // 0 0  | g 0
                    Vector128<byte> g1 = Sse2.And(input1, collectColorRedTransformsGreenMask);
                    Vector128<ushort> g = Sse41.PackUnsignedSaturate(g0.AsInt32(), g1.AsInt32()); // g 0
                    Vector128<int> a0 = Sse2.ShiftRightLogical(input0.AsInt32(), 16); // 0 0  | x r
                    Vector128<int> a1 = Sse2.ShiftRightLogical(input1.AsInt32(), 16);
                    Vector128<ushort> a = Sse41.PackUnsignedSaturate(a0, a1); // x r
                    Vector128<short> b = Sse2.MultiplyHigh(g.AsInt16(), multsg); // x dr
                    Vector128<byte> c = Sse2.Subtract(a.AsByte(), b.AsByte()); // x r'
                    Vector128<byte> d = Sse2.And(c, collectColorRedTransformsAndMask); // 0 r'

                    ref ushort outputRef = ref MemoryMarshal.GetReference(values);
                    Unsafe.As<ushort, Vector128<ushort>>(ref outputRef) = d.AsUInt16();

                    for (int i = 0; i < span; i++)
                    {
                        ++histo[values[i]];
                    }
                }
            }

            int leftOver = tileWidth & (span - 1);
            if (leftOver > 0)
            {
                CollectColorRedTransformsNoneVectorized(bgra[(tileWidth - leftOver)..], stride, leftOver, tileHeight, greenToRed, histo);
            }
        }
        else
        {
            CollectColorRedTransformsNoneVectorized(bgra, stride, tileWidth, tileHeight, greenToRed, histo);
        }
    }

    private static void CollectColorRedTransformsNoneVectorized(Span<uint> bgra, int stride, int tileWidth, int tileHeight, int greenToRed, Span<int> histo)
    {
        int pos = 0;
        while (tileHeight-- > 0)
        {
            for (int x = 0; x < tileWidth; x++)
            {
                int idx = LosslessUtils.TransformColorRed((sbyte)greenToRed, bgra[pos + x]);
                ++histo[idx];
            }

            pos += stride;
        }
    }
}
