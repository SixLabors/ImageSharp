// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopFilter;

/// <summary>
/// Traverses reconstructed transform boundaries for in-place deblocking.
/// </summary>
internal static class Av1LoopFilterBase
{
    /// <summary>
    /// Supplies the selected block and transform state at a frame position.
    /// </summary>
    /// <typeparam name="TState">The owning encoder or decoder state.</typeparam>
    /// <typeparam name="TMode">The retained block-mode type.</typeparam>
    public interface IFrameOperator<TState, TMode>
        where TMode : struct
    {
        /// <summary>
        /// Resolves the block and filter parameters covering one plane position.
        /// </summary>
        /// <param name="state">The owning frame state.</param>
        /// <param name="position">The position in luma 4x4 units, adjusted for chroma ownership.</param>
        /// <param name="plane">The component plane.</param>
        /// <param name="pass">Zero for vertical boundaries; one for horizontal boundaries.</param>
        /// <param name="subX">The horizontal subsampling shift.</param>
        /// <param name="subY">The vertical subsampling shift.</param>
        /// <param name="blockIndex">The storage index identifying the owning prediction block.</param>
        /// <param name="skippedTransform">Whether an inter block omits its residual.</param>
        /// <param name="transformSize">The transform covering the plane position.</param>
        /// <param name="mode">The mode state retained for subsequent level derivation.</param>
        public static abstract void GetParameters(
            TState state,
            Point position,
            Av1Plane plane,
            int pass,
            int subX,
            int subY,
            out int blockIndex,
            out bool skippedTransform,
            out Av1TransformSize transformSize,
            out TMode mode);

        /// <summary>
        /// Derives the level for a block whose boundary requires filtering.
        /// </summary>
        /// <param name="state">The owning frame state.</param>
        /// <param name="mode">The resolved block mode, read without another grid lookup or structure copy.</param>
        /// <param name="position">The block position in luma 4x4 units.</param>
        /// <param name="plane">The component plane.</param>
        /// <param name="pass">Zero for vertical boundaries; one for horizontal boundaries.</param>
        /// <returns>The adjusted level in the zero-to-63 domain.</returns>
        public static abstract int GetFilterLevel(TState state, ref TMode mode, Point position, Av1Plane plane, int pass);
    }

    /// <summary>
    /// Filters one plane band in vertical-then-horizontal boundary order.
    /// </summary>
    /// <typeparam name="TSample">The reconstructed sample storage type.</typeparam>
    /// <typeparam name="TState">The encoder or decoder state type.</typeparam>
    /// <typeparam name="TMode">The retained block-mode type.</typeparam>
    /// <typeparam name="TFrameOperator">The selected block-state accessor.</typeparam>
    /// <typeparam name="TVerticalOperator">The vertical sample accessor.</typeparam>
    /// <typeparam name="THorizontalOperator">The horizontal sample accessor.</typeparam>
    /// <param name="state">The owning frame state.</param>
    /// <param name="header">The decoded or selected frame header.</param>
    /// <param name="plane">The component plane.</param>
    /// <param name="rowStart">The inclusive band origin in luma 4x4 units.</param>
    /// <param name="rowEnd">The exclusive band limit in luma 4x4 units.</param>
    /// <param name="subX">The horizontal subsampling shift.</param>
    /// <param name="subY">The vertical subsampling shift.</param>
    /// <param name="samples">The plane storage including the required edge neighborhoods.</param>
    /// <param name="origin">The offset of the top-left coded sample within the storage.</param>
    /// <param name="stride">The plane stride in samples.</param>
    /// <param name="bitDepth">The component precision.</param>
    public static void FilterBand<TSample, TState, TMode, TFrameOperator, TVerticalOperator, THorizontalOperator>(
        TState state,
        ObuFrameHeader header,
        Av1Plane plane,
        int rowStart,
        int rowEnd,
        int subX,
        int subY,
        Span<TSample> samples,
        int origin,
        int stride,
        int bitDepth)
        where TSample : unmanaged
        where TMode : struct
        where TFrameOperator : struct, IFrameOperator<TState, TMode>
        where TVerticalOperator : struct, Av1DeblockingFilter.IEdgeOperator<TSample>
        where THorizontalOperator : struct, Av1DeblockingFilter.IEdgeOperator<TSample>
    {
        int rowStep = 1 << subY;
        int columnStep = 1 << subX;

        // Chroma remains in luma-grid coordinates. Each step still covers four samples in its own plane.
        // Finish vertical edges across each row before starting the horizontal traversal of this band.
        for (int row = rowStart; row < rowEnd; row += rowStep)
        {
            for (int column = 0; column < header.ModeInfoColumnCount; column += columnStep)
            {
                FilterEdge<TSample, TState, TMode, TFrameOperator, TVerticalOperator>(
                    state, header, plane, 0, row, column, subX, subY, samples, origin, stride, bitDepth);
            }
        }

        // Visit horizontal edges down each column: adjacent filters can read samples modified by earlier edges.
        for (int column = 0; column < header.ModeInfoColumnCount; column += columnStep)
        {
            for (int row = rowStart; row < rowEnd; row += rowStep)
            {
                FilterEdge<TSample, TState, TMode, TFrameOperator, THorizontalOperator>(
                    state, header, plane, 1, row, column, subX, subY, samples, origin, stride, bitDepth);
            }
        }
    }

