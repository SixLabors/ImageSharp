// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Cdef;

internal static partial class Av1CdefEncoder
{
    /// <summary>
    /// The horizontal border leaves eight samples available for packed filter loads.
    /// </summary>
    private const int HorizontalBorder = 8;

    /// <summary>
    /// Directional filter taps reach two rows above and below a block.
    /// </summary>
    private const int VerticalBorder = 2;

    /// <summary>
    /// The maximum search-unit width, including both horizontal borders.
    /// </summary>
    private const int SourceStride = 128 + (2 * HorizontalBorder);

    /// <summary>
    /// The complete bordered search-unit storage in sixteen-bit samples.
    /// </summary>
    private const int SourceLength = SourceStride * (128 + (2 * VerticalBorder));

    /// <summary>
    /// The maximum number of eight-by-eight blocks in a search unit.
    /// </summary>
    private const int MaximumBlockCount = 256;

    /// <summary>
    /// Selects strengths and filters the deblocked reconstruction before bitstream packing.
    /// </summary>
    /// <typeparam name="TSample">The native component storage type.</typeparam>
    /// <typeparam name="TOperator">The closed component operations.</typeparam>
    /// <param name="allocator">The allocator for frame-scoped search storage.</param>
    /// <param name="picture">The retained frame decisions.</param>
    /// <param name="source">The original component planes.</param>
    /// <param name="reconstruction">The deblocked component planes to filter.</param>
    public static void ApplyFrame<TSample, TOperator>(
        MemoryAllocator allocator,
        Av1PictureControlSet picture,
        Av1EncoderFrame<TSample> source,
        Av1EncoderFrame<TSample> reconstruction)
        where TSample : unmanaged
        where TOperator : struct, IEncodingOperator<TSample>
    {
        ObuFrameHeader header = picture.Parent.FrameHeader;
        ObuSequenceHeader sequence = picture.Sequence.SequenceHeader;
        if (!sequence.EnableCdef || header.CodedLossless || header.AllowIntraBlockCopy)
        {
            return;
        }

        // Adaptive CDEF follows the constant-quality level, which every usage but real time codes with. It turns CDEF
        // off up to quantizer index 32, which was best for still pictures; the damping keeps its earlier value.
        // Reference: av1_cdef_search().
        Av1EncoderOptions options = picture.Parent.EncoderOptions;
        bool adaptive = options.CdefControl == Av1CdefControl.Adaptive && !picture.Parent.SpeedSettings.IsRealtime;
        int qualityIndex = picture.Parent.ConstantQualityIndex;
        if (adaptive && qualityIndex <= 32)
        {
            header.CdefParameters.BitCount = 0;
            header.CdefParameters.YStrength[0] = 0;
            header.CdefParameters.UvStrength[0] = 0;
            return;
        }

        header.CdefParameters.Damping = 3 + (header.QuantizationParameters.BaseQIndex >> 6);
        ReadOnlySpan<byte> candidates = GetCandidateStrengths(
            picture.Parent.EncodingSpeed,
            sequence.IsStillPicture,
            picture.Parent.SpeedSettings.IsRealtime,
            new Size(source.Width, source.Height));

        if (candidates.IsEmpty)
        {
            // Adaptive CDEF leaves chroma unfiltered to save decode time.
            PredictStrengths(picture, avoidChroma: adaptive);
            if (header.CdefParameters.YStrength[0] == 0 && (source.IsMonochrome || header.CdefParameters.UvStrength[0] == 0))
            {
                return;
            }
        }

        // The unit workspace is shared across candidates and planes. Direction state and the compact
        // block list remain live across all three planes; no candidate owns an allocation.
        int outputLength = candidates.IsEmpty ? 0 : 128 * 128 * Unsafe.SizeOf<TSample>() / sizeof(ushort);
        int directionLength = MaximumBlockCount * sizeof(int) / sizeof(ushort);
        int stateOffset = SourceLength + outputLength;
        using IMemoryOwner<ushort> workspaceOwner = allocator.Allocate<ushort>(stateOffset + (2 * directionLength) + MaximumBlockCount);
        Span<ushort> workspace = workspaceOwner.Memory.Span;
        Span<ushort> input = workspace[..SourceLength];
        Span<TSample> output = MemoryMarshal.Cast<ushort, TSample>(workspace.Slice(SourceLength, outputLength));
        Span<int> directions = MemoryMarshal.Cast<ushort, int>(workspace.Slice(stateOffset, directionLength));
        Span<int> variances = MemoryMarshal.Cast<ushort, int>(workspace.Slice(stateOffset + directionLength, directionLength));
        Span<ushort> blocks = workspace.Slice(stateOffset + (2 * directionLength), MaximumBlockCount);

        if (!candidates.IsEmpty)
        {
            int unitColumns = (header.ModeInfoColumnCount + 15) >> 4;
            int unitRows = (header.ModeInfoRowCount + 15) >> 4;
            int capacity = unitColumns * unitRows;
            int errorLength = capacity * MaximumStrengthCount;
            using IMemoryOwner<ulong> errorOwner = allocator.Allocate<ulong>((2 * errorLength) + (MaximumStrengthCount * MaximumStrengthCount));
            using IMemoryOwner<int> indexOwner = allocator.Allocate<int>(capacity);
            Span<ulong> errors = errorOwner.Memory.Span;
            Span<ulong> lumaErrors = errors[..errorLength];
            Span<ulong> chromaErrors = errors.Slice(errorLength, errorLength);
            Span<int> indices = indexOwner.Memory.Span[..capacity];
            int count = 0;

            for (int row = 0; row < unitRows; row++)
            {
                for (int column = 0; column < unitColumns; column++)
                {
                    Point position = new(column << 4, row << 4);
                    Av1BlockSize size = picture.GetFromModeInfoGrid(position).Block.BlockSize;
                    bool wide = size is Av1BlockSize.Block128x128 or Av1BlockSize.Block128x64;
                    bool tall = size is Av1BlockSize.Block128x128 or Av1BlockSize.Block64x128;
                    if ((wide && (column & 1) != 0) || (tall && (row & 1) != 0))
                    {
                        continue;
                    }

                    int width = Math.Min(wide ? 32 : 16, header.ModeInfoColumnCount - position.X);
                    int height = Math.Min(tall ? 32 : 16, header.ModeInfoRowCount - position.Y);
                    int blockCount = GetBlocks(picture, position, width, height, blocks);
                    if (blockCount == 0)
                    {
                        continue;
                    }

                    MeasureUnit<TSample, TOperator>(
                        picture,
                        source,
                        reconstruction,
                        position,
                        width,
                        height,
                        blocks[..blockCount],
                        candidates,
                        input,
                        output,
                        directions,
                        variances,
                        lumaErrors.Slice(count * MaximumStrengthCount, MaximumStrengthCount),
                        chromaErrors.Slice(count * MaximumStrengthCount, MaximumStrengthCount));

                    indices[count++] = (position.Y * picture.ModeInfoStride) + position.X;
                }
            }

            int qIndex = header.QuantizationParameters.BaseQIndex + header.QuantizationParameters.DeltaQDc[0];
            int rateMultiplier = Av1RateDistortion.GetRateMultiplier(
                qIndex, sequence.ColorConfig.BitDepth, picture.Parent.FrameUpdateType, options.Tuning, picture.Parent.SpeedSettings.IsRealtime);

            // Adaptive CDEF halves the strengths up to quantizer index 220, and at low quantizers also zeroes the low
            // strengths, for which it searches at least one signaling bit. Reference: zero_low_cdef_strengths in
            // av1_set_speed_features_qindex_dependent().
            bool reduce = adaptive && qualityIndex <= 220;
            bool zeroLowStrengths = reduce &&
                (sequence.IsStillPicture || options.Tuning == Av1Tuning.Iq) &&
                header.QuantizationParameters.BaseQIndex <= 140;
            SelectStrengths(
                picture, candidates, lumaErrors, chromaErrors, indices[..count], errors[(2 * errorLength)..], rateMultiplier, zeroLowStrengths);
            if (reduce)
            {
                ReduceStrengths(header.CdefParameters, sequence.ColorConfig.PlaneCount > 1, zeroLowStrengths);
            }
        }

        FilterFrame<TSample, TOperator>(allocator, picture, reconstruction, input, directions, variances, blocks);
    }

