// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Finds the motion modes an encoded inter block may signal from its coded neighbors.
/// </summary>
internal static class Av1EncoderMotionVariation
{
    /// <summary>
    /// The largest number of warped-motion samples. Reference: LEAST_SQUARES_SAMPLES_MAX.
    /// </summary>
    private const int MaximumSampleCount = 8;

    /// <summary>
    /// The widest neighbor step, in 4x4 units, of the overlappable neighbor scans. Reference: mi_size_wide[BLOCK_64X64].
    /// </summary>
    private const int MaximumNeighborStep = 16;

    /// <summary>
    /// Returns the last motion mode the block may signal. Reference: motion_mode_allowed(), with
    /// overlappable_neighbors from av1_count_overlappable_neighbors() and num_proj_ref from av1_findSamples().
    /// </summary>
    /// <param name="picture">The frame decisions, with every preceding block final.</param>
    /// <param name="macroBlock">The neighbor availability of the block.</param>
    /// <param name="position">The block origin in 4x4 units.</param>
    /// <param name="mode">The block decisions.</param>
    /// <returns>The last allowed motion mode.</returns>
    public static Av1MotionMode GetLastAllowedMotionMode(
        Av1PictureControlSet picture,
        Av1MacroBlockD macroBlock,
        Point position,
        in Av1EncoderBlockModeInfo mode)
    {
        ObuFrameHeader frameHeader = picture.Parent.FrameHeader;
        if (!frameHeader.IsMotionModeSwitchable ||
            mode.ReferenceFrame <= Av1ReferenceFrameType.Intra ||
            mode.SecondaryReferenceFrame != Av1ReferenceFrameType.None ||
            mode.SkipMode ||
            !IsMotionVariationAllowed(mode.BlockSize) ||
            !HasOverlappableNeighbor(picture, macroBlock, position, mode.BlockSize))
        {
            return Av1MotionMode.SimpleTranslation;
        }

        if (!frameHeader.ForceIntegerMotionVector)
        {
            // is_global_mv_block()
            Av1GlobalMotionType globalMotionType = frameHeader.GetGlobalMotionParameters()[(int)mode.ReferenceFrame - 1].Type;
            if (mode.Mode == Av1PredictionMode.GlobalMotionVector && globalMotionType > Av1GlobalMotionType.Translation)
            {
                return Av1MotionMode.SimpleTranslation;
            }
        }

        // The references of a sequence all have the frame size, so none is scaled.
        if (frameHeader.AllowWarpedMotion &&
            !frameHeader.ForceIntegerMotionVector &&
            CountSamples(picture, macroBlock, position, mode) >= 1)
        {
            return Av1MotionMode.Warped;
        }

        return Av1MotionMode.Obmc;
    }

    /// <summary>
    /// Returns whether a block size admits OBMC and warped motion. Reference: is_motion_variation_allowed_bsize().
    /// </summary>
    /// <param name="blockSize">The block size.</param>
    /// <returns><see langword="true"/> when both dimensions are at least 8 samples.</returns>
    public static bool IsMotionVariationAllowed(Av1BlockSize blockSize)
        => Math.Min(blockSize.GetWidth(), blockSize.GetHeight()) >= 8;

    /// <summary>
    /// Returns whether an above or left neighbor is an inter block. Reference: av1_count_overlappable_neighbors()
    /// with foreach_overlappable_nb_above() and foreach_overlappable_nb_left().
    /// </summary>
    /// <param name="picture">The frame decisions.</param>
    /// <param name="macroBlock">The neighbor availability of the block.</param>
    /// <param name="position">The block origin in 4x4 units.</param>
    /// <param name="blockSize">The block size.</param>
    /// <returns><see langword="true"/> when at least one neighbor overlaps.</returns>
    public static bool HasOverlappableNeighbor(
        Av1PictureControlSet picture,
        Av1MacroBlockD macroBlock,
        Point position,
        Av1BlockSize blockSize)
    {
        ObuFrameHeader frameHeader = picture.Parent.FrameHeader;
        if (macroBlock.IsUpAvailable)
        {
            int endColumn = Math.Min(position.X + blockSize.Get4x4WideCount(), frameHeader.ModeInfoColumnCount);
            for (int column = position.X; column < endColumn;)
            {
                ref readonly Av1EncoderBlockModeInfo above = ref picture.GetFromModeInfoGrid(new Point(column, position.Y - 1)).Block;
                int step = Math.Min(above.BlockSize.Get4x4WideCount(), MaximumNeighborStep);
                if (step == 1)
                {
                    // A 4-wide neighbor is read from the second cell of its 8-wide pair.
                    column &= ~1;
                    above = ref picture.GetFromModeInfoGrid(new Point(column + 1, position.Y - 1)).Block;
                    step = 2;
                }

                if (IsOverlappable(in above))
                {
                    return true;
                }

                column += step;
            }
        }

        if (macroBlock.IsLeftAvailable)
        {
            int endRow = Math.Min(position.Y + blockSize.Get4x4HighCount(), frameHeader.ModeInfoRowCount);
            for (int row = position.Y; row < endRow;)
            {
                ref readonly Av1EncoderBlockModeInfo left = ref picture.GetFromModeInfoGrid(new Point(position.X - 1, row)).Block;
                int step = Math.Min(left.BlockSize.Get4x4HighCount(), MaximumNeighborStep);
                if (step == 1)
                {
                    row &= ~1;
                    left = ref picture.GetFromModeInfoGrid(new Point(position.X - 1, row + 1)).Block;
                    step = 2;
                }

                if (IsOverlappable(in left))
                {
                    return true;
                }

                row += step;
            }
        }

        return false;
    }

