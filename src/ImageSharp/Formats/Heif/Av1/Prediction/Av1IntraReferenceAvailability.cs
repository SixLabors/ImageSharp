// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

/// <summary>
/// Determines whether extended intra-prediction references have already been reconstructed.
/// </summary>
internal static class Av1IntraReferenceAvailability
{
    /// <summary>
    /// Determines whether every bottom-left reference sample required by a transform is already reconstructed.
    /// </summary>
    /// <param name="superblockSize">The sequence superblock size.</param>
    /// <param name="blockSize">The containing block size in the current plane's geometry.</param>
    /// <param name="modeInfoRow">The containing block row in 4-by-4 mode-information units.</param>
    /// <param name="modeInfoColumn">The containing block column in 4-by-4 mode-information units.</param>
    /// <param name="bottomAvailable">A value indicating whether the required rows remain inside the frame and tile.</param>
    /// <param name="haveLeft">A value indicating whether reconstructed samples exist immediately to the left.</param>
    /// <param name="partition">The partition type that determines reconstruction order.</param>
    /// <param name="transformSize">The transform size whose extended edge is required.</param>
    /// <param name="blockModeInfoRowOffset">The transform row offset within the containing block.</param>
    /// <param name="blockModeInfoColumnOffset">The transform column offset within the containing block.</param>
    /// <param name="subX">The horizontal chroma subsampling shift.</param>
    /// <param name="subY">The vertical chroma subsampling shift.</param>
    /// <returns><see langword="true"/> when the bottom-left reference extension is available; otherwise, <see langword="false"/>.</returns>
    public static bool HasBottomLeft(Av1BlockSize superblockSize, Av1BlockSize blockSize, int modeInfoRow, int modeInfoColumn, bool bottomAvailable, bool haveLeft, Av1PartitionType partition, Av1TransformSize transformSize, int blockModeInfoRowOffset, int blockModeInfoColumnOffset, int subX, int subY)
    {
        if (!bottomAvailable || !haveLeft)
        {
            return false;
        }

        // A 128-wide block is reconstructed as two 64-wide regions in raster order,
        // so the right half can consume references that already belong to the left half.
        if (blockSize.GetWidth() > 64 && blockModeInfoColumnOffset > 0)
        {
            int block64WidthInUnits = Av1BlockSize.Block64x64.Get4x4WideCount();
            int planeBlockWidthInUnits64 = block64WidthInUnits >> subX;
            int columnOffset64 = blockModeInfoColumnOffset % planeBlockWidthInUnits64;
            if (columnOffset64 == 0)
            {
                // We are at the left edge of top-right or bottom-right 64x* block.
                int block64HeightInUnits = Av1BlockSize.Block64x64.Get4x4HighCount();
                int planeBlockHeightInUnits64 = block64HeightInUnits >> subY;
                int rowOffset64 = blockModeInfoRowOffset % planeBlockHeightInUnits64;
                int planeBlockHeightInUnits = Math.Min(blockSize.Get4x4HighCount() >> subY, planeBlockHeightInUnits64);

                // Check if all bottom-left pixels are in the left 64x* block (which is
                // already coded).
                return rowOffset64 + transformSize.Get4x4HighCount() < planeBlockHeightInUnits;
            }
        }

        if (blockModeInfoColumnOffset > 0)
        {
            // Bottom-left pixels are in the bottom-left block, which is not available.
            return false;
        }
        else
        {
            int blockHeightInUnits = blockSize.GetHeight() >> Av1TransformSize.Size4x4.GetBlockHeightLog2();
            int planeBlockHeightInUnits = Math.Max(blockHeightInUnits >> subY, 1);
            int bottomLeftUnitCount = transformSize.Get4x4HighCount();

            // All bottom-left pixels are in the left block, which is already available.
            if (blockModeInfoRowOffset + bottomLeftUnitCount < planeBlockHeightInUnits)
            {
                return true;
            }

            int blockWidthInModeInfoLog2 = blockSize.Get4x4WidthLog2();
            int blockHeightInModeInfoLog2 = blockSize.Get4x4HeightLog2();
            int superblockModeInfoSize = superblockSize.Get4x4HighCount();
            int blockRowInSuperblock = (modeInfoRow & (superblockModeInfoSize - 1)) >> blockHeightInModeInfoLog2;
            int blockColumnInSuperblock = (modeInfoColumn & (superblockModeInfoSize - 1)) >> blockWidthInModeInfoLog2;

            // Leftmost column of superblock: so bottom-left pixels maybe in the left
            // and/or bottom-left superblocks. But only the left superblock is
            // available, so check if all required pixels fall in that superblock.
            if (blockColumnInSuperblock == 0)
            {
                int blockStartRowOffset = blockRowInSuperblock << (blockHeightInModeInfoLog2 + Av1Constants.ModeInfoSizeLog2 - Av1TransformSize.Size4x4.GetBlockWidthLog2()) >> subY;
                int rowOffsetInSuperblock = blockStartRowOffset + blockModeInfoRowOffset;
                int superblockHeightInUnits = superblockModeInfoSize >> subY;
                return rowOffsetInSuperblock + bottomLeftUnitCount < superblockHeightInUnits;
            }

            // Bottom row of superblock (and not the leftmost column): so bottom-left
            // pixels fall in the bottom superblock, which is not available yet.
            if (((blockRowInSuperblock + 1) << blockHeightInModeInfoLog2) >= superblockModeInfoSize)
            {
                return false;
            }

            // General case (neither leftmost column nor bottom row): check if the
            // bottom-left block is coded before the current block.
            int thisBlockIndex = ((blockRowInSuperblock + 0) << (Av1Constants.MaxSuperBlockSizeLog2 - Av1Constants.ModeInfoSizeLog2 - blockWidthInModeInfoLog2)) + blockColumnInSuperblock + 0;
            return Av1BottomRightTopLeftConstants.HasBottomLeft(partition, blockSize, thisBlockIndex);
        }
    }

