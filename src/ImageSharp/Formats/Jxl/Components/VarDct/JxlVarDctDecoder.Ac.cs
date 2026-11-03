// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;
using SixLabors.ImageSharp.Formats.Jxl.Components.Modular;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.VarDct;

internal sealed partial class JxlVarDctDecoder
{
    /// <summary>
    /// The log2 tile width in DC blocks. Native chroma_from_luma.h kColorTileDim covers 64 coding pixels, or eight blocks.
    /// </summary>
    private const int ColorTileShift = 3;

    /// <summary>
    /// The 27 raw transform strategies in libjxl ac_strategy.h AcStrategy::Type, before coefficient-order family sharing.
    /// </summary>
    private const int StrategyCount = 27;

    /// <summary>
    /// The unassigned block-map sentinel, outside every encoded strategy value. It detects overlapping transform footprints.
    /// </summary>
    private const byte InvalidStrategy = byte.MaxValue;

    /// <summary>
    /// The low bit of a stored strategy byte. It marks the transform origin; higher bits hold the raw strategy.
    /// </summary>
    private const byte FirstBlockFlag = 1;

    /// <summary>
    /// The largest decoded AC quantizer: native DecodeAcMetadata clamps its raw integer to [0,255], then adds one.
    /// </summary>
    private const int MaximumAcQuantizer = 256;

    /// <summary>
    /// The eight valid EPF sharpness indices from native DecodeAcMetadata. Values outside [0,7] are invalid.
    /// </summary>
    private const int SharpnessCount = 8;

    /// <summary>
    /// The two modular metadata rows: raw transform strategies, followed by their origin quantizers.
    /// </summary>
    private const int RecordRows = 2;

    private readonly List<Buffer2D<sbyte>> correlationMaps = new(2);
    private JxlModularChannel acRecords;
    private Buffer2D<byte>? strategies;
    private Buffer2D<int>? quantization;
    private Buffer2D<byte>? sharpness;
    private int acGroupBlockDimension;

    /// <summary>
    /// Gets the transform map on the padded block grid. Bit zero marks the first block; the remaining bits contain the strategy code.
    /// </summary>
    public Buffer2DRegion<byte> Strategies { get; private set; }

    /// <summary>
    /// Gets the AC quantizers, from one through 256, at the first block of each transform.
    /// </summary>
    /// <remarks>
    /// Read a quantizer only where bit zero of <see cref="Strategies"/> marks the first block.
    /// Continuation cells do not contain quantizers.
    /// </remarks>
    public Buffer2DRegion<int> Quantization { get; private set; }

    /// <summary>
    /// Gets the EPF sharpness indices, from zero through seven, on the padded block grid.
    /// </summary>
    public Buffer2DRegion<byte> Sharpness { get; private set; }

    /// <summary>
    /// Gets the signed Y-to-X correlation adjustments, with one entry per eight-by-eight block tile.
    /// </summary>
    public Buffer2DRegion<sbyte> CorrelationX { get; private set; }

    /// <summary>
    /// Gets the signed Y-to-B correlation adjustments, with one entry per eight-by-eight block tile.
    /// </summary>
    public Buffer2DRegion<sbyte> CorrelationB { get; private set; }

    /// <summary>
    /// Gets a mask whose bit n is set when raw strategy n occurs in decoded AC metadata.
    /// </summary>
    public uint UsedStrategies { get; private set; }

    /// <summary>
    /// Gets the number of horizontal DC blocks covered by each validated raw strategy code.
    /// </summary>
    public static ReadOnlySpan<byte> StrategyWidths =>
    [
        1, 1, 1, 1, 2, 4, 1, 2, 1, 4, 2, 4, 1, 1, 1, 1, 1, 1, 8, 4, 8, 16, 8, 16, 32, 16, 32
    ];

