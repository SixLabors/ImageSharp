// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics;

namespace SixLabors.ImageSharp;

/// <summary>
/// Provides SIMD kernels that convert, shuffle, interleave and pack spans of pixel and sample data.
/// </summary>
internal static partial class SimdUtils
{
    /// <summary>
    /// Checks in debug builds that the spans of a byte to float conversion have the same length, a multiple of
    /// <paramref name="shouldBeDivisibleBy"/>.
    /// </summary>
    /// <param name="source">The source bytes.</param>
    /// <param name="dest">The destination floats.</param>
    /// <param name="shouldBeDivisibleBy">The element count of one vector step. It is a power of two.</param>
    [Conditional("DEBUG")]
    private static void DebugVerifySpanInput(ReadOnlySpan<byte> source, ReadOnlySpan<float> dest, int shouldBeDivisibleBy)
    {
        DebugGuard.IsTrue(source.Length == dest.Length, nameof(source), "Input spans must be of same length!");
        DebugGuard.IsTrue(
            Numerics.ModuloP2(dest.Length, shouldBeDivisibleBy) == 0,
            nameof(source),
            $"length should be divisible by {shouldBeDivisibleBy}!");
    }

    /// <summary>
    /// Checks in debug builds that the spans of a float to byte conversion have the same length, a multiple of
    /// <paramref name="shouldBeDivisibleBy"/>.
    /// </summary>
    /// <param name="source">The source floats.</param>
    /// <param name="destination">The destination bytes.</param>
    /// <param name="shouldBeDivisibleBy">The element count of one vector step. It is a power of two.</param>
    [Conditional("DEBUG")]
    private static void DebugVerifySpanInput(ReadOnlySpan<float> source, ReadOnlySpan<byte> destination, int shouldBeDivisibleBy)
    {
        DebugGuard.IsTrue(source.Length == destination.Length, nameof(source), "Input spans must be of same length!");
        DebugGuard.IsTrue(
            Numerics.ModuloP2(destination.Length, shouldBeDivisibleBy) == 0,
            nameof(source),
            $"length should be divisible by {shouldBeDivisibleBy}!");
    }

    /// <summary>
    /// Reads four consecutive bytes of a channel plane as one value.
    /// </summary>
    private struct ByteTuple4
    {
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
        public byte V0;
        public byte V1;
        public byte V2;
        public byte V3;
#pragma warning restore CS0649 // Field is never assigned to, and will always have its default value
    }
}