    /// <summary>
    /// Combines the frame, superblock, segment, reference, and mode adjustments for one boundary.
    /// </summary>
    /// <param name="header">The frame's filter and segmentation state.</param>
    /// <param name="filterIndex">The luma-direction or chroma-component index.</param>
    /// <param name="baseLevel">The selected frame level for that component.</param>
    /// <param name="delta">The superblock adjustment for that component.</param>
    /// <param name="segmentId">The owning block's segment.</param>
    /// <param name="referenceFrame">The primary prediction reference.</param>
    /// <param name="mode">The luma prediction mode.</param>
    /// <returns>The adjusted level in the zero-to-63 domain.</returns>
    public static int GetFilterLevel(
        ObuFrameHeader header,
        int filterIndex,
        int baseLevel,
        int delta,
        int segmentId,
        Av1ReferenceFrameType referenceFrame,
        Av1PredictionMode mode)
    {
        ObuLoopFilterParameters parameters = header.LoopFilterParameters;
        int level = Av1Math.Clip3(0, Av1Constants.MaxLoopFilter, baseLevel + delta);
        ObuSegmentationLevelFeature feature = (ObuSegmentationLevelFeature)((int)ObuSegmentationLevelFeature.AlternativeLoopFilterYVertical + filterIndex);
        ObuSegmentationParameters segmentation = header.SegmentationParameters;

        if (segmentation.IsFeatureActive(segmentId, feature))
        {
            level = Av1Math.Clip3(
                0,
                Av1Constants.MaxLoopFilter,
                level + segmentation.GetFeatureData(segmentId, (int)feature));
        }

        if (parameters.ReferenceDeltaModeEnabled)
        {
            int referenceScale = 1 << (level >> 5);
            level += parameters.ReferenceDeltas[(int)referenceFrame] * referenceScale;

            if (referenceFrame > Av1ReferenceFrameType.Intra)
            {
                // Every inter mode except the two global-motion modes belongs to the second mode-delta class.
                int modeDeltaIndex = mode is Av1PredictionMode.GlobalMotionVector or
                    Av1PredictionMode.GlobalGlobalMotionVector ? 0 : 1;

                level += parameters.ModeDeltas[modeDeltaIndex] * referenceScale;
            }

            // Reference and mode adjustments use the same scale and may cancel beyond either limit.
            // Clipping the intermediate reference sum would discard part of that cancellation.
            level = Av1Math.Clip3(0, Av1Constants.MaxLoopFilter, level);
        }

        return level;
    }

