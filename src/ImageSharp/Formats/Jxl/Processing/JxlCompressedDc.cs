// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Jxl.IO.FrameHeader;
using SixLabors.ImageSharp.Formats.Jxl.Memory.ImageTypes;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Modular;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing;

internal static class JxlCompressedDc
{
    private const float W1 = 0.20345139757231578f;
    private const float W2 = 0.0334829185968739f;
    private const float W0 = 1.0f - (4.0f * (W1 + W2));

    private static unsafe void ComputePixelChannelPacked(float dcFactor, ReadOnlySpan<float> rowTop, ReadOnlySpan<float> row, ReadOnlySpan<float> rowBottom, out Vector<float> mc, out Vector<float> sm, ref Vector<float> gap, int x)
    {
        // Use ptr for aligned vector loads
        fixed (float* pRowTop = rowTop)
        {
            fixed (float* pRow = row)
            {
                fixed (float* pRowBottom = rowBottom)
                {
                    // libjxl seems to use the unaligned and aligned
                    // vector loads in a specific pattern here.
                    // Additionally, use nontemporal writes as we only
                    // iterate through parts of the image once.
                    Vector<float> tl = Vector.Load(pRowTop + x - 1);
                    Vector<float> tc = Vector.LoadAlignedNonTemporal(pRowTop + x);
                    Vector<float> tr = Vector.Load(pRowTop + x + 1);

                    Vector<float> ml = Vector.Load(pRow + x - 1);
                    mc = Vector.LoadAlignedNonTemporal(pRow + x);
                    Vector<float> mr = Vector.Load(pRow + x + 1);

                    Vector<float> bl = Vector.Load(pRowBottom + x - 1);
                    Vector<float> bc = Vector.LoadAligned(pRowBottom + x);
                    Vector<float> br = Vector.Load(pRowBottom + x + 1);

                    Vector<float> wCenter = Vector.Create(W0);
                    Vector<float> wSide = Vector.Create(W1);
                    Vector<float> wCorner = Vector.Create(W2);

                    Vector<float> corner = (tl + tr) + (bl + br);
                    Vector<float> side = (ml + mr) + (tc + bc);
                    sm = (corner * wCorner) + ((side * wSide) + (mc * wCenter));

                    Vector<float> dcQuant = Vector.Create(dcFactor);
                    gap = Vector.Max(gap, Vector.Abs((mc - sm) / dcQuant));
                }
            }
        }
    }

    private static void ComputePixelChannel(float dcFactor, ReadOnlySpan<float> rowTop, ReadOnlySpan<float> row, ReadOnlySpan<float> rowBottom, out float mc, out float sm, ref float gap, int x)
    {
        float tl = rowTop[x - 1];
        float tc = rowTop[x];
        float tr = rowTop[x + 1];

        float ml = row[x - 1];
        mc = row[x];
        float mr = row[x + 1];

        float bl = rowBottom[x - 1];
        float bc = rowBottom[x];
        float br = rowBottom[x + 1];

        float corner = (tl + tr) + (bl + br);
        float side = (ml + mr) + (tc + bc);

        sm = (corner * W2) + ((side * W1) + (mc * W0));

        gap = MathF.Max(gap, MathF.Abs((mc - sm) / dcFactor));
    }

    private static void ComputePixel(
        InlineArray3<float> dcFactors,
        ReadOnlySpan<float> rowsTop0,
        ReadOnlySpan<float> rowsTop1,
        ReadOnlySpan<float> rowsTop2,
        ReadOnlySpan<float> rows0,
        ReadOnlySpan<float> rows1,
        ReadOnlySpan<float> rows2,
        ReadOnlySpan<float> rowsBottom0,
        ReadOnlySpan<float> rowsBottom1,
        ReadOnlySpan<float> rowsBottom2,
        Span<float> outRows0,
        Span<float> outRows1,
        Span<float> outRows2,
        int x)
    {
        Vector<float> gap = new(0.5f);

        ComputePixelChannelPacked(dcFactors[0], rowsTop0, rows0, rowsBottom0, out Vector<float> mcX, out Vector<float> smX, ref gap, x);
        ComputePixelChannelPacked(dcFactors[1], rowsTop1, rows1, rowsBottom1, out Vector<float> mcY, out Vector<float> smY, ref gap, x);
        ComputePixelChannelPacked(dcFactors[2], rowsTop2, rows2, rowsBottom2, out Vector<float> mcB, out Vector<float> smB, ref gap, x);

        Vector<float> factor = Vector.Create(3.0f) - (4.0f * gap);
        factor = Vector.Max(factor, Vector<float>.Zero);

        Vector<float> output = ((smX - mcX) * factor) + mcX;
        output.CopyTo(outRows0[x..]);

        output = ((smY - mcY) * factor) + mcY;
        output.CopyTo(outRows1[x..]);

        output = ((smB - mcB) * factor) + mcB;
        output.CopyTo(outRows2[x..]);
    }

