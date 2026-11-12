// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.VarDct;

/// <summary>
/// Converts between JPEG XL VarDCT samples and normalized transform coefficients.
/// </summary>
internal static partial class JxlDct
{
    /// <summary>
    /// The loaded axis and two recursive temporary batches retained by TransformColumns.
    /// Each value reserves the maximum supported float lane count, including scalar and narrower SIMD calls.
    /// </summary>
    private const int AxisScratchBatches = 3;

    /// <summary>
    /// The conservative small-transform workspace: 64 staging floats, a 64-float transpose,
    /// and three eight-element batches of sixteen float lanes. All special transforms fit within these 512 floats.
    /// </summary>
    private const int MinimumSpecialScratchLength = 512;

    /// <summary>
    /// Gets the reusable scratch length for one validated transform strategy.
    /// </summary>
    /// <param name="strategy">The serialized strategy, from zero through 26.</param>
    /// <returns>The required number of floats. The same storage can serve successive transforms.</returns>
    public static int GetScratchLength(byte strategy)
    {
        int width = JxlCoefficientOrder.StrategyWidths[strategy] * 8;
        int height = JxlCoefficientOrder.StrategyHeights[strategy] * 8;
        int area = width * height;

        // One area-sized plane holds the transpose. Three longest-axis batches reserve the widest supported
        // lanes for loaded values and recursive scratch. Narrower SIMD and scalar calls reuse the same storage.
        // LF insertion first partitions three DC-grid planes, each with area/64 values, then uses the remaining axis storage.
        // Spatial reconstruction reuses the complete workspace after those DC frequencies have been inserted.
        int axisScratchLength = AxisScratchBatches * Vector512<float>.Count * Math.Max(width, height);
        return Math.Max(area + axisScratchLength, MinimumSpecialScratchLength) + (3 * (area / 64));
    }

    /// <summary>
    /// Inserts the separate DC frequencies and reconstructs one complete transform footprint.
    /// </summary>
    /// <param name="strategy">The validated serialized strategy.</param>
    /// <param name="coefficients">The mutable dequantized coefficients. Their row stride is the longer transform dimension.</param>
    /// <param name="dc">The exact covered DC-grid rectangle. A one-block transform requires one DC sample.</param>
    /// <param name="destination">The spatial output from the transform origin. Padding remains unchanged.</param>
    /// <param name="destinationStride">The destination row stride in floats.</param>
    /// <param name="scratch">Reusable storage of at least <see cref="GetScratchLength"/> floats. It must not overlap the input or output.</param>
    public static void Transform(
        byte strategy,
        Span<float> coefficients,
        Buffer2DRegion<float> dc,
        Span<float> destination,
        int destinationStride,
        Span<float> scratch)
    {
        int width = JxlCoefficientOrder.StrategyWidths[strategy] * 8;
        int height = JxlCoefficientOrder.StrategyHeights[strategy] * 8;
        InsertLowFrequencies(coefficients, dc, width, height, scratch);
        switch (strategy)
        {
            case 1:
                InverseIdentity(coefficients, destination, destinationStride);
                break;
            case 2:
                InverseDct2(coefficients, destination, destinationStride, scratch);
                break;
            case 3:
                InverseDct4(coefficients, destination, destinationStride, scratch);
                break;
            case 12:
            case 13:
                InverseDct4By8(coefficients, destination, destinationStride, strategy == 12, scratch);
                break;
            case >= 14 and <= 17:
                InverseAfv(coefficients, destination, destinationStride, strategy - 14, scratch);
                break;
            default:
                InverseDct(coefficients, height, width, destination, destinationStride, scratch);
                break;
        }
    }

