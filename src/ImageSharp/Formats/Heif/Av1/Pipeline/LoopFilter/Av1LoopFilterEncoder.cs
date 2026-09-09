// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopFilter;

/// <summary>
/// Applies selected deblocking parameters to the encoder reconstruction.
/// </summary>
internal static class Av1LoopFilterEncoder
{
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

        Buffer2DRegion<TSample> samples = reconstruction.CodedView.GetPlane(plane);
        int origin = (samples.Bounds.Y * samples.Stride) + samples.Bounds.X;
        int subX = plane == Av1Plane.Y ? 0 : reconstruction.ChromaSubsamplingX;
        int subY = plane == Av1Plane.Y ? 0 : reconstruction.ChromaSubsamplingY;
        int rowsPerBand = 1 << (Av1Constants.MaxSuperBlockSizeLog2 - Av1Constants.ModeInfoSizeLog2);

        // The frame owner supplies one contiguous allocation. Retain its full bordered view so kernels may
        // access their edge neighborhoods without copying the plane or materializing decoder frame state.
        Span<TSample> storage = samples.Buffer.DangerousGetSingleSpan();
        for (int rowStart = 0; rowStart < header.ModeInfoRowCount; rowStart += rowsPerBand)
        {
            int rowEnd = Math.Min(rowStart + rowsPerBand, header.ModeInfoRowCount);
            Av1LoopFilterBase.FilterBand<TSample, Av1PictureControlSet, Av1EncoderBlockModeInfo, FrameOperator, TVerticalOperator, THorizontalOperator>(
                picture, header, plane, rowStart, rowEnd, subX, subY, storage, origin, samples.Stride, reconstruction.LumaBitDepth);
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
            skippedTransform = mode.Skip && mode.ReferenceFrame > Av1ReferenceFrameType.Intra;

            // The selected encoder transform size is uniform within each luma block. Chroma owns its
            // maximum plane transform independently of luma splits, while lossless segments always use 4x4.
            transformSize = state.Parent.FrameHeader.LosslessArray[mode.SegmentId]
                ? Av1TransformSize.Size4x4
                : plane == Av1Plane.Y ? mode.TransformSize : mode.BlockSize.GetMaxUvTransformSize(subX != 0, subY != 0);
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
