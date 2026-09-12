// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Jxl.Memory.ImageTypes;
using SixLabors.ImageSharp.Formats.Jxl.Processing.AcStrategy;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder;

internal static class JxlHeuristics
{
    private static ReadOnlySpan<byte> SimpleContextMap =>
    [
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
    ];

    public static void FindBestBlockEntropyModel(JxlCompressParameters cparameters, JxlImageI rqf, JxlAcStrategyImage acStrategy, JxlBlockContextMap blockCtxMap)
    {
        if (cparameters.DecodingSpeedTier >= 1)
        {
            SimpleContextMap.CopyTo(blockCtxMap.ContextMap.AsSpan());
            blockCtxMap.ContextCount = 2;
            blockCtxMap.DcContextCount = 1;
            return;
        }

        if (cparameters.SpeedTier >= JxlSpeedTier.Falcon)
        {
            return;
        }

        int total = rqf.XSize * rqf.YSize;
        int sizeForContextModel = (1 << 10) * cparameters.ButteraugliDistance;

        if (total < sizeForContextModel)
        {
            return;
        }

        OccCounters counters = new(rqf, acStrategy);
        int sizeForQfSplit = (1 << 13) * cparameters.ButteraugliDistance;
        int numQfSegments = total < sizeForQfSplit ? 1 : 2;
        List<uint> qft = blockCtxMap.QfThresholds;
        qft.Clear();
        int cumulativeSum = 0;
        int next = 1;
        int lastCut = 256;
        int cut = total * next / numQfSegments;

        for (int j = 0; j < 256; j++)
        {
            cumulativeSum += counters.QfCounts[j];

            if (cumulativeSum > cut)
            {
                if (j != 0)
                {
                    qft.Add((uint)j);
                }

                lastCut = j;

                while (cumulativeSum > cut)
                {
                    next++;
                    cut = total * next / numQfSegments;
                }
            }
            else if (next > qft.Count + 1)
            {
                if (j - 1 == lastCut && j != 0)
                {
                    qft.Add((uint)j);
                }
            }
        }

        int[]? pooledCounts = null;
        int[]? pooledRemap = null;
        int[]? pooledClusters = null;
        int countsLength = JxlForwardCoefficientOrder.OrderCount * (qft.Count + 1);

        Span<int> counts =
            countsLength <= 128
                ? stackalloc int[128].Slice(0, countsLength)
                : pooledCounts = ArrayPool<int>.Shared.Rent(countsLength);

        Span<int> remap =
            countsLength <= 128
                ? stackalloc int[128].Slice(0, countsLength)
                : pooledRemap = ArrayPool<int>.Shared.Rent(countsLength);

        Span<int> clusters =
            countsLength <= 128
                ? stackalloc int[128].Slice(0, countsLength)
                : pooledClusters = ArrayPool<int>.Shared.Rent(countsLength);

        int qftPos = 0;

        for (int j = 0; j < 256; j++)
        {
            if (qftPos < qft.Count && j == qft[qftPos])
            {
                qftPos++;
            }

            for (int i = 0; i < JxlForwardCoefficientOrder.OrderCount; i++)
            {
                counts[qftPos + (i * (qft.Count + 1))] += counters.QfOrdCounts[i, j];
            }
        }

        JxlSimdUtils.Iota(remap, 0);
        remap.CopyTo(clusters);

        int numClusters = Math.Clamp(total / sizeForContextModel / 2, 2, 9);
        int numClustersChroma = Math.Clamp(total / sizeForContextModel / 3, 1, 5);

        // TODO: method incomplete
        // do not forget to ArrayPool<int>.Shared.Return pooledCounts, pooledRemap, pooledClusters if needed
    }

    private sealed class OccCounters : IDisposable
    {
        private readonly int[] qfCounts;
        private readonly int[] dataForQfOrdCounts;
        private readonly int[] ordCounts;

        public OccCounters(JxlImageI rqf, JxlAcStrategyImage acStrategy)
        {
            this.qfCounts = ArrayPool<int>.Shared.Rent(256);
            this.dataForQfOrdCounts = ArrayPool<int>.Shared.Rent(256 * JxlForwardCoefficientOrder.OrderCount);
            this.ordCounts = ArrayPool<int>.Shared.Rent(JxlForwardCoefficientOrder.OrderCount);

            this.QfOrdCounts = new(JxlForwardCoefficientOrder.OrderCount, 256, this.dataForQfOrdCounts);

            for (int y = 0; y < rqf.YSize; y++)
            {
                Span<int> qfRow = rqf.GetRow(y);
                JxlAcStrategyRow acsRow = acStrategy.GetRow(y);

                for (int x = 0; x < rqf.XSize; x++)
                {
                    int ord = JxlCoefficientOrder.StrategyOrder[acsRow[x].RawStrategy];
                    int qf = qfRow[x] - 1;
                    this.qfCounts[qf]++;
                    this.QfOrdCounts[ord, qf]++;
                    this.ordCounts[ord]++;
                }
            }
        }

        public Span<int> QfCounts => this.qfCounts.AsSpan();

        public DenseMatrix<int> QfOrdCounts { get; }

        public Span<int> OrdCounts => this.ordCounts.AsSpan();

        public void Dispose()
        {
            ArrayPool<int>.Shared.Return(this.qfCounts);
            ArrayPool<int>.Shared.Return(this.dataForQfOrdCounts);
            ArrayPool<int>.Shared.Return(this.ordCounts);
        }
    }
}