    /// <summary>
    /// Restores the low-frequency rectangle from the DC samples, including the downsampling scale on both axes.
    /// </summary>
    /// <param name="coefficients">The normalized coefficient rectangle.</param>
    /// <param name="dc">The spatial DC-grid samples.</param>
    /// <param name="width">The spatial transform width.</param>
    /// <param name="height">The spatial transform height.</param>
    /// <param name="scratch">The reusable DC planes and axis storage.</param>
    private static void InsertLowFrequencies(Span<float> coefficients, Buffer2DRegion<float> dc, int width, int height, Span<float> scratch)
    {
        if (width == 8 && height == 8)
        {
            coefficients[0] = dc.DangerousGetRowSpan(0)[0];
            return;
        }

        int dcWidth = width / 8;
        int dcHeight = height / 8;
        int area = dcWidth * dcHeight;
        Span<float> input = scratch[..area];
        Span<float> output = scratch.Slice(area, area);
        Span<float> temporary = scratch.Slice(2 * area, area);
        for (int y = 0; y < dcHeight; y++)
        {
            dc.DangerousGetRowSpan(y).CopyTo(input.Slice(y * dcWidth, dcWidth));
        }

        ForwardDct(input, dcWidth, dcHeight, dcWidth, output, temporary, scratch[(3 * area)..]);
        int shorter = Math.Min(dcWidth, dcHeight);
        int longer = Math.Max(dcWidth, dcHeight);
        ReadOnlySpan<float> verticalScale = GetResample(shorter);
        ReadOnlySpan<float> horizontalScale = GetResample(longer);
        int stride = longer * 8;

        // JPEG XL stores both orientations with the longer axis contiguous. The forward DC transform uses that same orientation.
        // Each DC coefficient represents eight source pixels on each axis, so both frequency resampling factors are required.
        for (int y = 0; y < shorter; y++)
        {
            for (int x = 0; x < longer; x++)
            {
                coefficients[(y * stride) + x] = output[(y * longer) + x] * verticalScale[y] * horizontalScale[x];
            }
        }
    }

    /// <summary>
    /// Applies the normalized forward DCT to a spatial rectangle and writes the longer-axis coefficient layout.
    /// </summary>
    /// <param name="source">The spatial input at the transform origin. Each row contains at least columns samples.</param>
    /// <param name="sourceStride">The distance between source row starts, in floats. Padding is not read.</param>
    /// <param name="rows">The spatial height.</param>
    /// <param name="columns">The spatial width.</param>
    /// <param name="destination">The contiguous normalized coefficients, with the longer frequency axis contiguous.</param>
    /// <param name="temporary">One disjoint matrix plane with rows * columns floats.</param>
    /// <param name="scratch">Three axis batches, each with max(rows * min(columns, 16), columns * min(rows, 16)) floats.</param>
    /// <remarks>
    /// Axis lengths are powers of two from one through 256. The caller supplies disjoint input, output and workspace.
    /// Each axis includes its 1/N normalization, so coefficient zero is the source rectangle's mean.
    /// Square transforms store the horizontal frequency in the row index and the vertical frequency in the column index.
    /// Each batch reserves at most sixteen independent columns. Eight-by-eight transforms need 192 scratch floats at every SIMD width.
    /// </remarks>
    public static void ForwardDct(
        ReadOnlySpan<float> source,
        int sourceStride,
        int rows,
        int columns,
        Span<float> destination,
        Span<float> temporary,
        Span<float> scratch)
    {
        if (rows < columns)
        {
            TransformAxis(source, sourceStride, temporary, columns, rows, columns, scratch, false);
            SimdUtils.Transpose(temporary, columns, destination, rows, rows, columns);
            TransformAxis(destination, rows, temporary, rows, columns, rows, scratch, false);
            SimdUtils.Transpose(temporary, rows, destination, columns, columns, rows);
        }
        else
        {
            TransformAxis(source, sourceStride, destination, columns, rows, columns, scratch, false);
            SimdUtils.Transpose(destination, columns, temporary, rows, rows, columns);
            TransformAxis(temporary, rows, destination, rows, columns, rows, scratch, false);
        }
    }

    /// <summary>
    /// Applies the separable inverse DCT and restores the spatial orientation.
    /// </summary>
    /// <param name="coefficients">The mutable normalized coefficient rectangle.</param>
    /// <param name="rows">The spatial height.</param>
    /// <param name="columns">The spatial width.</param>
    /// <param name="destination">The spatial output.</param>
    /// <param name="stride">The output row stride.</param>
    /// <param name="scratch">One matrix plane followed by axis storage.</param>
    private static void InverseDct(Span<float> coefficients, int rows, int columns, Span<float> destination, int stride, Span<float> scratch)
    {
        if (rows == 8 && columns == 8 && Vector256.IsHardwareAccelerated)
        {
            InverseDct8By8(coefficients, destination, stride);
            return;
        }

        int area = rows * columns;
        Span<float> temporary = scratch[..area];
        Span<float> axes = scratch[area..];
        if (rows < columns)
        {
            SimdUtils.Transpose(coefficients, columns, temporary, rows, rows, columns);
            TransformAxis(temporary, rows, coefficients, rows, columns, rows, axes, true);
            SimdUtils.Transpose(coefficients, rows, temporary, columns, columns, rows);
            TransformAxis(temporary, columns, destination, stride, rows, columns, axes, true);
        }
        else
        {
            TransformAxis(coefficients, rows, temporary, rows, columns, rows, axes, true);
            SimdUtils.Transpose(temporary, rows, coefficients, columns, columns, rows);
            TransformAxis(coefficients, columns, destination, stride, rows, columns, axes, true);
        }
    }

