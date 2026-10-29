// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Common.Helpers;
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
