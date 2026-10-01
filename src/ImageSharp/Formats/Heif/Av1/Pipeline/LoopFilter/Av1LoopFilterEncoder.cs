// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopFilter;

/// <summary>
/// Selects the frame deblocking levels and applies them to the encoder reconstruction.
/// </summary>
/// <remarks>
/// The level selection ports libaom <c>av1_pick_filter_level</c>. The configurations this encoder
/// exposes never enable two-pass statistics, screen-content cyclic refresh, selective loop filter control,
/// or the SSE-based skips, so those branches of the reference have no counterpart here.
/// </remarks>
internal static class Av1LoopFilterEncoder
{
    /// <summary>
    /// The side of the square tiles <c>get_sse</c> measures a plane in.
    /// </summary>
    private const int ErrorTileSize = 16;

    /// <summary>
    /// Chooses the four frame deblocking levels for the completed, unfiltered reconstruction.
    /// </summary>
    /// <remarks>
    /// Ports the frame-level part of <c>loopfilter_frame</c> together with
    /// <c>av1_pick_filter_level</c>. The reconstruction is unchanged on return.
    /// </remarks>
    /// <typeparam name="TSample">The reconstructed sample storage type.</typeparam>
    /// <typeparam name="TVerticalOperator">The vertical sample accessor.</typeparam>
    /// <typeparam name="THorizontalOperator">The horizontal sample accessor.</typeparam>
    /// <param name="allocator">The allocator for the unfiltered plane copy a level search restores from.</param>
    /// <param name="picture">The completed frame decisions whose header receives the levels.</param>
    /// <param name="source">The source planes the filtered reconstruction is measured against.</param>
    /// <param name="reconstruction">The unfiltered reconstructed planes.</param>
    /// <param name="previousLevels">
    /// The luma vertical, luma horizontal, U, and V levels retained from the preceding frame; updated for the next frame.
    /// </param>
    public static void PickFilterLevel<TSample, TVerticalOperator, THorizontalOperator>(
        MemoryAllocator allocator,
        Av1PictureControlSet picture,
        Av1EncoderFrame<TSample> source,
        Av1EncoderFrame<TSample> reconstruction,
        Span<int> previousLevels)
        where TSample : unmanaged
        where TVerticalOperator : struct, Av1DeblockingFilter.IEdgeOperator<TSample>
        where THorizontalOperator : struct, Av1DeblockingFilter.IEdgeOperator<TSample>
    {
        ObuFrameHeader header = picture.Parent.FrameHeader;
        ObuLoopFilterParameters parameters = header.LoopFilterParameters;

        // An inter frame starts each search at the levels the preceding frame chose.
        // Read them before the reset below, which only affects what the next frame receives.
        Span<int> lastLevels = stackalloc int[4];
        if (!header.IsIntra)
        {
            previousLevels.CopyTo(lastLevels);
        }

        // set_postproc_filter_default_params clears the luma levels and their backups
        // before every frame; the chroma backups keep their earlier values. The header levels were
        // cleared when the frame was prepared.
        previousLevels[0] = 0;
        previousLevels[1] = 0;

        // Intra block copy frames skip loopfilter_frame altogether and coded lossless frames do not use the
        // loop filter.
        if (header.AllowIntraBlockCopy || header.CodedLossless)
        {
            return;
        }

        // All-intra usage and the image and SSIMULACRA 2 tunes use the configured sharpness, which adaptive sharpness
        // limits by the quantizer. Reference: the sharpness_level assignments of av1_pick_filter_level().
        Av1EncoderOptions options = picture.Parent.EncoderOptions;
        int sharpness = picture.Sequence.SequenceHeader.IsStillPicture || options.Tuning.IsImageTuning() ? options.Sharpness : 0;
        if (options.EnableAdaptiveSharpness)
        {
            int baseQIndex = header.QuantizationParameters.BaseQIndex;
            int maximumSharpness = baseQIndex <= 112 ? 7 : baseQIndex <= 160 ? 1 : 0;
            sharpness = Math.Min(sharpness, maximumSharpness);
        }

        parameters.SharpnessLevel = sharpness;

        Av1LoopFilterPickMethod method = picture.Parent.SpeedSettings.LoopFilterPickMethod;
        if (method == Av1LoopFilterPickMethod.FromQuantizer)
        {
            int level = EstimateLevelFromQuantizer(header, picture.Sequence.SequenceHeader.ColorConfig.BitDepth);
            parameters.FilterLevel[0] = level;
            parameters.FilterLevel[1] = level;
            parameters.FilterLevelU = level;
            parameters.FilterLevelV = level;
            return;
        }

        // One pooled copy, sized for the largest plane, holds the unfiltered samples each trial restores
        // (the reference's last_frame_uf buffer).
        Av1PlaneRegion<TSample> lumaPlane = reconstruction.CodedView.GetPlane(Av1Plane.Y);
        using IMemoryOwner<TSample> backupOwner = allocator.Allocate<TSample>(lumaPlane.Width * lumaPlane.Height);
        Span<TSample> backup = backupOwner.Memory.Span;

        // Search one level for both luma directions, then, unless dual levels are disabled, refine the
        // vertical and horizontal levels separately. Each of those searches keeps the other direction at
        // its latest level.
        int bothDirections = SearchFilterLevel<TSample, TVerticalOperator, THorizontalOperator>(
            picture, source, reconstruction, backup, lastLevels, Av1Plane.Y, 2);

        parameters.FilterLevel[0] = bothDirections;
        parameters.FilterLevel[1] = bothDirections;
        if (method != Av1LoopFilterPickMethod.FullImageNonDual)
        {
            parameters.FilterLevel[0] = SearchFilterLevel<TSample, TVerticalOperator, THorizontalOperator>(
                picture, source, reconstruction, backup, lastLevels, Av1Plane.Y, 0);

            parameters.FilterLevel[1] = SearchFilterLevel<TSample, TVerticalOperator, THorizontalOperator>(
                picture, source, reconstruction, backup, lastLevels, Av1Plane.Y, 1);
        }

        if (picture.Sequence.SequenceHeader.ColorConfig.PlaneCount > 1)
        {
            parameters.FilterLevelU = SearchFilterLevel<TSample, TVerticalOperator, THorizontalOperator>(
                picture, source, reconstruction, backup, lastLevels, Av1Plane.U, 0);

            parameters.FilterLevelV = SearchFilterLevel<TSample, TVerticalOperator, THorizontalOperator>(
                picture, source, reconstruction, backup, lastLevels, Av1Plane.V, 0);
        }

        // The searched levels seed the next frame.
        previousLevels[0] = parameters.FilterLevel[0];
        previousLevels[1] = parameters.FilterLevel[1];
        previousLevels[2] = parameters.FilterLevelU;
        previousLevels[3] = parameters.FilterLevelV;
    }