    /// <summary>
    /// Reconstructs the four independent identity blocks with their shared four-coefficient DC transform.
    /// </summary>
    /// <param name="coefficients">The complete eight-by-eight coefficient block.</param>
    /// <param name="destination">The output block.</param>
    /// <param name="stride">The output row stride.</param>
    private static void InverseIdentity(ReadOnlySpan<float> coefficients, Span<float> destination, int stride)
    {
        Vector4 dcs = GetSubBlockDcs(coefficients);
        Vector4 sum = Vector4.Zero;

        // The four lanes represent the top-left, top-right, bottom-left and bottom-right 4-by-4 blocks.
        // All four residual sums follow the same row-major addition order as the scalar definition.
        for (int y = 0; y < 4; y++)
        {
            for (int x = 0; x < 4; x++)
            {
                if (x != 0 || y != 0)
                {
                    int offset = (y * 16) + (x * 2);
                    sum += new Vector4(coefficients[offset], coefficients[offset + 1], coefficients[offset + 8], coefficients[offset + 9]);
                }
            }
        }

        Vector4 centers = dcs - (sum * (1F / 16F));
        for (int block = 0; block < 4; block++)
        {
            int blockY = block / 2;
            int blockX = block % 2;
            float center = centers[block];
            for (int y = 0; y < 4; y++)
            {
                Span<float> row = destination.Slice((((blockY * 4) + y) * stride) + (blockX * 4), 4);
                for (int x = 0; x < 4; x++)
                {
                    int offset = ((blockY + (y * 2)) * 8) + blockX + (x * 2);
                    row[x] = coefficients[offset] + center;
                }
            }

            destination[(((blockY * 4) + 1) * stride) + (blockX * 4) + 1] = center;
            destination[(blockY * 4 * stride) + (blockX * 4)] = coefficients[((blockY + 2) * 8) + blockX + 2] + center;
        }
    }

    /// <summary>
    /// Reconstructs the hierarchical two-by-two transform, then the four-by-four and eight-by-eight levels.
    /// </summary>
    /// <param name="coefficients">The mutable coefficient block.</param>
    /// <param name="destination">The output block.</param>
    /// <param name="stride">The output row stride.</param>
    /// <param name="scratch">One 64-float staging plane.</param>
    private static void InverseDct2(Span<float> coefficients, Span<float> destination, int stride, Span<float> scratch)
    {
        Span<float> temporary = scratch[..64];
        for (int size = 2; size <= 8; size *= 2)
        {
            int half = size / 2;
            for (int y = 0; y < half; y++)
            {
                if (half == Vector128<float>.Count && Vector128.IsHardwareAccelerated)
                {
                    ref float source = ref coefficients[y * 8];
                    Vector128<float> a = Vector128.LoadUnsafe(ref source);
                    Vector128<float> b = Vector128.LoadUnsafe(ref source, (nuint)half);
                    Vector128<float> c = Vector128.LoadUnsafe(ref source, (nuint)(half * 8));
                    Vector128<float> d = Vector128.LoadUnsafe(ref source, (nuint)((half * 8) + half));
                    Vector128<float> first = ((a + b) + c) + d;
                    Vector128<float> second = ((a + b) - c) - d;
                    Vector128<float> third = ((a - b) + c) - d;
                    Vector128<float> fourth = ((a - b) - c) + d;

                    // Each register contains four adjacent source positions. Interleave places their two reconstructed columns beside each other.
                    SimdUtils.Interleave2(first, second, ref temporary[2 * y * 8]);
                    SimdUtils.Interleave2(third, fourth, ref temporary[((2 * y) + 1) * 8]);
                }
                else
                {
                    for (int x = 0; x < half; x++)
                    {
                        float a = coefficients[(y * 8) + x];
                        float b = coefficients[(y * 8) + half + x];
                        float c = coefficients[((y + half) * 8) + x];
                        float d = coefficients[((y + half) * 8) + half + x];
                        temporary[(2 * y * 8) + (2 * x)] = ((a + b) + c) + d;
                        temporary[(2 * y * 8) + (2 * x) + 1] = ((a + b) - c) - d;
                        temporary[(((2 * y) + 1) * 8) + (2 * x)] = ((a - b) + c) - d;
                        temporary[(((2 * y) + 1) * 8) + (2 * x) + 1] = ((a - b) - c) + d;
                    }
                }
            }

            for (int y = 0; y < size; y++)
            {
                temporary.Slice(y * 8, size).CopyTo(coefficients.Slice(y * 8, size));
            }
        }

        for (int y = 0; y < 8; y++)
        {
            coefficients.Slice(y * 8, 8).CopyTo(destination.Slice(y * stride, 8));
        }
    }