    /// <summary>
    /// Collects the non-skipped eight-by-eight blocks of one search or application unit.
    /// </summary>
    /// <param name="picture">The retained mode grid.</param>
    /// <param name="position">The unit origin in four-by-four luma units.</param>
    /// <param name="width">The unit width in mode units.</param>
    /// <param name="height">The unit height in mode units.</param>
    /// <param name="blocks">The destination for packed relative block coordinates.</param>
    /// <returns>The populated block count.</returns>
    private static int GetBlocks(Av1PictureControlSet picture, Point position, int width, int height, Span<ushort> blocks)
    {
        int count = 0;
        for (int y = 0; y < height; y += 2)
        {
            for (int x = 0; x < width; x += 2)
            {
                bool skipped = true;
                for (int dy = 0; dy < Math.Min(2, height - y); dy++)
                {
                    for (int dx = 0; dx < Math.Min(2, width - x); dx++)
                    {
                        skipped &= picture.GetFromModeInfoGrid(new Point(position.X + x + dx, position.Y + y + dy)).Block.Skip;
                    }
                }

                if (!skipped)
                {
                    // Four bits hold either relative coordinate in the maximum 128x128 search unit.
                    blocks[count++] = (ushort)(((y >> 1) << 4) | (x >> 1));
                }
            }
        }

        return count;
    }