    /// <summary>
    /// Filters the completed reconstruction before it becomes a prediction reference.
    /// </summary>
    /// <typeparam name="TSample">The reconstructed sample storage type.</typeparam>
    /// <typeparam name="TVerticalOperator">The vertical sample accessor.</typeparam>
    /// <typeparam name="THorizontalOperator">The horizontal sample accessor.</typeparam>
    /// <param name="picture">The completed frame decisions.</param>
    /// <param name="reconstruction">The writable reconstructed component planes.</param>
    public static void ApplyFrame<TSample, TVerticalOperator, THorizontalOperator>(
        Av1PictureControlSet picture,
        Av1EncoderFrame<TSample> reconstruction)
        where TSample : unmanaged
        where TVerticalOperator : struct, Av1DeblockingFilter.IEdgeOperator<TSample>
        where THorizontalOperator : struct, Av1DeblockingFilter.IEdgeOperator<TSample>
    {
        ObuFrameHeader header = picture.Parent.FrameHeader;
        ObuLoopFilterParameters parameters = header.LoopFilterParameters;
        if (header.CodedLossless || header.AllowIntraBlockCopy || (parameters.FilterLevel[0] == 0 && parameters.FilterLevel[1] == 0))
        {
            return;
        }

        int planeCount = picture.Sequence.SequenceHeader.ColorConfig.PlaneCount;
        for (int planeIndex = 0; planeIndex < planeCount; planeIndex++)
        {
            ApplyPlane<TSample, TVerticalOperator, THorizontalOperator>(picture, reconstruction, (Av1Plane)planeIndex);
        }
    }