    /// <summary>
    /// Determines whether every top-right reference sample required by a transform is already reconstructed.
    /// </summary>
    /// <param name="superblockSize">The sequence superblock size.</param>
    /// <param name="blockSize">The containing block size in the current plane's geometry.</param>
    /// <param name="modeInfoRow">The containing block row in 4-by-4 mode-information units.</param>
    /// <param name="modeInfoColumn">The containing block column in 4-by-4 mode-information units.</param>
    /// <param name="haveTop">A value indicating whether reconstructed samples exist immediately above.</param>
    /// <param name="rightAvailable">A value indicating whether the required columns remain inside the frame and tile.</param>
    /// <param name="partition">The partition type that determines reconstruction order.</param>
    /// <param name="transformSize">The transform size whose extended edge is required.</param>
    /// <param name="blockModeInfoRowOffset">The transform row offset within the containing block.</param>
    /// <param name="blockModeInfoColumnOffset">The transform column offset within the containing block.</param>
    /// <param name="subX">The horizontal chroma subsampling shift.</param>
    /// <param name="subY">The vertical chroma subsampling shift.</param>
    /// <returns><see langword="true"/> when the top-right reference extension is available; otherwise, <see langword="false"/>.</returns>
    public static bool HasTopRight(Av1BlockSize superblockSize, Av1BlockSize blockSize, int modeInfoRow, int modeInfoColumn, bool haveTop, bool rightAvailable, Av1PartitionType partition, Av1TransformSize transformSize, int blockModeInfoRowOffset, int blockModeInfoColumnOffset, int subX, int subY)
    {
        if (!haveTop || !rightAvailable)
        {
            return false;
        }

        int blockWideInUnits = blockSize.GetWidth() >> 2;
        int planeBlockWidthInUnits = Math.Max(blockWideInUnits >> subX, 1);
        int topRightUnitCount = transformSize.Get4x4WideCount();

        if (blockModeInfoRowOffset > 0)
        {
            // Transforms below the first row obtain their top edge from the containing block,
            // so only the reconstructed width to their right constrains availability.
            if (blockSize.GetWidth() > 64)
            {
                // Special case: For 128x128 blocks, the transform unit whose
                // top-right corner is at the center of the block does in fact have
                // pixels available at its top-right corner.
                int block64WidthInUnits = Av1BlockSize.Block64x64.Get4x4WideCount();
                int block64HeightInUnits = Av1BlockSize.Block64x64.Get4x4HighCount();
                if (blockModeInfoRowOffset == block64HeightInUnits >> subY &&
                    blockModeInfoColumnOffset + topRightUnitCount == block64WidthInUnits >> subX)
                {
                    return true;
                }

                int planeBlockWidthInUnits64 = block64WidthInUnits >> subX;
                int blockModeInfoColumnOffset64 = blockModeInfoColumnOffset % planeBlockWidthInUnits64;
                return blockModeInfoColumnOffset64 + topRightUnitCount < planeBlockWidthInUnits64;
            }

            return blockModeInfoColumnOffset + topRightUnitCount < planeBlockWidthInUnits;
        }
        else
        {
            // All top-right pixels are in the block above, which is already available.
            if (blockModeInfoColumnOffset + topRightUnitCount < planeBlockWidthInUnits)
            {
                return true;
            }

            int blockWidthInModeInfoLog2 = blockSize.Get4x4WidthLog2();
            int blockHeightInModeInfeLog2 = blockSize.Get4x4HeightLog2();
            int superBlockModeInfoSize = superblockSize.Get4x4HighCount();
            int blockRowInSuperblock = (modeInfoRow & (superBlockModeInfoSize - 1)) >> blockHeightInModeInfeLog2;
            int blockColumnInSuperBlock = (modeInfoColumn & (superBlockModeInfoSize - 1)) >> blockWidthInModeInfoLog2;

            // Top row of superblock: so top-right pixels are in the top and/or
            // top-right superblocks, both of which are already available.
            if (blockRowInSuperblock == 0)
            {
                return true;
            }

            // Rightmost column of superblock (and not the top row): so top-right pixels
            // fall in the right superblock, which is not available yet.
            if (((blockColumnInSuperBlock + 1) << blockWidthInModeInfoLog2) >= superBlockModeInfoSize)
            {
                return false;
            }

            // General case (neither top row nor rightmost column): check if the
            // top-right block is coded before the current block.
            int thisBlockIndex = ((blockRowInSuperblock + 0) << (Av1Constants.MaxSuperBlockSizeLog2 - Av1Constants.ModeInfoSizeLog2 - blockWidthInModeInfoLog2)) + blockColumnInSuperBlock + 0;
            return Av1BottomRightTopLeftConstants.HasTopRight(partition, blockSize, thisBlockIndex);
        }
    }
}