    /// <summary>
    /// Gets the number of vertical DC blocks covered by each validated raw strategy code.
    /// </summary>
    public static ReadOnlySpan<byte> StrategyHeights =>
    [
        1, 1, 1, 1, 2, 4, 2, 1, 4, 1, 4, 2, 1, 1, 1, 1, 1, 1, 8, 8, 4, 16, 16, 8, 32, 32, 16
    ];

    /// <summary>
    /// Reads one DC group's correlation maps, transform records and sharpness through the shared modular decoder.
    /// </summary>
    /// <param name="reader">The bounded section reader after this group's modular extra-channel data.</param>
    /// <param name="modular">The frame's shared modular tree, transforms and reusable entropy workspace.</param>
    /// <param name="group">The validated DC group index in row-major order.</param>
    /// <param name="cancellationToken">The token checked during entropy decoding and map traversal.</param>
    public void ReadAcMetadata(ref JxlBitReader reader, JxlModularDecoder modular, int group, CancellationToken cancellationToken)
    {
        Rectangle rectangle = this.GetDcGroupRectangle(group);
        int area = rectangle.Width * rectangle.Height;
        int count = (int)reader.ReadBits(32 - BitOperations.LeadingZeroCount((uint)(area - 1))) + 1;
        Size colorSize = new(((rectangle.Width - 1) >> ColorTileShift) + 1, ((rectangle.Height - 1) >> ColorTileShift) + 1);
        Rectangle colorBounds = new(Point.Empty, colorSize);
        InlineArray4<JxlModularChannel> targetStorage = default;
        Span<JxlModularChannel> targets = targetStorage;

        // DC floats and their context indices are already retained. Reuse their integer scratch for the next substream.
        // Correlation channels carry shifts (3,3); records and sharpness use (0,0), even when their shapes happen to match.
        targets[0] = this.dcScratch[1].Borrow(colorBounds, ColorTileShift, ColorTileShift);
        targets[1] = this.dcScratch[2].Borrow(colorBounds, ColorTileShift, ColorTileShift);
        targets[2] = this.acRecords.Borrow(new Rectangle(0, 0, count, RecordRows));
        targets[3] = this.dcScratch[0].Borrow(new Rectangle(Point.Empty, rectangle.Size));
        modular.ReadChannels(ref reader, targets, 1 + (2 * this.dcGroupCount) + group, this.sampleBitDepth, cancellationToken);

        Rectangle colorRectangle = new(rectangle.X >> ColorTileShift, rectangle.Y >> ColorTileShift, colorSize.Width, colorSize.Height);
        Buffer2DRegion<sbyte> outputCorrelationX = this.CorrelationX.GetSubRegion(colorRectangle);
        Buffer2DRegion<sbyte> outputCorrelationB = this.CorrelationB.GetSubRegion(colorRectangle);
        for (int y = 0; y < colorSize.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ConvertCorrelationRow(targets[0].Region.DangerousGetRowSpan(y), outputCorrelationX.DangerousGetRowSpan(y));
            ConvertCorrelationRow(targets[1].Region.DangerousGetRowSpan(y), outputCorrelationB.DangerousGetRowSpan(y));
        }

        Buffer2DRegion<byte> outputStrategies = this.Strategies.GetSubRegion(rectangle);
        Buffer2DRegion<int> outputQuantization = this.Quantization.GetSubRegion(rectangle);
        Buffer2DRegion<byte> outputSharpness = this.Sharpness.GetSubRegion(rectangle);
        ReadOnlySpan<int> strategyRecords = targets[2].Region.DangerousGetRowSpan(0);
        ReadOnlySpan<int> quantizerRecords = targets[2].Region.DangerousGetRowSpan(1);
        ReadOnlySpan<byte> widths = StrategyWidths;
        ReadOnlySpan<byte> heights = StrategyHeights;
        int record = 0;

        for (int y = 0; y < rectangle.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Sharpness belongs to every block, including continuations of a transform that started on an earlier row.
            // Validate before narrowing. Saturation here would conceal a malformed sharpness field.
            ConvertSharpnessRow(targets[3].Region.DangerousGetRowSpan(y), outputSharpness.DangerousGetRowSpan(y));
            Span<byte> strategyRow = outputStrategies.DangerousGetRowSpan(y);
            for (int x = 0; x < rectangle.Width; x++)
            {
                if (strategyRow[x] != InvalidStrategy)
                {
                    continue;
                }

                if (record >= count)
                {
                    throw new InvalidImageContentException("The JPEG XL AC metadata has too few transform records.");
                }

                int strategy = strategyRecords[record];
                if ((uint)strategy >= StrategyCount)
                {
                    throw new InvalidImageContentException("The JPEG XL AC transform strategy is invalid.");
                }

                int width = widths[strategy];
                int height = heights[strategy];
                if (!this.is444 && (width != 1 || height != 1))
                {
                    throw new InvalidImageContentException("The JPEG XL multiblock transform is incompatible with chroma subsampling.");
                }

                // A transform must fit both the padded frame grid and its AC group. DC groups are larger and cannot
                // provide the second bound. Origins and dimensions here are in eight-pixel blocks, not coding pixels.
                int absoluteX = rectangle.X + x;
                int absoluteY = rectangle.Y + y;
                int groupRight = ((absoluteX / this.acGroupBlockDimension) + 1) * this.acGroupBlockDimension;
                int groupBottom = ((absoluteY / this.acGroupBlockDimension) + 1) * this.acGroupBlockDimension;
                if (absoluteX + width > Math.Min(rectangle.Right, groupRight) || absoluteY + height > Math.Min(rectangle.Bottom, groupBottom))
                {
                    throw new InvalidImageContentException("The JPEG XL AC transform exceeds its block grid or group boundary.");
                }

                int quantizer = 1 + Math.Clamp(quantizerRecords[record], 0, MaximumAcQuantizer - 1);
                byte packedStrategy = (byte)(strategy << 1);
                for (int row = y; row < y + height; row++)
                {
                    Span<byte> covered = outputStrategies.DangerousGetRowSpan(row).Slice(x, width);
                    if (covered.ContainsAnyExcept(InvalidStrategy))
                    {
                        throw new InvalidImageContentException("The JPEG XL AC transforms overlap.");
                    }

                    // Mark every covered block so later record traversal can skip continuations and detect overlap.
                    covered.Fill(packedStrategy);
                }

                strategyRow[x] |= FirstBlockFlag;

                // AC coefficient decoding and EPF read quantization only at transform origins, as in native decoding.
                outputQuantization.DangerousGetRowSpan(y)[x] = quantizer;
                this.UsedStrategies |= 1U << strategy;
                record++;
            }
        }

        // The encoded count can exceed the number of placed transforms. Both reference decoders permit unused pairs;
        // all records were still entropy-decoded above, so their final state and following bit position are validated.
    }

