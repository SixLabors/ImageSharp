// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Processing;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

namespace SixLabors.ImageSharp.Tests.Formats.Jxl.Processing.Primitives;

public class LehmerCodeTests
{
    /// <summary>
    /// Owns reusable numeric storage for one range of permutation lengths.
    /// </summary>
    /// <param name="maxN">The largest positive length tested with this workspace.</param>
    private sealed class WorkingSet(int maxN)
    {
        /// <summary>Gets the permutation entries.</summary>
        public uint[] Permutation { get; } = new uint[maxN];

        // Encoding needs n plus one tree entries; decoding needs the next power of two. These are uint element counts,
        // not byte lengths. One allocation supports every tested length in the range without caller-side clearing.
        /// <summary>Gets dirty reusable scratch large enough for both shared conversion APIs.</summary>
        public uint[] Temporary { get; } = new uint[Math.Max(maxN + 1, 1 << JxlMath.CeilLog2Nonzero(maxN))];

        /// <summary>Gets the encoded rank entries.</summary>
        public uint[] LehmerCodes { get; } = new uint[maxN];

        /// <summary>Gets the restored permutation entries.</summary>
        public uint[] Decoded { get; } = new uint[maxN];
    }

    /// <summary>
    /// Round-trips three deterministic shuffled permutations while reusing both encoder and decoder scratch.
    /// </summary>
    /// <param name="n">The positive permutation length.</param>
    /// <param name="ws">The storage prepared for at least this length.</param>
    private static void RoundTrip(int n, WorkingSet ws)
    {
        Assert.NotEqual(0, n);
        Rng rng = new(((ulong)n * 65537) + 13);

        Span<uint> permutationsSpan = ws.Permutation.AsSpan();
        SimdUtils.FillSequence(permutationsSpan[..n], 0u);

        for (int rep = 0; rep < 3; rep++)
        {
            rng.Shuffle(permutationsSpan[..n]);

            Assert.True(
                JxlLehmerCode.ComputeLehmerCode(permutationsSpan, ws.Temporary, n, ws.LehmerCodes),
                "Could not compute Lehmer code");

            Assert.True(
                JxlLehmerCode.DecodeLehmerCode(ws.LehmerCodes.AsSpan(), ws.Temporary.AsSpan(), n, ws.Decoded.AsSpan()),
                "Could not decode Lehmer code");

            for (int i = 0; i < n; ++i)
            {
                Assert.Equal(permutationsSpan[i], ws.Decoded[i]);
            }
        }
    }

    /// <summary>
    /// Checks every length in a range using the same scratch storage for successive permutations.
    /// </summary>
    /// <param name="begin">The first positive permutation length.</param>
    /// <param name="end">The exclusive upper length bound.</param>
    private static void RoundTripSizeRange(int begin, int end)
    {
        WorkingSet workingSet = new(end - 1);
        for (int n = begin; n < end; n++)
        {
            RoundTrip(n, workingSet);
        }
    }

    /// <summary>
    /// Checks the existing small-length range and lengths at and immediately above the 65,536-entry power-of-two boundary.
    /// </summary>
    [Fact]
    public void TestLehmerCodes()
    {
        RoundTripSizeRange(1, 1026);
        RoundTripSizeRange(65536, 65540);
    }

    /// <summary>
    /// Checks known ranks with dirty reused scratch, a non-power-of-two length and unchanged output guards.
    /// </summary>
    [Fact]
    public void ConvertLehmerCode_InitializesRequiredScratchPrefix()
    {
        ReadOnlySpan<uint> permutation = [3U, 0U, 4U, 1U, 2U];
        // Among the values still available, 3 has rank 3, then 0 has rank 0, then 4 has rank 2.
        // The last two entries are already in increasing order and both have rank zero.
        ReadOnlySpan<uint> expectedCode = [3U, 0U, 2U, 0U, 0U];
        Span<uint> scratch = stackalloc uint[10];
        Span<uint> codeStorage = stackalloc uint[7];
        Span<uint> decodedStorage = stackalloc uint[7];
        scratch.Fill(uint.MaxValue);
        codeStorage.Fill(uint.MaxValue);
        decodedStorage.Fill(uint.MaxValue);
        Span<uint> code = codeStorage.Slice(1, permutation.Length);
        Span<uint> decoded = decodedStorage.Slice(1, permutation.Length);

        // The first conversion starts with arbitrary allocator contents. The second starts with the used Fenwick tree
        // from the first conversion; the shared API must initialize its own n-plus-one prefix in both cases.
        for (int repetition = 0; repetition < 2; repetition++)
        {
            Assert.True(JxlLehmerCode.ComputeLehmerCode(permutation, scratch, permutation.Length, code));
            for (int i = 0; i < expectedCode.Length; i++)
            {
                Assert.Equal(expectedCode[i], code[i]);
            }

            Assert.Equal(-1, scratch[(permutation.Length + 1)..].IndexOfAnyExcept(uint.MaxValue));
        }

        Assert.Equal(uint.MaxValue, codeStorage[0]);
        Assert.Equal(uint.MaxValue, codeStorage[^1]);
        scratch.Fill(uint.MaxValue);
        Assert.True(JxlLehmerCode.DecodeLehmerCode(code, scratch, permutation.Length, decoded));
        for (int i = 0; i < permutation.Length; i++)
        {
            Assert.Equal(permutation[i], decoded[i]);
        }

        // Decoding initializes the eight entries required by this five-element permutation, without touching its tail.
        Assert.Equal(-1, scratch[8..].IndexOfAnyExcept(uint.MaxValue));
        Assert.Equal(uint.MaxValue, decodedStorage[0]);
        Assert.Equal(uint.MaxValue, decodedStorage[^1]);
    }
}
