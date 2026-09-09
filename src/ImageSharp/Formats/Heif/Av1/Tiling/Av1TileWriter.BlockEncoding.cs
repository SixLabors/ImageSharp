// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <content>
/// Defines the final-block decision contract used by interleaved tile encoding.
/// </content>
internal partial class Av1TileWriter
{
    /// <summary>
    /// Supplies a final block decision immediately before its symbols are written.
    /// </summary>
    internal interface IBlockEncodingHandler
    {
        /// <summary>
        /// Gets a value indicating whether decisions come from completed frame analysis.
        /// </summary>
        static abstract bool UsesRetainedDecisions { get; }

        /// <summary>
        /// Selects the partition used for the current tree node.
        /// </summary>
        /// <param name="writer">The live tile symbol encoder.</param>
        /// <param name="macroBlock">The tile-local macroblock state.</param>
        /// <param name="blockOrigin">The absolute luma-sample origin.</param>
        /// <param name="tileIndex">The zero-based tile index.</param>
        /// <param name="blockSize">The current square partition size.</param>
        /// <param name="preparedPartition">The partition retained before live analysis.</param>
        /// <returns>The partition to encode.</returns>
        Av1PartitionType SelectPartition(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ushort tileIndex,
            Av1BlockSize blockSize,
            Av1PartitionType preparedPartition);

        /// <summary>
        /// Encodes one final block against the current reconstructed neighbors and live tile probabilities.
        /// </summary>
        /// <param name="writer">The live tile symbol encoder.</param>
        /// <param name="macroBlock">The current block's mapped neighbor state.</param>
        /// <param name="blockOrigin">The absolute luma-sample origin.</param>
        /// <param name="tileIndex">The zero-based tile index.</param>
        /// <param name="modeInfo">The mode information to publish.</param>
        /// <param name="block">The encoder block state to publish.</param>
        /// <param name="paletteInfo">The current block's palette sizes and colors.</param>
        void EncodeBlock(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ushort tileIndex,
            ref Av1MacroBlockModeInfo modeInfo,
            ref Av1EncoderBlockStruct block,
            ref Av1EncoderPaletteInfo paletteInfo);
    }

    internal readonly struct RetainedBlockEncodingHandler : IBlockEncodingHandler
    {
        private readonly Av1PictureControlSet picture;

        public RetainedBlockEncodingHandler(Av1PictureControlSet picture)
            => this.picture = picture;

        public static bool UsesRetainedDecisions => true;

        public Av1PartitionType SelectPartition(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ushort tileIndex,
            Av1BlockSize blockSize,
            Av1PartitionType preparedPartition)
        {
            Point position = new(blockOrigin.X >> Av1Constants.ModeInfoSizeLog2, blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2);
            Av1BlockSize selectedSize = this.picture.GetFromModeInfoGrid(position).Block.BlockSize;
            if (selectedSize == blockSize)
            {
                return Av1PartitionType.None;
            }

            int width = blockSize.Get4x4WideCount();
            int height = blockSize.Get4x4HighCount();
            int selectedWidth = selectedSize.Get4x4WideCount();
            int selectedHeight = selectedSize.Get4x4HighCount();
            if (blockSize > Av1BlockSize.Block8x8 &&
                position.Y + (height / 2) < this.picture.Parent.Common.ModeInfoRowCount &&
                position.X + (width / 2) < this.picture.Parent.Common.ModeInfoColumnCount)
            {
                // A half-sized top-left block alone cannot distinguish an asymmetric partition from a split.
                // The mapped blocks at the two half boundaries identify which half remains unsplit.
                Av1BlockSize below = this.picture.GetFromModeInfoGrid(position + new Size(0, height / 2)).Block.BlockSize;
                Av1BlockSize right = this.picture.GetFromModeInfoGrid(position + new Size(width / 2, 0)).Block.BlockSize;
                if (selectedWidth == width)
                {
                    return selectedHeight * 4 == height
                        ? Av1PartitionType.Horizontal4
                        : below == selectedSize ? Av1PartitionType.Horizontal : Av1PartitionType.HorizontalB;
                }

                if (selectedHeight == height)
                {
                    return selectedWidth * 4 == width
                        ? Av1PartitionType.Vertical4
                        : right == selectedSize ? Av1PartitionType.Vertical : Av1PartitionType.VerticalB;
                }

                if (selectedWidth * 2 == width && selectedHeight * 2 == height)
                {
                    if (below.Get4x4WideCount() == width)
                    {
                        return Av1PartitionType.HorizontalA;
                    }

                    if (right.Get4x4HighCount() == height)
                    {
                        return Av1PartitionType.VerticalA;
                    }
                }

                return Av1PartitionType.Split;
            }

            // At a frame edge only the basic partitions are available. Each smaller dimension contributes
            // one split axis; recursive descent then reaches the retained leaf geometry.
            return selectedWidth == width
                ? Av1PartitionType.Horizontal
                : selectedHeight == height ? Av1PartitionType.Vertical : Av1PartitionType.Split;
        }

        public void EncodeBlock(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ushort tileIndex,
            ref Av1MacroBlockModeInfo modeInfo,
            ref Av1EncoderBlockStruct block,
            ref Av1EncoderPaletteInfo paletteInfo)
        {
            int row = blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2;
            int column = blockOrigin.X >> Av1Constants.ModeInfoSizeLog2;
            int allocationOffset = this.picture.ModeInfoGrid.Span[(row * this.picture.ModeInfoStride) + column];
            block = this.picture.BlockEncodings.Span[allocationOffset];
            if (this.picture.Parent.FrameHeader.AllowScreenContentTools)
            {
                paletteInfo = this.picture.BlockPalettes.Span[allocationOffset];
            }
        }
    }

    private readonly struct PrecomputedBlockEncodingHandler : IBlockEncodingHandler
    {
        /// <inheritdoc/>
        public static bool UsesRetainedDecisions => false;

        /// <inheritdoc/>
        public Av1PartitionType SelectPartition(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ushort tileIndex,
            Av1BlockSize blockSize,
            Av1PartitionType preparedPartition)
            => preparedPartition;

        /// <inheritdoc/>
        public void EncodeBlock(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ushort tileIndex,
            ref Av1MacroBlockModeInfo modeInfo,
            ref Av1EncoderBlockStruct block,
            ref Av1EncoderPaletteInfo paletteInfo)
        {
        }
    }
}
