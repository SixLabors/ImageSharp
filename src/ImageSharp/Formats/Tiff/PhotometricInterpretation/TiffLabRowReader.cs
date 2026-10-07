// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Loads TIFF Lab samples into a CieLab row for ICC conversion.
/// </summary>
/// <typeparam name="TSource">The packed or planar Lab source.</typeparam>
internal static class TiffLabRowReader<TSource>
    where TSource : struct, ITiffColorSource<float>
{
    /// <summary>
    /// Normalizes TIFF codes into the Lab values consumed by the ICC converter.
    /// </summary>
    /// <param name="source0">The packed row or first plane.</param>
    /// <param name="source1">The second plane, if planar.</param>
    /// <param name="source2">The third plane, if planar.</param>
    /// <param name="destination">The typed Lab row.</param>
    /// <param name="source">The stored sample reader.</param>
    public static void Read(ReadOnlySpan<byte> source0, ReadOnlySpan<byte> source1, ReadOnlySpan<byte> source2, Span<CieLab> destination, TSource source)
    {
        ReadOnlySpan<float> components = MemoryMarshal.Cast<CieLab, float>(destination);
        int i = 0;

        // The ICC converter accepts a whole typed Lab row; load and normalize complete register groups first.
        if (Vector512.IsHardwareAccelerated)
        {
            // One 512-bit float register has 16 lanes. Three registers hold L, a, and b for 16 pixels;
            // divide the destination's float-register count by three to leave incomplete groups for narrower paths.
            nuint groups = components.Vector512Count<float>() / 3;
            for (nuint group = 0; group < groups; group++, i += Vector512<float>.Count)
            {
                source.Read(source0, source1, source2, [], i, out Vector512<float> l, out Vector512<float> a, out Vector512<float> b, out _);

                // Each 512-bit register is four four-pixel groups; write each group as twelve contiguous Lab floats.
                StoreFour(l.GetLower().GetLower(), a.GetLower().GetLower(), b.GetLower().GetLower(), ref destination[i]);
                StoreFour(l.GetLower().GetUpper(), a.GetLower().GetUpper(), b.GetLower().GetUpper(), ref destination[i + 4]);
                StoreFour(l.GetUpper().GetLower(), a.GetUpper().GetLower(), b.GetUpper().GetLower(), ref destination[i + 8]);
                StoreFour(l.GetUpper().GetUpper(), a.GetUpper().GetUpper(), b.GetUpper().GetUpper(), ref destination[i + 12]);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            // Count only the remaining floats: three eight-lane registers represent eight complete Lab pixels.
            nuint groups = components[(i * 3)..].Vector256Count<float>() / 3;
            for (nuint group = 0; group < groups; group++, i += Vector256<float>.Count)
            {
                source.Read(source0, source1, source2, [], i, out Vector256<float> l, out Vector256<float> a, out Vector256<float> b, out _);

                StoreFour(l.GetLower(), a.GetLower(), b.GetLower(), ref destination[i]);
                StoreFour(l.GetUpper(), a.GetUpper(), b.GetUpper(), ref destination[i + 4]);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            // Three four-lane registers represent four complete Lab pixels; scalar reads finish the tail.
            nuint groups = components[(i * 3)..].Vector128Count<float>() / 3;
            for (nuint group = 0; group < groups; group++, i += Vector128<float>.Count)
            {
                source.Read(source0, source1, source2, [], i, out Vector128<float> l, out Vector128<float> a, out Vector128<float> b, out _);

                StoreFour(l, a, b, ref destination[i]);
            }
        }

        for (; i < destination.Length; i++)
        {
            source.Read(source0, source1, source2, [], i, out float l, out float a, out float b, out _);
            destination[i] = new CieLab(l, a, b);
        }
    }

    /// <summary>
    /// Writes four Lab values from component lanes without per-pixel register extraction.
    /// </summary>
    /// <param name="l">The four lightness components.</param>
    /// <param name="a">The four first chroma components.</param>
    /// <param name="b">The four second chroma components.</param>
    /// <param name="destination">The first of four destination Lab values.</param>
    private static void StoreFour(Vector128<float> l, Vector128<float> a, Vector128<float> b, ref CieLab destination)
    {
        // CieLab is three sequential floats, so the shared interleave kernel can write its twelve float words directly.
        SimdUtils.Interleave3(l, a, b, ref Unsafe.As<CieLab, float>(ref destination));
    }
}
