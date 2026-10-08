// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <content>
/// Defines the final-block decision contract used by interleaved tile encoding.
/// </content>
internal partial class Av1TileWriter
{
    /// <summary>
    /// Supplies a final block decision immediately before its symbols are written.
    /// </summary>
    /// <typeparam name="TSample">The frame's unsigned sample storage type.</typeparam>
    internal interface IBlockEncodingHandler<TSample>
        where TSample : unmanaged
    {
        /// <summary>
        /// Gets a value indicating whether decisions come from completed frame analysis.
        /// </summary>
        static abstract bool UsesRetainedDecisions { get; }

        /// <summary>
        /// Selects the partition used for the current tree node.
        /// </summary>
        /// <param name="writer">The live tile symbol encoder.</param>
        /// <param name="tables">The rate tables of the tile.</param>
        /// <param name="modeWorkspace">The mode decision buffers of the block.</param>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficient buffer of the candidate.</param>
        /// <param name="searchDequantizedCoefficients">The dequantized coefficient buffer of the winner.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="transformTypeProbabilities">The transform type probabilities of every update type and size.</param>
        /// <param name="blockResidual">The residual buffer of the motion search.</param>
        /// <param name="searchCoefficients">The quantized coefficient buffer of the winner.</param>
        /// <param name="searchReconstructions">The storage of the candidate and winner reconstructions of the type search.</param>
        /// <param name="estimationRowCoefficients">The coefficients of one row of estimation transforms.</param>
        /// <param name="interWorkspace">The inter prediction buffers of the block.</param>
        /// <param name="motionSearchPrediction">The prediction buffer of the motion search.</param>
        /// <param name="motionVectorCosts">The motion vector rates of the frame precision.</param>
        /// <param name="transformPrediction">The prediction storage of one transform block.</param>
        /// <param name="interIntraAbove">The extended above edge of an inter-intra prediction.</param>
        /// <param name="interIntraLeft">The extended left edge of an inter-intra prediction.</param>
        /// <param name="firstIntermediate">The compound intermediate of the first reference.</param>
        /// <param name="secondIntermediate">The compound intermediate of the second reference.</param>
        /// <param name="compoundMask">The blend mask of a masked compound prediction.</param>
        /// <param name="partitionEdges">The partition context edges of the tile.</param>
        /// <param name="transformEdges">The transform size context edges of the tile.</param>
        /// <param name="paletteEdges">The palette color context edges of the tile.</param>
        /// <param name="lumaCoefficientEdges">The luma coefficient context edges of the tile.</param>
        /// <param name="blueCoefficientEdges">The blue-difference coefficient context edges of the tile.</param>
        /// <param name="redCoefficientEdges">The red-difference coefficient context edges of the tile.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="displacementVectors">The displacement vectors of the picture, one per allocation entry.</param>
        /// <param name="referenceContexts">The motion vector reference contexts of the picture, one per allocation entry.</param>
        /// <param name="blockEncodings">The final block decisions of the picture, one per allocation entry.</param>
        /// <param name="blockPalettes">The palettes of the picture, one per allocation entry.</param>
        /// <param name="paletteTokens">The color map tokens of the picture.</param>
        /// <param name="cdefPreset">The constrained directional enhancement filter strengths of each tile.</param>
        /// <param name="previousQIndex">The previous quantizer index of each tile.</param>
        /// <param name="encoderSegmentMap">The segment identifiers that the encoder keeps for the frame.</param>
        /// <param name="searchSegmentMap">The segment map that the frame buffer holds while the frame is searched.</param>
        /// <param name="previousSegmentMap">The segment map of the primary reference frame, or an empty map.</param>
        /// <param name="superblockCoefficients">The coefficients and transform block states of the superblock.</param>
        /// <param name="workspaceStorage">The storage of the block workspace, which holds the search buffers of every block.</param>
        /// <param name="sourcePlanes">The samples of the source frame planes, read once per frame pass.</param>
        /// <param name="reconstructionPlanes">The samples of the reconstructed frame planes, read once per frame pass.</param>
        /// <param name="macroBlock">The tile-local macroblock state.</param>
        /// <param name="blockOrigin">The absolute luma-sample origin.</param>
        /// <param name="blockSize">The current square partition size.</param>
        /// <param name="preparedPartition">The partition retained before live analysis.</param>
        /// <returns>The partition to encode.</returns>
        Av1PartitionType SelectPartition(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            in Av1EncoderModeDecisionWorkspace<TSample> modeWorkspace,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> searchDequantizedCoefficients,
            Span<int> transformWorkspace,
            ReadOnlySpan<int> transformTypeProbabilities,
            Span<short> blockResidual,
            Span<int> searchCoefficients,
            Span<int> searchReconstructions,
            Span<int> estimationRowCoefficients,
            in Av1EncoderInterPredictionWorkspace<TSample> interWorkspace,
            Span<TSample> motionSearchPrediction,
            in Av1MotionVectorCosts motionVectorCosts,
            Span<TSample> transformPrediction,
            Span<TSample> interIntraAbove,
            Span<TSample> interIntraLeft,
            Span<ushort> firstIntermediate,
            Span<ushort> secondIntermediate,
            Span<byte> compoundMask,
            in Av1NeighborEdges<Av1PartitionContext> partitionEdges,
            in Av1NeighborEdges<byte> transformEdges,
            in Av1NeighborEdges<Av1EncoderPaletteInfo> paletteEdges,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            in Av1NeighborEdges<byte> blueCoefficientEdges,
            in Av1NeighborEdges<byte> redCoefficientEdges,
            Span<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            Span<Av1EncoderDisplacementVector> displacementVectors,
            Span<Av1EncoderReferenceContext> referenceContexts,
            Span<Av1EncoderBlockStruct> blockEncodings,
            Span<Av1EncoderPaletteInfo> blockPalettes,
            Span<byte> paletteTokens,
            Span<int> cdefPreset,
            Span<int> previousQIndex,
            Span<byte> encoderSegmentMap,
            Span<byte> searchSegmentMap,
            ReadOnlySpan<byte> previousSegmentMap,
            Span<int> superblockCoefficients,
            Span<int> workspaceStorage,
            in Av1EncoderFrame<TSample>.PlanarSamples sourcePlanes,
            in Av1EncoderFrame<TSample>.PlanarSamples reconstructionPlanes,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            Av1PartitionType preparedPartition);

        /// <summary>
        /// Encodes one final block against the current reconstructed neighbors and live tile probabilities.
        /// </summary>
        /// <param name="writer">The live tile symbol encoder.</param>
        /// <param name="tables">The rate tables of the tile.</param>
        /// <param name="modeWorkspace">The mode decision buffers of the block.</param>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficient buffer of the candidate.</param>
        /// <param name="searchDequantizedCoefficients">The dequantized coefficient buffer of the winner.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="transformTypeProbabilities">The transform type probabilities of every update type and size.</param>
        /// <param name="blockResidual">The residual buffer of the motion search.</param>
        /// <param name="searchCoefficients">The quantized coefficient buffer of the winner.</param>
        /// <param name="searchReconstructions">The storage of the candidate and winner reconstructions of the type search.</param>
        /// <param name="estimationRowCoefficients">The coefficients of one row of estimation transforms.</param>
        /// <param name="interWorkspace">The inter prediction buffers of the block.</param>
        /// <param name="motionSearchPrediction">The prediction buffer of the motion search.</param>
        /// <param name="motionVectorCosts">The motion vector rates of the frame precision.</param>
        /// <param name="transformPrediction">The prediction storage of one transform block.</param>
        /// <param name="interIntraAbove">The extended above edge of an inter-intra prediction.</param>
        /// <param name="interIntraLeft">The extended left edge of an inter-intra prediction.</param>
        /// <param name="firstIntermediate">The compound intermediate of the first reference.</param>
        /// <param name="secondIntermediate">The compound intermediate of the second reference.</param>
        /// <param name="compoundMask">The blend mask of a masked compound prediction.</param>
        /// <param name="transformEdges">The transform size context edges of the tile.</param>
        /// <param name="paletteEdges">The palette color context edges of the tile.</param>
        /// <param name="lumaCoefficientEdges">The luma coefficient context edges of the tile.</param>
        /// <param name="blueCoefficientEdges">The blue-difference coefficient context edges of the tile.</param>
        /// <param name="redCoefficientEdges">The red-difference coefficient context edges of the tile.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="displacementVectors">The displacement vectors of the picture, one per allocation entry.</param>
        /// <param name="referenceContexts">The motion vector reference contexts of the picture, one per allocation entry.</param>
        /// <param name="blockEncodings">The final block decisions of the picture, one per allocation entry.</param>
        /// <param name="blockPalettes">The palettes of the picture, one per allocation entry.</param>
        /// <param name="paletteTokens">The color map tokens of the picture.</param>
        /// <param name="cdefPreset">The constrained directional enhancement filter strengths of each tile.</param>
        /// <param name="previousQIndex">The previous quantizer index of each tile.</param>
        /// <param name="encoderSegmentMap">The segment identifiers that the encoder keeps for the frame.</param>
        /// <param name="searchSegmentMap">The segment map that the frame buffer holds while the frame is searched.</param>
        /// <param name="previousSegmentMap">The segment map of the primary reference frame, or an empty map.</param>
        /// <param name="superblockCoefficients">The coefficients and transform block states of the superblock.</param>
        /// <param name="workspaceStorage">The storage of the block workspace, which holds the search buffers of every block.</param>
        /// <param name="sourcePlanes">The samples of the source frame planes, read once per frame pass.</param>
        /// <param name="reconstructionPlanes">The samples of the reconstructed frame planes, read once per frame pass.</param>
        /// <param name="macroBlock">The current block's mapped neighbor state.</param>
        /// <param name="blockOrigin">The absolute luma-sample origin.</param>
        /// <param name="modeInfo">The mode information to publish.</param>
        /// <param name="block">The encoder block state to publish.</param>
        /// <param name="paletteInfo">The current block's palette sizes and colors.</param>
        void EncodeBlock(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            in Av1EncoderModeDecisionWorkspace<TSample> modeWorkspace,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> searchDequantizedCoefficients,
            Span<int> transformWorkspace,
            ReadOnlySpan<int> transformTypeProbabilities,
            Span<short> blockResidual,
            Span<int> searchCoefficients,
            Span<int> searchReconstructions,
            Span<int> estimationRowCoefficients,
            in Av1EncoderInterPredictionWorkspace<TSample> interWorkspace,
            Span<TSample> motionSearchPrediction,
            in Av1MotionVectorCosts motionVectorCosts,
            Span<TSample> transformPrediction,
            Span<TSample> interIntraAbove,
            Span<TSample> interIntraLeft,
            Span<ushort> firstIntermediate,
            Span<ushort> secondIntermediate,
            Span<byte> compoundMask,
            in Av1NeighborEdges<byte> transformEdges,
            in Av1NeighborEdges<Av1EncoderPaletteInfo> paletteEdges,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            in Av1NeighborEdges<byte> blueCoefficientEdges,
            in Av1NeighborEdges<byte> redCoefficientEdges,
            Span<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            Span<Av1EncoderDisplacementVector> displacementVectors,
            Span<Av1EncoderReferenceContext> referenceContexts,
            Span<Av1EncoderBlockStruct> blockEncodings,
            Span<Av1EncoderPaletteInfo> blockPalettes,
            Span<byte> paletteTokens,
            Span<int> cdefPreset,
            Span<int> previousQIndex,
            Span<byte> encoderSegmentMap,
            Span<byte> searchSegmentMap,
            ReadOnlySpan<byte> previousSegmentMap,
            Span<int> superblockCoefficients,
            Span<int> workspaceStorage,
            in Av1EncoderFrame<TSample>.PlanarSamples sourcePlanes,
            in Av1EncoderFrame<TSample>.PlanarSamples reconstructionPlanes,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ref Av1MacroBlockModeInfo modeInfo,
            ref Av1EncoderBlockStruct block,
            ref Av1EncoderPaletteInfo paletteInfo);
    }

