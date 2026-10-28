// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Tiff.ColorConverters;

/// <summary>
/// Converts complete TIFF pixel groups with wide SIMD registers, then converts remaining pixels one at a time.
/// </summary>
/// <typeparam name="TPipeline">The closed source, operator, and store composition.</typeparam>
/// <typeparam name="TDestination">The destination element type.</typeparam>
internal static class TiffColorTraversal<TPipeline, TDestination>
    where TPipeline : struct, ITiffColorPipeline<TDestination>
    where TDestination : unmanaged
{
    /// <summary>
    /// Converts the destination's complete pixel count from the supplied source spans.
    /// </summary>
    /// <param name="source0">The packed source or first component plane.</param>
    /// <param name="source1">The second component plane, when present.</param>
    /// <param name="source2">The third component plane, when present.</param>
    /// <param name="source3">The fourth component plane, when present.</param>
    /// <param name="destination">The bounded destination group.</param>
    /// <param name="pixelCount">The number of complete pixels to write.</param>
    /// <param name="pipeline">The closed source, arithmetic, and store composition.</param>
    public static void Convert(
        ReadOnlySpan<byte> source0,
        ReadOnlySpan<byte> source1,
        ReadOnlySpan<byte> source2,
        ReadOnlySpan<byte> source3,
        Span<TDestination> destination,
        int pixelCount,
        TPipeline pipeline)
    {
        int i = 0;
        int destinationBytesPerPixel = TPipeline.DestinationElementsPerPixel * Unsafe.SizeOf<TDestination>();
        ReadOnlySpan<byte> outputBytes = MemoryMarshal.AsBytes(destination[..(pixelCount * TPipeline.DestinationElementsPerPixel)]);

        // Count complete output byte registers, then divide by the registers one source group produces.
        // This gives the number of complete pixel groups for either separate planes or packed RGBA words.
        if (Vector512.IsHardwareAccelerated)
        {
            int outputVectorsPerGroup = (TPipeline.Pixels512 * destinationBytesPerPixel) / Vector512<byte>.Count;
            nuint groups = outputBytes.Vector512Count<byte>() / (uint)outputVectorsPerGroup;

            for (nuint group = 0; group < groups; group++)
            {
                pipeline.Convert512(source0, source1, source2, source3, destination, i);
                i += TPipeline.Pixels512;
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            int outputVectorsPerGroup = (TPipeline.Pixels256 * destinationBytesPerPixel) / Vector256<byte>.Count;
            nuint groups = outputBytes[(i * destinationBytesPerPixel)..].Vector256Count<byte>() / (uint)outputVectorsPerGroup;

            for (nuint group = 0; group < groups; group++)
            {
                pipeline.Convert256(source0, source1, source2, source3, destination, i);
                i += TPipeline.Pixels256;
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            int outputVectorsPerGroup = (TPipeline.Pixels128 * destinationBytesPerPixel) / Vector128<byte>.Count;
            nuint groups = outputBytes[(i * destinationBytesPerPixel)..].Vector128Count<byte>() / (uint)outputVectorsPerGroup;

            for (nuint group = 0; group < groups; group++)
            {
                pipeline.Convert128(source0, source1, source2, source3, destination, i);
                i += TPipeline.Pixels128;
            }
        }

        for (; i < pixelCount; i++)
        {
            pipeline.ConvertScalar(source0, source1, source2, source3, destination, i);
        }
    }
}