    /// <summary>
    /// Reconstructs four interleaved four-by-four DCT blocks.
    /// </summary>
    /// <param name="coefficients">The complete coefficient block.</param>
    /// <param name="destination">The output block.</param>
    /// <param name="stride">The output row stride.</param>
    /// <param name="scratch">The subblock and inverse-transform storage.</param>
    private static void InverseDct4(ReadOnlySpan<float> coefficients, Span<float> destination, int stride, Span<float> scratch)
    {
        Vector4 dcs = GetSubBlockDcs(coefficients);
        Span<float> block = scratch[..16];
        for (int by = 0; by < 2; by++)
        {
            for (int bx = 0; bx < 2; bx++)
            {
                for (int y = 0; y < 4; y++)
                {
                    for (int x = 0; x < 4; x++)
                    {
                        block[(y * 4) + x] = coefficients[((by + (y * 2)) * 8) + bx + (x * 2)];
                    }
                }

                block[0] = dcs[(by * 2) + bx];
                InverseDct(block, 4, 4, destination[((by * 4 * stride) + (bx * 4))..], stride, scratch[16..]);
            }
        }
    }

    /// <summary>
    /// Reconstructs two four-by-eight DCT blocks in the requested spatial orientation.
    /// </summary>
    /// <param name="coefficients">The complete coefficient block.</param>
    /// <param name="destination">The output block.</param>
    /// <param name="stride">The output row stride.</param>
    /// <param name="horizontal">Whether each subblock has eight columns and four rows.</param>
    /// <param name="scratch">The subblock and inverse-transform storage.</param>
    private static void InverseDct4By8(ReadOnlySpan<float> coefficients, Span<float> destination, int stride, bool horizontal, Span<float> scratch)
    {
        float first = coefficients[0] + coefficients[8];
        float second = coefficients[0] - coefficients[8];
        Span<float> block = scratch[..32];
        for (int index = 0; index < 2; index++)
        {
            for (int y = 0; y < 4; y++)
            {
                coefficients.Slice(((2 * y) + index) * 8, 8).CopyTo(block.Slice(y * 8, 8));
            }

            block[0] = index == 0 ? first : second;
            int offset = horizontal ? index * 4 * stride : index * 4;
            InverseDct(block, horizontal ? 4 : 8, horizontal ? 8 : 4, destination[offset..], stride, scratch[32..]);
        }
    }

