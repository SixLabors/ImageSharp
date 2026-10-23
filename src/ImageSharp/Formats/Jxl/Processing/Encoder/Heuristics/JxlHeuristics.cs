// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Numerics.Tensors;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Jxl.Memory;
using SixLabors.ImageSharp.Formats.Jxl.Memory.ImageTypes;
using SixLabors.ImageSharp.Formats.Jxl.Processing.AcStrategy;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Dct;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Modular;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Image;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Quantization;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Heuristics;

internal static class JxlHeuristics
{
    /// <summary>
    /// Default upsampling kernels used by the decoder upsampler.
    /// </summary>
    private const int Size = 5;

    /// <summary>
    /// Gets a very simple table that maps contexts to integers.
    /// </summary>
    private static ReadOnlySpan<byte> SimpleContextMap =>
    [
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
    ];

    /// <summary>
    /// Gets the kernels for sharper 2x downsampling.
    /// </summary>
    private static ReadOnlySpan<float> Downsample2SharperKernel =>
    [
        -0.000314256996835f, -0.000314256996835f, -0.000897597057705f,
        -0.000562751488849f, -0.000176807273646f, 0.001864627368902f,
        0.001864627368902f,  -0.000176807273646f, -0.000562751488849f,
        -0.000897597057705f, -0.000314256996835f, -0.000314256996835f,
        -0.000314256996835f, -0.001527942804748f, -0.000121760530512f,
        0.000191123989093f,  0.010193185932466f,  0.058637519197110f,
        0.058637519197110f,  0.010193185932466f,  0.000191123989093f,
        -0.000121760530512f, -0.001527942804748f, -0.000314256996835f,
        -0.000897597057705f, -0.000121760530512f, 0.000946363683751f,
        0.007113577630288f,  0.000437956841058f,  -0.000372823835211f,
        -0.000372823835211f, 0.000437956841058f,  0.007113577630288f,
        0.000946363683751f,  -0.000121760530512f, -0.000897597057705f,
        -0.000562751488849f, 0.000191123989093f,  0.007113577630288f,
        0.044592622228814f,  0.000222278879007f,  -0.162864473015945f,
        -0.162864473015945f, 0.000222278879007f,  0.044592622228814f,
        0.007113577630288f,  0.000191123989093f,  -0.000562751488849f,
        -0.000176807273646f, 0.010193185932466f,  0.000437956841058f,
        0.000222278879007f,  -0.000913092543974f, -0.017071696107902f,
        -0.017071696107902f, -0.000913092543974f, 0.000222278879007f,
        0.000437956841058f,  0.010193185932466f,  -0.000176807273646f,
        0.001864627368902f,  0.058637519197110f,  -0.000372823835211f,
        -0.162864473015945f, -0.017071696107902f, 0.414660099370354f,
        0.414660099370354f,  -0.017071696107902f, -0.162864473015945f,
        -0.000372823835211f, 0.058637519197110f,  0.001864627368902f,
        0.001864627368902f,  0.058637519197110f,  -0.000372823835211f,
        -0.162864473015945f, -0.017071696107902f, 0.414660099370354f,
        0.414660099370354f,  -0.017071696107902f, -0.162864473015945f,
        -0.000372823835211f, 0.058637519197110f,  0.001864627368902f,
        -0.000176807273646f, 0.010193185932466f,  0.000437956841058f,
        0.000222278879007f,  -0.000913092543974f, -0.017071696107902f,
        -0.017071696107902f, -0.000913092543974f, 0.000222278879007f,
        0.000437956841058f,  0.010193185932466f,  -0.000176807273646f,
        -0.000562751488849f, 0.000191123989093f,  0.007113577630288f,
        0.044592622228814f,  0.000222278879007f,  -0.162864473015945f,
        -0.162864473015945f, 0.000222278879007f,  0.044592622228814f,
        0.007113577630288f,  0.000191123989093f,  -0.000562751488849f,
        -0.000897597057705f, -0.000121760530512f, 0.000946363683751f,
        0.007113577630288f,  0.000437956841058f,  -0.000372823835211f,
        -0.000372823835211f, 0.000437956841058f,  0.007113577630288f,
        0.000946363683751f,  -0.000121760530512f, -0.000897597057705f,
        -0.000314256996835f, -0.001527942804748f, -0.000121760530512f,
        0.000191123989093f,  0.010193185932466f,  0.058637519197110f,
        0.058637519197110f,  0.010193185932466f,  0.000191123989093f,
        -0.000121760530512f, -0.001527942804748f, -0.000314256996835f,
        -0.000314256996835f, -0.000314256996835f, -0.000897597057705f,
        -0.000562751488849f, -0.000176807273646f, 0.001864627368902f,
        0.001864627368902f,  -0.000176807273646f, -0.000562751488849f,
        -0.000897597057705f, -0.000314256996835f, -0.000314256996835f
    ];