    /// <summary>
    /// Counts the neighbors that predict from the same single reference as the block, which supply warped-motion
    /// samples. Reference: av1_findSamples().
    /// </summary>
    /// <param name="picture">The frame decisions.</param>
    /// <param name="macroBlock">The neighbor availability of the block.</param>
    /// <param name="position">The block origin in 4x4 units.</param>
    /// <param name="mode">The block decisions.</param>
    /// <returns>The number of samples, at most eight.</returns>
    public static int CountSamples(
        Av1PictureControlSet picture,
        Av1MacroBlockD macroBlock,
        Point position,
        in Av1EncoderBlockModeInfo mode)
    {
        ObuFrameHeader frameHeader = picture.Parent.FrameHeader;
        Av1ReferenceFrameType referenceFrame = mode.ReferenceFrame;
        int width = mode.BlockSize.Get4x4WideCount();
        int height = mode.BlockSize.Get4x4HighCount();
        int row = position.Y;
        int column = position.X;
        int count = 0;
        bool doTopLeft = true;
        bool doTopRight = true;

        if (macroBlock.IsUpAvailable)
        {
            ref readonly Av1EncoderBlockModeInfo above = ref picture.GetFromModeInfoGrid(new Point(column, row - 1)).Block;
            int aboveWidth = above.BlockSize.Get4x4WideCount();
            if (width <= aboveWidth)
            {
                int columnOffset = -column % aboveWidth;
                doTopLeft &= columnOffset >= 0;
                doTopRight &= columnOffset + aboveWidth <= width;
                if (IsSample(in above, referenceFrame) && ++count >= MaximumSampleCount)
                {
                    return MaximumSampleCount;
                }
            }
            else
            {
                int end = Math.Min(width, frameHeader.ModeInfoColumnCount - column);
                for (int i = 0; i < end; i += aboveWidth)
                {
                    above = ref picture.GetFromModeInfoGrid(new Point(column + i, row - 1)).Block;
                    aboveWidth = above.BlockSize.Get4x4WideCount();
                    if (IsSample(in above, referenceFrame) && ++count >= MaximumSampleCount)
                    {
                        return MaximumSampleCount;
                    }
                }
            }
        }

        if (macroBlock.IsLeftAvailable)
        {
            ref readonly Av1EncoderBlockModeInfo left = ref picture.GetFromModeInfoGrid(new Point(column - 1, row)).Block;
            int leftHeight = left.BlockSize.Get4x4HighCount();
            if (height <= leftHeight)
            {
                int rowOffset = -row % leftHeight;
                doTopLeft &= rowOffset >= 0;
                if (IsSample(in left, referenceFrame) && ++count >= MaximumSampleCount)
                {
                    return MaximumSampleCount;
                }
            }
            else
            {
                int end = Math.Min(height, frameHeader.ModeInfoRowCount - row);
                for (int i = 0; i < end; i += leftHeight)
                {
                    left = ref picture.GetFromModeInfoGrid(new Point(column - 1, row + i)).Block;
                    leftHeight = left.BlockSize.Get4x4HighCount();
                    if (IsSample(in left, referenceFrame) && ++count >= MaximumSampleCount)
                    {
                        return MaximumSampleCount;
                    }
                }
            }
        }

        if (doTopLeft && macroBlock.IsLeftAvailable && macroBlock.IsUpAvailable)
        {
            ref readonly Av1EncoderBlockModeInfo topLeft = ref picture.GetFromModeInfoGrid(new Point(column - 1, row - 1)).Block;
            if (IsSample(in topLeft, referenceFrame) && ++count >= MaximumSampleCount)
            {
                return MaximumSampleCount;
            }
        }

        Av1TileInfo tile = macroBlock.Tile;
        int topRightRow = row - 1;
        int topRightColumn = column + width;
        if (doTopRight &&
            Av1PartitionInfo.HasTopRight(
                mode.BlockSize,
                mode.PartitionType,
                row,
                column,
                picture.Sequence.SequenceHeader.SuperblockModeInfoSize) &&
            topRightRow >= tile.ModeInfoRowStart &&
            topRightRow < tile.ModeInfoRowEnd &&
            topRightColumn >= tile.ModeInfoColumnStart &&
            topRightColumn < tile.ModeInfoColumnEnd)
        {
            ref readonly Av1EncoderBlockModeInfo topRight = ref picture.GetFromModeInfoGrid(new Point(topRightColumn, topRightRow)).Block;
            if (IsSample(in topRight, referenceFrame) && ++count >= MaximumSampleCount)
            {
                return MaximumSampleCount;
            }
        }

        return count;
    }

    /// <summary>
    /// Returns whether a neighbor predicts from the given single reference. Reference: the ref_frame test of
    /// av1_findSamples().
    /// </summary>
    private static bool IsSample(in Av1EncoderBlockModeInfo neighbor, Av1ReferenceFrameType referenceFrame)
        => neighbor.ReferenceFrame == referenceFrame && neighbor.SecondaryReferenceFrame == Av1ReferenceFrameType.None;

    /// <summary>
    /// Returns whether a neighbor is an inter or intra block copy block. Reference: is_neighbor_overlappable().
    /// </summary>
    private static bool IsOverlappable(in Av1EncoderBlockModeInfo neighbor)
        => neighbor.ReferenceFrame > Av1ReferenceFrameType.Intra || neighbor.UseIntraBlockCopy;
}
