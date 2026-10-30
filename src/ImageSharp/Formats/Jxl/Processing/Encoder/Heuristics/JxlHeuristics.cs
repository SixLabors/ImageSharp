// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Numerics.Tensors;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Formats.Jxl.Cms;
using SixLabors.ImageSharp.Formats.Jxl.IO.FrameHeader;
using SixLabors.ImageSharp.Formats.Jxl.IO.Metadata;
using SixLabors.ImageSharp.Formats.Jxl.Memory;
using SixLabors.ImageSharp.Formats.Jxl.Processing.AcStrategy;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Butteraugli;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Dct;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder.Group;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.AuxiliaryOutput;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Modular;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Image;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Transforms;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Quantization;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Splines;
using SixLabors.ImageSharp.Memory;

// For a 'stackalloc byte[128].Slice(0, n)' this suggests
// to update to '(stackalloc byte[128])[..n]', then suggests
// to remove the parentheses, producing invalid syntax.
#pragma warning disable IDE0057 // Use range operator

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

    public static void DownsampleImage2Sharper(Configuration configuration, ref JxlImage3<float> opsin)
    {
        using JxlImage3<float> downsampled = new(
            configuration,
            JxlMath.DivCeil(opsin.XSize, 2) + JxlFrameDimensions.BlockDimensions,
            JxlMath.DivCeil(opsin.YSize, 2) + JxlFrameDimensions.BlockDimensions);

        if (!downsampled.ShrinkTo(
            downsampled.XSize - JxlFrameDimensions.BlockDimensions,
            downsampled.YSize - JxlFrameDimensions.BlockDimensions))
        {
            throw new InvalidOperationException("Could not shrink the downsampled image.");
        }

        DownsampleImage2Sharper(configuration, opsin.Plane(0), downsampled.Plane(0));
        DownsampleImage2Sharper(configuration, opsin.Plane(1), downsampled.Plane(1));
        DownsampleImage2Sharper(configuration, opsin.Plane(2), downsampled.Plane(2));

        opsin = downsampled;
    }

    public static void DownsampleImage2Iterative(Configuration configuration, JxlPlane<float> original, JxlPlane<float> output)
    {
        // Get the image sizes.
        int xsize = original.XSize;
        int ysize = original.YSize;
        int xsize2 = JxlMath.DivCeil(original.XSize, 2);
        int ysize2 = JxlMath.DivCeil(original.YSize, 2);

        // oldBoxDownsample is there so it can be disposed.
        JxlPlane<float> oldBoxDownsample = new(configuration, xsize, ysize);
        JxlPlane<float> boxDownsample = oldBoxDownsample;
        JxlImageOperations.ForceCopyImage(original, boxDownsample);

        // Downsample.
        boxDownsample = DownsampleImage(boxDownsample, 2);
        oldBoxDownsample.Dispose();

        // Create a mask.
        JxlPlane<float> mask = new(configuration, boxDownsample.XSize, boxDownsample.YSize));
        CreateMask(boxDownsample, mask);

        // Try to shrink the output down to the width/height divided by 2.
        if (!output.ShrinkTo(xsize2, ysize2))
        {
            throw new InvalidOperationException("Could not shrink output image");
        }

        // Contains the result of sharper image downscale.
        JxlPlane<float> initial = new(
            configuration,
            JxlMath.DivCeil(original.XSize, 2) + JxlFrameDimensions.BlockDimensions,
            JxlMath.DivCeil(original.YSize, 2) + JxlFrameDimensions.BlockDimensions);

        if (!initial.ShrinkTo(initial.XSize - JxlFrameDimensions.BlockDimensions, initial.YSize - JxlFrameDimensions.BlockDimensions))
        {
            throw new InvalidOperationException("Could not shrink initial image");
        }

        // Downscale the image and store into initial.
        DownsampleImage2Sharper(configuration, original, initial);

        JxlPlane<float> down = new(configuration, initial.XSize, initial.YSize);
        JxlImageOperations.ForceCopyImage(initial, down);

        JxlPlane<float> up = new(configuration, xsize, ysize);
        JxlPlane<float> corr = new(configuration, xsize, ysize);
        JxlPlane<float> corr2 = new(configuration, xsize2, ysize2);
        JxlPlane<float> weights = new(configuration, xsize, ysize);

        for (int y = 0; y < weights.YSize; y++)
        {
            Span<float> row = weights.GetRow(y);
            row[..weights.XSize].Fill(1);
        }

        JxlPlane<float> weights2 = new(configuration, xsize2, ysize2);
        AntiUpsample(weights, weights2);

        // Three iterations.
        const int numberOfIterations = 3;
        for (int it = 0; it < numberOfIterations; ++it)
        {
            UpsampleImage(down, up);
            corr = JxlImageOperations.LinComb(configuration, 1, original, -1, up) ?? throw new InvalidOperationException("Linear combination failed");
            ElementwiseMultiply(corr, weights, corr);
            AntiUpsample(corr, corr2);
            ElementwiseDivide(corr2, weights2, corr2);

            down = JxlImageOperations.LinComb(configuration, 1, down, 1, corr2) ?? throw new InvalidOperationException("Linear combination failed");
        }

        // Reduce ringing.
        ReduceRinging(initial, mask, down);

        for (int y = 0; y < down.YSize; y++)
        {
            Span<float> downRow = down.GetRow(y);
            Span<float> outputRow = output.GetRow(y);

            downRow[..down.XSize].CopyTo(outputRow);
        }
    }

    public static void FindBestBlockEntropyModel(
        JxlCompressParameters cparameters,
        JxlPlane<int> rqf,
        JxlAcStrategyImage acStrategy,
        JxlBlockContextMap blockCtxMap)
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
                if (j == lastCut + 1 && j != 0)
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

    public static void CreateMask(JxlPlane<float> image, JxlPlane<float> mask)
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

    public static void ReduceRinging(JxlPlane<float> initial, JxlPlane<float> mask, JxlPlane<float> down)
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

                        if ((uint)x2 >= xsize2 || (uint)y2 >= ysize2)
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

        if ((uint)kx >= Size || (uint)ky >= Size)
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

    public static void AntiUpsample(JxlPlane<float> input, JxlPlane<float> d)
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

    public static void UpsampleImage(JxlPlane<float> input, JxlPlane<float> output)
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
                        else if (xi >= xsize)
                        {
                            xi = xsize - 1;
                        }

                        if (yi < 0)
                        {
                            yi = 0;
                        }
                        else if (yi >= ysize)
                        {
                            yi = ysize - 1;
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
                        else if (xi >= xsize)
                        {
                            xi = xsize - 1;
                        }

                        if (yi < 0)
                        {
                            yi = 0;
                        }
                        else if (yi >= ysize)
                        {
                            yi = ysize - 1;
                        }

                        sum += input.GetRow(yi)[xi] * kernel[(ky * Size) + kx];
                    }
                }

                ref float rowYX = ref output.GetRow(y)[x];

                rowYX = sum;

                if (rowYX < min)
                {
                    rowYX = min;
                }
                else if (rowYX > max)
                {
                    rowYX = max;
                }
            }
        }
    }

    public static void FindBestDequantMatrices(
        JxlCompressParameters cparams,
        JxlModularFrameEncoder modularFrameEncoder,
        out JxlDequantMatrices dequantMatrices)
    {
        dequantMatrices = new();
        if (cparams.MaxErrorMode || cparams.DisablePerceptualOptimizations)
        {
            // Weights for the Mean Squared Error (MSE)
            ReadOnlySpan<float> mseWeights = [0.001f, 0.001f, 0.001f];

            // When perceptual options are enabled, these are MSE weights,
            // otherwise these are max errors specified by user compression parameters.
            ReadOnlySpan<float> wp = cparams.DisablePerceptualOptimizations ? mseWeights : cparams.MaxError;

            ReadOnlySpan<float> weights = [1.0f / wp[0], 1.0f / wp[1], 1.0f / wp[2]];

            JxlDctQuantWeightParameters dctParameters = new(weights, 1);
            JxlQuantizerEncoding[] encodings = new JxlQuantizerEncoding[JxlQuantizerConstants.NumberOfQuantizerTables];
            for (int i = 0; i < encodings.Length; i++)
            {
                encodings[i] = JxlQuantizerEncoding.Dct(in dctParameters);
            }

            dequantMatrices.SetCustom(encodings, modularFrameEncoder);
            Span<float> dcWeights = [1.0f / wp[0], 1.0f / wp[1], 1.0f / wp[2]];

            dequantMatrices.SetCustomDc(dequantMatrices, dcWeights);
        }
    }

    public static void DownsampleImage2Sharper(Configuration configuration, JxlPlane<float> input, JxlPlane<float> output)
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

        JxlPlane<float> tempDownsample = new(configuration, xSize, ySize);
        JxlImageOperations.ForceCopyImage(input, tempDownsample);

        using JxlPlane<float> boxDownsample = DownsampleImage(tempDownsample, 2);
        tempDownsample.Dispose();

        using JxlPlane<float> mask = new(configuration, boxDownsample.XSize, boxDownsample.YSize);
        CreateMask(boxDownsample, mask);

        for (int y = 0; y < output.YSize; y++)
        {
            Span<float> rowOut = output.GetRow(y);
            Span<float> rowMask = mask.GetRow(y);

            for (int ky = 0; ky < kernelY; ky++)
            {
                int iy = Math.Clamp((y * 2) + ky - ((kernelY - 1) / 2), 0, ySize - 1);
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
    public static float ComputeBlockL2Distance(JxlImage3<float> a, JxlImage3<float> b, JxlPlane<float> mask1x1, int by, int bx)
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

    public static void ComputeARHeuristics(Configuration configuration, JxlFrameHeader frameHeader, JxlPassesEncoderContext encoderState, JxlImage3<float> originalOpsin, Rectangle rect)
    {
        JxlCompressParameters cparams = encoderState.CompressionParameters;
        JxlPassesCommonContext shared = encoderState.PassesCommonContext;
        JxlFrameDimensions frameDimensions = shared.FrameDimensions;
        JxlPlane<float> initialQuantMasking1x1 = encoderState.InitialQuantMasking1x1;
        JxlPlane<byte> epfSharpness = shared.EpfSharpness;

        float clampedButteraugli = MathF.Min(5.0f, cparams.ButteraugliDistance);
        if (cparams.ButteraugliDistance < MinimumButteraugliForDynamicAR ||
            cparams.SpeedTier > JxlSpeedTier.Wombat ||
            frameHeader.LoopFilter.EpfIterations == 0)
        {
            JxlImageOperations.FillPlane((byte)4, epfSharpness, epfSharpness.GetRectangle());
            return;
        }

        // Steps for the edge preserving filter
        Span<byte> epfSteps = cparams.ButteraugliDistance > 4.5f ? [0, 4] : [0, 2, 7];

        const int numberOfEPFValues = 8;
        Span<int> epfStepsLookupTable = stackalloc int[numberOfEPFValues];
        epfStepsLookupTable.Clear();

        for (int i = 0; i < epfSteps.Length; i++)
        {
            epfStepsLookupTable[epfSteps[i]] = i;
        }

        // Contains the L2 distance for each coefficient. This will be
        // initialized.
        JxlPlane<float>[] errorImages = new JxlPlane<float>[numberOfEPFValues];

        for (int i = 0; i < epfSteps.Length; i++)
        {
            // Allocate and reconstruct the images.
            byte value = epfSteps[i];
            JxlImage3<float> decoded = ReconstructImage(configuration, frameHeader, shared, encoderState.Coefficients);
            errorImages[value] = new(configuration, frameDimensions.XSizeBlocks, frameDimensions.YSizeBlocks);

            // Compute the L2 distance between original opsin and the decoded image.
            for (int by = 0; by < frameDimensions.YSizeBlocks; by++)
            {
                Span<float> errorRow = errorImages[value].GetRow(by);
                for (int bx = 0; bx < frameDimensions.XSize; bx++)
                {
                    errorRow[bx] = ComputeBlockL2Distance(originalOpsin, decoded, initialQuantMasking1x1, by, bx);
                }
            }
        }

        using Buffer2D<int> histo = configuration.MemoryAllocator.Allocate2D<int>(9, numberOfEPFValues);
        Span<int> totals = stackalloc int[16].Slice(0, 9);
        totals.Fill(1);
        const float favorNoSmoothing = 0.99f;

        for (int by = 0; by < frameDimensions.YSizeBlocks; by++)
        {
            // Get the current (output) and previous row. If Y
            // is 0, then the previous row is also row 0.
            Span<byte> outputRow = epfSharpness.GetRow(by);
            Span<byte> previousRow = epfSharpness.GetRow(by > 0 ? by - 1 : 0);

            for (int bx = 0; bx < frameDimensions.XSizeBlocks; bx++)
            {
                byte bestValue = 0;
                float bestError = float.MaxValue;

                // Neighboring values
                byte topValue = by > 0 ? previousRow[bx] : (byte)0;
                byte leftValue = bx > 0 ? outputRow[bx - 1] : (byte)0;

                // Errors of neighboring values
                float topError = errorImages[topValue].GetRow(by)[bx];
                float leftError = errorImages[leftValue].GetRow(by)[bx];

                for (int i = 0; i < epfSteps.Length; i++)
                {
                    // Current EPF step
                    byte val = epfSteps[i];

                    // Current error
                    float error = errorImages[val].GetRow(by)[bx];

                    if (val == 0)
                    {
                        error *= favorNoSmoothing;
                    }

                    if (error < bestError)
                    {
                        bestValue = val;
                        bestError = error;
                    }
                }

                if (bestError < MathF.Min(topError, leftError))
                {
                    outputRow[bx] = bestValue;
                }
                else if (topError < leftError)
                {
                    outputRow[bx] = topValue;
                }
                else
                {
                    outputRow[bx] = leftValue;
                }

                int context = (epfStepsLookupTable[topValue] * 3) + epfStepsLookupTable[leftValue];
                histo[context, outputRow[bx]]++;
                totals[context]++;
            }
        }

        const float c3base = 0.98017198824148288f;
        const float c3clamp = 0.85970338919928291f;
        float c3 = MathF.Max(c3clamp, MathF.Pow(c3base, clampedButteraugli));
        const float c5 = 0.1087690359555803f;

        Span<float> mul = stackalloc float[32].Slice(0, 3 * 3 * 3);
        mul.Clear();

        foreach (byte topValue in epfSteps)
        {
            foreach (byte leftValue in epfSteps)
            {
                foreach (byte val in epfSteps)
                {
                    int context = (epfStepsLookupTable[topValue] * 3) + epfStepsLookupTable[leftValue];
                    Span<int> contextHisto = histo.DangerousGetRowSpan(context);

                    int multiplierIndex = epfStepsLookupTable[val] + (3 * context);
                    mul[multiplierIndex] = 1.0f / (1.0f + (c5 * Numerics.Log1P(contextHisto[val] / totals[context]) / clampedButteraugli));

                    if (val == 0)
                    {
                        mul[multiplierIndex] *= c3;
                    }
                }
            }
        }

        for (int by = 0; by < frameDimensions.YSizeBlocks; by++)
        {
            // Get the current (output) and previous row. If Y
            // is 0, then the previous row is also row 0.
            Span<byte> outputRow = epfSharpness.GetRow(by);
            Span<byte> previousRow = epfSharpness.GetRow(by > 0 ? by - 1 : 0);

            for (int bx = 0; bx < frameDimensions.XSizeBlocks; bx++)
            {
                byte bestValue = 0;
                float bestError = float.MaxValue;

                // Neighbors
                byte topValue = by > 0 ? previousRow[bx] : (byte)0;
                byte leftValue = bx > 0 ? outputRow[bx - 1] : (byte)0;

                int context = (epfStepsLookupTable[topValue] * 3) + epfStepsLookupTable[leftValue];

                foreach (byte val in epfSteps)
                {
                    int multiplierIndex = epfStepsLookupTable[val] + (3 * context);
                    float error = errorImages[val].GetRow(by)[bx] * mul[multiplierIndex];

                    if (error < bestError)
                    {
                        bestValue = val;
                        bestError = error;
                    }
                }

                outputRow[bx] = bestValue;
            }
        }
    }

    public static void DownsampleImage2Iterative(Configuration configuration, ref JxlImage3<float> opsin)
    {
        JxlImage3<float> downsampled = new(
            configuration,
            JxlMath.DivCeil(opsin.XSize, 2) + JxlFrameDimensions.BlockDimensions,
            JxlMath.DivCeil(opsin.YSize, 2) + JxlFrameDimensions.BlockDimensions);

        if (!downsampled.ShrinkTo(downsampled.XSize - JxlFrameDimensions.BlockDimensions, downsampled.YSize - JxlFrameDimensions.BlockDimensions))
        {
            throw new InvalidOperationException("Could not shrink image");
        }

        // Temporary conversion data
        using JxlImage3<float> rgb = new(configuration, opsin.XSize, opsin.YSize);

        // Convert Opsin XYB to linear
        if (!JxlXybDecoder.OpsinToLinear(opsin, rgb.GetRectangle(), rgb, KnownXybTransforms.Default))
        {
            throw new InvalidOperationException("Failed to convert Opsin color-space to RGB");
        }

        // Compute the Butteraugli mask for the image.
        JxlPlane<float> mask = new(configuration, opsin.XSize, opsin.YSize);
        ButteraugliParameters butterParameters = new();
        ButteraugliComparator butter = ButteraugliComparator.Make(configuration, rgb, butterParameters);

        if (!butter.Mask(configuration, mask))
        {
            throw new InvalidOperationException("Could not compute the Butteraugli mask.");
        }

        JxlPlane<float> maskFuzzy = new(configuration, opsin.XSize, opsin.YSize);

        for (int c = 0; c < 3; c++)
        {
            // This calls into a separate method overload.
            DownsampleImage2Iterative(configuration, opsin.Plane(c), downsampled.Plane(c));
        }

        opsin = downsampled;
    }

    public static JxlImage3<float> ReconstructImage(Configuration configuration, JxlFrameHeader origFrameHeader, JxlPassesCommonContext shared, IReadOnlyList<IJxlDctAcImage> coeffs)
    {
        JxlFrameDimensions frameDim = shared.FrameDimensions;

        JxlFrameHeader frameHeader = origFrameHeader;
        frameHeader.UpdateFlag(shared.ImageFeatures.PatchDictionary.HasAny, (ulong)JxlFrameHeaderFlags.Patches);
        frameHeader.UpdateFlag(shared.ImageFeatures.Splines.HasAny, (ulong)JxlFrameHeaderFlags.Splines);
        frameHeader.ColorTransform = JxlColorTransform.None;

        JxlCodecMetadata metadata = frameHeader.Metadata!.DeepClone();
        metadata.ImageMetadata!.ExtraChannels.Clear();
        metadata.ImageMetadata!.ExtraChannelCount = metadata.ImageMetadata.ExtraChannels.Count;
        frameHeader.Metadata = metadata;
        frameHeader.ExtraChannelUpsampling.Clear();

        bool isGray = shared.CodecMetadata.ImageMetadata.ColorEncoding.IsGray;

        JxlPassesDecoderContext decState = new(frameHeader, configuration);

        decState.OutputEncodingInfo.SetFromMetadata(shared.CodecMetadata);
        decState.OutputEncodingInfo.MaybeSetColorEncoding(JxlColorEncoding.LinearSRgb(isGray));

        decState.Shared = shared;
        decState.Initialize(frameHeader);

        JxlImageBundle decoded = new(shared.CodecMetadata.ImageMetadata)
        {
            Origin = frameHeader.FrameOrigin
        };

        JxlImage3<float> tmp = new(configuration, frameDim.XSize, frameDim.YSize);
        decoded.SetFromImage(tmp, decState.OutputEncodingInfo.ColorEncoding);

        JxlPassesDecoderContext.PipelineOptions options = new()
        {
            UseSlowRenderPipeline = false,
            Coalescing = false,
            RenderSpotcolors = false,
            RenderNoise = true
        };

        decState.PreparePipeline(frameHeader, shared.CodecMetadata.ImageMetadata, decoded, options);

        decState.RenderPipeline.PrepareForThreads(1, false);
        JxlGroupDecoderCache groupDecCaches = new(configuration);

        void ProcessGroup(uint groupIndex, int thread)
        {
            if (frameHeader.LoopFilter.EpfIterations > 0)
            {
                frameHeader.LoopFilter.ComputeSigma(frameDim.BlockGroupRect(groupIndex), decState);
            }

            JxlRenderPipelineInput input = decState.RenderPipeline.GetInputBuffers(groupIndex, thread);
            JxlGroupDecoder.DecodeGroupForRoundtrip(frameHeader, coeffs, groupIndex, decState, ref groupDecCaches, thread, input, null, null);

            if ((frameHeader.Flags & (ulong)JxlFrameHeaderFlags.Noise) != 0)
            {
                JxlNoiseDecoder.PrepareNoiseInput(decState, shared.FrameDimensions, frameHeader, (int)groupIndex, thread);
            }

            input.Done();
        }

        for (int i = 0; i < frameDim.NumGroups; i++)
        {
            ProcessGroup((uint)i, i);
        }

        return decoded.Color;
    }

    public static void LossyFrameHeuristics(
        JxlFrameHeader frameHeader,
        JxlPassesEncoderState encState,
        JxlModularFrameEncoder modularFrameEncoder,
        JxlImage3<float> linear,
        JxlImage3<float> opsin,
        Rectangle rect,
        JxlCmsInterface cms,
        JxlAuxiliaryOutput? auxOut)
    {
        JxlCompressParams cparams = encState.CompressParams;
        bool streamingMode = encState.StreamingMode;
        bool initializeGlobalState = encState.InitializeGlobalState;
        JxlPassesCommonContext shared = encState.Shared;
        JxlFrameDimensions frameDim = shared.FrameDimensions;
        JxlImageFeatures imageFeatures = shared.ImageFeatures;
        JxlDequantMatrices matrices = shared.Matrices;
        JxlQuantizer quantizer = shared.Quantizer;
        JxlPlane<float> initialQuantMasking1x1 = encState.InitialQuantMasking1x1;
        JxlPlane<int> rawQuantField = shared.RawQuantField;
        JxlColorCorrelationMap cmap = shared.ColorMap;
        JxlAcStrategyImage acStrategy = shared.AcStrategy;
        JxlBlockContextMap blockCtxMap = shared.BlockContextMap;
        Configuration configuration = encState.Configuration;

        bool overrideSplines = cparams.CustomSplines.HasAny;
        if (overrideSplines)
        {
            imageFeatures.Splines.SetData(cparams.CustomSplines);
        }

        if (!streamingMode && cparams.SpeedTier <= JxlSpeedTier.Squirrel)
        {
            if (!overrideSplines)
            {
                imageFeatures.Splines = FindSplines(configuration, opsin);
            }

            imageFeatures.Splines.InitializeDrawCache(configuration, opsin.XSize, opsin.YSize, cmap.Base);
            imageFeatures.Splines.SubtractFrom(opsin);
        }

        if (!streamingMode && JxlOverrideHelpers.ApplyOverride(cparams.Patches, cparams.SpeedTier <= JxlSpeedTier.Squirrel))
        {
            JxlPatchDictionaryEncoder.FindBestPatchDictionary(configuration, opsin, encState, cms, pool, auxOut);
            JxlPatchDictionaryEncoder.SubtractFrom(imageFeatures.Patches, opsin);
        }

        float quantDc = JxlAdaptiveQuantization.InitialQuantDc(cparams.ButteraugliDistance);

        JxlAcStrategyHeuristics acsHeuristics = new(configuration, cparams, matrices);
        JxlCflHeuristics cflHeuristics = new(configuration, rect);
        JxlPlane<float> initialQuantField;
        JxlPlane<float> initialQuantMasking;

        if (cparams.SpeedTier > JxlSpeedTier.Hare || cparams.DisablePerceptualOptimizations)
        {
            initialQuantField = new(configuration, frameDim.XSizeBlocks, frameDim.YSizeBlocks);
            initialQuantMasking = new(configuration, frameDim.XSizeBlocks, frameDim.YSizeBlocks);

            // Fill the quant field with 0.79 / butteraugliDistance.
            float q = 0.79f / cparams.ButteraugliDistance;
            JxlImageOperations.FillImage(q, initialQuantField);

            // Fill initial quant masking with the inverse of q+0.001.
            float masking = 1.0f / (q + 0.001f);
            JxlImageOperations.FillImage(masking, initialQuantMasking);

            if (cparams.DisablePerceptualOptimizations)
            {
                initialQuantMasking1x1 = new(configuration, frameDim.XSize, frameDim.YSize);
                JxlImageOperations.FillImage(masking, initialQuantMasking1x1);
            }

            quantizer.ComputeGlobalScaleAndQuant(quantDc, q, 0);
        }
        else
        {
            float butteraugliDistanceForIqf = cparams.ButteraugliDistance;
            if (!frameHeader.LoopFilter.UseGaborishConvolution)
            {
                butteraugliDistanceForIqf *= 0.62f;
            }

            initialQuantField = JxlAdaptiveQuantization.InitialQuantField(configuration, butteraugliDistanceForIqf, opsin, rect, pool, 1.0f, out initialQuantMasking, ref initialQuantMasking1x1);

            float q = 0.39f / cparams.ButteraugliDistance;
            quantizer.ComputeGlobalScaleAndQuant(quantDc, q, 0);
        }

        if (frameHeader.LoopFilter.UseGaborishConvolution)
        {
            ReadOnlySpan<float> weight = [1.0f, 1.0f, 1.0f];
            JxlGaborish.InverseGaborish(opsin, rect, weight, pool);
        }

        if (initializeGlobalState)
        {
            JxlDequantMatrices.FindBestDequantMatrices(configuration, cparams, modularFrameEncoder, matrices);
        }

        acsHeuristics.Initialize(opsin, rect, initialQuantField, initialQuantMasking, initialQuantMasking1x1, matrices);

        void ProcessTile(uint tid, int thread)
        {
            // Find the tile offset.
            int nEncTiles = JxlMath.DivCeil(frameDim.XSizeBlocks, JxlChromaFromLuma.ColorTileDimensionInBlocks);
            int tx = (int)(tid % nEncTiles);
            int ty = (int)(tid / nEncTiles);

            // Find the block ranges
            int by0 = ty * JxlChromaFromLuma.ColorTileDimensionInBlocks;
            int by1 = Math.Min((ty + 1) * JxlChromaFromLuma.ColorTileDimensionInBlocks, frameDim.YSizeBlocks);
            int bx0 = tx * JxlChromaFromLuma.ColorTileDimensionInBlocks;
            int bx1 = Math.Min((tx + 1) * JxlChromaFromLuma.ColorTileDimensionInBlocks, frameDim.XSizeBlocks);

            // Create the rectangle
            Rectangle r = new(bx0, by0, bx1 - bx0, by1 - by0);

            // If the user settings allow worse performance,
            // compute the CFL tile.
            if (cparams.SpeedTier <= JxlSpeedTier.Squirrel)
            {
                cflHeuristics.ComputeTile(r, opsin, rect, matrices, null, null, null, false, cmap);
            }

            // Process the AC Strategy rectangle.
            acsHeuristics.ProcessRectangle(r, cmap, acStrategy);

            // Quantize.
            JxlAdaptiveQuantization.AdjustQuantField(acStrategy, r, cparams.ButteraugliDistance, initialQuantField);
            quantizer.SetQuantFieldRect(initialQuantField, r, rawQuantField);

            // Compute the CFL tile for the quant field.
            if (cparams.SpeedTier <= JxlSpeedTier.Hare)
            {
                cflHeuristics.ComputeTile(r, opsin, rect, matrices, acStrategy, rawQuantField, quantizer, cparams.SpeedTier >= JxlSpeedTier.Wombat, thread, cmap);
            }
        }

        int numTiles = JxlMath.DivCeil(frameDim.XSizeBlocks, JxlChromaFromLuma.ColorTileDimensionInBlocks) * JxlMath.DivCeil(frameDim.YSizeBlocks, JxlChromaFromLuma.ColorTileDimensionInBlocks);

        for (int i = 0; i < numTiles; i++)
        {
            ProcessTile((uint)i, i);
        }

        acsHeuristics.Finalize(acStrategy, auxOut);

        if (!streamingMode && !cparams.DisablePerceptualOptimizations)
        {
            JxlPlane<byte> epfSharpness = shared.EpfSharpness;
            JxlImageOperations.FillPlane((byte)4, epfSharpness, epfSharpness.GetRectangle());

            JxlAdaptiveQuantization.FindBestQuantizer(frameHeader, linear, opsin, initialQuantField, encState, cms, pool, auxOut);
        }

        if (cparams.SpeedTier < JxlSpeedTier.Falcon && initializeGlobalState)
        {
            FindBestBlockEntropyModel(cparams, rawQuantField, acStrategy, blockCtxMap);
        }
    }

    private sealed class OccCounters : IDisposable
    {
        // These are pooled using Array<T>.Shared.Rent.
        private readonly int[] qfCounts;
        private readonly int[] dataForQfOrdCounts;
        private readonly int[] ordCounts;

        public OccCounters(JxlPlane<int> rqf, JxlAcStrategyImage acStrategy)
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