    private static ReadOnlySpan<float> Kernel00 =>
    [
        -0.01716200f, -0.03452303f, -0.04022174f, -0.02921014f, -0.00624645f,
        -0.03452303f, 0.14111091f,  0.28896755f,  0.00278718f,  -0.01610267f,
        -0.04022174f, 0.28896755f,  0.56661550f,  0.03777607f,  -0.01986694f,
        -0.02921014f, 0.00278718f,  0.03777607f,  -0.03144731f, -0.01185068f,
        -0.00624645f, -0.01610267f, -0.01986694f, -0.01185068f, -0.00213539f,
    ];

    private static ReadOnlySpan<float> Kernel01 =>
    [
        -0.00624645f, -0.01610267f, -0.01986694f, -0.01185068f, -0.00213539f,
        -0.02921014f, 0.00278718f,  0.03777607f,  -0.03144731f, -0.01185068f,
        -0.04022174f, 0.28896755f,  0.56661550f,  0.03777607f,  -0.01986694f,
        -0.03452303f, 0.14111091f,  0.28896755f,  0.00278718f,  -0.01610267f,
        -0.01716200f, -0.03452303f, -0.04022174f, -0.02921014f, -0.00624645f,
    ];

    private static ReadOnlySpan<float> Kernel10 =>
    [
        -0.00624645f, -0.02921014f, -0.04022174f, -0.03452303f, -0.01716200f,
        -0.01610267f, 0.00278718f,  0.28896755f,  0.14111091f,  -0.03452303f,
        -0.01986694f, 0.03777607f,  0.56661550f,  0.28896755f,  -0.04022174f,
        -0.01185068f, -0.03144731f, 0.03777607f,  0.00278718f,  -0.02921014f,
        -0.00213539f, -0.01185068f, -0.01986694f, -0.01610267f, -0.00624645f,
    ];