    /// <summary>
    /// Saturates signed correlation entries to their encoded eight-bit map range.
    /// </summary>
    /// <param name="source">The signed reconstructed correlation row.</param>
    /// <param name="destination">The disjoint output row, with at least the source length.</param>
    public static void ConvertCorrelationRow(ReadOnlySpan<int> source, Span<sbyte> destination)
        => ConvertMetadataRow<CorrelationOperator>(source, MemoryMarshal.Cast<sbyte, byte>(destination));

    /// <summary>
    /// Validates sharpness indices and narrows them without changing valid values.
    /// </summary>
    /// <param name="source">The reconstructed indices, each from zero through seven.</param>
    /// <param name="destination">The disjoint output row, with at least the source length.</param>
    /// <exception cref="InvalidImageContentException">A reconstructed index is outside its encoded range.</exception>
    public static void ConvertSharpnessRow(ReadOnlySpan<int> source, Span<byte> destination)
        => ConvertMetadataRow<SharpnessOperator>(source, destination);

    /// <summary>
    /// Allocates frame maps and fixed AC metadata scratch before entering any DC-group loop.
    /// </summary>
    /// <param name="allocator">The tracked allocator for all retained maps and integer records.</param>
    private void PrepareAcStorage(MemoryAllocator allocator)
    {
        Size colorSize = new(((this.dcSize.Width - 1) >> ColorTileShift) + 1, ((this.dcSize.Height - 1) >> ColorTileShift) + 1);
        for (int c = 0; c < 2; c++)
        {
            this.correlationMaps.Add(allocator.Allocate2D<sbyte>(colorSize));
        }

        this.CorrelationX = this.correlationMaps[0].GetRegion();
        this.CorrelationB = this.correlationMaps[1].GetRegion();
        this.strategies = allocator.Allocate2D<byte>(this.dcSize);
        this.Strategies = this.strategies.GetRegion();
        this.quantization = allocator.Allocate2D<int>(this.dcSize);
        this.Quantization = this.quantization.GetRegion();
        this.sharpness = allocator.Allocate2D<byte>(this.dcSize);
        this.Sharpness = this.sharpness.GetRegion();
        this.acGroupBlockDimension = this.dcGroupDimension / BlockSize;
        for (int y = 0; y < this.dcSize.Height; y++)
        {
            this.Strategies.DangerousGetRowSpan(y).Fill(InvalidStrategy);
        }

        // The field stores count - 1 in ceil(log2(group area)) bits. Round the largest group area up to a power of two;
        // this also covers unused encoded records at a partial edge. VarDCT's maximum is 65536 entries in each row.
        // The two-row allocation is at most 512 KiB and is reused unchanged by every DC group.
        Size largestGroup = this.GetDcGroupRectangle(0).Size;
        int capacity = (int)BitOperations.RoundUpToPowerOf2((uint)(largestGroup.Width * largestGroup.Height));
        this.acRecords = JxlModularChannel.Allocate(allocator, new JxlModularChannelLayout(new Size(capacity, RecordRows), 0, 0));
    }

