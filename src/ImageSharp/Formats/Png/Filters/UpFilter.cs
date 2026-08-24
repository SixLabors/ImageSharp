// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics.Tensors;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Png.Filters;

/// <summary>
/// The Up filter is just like the Sub filter except that the pixel immediately above the current pixel,
/// rather than just to its left, is used as the predictor.
/// <see href="https://www.w3.org/TR/PNG-Filters.html"/>
/// </summary>
internal static class UpFilter
{
    /// <summary>
    /// Decodes a scanline, which was filtered with the up filter.
    /// </summary>
    /// <param name="scanline">The scanline to decode</param>
    /// <param name="previousScanline">The previous scanline.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Decode(Span<byte> scanline, Span<byte> previousScanline)
    {
        DebugGuard.MustBeSameSized<byte>(scanline, previousScanline, nameof(scanline));

        // The leading filter byte is metadata; every remaining byte is the modulo-256 sum of Raw(x) and Prior(x).
        TensorPrimitives.Add(scanline[1..], previousScanline[1..], scanline[1..]);
    }

    /// <summary>
    /// Encodes a scanline with the up filter applied.
    /// </summary>
    /// <param name="scanline">The scanline to encode.</param>
    /// <param name="previousScanline">The previous scanline.</param>
    /// <param name="result">The filtered scanline result.</param>
    /// <param name="sum">The sum of the total variance of the filtered row.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Encode(ReadOnlySpan<byte> scanline, ReadOnlySpan<byte> previousScanline, Span<byte> result, out int sum)

        // Up never reads a left neighbor, so bytesPerPixel is deliberately zero in
        // the shared traversal and no unsigned left offset is evaluated.
        => PngFilterEncoder.Encode<UpFilterOperator>(scanline, previousScanline, result, 0, out sum);
}
