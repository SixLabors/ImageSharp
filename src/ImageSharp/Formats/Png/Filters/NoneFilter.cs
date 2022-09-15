// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Png.Filters;

/// <summary>
/// The None filter, the scanline is transmitted unmodified; it is only necessary to
/// insert a filter type byte before the data.
/// <see href="https://www.w3.org/TR/PNG-Filters.html"/>
/// </summary>
internal static class NoneFilter
{
    /// <summary>
    /// Encodes the scanline
    /// </summary>
    /// <param name="scanline">The scanline to encode</param>
    /// <param name="result">The filtered scanline result.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Encode(ReadOnlySpan<byte> scanline, Span<byte> result)
    {
        // Insert row filter byte before the data.
        result[0] = (byte)FilterType.None;
        result = result[1..];
        scanline[..Math.Min(scanline.Length, result.Length)].CopyTo(result);
    }
}