    private static void ComputePixelScalar(
        InlineArray3<float> dcFactors,
        ReadOnlySpan<float> rowsTop0,
        ReadOnlySpan<float> rowsTop1,
        ReadOnlySpan<float> rowsTop2,
        ReadOnlySpan<float> rows0,
        ReadOnlySpan<float> rows1,
        ReadOnlySpan<float> rows2,
        ReadOnlySpan<float> rowsBottom0,
        ReadOnlySpan<float> rowsBottom1,
        ReadOnlySpan<float> rowsBottom2,
        Span<float> outRows0,
        Span<float> outRows1,
        Span<float> outRows2,
        int x)
    {
        float gap = 0.5f;

        ComputePixelChannel(dcFactors[0], rowsTop0, rows0, rowsBottom0, out float mcX, out float smX, ref gap, x);
        ComputePixelChannel(dcFactors[1], rowsTop1, rows1, rowsBottom1, out float mcY, out float smY, ref gap, x);
        ComputePixelChannel(dcFactors[2], rowsTop2, rows2, rowsBottom2, out float mcB, out float smB, ref gap, x);

        float factor = MathF.Max(3.0f - (4.0f * gap), 0.0f);

        outRows0[x] = ((smX - mcX) * factor) + mcX;
        outRows1[x] = ((smY - mcY) * factor) + mcY;
        outRows2[x] = ((smB - mcB) * factor) + mcB;
    }

    public static bool AdaptiveDCSmoothing(Configuration configuration, InlineArray3<float> dcFactors, JxlImage3F dc)
    {
        int xSize = dc.XSize;
        int ySize = dc.YSize;

        if (ySize <= 2 || xSize <= 2)
        {
            return true;
        }

        // Don't dispose.
        JxlImage3F smoothed = new(configuration, xSize, ySize);

        // Fill borders that the loop below will not.
        // First and last rows are unused by the processing loop.
        for (int c = 0; c < 3; c++)
        {
            dc.PlaneRow(c, 0).CopyTo(smoothed.PlaneRow(c, 0));
            dc.PlaneRow(c, ySize - 1).CopyTo(smoothed.PlaneRow(c, ySize - 1));
        }

        _ = Parallel.For(1, ySize, configuration.GetParallelOptions(), y =>
        {
            ReadOnlySpan<float> rowTopX = dc.PlaneRow(0, y - 1);
            ReadOnlySpan<float> rowTopY = dc.PlaneRow(1, y - 1);
            ReadOnlySpan<float> rowTopB = dc.PlaneRow(2, y - 1);

            ReadOnlySpan<float> rowX = dc.PlaneRow(0, y);
            ReadOnlySpan<float> rowY = dc.PlaneRow(1, y);
            ReadOnlySpan<float> rowB = dc.PlaneRow(2, y);

            ReadOnlySpan<float> rowBottomX = dc.PlaneRow(0, y + 1);
            ReadOnlySpan<float> rowBottomY = dc.PlaneRow(1, y + 1);
            ReadOnlySpan<float> rowBottomB = dc.PlaneRow(2, y + 1);

            Span<float> rowOutX = smoothed.PlaneRow(0, y);
            Span<float> rowOutY = smoothed.PlaneRow(1, y);
            Span<float> rowOutB = smoothed.PlaneRow(2, y);

            // Preserve the left and right borders.
            rowOutX[0] = rowX[0];
            rowOutY[0] = rowY[0];
            rowOutB[0] = rowB[0];

            rowOutX[xSize - 1] = rowX[xSize - 1];
            rowOutY[xSize - 1] = rowY[xSize - 1];
            rowOutB[xSize - 1] = rowB[xSize - 1];

            int x = 1;

            // Scalar pixels before the first complete SIMD vector.
            int vectorLength = Vector<float>.Count;
            for (; x < Math.Min(vectorLength, xSize - 1); x++)
            {
                ComputePixelScalar(dcFactors, rowTopX, rowTopY, rowTopB, rowX, rowY, rowB, rowBottomX, rowBottomY, rowBottomB, rowOutX, rowOutY, rowOutB, x);
            }

            // Full SIMD vectors.
            for (; x + vectorLength <= xSize - 1; x += vectorLength)
            {
                ComputePixel(dcFactors, rowTopX, rowTopY, rowTopB, rowX, rowY, rowB, rowBottomX, rowBottomY, rowBottomB, rowOutX, rowOutY, rowOutB, x);
            }

            // Remaining scalar pixels.
            for (; x < xSize - 1; x++)
            {
                ComputePixelScalar(dcFactors, rowTopX, rowTopY, rowTopB, rowX, rowY, rowB, rowBottomX, rowBottomY, rowBottomB, rowOutX, rowOutY, rowOutB, x);
            }
        });

        dc.Swap(smoothed);
        return true;
    }