    /// <summary>
    /// Measures all strengths against the original samples without changing reconstruction.
    /// </summary>
    /// <typeparam name="TSample">The native component storage type.</typeparam>
    /// <typeparam name="TOperator">The closed component operations.</typeparam>
    /// <param name="picture">The frame parameters.</param>
    /// <param name="source">The original component planes.</param>
    /// <param name="reconstruction">The deblocked component planes.</param>
    /// <param name="position">The unit origin in mode units.</param>
    /// <param name="width">The unit width in mode units.</param>
    /// <param name="height">The unit height in mode units.</param>
    /// <param name="blocks">The non-skipped block coordinates.</param>
    /// <param name="candidates">The ordered packed strengths.</param>
    /// <param name="input">The bordered unit workspace.</param>
    /// <param name="output">The native filtered unit workspace.</param>
    /// <param name="directions">The reusable luma directions.</param>
    /// <param name="variances">The reusable luma direction variances.</param>
    /// <param name="lumaErrors">The destination luma errors.</param>
    /// <param name="chromaErrors">The destination combined chroma errors.</param>
    private static void MeasureUnit<TSample, TOperator>(
        Av1PictureControlSet picture,
        Av1EncoderFrame<TSample> source,
        Av1EncoderFrame<TSample> reconstruction,
        Point position,
        int width,
        int height,
        ReadOnlySpan<ushort> blocks,
        ReadOnlySpan<byte> candidates,
        Span<ushort> input,
        Span<TSample> output,
        Span<int> directions,
        Span<int> variances,
        Span<ulong> lumaErrors,
        Span<ulong> chromaErrors)
        where TSample : unmanaged
        where TOperator : struct, IEncodingOperator<TSample>
    {
        ObuFrameHeader header = picture.Parent.FrameHeader;
        int shift = reconstruction.LumaBitDepth - 8;
        int planeCount = source.IsMonochrome ? 1 : 3;
        for (int planeIndex = 0; planeIndex < planeCount; planeIndex++)
        {
            Av1Plane plane = (Av1Plane)planeIndex;
            int subX = planeIndex == 0 ? 0 : reconstruction.ChromaSubsamplingX;
            int subY = planeIndex == 0 ? 0 : reconstruction.ChromaSubsamplingY;
            int x = position.X << (2 - subX);
            int y = position.Y << (2 - subY);
            int planeWidth = header.ModeInfoColumnCount << (2 - subX);
            int planeHeight = header.ModeInfoRowCount << (2 - subY);
            int unitWidth = width << (2 - subX);
            int unitHeight = height << (2 - subY);
            Buffer2DRegion<TSample> samples = reconstruction.CodedView.GetPlane(plane);
            Buffer2DRegion<TSample> original = source.CodedView.GetPlane(plane);
            CopyUnit<TSample, TOperator>(samples, x, y, unitWidth, unitHeight, planeWidth, planeHeight, input);
            if (planeIndex == 0)
            {
                FindDirections(input, blocks, directions, variances, shift);
            }

            ReadOnlySpan<TSample> originalStorage = original.Buffer.DangerousGetSingleSpan();
            int originalOffset = ((original.Bounds.Y + y) * original.Stride) + original.Bounds.X + x;
            int blockWidth = 8 >> subX;
            int blockHeight = 8 >> subY;
            Span<ulong> errors = planeIndex == 0 ? lumaErrors : chromaErrors;
            for (int candidate = 0; candidate < candidates.Length; candidate++)
            {
                int strength = candidates[candidate];
                FilterUnit<TSample, TOperator>(
                    input,
                    output,
                    128,
                    blocks,
                    directions,
                    variances,
                    subX,
                    subY,
                    strength,
                    header.CdefParameters.Damping,
                    shift,
                    planeIndex == 0);

                long error = 0;
                foreach (ushort block in blocks)
                {
                    int blockX = (block & 15) * blockWidth;
                    int blockY = (block >> 4) * blockHeight;
                    error += TOperator.GetError(
                        originalStorage[(originalOffset + (blockY * original.Stride) + blockX)..],
                        original.Stride,
                        output[((blockY * 128) + blockX)..],
                        128,
                        blockWidth,
                        blockHeight);
                }

                // Normalize once after summing a plane's blocks. Truncating each block separately
                // would bias high-bit-depth candidates when their discarded low bits accumulate.
                ulong normalizedError = (ulong)(error >> (2 * shift));
                errors[candidate] = planeIndex == 2 ? errors[candidate] + normalizedError : normalizedError;
            }
        }
    }

