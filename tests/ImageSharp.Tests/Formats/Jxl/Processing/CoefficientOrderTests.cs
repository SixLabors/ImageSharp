// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Processing;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder;

namespace SixLabors.ImageSharp.Tests.Formats.Jxl.Processing;

/// <summary>
/// Tests the ordering of coefficients against random
/// permutations and bit-stream representations.
/// </summary>
public class CoefficientOrderTests
{
    /// <summary>
    /// Number of swaps. See <see cref="Permutation.FewSwaps"/>
    /// and <see cref="Permutation.FewSlides"/>.
    /// </summary>
    private const int Swaps = 32;

    /// <summary>
    /// Identifies the kind of permutation for testing.
    /// </summary>
    private enum Permutation : byte
    {
        /// <summary>
        /// Each permutation is incremental and starts at 0.
        /// See <see cref="JxlSimdUtils.Iota{T}(Span{T}, T)"/>.
        /// </summary>
        Identity,

        /// <summary>
        /// Incremental permutation starting at 0, just like in
        /// <see cref="Identity"/>, but additionally, random values
        /// are swapped <see cref="Swaps"/> times, producing a slightly
        /// more randomized result.
        /// </summary>
        FewSwaps,

        /// <summary>
        /// Incremental permutation starting at 0, just like in
        /// <see cref="Identity"/>, but additionally, random ranges
        /// of offsets are moved backwards by one <see cref="Swaps"/> times.
        /// </summary>
        FewSlides,

        /// <summary>
        /// Everything is random.
        /// </summary>
        Random
    }

    private static void RoundtripPermutation(Span<uint> permutation, Span<int> output, int len, out int size)
    {
        using MemoryStream ms = new();
        JxlBitWriter writer = new(ms);

        Assert.True(JxlCoefficientOrderEncoder.EncodePermutation(permutation, 0, len, writer));
        writer.ZeroPadToByte();

        ms.Position = 0;
        JxlBitReader reader = new(ms);

        //TODO
    }
}