    /// <summary>
    /// Filters one component using the current frame levels.
    /// </summary>
    /// <typeparam name="TSample">The reconstructed sample storage type.</typeparam>
    /// <typeparam name="TVerticalOperator">The vertical sample accessor.</typeparam>
    /// <typeparam name="THorizontalOperator">The horizontal sample accessor.</typeparam>
    /// <param name="picture">The completed frame decisions.</param>
    /// <param name="reconstruction">The writable reconstructed component planes.</param>
    /// <param name="plane">The component to filter.</param>
    public static void ApplyPlane<TSample, TVerticalOperator, THorizontalOperator>(
        Av1PictureControlSet picture,
        Av1EncoderFrame<TSample> reconstruction,
        Av1Plane plane)
        where TSample : unmanaged
        where TVerticalOperator : struct, Av1DeblockingFilter.IEdgeOperator<TSample>
        where THorizontalOperator : struct, Av1DeblockingFilter.IEdgeOperator<TSample>
    {
        ObuFrameHeader header = picture.Parent.FrameHeader;
        ObuLoopFilterParameters parameters = header.LoopFilterParameters;
        int level = plane switch
        {
            Av1Plane.U => parameters.FilterLevelU,
            Av1Plane.V => parameters.FilterLevelV,
            _ => Math.Max(parameters.FilterLevel[0], parameters.FilterLevel[1])
        };

        if (level == 0)
        {
            return;
        }

        Av1PlaneRegion<TSample> samples = reconstruction.CodedView.GetPlane(plane);
        int origin = (samples.Bounds.Y * samples.Stride) + samples.Bounds.X;
        int subX = plane == Av1Plane.Y ? 0 : reconstruction.ChromaSubsamplingX;
        int subY = plane == Av1Plane.Y ? 0 : reconstruction.ChromaSubsamplingY;
        int rowsPerBand = 1 << (Av1Constants.MaxSuperBlockSizeLog2 - Av1Constants.ModeInfoSizeLog2);

        // Each plane is one contiguous allocation. Retain its full bordered view so kernels may
        // access their edge neighborhoods without copying the plane or materializing decoder frame state.
        Span<TSample> storage = samples.Samples;
        for (int rowStart = 0; rowStart < header.ModeInfoRowCount; rowStart += rowsPerBand)
        {
            int rowEnd = Math.Min(rowStart + rowsPerBand, header.ModeInfoRowCount);
            Av1LoopFilterBase.FilterBand<TSample, Av1PictureControlSet, Av1EncoderBlockModeInfo, FrameOperator, TVerticalOperator, THorizontalOperator>(
                picture, header, plane, rowStart, rowEnd, subX, subY, storage, origin, samples.Stride, reconstruction.LumaBitDepth);
        }
    }

