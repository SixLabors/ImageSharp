// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;
using SixLabors.ImageSharp.Formats.Jxl.Components.Modular;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.VarDct;

internal sealed partial class JxlVarDctDecoder
{
    private const int BlockSize = 8;
    private readonly List<Buffer2D<float>> dcChannels = new(3);
    private InlineArray4<JxlModularChannel> dcScratch;
    private InlineArray4<byte> horizontalShifts;
    private InlineArray4<byte> verticalShifts;
    private Buffer2D<byte>? dcContexts;
    private Size dcSize;
    private int dcGroupDimension;
    private int dcGroupColumns;
    private int dcGroupCount;
    private bool is444;

    /// <summary>
    /// Gets the frame's DC context indices on the padded eight-pixel block grid.
    /// </summary>
    public Buffer2DRegion<byte> DcContexts { get; private set; }

    /// <summary>
    /// Borrows one frame DC plane in X, Y, B component order.
    /// </summary>
    /// <param name="component">The component index, from zero through two.</param>
    /// <returns>The float DC plane at that component's sampling resolution.</returns>
    public Buffer2DRegion<float> GetDcChannel(int component) => this.dcChannels[component].GetRegion();

    /// <summary>
    /// Reads one coded DC group and retains its dequantized planes and AC context indices.
    /// </summary>
    /// <param name="reader">The section reader at the group's two-bit extra-precision field.</param>
    /// <param name="modular">The frame's shared modular tree, transforms and reusable entropy workspace.</param>
    /// <param name="group">The validated DC group index in row-major order.</param>
    /// <param name="cancellationToken">The token checked between channels and rows.</param>
    public void ReadDcGroup(ref JxlBitReader reader, JxlModularDecoder modular, int group, CancellationToken cancellationToken)
    {
        int extraPrecision = (int)reader.ReadBits(2);
        Rectangle rectangle = this.GetDcGroupRectangle(group);
        InlineArray4<JxlModularChannel> targetStorage = default;
        Span<JxlModularChannel> targets = targetStorage;

        for (int c = 0; c < 3; c++)
        {
            // The modular stream stores Y, X, B. Its channel shifts stay zero: subsampling changes their dimensions,
            // not the cross-channel matching or squeeze coordinates within this independent DC stream.
            int component = c < 2 ? c ^ 1 : c;
            Size size = new(rectangle.Width >> this.horizontalShifts[component], rectangle.Height >> this.verticalShifts[component]);
            targets[c] = this.dcScratch[c].Borrow(new Rectangle(Point.Empty, size));
        }

        // DC streams inherit the image's modular bit depth, including inverse-palette scaling. They use stream IDs
        // 1 + group, before extra-channel DC streams and AC metadata. No intermediate pixels or color clipping apply.
        modular.ReadChannels(ref reader, targets[..3], 1 + group, cancellationToken);

        Buffer2DRegion<int> quantizedY = targets[0].Region;
        Buffer2DRegion<int> quantizedX = targets[1].Region;
        Buffer2DRegion<int> quantizedB = targets[2].Region;
        if (this.is444)
        {
            Buffer2DRegion<float> outputX = this.GetDcChannel(0).GetSubRegion(rectangle);
            Buffer2DRegion<float> outputY = this.GetDcChannel(1).GetSubRegion(rectangle);
            Buffer2DRegion<float> outputB = this.GetDcChannel(2).GetSubRegion(rectangle);
            Buffer2DRegion<byte> contexts = this.DcContexts.GetSubRegion(rectangle);

            for (int row = 0; row < rectangle.Height; row++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                this.DequantizeDcRow(
                    quantizedX.DangerousGetRowSpan(row),
                    quantizedY.DangerousGetRowSpan(row),
                    quantizedB.DangerousGetRowSpan(row),
                    outputX.DangerousGetRowSpan(row),
                    outputY.DangerousGetRowSpan(row),
                    outputB.DangerousGetRowSpan(row),
                    contexts.DangerousGetRowSpan(row),
                    extraPrecision);
            }
        }
        else
        {
            this.DequantizeSubsampledDcGroup(quantizedX, quantizedY, quantizedB, rectangle, extraPrecision, cancellationToken);
        }
    }