    public static void DequantDC(Rectangle r, JxlImage3F dc, JxlImageB quantDc, JxlModularImage input, InlineArray3<float> dcFactors, float mul, InlineArray3<float> cflFactors, JxlYCbCrChromaSubsampling chromaSubsampling, JxlBlockContextMap bctx)
    {
        if (chromaSubsampling.Is444)
        {
            Vector<float> facX = new(dcFactors[0] * mul);
            Vector<float> facY = new(dcFactors[1] * mul);
            Vector<float> facB = new(dcFactors[2] * mul);
            Vector<float> cflFacX = new(cflFactors[0]);
            Vector<float> cflFacB = new(cflFactors[2]);

            for (int y = 0; y < r.Height; y++)
            {
                Span<float> decRowX = dc.Plane(0).GetRow(r, y);
                Span<float> decRowY = dc.Plane(1).GetRow(r, y);
                Span<float> decRowB = dc.Plane(2).GetRow(r, y);

                ReadOnlySpan<int> quantRowX = input.Channels[1].Plane.GetRow(y);
                ReadOnlySpan<int> quantRowY = input.Channels[0].Plane.GetRow(y);
                ReadOnlySpan<int> quantRowB = input.Channels[2].Plane.GetRow(y);

                int x = 0;
                int vectorCount = Vector<float>.Count;

                for (; x + vectorCount <= r.Width; x += vectorCount)
                {
                    Vector<float> inX = Vector.ConvertToSingle(Vector.Create(quantRowX[x..])) * facX;
                    Vector<float> inY = Vector.ConvertToSingle(Vector.Create(quantRowY[x..])) * facY;
                    Vector<float> inB = Vector.ConvertToSingle(Vector.Create(quantRowB[x..])) * facB;

                    inY.CopyTo(decRowY[x..]);
                    ((inY * cflFacX) + inX).CopyTo(decRowX[x..]);
                    ((inY * cflFacB) + inB).CopyTo(decRowB[x..]);
                }

                for (; x < r.Width; x++)
                {
                    float inX = quantRowX[x] * dcFactors[0] * mul;
                    float inY = quantRowY[x] * dcFactors[1] * mul;
                    float inB = quantRowB[x] * dcFactors[2] * mul;

                    decRowY[x] = inY;
                    decRowX[x] = (inY * cflFactors[0]) + inX;
                    decRowB[x] = (inY * cflFactors[2]) + inB;
                }
            }
        }
        else
        {
            foreach (int c in (ReadOnlySpan<int>)[1, 0, 2])
            {
                Rectangle rect = new(
                    r.X0() >> chromaSubsampling.HShift(c),
                    r.Y0() >> chromaSubsampling.VShift(c),
                    r.Width >> chromaSubsampling.HShift(c),
                    r.Height >> chromaSubsampling.VShift(c));

                Vector<float> fac = new(dcFactors[c] * mul);
                JxlModularChannel channel = input.Channels[c < 2 ? c ^ 1 : c];

                for (int y = 0; y < rect.Height; y++)
                {
                    ReadOnlySpan<int> quantRow = channel.Plane.GetRow(y);
                    Span<float> row = dc.Plane(c).GetRow(rect, y);

                    int x = 0;
                    int vectorCount = Vector<float>.Count;

                    for (; x + vectorCount <= rect.Width; x += vectorCount)
                    {
                        Vector<float> output = Vector.ConvertToSingle(Vector.Create(quantRow[x..])) * fac;
                        output.CopyTo(row[x..]);
                    }

                    for (; x < rect.Width; x++)
                    {
                        row[x] = quantRow[x] * dcFactors[c] * mul;
                    }
                }
            }
        }

        if (bctx.DcContextCount <= 1)
        {
            for (int y = 0; y < r.Height; y++)
            {
                Span<byte> qdcRow = quantDc.GetRow(r, y);
                qdcRow.Clear();
            }
        }
        else
        {
            for (int y = 0; y < r.Height; y++)
            {
                Span<byte> qdcRow = quantDc.GetRow(r, y);

                ReadOnlySpan<int> quantRowX = input.Channels[1].Plane.GetRow(y >> chromaSubsampling.VShift(0));
                ReadOnlySpan<int> quantRowY = input.Channels[0].Plane.GetRow(y >> chromaSubsampling.VShift(1));
                ReadOnlySpan<int> quantRowB = input.Channels[2].Plane.GetRow(y >> chromaSubsampling.VShift(2));

                for (int x = 0; x < r.Width; x++)
                {
                    int bucketX = CountThresholds(
                        quantRowX[x >> chromaSubsampling.HShift(0)],
                        CollectionsMarshal.AsSpan(bctx.DcThresholds[0]));

                    int bucketY = CountThresholds(
                        quantRowY[x >> chromaSubsampling.HShift(1)],
                        CollectionsMarshal.AsSpan(bctx.DcThresholds[1]));

                    int bucketB = CountThresholds(
                        quantRowB[x >> chromaSubsampling.HShift(2)],
                        CollectionsMarshal.AsSpan(bctx.DcThresholds[2]));

                    int bucket = bucketX;

                    bucket *= bctx.DcThresholds[2].Count + 1;
                    bucket += bucketB;
                    bucket *= bctx.DcThresholds[1].Count + 1;
                    bucket += bucketY;

                    qdcRow[x] = (byte)bucket;
                }
            }
        }
    }

    private static int CountThresholds(int value, ReadOnlySpan<int> thresholds)
    {
        int bucket = 0;

        foreach (int threshold in thresholds)
        {
            if (value > threshold)
            {
                bucket++;
            }
        }

        return bucket;
    }
}