    /// <summary>
    /// Estimates one deblocking level for every plane from the base quantizer, as the
    /// <c>LPF_PICK_FROM_Q</c> branch of <c>av1_pick_filter_level</c> does.
    /// </summary>
    /// <param name="header">The frame header holding the base quantizer index and frame type.</param>
    /// <param name="bitDepth">The sequence sample depth.</param>
    /// <returns>The estimated level, clamped to the valid range.</returns>
    private static int EstimateLevelFromQuantizer(ObuFrameHeader header, Av1BitDepth bitDepth)
    {
        int q = Av1QuantizationLookup.GetAcQuant(header.QuantizationParameters.BaseQIndex, 0, bitDepth);
        bool keyFrame = header.FrameType == ObuFrameType.KeyFrame;

        // Linear fits of the searched level in 18-, 20-, and 22-bit fixed point. The non-key 8-bit
        // multiplier is the boosted value, because every AC quantizer exceeds the zero threshold.
        int guess = bitDepth switch
        {
            Av1BitDepth.EightBit => keyFrame
                ? Av1Math.RoundPowerOf2((q * 17563) - 421574, 18)
                : Av1Math.RoundPowerOf2((q * 12034) + 650707, 18),
            Av1BitDepth.TenBit => Av1Math.RoundPowerOf2((q * 20723) + 4060632, 20),
            _ => Av1Math.RoundPowerOf2((q * 20723) + 16242526, 22)
        };

        if (bitDepth != Av1BitDepth.EightBit && keyFrame)
        {
            guess -= 4;
        }

        return Math.Clamp(guess, 0, Av1Constants.MaxLoopFilter);
    }

    /// <summary>
    /// Finds the level that minimizes the filtered error of one plane and direction by a biased step search,
    /// as <c>search_filter_level</c> does.
    /// </summary>
    /// <typeparam name="TSample">The reconstructed sample storage type.</typeparam>
    /// <typeparam name="TVerticalOperator">The vertical sample accessor.</typeparam>
    /// <typeparam name="THorizontalOperator">The horizontal sample accessor.</typeparam>
    /// <param name="picture">The completed frame decisions whose header holds the trial levels.</param>
    /// <param name="source">The source planes.</param>
    /// <param name="reconstruction">The unfiltered reconstructed planes.</param>
    /// <param name="backup">The storage for the unfiltered plane.</param>
    /// <param name="lastLevels">The luma vertical, luma horizontal, U, and V levels the search starts from.</param>
    /// <param name="plane">The plane to search.</param>
    /// <param name="direction">
    /// For luma, 0 searches the vertical-edge level, 1 the horizontal-edge level, and 2 one level for both.
    /// </param>
    /// <returns>The selected level.</returns>
    private static int SearchFilterLevel<TSample, TVerticalOperator, THorizontalOperator>(
        Av1PictureControlSet picture,
        Av1EncoderFrame<TSample> source,
        Av1EncoderFrame<TSample> reconstruction,
        Span<TSample> backup,
        ReadOnlySpan<int> lastLevels,
        Av1Plane plane,
        int direction)
        where TSample : unmanaged
        where TVerticalOperator : struct, Av1DeblockingFilter.IEdgeOperator<TSample>
        where THorizontalOperator : struct, Av1DeblockingFilter.IEdgeOperator<TSample>
    {
        const int minimumLevel = 0;
        const int maximumLevel = Av1Constants.MaxLoopFilter;
        int start = plane switch
        {
            Av1Plane.Y => direction == 2 ? (lastLevels[0] + lastLevels[1] + 1) >> 1 : lastLevels[direction],
            Av1Plane.U => lastLevels[2],
            _ => lastLevels[3]
        };

        int middle = Math.Clamp(start, minimumLevel, maximumLevel);
        int step = middle < 16 ? 4 : middle / 4;

        // The measured error of each level; -1 marks a level not yet tried.
        Span<long> errors = stackalloc long[maximumLevel + 1];
        errors.Fill(-1);

        Av1PlaneRegion<TSample> coded = reconstruction.CodedView.GetPlane(plane);
        backup = backup[..(coded.Width * coded.Height)];
        CopyPlane(coded, backup);

        long bestError = TryFilterFrame<TSample, TVerticalOperator, THorizontalOperator>(
            picture, source, reconstruction, backup, middle, plane, direction);

        int best = middle;
        errors[middle] = bestError;

        // The coarse search ends at a step of two; otherwise the step halves until it reaches zero.
        // Reference: min_filter_step_thesh in search_filter_level().
        int minimumStep = picture.Parent.SpeedSettings.UseCoarseFilterLevelSearch ? 2 : 0;
        int searchDirection = 0;
        while (step > minimumStep)
        {
            int high = Math.Min(middle + step, maximumLevel);
            int low = Math.Max(middle - step, minimumLevel);

            // Bias against raising the level in favor of lowering it. The bias is halved when transforms
            // larger than 4x4 are allowed.
            long bias = (bestError >> (15 - (middle / 8))) * step;
            if (picture.Parent.FrameHeader.TransformMode != Av1TransformMode.Only4x4)
            {
                bias >>= 1;
            }

            if (searchDirection <= 0 && low != middle)
            {
                if (errors[low] < 0)
                {
                    errors[low] = TryFilterFrame<TSample, TVerticalOperator, THorizontalOperator>(
                        picture, source, reconstruction, backup, low, plane, direction);
                }

                // A lower level within the bias of the best is preferred. The best error only moves when the
                // lower level is actually better.
                if (errors[low] < bestError + bias)
                {
                    if (errors[low] < bestError)
                    {
                        bestError = errors[low];
                    }

                    best = low;
                }
            }

            if (searchDirection >= 0 && high != middle)
            {
                if (errors[high] < 0)
                {
                    errors[high] = TryFilterFrame<TSample, TVerticalOperator, THorizontalOperator>(
                        picture, source, reconstruction, backup, high, plane, direction);
                }

                // A higher level must improve on the best by more than the bias.
                if (errors[high] < bestError - bias)
                {
                    bestError = errors[high];
                    best = high;
                }
            }

            // Halve the step when the middle level stays best. Otherwise continue from the new best, in the
            // direction it moved.
            if (best == middle)
            {
                step /= 2;
                searchDirection = 0;
            }
            else
            {
                searchDirection = best < middle ? -1 : 1;
                middle = best;
            }
        }

        return best;
    }