    /// <summary>
    /// Reconstructs the AFV quadrant and its adjacent DCT quadrants.
    /// </summary>
    /// <param name="coefficients">The complete coefficient block.</param>
    /// <param name="destination">The output block.</param>
    /// <param name="stride">The output row stride.</param>
    /// <param name="kind">The horizontal and vertical AFV reflection bits.</param>
    /// <param name="scratch">The subblocks, AFV output and inverse-transform storage.</param>
    private static void InverseAfv(ReadOnlySpan<float> coefficients, Span<float> destination, int stride, int kind, Span<float> scratch)
    {
        int quadrantX = kind & 1;
        int quadrantY = kind / 2;
        Span<float> block = scratch[..32];
        Span<float> afv = scratch.Slice(32, 16);
        for (int y = 0; y < 4; y++)
        {
            for (int x = 0; x < 4; x++)
            {
                block[(y * 4) + x] = coefficients[(y * 16) + (x * 2)];
            }
        }

        block[0] = ((coefficients[0] + coefficients[8]) + coefficients[1]) * 4F;
        InverseAfvBasis(block[..16], afv);
        for (int y = 0; y < 4; y++)
        {
            for (int x = 0; x < 4; x++)
            {
                int sourceY = quadrantY == 0 ? y : 3 - y;
                int sourceX = quadrantX == 0 ? x : 3 - x;
                destination[(((quadrantY * 4) + y) * stride) + (quadrantX * 4) + x] = afv[(sourceY * 4) + sourceX];
            }
        }

        for (int y = 0; y < 4; y++)
        {
            for (int x = 0; x < 4; x++)
            {
                block[(y * 4) + x] = coefficients[(y * 16) + (x * 2) + 1];
            }
        }

        block[0] = (coefficients[0] + coefficients[8]) - coefficients[1];
        int rightOffset = (quadrantY * 4 * stride) + ((1 - quadrantX) * 4);
        InverseDct(block[..16], 4, 4, destination[rightOffset..], stride, scratch[48..]);
        for (int y = 0; y < 4; y++)
        {
            coefficients.Slice(((2 * y) + 1) * 8, 8).CopyTo(block.Slice(y * 8, 8));
        }

        block[0] = coefficients[0] - coefficients[8];
        InverseDct(block, 4, 8, destination[((1 - quadrantY) * 4 * stride)..], stride, scratch[48..]);
    }

    /// <summary>
    /// Evaluates the normative AFV basis with independent spatial positions in SIMD lanes.
    /// </summary>
    /// <param name="coefficients">The sixteen AFV coefficients.</param>
    /// <param name="destination">The sixteen spatial positions in row-major order.</param>
    private static void InverseAfvBasis(ReadOnlySpan<float> coefficients, Span<float> destination)
    {
        if (Vector512.IsHardwareAccelerated)
        {
            AfvColumns<Vector512<float>, Vector512Operator>(coefficients, destination);
        }
        else if (Vector256.IsHardwareAccelerated)
        {
            AfvColumns<Vector256<float>, Vector256Operator>(coefficients, destination);
        }
        else if (Vector128.IsHardwareAccelerated)
        {
            AfvColumns<Vector128<float>, Vector128Operator>(coefficients, destination);
        }
        else
        {
            AfvColumns<float, ScalarOperator>(coefficients, destination);
        }
    }

    /// <summary>
    /// Evaluates complete SIMD batches of the sixteen independent AFV output positions.
    /// </summary>
    /// <typeparam name="TLanes">The scalar or SIMD storage.</typeparam>
    /// <typeparam name="TOperator">The lane arithmetic.</typeparam>
    /// <param name="coefficients">The AFV coefficients.</param>
    /// <param name="destination">The sixteen spatial positions.</param>
    private static void AfvColumns<TLanes, TOperator>(ReadOnlySpan<float> coefficients, Span<float> destination)
        where TLanes : unmanaged
        where TOperator : struct, ILaneOperator<TLanes>
    {
        ReadOnlySpan<float> basis = AfvBasis;
        ref float weights = ref MemoryMarshal.GetReference(basis);
        ref float output = ref MemoryMarshal.GetReference(destination);
        int lanes = Unsafe.SizeOf<TLanes>() / sizeof(float);
        for (int column = 0; column < 16; column += lanes)
        {
            TLanes sum = default;
            for (int coefficient = 0; coefficient < 16; coefficient++)
            {
                TLanes weight = TOperator.Load(ref Unsafe.Add(ref weights, (coefficient * 16) + column));

                // Each basis row supplies the weight of one coefficient at consecutive output positions.
                // Keep the specified coefficient summation order. Unlike the DCT butterflies, this is a fixed sixteen-term basis.
                sum = TOperator.Add(sum, TOperator.Multiply(weight, coefficients[coefficient]));
            }

            TOperator.Store(sum, ref Unsafe.Add(ref output, column));
        }
    }

    /// <summary>
    /// Expands the four lowest coefficients into the DC values of the four spatial subblocks.
    /// </summary>
    /// <param name="coefficients">The complete coefficient block.</param>
    /// <returns>The DC values in top-left, top-right, bottom-left, bottom-right order.</returns>
    private static Vector4 GetSubBlockDcs(ReadOnlySpan<float> coefficients)
    {
        float a = coefficients[0];
        float b = coefficients[1];
        float c = coefficients[8];
        float d = coefficients[9];
        return new Vector4(((a + b) + c) + d, ((a + b) - c) - d, ((a - b) + c) - d, ((a - b) - c) + d);
    }
}