    /// <summary>
    /// Derives and applies the kernel at one transform boundary.
    /// </summary>
    /// <typeparam name="TSample">The reconstructed sample storage type.</typeparam>
    /// <typeparam name="TState">The encoder or decoder state type.</typeparam>
    /// <typeparam name="TMode">The retained block-mode type.</typeparam>
    /// <typeparam name="TFrameOperator">The selected block-state accessor.</typeparam>
    /// <typeparam name="TEdgeOperator">The oriented sample accessor.</typeparam>
    /// <param name="state">The owning frame state.</param>
    /// <param name="header">The decoded or selected frame header.</param>
    /// <param name="plane">The component plane.</param>
    /// <param name="pass">Zero for vertical boundaries; one for horizontal boundaries.</param>
    /// <param name="row">The boundary row in luma 4x4 units.</param>
    /// <param name="column">The boundary column in luma 4x4 units.</param>
    /// <param name="subX">The horizontal subsampling shift.</param>
    /// <param name="subY">The vertical subsampling shift.</param>
    /// <param name="samples">The plane storage including the required edge neighborhoods.</param>
    /// <param name="origin">The offset of the top-left coded sample.</param>
    /// <param name="stride">The plane stride in samples.</param>
    /// <param name="bitDepth">The component precision.</param>
    private static void FilterEdge<TSample, TState, TMode, TFrameOperator, TEdgeOperator>(
        TState state,
        ObuFrameHeader header,
        Av1Plane plane,
        int pass,
        int row,
        int column,
        int subX,
        int subY,
        Span<TSample> samples,
        int origin,
        int stride,
        int bitDepth)
        where TSample : unmanaged
        where TMode : struct
        where TFrameOperator : struct, IFrameOperator<TState, TMode>
        where TEdgeOperator : struct, Av1DeblockingFilter.IEdgeOperator<TSample>
    {
        int x = column << Av1Constants.ModeInfoSizeLog2;
        int y = row << Av1Constants.ModeInfoSizeLog2;
        bool verticalBoundary = pass == 0;
        if (x >= header.FrameSize.FrameWidth || y >= header.FrameSize.FrameHeight || (verticalBoundary ? x == 0 : y == 0))
        {
            return;
        }

        // Subsampled chroma belongs to the bottom/right luma unit within its 8-sample footprint.
        Point position = new(column | subX, row | subY);
        Point previous = new(position.X - (verticalBoundary ? 1 << subX : 0), position.Y - (verticalBoundary ? 0 : 1 << subY));
        TFrameOperator.GetParameters(
            state, position, plane, pass, subX, subY, out int blockIndex, out bool skipped, out Av1TransformSize transform, out TMode mode);

        TFrameOperator.GetParameters(
            state,
            previous,
            plane,
            pass,
            subX,
            subY,
            out int previousIndex,
            out bool previousSkipped,
            out Av1TransformSize previousTransform,
            out TMode previousMode);

        int planeX = x >> subX;
        int planeY = y >> subY;
        bool isTransformEdge = verticalBoundary ? planeX % transform.GetWidth() == 0 : planeY % transform.GetHeight() == 0;

        // Residual-free intra predictions still need deblocking. Only skipped inter transforms suppress
        // internal boundaries; two different prediction blocks retain their common boundary.
        if (!isTransformEdge || (blockIndex == previousIndex && skipped && previousSkipped))
        {
            return;
        }

        int level = TFrameOperator.GetFilterLevel(state, ref mode, position, plane, pass);
        int filterLevel = level != 0 ? level : TFrameOperator.GetFilterLevel(state, ref previousMode, previous, plane, pass);
        if (filterLevel == 0)
        {
            return;
        }

        int size = verticalBoundary
            ? Math.Min(transform.GetWidth(), previousTransform.GetWidth())
            : Math.Min(transform.GetHeight(), previousTransform.GetHeight());

        int kernelLength = plane == Av1Plane.Y ? size == 4 ? 4 : size == 8 ? 8 : 14 : size == 4 ? 4 : 6;
        int sharpness = header.LoopFilterParameters.SharpnessLevel;
        int shift = sharpness > 4 ? 2 : sharpness > 0 ? 1 : 0;
        int limit = sharpness > 0 ? Av1Math.Clip3(1, 9 - sharpness, filterLevel >> shift) : Math.Max(1, filterLevel);
        int boundaryLimit = (2 * (filterLevel + 2)) + limit;

        // Thresholds stay in the eight-bit domain; the sample operator and kernel handle storage and precision.
        // The explicit origin accommodates bordered encoder planes and the decoder's preceding-row view.
        Av1DeblockingFilter.Filter<TSample, TEdgeOperator>(
            samples, origin + (planeY * stride) + planeX, stride, kernelLength, limit, boundaryLimit, filterLevel >> 4, bitDepth);
    }
}