    /// <summary>
    /// Filters one plane at a trial level, measures it against the source, and restores the unfiltered plane,
    /// as <c>try_filter_frame</c> does.
    /// </summary>
    /// <typeparam name="TSample">The reconstructed sample storage type.</typeparam>
    /// <typeparam name="TVerticalOperator">The vertical sample accessor.</typeparam>
    /// <typeparam name="THorizontalOperator">The horizontal sample accessor.</typeparam>
    /// <param name="picture">The completed frame decisions whose header holds the trial levels.</param>
    /// <param name="source">The source planes.</param>
    /// <param name="reconstruction">The reconstructed planes, unfiltered on entry and on return.</param>
    /// <param name="backup">The unfiltered samples of the plane.</param>
    /// <param name="level">The trial level.</param>
    /// <param name="plane">The plane to filter.</param>
    /// <param name="direction">
    /// For luma, 0 sets only the vertical-edge level, 1 only the horizontal-edge level, and 2 both.
    /// </param>
    /// <returns>The squared error of the filtered visible plane.</returns>
    private static long TryFilterFrame<TSample, TVerticalOperator, THorizontalOperator>(
        Av1PictureControlSet picture,
        Av1EncoderFrame<TSample> source,
        Av1EncoderFrame<TSample> reconstruction,
        ReadOnlySpan<TSample> backup,
        int level,
        Av1Plane plane,
        int direction)
        where TSample : unmanaged
        where TVerticalOperator : struct, Av1DeblockingFilter.IEdgeOperator<TSample>
        where THorizontalOperator : struct, Av1DeblockingFilter.IEdgeOperator<TSample>
    {
        // A single-direction luma trial keeps the current level of the other direction.
        ObuLoopFilterParameters parameters = picture.Parent.FrameHeader.LoopFilterParameters;
        switch (plane)
        {
            case Av1Plane.Y:
                if (direction != 1)
                {
                    parameters.FilterLevel[0] = level;
                }

                if (direction != 0)
                {
                    parameters.FilterLevel[1] = level;
                }

                break;
            case Av1Plane.U:
                parameters.FilterLevelU = level;
                break;
            default:
                parameters.FilterLevelV = level;
                break;
        }

        ApplyPlane<TSample, TVerticalOperator, THorizontalOperator>(picture, reconstruction, plane);
        long error = GetSumSquaredError(source.View.GetPlane(plane), reconstruction.View.GetPlane(plane));
        RestorePlane(backup, reconstruction.CodedView.GetPlane(plane));
        return error;
    }