    /// <summary>
    /// Converts one metadata row with a selected scalar and SIMD rule, preserving column order while narrowing.
    /// </summary>
    /// <typeparam name="TOperator">The correlation saturation or sharpness validation rule.</typeparam>
    /// <param name="source">The reconstructed integer row.</param>
    /// <param name="destination">The output bytes, with at least the source length.</param>
    private static void ConvertMetadataRow<TOperator>(ReadOnlySpan<int> source, Span<byte> destination)
        where TOperator : struct, IAcMetadataOperator
    {
        ref int input = ref MemoryMarshal.GetReference(source);
        ref byte output = ref MemoryMarshal.GetReference(destination);
        int i = 0;

        // One integer lane represents one column. The operator establishes the byte range before narrowing.
        // Unaligned row origins are supported. Each store writes only four, eight or sixteen populated bytes.
        if (Vector512.IsHardwareAccelerated)
        {
            nuint count = (source.Length - i).Vector512Count<int>();
            for (nuint block = 0; block < count; block++, i += Vector512<int>.Count)
            {
                StoreMetadata(TOperator.Invoke(Vector512.LoadUnsafe(ref input, (nuint)i)), ref Unsafe.Add(ref output, i));
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint count = (source.Length - i).Vector256Count<int>();
            for (nuint block = 0; block < count; block++, i += Vector256<int>.Count)
            {
                StoreMetadata(TOperator.Invoke(Vector256.LoadUnsafe(ref input, (nuint)i)), ref Unsafe.Add(ref output, i));
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint count = (source.Length - i).Vector128Count<int>();
            for (nuint block = 0; block < count; block++, i += Vector128<int>.Count)
            {
                StoreMetadata(TOperator.Invoke(Vector128.LoadUnsafe(ref input, (nuint)i)), ref Unsafe.Add(ref output, i));
            }
        }

        // The remaining columns use the same range rule. Preserve signed correlation bytes by keeping their low eight bits.
        for (; i < source.Length; i++)
        {
            destination[i] = unchecked((byte)TOperator.Invoke(source[i]));
        }
    }
}
