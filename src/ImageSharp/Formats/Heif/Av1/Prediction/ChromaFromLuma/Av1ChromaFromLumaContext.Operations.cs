// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.ChromaFromLuma;

/// <content>
/// Provides packed luma subsampling and mean-subtraction operations for chroma-from-luma prediction. Consecutive lanes
/// represent chroma coordinates. Horizontal and vertical luma sums are converted directly to Q3, after which a vector
/// reduction derives the common rounded mean and lane-wise subtraction leaves the zero-mean AC predictor surface.
/// </content>
internal partial class Av1ChromaFromLumaContext
{
    /// <summary>
    /// Subsamples one reconstructed eight-bit luma block and removes its rounded Q3 mean.
    /// </summary>
    /// <param name="input">The reconstructed luma samples.</param>
    /// <param name="inputStride">The distance, in samples, between input rows.</param>
    /// <param name="output">The fixed-stride Q3 predictor workspace.</param>
    /// <param name="transformSize">The chroma transform dimensions.</param>
    /// <param name="subsamplingX">Whether two horizontal luma samples map to each chroma sample.</param>
    /// <param name="subsamplingY">Whether two vertical luma samples map to each chroma sample.</param>
    public static void PrepareBlock(
        ReadOnlySpan<byte> input,
        int inputStride,
        Span<short> output,
        Av1TransformSize transformSize,
        bool subsamplingX,
        bool subsamplingY)
    {
        int lumaWidth = transformSize.GetWidth() << (subsamplingX ? 1 : 0);
        int lumaHeight = transformSize.GetHeight() << (subsamplingY ? 1 : 0);
        StoreSamples(input, inputStride, 0, lumaWidth, lumaHeight, output, subsamplingX, subsamplingY);
        SubtractAverage(output, transformSize);
    }

    /// <summary>
    /// Subsamples one reconstructed high-bit-depth luma block and removes its rounded Q3 mean.
    /// </summary>
    /// <param name="input">The reconstructed luma samples.</param>
    /// <param name="inputStride">The distance, in samples, between input rows.</param>
    /// <param name="output">The fixed-stride Q3 predictor workspace.</param>
    /// <param name="transformSize">The chroma transform dimensions.</param>
    /// <param name="subsamplingX">Whether two horizontal luma samples map to each chroma sample.</param>
    /// <param name="subsamplingY">Whether two vertical luma samples map to each chroma sample.</param>
    public static void PrepareBlock(
        ReadOnlySpan<short> input,
        int inputStride,
        Span<short> output,
        Av1TransformSize transformSize,
        bool subsamplingX,
        bool subsamplingY)
    {
        int lumaWidth = transformSize.GetWidth() << (subsamplingX ? 1 : 0);
        int lumaHeight = transformSize.GetHeight() << (subsamplingY ? 1 : 0);
        StoreSamples(input, inputStride, 0, lumaWidth, lumaHeight, output, subsamplingX, subsamplingY);
        SubtractAverage(output, transformSize);
    }