    /// <summary>
    /// Sums the squared differences of two visible planes in 16 by 16 tiles plus the right and bottom
    /// remainders, as <c>get_sse</c> and <c>highbd_get_sse</c> do.
    /// </summary>
    /// <typeparam name="TSample">The sample storage type.</typeparam>
    /// <param name="first">The first plane.</param>
    /// <param name="second">The second plane, of the same size.</param>
    /// <returns>The exact sum of squared sample differences.</returns>
    private static long GetSumSquaredError<TSample>(Av1PlaneRegion<TSample> first, Av1PlaneRegion<TSample> second)
        where TSample : unmanaged
    {
        int width = first.Width;
        int height = first.Height;
        int remainderWidth = width % ErrorTileSize;
        int remainderHeight = height % ErrorTileSize;
        long total = 0;
        if (remainderWidth > 0)
        {
            total += GetSumSquaredError(first, second, width - remainderWidth, 0, remainderWidth, height);
        }

        if (remainderHeight > 0)
        {
            total += GetSumSquaredError(first, second, 0, height - remainderHeight, width - remainderWidth, remainderHeight);
        }

        for (int y = 0; y + ErrorTileSize <= height; y += ErrorTileSize)
        {
            for (int x = 0; x + ErrorTileSize <= width; x += ErrorTileSize)
            {
                total += GetSumSquaredError(first, second, x, y, ErrorTileSize, ErrorTileSize);
            }
        }

        return total;
    }

    /// <summary>
    /// Sums the squared differences of one rectangle of two planes with the shared vectorized residual operation.
    /// </summary>
    /// <typeparam name="TSample">The sample storage type.</typeparam>
    /// <param name="first">The first plane.</param>
    /// <param name="second">The second plane.</param>
    /// <param name="x">The left column of the rectangle within the planes.</param>
    /// <param name="y">The top row of the rectangle within the planes.</param>
    /// <param name="width">The rectangle width.</param>
    /// <param name="height">The rectangle height.</param>
    /// <returns>The exact sum of squared sample differences.</returns>
    private static long GetSumSquaredError<TSample>(
        Av1PlaneRegion<TSample> first,
        Av1PlaneRegion<TSample> second,
        int x,
        int y,
        int width,
        int height)
        where TSample : unmanaged
    {
        int firstOffset = ((first.Bounds.Y + y) * first.Stride) + first.Bounds.X + x;
        int secondOffset = ((second.Bounds.Y + y) * second.Stride) + second.Bounds.X + x;
        ReadOnlySpan<TSample> firstSamples = first.Samples[firstOffset..];
        ReadOnlySpan<TSample> secondSamples = second.Samples[secondOffset..];

        // The sample type is fixed by the closed generic frame path, so this folds to one direct call.
        if (typeof(TSample) == typeof(byte))
        {
            return Av1ResidualBuilder.SumSquaredError(
                MemoryMarshal.Cast<TSample, byte>(firstSamples),
                first.Stride,
                MemoryMarshal.Cast<TSample, byte>(secondSamples),
                second.Stride,
                width,
                height);
        }

        return Av1ResidualBuilder.SumSquaredError(
            MemoryMarshal.Cast<TSample, ushort>(firstSamples),
            first.Stride,
            MemoryMarshal.Cast<TSample, ushort>(secondSamples),
            second.Stride,
            width,
            height);
    }

