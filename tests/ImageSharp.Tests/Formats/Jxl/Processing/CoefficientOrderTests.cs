// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Formats.Jxl.Processing;
using SixLabors.ImageSharp.Formats.Jxl.Processing.AcStrategy;
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
    /// Checks that canonical orientation retains both original axes, including when the first one is longer.
    /// </summary>
    /// <param name="rows">The original row extent.</param>
    /// <param name="columns">The original column extent.</param>
    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 2)]
    [InlineData(2, 1)]
    [InlineData(2, 4)]
    [InlineData(4, 2)]
    [InlineData(16, 32)]
    [InlineData(32, 16)]
    [InlineData(32, 32)]
    public void CoefficientLayout_PreservesBothOriginalAxes(int rows, int columns)
    {
        int expectedRows = Math.Min(rows, columns);
        int expectedColumns = Math.Max(rows, columns);
        JxlForwardCoefficientOrder.CoefficientLayout(ref rows, ref columns);
        Assert.Equal(expectedRows, rows);
        Assert.Equal(expectedColumns, columns);
    }

    /// <summary>
    /// Checks stored natural-order vectors from libjxl and jxl-rs, including both spatial orientations of a rectangular transform.
    /// </summary>
    /// <param name="type">The transform whose canonical coefficient order is checked.</param>
    [Theory]
    [InlineData(JxlAcStrategyType.DCT)]
    [InlineData(JxlAcStrategyType.DCT8X16)]
    [InlineData(JxlAcStrategyType.DCT16X8)]
    public void NaturalOrder_MatchesStoredCanonicalOrders(JxlAcStrategyType type)
    {
        // These fixed numeric vectors are also stored in jxl-rs frame/coeff_order.rs. Its natural-order algorithm agrees with libjxl ac_strategy.cc.
        // The first square diagonal visits column one before row one; rectangular orders retain the complete longer coefficient axis.
        ReadOnlySpan<int> expected = type == JxlAcStrategyType.DCT
            ?
            [
                0, 1, 8, 16, 9, 2, 3, 10, 17, 24, 32, 25, 18, 11, 4, 5,
                12, 19, 26, 33, 40, 48, 41, 34, 27, 20, 13, 6, 7, 14, 21, 28,
                35, 42, 49, 56, 57, 50, 43, 36, 29, 22, 15, 23, 30, 37, 44, 51,
                58, 59, 52, 45, 38, 31, 39, 46, 53, 60, 61, 54, 47, 55, 62, 63
            ]
            :
            [
                0, 1, 16, 2, 3, 17, 32, 18, 4, 5, 19, 33, 48, 34, 20, 6,
                7, 21, 35, 49, 64, 50, 36, 22, 8, 9, 23, 37, 51, 65, 80, 66,
                52, 38, 24, 10, 11, 25, 39, 53, 67, 81, 96, 82, 68, 54, 40, 26,
                12, 13, 27, 41, 55, 69, 83, 97, 112, 98, 84, 70, 56, 42, 28, 14,
                15, 29, 43, 57, 71, 85, 99, 113, 114, 100, 86, 72, 58, 44, 30, 31,
                45, 59, 73, 87, 101, 115, 116, 102, 88, 74, 60, 46, 47, 61, 75, 89,
                103, 117, 118, 104, 90, 76, 62, 63, 77, 91, 105, 119, 120, 106, 92, 78,
                79, 93, 107, 121, 122, 108, 94, 95, 109, 123, 124, 110, 111, 125, 126, 127
            ];

        JxlAcStrategy strategy = new(type);
        int[] actual = new int[expected.Length];
        strategy.ComputeNaturalCoefficientOrder(actual);
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], actual[i]);
        }
    }

    /// <summary>
    /// Checks every valid strategy's complete order, low-frequency rectangle, inverse lookup and output guards.
    /// </summary>
    [Fact]
    public void NaturalOrderAndLookup_CoverEveryCoefficient()
    {
        for (int raw = 0; raw < JxlAcStrategy.NumberOfValidStrategies; raw++)
        {
            JxlAcStrategy strategy = new(raw);
            int blocksX = Math.Max(strategy.CoveredBlocksX, strategy.CoveredBlocksY);
            int blocksY = Math.Min(strategy.CoveredBlocksX, strategy.CoveredBlocksY);
            int area = blocksX * blocksY * 64;
            int[] order = new int[area + 2];
            int[] lookup = new int[area + 2];
            order.AsSpan().Fill(-1);
            lookup.AsSpan().Fill(-1);
            strategy.ComputeNaturalCoefficientOrder(order.AsSpan(1, area));
            strategy.ComputeNaturalCoefficientOrderLookup(lookup.AsSpan(1, area));

            for (int i = 0; i < area; i++)
            {
                int coefficient = order[1 + i];
                Assert.InRange(coefficient, 0, area - 1);
                Assert.Equal(i, lookup[1 + coefficient]);
            }

            // DC coefficients of the covered eight-by-eight blocks occupy the top-left frequency rectangle in row-major order.
            for (int i = 0; i < blocksX * blocksY; i++)
            {
                int expected = ((i / blocksX) * blocksX * 8) + (i % blocksX);
                Assert.Equal(expected, order[1 + i]);
            }

            Assert.Equal(-1, order[0]);
            Assert.Equal(-1, order[^1]);
            Assert.Equal(-1, lookup[0]);
            Assert.Equal(-1, lookup[^1]);
        }
    }

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

    private static void RoundtripPermutation(Span<uint> permutation, Span<uint> output, int len, out int size)
    {
        using MemoryStream ms = new();
        JxlBitWriter writer = new(ms);

        Assert.True(JxlCoefficientOrderEncoder.EncodePermutation(permutation, 0, len, writer));
        writer.ZeroPadToByte();

        ms.Position = 0;

        JxlBitReader reader = new(ms);
        Assert.True(JxlCoefficientOrder.DecodePermutation(TestEnvironment.Configuration, 0, len, output, reader));

        size = (int)ms.Length;
    }

    /// <summary>
    /// Ensures that permutations can be decoded exactly as they
    /// were encoded, losslessly.
    /// </summary>
    /// <param name="permutationKind">The kind of permutation.</param>
    /// <param name="length">Number of permutations to round-trip.</param>
    private static void TestPermutation(Permutation permutationKind, int length)
    {
        // The permutations can be very large, so use
        // configuration.MemoryAllocator.Allocate. A stackalloc
        // can lead to a stack overflow exception.
        using IMemoryOwner<uint> permOwner = TestEnvironment.Configuration.MemoryAllocator.Allocate<uint>(length);
        Span<uint> perm = permOwner.Memory.Span;
        JxlSimdUtils.Iota(perm, 0u);

        Rng rng = new(0);

        switch (permutationKind)
        {
            case Permutation.FewSwaps:
                for (int i = 0; i < Swaps; i++)
                {
                    int a = (int)rng.UniformU(0, (ulong)(length - 1));
                    int b = (int)rng.UniformU(0, (ulong)(length - 1));
                    RuntimeUtility.Swap(ref perm[a], ref perm[b]);
                }

                break;

            case Permutation.FewSlides:
                for (int i = 0; i < Swaps; i++)
                {
                    int a = (int)rng.UniformU(0, (ulong)(length - 1));
                    int b = (int)rng.UniformU(0, (ulong)(length - 1));
                    int from = Math.Min(a, b);
                    int to = Math.Max(a, b);
                    uint start = perm[from];

                    for (int j = from; j < to; j++)
                    {
                        perm[j] = perm[j + 1];
                    }

                    perm[to] = start;
                }

                break;

            case Permutation.Random:
                rng.Shuffle(perm);
                break;
        }

        using IMemoryOwner<uint> outOwner = TestEnvironment.Configuration.MemoryAllocator.Allocate<uint>(length);
        Span<uint> output = outOwner.Memory.Span;

        RoundtripPermutation(perm, output, length, out int size);

        // We want to make sure the permutations are lossless.
        for (int idx = 0; idx < length; idx++)
        {
            Assert.Equal(perm[idx], output[idx]);
        }
    }

    /// <summary>
    /// Tests a small number of permutations.
    /// </summary>
    [Fact]
    public void TestSmall()
    {
        const int permutationCount = 256;
        TestPermutation(Permutation.Identity, permutationCount);
        TestPermutation(Permutation.FewSlides, permutationCount);
        TestPermutation(Permutation.FewSwaps, permutationCount);
        TestPermutation(Permutation.Random, permutationCount);
    }

    /// <summary>
    /// Tests a medium number of permutations.
    /// </summary>
    [Fact]
    public void TestMedium()
    {
        const int permutationCount = 1 << 12;
        TestPermutation(Permutation.Identity, permutationCount);
        TestPermutation(Permutation.FewSlides, permutationCount);
        TestPermutation(Permutation.FewSwaps, permutationCount);
        TestPermutation(Permutation.Random, permutationCount);
    }

    /// <summary>
    /// Tests a large number of permutations.
    /// </summary>
    [Fact]
    public void TestLarge()
    {
        const int permutationCount = 1 << 16;
        TestPermutation(Permutation.Identity, permutationCount);
        TestPermutation(Permutation.FewSlides, permutationCount);
        TestPermutation(Permutation.FewSwaps, permutationCount);
        TestPermutation(Permutation.Random, permutationCount);
    }
}
