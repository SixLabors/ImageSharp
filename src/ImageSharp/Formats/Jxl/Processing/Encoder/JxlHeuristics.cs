// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Numerics.Tensors;
using SixLabors.ImageSharp.Formats.Jxl.Memory;
using SixLabors.ImageSharp.Formats.Jxl.Memory.ImageTypes;
using SixLabors.ImageSharp.Formats.Jxl.Processing.AcStrategy;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder;

internal static class JxlHeuristics
{
    /// <summary>
    /// Default upsampling kernels used by the decoder upsampler.
    /// </summary>
    private const int Size = 5;

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

        byte[]? pooledRemap = null;
        byte[]? pooledClusters = null;
        int countsLength = JxlForwardCoefficientOrder.OrderCount * (qft.Count + 1);

        int[] counts = ArrayPool<int>.Shared.Rent(countsLength);

        Span<byte> remap =
            countsLength <= 128
                ? stackalloc byte[128].Slice(0, countsLength)
                : pooledRemap = ArrayPool<byte>.Shared.Rent(countsLength);

        Span<byte> clusters =
            countsLength <= 128
                ? stackalloc byte[128].Slice(0, countsLength)
                : pooledClusters = ArrayPool<byte>.Shared.Rent(countsLength);

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

        JxlSimdUtils.Iota(remap, (byte)0);
        remap.CopyTo(clusters);

        int numClusters = Math.Clamp(total / sizeForContextModel / 2, 2, 9);
        int numClustersChroma = Math.Clamp(total / sizeForContextModel / 3, 1, 5);

        while (clusters.Length > numClusters)
        {
            clusters.Sort((a, b) => counts[b].CompareTo(counts[a]));
            counts[clusters[^2]] += counts[clusters[^1]];
            counts[^1] = 0;
            remap[^1] = clusters[^2];
            clusters = clusters[..^1];
        }

        for (int i = 0; i < remap.Length; i++)
        {
            while (remap[remap[i]] != remap[i])
            {
                remap[i] = remap[remap[i]];
            }
        }

        Span<byte> remapRemap = stackalloc byte[remap.Length];
        remapRemap.Fill((byte)remap.Length);

        int num = 0;
        for (int i = 0; i < remap.Length; i++)
        {
            if (remapRemap[remap[i]] == remap.Length)
            {
                remapRemap[remap[i]] = (byte)(num++);
            }

            remap[i] = remapRemap[remap[i]];
        }

        // Write the block context map.
        blockCtxMap.ContextMap = remap.ToArray();
        Array.Resize(ref blockCtxMap.ContextMap, remap.Length * 3);

        // For chroma, only use up to numClustersChroma separate block contexts
        // (those for the biggest clusters)
        for (int i = remap.Length; i < remap.Length * 3; i++)
        {
            blockCtxMap.ContextMap[i] = (byte)(num + Math.Clamp(remap[i % remap.Length], 0, numClustersChroma - 1));
        }

        blockCtxMap.ContextCount = TensorPrimitives.Max((ReadOnlySpan<byte>)blockCtxMap.ContextMap.AsSpan()) + 1;

        if (pooledRemap is not null)
        {
            ArrayPool<byte>.Shared.Return(pooledRemap);
        }