    /// <summary>
    /// Restores one 4:4:4 DC row and its context buckets without intermediate float rows.
    /// </summary>
    /// <param name="quantizedX">The signed quantized X samples.</param>
    /// <param name="quantizedY">The signed quantized Y samples.</param>
    /// <param name="quantizedB">The signed quantized B samples.</param>
    /// <param name="outputX">The restored X samples.</param>
    /// <param name="outputY">The restored Y samples.</param>
    /// <param name="outputB">The restored B samples.</param>
    /// <param name="contexts">The AC context index for each original quantized sample triple.</param>
    /// <param name="extraPrecision">The two-bit precision field, from zero through three.</param>
    /// <remarks>
    /// All rows have equal length. Outputs are disjoint from the integer input storage.
    /// This operation applies DC color correlation. Subsampled DC planes use independent scaling instead.
    /// </remarks>
    public void DequantizeDcRow(
        ReadOnlySpan<int> quantizedX,
        ReadOnlySpan<int> quantizedY,
        ReadOnlySpan<int> quantizedB,
        Span<float> outputX,
        Span<float> outputY,
        Span<float> outputB,
        Span<byte> contexts,
        int extraPrecision)
    {
        DcOperator operation = new(
            this.DcMultipliers * (1F / (1 << extraPrecision)),
            this.DcCorrelationX,
            this.DcCorrelationB,
            this.GetDcThresholds(0),
            this.GetDcThresholds(1),
            this.GetDcThresholds(2));

        ref int sourceX = ref MemoryMarshal.GetReference(quantizedX);
        ref int sourceY = ref MemoryMarshal.GetReference(quantizedY);
        ref int sourceB = ref MemoryMarshal.GetReference(quantizedB);
        ref float destinationX = ref MemoryMarshal.GetReference(outputX);
        ref float destinationY = ref MemoryMarshal.GetReference(outputY);
        ref float destinationB = ref MemoryMarshal.GetReference(outputB);
        ref byte destinationContext = ref MemoryMarshal.GetReference(contexts);
        int i = 0;

        // One lane owns a complete DC block position. Convert and scale once, then restore X and B from Y.
        // Contexts use the original integer values, before extra precision, dequantization or color correlation.
        if (Vector512.IsHardwareAccelerated)
        {
            nuint count = (quantizedX.Length - i).Vector512Count<int>();
            for (nuint block = 0; block < count; block++, i += Vector512<int>.Count)
            {
                operation.Invoke(
                    Vector512.LoadUnsafe(ref sourceX, (nuint)i),
                    Vector512.LoadUnsafe(ref sourceY, (nuint)i),
                    Vector512.LoadUnsafe(ref sourceB, (nuint)i),
                    out Vector512<float> x,
                    out Vector512<float> y,
                    out Vector512<float> b,
                    out Vector512<int> context);

                x.StoreUnsafe(ref destinationX, (nuint)i);
                y.StoreUnsafe(ref destinationY, (nuint)i);
                b.StoreUnsafe(ref destinationB, (nuint)i);
                DcOperator.StoreContexts(context, ref Unsafe.Add(ref destinationContext, i));
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint count = (quantizedX.Length - i).Vector256Count<int>();
            for (nuint block = 0; block < count; block++, i += Vector256<int>.Count)
            {
                operation.Invoke(
                    Vector256.LoadUnsafe(ref sourceX, (nuint)i),
                    Vector256.LoadUnsafe(ref sourceY, (nuint)i),
                    Vector256.LoadUnsafe(ref sourceB, (nuint)i),
                    out Vector256<float> x,
                    out Vector256<float> y,
                    out Vector256<float> b,
                    out Vector256<int> context);

                x.StoreUnsafe(ref destinationX, (nuint)i);
                y.StoreUnsafe(ref destinationY, (nuint)i);
                b.StoreUnsafe(ref destinationB, (nuint)i);
                DcOperator.StoreContexts(context, ref Unsafe.Add(ref destinationContext, i));
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint count = (quantizedX.Length - i).Vector128Count<int>();
            for (nuint block = 0; block < count; block++, i += Vector128<int>.Count)
            {
                operation.Invoke(
                    Vector128.LoadUnsafe(ref sourceX, (nuint)i),
                    Vector128.LoadUnsafe(ref sourceY, (nuint)i),
                    Vector128.LoadUnsafe(ref sourceB, (nuint)i),
                    out Vector128<float> x,
                    out Vector128<float> y,
                    out Vector128<float> b,
                    out Vector128<int> context);

                x.StoreUnsafe(ref destinationX, (nuint)i);
                y.StoreUnsafe(ref destinationY, (nuint)i);
                b.StoreUnsafe(ref destinationB, (nuint)i);
                DcOperator.StoreContexts(context, ref Unsafe.Add(ref destinationContext, i));
            }
        }

        // Scalar samples use the same single-precision conversion order and multiply-add policy as vector lanes.
        for (; i < quantizedX.Length; i++)
        {
            operation.Invoke(quantizedX[i], quantizedY[i], quantizedB[i], out outputX[i], out outputY[i], out outputB[i], out contexts[i]);
        }
    }

    /// <summary>
    /// Allocates all DC planes and the maximum reusable integer group once at the frame boundary.
    /// </summary>
    /// <param name="frame">The frame's coding size and cached sampling shifts.</param>
    /// <param name="allocator">The tracked allocator used by every retained sample plane.</param>
    private void PrepareDcStorage(JxlFrameHeader frame, MemoryAllocator allocator)
    {
        ReadOnlySpan<byte> horizontal = frame.HorizontalSamplingShifts;
        ReadOnlySpan<byte> vertical = frame.VerticalSamplingShifts;
        int maximumHorizontal = 0;
        int maximumVertical = 0;
        this.is444 = true;
        for (int c = 0; c < 3; c++)
        {
            this.horizontalShifts[c] = horizontal[c];
            this.verticalShifts[c] = vertical[c];
            maximumHorizontal = Math.Max(maximumHorizontal, horizontal[c]);
            maximumVertical = Math.Max(maximumVertical, vertical[c]);
            this.is444 &= horizontal[c] == 0 && vertical[c] == 0;
        }

        // Pad the eight-pixel block grid to the largest sampling factor. Every reduced channel then has an integer
        // number of blocks, including a partial image edge. DC groups cover GroupSize blocks, not GroupSize pixels.
        Size codingSize = frame.CodingSize;
        this.dcSize = new Size(
            (((codingSize.Width - 1) / (BlockSize << maximumHorizontal)) + 1) << maximumHorizontal,
            (((codingSize.Height - 1) / (BlockSize << maximumVertical)) + 1) << maximumVertical);

        this.dcGroupDimension = frame.GroupSize;
        this.dcGroupColumns = ((this.dcSize.Width - 1) / this.dcGroupDimension) + 1;

        // The frame header already bounds the total section count, including every DC group, to an int-sized table.
        this.dcGroupCount = this.dcGroupColumns * (((this.dcSize.Height - 1) / this.dcGroupDimension) + 1);
        Size scratchSize = new(Math.Min(this.dcSize.Width, this.dcGroupDimension), Math.Min(this.dcSize.Height, this.dcGroupDimension));
        for (int c = 0; c < 3; c++)
        {
            Size size = new(this.dcSize.Width >> horizontal[c], this.dcSize.Height >> vertical[c]);
            this.dcChannels.Add(allocator.Allocate2D<float>(size));

            // Scratch is encoded Y, X, B. After DC conversion, plane zero also holds the full-grid sharpness samples.
            // Keep that backing plane unreduced; each DC stream still borrows its actual component dimensions.
            int component = c < 2 ? c ^ 1 : c;
            Size integerSize = c == 0 ? scratchSize : new(scratchSize.Width >> horizontal[component], scratchSize.Height >> vertical[component]);
            this.dcScratch[c] = JxlModularChannel.Allocate(allocator, new JxlModularChannelLayout(integerSize, 0, 0));
        }

        this.dcContexts = allocator.Allocate2D<byte>(this.dcSize);
        this.DcContexts = this.dcContexts.GetRegion();
    }

    /// <summary>
    /// Locates one DC group on the sampling-padded block grid shared by DC samples and AC metadata.
    /// </summary>
    /// <param name="group">The validated row-major DC group index.</param>
    /// <returns>The group clipped to the padded eight-pixel block dimensions.</returns>
    private Rectangle GetDcGroupRectangle(int group)
    {
        // Native DCGroupRect clips to xsize_blocks/ysize_blocks, not the unpadded pixel size.
        // This retains the final sampling-alignment blocks used by subsampled DC and strategy metadata.
        int x = (group % this.dcGroupColumns) * this.dcGroupDimension;
        int y = (group / this.dcGroupColumns) * this.dcGroupDimension;
        return new Rectangle(x, y, Math.Min(this.dcGroupDimension, this.dcSize.Width - x), Math.Min(this.dcGroupDimension, this.dcSize.Height - y));
    }

    /// <summary>
    /// Restores reduced DC planes, then classifies their integer samples on the common block grid.
    /// </summary>
    /// <param name="quantizedX">The reduced quantized X plane.</param>
    /// <param name="quantizedY">The reduced quantized Y plane.</param>
    /// <param name="quantizedB">The reduced quantized B plane.</param>
    /// <param name="rectangle">The group on the common DC block grid.</param>
    /// <param name="extraPrecision">The encoded precision field.</param>
    /// <param name="cancellationToken">The token checked between rows.</param>
    private void DequantizeSubsampledDcGroup(
        Buffer2DRegion<int> quantizedX,
        Buffer2DRegion<int> quantizedY,
        Buffer2DRegion<int> quantizedB,
        Rectangle rectangle,
        int extraPrecision,
        CancellationToken cancellationToken)
    {
        Vector3 factors = this.DcMultipliers * (1F / (1 << extraPrecision));
        InlineArray4<Buffer2DRegion<int>> quantizedStorage = default;
        Span<Buffer2DRegion<int>> quantized = quantizedStorage;
        quantized[0] = quantizedX;
        quantized[1] = quantizedY;
        quantized[2] = quantizedB;
        for (int c = 0; c < 3; c++)
        {
            int horizontal = this.horizontalShifts[c];
            int vertical = this.verticalShifts[c];
            Rectangle componentRectangle = new(rectangle.X >> horizontal, rectangle.Y >> vertical, rectangle.Width >> horizontal, rectangle.Height >> vertical);

            Buffer2DRegion<float> output = this.GetDcChannel(c).GetSubRegion(componentRectangle);
            for (int y = 0; y < output.Height; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ScaleDcRow(quantized[c].DangerousGetRowSpan(y), output.DangerousGetRowSpan(y), factors[c]);
            }
        }

        // Different grids cannot share the fused 4:4:4 output traversal. Contexts still occupy the common block grid.
        // Native subsampled DC does not apply color-from-luma correlation; only the independent factors above apply.
        Buffer2DRegion<byte> contexts = this.DcContexts.GetSubRegion(rectangle);
        if (this.DcContextCount == 1)
        {
            contexts.Clear();
            return;
        }

        DcOperator operation = new(factors, 0F, 0F, this.GetDcThresholds(0), this.GetDcThresholds(1), this.GetDcThresholds(2));

        for (int y = 0; y < rectangle.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            this.ClassifySubsampledDcRow(
                quantizedX.DangerousGetRowSpan(y >> this.verticalShifts[0]),
                quantizedY.DangerousGetRowSpan(y >> this.verticalShifts[1]),
                quantizedB.DangerousGetRowSpan(y >> this.verticalShifts[2]),
                contexts.DangerousGetRowSpan(y),
                in operation);
        }
    }

    /// <summary>
    /// Converts and scales one integer row with the same operation order at every SIMD width.
    /// </summary>
    /// <param name="source">The quantized DC samples.</param>
    /// <param name="destination">The component's float DC row.</param>
    /// <param name="factor">The complete DC factor including extra precision.</param>
    private static void ScaleDcRow(ReadOnlySpan<int> source, Span<float> destination, float factor)
    {
        ref int input = ref MemoryMarshal.GetReference(source);
        ref float output = ref MemoryMarshal.GetReference(destination);
        int i = 0;

        if (Vector512.IsHardwareAccelerated)
        {
            nuint count = (source.Length - i).Vector512Count<int>();
            for (nuint block = 0; block < count; block++, i += Vector512<int>.Count)
            {
                DcOperator.Scale(Vector512.LoadUnsafe(ref input, (nuint)i), factor).StoreUnsafe(ref output, (nuint)i);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint count = (source.Length - i).Vector256Count<int>();
            for (nuint block = 0; block < count; block++, i += Vector256<int>.Count)
            {
                DcOperator.Scale(Vector256.LoadUnsafe(ref input, (nuint)i), factor).StoreUnsafe(ref output, (nuint)i);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint count = (source.Length - i).Vector128Count<int>();
            for (nuint block = 0; block < count; block++, i += Vector128<int>.Count)
            {
                DcOperator.Scale(Vector128.LoadUnsafe(ref input, (nuint)i), factor).StoreUnsafe(ref output, (nuint)i);
            }
        }

        for (; i < source.Length; i++)
        {
            destination[i] = DcOperator.Scale(source[i], factor);
        }
    }

    /// <summary>
    /// Classifies subsampled integer rows without expanding them into full-width temporary rows.
    /// </summary>
    /// <param name="x">The quantized X row at its own resolution.</param>
    /// <param name="y">The quantized Y row at its own resolution.</param>
    /// <param name="b">The quantized B row at its own resolution.</param>
    /// <param name="contexts">The common-grid destination context row.</param>
    /// <param name="operation">The frame's prepared threshold operator.</param>
    private void ClassifySubsampledDcRow(ReadOnlySpan<int> x, ReadOnlySpan<int> y, ReadOnlySpan<int> b, Span<byte> contexts, in DcOperator operation)
    {
        ref int sourceX = ref MemoryMarshal.GetReference(x);
        ref int sourceY = ref MemoryMarshal.GetReference(y);
        ref int sourceB = ref MemoryMarshal.GetReference(b);
        ref byte destination = ref MemoryMarshal.GetReference(contexts);
        int i = 0;

        // Shift logical block indices to repeat each reduced sample over its horizontal sampling interval.
        // Shared gather helpers load those indices directly, including portable fallbacks, without staging expanded rows.
        if (Vector512.IsHardwareAccelerated)
        {
            nuint count = (contexts.Length - i).Vector512Count<int>();
            for (nuint block = 0; block < count; block++, i += Vector512<int>.Count)
            {
                Vector512<int> indices = Vector512.Create(i) + Vector512<int>.Indices;
                Vector512<int> qx = Vector512_.Gather(ref sourceX, indices >> this.horizontalShifts[0]);
                Vector512<int> qy = Vector512_.Gather(ref sourceY, indices >> this.horizontalShifts[1]);
                Vector512<int> qb = Vector512_.Gather(ref sourceB, indices >> this.horizontalShifts[2]);
                DcOperator.StoreContexts(operation.GetContext(qx, qy, qb), ref Unsafe.Add(ref destination, i));
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint count = (contexts.Length - i).Vector256Count<int>();
            for (nuint block = 0; block < count; block++, i += Vector256<int>.Count)
            {
                Vector256<int> indices = Vector256.Create(i) + Vector256<int>.Indices;
                Vector256<int> qx = Vector256_.Gather(ref sourceX, indices >> this.horizontalShifts[0]);
                Vector256<int> qy = Vector256_.Gather(ref sourceY, indices >> this.horizontalShifts[1]);
                Vector256<int> qb = Vector256_.Gather(ref sourceB, indices >> this.horizontalShifts[2]);
                DcOperator.StoreContexts(operation.GetContext(qx, qy, qb), ref Unsafe.Add(ref destination, i));
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint count = (contexts.Length - i).Vector128Count<int>();
            for (nuint block = 0; block < count; block++, i += Vector128<int>.Count)
            {
                Vector128<int> indices = Vector128.Create(i) + Vector128<int>.Indices;
                Vector128<int> qx = Vector128_.Gather(ref sourceX, indices >> this.horizontalShifts[0]);
                Vector128<int> qy = Vector128_.Gather(ref sourceY, indices >> this.horizontalShifts[1]);
                Vector128<int> qb = Vector128_.Gather(ref sourceB, indices >> this.horizontalShifts[2]);
                DcOperator.StoreContexts(operation.GetContext(qx, qy, qb), ref Unsafe.Add(ref destination, i));
            }
        }

        for (; i < contexts.Length; i++)
        {
            contexts[i] = operation.GetContext(x[i >> this.horizontalShifts[0]], y[i >> this.horizontalShifts[1]], b[i >> this.horizontalShifts[2]]);
        }
    }
}