    /// <summary>
    /// Copies the available neighborhood and marks frame-edge taps as unavailable.
    /// </summary>
    /// <typeparam name="TSample">The native component storage type.</typeparam>
    /// <typeparam name="TOperator">The closed component operations.</typeparam>
    /// <param name="plane">The bordered component plane.</param>
    /// <param name="x">The unit's plane column.</param>
    /// <param name="y">The unit's plane row.</param>
    /// <param name="width">The unit width.</param>
    /// <param name="height">The unit height.</param>
    /// <param name="planeWidth">The coded mode-grid width.</param>
    /// <param name="planeHeight">The coded mode-grid height.</param>
    /// <param name="input">The bordered filtering workspace.</param>
    private static void CopyUnit<TSample, TOperator>(
        Buffer2DRegion<TSample> plane,
        int x,
        int y,
        int width,
        int height,
        int planeWidth,
        int planeHeight,
        Span<ushort> input)
        where TSample : unmanaged
        where TOperator : struct, IEncodingOperator<TSample>
    {
        int left = x == 0 ? 0 : HorizontalBorder;
        int top = y == 0 ? 0 : VerticalBorder;
        int right = x + width == planeWidth ? 0 : HorizontalBorder;
        int bottom = y + height == planeHeight ? 0 : VerticalBorder;
        input.Fill(Av1CdefFilter.VeryLarge);
        int offset = ((plane.Bounds.Y + y - top) * plane.Stride) + plane.Bounds.X + x - left;
        ReadOnlySpan<TSample> storage = plane.Buffer.DangerousGetSingleSpan();
        TOperator.Copy(
            storage[offset..],
            plane.Stride,
            input[(((VerticalBorder - top) * SourceStride) + HorizontalBorder - left)..],
            SourceStride,
            left + width + right,
            top + height + bottom);
    }