    private static ReadOnlySpan<float> Kernel11 =>
    [
        -0.00213539f, -0.01185068f, -0.01986694f, -0.01610267f, -0.00624645f,
        -0.01185068f, -0.03144731f, 0.03777607f,  0.00278718f,  -0.02921014f,
        -0.01986694f, 0.03777607f,  0.56661550f,  0.28896755f,  -0.04022174f,
        -0.01610267f, 0.00278718f,  0.28896755f,  0.14111091f,  -0.03452303f,
        -0.00624645f, -0.02921014f, -0.04022174f, -0.03452303f, -0.01716200f,
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

        if (width != image2.XSize || width != output.XSize ||
            height != image2.YSize || height != output.YSize)
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

    public static void AntiUpsample(JxlImageF input, JxlImageF d)
    {
        int xsize = input.XSize;
        int ysize = input.YSize;
        int xsize2 = d.XSize;
        int ysize2 = d.YSize;
        int k0 = Size - 1;
        int k1 = Size;

        for (int y2 = 0; y2 < ysize2; ++y2)
        {
            Span<float> row = d.GetRow(y2);

            for (int x2 = 0; x2 < xsize2; ++x2)
            {
                int x0 = (x2 * 2) - k0;
                if (x0 < 0)
                {
                    x0 = 0;
                }

                int x1 = (x2 * 2) + k1 + 1;
                if (x1 > xsize)
                {
                    x1 = xsize;
                }

                int y0 = (y2 * 2) - k0;
                if (y0 < 0)
                {
                    y0 = 0;
                }

                int y1 = (y2 * 2) + k1 + 1;
                if (y1 > ysize)
                {
                    y1 = ysize;
                }

                float sum = 0;
                for (int y = y0; y < y1; ++y)
                {
                    Span<float> rowIn = input.GetRow(y);

                    for (int x = x0; x < x1; ++x)
                    {
                        float deriv = UpsamplerDerivative(x2, y2, x, y);
                        sum += deriv * rowIn[x];
                    }
                }

                row[x2] = sum;
            }
        }
    }

    public static void UpsampleImage(JxlImageF input, JxlImageF output)
    {
        int xsize = input.XSize;
        int ysize = input.YSize;
        int xsize2 = output.XSize;
        int ysize2 = output.YSize;

        for (int y = 0; y < ysize2; y++)
        {
            for (int x = 0; x < xsize2; x++)
            {
                ReadOnlySpan<float> kernel = Kernel00;

                if (((x & 1) & (y & 1)) != 0)
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

                float sum = 0;
                int x2 = x / 2;
                int y2 = y / 2;

                // The min, max is initially opposite
                float min = float.MaxValue;
                float max = float.MinValue;

                for (int ky = 0; ky < Size; ky++)
                {
                    for (int kx = 0; kx < Size; kx++)
                    {
                        int xi = x2 - (Size / 2) + kx;
                        int yi = y2 - (Size / 2) + ky;

                        if (xi < 0)
                        {
                            xi = 0;
                        }

                        if (xi >= xsize)
                        {
                            xi = input.XSize - 1;
                        }

                        if (yi < 0)
                        {
                            yi = 0;
                        }

                        if (yi >= ysize)
                        {
                            yi = input.YSize - 1;
                        }

                        min = MathF.Min(min, input.GetRow(yi)[xi]);
                        max = MathF.Max(max, input.GetRow(yi)[xi]);
                    }
                }

                for (int ky = 0; ky < Size; ky++)
                {
                    for (int kx = 0; kx < Size; kx++)
                    {
                        int xi = x2 - (Size / 2) + kx;
                        int yi = y2 - (Size / 2) + ky;

                        if (xi < 0)
                        {
                            xi = 0;
                        }

                        if (xi >= xsize)
                        {
                            xi = input.XSize - 1;
                        }

                        if (yi < 0)
                        {
                            yi = 0;
                        }

                        if (yi >= ysize)
                        {
                            yi = input.YSize - 1;
                        }

                        sum += input.GetRow(yi)[xi] * kernel[(ky * Size) + kx];
                    }
                }

                output.GetRow(y)[x] = sum;

                if (output.GetRow(y)[x] < min)
                {
                    output.GetRow(y)[x] = min;
                }

                if (output.GetRow(y)[x] > max)
                {
                    output.GetRow(y)[x] = max;
                }
            }
        }
    }

    public static void FindBestDequantMatrices(JxlCompressParameters cparams, JxlModularFrameEncoder modularFrameEncoder, out JxlDequantMatrices dequantMatrices)
    {
        dequantMatrices = new();
        if (cparams.MaxErrorMode || cparams.DisablePerceptualOptimizations)
        {
            // Weights for the Mean Squared Error (MSE)
            ReadOnlySpan<float> mseWeights = [0.001f, 0.001f, 0.001f];

            // When perceptual options are enabled, these are MSE weights,
            // otherwise these are max errors specified by user compression parameters.
            ReadOnlySpan<float> wp = cparams.DisablePerceptualOptimizations ? mseWeights : cparams.MaxError;

            float[][] weights = [[1.0f / wp[0]], [1.0f / wp[1]], [1.0f / wp[2]]];

            JxlDctQuantWeightParameters dctParameters = new(weights);
            JxlQuantizerEncoding[] encodings = ArrayHelpers.CreateAndInitialize(JxlQuantizerConstants.NumberOfQuantizerTables, () => JxlQuantizerEncoding.Dct(dctParameters));

            dequantMatrices.SetCustom(encodings, modularFrameEncoder);
            Span<float> dcWeights = [1.0f / wp[0], 1.0f / wp[1], 1.0f / wp[2]];

            dequantMatrices.SetCustomDc(dequantMatrices, dcWeights);
        }
    }

    public static void DownsampleImage2Sharper(Configuration configuration, JxlImageF input, JxlImageF output)
    {
        const int kernelX = 12;
        const int kernelY = 12;

        // A buffer used by the downsampling iteration
        // ⚠️ IMPORTANT ⚠️ The length of the inline array
        // MUST be equal to kernelY. When we update kernelY,
        // please make sure to update the width of this
        // inline array as well.
        InlineArray12<Memory<float>> rowIn = default;

        // Retrieving image dimensions
        int xSize = input.XSize;
        int ySize = input.YSize;

        JxlImageF tempDownsample = new(configuration, xSize, ySize);
        JxlImageOperations.ForceCopyImage(input, tempDownsample);

        using JxlImageF boxDownsample = DownsampleImage(tempDownsample, 2);
        tempDownsample.Dispose();

        using JxlImageF mask = new(configuration, boxDownsample.XSize, boxDownsample.YSize);
        CreateMask(boxDownsample, mask);

        for (int y = 0; y < output.YSize; y++)
        {
            Span<float> rowOut = output.GetRow(y);
            Span<float> rowMask = mask.GetRow(y);

            for (int ky = 0; ky < kernelY; ky++)
            {
                int iy = (y * 2) + ky - ((kernelY - 1) / 2);

                if (iy < 0)
                {
                    iy = 0;
                }
                else if (iy >= ySize)
                {
                    iy = ySize - 1;
                }

                rowIn[ky] = input.GetRowMemory(iy);
            }

            for (int x = 0; x < output.XSize; x++)
            {
                // Processing of min and max values of the original image
                float min = float.MaxValue;
                float max = float.MinValue;

                // Radius
                const int R = 5;

                for (int ky = R; ky + R < kernelY; ky++)
                {
                    // Reusing this span as a performance improvement.
                    // Memory<T>.Span seems to be expensive to compute.
                    Span<float> currentRowIn = rowIn[ky].Span;

                    for (int kx = R; kx + R < kernelX; kx++)
                    {
                        int ix = (x * 2) + kx - ((kernelX - 1) / 2);

                        // Making sure x is not negative. If it is, set it to 0.
                        if (ix < 0)
                        {
                            ix = 0;
                        }

                        // ix should not exceed image width. If it does, limit it.
                        else if (ix >= xSize)
                        {
                            ix = xSize - 1;
                        }

                        min = float.Min(min, currentRowIn[ix]);
                        max = float.Max(max, currentRowIn[ix]);
                    }
                }

                float sum = 0;

                for (int ky = 0; ky < kernelY; ky++)
                {
                    // Reusing this span as a performance improvement.
                    // Memory<T>.Span seems to be expensive to compute.
                    Span<float> currentRowIn = rowIn[ky].Span;

                    // ky * kernelX to prevent repeated multiplications
                    int kyKernelX = ky * kernelX;

                    for (int kx = 0; kx < kernelX; kx++)
                    {
                        int ix = (x * 2) + kx - ((kernelX - 1) / 2);

                        // Making sure x is not negative. If it is, set it to 0.
                        if (ix < 0)
                        {
                            ix = 0;
                        }

                        // ix should not exceed image width. If it does, limit it.
                        else if (ix >= xSize)
                        {
                            ix = xSize - 1;
                        }

                        sum += currentRowIn[ix] * Downsample2SharperKernel[kyKernelX + kx];
                    }
                }

                rowOut[x] = sum;

                const float maskMultiplier = 1;

                float a = rowMask[x] * maskMultiplier;

                // clipMin specifies the minimum bound.
                // clipMax specifies the maximum bound.
                float clipMin = min - a;
                float clipMax = max + a;

                // To prevent repeated bounds checks,
                // this is a reference to rowOut[x].
                ref float rowOutX = ref rowOut[x];

                // Clamping to stay within the bounds
                if (rowOutX < clipMin)
                {
                    rowOutX = clipMin;
                }
                else if (rowOutX > clipMax)
                {
                    rowOutX = clipMax;
                }
            }
        }
    }

    /// <summary>
    /// Computes the weighted, mask-adjusted L2 difference between
    /// two image blocks independently for each channel.
    /// </summary>
    /// <param name="a">First image</param>
    /// <param name="b">Second image</param>
    /// <param name="mask1x1">Mask for each channel</param>
    /// <param name="by">Y block coordinate</param>
    /// <param name="bx">X block coordinate</param>
    /// <returns>
    /// The L2 distance for the images.
    /// </returns>
    public static float ComputeBlockL2Distance(JxlImage3F a, JxlImage3F b, JxlImageF mask1x1, int by, int bx)
    {
        Rectangle rect = RectangleUtils.CreateRectangle(
            bx * JxlFrameDimensions.BlockDimensions,
            by * JxlFrameDimensions.BlockDimensions,
            JxlFrameDimensions.BlockDimensions,
            JxlFrameDimensions.BlockDimensions,
            a.XSize,
            a.YSize);

        // Squared errors per channel. These are initially 0.
        InlineArray3<float> squaredError = default;

        for (int y = 0; y < rect.Height; y++)
        {
            // We precompute repeated PlaneRow calls for performance.
            // The result is wrapped in an InlineArray3<T>, so we can
            // retrieve the rows using an array-like access, which
            // lets us use loops for each of the 3 channels.
            //
            // Note that we have to use Memory<T> here, as Span<T>
            // cannot be wrapped in an InlineArray.
            InlineArray3<Memory<float>> rowA = default;
            InlineArray3<Memory<float>> rowB = default;

            // Computing for the 'a' image first
            rowA[0] = a.Plane(0).GetRowMemory(rect, y);
            rowA[1] = a.Plane(1).GetRowMemory(rect, y);
            rowA[2] = a.Plane(2).GetRowMemory(rect, y);

            // And for the 'b' image here
            rowB[0] = a.Plane(0).GetRowMemory(rect, y);
            rowB[1] = a.Plane(1).GetRowMemory(rect, y);
            rowB[2] = a.Plane(2).GetRowMemory(rect, y);

            // We store a row for the mask directly.
            // It's only one channel, so it can be stored
            // as a single Span<T>.
            Span<float> rowMask = mask1x1.GetRow(rect, y);

            // Actual computation logic here.
            for (int x = 0; x < rect.Width; x++)
            {
                // First we retrieve the mask for the current row and column.
                // And then we compute the square of it.
                float mask = rowMask[x];
                float mask2 = mask * mask;

                for (int i = 0; i < 3; ++i)
                {
                    // Here we compute the difference of the
                    // two rows at the current channel and location.
                    // We then compute the square of the difference.
                    float diff = rowA[i].Span[x] - rowB[i].Span[x];
                    float diff2 = diff * diff;

                    // Add the squared mask multiplied by the squared
                    // difference to the error for this channel.
                    squaredError[i] += mask2 * diff2;
                }
            }
        }

        // These are weights for each channel. They're read-only.
        ReadOnlySpan<float> weights = [12.339445295782363f, 1.0f, 0.2f];

        // The result is a dot product of squared errors for each
        // channel multiplied by the channel's respective weights.
        float result =
            (weights[0] * squaredError[0])
            + (weights[1] * squaredError[1])
            + (weights[2] * squaredError[2]);

        return result;
    }

    private sealed class OccCounters : IDisposable
    {
        // These are pooled using Array<T>.Shared.Rent.
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