    /// <summary>
    /// Supplies the block decisions that a completed frame analysis retained in the picture.
    /// </summary>
    /// <typeparam name="TSample">The frame's unsigned sample storage type.</typeparam>
    internal readonly struct RetainedBlockEncodingHandler<TSample> : IBlockEncodingHandler<TSample>
        where TSample : unmanaged
    {
        private readonly Av1PictureControlSet picture;

        /// <summary>
        /// Initializes a new instance of the <see cref="RetainedBlockEncodingHandler{TSample}"/> struct.
        /// </summary>
        /// <param name="picture">The picture that holds the retained decisions.</param>
        public RetainedBlockEncodingHandler(Av1PictureControlSet picture)
            => this.picture = picture;

        /// <inheritdoc/>
        public static bool UsesRetainedDecisions => true;

        /// <inheritdoc/>
        public Av1PartitionType SelectPartition(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            in Av1EncoderModeDecisionWorkspace<TSample> modeWorkspace,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> searchDequantizedCoefficients,
            Span<int> transformWorkspace,
            ReadOnlySpan<int> transformTypeProbabilities,
            Span<short> blockResidual,
            Span<int> searchCoefficients,
            Span<int> searchReconstructions,
            Span<int> estimationRowCoefficients,
            in Av1EncoderInterPredictionWorkspace<TSample> interWorkspace,
            Span<TSample> motionSearchPrediction,
            in Av1MotionVectorCosts motionVectorCosts,
            Span<TSample> transformPrediction,
            Span<TSample> interIntraAbove,
            Span<TSample> interIntraLeft,
            Span<ushort> firstIntermediate,
            Span<ushort> secondIntermediate,
            Span<byte> compoundMask,
            in Av1NeighborEdges<Av1PartitionContext> partitionEdges,
            in Av1NeighborEdges<byte> transformEdges,
            in Av1NeighborEdges<Av1EncoderPaletteInfo> paletteEdges,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            in Av1NeighborEdges<byte> blueCoefficientEdges,
            in Av1NeighborEdges<byte> redCoefficientEdges,
            Span<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            Span<Av1EncoderDisplacementVector> displacementVectors,
            Span<Av1EncoderReferenceContext> referenceContexts,
            Span<Av1EncoderBlockStruct> blockEncodings,
            Span<Av1EncoderPaletteInfo> blockPalettes,
            Span<byte> paletteTokens,
            Span<int> cdefPreset,
            Span<int> previousQIndex,
            Span<byte> encoderSegmentMap,
            Span<byte> searchSegmentMap,
            ReadOnlySpan<byte> previousSegmentMap,
            Span<int> superblockCoefficients,
            Span<int> workspaceStorage,
            in Av1EncoderFrame<TSample>.PlanarSamples sourcePlanes,
            in Av1EncoderFrame<TSample>.PlanarSamples reconstructionPlanes,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            Av1PartitionType preparedPartition)
        {
            Point position = new(blockOrigin.X >> Av1Constants.ModeInfoSizeLog2, blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2);
            Av1BlockSize selectedSize = this.picture.GetFromModeInfoGrid(modeInfoGrid, modeInfoAllocation, position).Block.BlockSize;
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
                Av1BlockSize below = this.picture.GetFromModeInfoGrid(modeInfoGrid, modeInfoAllocation, position + new Size(0, height / 2)).Block.BlockSize;
                Av1BlockSize right = this.picture.GetFromModeInfoGrid(modeInfoGrid, modeInfoAllocation, position + new Size(width / 2, 0)).Block.BlockSize;
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

        /// <inheritdoc/>
        public void EncodeBlock(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            in Av1EncoderModeDecisionWorkspace<TSample> modeWorkspace,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> searchDequantizedCoefficients,
            Span<int> transformWorkspace,
            ReadOnlySpan<int> transformTypeProbabilities,
            Span<short> blockResidual,
            Span<int> searchCoefficients,
            Span<int> searchReconstructions,
            Span<int> estimationRowCoefficients,
            in Av1EncoderInterPredictionWorkspace<TSample> interWorkspace,
            Span<TSample> motionSearchPrediction,
            in Av1MotionVectorCosts motionVectorCosts,
            Span<TSample> transformPrediction,
            Span<TSample> interIntraAbove,
            Span<TSample> interIntraLeft,
            Span<ushort> firstIntermediate,
            Span<ushort> secondIntermediate,
            Span<byte> compoundMask,
            in Av1NeighborEdges<byte> transformEdges,
            in Av1NeighborEdges<Av1EncoderPaletteInfo> paletteEdges,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            in Av1NeighborEdges<byte> blueCoefficientEdges,
            in Av1NeighborEdges<byte> redCoefficientEdges,
            Span<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            Span<Av1EncoderDisplacementVector> displacementVectors,
            Span<Av1EncoderReferenceContext> referenceContexts,
            Span<Av1EncoderBlockStruct> blockEncodings,
            Span<Av1EncoderPaletteInfo> blockPalettes,
            Span<byte> paletteTokens,
            Span<int> cdefPreset,
            Span<int> previousQIndex,
            Span<byte> encoderSegmentMap,
            Span<byte> searchSegmentMap,
            ReadOnlySpan<byte> previousSegmentMap,
            Span<int> superblockCoefficients,
            Span<int> workspaceStorage,
            in Av1EncoderFrame<TSample>.PlanarSamples sourcePlanes,
            in Av1EncoderFrame<TSample>.PlanarSamples reconstructionPlanes,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ref Av1MacroBlockModeInfo modeInfo,
            ref Av1EncoderBlockStruct block,
            ref Av1EncoderPaletteInfo paletteInfo)
        {
            int row = blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2;
            int column = blockOrigin.X >> Av1Constants.ModeInfoSizeLog2;
            int allocationOffset = modeInfoGrid[(row * this.picture.ModeInfoStride) + column];
            block = blockEncodings[allocationOffset];
            if (this.picture.Parent.FrameHeader.AllowScreenContentTools)
            {
                paletteInfo = blockPalettes[allocationOffset];
            }
        }
    }
}