    /// <summary>
    /// Finds paired luma directions once for all strength candidates and chroma planes.
    /// </summary>
    /// <param name="input">The bordered luma unit.</param>
    /// <param name="blocks">The non-skipped block coordinates.</param>
    /// <param name="directions">The destination directions.</param>
    /// <param name="variances">The destination direction variances.</param>
    /// <param name="shift">The number of sample bits above eight.</param>
    private static void FindDirections(ReadOnlySpan<ushort> input, ReadOnlySpan<ushort> blocks, Span<int> directions, Span<int> variances, int shift)
    {
        int index = 0;
        for (; index + 1 < blocks.Length; index += 2)
        {
            int first = ((VerticalBorder + ((blocks[index] >> 4) * 8)) * SourceStride) + HorizontalBorder + ((blocks[index] & 15) * 8);
            int second = ((VerticalBorder + ((blocks[index + 1] >> 4) * 8)) * SourceStride) + HorizontalBorder + ((blocks[index + 1] & 15) * 8);
            Av1CdefFilter.FindDirections(
                input,
                first,
                second,
                SourceStride,
                shift,
                out directions[index],
                out variances[index],
                out directions[index + 1],
                out variances[index + 1]);
        }

        if (index < blocks.Length)
        {
            int offset = ((VerticalBorder + ((blocks[index] >> 4) * 8)) * SourceStride) + HorizontalBorder + ((blocks[index] & 15) * 8);
            directions[index] = Av1CdefFilter.FindDirection(input, offset, SourceStride, shift, out variances[index]);
        }
    }

    /// <summary>
    /// Filters the listed blocks using shared luma direction state.
    /// </summary>
    /// <typeparam name="TSample">The native component storage type.</typeparam>
    /// <typeparam name="TOperator">The closed component operations.</typeparam>
    /// <param name="input">The bordered source unit.</param>
    /// <param name="output">The first unit destination sample and remaining storage.</param>
    /// <param name="stride">The destination stride.</param>
    /// <param name="blocks">The non-skipped block coordinates.</param>
    /// <param name="directions">The luma directions.</param>
    /// <param name="variances">The luma direction variances.</param>
    /// <param name="subX">The horizontal subsampling shift.</param>
    /// <param name="subY">The vertical subsampling shift.</param>
    /// <param name="strength">The packed strength.</param>
    /// <param name="damping">The frame damping.</param>
    /// <param name="shift">The number of sample bits above eight.</param>
    /// <param name="luma">Whether luma variance adjusts the primary strength.</param>
    private static void FilterUnit<TSample, TOperator>(
        ReadOnlySpan<ushort> input,
        Span<TSample> output,
        int stride,
        ReadOnlySpan<ushort> blocks,
        ReadOnlySpan<int> directions,
        ReadOnlySpan<int> variances,
        int subX,
        int subY,
        int strength,
        int damping,
        int shift,
        bool luma)
        where TSample : unmanaged
        where TOperator : struct, IEncodingOperator<TSample>
    {
        // Secondary code three represents strength four. Sample precision scales both strengths;
        // damping follows that scale, with chroma damping one level below luma.
        int primary = (strength >> 2) << shift;
        int secondary = strength & 3;
        secondary = (secondary + (secondary == 3 ? 1 : 0)) << shift;
        damping += shift - (luma ? 0 : 1);
        int width = 8 >> subX;
        int height = 8 >> subY;
        for (int index = 0; index < blocks.Length; index++)
        {
            int x = (blocks[index] & 15) * width;
            int y = (blocks[index] >> 4) * height;
            int adjustedPrimary = luma ? Av1CdefFilter.AdjustStrength(primary, variances[index]) : primary;
            int direction = primary == 0 ? 0 : Av1CdefFilter.ConvertDirection(directions[index], subX, subY);
            int offset = ((VerticalBorder + y) * SourceStride) + HorizontalBorder + x;
            TOperator.Filter(
                input,
                offset,
                output[((y * stride) + x)..],
                stride,
                adjustedPrimary,
                secondary,
                direction,
                damping,
                shift,
                width,
                height);
        }
    }
}