        if (pooledClusters is not null)
        {
            ArrayPool<byte>.Shared.Return(pooledClusters);
        }
    }

    public static void StoreMin2(float v, ref float min1, ref float min2)
    {
        if (v < min2)
        {
            if (v < min1)
            {
                min2 = min1;
                min1 = v;
            }
            else
            {
                min2 = v;
            }
        }
    }

    public static void CreateMask(JxlImageF image, JxlImageF mask)
    {
        for (int y = 0; y < image.YSize; y++)
        {
            Span<float> rowN = y > 0 ? image.GetRow(y - 1) : image.GetRow(y);
            Span<float> rowIn = image.GetRow(y);
            Span<float> rowS = y + 1 < image.YSize ? image.GetRow(y + 1) : image.GetRow(y);
            Span<float> rowOut = mask.GetRow(y);

            for (int x = 0; x < image.XSize; x++)
            {
                // Center, west, east, north, south values and their absolute difference
                float c = rowIn[x];
                float w = x > 0 ? rowIn[x - 1] : rowIn[x];
                float e = x + 1 < image.XSize ? rowIn[x + 1] : rowIn[x];
                float n = rowN[x];
                float s = rowS[x];

                // absolute
                float dw = MathF.Abs(c - w);
                float de = MathF.Abs(c - e);
                float dn = MathF.Abs(c - n);
                float ds = MathF.Abs(c - s);

                float min = float.MaxValue;
                float min2 = float.MaxValue;

                StoreMin2(dw, ref min, ref min2);
                StoreMin2(de, ref min, ref min2);
                StoreMin2(dn, ref min, ref min2);
                StoreMin2(ds, ref min, ref min2);

                rowOut[x] = min2;
            }
        }
    }

    public static void ReduceRinging(JxlImageF initial, JxlImageF mask, JxlImageF down)
    {
        int xsize2 = down.XSize;
        int ysize2 = down.YSize;

        for (int y = 0; y < down.YSize; y++)
        {
            Span<float> rowDown = down.GetRow(y);
            Span<float> rowInitial = initial.GetRow(y);

            Span<float> rowMask = mask.GetRow(y);
            Span<float> rowOut = down.GetRow(y);

            for (int x = 0; x < down.XSize; x++)
            {
                float v = rowDown[x];
                float min = rowInitial[x];
                float max = rowInitial[x];

                for (int yi = -1; yi < 2; yi++)
                {
                    for (int xi = -1; xi < 2; xi++)
                    {
                        int x2 = x + xi;
                        int y2 = y + yi;

                        if (x2 < 0 || y2 < 0 || x2 >= xsize2 || y2 >= ysize2)
                        {
                            continue;
                        }

                        min = MathF.Min(min, initial.GetRow(y2)[x2]);
                        max = MathF.Max(max, initial.GetRow(y2)[x2]);
                    }
                }

                rowOut[x] = v;

                const float maskMultiplier = 2;
                float a = rowMask[x] * maskMultiplier;
                float clipMin = min - a;
                float clipMax = max + a;

                if (rowOut[x] < clipMin)
                {
                    rowOut[x] = clipMin;
                }

                if (rowOut[x] > clipMax)
                {
                    rowOut[x] = clipMax;
                }
            }
        }
    }

    public static float UpsamplerDerivative(int x2, int y2, int x, int y)
    {
        ReadOnlySpan<float> kernel = Kernel00;

        if ((x & 1) != 0 && (y & 1) != 0)
        {
            kernel = Kernel11;
        }
        else if ((x & 1) != 0)
        {
            kernel = Kernel10;
        }
        else if ((y & 1) != 0)
        {
            kernel = Kernel01;
        }

        int ix = x / 2;
        int iy = y / 2;
        int kx = x2 - ix + (Size / 2);
        int ky = y2 - iy + (Size / 2);

        if (kx < 0 || kx >= Size || ky < 0 || ky >= Size)
        {
            // Shouldn't happen.
            return 0;
        }

        return kernel[(ky * Size) + kx];
    }

    private static void ElementwiseMultiply<T>(JxlPlane<T> image1, JxlPlane<T> image2, JxlPlane<T> output)
        where T : unmanaged,
        IMultiplyOperators<T, T, T>,
        IMultiplicativeIdentity<T, T>
    {
        int width = image1.XSize;
        int height = image1.YSize;

        if (width != image2.XSize ||
            width != output.XSize ||
            height != image2.YSize ||
            height != output.YSize)
        {
            throw new InvalidOperationException("Each plane must have equal sizes");
        }

        for (int y = 0; y < height; y++)
        {
            ReadOnlySpan<T> row1 = image1.GetRow(y);
            ReadOnlySpan<T> row2 = image2.GetRow(y);
            Span<T> rowOut = output.GetRow(y);

            TensorPrimitives.Multiply(row1, row2, rowOut);
        }
    }

    private static void ElementwiseDivide<T>(JxlPlane<T> image1, JxlPlane<T> image2, JxlPlane<T> output)
        where T : unmanaged, IDivisionOperators<T, T, T>
    {
        int width = image1.XSize;
        int height = image1.YSize;

        if (width != image2.XSize ||
            width != output.XSize ||
            height != image2.YSize ||
            height != output.YSize)
        {
            throw new InvalidOperationException("Each plane must have equal sizes");
        }

        for (int y = 0; y < height; y++)
        {
            ReadOnlySpan<T> row1 = image1.GetRow(y);
            ReadOnlySpan<T> row2 = image2.GetRow(y);
            Span<T> rowOut = output.GetRow(y);

            TensorPrimitives.Divide(row1, row2, rowOut);
        }
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
