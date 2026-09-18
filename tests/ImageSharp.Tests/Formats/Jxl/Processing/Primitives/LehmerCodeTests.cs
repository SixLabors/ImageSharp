// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Processing;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

namespace SixLabors.ImageSharp.Tests.Formats.Jxl.Processing.Primitives;

public class LehmerCodeTests
{
    private sealed class WorkingSet(int maxN)
    {
        public int PaddedN { get; } = maxN << JxlMath.CeilLog2Nonzero(maxN + 1);

        public uint[] Permutation { get; } = new uint[maxN];

        public uint[] Temporary { get; } = new uint[maxN];

        public uint[] LehmerCodes { get; } = new uint[maxN];

        public uint[] Decoded { get; } = new uint[maxN];
    }

    private static void RoundTrip(int n, WorkingSet ws)
    {
        Assert.NotEqual(0, n);
        int paddedN = 1 << JxlMath.CeilLog2Nonzero(n);

        Rng rng = new(((ulong)n * 65537) + 13);
        Assert.True(n < 1 << (sizeof(uint) * 8));

        Span<uint> permutationsSpan = ws.Permutation.AsSpan();
        JxlSimdUtils.Iota(permutationsSpan[..n], 0u);

        for (int rep = 0; rep < 3; rep++)
        {
            rng.Shuffle(permutationsSpan[..n]);

            Assert.True(
                JxlLehmerCode.ComputeLehmerCode(permutationsSpan, ws.Temporary, n, ws.LehmerCodes),
                "Could not compute Lehmer code");

            ws.Temporary.AsSpan()[..(paddedN * 4)].Clear();

            Assert.True(
                JxlLehmerCode.DecodeLehmerCode(ws.LehmerCodes.AsSpan(), ws.Temporary.AsSpan(), n, ws.Decoded.AsSpan()),
                "Could not decode Lehmer code");

            for (int i = 0; i < n; ++i)
            {
                Assert.Equal(permutationsSpan[i], ws.Decoded[i]);
            }
        }
    }

    private static void RoundTripSizeRange(int begin, int end)
    {
        Assert.NotEqual(0, begin);
        List<WorkingSet> workingSets = [];

        int numThreads = Environment.ProcessorCount;

        // initialization
        for (int i = 0; i < numThreads; i++)
        {
            workingSets.Add(new WorkingSet(end - 1));
        }

        // loop
        Parallel.For(
            begin,
            end,
            () => new WorkingSet(end - 1),
            (n, _, workingSet) =>
            {
                RoundTrip(n, workingSet);
                return workingSet;
            },
            _ => { });
    }

    [Fact]
    public void TestLehmerCodes()
    {
        RoundTripSizeRange(1, 1026);
        RoundTripSizeRange(65536, 65540);
    }
}