    /// <summary>
    /// Copies the coded area of a plane into contiguous rows.
    /// </summary>
    /// <typeparam name="TSample">The sample storage type.</typeparam>
    /// <param name="plane">The coded plane.</param>
    /// <param name="destination">The contiguous destination rows.</param>
    private static void CopyPlane<TSample>(Av1PlaneRegion<TSample> plane, Span<TSample> destination)
        where TSample : unmanaged
    {
        for (int y = 0; y < plane.Height; y++)
        {
            plane.GetRowSpan(y).CopyTo(destination.Slice(y * plane.Width, plane.Width));
        }
    }

    /// <summary>
    /// Restores the coded area of a plane from contiguous rows.
    /// </summary>
    /// <typeparam name="TSample">The sample storage type.</typeparam>
    /// <param name="source">The contiguous source rows.</param>
    /// <param name="plane">The coded plane.</param>
    private static void RestorePlane<TSample>(ReadOnlySpan<TSample> source, Av1PlaneRegion<TSample> plane)
        where TSample : unmanaged
    {
        for (int y = 0; y < plane.Height; y++)
        {
            source.Slice(y * plane.Width, plane.Width).CopyTo(plane.GetRowSpan(y));
        }
    }

    /// <summary>
    /// Reads deblocking parameters from the retained encoder mode grid.
    /// </summary>
    private readonly struct FrameOperator : Av1LoopFilterBase.IFrameOperator<Av1PictureControlSet, Av1EncoderBlockModeInfo>
    {
        /// <inheritdoc/>
        public static void GetParameters(
            Av1PictureControlSet state,
            Point position,
            Av1Plane plane,
            int pass,
            int subX,
            int subY,
            out int blockIndex,
            out bool skippedTransform,
            out Av1TransformSize transformSize,
            out Av1EncoderBlockModeInfo mode)
        {
            blockIndex = state.ModeInfoGrid.Span[(position.Y * state.ModeInfoStride) + position.X];
            mode = state.ModeInfoAllocation.Span[blockIndex].Block;
            skippedTransform = mode.Skip && (mode.ReferenceFrame > Av1ReferenceFrameType.Intra || mode.UseIntraBlockCopy);

            // Chroma uses its own maximum plane transform. Non-skipped inter luma reads the
            // selected tree cell so filter lengths follow the transform boundary at this position.
            transformSize = state.Parent.FrameHeader.LosslessArray[mode.SegmentId]
                ? Av1TransformSize.Size4x4
                : plane == Av1Plane.Y ? mode.TransformSize : mode.BlockSize.GetMaxUvTransformSize(subX != 0, subY != 0);

            if (plane == Av1Plane.Y && (mode.ReferenceFrame > Av1ReferenceFrameType.Intra || mode.UseIntraBlockCopy) &&
                !mode.Skip && !state.Parent.FrameHeader.LosslessArray[mode.SegmentId])
            {
                int row = position.Y & (mode.BlockSize.Get4x4HighCount() - 1);
                int column = position.X & (mode.BlockSize.Get4x4WideCount() - 1);
                transformSize = mode.InterTransformSizes[mode.GetInterTransformSizeIndex(row, column)];
            }
        }

        /// <inheritdoc/>
        public static int GetFilterLevel(Av1PictureControlSet state, ref Av1EncoderBlockModeInfo mode, Point position, Av1Plane plane, int pass)
        {
            ObuFrameHeader header = state.Parent.FrameHeader;
            ObuLoopFilterParameters parameters = header.LoopFilterParameters;
            int index = plane == Av1Plane.Y ? pass : (int)plane + 1;
            int level = index switch
            {
                0 => parameters.FilterLevel[0],
                1 => parameters.FilterLevel[1],
                2 => parameters.FilterLevelU,
                _ => parameters.FilterLevelV
            };

            // The encoder does not emit superblock filter deltas. Frame, segment, reference, and mode
            // adjustments still follow the same clipping and scaling as the decoder.
            return Av1LoopFilterBase.GetFilterLevel(header, index, level, 0, mode.SegmentId, mode.ReferenceFrame, mode.Mode);
        }
    }
}