    /// <summary>
    /// Stores 8-bit reconstructed luma samples in the Q3 predictor surface.
    /// </summary>
    /// <param name="input">The reconstructed luma samples.</param>
    /// <param name="inputStride">The distance, in samples, between input rows.</param>
    /// <param name="outputOffset">The first destination sample in the fixed-stride predictor buffer.</param>
    /// <param name="width">The luma width in samples.</param>
    /// <param name="height">The luma height in samples.</param>
    /// <param name="output">The fixed-stride Q3 predictor workspace.</param>
    /// <param name="subsamplingX">Whether horizontal luma pairs are subsampled.</param>
    /// <param name="subsamplingY">Whether vertical luma pairs are subsampled.</param>
    private static void StoreSamples(
        ReadOnlySpan<byte> input,
        int inputStride,
        int outputOffset,
        int width,
        int height,
        Span<short> output,
        bool subsamplingX,
        bool subsamplingY)
    {
        ref byte inputBase = ref MemoryMarshal.GetReference(input);
        ref short outputBase = ref MemoryMarshal.GetReference(output);

        if (!subsamplingX)
        {
            // One luma sample maps directly to one chroma sample, so multiplying by eight converts it to Q3.
            for (int row = 0; row < height; row++)
            {
                ref byte inputRow = ref Unsafe.Add(ref inputBase, row * inputStride);
                ref short outputRow = ref Unsafe.Add(ref outputBase, outputOffset + (row * BufferLine));
                int column = 0;

                if (Vector256.IsHardwareAccelerated)
                {
                    nuint vectorCount = Numerics.Vector256Count<byte>(width - column);
                    for (; vectorCount > 0; vectorCount--, column += Vector256<byte>.Count)
                    {
                        (Vector256<ushort> lower, Vector256<ushort> upper) = Vector256.Widen(Vector256.LoadUnsafe(ref inputRow, (nuint)column));

                        (lower << 3).AsInt16().StoreUnsafe(ref outputRow, (nuint)column);
                        (upper << 3).AsInt16().StoreUnsafe(ref outputRow, (nuint)(column + Vector256<ushort>.Count));
                    }
                }

                if (Vector128.IsHardwareAccelerated)
                {
                    nuint vectorCount = Numerics.Vector128Count<byte>(width - column);
                    for (; vectorCount > 0; vectorCount--, column += Vector128<byte>.Count)
                    {
                        (Vector128<ushort> lower, Vector128<ushort> upper) = Vector128.Widen(Vector128.LoadUnsafe(ref inputRow, (nuint)column));

                        (lower << 3).AsInt16().StoreUnsafe(ref outputRow, (nuint)column);
                        (upper << 3).AsInt16().StoreUnsafe(ref outputRow, (nuint)(column + Vector128<ushort>.Count));
                    }

                    if (column < width)
                    {
                        ulong packed = width - column == 4
                            ? Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref inputRow, column))
                            : Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref inputRow, column));
                        Vector128<short> q3 = (Vector128.WidenLower(Vector128.CreateScalarUnsafe(packed).AsByte()) << 3).AsInt16();

                        if (width - column == 4)
                        {
                            Unsafe.WriteUnaligned(ref Unsafe.As<short, byte>(ref Unsafe.Add(ref outputRow, column)), q3.AsUInt64().ToScalar());
                        }
                        else
                        {
                            q3.StoreUnsafe(ref outputRow, (nuint)column);
                        }

                        column = width;
                    }
                }

                for (; column < width; column++)
                {
                    Unsafe.Add(ref outputRow, column) = (short)(Unsafe.Add(ref inputRow, column) << 3);
                }
            }

            return;
        }

        Vector256<sbyte> ones256 = Vector256.Create((sbyte)1);
        Vector128<sbyte> ones128 = Vector128.Create((sbyte)1);
        int rowStep = subsamplingY ? 2 : 1;
        int outputShift = subsamplingY ? 1 : 2;
        for (int row = 0; row < height; row += rowStep)
        {
            ref byte inputRow = ref Unsafe.Add(ref inputBase, row * inputStride);
            ref byte nextInputRow = ref Unsafe.Add(ref inputRow, subsamplingY ? inputStride : 0);
            ref short outputRow = ref Unsafe.Add(ref outputBase, outputOffset + ((row >> (subsamplingY ? 1 : 0)) * BufferLine));
            int column = 0;

            if (Avx2.IsSupported)
            {
                nuint vectorCount = Numerics.Vector256Count<byte>(width - column);
                for (; vectorCount > 0; vectorCount--, column += Vector256<byte>.Count)
                {
                    Vector256<short> sum = Avx2.MultiplyAddAdjacent(Vector256.LoadUnsafe(ref inputRow, (nuint)column), ones256);
                    if (subsamplingY)
                    {
                        sum += Avx2.MultiplyAddAdjacent(Vector256.LoadUnsafe(ref nextInputRow, (nuint)column), ones256);
                    }

                    (sum << outputShift).StoreUnsafe(ref outputRow, (nuint)(column >> 1));
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                nuint vectorCount = Numerics.Vector128Count<byte>(width - column);
                for (; vectorCount > 0; vectorCount--, column += Vector128<byte>.Count)
                {
                    Vector128<short> sum = PairSum(Vector128.LoadUnsafe(ref inputRow, (nuint)column), ones128);
                    if (subsamplingY)
                    {
                        sum += PairSum(Vector128.LoadUnsafe(ref nextInputRow, (nuint)column), ones128);
                    }

                    (sum << outputShift).StoreUnsafe(ref outputRow, (nuint)(column >> 1));
                }

                if (column < width)
                {
                    int remaining = width - column;
                    ulong packed = remaining == 4
                        ? Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref inputRow, column))
                        : Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref inputRow, column));
                    Vector128<short> sum = PairSum(Vector128.CreateScalarUnsafe(packed).AsByte(), ones128);
                    if (subsamplingY)
                    {
                        packed = remaining == 4
                            ? Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref nextInputRow, column))
                            : Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref nextInputRow, column));
                        sum += PairSum(Vector128.CreateScalarUnsafe(packed).AsByte(), ones128);
                    }

                    Vector128<short> q3 = sum << outputShift;
                    if (remaining == 4)
                    {
                        Unsafe.WriteUnaligned(ref Unsafe.As<short, byte>(ref Unsafe.Add(ref outputRow, column >> 1)), q3.AsUInt32().ToScalar());
                    }
                    else
                    {
                        Unsafe.WriteUnaligned(ref Unsafe.As<short, byte>(ref Unsafe.Add(ref outputRow, column >> 1)), q3.AsUInt64().ToScalar());
                    }

                    column = width;
                }
            }

            for (; column < width; column += 2)
            {
                int sum = Unsafe.Add(ref inputRow, column) + Unsafe.Add(ref inputRow, column + 1);
                if (subsamplingY)
                {
                    sum += Unsafe.Add(ref nextInputRow, column) + Unsafe.Add(ref nextInputRow, column + 1);
                }

                Unsafe.Add(ref outputRow, column >> 1) = (short)(sum << outputShift);
            }
        }
    }

    /// <summary>
    /// Stores high-bit-depth reconstructed luma samples in the Q3 predictor surface.
    /// </summary>
    /// <param name="input">The reconstructed luma samples.</param>
    /// <param name="inputStride">The distance, in samples, between input rows.</param>
    /// <param name="outputOffset">The first destination sample in the fixed-stride predictor buffer.</param>
    /// <param name="width">The luma width in samples.</param>
    /// <param name="height">The luma height in samples.</param>
    /// <param name="output">The fixed-stride Q3 predictor workspace.</param>
    /// <param name="subsamplingX">Whether horizontal luma pairs are subsampled.</param>
    /// <param name="subsamplingY">Whether vertical luma pairs are subsampled.</param>
    private static void StoreSamples(
        ReadOnlySpan<short> input,
        int inputStride,
        int outputOffset,
        int width,
        int height,
        Span<short> output,
        bool subsamplingX,
        bool subsamplingY)
    {
        ref short inputBase = ref MemoryMarshal.GetReference(input);
        ref short outputBase = ref MemoryMarshal.GetReference(output);

        if (!subsamplingX)
        {
            for (int row = 0; row < height; row++)
            {
                ref short inputRow = ref Unsafe.Add(ref inputBase, row * inputStride);
                ref short outputRow = ref Unsafe.Add(ref outputBase, outputOffset + (row * BufferLine));
                int column = 0;

                if (Vector256.IsHardwareAccelerated)
                {
                    nuint vectorCount = Numerics.Vector256Count<short>(width - column);
                    for (; vectorCount > 0; vectorCount--, column += Vector256<short>.Count)
                    {
                        (Vector256.LoadUnsafe(ref inputRow, (nuint)column) << 3).StoreUnsafe(ref outputRow, (nuint)column);
                    }
                }

                if (Vector128.IsHardwareAccelerated)
                {
                    nuint vectorCount = Numerics.Vector128Count<short>(width - column);
                    for (; vectorCount > 0; vectorCount--, column += Vector128<short>.Count)
                    {
                        (Vector128.LoadUnsafe(ref inputRow, (nuint)column) << 3).StoreUnsafe(ref outputRow, (nuint)column);
                    }

                    if (column < width)
                    {
                        ulong packed = Unsafe.ReadUnaligned<ulong>(ref Unsafe.As<short, byte>(ref Unsafe.Add(ref inputRow, column)));
                        Vector128<short> q3 = Vector128.CreateScalarUnsafe(packed).AsInt16() << 3;
                        Unsafe.WriteUnaligned(ref Unsafe.As<short, byte>(ref Unsafe.Add(ref outputRow, column)), q3.AsUInt64().ToScalar());
                        column += 4;
                    }
                }

                for (; column < width; column++)
                {
                    Unsafe.Add(ref outputRow, column) = (short)(Unsafe.Add(ref inputRow, column) << 3);
                }
            }

            return;
        }

        Vector256<short> ones256 = Vector256.Create((short)1);
        Vector128<short> ones128 = Vector128.Create((short)1);
        int rowStep = subsamplingY ? 2 : 1;
        int outputShift = subsamplingY ? 1 : 2;
        for (int row = 0; row < height; row += rowStep)
        {
            ref short inputRow = ref Unsafe.Add(ref inputBase, row * inputStride);
            ref short nextInputRow = ref Unsafe.Add(ref inputRow, subsamplingY ? inputStride : 0);
            ref short outputRow = ref Unsafe.Add(ref outputBase, outputOffset + ((row >> (subsamplingY ? 1 : 0)) * BufferLine));
            int column = 0;

            if (Vector256.IsHardwareAccelerated)
            {
                nuint vectorCount = Numerics.Vector256Count<short>(width - column);
                for (; vectorCount > 0; vectorCount--, column += Vector256<short>.Count)
                {
                    Vector256<int> sum = Vector256_.MultiplyAddAdjacent(Vector256.LoadUnsafe(ref inputRow, (nuint)column), ones256);
                    if (subsamplingY)
                    {
                        sum += Vector256_.MultiplyAddAdjacent(Vector256.LoadUnsafe(ref nextInputRow, (nuint)column), ones256);
                    }

                    Vector256.Narrow(sum << outputShift, Vector256<int>.Zero).GetLower().StoreUnsafe(ref outputRow, (nuint)(column >> 1));
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                nuint vectorCount = Numerics.Vector128Count<short>(width - column);
                for (; vectorCount > 0; vectorCount--, column += Vector128<short>.Count)
                {
                    Vector128<int> sum = Vector128_.MultiplyAddAdjacent(Vector128.LoadUnsafe(ref inputRow, (nuint)column), ones128);
                    if (subsamplingY)
                    {
                        sum += Vector128_.MultiplyAddAdjacent(Vector128.LoadUnsafe(ref nextInputRow, (nuint)column), ones128);
                    }

                    Vector128.Narrow(sum << outputShift, Vector128<int>.Zero).GetLower().StoreUnsafe(ref outputRow, (nuint)(column >> 1));
                }

                if (column < width)
                {
                    Vector128<short> samples = Vector128.CreateScalarUnsafe(
                        Unsafe.ReadUnaligned<ulong>(ref Unsafe.As<short, byte>(ref Unsafe.Add(ref inputRow, column)))).AsInt16();
                    Vector128<int> sum = Vector128_.MultiplyAddAdjacent(samples, ones128);
                    if (subsamplingY)
                    {
                        samples = Vector128.CreateScalarUnsafe(
                            Unsafe.ReadUnaligned<ulong>(ref Unsafe.As<short, byte>(ref Unsafe.Add(ref nextInputRow, column)))).AsInt16();
                        sum += Vector128_.MultiplyAddAdjacent(samples, ones128);
                    }

                    Vector64<short> q3 = Vector128.Narrow(sum << outputShift, Vector128<int>.Zero).GetLower();
                    Unsafe.WriteUnaligned(ref Unsafe.As<short, byte>(ref Unsafe.Add(ref outputRow, column >> 1)), q3.AsUInt32().ToScalar());
                    column += 4;
                }
            }

            for (; column < width; column += 2)
            {
                int sum = Unsafe.Add(ref inputRow, column) + Unsafe.Add(ref inputRow, column + 1);
                if (subsamplingY)
                {
                    sum += Unsafe.Add(ref nextInputRow, column) + Unsafe.Add(ref nextInputRow, column + 1);
                }

                Unsafe.Add(ref outputRow, column >> 1) = (short)(sum << outputShift);
            }
        }
    }

    /// <summary>
    /// Subtracts the rounded Q3 average from each predictor sample, leaving the AC contribution used by CfL.
    /// </summary>
    /// <param name="buffer">The fixed-stride Q3 predictor workspace.</param>
    /// <param name="transformSize">The populated predictor dimensions.</param>
    private static void SubtractAverage(Span<short> buffer, Av1TransformSize transformSize)
    {
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();

        // Transform dimensions are powers of two, so division by the sample count is an exact right shift. Half
        // the sample count is accumulated first to implement the normative nearest-integer rounding.
        int sumQ3 = (width * height) >> 1;
        ref short bufferBase = ref MemoryMarshal.GetReference(buffer);

        if (Vector256.IsHardwareAccelerated && width >= Vector256<short>.Count)
        {
            for (int row = 0; row < height; row++)
            {
                int rowOffset = row * BufferLine;
                for (int column = 0; column < width; column += Vector256<short>.Count)
                {
                    (Vector256<int> lower, Vector256<int> upper) = Vector256.Widen(Vector256.LoadUnsafe(ref bufferBase, (nuint)(rowOffset + column)));

                    sumQ3 += Vector256.Sum(lower) + Vector256.Sum(upper);
                }
            }
        }
        else if (Vector128.IsHardwareAccelerated)
        {
            for (int row = 0; row < height; row++)
            {
                int rowOffset = row * BufferLine;
                int column = 0;
                nuint vectorCount = Numerics.Vector128Count<short>(width - column);
                for (; vectorCount > 0; vectorCount--, column += Vector128<short>.Count)
                {
                    (Vector128<int> lower, Vector128<int> upper) = Vector128.Widen(Vector128.LoadUnsafe(ref bufferBase, (nuint)(rowOffset + column)));

                    sumQ3 += Vector128.Sum(lower) + Vector128.Sum(upper);
                }

                if (column < width)
                {
                    ulong packed = Unsafe.ReadUnaligned<ulong>(ref Unsafe.As<short, byte>(ref Unsafe.Add(ref bufferBase, rowOffset + column)));
                    sumQ3 += Vector128.Sum(Vector128.WidenLower(Vector128.CreateScalarUnsafe(packed).AsInt16()));
                }
            }
        }
        else
        {
            for (int row = 0; row < height; row++)
            {
                int rowOffset = row * BufferLine;
                for (int column = 0; column < width; column++)
                {
                    sumQ3 += Unsafe.Add(ref bufferBase, rowOffset + column);
                }
            }
        }

        int pelCountLog2 = transformSize.GetBlockWidthLog2() + transformSize.GetBlockHeightLog2();
        short averageQ3 = (short)(sumQ3 >> pelCountLog2);

        if (Vector256.IsHardwareAccelerated && width >= Vector256<short>.Count)
        {
            Vector256<short> average = Vector256.Create(averageQ3);
            for (int row = 0; row < height; row++)
            {
                int rowOffset = row * BufferLine;
                for (int column = 0; column < width; column += Vector256<short>.Count)
                {
                    (Vector256.LoadUnsafe(ref bufferBase, (nuint)(rowOffset + column)) - average).StoreUnsafe(ref bufferBase, (nuint)(rowOffset + column));
                }
            }

            return;
        }

        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<short> average = Vector128.Create(averageQ3);
            for (int row = 0; row < height; row++)
            {
                int rowOffset = row * BufferLine;
                int column = 0;
                nuint vectorCount = Numerics.Vector128Count<short>(width - column);
                for (; vectorCount > 0; vectorCount--, column += Vector128<short>.Count)
                {
                    (Vector128.LoadUnsafe(ref bufferBase, (nuint)(rowOffset + column)) - average).StoreUnsafe(ref bufferBase, (nuint)(rowOffset + column));
                }

                if (column < width)
                {
                    ulong packed = Unsafe.ReadUnaligned<ulong>(ref Unsafe.As<short, byte>(ref Unsafe.Add(ref bufferBase, rowOffset + column)));
                    Vector128<short> result = Vector128.CreateScalarUnsafe(packed).AsInt16() - average;
                    Unsafe.WriteUnaligned(ref Unsafe.As<short, byte>(ref Unsafe.Add(ref bufferBase, rowOffset + column)), result.AsUInt64().ToScalar());
                }
            }

            return;
        }

        for (int row = 0; row < height; row++)
        {
            int rowOffset = row * BufferLine;
            for (int column = 0; column < width; column++)
            {
                Unsafe.Add(ref bufferBase, rowOffset + column) -= averageQ3;
            }
        }
    }

    /// <summary>
    /// Adds adjacent 8-bit samples into eight 16-bit lanes.
    /// </summary>
    /// <param name="samples">The packed source samples.</param>
    /// <param name="ones">The multiplier used by the x86 adjacent multiply-add instruction.</param>
    /// <returns>The adjacent pair sums.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<short> PairSum(Vector128<byte> samples, Vector128<sbyte> ones)
    {
        if (Ssse3.IsSupported)
        {
            return Ssse3.MultiplyAddAdjacent(samples, ones);
        }

        if (AdvSimd.IsSupported)
        {
            return AdvSimd.AddPairwiseWidening(samples).AsInt16();
        }

        // WebAssembly has byte shuffles but no pairwise-widening intrinsic. Grouping even and odd bytes before
        // widening keeps all eight pair additions vectorized without adding a general-purpose utility wrapper.
        Vector128<byte> even = Vector128.Shuffle(samples, Vector128.Create((byte)0, 2, 4, 6, 8, 10, 12, 14, 255, 255, 255, 255, 255, 255, 255, 255));
        Vector128<byte> odd = Vector128.Shuffle(samples, Vector128.Create((byte)1, 3, 5, 7, 9, 11, 13, 15, 255, 255, 255, 255, 255, 255, 255, 255));
        return (Vector128.WidenLower(even) + Vector128.WidenLower(odd)).AsInt16();
    }
}
