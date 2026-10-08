// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Writes the partition, mode, transform, and coefficient syntax for one AV1 tile.
/// </summary>
internal partial class Av1TileWriter
{
    /// <summary>
    /// Maps each AV1 block size to the five-bit partition contexts written to its bottom and right edges.
    /// </summary>
    /// <remarks>
    /// Each set bit represents a split level from 128x128 through 8x8. For example, <c>11111</c>
    /// records every split level, while <c>10000</c> records only the 128x128 split.
    /// </remarks>
    private static readonly Av1PartitionContext[] PartitionContextLookup =
        [
            new(31, 31),  // 4X4   - {0b11111, 0b11111}
            new(31, 30),  // 4X8   - {0b11111, 0b11110}
            new(30, 31),  // 8X4   - {0b11110, 0b11111}
            new(30, 30),  // 8X8   - {0b11110, 0b11110}
            new(30, 28),  // 8X16  - {0b11110, 0b11100}
            new(28, 30),  // 16X8  - {0b11100, 0b11110}
            new(28, 28),  // 16X16 - {0b11100, 0b11100}
            new(28, 24),  // 16X32 - {0b11100, 0b11000}
            new(24, 28),  // 32X16 - {0b11000, 0b11100}
            new(24, 24),  // 32X32 - {0b11000, 0b11000}
            new(24, 16),  // 32X64 - {0b11000, 0b10000}
            new(16, 24),  // 64X32 - {0b10000, 0b11000}
            new(16, 16),  // 64X64 - {0b10000, 0b10000}
            new(16, 0),   // 64X128- {0b10000, 0b00000}
            new(0, 16),   // 128X64- {0b00000, 0b10000}
            new(0, 0),    // 128X128-{0b00000, 0b00000}
            new(31, 28),  // 4X16  - {0b11111, 0b11100}
            new(28, 31),  // 16X4  - {0b11100, 0b11111}
            new(30, 24),  // 8X32  - {0b11110, 0b11000}
            new(24, 30),  // 32X8  - {0b11000, 0b11110}
            new(28, 16),  // 16X64 - {0b11100, 0b10000}
            new(16, 28),  // 64X16 - {0b10000, 0b11100}
    ];

    /// <summary>
    /// Maps neighboring intra prediction modes to the key-frame luma-mode entropy contexts.
    /// </summary>
    private static readonly byte[] IntraModeContextLookup = [0, 1, 2, 3, 4, 4, 4, 4, 3, 0, 1, 2, 0];

    /// <summary>
    /// Writes a partition tree while producing each final block against the immediately preceding tile state.
    /// </summary>
    /// <typeparam name="TOperation">Selects whether symbols are written or only adapt the probabilities.</typeparam>
    /// <typeparam name="TBlockEncoder">The value type that produces final-block decisions.</typeparam>
    /// <typeparam name="TSample">The frame's unsigned sample storage type.</typeparam>
    /// <param name="output">The tile buffer that the caller read once. A write that grows the buffer replaces it.</param>
    /// <param name="pcs">The picture coding state.</param>
    /// <param name="ec_ctx">The entropy-coding position state for the superblock.</param>
    /// <param name="writer">The tile symbol encoder.</param>
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
    /// <param name="superblock">The encoder decisions for the superblock.</param>
    /// <param name="coefficientBuffer">The transformed coefficients retained by raster-ordered superblock.</param>
    /// <param name="tileIndex">The zero-based tile index.</param>
    /// <param name="blockEncoder">The handler invoked for each final block.</param>
    public static void WriteSuperblock<TOperation, TBlockEncoder, TSample>(
        ref Span<byte> output,
        Av1PictureControlSet pcs,
        Av1EntropyCodingContext ec_ctx,
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
        Av1Superblock superblock,
        Av1EncoderCoefficientBuffer coefficientBuffer,
        ushort tileIndex,
        ref TBlockEncoder blockEncoder)
        where TBlockEncoder : struct, IBlockEncodingHandler<TSample>
        where TOperation : struct, Av1SymbolEncoder.ISymbolOperation
        where TSample : unmanaged
    {
        ec_ctx.CodedAreaSuperblock = 0;
        ec_ctx.CodedAreaSuperblockUv = 0;
        ec_ctx.PaletteTokenOffset = superblock.Index *
            (1 << (2 * pcs.Sequence.SequenceHeader.SuperblockSizeLog2)) *
            Math.Min(2, pcs.Sequence.SequenceHeader.ColorConfig.PlaneCount);

        ec_ctx.MacroBlock.Tile = superblock.TileInfo;
        int partitionIndex = 0;
        int finalBlockIndex = 0;

        ObuSequenceHeader sequence = pcs.Sequence.SequenceHeader;
        ObuFrameHeader header = pcs.Parent.FrameHeader;
        ObuColorConfig color = sequence.ColorConfig;
        Span<Av1LoopRestorationUnit> references = pcs.RestorationReferences.Span;
        int referenceOffset = tileIndex * color.PlaneCount;
        ReadOnlySpan<Av1LoopRestorationUnit> allUnits = pcs.RestorationUnits.Span;
        InlineArray3<int> unitOffsetStorage = pcs.RestorationUnitOffsets;
        ReadOnlySpan<int> unitOffsets = unitOffsetStorage;
        for (int plane = 0; plane < color.PlaneCount; plane++)
        {
            ObuLoopRestorationItem item = header.LoopRestorationParameters.Items[plane];
            if (item.Type == ObuRestorationType.None)
            {
                continue;
            }

            int subX = plane != 0 && color.SubSamplingX ? 1 : 0;
            int subY = plane != 0 && color.SubSamplingY ? 1 : 0;
            int width = Av1Math.DivideLog2Ceiling(header.FrameSize.SuperResolutionUpscaledWidth, subX);
            int height = Av1Math.DivideLog2Ceiling(header.FrameSize.FrameHeight, subY);
            int columns = Math.Max(1, (width + (item.Size >> 1)) / item.Size);
            int rows = Math.Max(1, (height + (item.Size >> 1)) / item.Size);
            int superblockSize = 1 << sequence.SuperblockSizeLog2;
            bool usesSuperResolution = header.FrameSize.FrameWidth != header.FrameSize.SuperResolutionUpscaledWidth;
            int horizontalScale = usesSuperResolution ? header.FrameSize.SuperResolutionDenominator : 1;
            int horizontalDivisor = item.Size * (usesSuperResolution ? Av1Constants.ScaleNumerator : 1);
            int firstColumn = (((ec_ctx.SuperblockOrigin.X >> subX) * horizontalScale) + horizontalDivisor - 1) / horizontalDivisor;
            int lastColumn = Math.Min(
                columns,
                ((((ec_ctx.SuperblockOrigin.X + superblockSize) >> subX) * horizontalScale) + horizontalDivisor - 1) / horizontalDivisor);

            int firstRow = ((ec_ctx.SuperblockOrigin.Y >> subY) + item.Size - 1) / item.Size;
            int lastRow = Math.Min(rows, (((ec_ctx.SuperblockOrigin.Y + superblockSize) >> subY) + item.Size - 1) / item.Size);
            ref Av1LoopRestorationUnit reference = ref references[referenceOffset + plane];
            ReadOnlySpan<Av1LoopRestorationUnit> units = allUnits.Slice(unitOffsets[plane], rows * columns);

            // Each unit is signaled before the partition syntax of the superblock containing its
            // upper-left corner. Coefficient histories advance only for transmitted filters.
            for (int row = firstRow; row < lastRow; row++)
            {
                for (int column = firstColumn; column < lastColumn; column++)
                {
                    writer.WriteRestorationUnit<TOperation>(ref output, item.Type, units[(row * columns) + column], plane != 0, ref reference);
                }
            }
        }

        // Partition decisions are stored in preorder, so recursive traversal keeps the current geometry
        // on the stack and visits each selected child after its parent.
        WritePartitionTree<TOperation, TBlockEncoder, TSample>(
            ref output,
            pcs,
            ec_ctx,
            writer,
            in tables,
            in modeWorkspace,
            transformCoefficients,
            dequantizedCoefficients,
            searchDequantizedCoefficients,
            transformWorkspace,
            transformTypeProbabilities,
            blockResidual,
            searchCoefficients,
            searchReconstructions,
            estimationRowCoefficients,
            in interWorkspace,
            motionSearchPrediction,
            in motionVectorCosts,
            transformPrediction,
            interIntraAbove,
            interIntraLeft,
            firstIntermediate,
            secondIntermediate,
            compoundMask,
            in partitionEdges,
            in transformEdges,
            in paletteEdges,
            in lumaCoefficientEdges,
            in blueCoefficientEdges,
            in redCoefficientEdges,
            superblock,
            coefficientBuffer,
            tileIndex,
            pcs.Sequence.SequenceHeader.SuperblockSize,
            ec_ctx.SuperblockOrigin,
            ref partitionIndex,
            ref finalBlockIndex,
            ref blockEncoder);
    }

    /// <summary>
    /// Writes one selected partition node and recursively visits its split children.
    /// </summary>
    /// <typeparam name="TOperation">Selects whether symbols are written or only adapt the probabilities.</typeparam>
    /// <typeparam name="TBlockEncoder">The value type that produces final-block decisions.</typeparam>
    /// <typeparam name="TSample">The frame's unsigned sample storage type.</typeparam>
    /// <param name="output">The tile buffer that the caller read once. A write that grows the buffer replaces it.</param>
    /// <param name="pcs">The picture coding state.</param>
    /// <param name="entropyCodingContext">The entropy-coding position state for the superblock.</param>
    /// <param name="writer">The tile symbol encoder.</param>
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
    /// <param name="superblock">The encoder decisions for the superblock.</param>
    /// <param name="coefficientBuffer">The transformed coefficients retained by raster-ordered superblock.</param>
    /// <param name="tileIndex">The zero-based tile index.</param>
    /// <param name="blockSize">The size of the partition node.</param>
    /// <param name="blockOrigin">The absolute luma-sample origin of the partition node.</param>
    /// <param name="partitionIndex">The index of the next partition decision of the superblock, advanced for each node.</param>
    /// <param name="finalBlockIndex">The index of the next final block of the superblock, advanced for each final block.</param>
    /// <param name="blockEncoder">The handler invoked for each final block.</param>
    private static void WritePartitionTree<TOperation, TBlockEncoder, TSample>(
        ref Span<byte> output,
        Av1PictureControlSet pcs,
        Av1EntropyCodingContext entropyCodingContext,
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
        Av1Superblock superblock,
        Av1EncoderCoefficientBuffer coefficientBuffer,
        ushort tileIndex,
        Av1BlockSize blockSize,
        Point blockOrigin,
        ref int partitionIndex,
        ref int finalBlockIndex,
        ref TBlockEncoder blockEncoder)
        where TBlockEncoder : struct, IBlockEncodingHandler<TSample>
        where TOperation : struct, Av1SymbolEncoder.ISymbolOperation
        where TSample : unmanaged
    {
        Av1EncoderCommon common = pcs.Parent.Common;
        int modeInfoRow = blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2;
        int modeInfoColumn = blockOrigin.X >> Av1Constants.ModeInfoSizeLog2;
        if (modeInfoRow >= common.ModeInfoRowCount || modeInfoColumn >= common.ModeInfoColumnCount)
        {
            return;
        }

        int currentPartitionIndex = partitionIndex++;
        Av1PartitionType preparedPartition =
            (Av1PartitionType)superblock.CodingUnitPartitionTypes[currentPartitionIndex];

        Av1PartitionType partition = blockEncoder.SelectPartition(
            writer,
            in tables,
            in modeWorkspace,
            transformCoefficients,
            dequantizedCoefficients,
            searchDequantizedCoefficients,
            transformWorkspace,
            transformTypeProbabilities,
            blockResidual,
            searchCoefficients,
            searchReconstructions,
            estimationRowCoefficients,
            in interWorkspace,
            motionSearchPrediction,
            in motionVectorCosts,
            transformPrediction,
            interIntraAbove,
            interIntraLeft,
            firstIntermediate,
            secondIntermediate,
            compoundMask,
            in partitionEdges,
            in transformEdges,
            in paletteEdges,
            in lumaCoefficientEdges,
            in blueCoefficientEdges,
            in redCoefficientEdges,
            entropyCodingContext.MacroBlock,
            blockOrigin,
            blockSize,
            preparedPartition);

        superblock.CodingUnitPartitionTypes[currentPartitionIndex] = (byte)partition;
        Av1BlockSize subSize = partition.GetBlockSubSize(blockSize);
        int halfBlockSize = blockSize.GetWidth() >> 1;
        int quarterBlockSize = blockSize.GetWidth() >> 2;

        // The real-time search encodes through nonrd_use_partition(), which never adapts the partition
        // distributions, so its partition costs stay at the frame context. Only the packed partition adapts them.
        // Reference: the partition update_cdf() of encode_sb(), which the non-RD path does not call.
        if (TOperation.WritesOutput || !pcs.Parent.SpeedSettings.IsRealtime)
        {
            EncodePartition<TOperation>(ref output, pcs, writer, blockSize, partition, blockOrigin, in partitionEdges);
        }

        switch (partition)
        {
            case Av1PartitionType.None:
                WriteFinalBlock<TOperation, TBlockEncoder, TSample>(
                    ref output,
                    pcs,
                    entropyCodingContext,
                    writer,
                    in tables,
                    in modeWorkspace,
                    transformCoefficients,
                    dequantizedCoefficients,
                    searchDequantizedCoefficients,
                    transformWorkspace,
                    transformTypeProbabilities,
                    blockResidual,
                    searchCoefficients,
                    searchReconstructions,
                    estimationRowCoefficients,
                    in interWorkspace,
                    motionSearchPrediction,
                    in motionVectorCosts,
                    transformPrediction,
                    interIntraAbove,
                    interIntraLeft,
                    firstIntermediate,
                    secondIntermediate,
                    compoundMask,
                    in transformEdges,
                    in paletteEdges,
                    in lumaCoefficientEdges,
                    in blueCoefficientEdges,
                    in redCoefficientEdges,
                    superblock,
                    coefficientBuffer,
                    tileIndex,
                    blockOrigin,
                    ref finalBlockIndex,
                    ref blockEncoder);

                break;
            case Av1PartitionType.Horizontal:
                WriteFinalBlock<TOperation, TBlockEncoder, TSample>(
                    ref output,
                    pcs,
                    entropyCodingContext,
                    writer,
                    in tables,
                    in modeWorkspace,
                    transformCoefficients,
                    dequantizedCoefficients,
                    searchDequantizedCoefficients,
                    transformWorkspace,
                    transformTypeProbabilities,
                    blockResidual,
                    searchCoefficients,
                    searchReconstructions,
                    estimationRowCoefficients,
                    in interWorkspace,
                    motionSearchPrediction,
                    in motionVectorCosts,
                    transformPrediction,
                    interIntraAbove,
                    interIntraLeft,
                    firstIntermediate,
                    secondIntermediate,
                    compoundMask,
                    in transformEdges,
                    in paletteEdges,
                    in lumaCoefficientEdges,
                    in blueCoefficientEdges,
                    in redCoefficientEdges,
                    superblock,
                    coefficientBuffer,
                    tileIndex,
                    blockOrigin,
                    ref finalBlockIndex,
                    ref blockEncoder);

                if (modeInfoRow + (blockSize.Get4x4HighCount() >> 1) < common.ModeInfoRowCount)
                {
                    WriteFinalBlock<TOperation, TBlockEncoder, TSample>(
                        ref output,
                        pcs,
                        entropyCodingContext,
                        writer,
                        in tables,
                        in modeWorkspace,
                        transformCoefficients,
                        dequantizedCoefficients,
                        searchDequantizedCoefficients,
                        transformWorkspace,
                        transformTypeProbabilities,
                        blockResidual,
                        searchCoefficients,
                        searchReconstructions,
                        estimationRowCoefficients,
                        in interWorkspace,
                        motionSearchPrediction,
                        in motionVectorCosts,
                        transformPrediction,
                        interIntraAbove,
                        interIntraLeft,
                        firstIntermediate,
                        secondIntermediate,
                        compoundMask,
                        in transformEdges,
                        in paletteEdges,
                        in lumaCoefficientEdges,
                        in blueCoefficientEdges,
                        in redCoefficientEdges,
                        superblock,
                        coefficientBuffer,
                        tileIndex,
                        blockOrigin + new Size(0, halfBlockSize),
                        ref finalBlockIndex,
                        ref blockEncoder);
                }

                break;
            case Av1PartitionType.Vertical:
                WriteFinalBlock<TOperation, TBlockEncoder, TSample>(
                    ref output,
                    pcs,
                    entropyCodingContext,
                    writer,
                    in tables,
                    in modeWorkspace,
                    transformCoefficients,
                    dequantizedCoefficients,
                    searchDequantizedCoefficients,
                    transformWorkspace,
                    transformTypeProbabilities,
                    blockResidual,
                    searchCoefficients,
                    searchReconstructions,
                    estimationRowCoefficients,
                    in interWorkspace,
                    motionSearchPrediction,
                    in motionVectorCosts,
                    transformPrediction,
                    interIntraAbove,
                    interIntraLeft,
                    firstIntermediate,
                    secondIntermediate,
                    compoundMask,
                    in transformEdges,
                    in paletteEdges,
                    in lumaCoefficientEdges,
                    in blueCoefficientEdges,
                    in redCoefficientEdges,
                    superblock,
                    coefficientBuffer,
                    tileIndex,
                    blockOrigin,
                    ref finalBlockIndex,
                    ref blockEncoder);

                if (modeInfoColumn + (blockSize.Get4x4WideCount() >> 1) < common.ModeInfoColumnCount)
                {
                    WriteFinalBlock<TOperation, TBlockEncoder, TSample>(
                        ref output,
                        pcs,
                        entropyCodingContext,
                        writer,
                        in tables,
                        in modeWorkspace,
                        transformCoefficients,
                        dequantizedCoefficients,
                        searchDequantizedCoefficients,
                        transformWorkspace,
                        transformTypeProbabilities,
                        blockResidual,
                        searchCoefficients,
                        searchReconstructions,
                        estimationRowCoefficients,
                        in interWorkspace,
                        motionSearchPrediction,
                        in motionVectorCosts,
                        transformPrediction,
                        interIntraAbove,
                        interIntraLeft,
                        firstIntermediate,
                        secondIntermediate,
                        compoundMask,
                        in transformEdges,
                        in paletteEdges,
                        in lumaCoefficientEdges,
                        in blueCoefficientEdges,
                        in redCoefficientEdges,
                        superblock,
                        coefficientBuffer,
                        tileIndex,
                        blockOrigin + new Size(halfBlockSize, 0),
                        ref finalBlockIndex,
                        ref blockEncoder);
                }

                break;
            case Av1PartitionType.Split:
                if (blockSize == Av1BlockSize.Block8x8)
                {
                    // A split 8x8 node terminates in four 4x4 coding blocks. AV1 does not carry another
                    // partition symbol at that size, so the children are final blocks rather than tree nodes.
                    for (int childIndex = 0; childIndex < 4; childIndex++)
                    {
                        Point childOrigin = blockOrigin + new Size(
                            (childIndex & 1) * halfBlockSize,
                            (childIndex >> 1) * halfBlockSize);

                        Point childModeInfoPosition = new(childOrigin.X >> Av1Constants.ModeInfoSizeLog2, childOrigin.Y >> Av1Constants.ModeInfoSizeLog2);
                        if (childModeInfoPosition.Y >= common.ModeInfoRowCount ||
                            childModeInfoPosition.X >= common.ModeInfoColumnCount)
                        {
                            continue;
                        }

                        WriteFinalBlock<TOperation, TBlockEncoder, TSample>(
                            ref output,
                            pcs,
                            entropyCodingContext,
                            writer,
                            in tables,
                            in modeWorkspace,
                            transformCoefficients,
                            dequantizedCoefficients,
                            searchDequantizedCoefficients,
                            transformWorkspace,
                            transformTypeProbabilities,
                            blockResidual,
                            searchCoefficients,
                            searchReconstructions,
                            estimationRowCoefficients,
                            in interWorkspace,
                            motionSearchPrediction,
                            in motionVectorCosts,
                            transformPrediction,
                            interIntraAbove,
                            interIntraLeft,
                            firstIntermediate,
                            secondIntermediate,
                            compoundMask,
                            in transformEdges,
                            in paletteEdges,
                            in lumaCoefficientEdges,
                            in blueCoefficientEdges,
                            in redCoefficientEdges,
                            superblock,
                            coefficientBuffer,
                            tileIndex,
                            childOrigin,
                            ref finalBlockIndex,
                            ref blockEncoder);
                    }
                }
                else
                {
                    WritePartitionTree<TOperation, TBlockEncoder, TSample>(
                        ref output,
                        pcs,
                        entropyCodingContext,
                        writer,
                        in tables,
                        in modeWorkspace,
                        transformCoefficients,
                        dequantizedCoefficients,
                        searchDequantizedCoefficients,
                        transformWorkspace,
                        transformTypeProbabilities,
                        blockResidual,
                        searchCoefficients,
                        searchReconstructions,
                        estimationRowCoefficients,
                        in interWorkspace,
                        motionSearchPrediction,
                        in motionVectorCosts,
                        transformPrediction,
                        interIntraAbove,
                        interIntraLeft,
                        firstIntermediate,
                        secondIntermediate,
                        compoundMask,
                        in partitionEdges,
                        in transformEdges,
                        in paletteEdges,
                        in lumaCoefficientEdges,
                        in blueCoefficientEdges,
                        in redCoefficientEdges,
                        superblock,
                        coefficientBuffer,
                        tileIndex,
                        subSize,
                        blockOrigin,
                        ref partitionIndex,
                        ref finalBlockIndex,
                        ref blockEncoder);

                    WritePartitionTree<TOperation, TBlockEncoder, TSample>(
                        ref output,
                        pcs,
                        entropyCodingContext,
                        writer,
                        in tables,
                        in modeWorkspace,
                        transformCoefficients,
                        dequantizedCoefficients,
                        searchDequantizedCoefficients,
                        transformWorkspace,
                        transformTypeProbabilities,
                        blockResidual,
                        searchCoefficients,
                        searchReconstructions,
                        estimationRowCoefficients,
                        in interWorkspace,
                        motionSearchPrediction,
                        in motionVectorCosts,
                        transformPrediction,
                        interIntraAbove,
                        interIntraLeft,
                        firstIntermediate,
                        secondIntermediate,
                        compoundMask,
                        in partitionEdges,
                        in transformEdges,
                        in paletteEdges,
                        in lumaCoefficientEdges,
                        in blueCoefficientEdges,
                        in redCoefficientEdges,
                        superblock,
                        coefficientBuffer,
                        tileIndex,
                        subSize,
                        blockOrigin + new Size(halfBlockSize, 0),
                        ref partitionIndex,
                        ref finalBlockIndex,
                        ref blockEncoder);

                    WritePartitionTree<TOperation, TBlockEncoder, TSample>(
                        ref output,
                        pcs,
                        entropyCodingContext,
                        writer,
                        in tables,
                        in modeWorkspace,
                        transformCoefficients,
                        dequantizedCoefficients,
                        searchDequantizedCoefficients,
                        transformWorkspace,
                        transformTypeProbabilities,
                        blockResidual,
                        searchCoefficients,
                        searchReconstructions,
                        estimationRowCoefficients,
                        in interWorkspace,
                        motionSearchPrediction,
                        in motionVectorCosts,
                        transformPrediction,
                        interIntraAbove,
                        interIntraLeft,
                        firstIntermediate,
                        secondIntermediate,
                        compoundMask,
                        in partitionEdges,
                        in transformEdges,
                        in paletteEdges,
                        in lumaCoefficientEdges,
                        in blueCoefficientEdges,
                        in redCoefficientEdges,
                        superblock,
                        coefficientBuffer,
                        tileIndex,
                        subSize,
                        blockOrigin + new Size(0, halfBlockSize),
                        ref partitionIndex,
                        ref finalBlockIndex,
                        ref blockEncoder);

                    WritePartitionTree<TOperation, TBlockEncoder, TSample>(
                        ref output,
                        pcs,
                        entropyCodingContext,
                        writer,
                        in tables,
                        in modeWorkspace,
                        transformCoefficients,
                        dequantizedCoefficients,
                        searchDequantizedCoefficients,
                        transformWorkspace,
                        transformTypeProbabilities,
                        blockResidual,
                        searchCoefficients,
                        searchReconstructions,
                        estimationRowCoefficients,
                        in interWorkspace,
                        motionSearchPrediction,
                        in motionVectorCosts,
                        transformPrediction,
                        interIntraAbove,
                        interIntraLeft,
                        firstIntermediate,
                        secondIntermediate,
                        compoundMask,
                        in partitionEdges,
                        in transformEdges,
                        in paletteEdges,
                        in lumaCoefficientEdges,
                        in blueCoefficientEdges,
                        in redCoefficientEdges,
                        superblock,
                        coefficientBuffer,
                        tileIndex,
                        subSize,
                        blockOrigin + new Size(halfBlockSize, halfBlockSize),
                        ref partitionIndex,
                        ref finalBlockIndex,
                        ref blockEncoder);
                }

                break;
            case Av1PartitionType.HorizontalA:
                WriteFinalBlock<TOperation, TBlockEncoder, TSample>(
                    ref output,
                    pcs,
                    entropyCodingContext,
                    writer,
                    in tables,
                    in modeWorkspace,
                    transformCoefficients,
                    dequantizedCoefficients,
                    searchDequantizedCoefficients,
                    transformWorkspace,
                    transformTypeProbabilities,
                    blockResidual,
                    searchCoefficients,
                    searchReconstructions,
                    estimationRowCoefficients,
                    in interWorkspace,
                    motionSearchPrediction,
                    in motionVectorCosts,
                    transformPrediction,
                    interIntraAbove,
                    interIntraLeft,
                    firstIntermediate,
                    secondIntermediate,
                    compoundMask,
                    in transformEdges,
                    in paletteEdges,
                    in lumaCoefficientEdges,
                    in blueCoefficientEdges,
                    in redCoefficientEdges,
                    superblock,
                    coefficientBuffer,
                    tileIndex,
                    blockOrigin,
                    ref finalBlockIndex,
                    ref blockEncoder);

                WriteFinalBlock<TOperation, TBlockEncoder, TSample>(
                    ref output,
                    pcs,
                    entropyCodingContext,
                    writer,
                    in tables,
                    in modeWorkspace,
                    transformCoefficients,
                    dequantizedCoefficients,
                    searchDequantizedCoefficients,
                    transformWorkspace,
                    transformTypeProbabilities,
                    blockResidual,
                    searchCoefficients,
                    searchReconstructions,
                    estimationRowCoefficients,
                    in interWorkspace,
                    motionSearchPrediction,
                    in motionVectorCosts,
                    transformPrediction,
                    interIntraAbove,
                    interIntraLeft,
                    firstIntermediate,
                    secondIntermediate,
                    compoundMask,
                    in transformEdges,
                    in paletteEdges,
                    in lumaCoefficientEdges,
                    in blueCoefficientEdges,
                    in redCoefficientEdges,
                    superblock,
                    coefficientBuffer,
                    tileIndex,
                    blockOrigin + new Size(halfBlockSize, 0),
                    ref finalBlockIndex,
                    ref blockEncoder);

                WriteFinalBlock<TOperation, TBlockEncoder, TSample>(
                    ref output,
                    pcs,
                    entropyCodingContext,
                    writer,
                    in tables,
                    in modeWorkspace,
                    transformCoefficients,
                    dequantizedCoefficients,
                    searchDequantizedCoefficients,
                    transformWorkspace,
                    transformTypeProbabilities,
                    blockResidual,
                    searchCoefficients,
                    searchReconstructions,
                    estimationRowCoefficients,
                    in interWorkspace,
                    motionSearchPrediction,
                    in motionVectorCosts,
                    transformPrediction,
                    interIntraAbove,
                    interIntraLeft,
                    firstIntermediate,
                    secondIntermediate,
                    compoundMask,
                    in transformEdges,
                    in paletteEdges,
                    in lumaCoefficientEdges,
                    in blueCoefficientEdges,
                    in redCoefficientEdges,
                    superblock,
                    coefficientBuffer,
                    tileIndex,
                    blockOrigin + new Size(0, halfBlockSize),
                    ref finalBlockIndex,
                    ref blockEncoder);

                break;
            case Av1PartitionType.HorizontalB:
                WriteFinalBlock<TOperation, TBlockEncoder, TSample>(
                    ref output,
                    pcs,
                    entropyCodingContext,
                    writer,
                    in tables,
                    in modeWorkspace,
                    transformCoefficients,
                    dequantizedCoefficients,
                    searchDequantizedCoefficients,
                    transformWorkspace,
                    transformTypeProbabilities,
                    blockResidual,
                    searchCoefficients,
                    searchReconstructions,
                    estimationRowCoefficients,
                    in interWorkspace,
                    motionSearchPrediction,
                    in motionVectorCosts,
                    transformPrediction,
                    interIntraAbove,
                    interIntraLeft,
                    firstIntermediate,
                    secondIntermediate,
                    compoundMask,
                    in transformEdges,
                    in paletteEdges,
                    in lumaCoefficientEdges,
                    in blueCoefficientEdges,
                    in redCoefficientEdges,
                    superblock,
                    coefficientBuffer,
                    tileIndex,
                    blockOrigin,
                    ref finalBlockIndex,
                    ref blockEncoder);

                WriteFinalBlock<TOperation, TBlockEncoder, TSample>(
                    ref output,
                    pcs,
                    entropyCodingContext,
                    writer,
                    in tables,
                    in modeWorkspace,
                    transformCoefficients,
                    dequantizedCoefficients,
                    searchDequantizedCoefficients,
                    transformWorkspace,
                    transformTypeProbabilities,
                    blockResidual,
                    searchCoefficients,
                    searchReconstructions,
                    estimationRowCoefficients,
                    in interWorkspace,
                    motionSearchPrediction,
                    in motionVectorCosts,
                    transformPrediction,
                    interIntraAbove,
                    interIntraLeft,
                    firstIntermediate,
                    secondIntermediate,
                    compoundMask,
                    in transformEdges,
                    in paletteEdges,
                    in lumaCoefficientEdges,
                    in blueCoefficientEdges,
                    in redCoefficientEdges,
                    superblock,
                    coefficientBuffer,
                    tileIndex,
                    blockOrigin + new Size(0, halfBlockSize),
                    ref finalBlockIndex,
                    ref blockEncoder);

                WriteFinalBlock<TOperation, TBlockEncoder, TSample>(
                    ref output,
                    pcs,
                    entropyCodingContext,
                    writer,
                    in tables,
                    in modeWorkspace,
                    transformCoefficients,
                    dequantizedCoefficients,
                    searchDequantizedCoefficients,
                    transformWorkspace,
                    transformTypeProbabilities,
                    blockResidual,
                    searchCoefficients,
                    searchReconstructions,
                    estimationRowCoefficients,
                    in interWorkspace,
                    motionSearchPrediction,
                    in motionVectorCosts,
                    transformPrediction,
                    interIntraAbove,
                    interIntraLeft,
                    firstIntermediate,
                    secondIntermediate,
                    compoundMask,
                    in transformEdges,
                    in paletteEdges,
                    in lumaCoefficientEdges,
                    in blueCoefficientEdges,
                    in redCoefficientEdges,
                    superblock,
                    coefficientBuffer,
                    tileIndex,
                    blockOrigin + new Size(halfBlockSize, halfBlockSize),
                    ref finalBlockIndex,
                    ref blockEncoder);

                break;
            case Av1PartitionType.VerticalA:
                WriteFinalBlock<TOperation, TBlockEncoder, TSample>(
                    ref output,
                    pcs,
                    entropyCodingContext,
                    writer,
                    in tables,
                    in modeWorkspace,
                    transformCoefficients,
                    dequantizedCoefficients,
                    searchDequantizedCoefficients,
                    transformWorkspace,
                    transformTypeProbabilities,
                    blockResidual,
                    searchCoefficients,
                    searchReconstructions,
                    estimationRowCoefficients,
                    in interWorkspace,
                    motionSearchPrediction,
                    in motionVectorCosts,
                    transformPrediction,
                    interIntraAbove,
                    interIntraLeft,
                    firstIntermediate,
                    secondIntermediate,
                    compoundMask,
                    in transformEdges,
                    in paletteEdges,
                    in lumaCoefficientEdges,
                    in blueCoefficientEdges,
                    in redCoefficientEdges,
                    superblock,
                    coefficientBuffer,
                    tileIndex,
                    blockOrigin,
                    ref finalBlockIndex,
                    ref blockEncoder);

                WriteFinalBlock<TOperation, TBlockEncoder, TSample>(
                    ref output,
                    pcs,
                    entropyCodingContext,
                    writer,
                    in tables,
                    in modeWorkspace,
                    transformCoefficients,
                    dequantizedCoefficients,
                    searchDequantizedCoefficients,
                    transformWorkspace,
                    transformTypeProbabilities,
                    blockResidual,
                    searchCoefficients,
                    searchReconstructions,
                    estimationRowCoefficients,
                    in interWorkspace,
                    motionSearchPrediction,
                    in motionVectorCosts,
                    transformPrediction,
                    interIntraAbove,
                    interIntraLeft,
                    firstIntermediate,
                    secondIntermediate,
                    compoundMask,
                    in transformEdges,
                    in paletteEdges,
                    in lumaCoefficientEdges,
                    in blueCoefficientEdges,
                    in redCoefficientEdges,
                    superblock,
                    coefficientBuffer,
                    tileIndex,
                    blockOrigin + new Size(0, halfBlockSize),
                    ref finalBlockIndex,
                    ref blockEncoder);

                WriteFinalBlock<TOperation, TBlockEncoder, TSample>(
                    ref output,
                    pcs,
                    entropyCodingContext,
                    writer,
                    in tables,
                    in modeWorkspace,
                    transformCoefficients,
                    dequantizedCoefficients,
                    searchDequantizedCoefficients,
                    transformWorkspace,
                    transformTypeProbabilities,
                    blockResidual,
                    searchCoefficients,
                    searchReconstructions,
                    estimationRowCoefficients,
                    in interWorkspace,
                    motionSearchPrediction,
                    in motionVectorCosts,
                    transformPrediction,
                    interIntraAbove,
                    interIntraLeft,
                    firstIntermediate,
                    secondIntermediate,
                    compoundMask,
                    in transformEdges,
                    in paletteEdges,
                    in lumaCoefficientEdges,
                    in blueCoefficientEdges,
                    in redCoefficientEdges,
                    superblock,
                    coefficientBuffer,
                    tileIndex,
                    blockOrigin + new Size(halfBlockSize, 0),
                    ref finalBlockIndex,
                    ref blockEncoder);

                break;
            case Av1PartitionType.VerticalB:
                WriteFinalBlock<TOperation, TBlockEncoder, TSample>(
                    ref output,
                    pcs,
                    entropyCodingContext,
                    writer,
                    in tables,
                    in modeWorkspace,
                    transformCoefficients,
                    dequantizedCoefficients,
                    searchDequantizedCoefficients,
                    transformWorkspace,
                    transformTypeProbabilities,
                    blockResidual,
                    searchCoefficients,
                    searchReconstructions,
                    estimationRowCoefficients,
                    in interWorkspace,
                    motionSearchPrediction,
                    in motionVectorCosts,
                    transformPrediction,
                    interIntraAbove,
                    interIntraLeft,
                    firstIntermediate,
                    secondIntermediate,
                    compoundMask,
                    in transformEdges,
                    in paletteEdges,
                    in lumaCoefficientEdges,
                    in blueCoefficientEdges,
                    in redCoefficientEdges,
                    superblock,
                    coefficientBuffer,
                    tileIndex,
                    blockOrigin,
                    ref finalBlockIndex,
                    ref blockEncoder);

                WriteFinalBlock<TOperation, TBlockEncoder, TSample>(
                    ref output,
                    pcs,
                    entropyCodingContext,
                    writer,
                    in tables,
                    in modeWorkspace,
                    transformCoefficients,
                    dequantizedCoefficients,
                    searchDequantizedCoefficients,
                    transformWorkspace,
                    transformTypeProbabilities,
                    blockResidual,
                    searchCoefficients,
                    searchReconstructions,
                    estimationRowCoefficients,
                    in interWorkspace,
                    motionSearchPrediction,
                    in motionVectorCosts,
                    transformPrediction,
                    interIntraAbove,
                    interIntraLeft,
                    firstIntermediate,
                    secondIntermediate,
                    compoundMask,
                    in transformEdges,
                    in paletteEdges,
                    in lumaCoefficientEdges,
                    in blueCoefficientEdges,
                    in redCoefficientEdges,
                    superblock,
                    coefficientBuffer,
                    tileIndex,
                    blockOrigin + new Size(halfBlockSize, 0),
                    ref finalBlockIndex,
                    ref blockEncoder);

                WriteFinalBlock<TOperation, TBlockEncoder, TSample>(
                    ref output,
                    pcs,
                    entropyCodingContext,
                    writer,
                    in tables,
                    in modeWorkspace,
                    transformCoefficients,
                    dequantizedCoefficients,
                    searchDequantizedCoefficients,
                    transformWorkspace,
                    transformTypeProbabilities,
                    blockResidual,
                    searchCoefficients,
                    searchReconstructions,
                    estimationRowCoefficients,
                    in interWorkspace,
                    motionSearchPrediction,
                    in motionVectorCosts,
                    transformPrediction,
                    interIntraAbove,
                    interIntraLeft,
                    firstIntermediate,
                    secondIntermediate,
                    compoundMask,
                    in transformEdges,
                    in paletteEdges,
                    in lumaCoefficientEdges,
                    in blueCoefficientEdges,
                    in redCoefficientEdges,
                    superblock,
                    coefficientBuffer,
                    tileIndex,
                    blockOrigin + new Size(halfBlockSize, halfBlockSize),
                    ref finalBlockIndex,
                    ref blockEncoder);

                break;
            case Av1PartitionType.Horizontal4:
                for (int childIndex = 0; childIndex < 4; childIndex++)
                {
                    Point childOrigin = blockOrigin + new Size(0, childIndex * quarterBlockSize);
                    if (childIndex > 0 &&
                        (childOrigin.Y >> Av1Constants.ModeInfoSizeLog2) >= common.ModeInfoRowCount)
                    {
                        break;
                    }

                    WriteFinalBlock<TOperation, TBlockEncoder, TSample>(
                        ref output,
                        pcs,
                        entropyCodingContext,
                        writer,
                        in tables,
                        in modeWorkspace,
                        transformCoefficients,
                        dequantizedCoefficients,
                        searchDequantizedCoefficients,
                        transformWorkspace,
                        transformTypeProbabilities,
                        blockResidual,
                        searchCoefficients,
                        searchReconstructions,
                        estimationRowCoefficients,
                        in interWorkspace,
                        motionSearchPrediction,
                        in motionVectorCosts,
                        transformPrediction,
                        interIntraAbove,
                        interIntraLeft,
                        firstIntermediate,
                        secondIntermediate,
                        compoundMask,
                        in transformEdges,
                        in paletteEdges,
                        in lumaCoefficientEdges,
                        in blueCoefficientEdges,
                        in redCoefficientEdges,
                        superblock,
                        coefficientBuffer,
                        tileIndex,
                        childOrigin,
                        ref finalBlockIndex,
                        ref blockEncoder);
                }

                break;
            case Av1PartitionType.Vertical4:
                for (int childIndex = 0; childIndex < 4; childIndex++)
                {
                    Point childOrigin = blockOrigin + new Size(childIndex * quarterBlockSize, 0);
                    if (childIndex > 0 &&
                        (childOrigin.X >> Av1Constants.ModeInfoSizeLog2) >= common.ModeInfoColumnCount)
                    {
                        break;
                    }

                    WriteFinalBlock<TOperation, TBlockEncoder, TSample>(
                        ref output,
                        pcs,
                        entropyCodingContext,
                        writer,
                        in tables,
                        in modeWorkspace,
                        transformCoefficients,
                        dequantizedCoefficients,
                        searchDequantizedCoefficients,
                        transformWorkspace,
                        transformTypeProbabilities,
                        blockResidual,
                        searchCoefficients,
                        searchReconstructions,
                        estimationRowCoefficients,
                        in interWorkspace,
                        motionSearchPrediction,
                        in motionVectorCosts,
                        transformPrediction,
                        interIntraAbove,
                        interIntraLeft,
                        firstIntermediate,
                        secondIntermediate,
                        compoundMask,
                        in transformEdges,
                        in paletteEdges,
                        in lumaCoefficientEdges,
                        in blueCoefficientEdges,
                        in redCoefficientEdges,
                        superblock,
                        coefficientBuffer,
                        tileIndex,
                        childOrigin,
                        ref finalBlockIndex,
                        ref blockEncoder);
                }

                break;
        }

        // nonrd_use_partition() leaves the partition contexts cleared while it encodes, so real-time partition
        // costs read zero neighbor contexts. Only the packed partitions update them.
        if (TOperation.WritesOutput || !pcs.Parent.SpeedSettings.IsRealtime)
        {
            UpdatePartitionContexts(in partitionEdges, blockOrigin, subSize, blockSize, partition);
        }
    }

    /// <summary>
    /// Writes the next final block selected by partition traversal.
    /// </summary>
    /// <typeparam name="TOperation">Selects whether symbols are written or only adapt the probabilities.</typeparam>
    /// <typeparam name="TBlockEncoder">The value type that produces final-block decisions.</typeparam>
    /// <typeparam name="TSample">The frame's unsigned sample storage type.</typeparam>
    /// <param name="output">The tile buffer that the caller read once. A write that grows the buffer replaces it.</param>
    /// <param name="pcs">The picture coding state.</param>
    /// <param name="entropyCodingContext">The entropy-coding position state for the superblock.</param>
    /// <param name="writer">The tile symbol encoder.</param>
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
    /// <param name="superblock">The encoder decisions for the superblock.</param>
    /// <param name="coefficientBuffer">The transformed coefficients retained by raster-ordered superblock.</param>
    /// <param name="tileIndex">The zero-based tile index.</param>
    /// <param name="blockOrigin">The absolute luma-sample origin of the block.</param>
    /// <param name="finalBlockIndex">The index of the next final block of the superblock, advanced for this block.</param>
    /// <param name="blockEncoder">The handler invoked for each final block.</param>
    private static void WriteFinalBlock<TOperation, TBlockEncoder, TSample>(
        ref Span<byte> output,
        Av1PictureControlSet pcs,
        Av1EntropyCodingContext entropyCodingContext,
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
        Av1Superblock superblock,
        Av1EncoderCoefficientBuffer coefficientBuffer,
        ushort tileIndex,
        Point blockOrigin,
        ref int finalBlockIndex,
        ref TBlockEncoder blockEncoder)
        where TBlockEncoder : struct, IBlockEncodingHandler<TSample>
        where TOperation : struct, Av1SymbolEncoder.ISymbolOperation
        where TSample : unmanaged
    {
        ref Av1EncoderBlockStruct block = ref superblock.FinalBlocks[finalBlockIndex++];
        WriteModesBlock<TOperation, TBlockEncoder, TSample>(
            ref output,
            pcs,
            entropyCodingContext,
            writer,
            in tables,
            in modeWorkspace,
            transformCoefficients,
            dequantizedCoefficients,
            searchDequantizedCoefficients,
            transformWorkspace,
            transformTypeProbabilities,
            blockResidual,
            searchCoefficients,
            searchReconstructions,
            estimationRowCoefficients,
            in interWorkspace,
            motionSearchPrediction,
            in motionVectorCosts,
            transformPrediction,
            interIntraAbove,
            interIntraLeft,
            firstIntermediate,
            secondIntermediate,
            compoundMask,
            in transformEdges,
            in paletteEdges,
            in lumaCoefficientEdges,
            in blueCoefficientEdges,
            in redCoefficientEdges,
            superblock,
            ref block,
            tileIndex,
            blockOrigin,
            coefficientBuffer,
            ref blockEncoder);
    }

    /// <summary>
    /// Publishes the partition contexts produced by one completed partition node.
    /// </summary>
    /// <param name="partitionEdges">The partition context edges of the tile.</param>
    /// <param name="blockOrigin">The origin of the partition node in luma samples.</param>
    /// <param name="subSize">The block size of the partition children.</param>
    /// <param name="blockSize">The block size of the partition node.</param>
    /// <param name="partition">The partition type of the node.</param>
    internal static void UpdatePartitionContexts(
        in Av1NeighborEdges<Av1PartitionContext> partitionEdges,
        Point blockOrigin,
        Av1BlockSize subSize,
        Av1BlockSize blockSize,
        Av1PartitionType partition)
    {
        if (blockSize < Av1BlockSize.Block8x8)
        {
            return;
        }

        int halfBlockSize = blockSize.GetWidth() >> 1;
        Av1BlockSize splitSize = Av1PartitionType.Split.GetBlockSubSize(blockSize);
        switch (partition)
        {
            case Av1PartitionType.Split:
                if (blockSize != Av1BlockSize.Block8x8)
                {
                    return;
                }

                UpdatePartitionContext(in partitionEdges, blockOrigin, subSize, blockSize);
                break;
            case Av1PartitionType.None:
            case Av1PartitionType.Horizontal:
            case Av1PartitionType.Vertical:
            case Av1PartitionType.Horizontal4:
            case Av1PartitionType.Vertical4:
                UpdatePartitionContext(in partitionEdges, blockOrigin, subSize, blockSize);
                break;
            case Av1PartitionType.HorizontalA:
                UpdatePartitionContext(in partitionEdges, blockOrigin, splitSize, subSize);
                UpdatePartitionContext(in partitionEdges, blockOrigin + new Size(0, halfBlockSize), subSize, subSize);
                break;
            case Av1PartitionType.HorizontalB:
                UpdatePartitionContext(in partitionEdges, blockOrigin, subSize, subSize);
                UpdatePartitionContext(in partitionEdges, blockOrigin + new Size(0, halfBlockSize), splitSize, subSize);
                break;
            case Av1PartitionType.VerticalA:
                UpdatePartitionContext(in partitionEdges, blockOrigin, splitSize, subSize);
                UpdatePartitionContext(in partitionEdges, blockOrigin + new Size(halfBlockSize, 0), subSize, subSize);
                break;
            case Av1PartitionType.VerticalB:
                UpdatePartitionContext(in partitionEdges, blockOrigin, subSize, subSize);
                UpdatePartitionContext(in partitionEdges, blockOrigin + new Size(halfBlockSize, 0), splitSize, subSize);
                break;
        }
    }

    /// <summary>
    /// Writes one partition-context value across the complete parent edges.
    /// </summary>
    /// <param name="partitionEdges">The partition context edges of the tile.</param>
    /// <param name="blockOrigin">The origin of the covered area in luma samples.</param>
    /// <param name="contextBlockSize">The block size whose context value is written.</param>
    /// <param name="coveredBlockSize">The block size of the area that the value covers.</param>
    private static void UpdatePartitionContext(
        in Av1NeighborEdges<Av1PartitionContext> partitionEdges,
        Point blockOrigin,
        Av1BlockSize contextBlockSize,
        Av1BlockSize coveredBlockSize)
    {
        Av1PartitionContext context = PartitionContextLookup[(int)contextBlockSize];
        Av1NeighborArrayUnit<Av1PartitionContext>.UnitMask edgeMask =
            Av1NeighborArrayUnit<Av1PartitionContext>.UnitMask.Left |
            Av1NeighborArrayUnit<Av1PartitionContext>.UnitMask.Top;

        partitionEdges.Write(context, blockOrigin, new Size(coveredBlockSize.GetWidth(), coveredBlockSize.GetHeight()), edgeMask);
    }

    /// <summary>
    /// Gets the partition-symbol rate from the above and left contexts available at a block origin.
    /// </summary>
    /// <param name="pcs">The picture coding state.</param>
    /// <param name="writer">The live tile symbol encoder.</param>
    /// <param name="modeCosts">The mode rate tables of the tile.</param>
    /// <param name="blockSize">The square parent block size.</param>
    /// <param name="partitionType">The partition type to measure.</param>
    /// <param name="blockOrigin">The block origin in samples.</param>
    /// <param name="partitionContexts">The partition neighbor arrays for the tile.</param>
    /// <returns>The rate cost in 1/512-bit units.</returns>
    public static int GetPartitionCost(
        Av1PictureControlSet pcs,
        Av1SymbolEncoder writer,
        Av1ModeCosts modeCosts,
        Av1BlockSize blockSize,
        Av1PartitionType partitionType,
        Point blockOrigin,
        in Av1NeighborEdges<Av1PartitionContext> partitionContexts)
    {
        int context = GetPartitionContext(
            pcs,
            blockSize,
            blockOrigin,
            in partitionContexts,
            out bool hasRows,
            out bool hasColumns);

        if (!hasRows && !hasColumns)
        {
            return 0;
        }

        if (hasRows && hasColumns)
        {
            return Av1SymbolEncoder.GetPartitionTypeCost(modeCosts, partitionType, context);
        }

        return !hasRows
            ? writer.GetSplitOrHorizontalCost(partitionType, blockSize, context)
            : writer.GetSplitOrVerticalCost(partitionType, blockSize, context);
    }

    /// <summary>
    /// Writes a partition symbol using the above and left partition contexts available at a block origin.
    /// </summary>
    /// <typeparam name="TOperation">Selects whether symbols are written or only adapt the probabilities.</typeparam>
    /// <param name="output">The tile buffer that the caller read once. A write that grows the buffer replaces it.</param>
    /// <param name="pcs">The picture coding state.</param>
    /// <param name="writer">The tile symbol encoder.</param>
    /// <param name="blockSize">The square parent block size.</param>
    /// <param name="partitionType">The selected partition type.</param>
    /// <param name="blockOrigin">The block origin in samples.</param>
    /// <param name="partition_context_na">The partition edges of the tile, read once by the caller.</param>
    public static void EncodePartition<TOperation>(
        ref Span<byte> output,
        Av1PictureControlSet pcs,
        Av1SymbolEncoder writer,
        Av1BlockSize blockSize,
        Av1PartitionType partitionType,
        Point blockOrigin,
        in Av1NeighborEdges<Av1PartitionContext> partition_context_na)
        where TOperation : struct, Av1SymbolEncoder.ISymbolOperation
    {
        bool is_partition_point = blockSize >= Av1BlockSize.Block8x8;

        if (!is_partition_point)
        {
            return;
        }

        int context_index = GetPartitionContext(
            pcs,
            blockSize,
            blockOrigin,
            in partition_context_na,
            out bool has_rows,
            out bool has_cols);

        if (!has_rows && !has_cols)
        {
            Guard.IsTrue(partitionType == Av1PartitionType.Split, nameof(partitionType), "Partition outside frame boundaries should have Split type.");
            return;
        }

        if (has_rows && has_cols)
        {
            writer.WritePartitionType<TOperation>(ref output, partitionType, context_index);
        }
        else if (!has_rows && has_cols)
        {
            writer.WriteSplitOrHorizontal<TOperation>(ref output, partitionType, blockSize, context_index);
        }
        else
        {
            writer.WriteSplitOrVertical<TOperation>(ref output, partitionType, blockSize, context_index);
        }

        return;
    }

    private static int GetPartitionContext(
        Av1PictureControlSet pcs,
        Av1BlockSize blockSize,
        Point blockOrigin,
        in Av1NeighborEdges<Av1PartitionContext> partitionContexts,
        out bool hasRows,
        out bool hasColumns)
    {
        int halfBlockModeInfoCount = blockSize.Get4x4WideCount() >> 1;
        Point modeInfoPosition = new(blockOrigin.X >> Av1Constants.ModeInfoSizeLog2, blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2);
        hasRows = modeInfoPosition.Y + halfBlockModeInfoCount < pcs.Parent.Common.ModeInfoRowCount;
        hasColumns = modeInfoPosition.X + halfBlockModeInfoCount < pcs.Parent.Common.ModeInfoColumnCount;
        int leftIndex = partitionContexts.GetLeftIndex(blockOrigin);
        int topIndex = partitionContexts.GetTopIndex(blockOrigin);
        byte aboveContext = partitionContexts.Top[topIndex].Above == byte.MaxValue
            ? (byte)0
            : partitionContexts.Top[topIndex].Above;

        byte leftContext = partitionContexts.Left[leftIndex].Left == byte.MaxValue
            ? (byte)0
            : partitionContexts.Left[leftIndex].Left;

        int blockSizeLog2 = blockSize.Get4x4WidthLog2() - 1;
        int above = (aboveContext >> blockSizeLog2) & 1;
        int left = (leftContext >> blockSizeLog2) & 1;

        Guard.IsTrue(blockSize.Get4x4WidthLog2() == blockSize.Get4x4HeightLog2(), nameof(blockSize), "Blocks need to be square.");
        Guard.IsTrue(blockSizeLog2 >= 0, nameof(blockSizeLog2), "bsl needs to be a positive integer.");

        // Each square block-size level owns four contexts selected by the current split bit of its neighbors.
        return ((left * 2) + above) + (blockSizeLog2 * Av1Constants.PartitionProbabilitySet);
    }

    /// <summary>
    /// Writes the segmentation, prediction, transform, coefficient, and filter syntax for one final coding block.
    /// </summary>
    /// <param name="output">The tile buffer that the caller read once. A write that grows the buffer replaces it.</param>
    /// <param name="pcs">The picture coding state.</param>
    /// <param name="entropyCodingContext">The entropy-coding position state for the superblock.</param>
    /// <param name="writer">The tile symbol encoder.</param>
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
    /// <param name="tb_ptr">The containing superblock.</param>
    /// <param name="blk_ptr">The final encoder decisions for the block.</param>
    /// <param name="tile_idx">The zero-based tile index.</param>
    /// <param name="blockOrigin">The absolute luma-sample origin of the block.</param>
    /// <param name="coefficientBuffer">The transformed coefficients retained by raster-ordered superblock.</param>
    /// <param name="blockEncoder">The final-block decision producer.</param>
    /// <typeparam name="TOperation">Selects whether symbols are written or only adapt the probabilities.</typeparam>
    /// <typeparam name="TBlockEncoder">The value type that produces final-block decisions.</typeparam>
    /// <typeparam name="TSample">The frame's unsigned sample storage type.</typeparam>
    private static void WriteModesBlock<TOperation, TBlockEncoder, TSample>(
        ref Span<byte> output,
        Av1PictureControlSet pcs,
        Av1EntropyCodingContext entropyCodingContext,
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
        Av1Superblock tb_ptr,
        ref Av1EncoderBlockStruct blk_ptr,
        ushort tile_idx,
        Point blockOrigin,
        Av1EncoderCoefficientBuffer coefficientBuffer,
        ref TBlockEncoder blockEncoder)
        where TBlockEncoder : struct, IBlockEncodingHandler<TSample>
        where TOperation : struct, Av1SymbolEncoder.ISymbolOperation
        where TSample : unmanaged
    {
        Av1SequenceControlSet scs = pcs.Sequence;
        ObuFrameHeader frm_hdr = pcs.Parent.FrameHeader;
        int mi_row = blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2;
        int mi_col = blockOrigin.X >> Av1Constants.ModeInfoSizeLog2;
        int mi_stride = pcs.Parent.Common.ModeInfoStride;
        Point modeInfoPosition = new(mi_col, mi_row);
        ref Av1MacroBlockModeInfo macroBlockModeInfo = ref pcs.GetMacroBlockModeInfo(modeInfoPosition);
        Av1BlockSize blockSize = macroBlockModeInfo.Block.BlockSize;
        pcs.MapModeInfoBlock(modeInfoPosition, blockSize);
        Av1MacroBlockD macroBlock = entropyCodingContext.MacroBlock;

        Guard.MustBeLessThan((int)blockSize, (int)Av1BlockSize.AllSizes, nameof(blockSize));

        SetModeInfoRowAndColumn(
            pcs,
            macroBlock,
            macroBlock.Tile,
            modeInfoPosition,
            blockSize,
            mi_stride,
            pcs.Parent.Common.ModeInfoRowCount,
            pcs.Parent.Common.ModeInfoColumnCount);

        // Producing the decision here exposes exactly the reconstructed neighbors, coefficient contexts,
        // and adaptive probabilities that the following syntax writes.
        tb_ptr.Workspace.PaletteInfo = default;
        ref Av1EncoderPaletteInfo paletteInfo = ref tb_ptr.Workspace.PaletteInfo;
        blockEncoder.EncodeBlock(
            writer,
            in tables,
            in modeWorkspace,
            transformCoefficients,
            dequantizedCoefficients,
            searchDequantizedCoefficients,
            transformWorkspace,
            transformTypeProbabilities,
            blockResidual,
            searchCoefficients,
            searchReconstructions,
            estimationRowCoefficients,
            in interWorkspace,
            motionSearchPrediction,
            in motionVectorCosts,
            transformPrediction,
            interIntraAbove,
            interIntraLeft,
            firstIntermediate,
            secondIntermediate,
            compoundMask,
            in transformEdges,
            in paletteEdges,
            in lumaCoefficientEdges,
            in blueCoefficientEdges,
            in redCoefficientEdges,
            macroBlock,
            blockOrigin,
            ref macroBlockModeInfo,
            ref blk_ptr,
            ref paletteInfo);

        int allocationOffset = pcs.ModeInfoGrid.Span[(mi_row * mi_stride) + mi_col];
        if (!TOperation.WritesOutput)
        {
            pcs.BlockEncodings.Span[allocationOffset] = blk_ptr;
            if (frm_hdr.AllowScreenContentTools)
            {
                pcs.BlockPalettes.Span[allocationOffset] = paletteInfo;

                // Reference: the palette_pixels count of encode_superblock() for an OUTPUT_ENABLED block.
                if (IsPaletteAllowed(true, blockSize) && paletteInfo.PaletteSizes[0] > 0)
                {
                    pcs.Parent.PalettePixelCount += blockSize.GetWidth() * blockSize.GetHeight();
                }
            }
        }

        bool skipWritingCoefficients = macroBlockModeInfo.Block.Skip;

        // Segmentation, skip, filter, and quantizer syntax precede the prediction-domain branch in both
        // intra and inter frames. Keeping this prefix shared preserves the decoder's symbol order.
        {
            if (pcs.Parent.FrameHeader.SegmentationParameters.Enabled && pcs.Parent.FrameHeader.SegmentationParameters.SegmentIdPrecedesSkip)
            {
                WriteSegmentId<TOperation>(
                    ref output,
                    pcs,
                    writer,
                    blockSize,
                    blockOrigin,
                    macroBlock,
                    ref blk_ptr,
                    skipWritingCoefficients,
                    beforeSkip: true);
            }

            ObuSegmentationParameters segmentation = frm_hdr.SegmentationParameters;
            int segmentId = macroBlockModeInfo.Block.SegmentId;
            bool writesSkipMode = !frm_hdr.IsIntra &&
                frm_hdr.SkipModeParameters.SkipModeFlag &&
                Math.Min(blockSize.GetWidth(), blockSize.GetHeight()) >= 8 &&
                !segmentation.IsFeatureActive(segmentId, ObuSegmentationLevelFeature.Skip) &&
                !segmentation.IsFeatureActive(segmentId, ObuSegmentationLevelFeature.ReferenceFrame) &&
                !segmentation.IsFeatureActive(segmentId, ObuSegmentationLevelFeature.GlobalMotionVector);

            if (writesSkipMode)
            {
                writer.WriteSkipMode<TOperation>(ref output, macroBlockModeInfo.Block.SkipMode, GetSkipModeContext(macroBlock));
            }

            if (!macroBlockModeInfo.Block.SkipMode)
            {
                EncodeSkipCoefficients<TOperation>(ref output, writer, macroBlock, skipWritingCoefficients);
            }

            if (pcs.Parent.FrameHeader.SegmentationParameters.Enabled && !pcs.Parent.FrameHeader.SegmentationParameters.SegmentIdPrecedesSkip)
            {
                WriteSegmentId<TOperation>(
                    ref output,
                    pcs,
                    writer,
                    blockSize,
                    blockOrigin,
                    macroBlock,
                    ref blk_ptr,
                    skipWritingCoefficients,
                    beforeSkip: false);
            }

            WriteCdef<TOperation>(
                ref output,
                scs,
                pcs,
                writer,
                tile_idx,
                skipWritingCoefficients,
                modeInfoPosition);

            if (pcs.Parent.FrameHeader.DeltaQParameters.IsPresent)
            {
                int current_q_index = blk_ptr.QuantizationIndex;
                bool super_block_upper_left = (((blockOrigin.Y >> 2) & (scs.SequenceHeader.SuperblockModeInfoSize - 1)) == 0) &&
                    (((blockOrigin.X >> 2) & (scs.SequenceHeader.SuperblockModeInfoSize - 1)) == 0);

                if ((blockSize != scs.SequenceHeader.SuperblockSize || !skipWritingCoefficients) && super_block_upper_left)
                {
                    Guard.MustBeGreaterThan(current_q_index, 0, nameof(current_q_index));
                    int reduced_delta_qindex = (current_q_index - pcs.Parent.PreviousQIndex.Span[tile_idx]) /
                        frm_hdr.DeltaQParameters.Resolution;

                    writer.WriteDeltaQuantizerIndex<TOperation>(ref output, reduced_delta_qindex);
                    pcs.Parent.PreviousQIndex.Span[tile_idx] = current_q_index;
                }
            }

            bool isInterBlock = macroBlockModeInfo.Block.ReferenceFrame > Av1ReferenceFrameType.Intra;
            bool isGlobalMotionForced = false;
            bool isReferenceForced = false;
            if (!frm_hdr.IsIntra)
            {
                isReferenceForced = segmentation.IsFeatureActive(
                    segmentId,
                    ObuSegmentationLevelFeature.ReferenceFrame);

                isGlobalMotionForced = segmentation.IsFeatureActive(
                    segmentId,
                    ObuSegmentationLevelFeature.GlobalMotionVector);

                if (!macroBlockModeInfo.Block.SkipMode && !isReferenceForced && !isGlobalMotionForced)
                {
                    int intraInterContext = GetIntraInterContext(macroBlock);
                    writer.WriteIsInter<TOperation>(ref output, isInterBlock, intraInterContext);
                }
            }

            Av1PredictionMode lumaMode = macroBlockModeInfo.Block.Mode;
            Av1ChromaPredictionMode intra_chroma_mode = macroBlockModeInfo.Block.UvMode;
            if (isInterBlock)
            {
                if (!macroBlockModeInfo.Block.SkipMode && !isReferenceForced && !isGlobalMotionForced)
                {
                    Span<byte> referenceCounts = stackalloc byte[Av1Constants.ReferenceFrameCount];
                    CollectNeighborReferenceCounts(macroBlock, referenceCounts);
                    if (macroBlockModeInfo.Block.SecondaryReferenceFrame > Av1ReferenceFrameType.Intra)
                    {
                        writer.WriteCompoundReference<TOperation>(
                            ref output,
                            macroBlockModeInfo.Block.ReferenceFrame,
                            macroBlockModeInfo.Block.SecondaryReferenceFrame,
                            Av1SymbolContextHelper.GetReferenceModeContext(macroBlock),
                            Av1SymbolContextHelper.GetCompoundReferenceTypeContext(macroBlock),
                            referenceCounts);
                    }
                    else
                    {
                        if (frm_hdr.ReferenceMode == ObuReferenceMode.ReferenceModeSelect &&
                            Math.Min(blockSize.GetWidth(), blockSize.GetHeight()) >= 8)
                        {
                            writer.WriteIsCompoundReference<TOperation>(ref output, false, Av1SymbolContextHelper.GetReferenceModeContext(macroBlock));
                        }

                        writer.WriteSingleReference<TOperation>(
                            ref output,
                            macroBlockModeInfo.Block.ReferenceFrame,
                            referenceCounts);
                    }
                }

                if (!macroBlockModeInfo.Block.SkipMode && !isGlobalMotionForced)
                {
                    Av1EncoderReferenceContext referenceContext;
                    if (TBlockEncoder.UsesRetainedDecisions)
                    {
                        referenceContext = pcs.ReferenceContexts.Span[allocationOffset];
                    }
                    else
                    {
                        ref Av1ReferenceMotionVectors referenceMotionVectors = ref tb_ptr.Workspace.ReferenceMotionVectors;
                        referenceMotionVectors.Build(
                            pcs,
                            macroBlock,
                            modeInfoPosition,
                            blockSize,
                            macroBlockModeInfo.Block.PartitionType,
                            scs.SequenceHeader,
                            frm_hdr,
                            macroBlockModeInfo.Block.ReferenceFrame,
                            macroBlockModeInfo.Block.SecondaryReferenceFrame);

                        referenceContext = default;
                        referenceContext.Count = (byte)referenceMotionVectors.Count;
                        referenceContext.ModeContext = (ushort)referenceMotionVectors.ModeContext;
                        if (macroBlockModeInfo.Block.SecondaryReferenceFrame > Av1ReferenceFrameType.Intra)
                        {
                            Av1MotionVector secondaryVector = pcs.GetSecondaryDisplacementVector(modeInfoPosition);
                            referenceContext.SecondaryVector = new Av1EncoderDisplacementVector
                            {
                                Row = (short)secondaryVector.Row,
                                Column = (short)secondaryVector.Column
                            };
                        }

                        int candidateCount = Math.Min(4, referenceMotionVectors.Count);
                        referenceMotionVectors.Weights[..candidateCount].CopyTo(referenceContext.Weights);

                        // A stack with no candidates still has a differential fallback vector. Candidate
                        // weights exist only for discovered entries, while reference zero always remains usable.
                        int referenceCount = Math.Max(1, candidateCount);
                        for (int index = 0; index < referenceCount; index++)
                        {
                            bool isCompound = macroBlockModeInfo.Block.SecondaryReferenceFrame > Av1ReferenceFrameType.Intra;
                            Av1MotionVector candidate = isCompound
                                ? referenceMotionVectors.GetCompoundNewReference(index, 0)
                                : referenceMotionVectors.GetNewReference(index);

                            referenceContext.References[index] = new Av1EncoderDisplacementVector
                            {
                                Row = (short)candidate.Row,
                                Column = (short)candidate.Column
                            };

                            if (isCompound)
                            {
                                Av1MotionVector secondaryCandidate = referenceMotionVectors.GetCompoundNewReference(index, 1);
                                referenceContext.SecondaryReferences[index] = new Av1EncoderDisplacementVector
                                {
                                    Row = (short)secondaryCandidate.Row,
                                    Column = (short)secondaryCandidate.Column
                                };
                            }
                        }

                        // Save the contexts before later blocks become visible through the completed frame grid.
                        if (!TOperation.WritesOutput)
                        {
                            pcs.ReferenceContexts.Span[allocationOffset] = referenceContext;
                        }
                    }

                    if (macroBlockModeInfo.Block.SecondaryReferenceFrame > Av1ReferenceFrameType.Intra)
                    {
                        writer.WriteInterCompoundMode<TOperation>(ref output, lumaMode, referenceContext.ModeContext);
                    }
                    else
                    {
                        writer.WriteInterMode<TOperation>(ref output, lumaMode, referenceContext.ModeContext);
                    }

                    int referenceMotionVectorIndex = blk_ptr.ReferenceMotionVectorIndex;
                    if (lumaMode is
                        Av1PredictionMode.NearMotionVector or
                        Av1PredictionMode.NearNearMotionVector or
                        Av1PredictionMode.NearNewMotionVector or
                        Av1PredictionMode.NewNearMotionVector)
                    {
                        // NEARMV reserves stack entry zero for NEARESTMV, so its DRL decisions advance from
                        // near entry zero to one and then from one to two.
                        for (int index = 1; index < 3 && referenceContext.Count > index + 1; index++)
                        {
                            bool advance = referenceMotionVectorIndex >= index;
                            int context = Av1SymbolContextHelper.GetDrlContext(referenceContext.Weights, index);
                            writer.WriteDynamicReferenceList<TOperation>(ref output, advance, context);
                            if (!advance)
                            {
                                break;
                            }
                        }
                    }
                    else if (lumaMode is Av1PredictionMode.NewMotionVector or Av1PredictionMode.NewNewMotionVector)
                    {
                        // NEWMV begins at stack entry zero and can advance through entries one and two.
                        for (int index = 0; index < 2 && referenceContext.Count > index + 1; index++)
                        {
                            bool advance = referenceMotionVectorIndex > index;
                            int context = Av1SymbolContextHelper.GetDrlContext(referenceContext.Weights, index);
                            writer.WriteDynamicReferenceList<TOperation>(ref output, advance, context);
                            if (!advance)
                            {
                                break;
                            }
                        }
                    }

                    int newReferenceIndex = lumaMode is Av1PredictionMode.NearNewMotionVector or Av1PredictionMode.NewNearMotionVector
                        ? referenceMotionVectorIndex + 1
                        : referenceMotionVectorIndex;

                    if (lumaMode is Av1PredictionMode.NewMotionVector or
                        Av1PredictionMode.NewNearestMotionVector or
                        Av1PredictionMode.NewNearMotionVector or
                        Av1PredictionMode.NewNewMotionVector)
                    {
                        Av1MotionVector vector = pcs.GetDisplacementVector(modeInfoPosition);
                        writer.WriteMotionVector<TOperation>(
                            ref output,
                            vector,
                            new Av1MotionVector(
                                referenceContext.References[newReferenceIndex].Row,
                                referenceContext.References[newReferenceIndex].Column),
                            frm_hdr.MotionVectorPrecision);

                        if (TOperation.WritesOutput && pcs.Parent.MotionSearchSettings.AutomaticStepSizeLevel != 0)
                        {
                            // Retain the absolute displacement, not the coded difference from the reference.
                            // Only packed NEWMV syntax contributes to the following frame's search range.
                            int magnitude = Math.Max(Math.Abs(vector.Row), Math.Abs(vector.Column)) >> Av1MotionVector.SubpixelBits;
                            pcs.Parent.MaximumMotionVectorMagnitude = Math.Max(pcs.Parent.MaximumMotionVectorMagnitude, magnitude);
                        }
                    }

                    if (lumaMode is Av1PredictionMode.NearestNewMotionVector or
                        Av1PredictionMode.NearNewMotionVector or
                        Av1PredictionMode.NewNewMotionVector)
                    {
                        Av1MotionVector vector = pcs.GetSecondaryDisplacementVector(modeInfoPosition);
                        writer.WriteMotionVector<TOperation>(
                            ref output,
                            vector,
                            new Av1MotionVector(
                                referenceContext.SecondaryReferences[newReferenceIndex].Row,
                                referenceContext.SecondaryReferences[newReferenceIndex].Column),
                            frm_hdr.MotionVectorPrecision);

                        if (TOperation.WritesOutput && pcs.Parent.MotionSearchSettings.AutomaticStepSizeLevel != 0)
                        {
                            int magnitude = Math.Max(Math.Abs(vector.Row), Math.Abs(vector.Column)) >> Av1MotionVector.SubpixelBits;
                            pcs.Parent.MaximumMotionVectorMagnitude = Math.Max(pcs.Parent.MaximumMotionVectorMagnitude, magnitude);
                        }
                    }

                    if (TOperation.WritesOutput && pcs.Parent.MotionVectorStatistics is Av1MotionVectorStatistics statistics)
                    {
                        // The final blocks are visited in the coding order the reference walks the frame in after
                        // encoding it. Reference: av1_collect_mv_stats().
                        statistics.CollectInterBlock(
                            lumaMode,
                            macroBlockModeInfo.Block.SecondaryReferenceFrame > Av1ReferenceFrameType.Intra,
                            new Av1MotionVector(
                                referenceContext.References[newReferenceIndex].Row,
                                referenceContext.References[newReferenceIndex].Column),
                            pcs.GetDisplacementVector(modeInfoPosition),
                            new Av1MotionVector(
                                referenceContext.SecondaryReferences[newReferenceIndex].Row,
                                referenceContext.SecondaryReferences[newReferenceIndex].Column),
                            pcs.GetSecondaryDisplacementVector(modeInfoPosition),
                            blockOrigin,
                            blockSize);
                    }
                }
                else if (TOperation.WritesOutput)
                {
                    pcs.Parent.MotionVectorStatistics?.CollectInterBlock(
                        lumaMode,
                        macroBlockModeInfo.Block.SecondaryReferenceFrame > Av1ReferenceFrameType.Intra,
                        default,
                        default,
                        default,
                        default,
                        blockOrigin,
                        blockSize);
                }

                if (TOperation.WritesOutput && macroBlockModeInfo.Block.ReferenceFrame == Av1ReferenceFrameType.Last)
                {
                    Av1MotionVector vector = pcs.GetDisplacementVector(modeInfoPosition);
                    if (Math.Abs(vector.Row) < 8 && Math.Abs(vector.Column) < 8)
                    {
                        // Count the retained block once, during packing. Two-row units include a
                        // partial bottom row, while the block width retains its coded geometry.
                        int rows = Math.Min(frm_hdr.ModeInfoRowCount - modeInfoPosition.Y, blockSize.Get4x4HighCount());
                        pcs.Parent.LowMotionArea += ((rows + 1) & ~1) * blockSize.Get4x4WideCount();
                    }
                }

                if (macroBlockModeInfo.Block.SecondaryReferenceFrame > Av1ReferenceFrameType.Intra &&
                    !macroBlockModeInfo.Block.SkipMode)
                {
                    bool maskedCompoundEnabled = scs.SequenceHeader.EnableMaskedCompound &&
                        Math.Min(blockSize.GetWidth(), blockSize.GetHeight()) >= 8;

                    bool jointCompoundEnabled = scs.SequenceHeader.OrderHintInfo.EnableJointCompound;
                    int compoundIndexContext = Av1SymbolContextHelper.GetCompoundIndexContext(
                        scs.SequenceHeader.OrderHintInfo,
                        frm_hdr,
                        macroBlockModeInfo.Block.ReferenceFrame,
                        macroBlockModeInfo.Block.SecondaryReferenceFrame,
                        macroBlock);

                    writer.WriteCompoundBlend<TOperation>(
                        ref output,
                        blockSize,
                        macroBlockModeInfo.Block.CompoundType,
                        GetCompoundGroupIndexContext(macroBlock),
                        compoundIndexContext,
                        macroBlockModeInfo.Block.CompoundWedgeIndex,
                        macroBlockModeInfo.Block.CompoundWedgeSign,
                        macroBlockModeInfo.Block.DifferenceWeightedMaskType,
                        maskedCompoundEnabled,
                        jointCompoundEnabled);
                }
                else if (!macroBlockModeInfo.Block.SkipMode &&
                    scs.SequenceHeader.EnableInterIntraCompound &&
                    blockSize is >= Av1BlockSize.Block8x8 and <= Av1BlockSize.Block32x32)
                {
                    bool interIntra = macroBlockModeInfo.Block.SecondaryReferenceFrame == Av1ReferenceFrameType.Intra;
                    writer.WriteInterIntra<TOperation>(
                        ref output,
                        blockSize,
                        interIntra,
                        macroBlockModeInfo.Block.InterIntraMode,
                        macroBlockModeInfo.Block.UseInterIntraWedge,
                        macroBlockModeInfo.Block.InterIntraWedgeIndex);
                }

                if (macroBlockModeInfo.Block.SecondaryReferenceFrame == Av1ReferenceFrameType.None)
                {
                    // The mode is coded whenever the frame and the neighbors allow another one.
                    // Reference: write_motion_mode().
                    Av1MotionMode lastAllowedMode = Av1EncoderMotionVariation.GetLastAllowedMotionMode(
                        pcs,
                        macroBlock,
                        modeInfoPosition,
                        macroBlockModeInfo.Block);

                    writer.WriteMotionMode<TOperation>(ref output, blockSize, lastAllowedMode, macroBlockModeInfo.Block.MotionMode);
                    if (TOperation.WritesOutput && lastAllowedMode == Av1MotionMode.Warped)
                    {
                        pcs.Parent.WarpedUsage[macroBlockModeInfo.Block.MotionMode == Av1MotionMode.Warped ? 1 : 0]++;
                    }

                    // Reference: the obmc_used count of encode_superblock().
                    if (TOperation.WritesOutput && lastAllowedMode >= Av1MotionMode.Obmc)
                    {
                        pcs.Parent.ObmcUsage[((int)blockSize * 2) + (macroBlockModeInfo.Block.MotionMode == Av1MotionMode.Obmc ? 1 : 0)]++;
                    }
                }

                if (UsesSwitchableInterpolation(frm_hdr, macroBlockModeInfo.Block))
                {
                    // The vertical symbol is first and supplies both axes unless the sequence enables dual filters.
                    int verticalContext = Av1SymbolContextHelper.GetSwitchableInterpolationContext(
                        macroBlockModeInfo.Block,
                        macroBlock,
                        direction: 0);

                    writer.WriteSwitchableInterpolationFilter<TOperation>(ref output, macroBlockModeInfo.Block.VerticalInterpolationFilter, verticalContext);
                    if (TOperation.WritesOutput)
                    {
                        pcs.Parent.SelectedInterpolationCounts.Span[(int)macroBlockModeInfo.Block.VerticalInterpolationFilter]++;
                    }
                    else
                    {
                        pcs.Parent.InterpolationCounts.Span[
                            (verticalContext * Av1InterpolationProbabilities.FilterCount) + (int)macroBlockModeInfo.Block.VerticalInterpolationFilter]++;
                    }

                    if (scs.SequenceHeader.EnableDualFilter)
                    {
                        int horizontalContext = Av1SymbolContextHelper.GetSwitchableInterpolationContext(
                            macroBlockModeInfo.Block,
                            macroBlock,
                            direction: 1);

                        writer.WriteSwitchableInterpolationFilter<TOperation>(
                            ref output, macroBlockModeInfo.Block.HorizontalInterpolationFilter, horizontalContext);

                        if (TOperation.WritesOutput)
                        {
                            pcs.Parent.SelectedInterpolationCounts.Span[(int)macroBlockModeInfo.Block.HorizontalInterpolationFilter]++;
                        }
                        else
                        {
                            pcs.Parent.InterpolationCounts.Span[
                                (horizontalContext * Av1InterpolationProbabilities.FilterCount) + (int)macroBlockModeInfo.Block.HorizontalInterpolationFilter]++;
                        }
                    }
                }
            }
            else if (IsIntraBlockCopyAllowed(pcs.Parent.FrameHeader/*, pcs.Parent.SliceType*/))
            {
                WriteIntraBlockCopyInfo<TOperation>(
                    ref output,
                    pcs,
                    writer,
                    macroBlock,
                    modeInfoPosition,
                    macroBlockModeInfo,
                    TBlockEncoder.UsesRetainedDecisions);
            }

            if (TOperation.WritesOutput && !isInterBlock)
            {
                pcs.Parent.MotionVectorStatistics?.CollectIntraBlock();
            }

            if (!isInterBlock && !macroBlockModeInfo.Block.UseIntraBlockCopy)
            {
                EncodeIntraLumaMode<TOperation>(
                    ref output,
                    writer,
                    frm_hdr,
                    macroBlockModeInfo,
                    macroBlock,
                    ref blk_ptr,
                    blockSize,
                    lumaMode);
            }

            if (!isInterBlock && !macroBlockModeInfo.Block.UseIntraBlockCopy)
            {
                if (blk_ptr.HasChroma)
                {
                    EncodeIntraChromaMode<TOperation>(
                        ref output,
                        writer,
                        frm_hdr,
                        scs.SequenceHeader.ColorConfig,
                        macroBlockModeInfo,
                        ref blk_ptr,
                        blockSize,
                        lumaMode,
                        intra_chroma_mode);
                }
            }

            bool paletteAllowed = !isInterBlock &&
                !macroBlockModeInfo.Block.UseIntraBlockCopy &&
                IsPaletteAllowed(frm_hdr.AllowScreenContentTools, blockSize);

            if (paletteAllowed)
            {
                WritePaletteModeInfo<TOperation>(
                    ref output,
                    scs,
                    writer,
                    macroBlock,
                    macroBlockModeInfo,
                    ref paletteInfo,
                    blockSize,
                    blockOrigin,
                    in paletteEdges,
                    blk_ptr.HasChroma);
            }

            if (!isInterBlock &&
                !macroBlockModeInfo.Block.UseIntraBlockCopy &&
                IsFilterIntraAllowed(
                    scs.SequenceHeader.EnableFilterIntra,
                    blockSize,
                    paletteInfo.PaletteSizes[0],
                    lumaMode))
            {
                writer.WriteFilterIntraMode<TOperation>(ref output, blk_ptr.FilterIntraMode, blockSize);
            }

            if (paletteAllowed)
            {
                ObuColorConfig colorConfig = scs.SequenceHeader.ColorConfig;
                int palettePlaneCount = Math.Min(2, colorConfig.PlaneCount);
                for (int plane = 0; plane < palettePlaneCount; ++plane)
                {
                    int paletteSize = paletteInfo.PaletteSizes[plane];
                    if (paletteSize == 0)
                    {
                        continue;
                    }

                    Av1PlaneType planeType = (Av1PlaneType)plane;
                    int subX = planeType == Av1PlaneType.Uv && colorConfig.SubSamplingX ? 1 : 0;
                    int subY = planeType == Av1PlaneType.Uv && colorConfig.SubSamplingY ? 1 : 0;
                    int blockWidth = blockSize.GetWidth();
                    int blockHeight = blockSize.GetHeight();

                    // A chroma plane narrower or shorter than four samples belongs to a block that shares
                    // its chroma with the neighbour it pairs with, so its map covers the pair.
                    // Reference: av1_get_block_dimensions().
                    int planeBlockWidth = blockWidth >> subX;
                    int planeBlockHeight = blockHeight >> subY;
                    int chromaSub8Width = planeType == Av1PlaneType.Uv && planeBlockWidth < 4 ? 2 : 0;
                    int chromaSub8Height = planeType == Av1PlaneType.Uv && planeBlockHeight < 4 ? 2 : 0;
                    int planeWidth = planeBlockWidth + chromaSub8Width;
                    int planeHeight = planeBlockHeight + chromaSub8Height;

                    // Palette syntax covers coded alignment samples too. Visible-frame clipping would omit symbols
                    // that the decoder consumes before transform syntax and corrupt the remainder of the tile.
                    int columns = ((blockWidth + (Math.Min(0, macroBlock.ToRightEdge) >> 3)) >> subX) + chromaSub8Width;
                    int rows = ((blockHeight + (Math.Min(0, macroBlock.ToBottomEdge) >> 3)) >> subY) + chromaSub8Height;
                    int tokenCount = rows * columns;
                    if (TBlockEncoder.UsesRetainedDecisions)
                    {
                        writer.WritePaletteTokens(
                            ref output,
                            paletteSize,
                            planeType,
                            pcs.PaletteTokens.Span.Slice(entropyCodingContext.PaletteTokenOffset, tokenCount));
                    }
                    else
                    {
                        Av1PlaneRegion<byte> colorIndexMap = tb_ptr.Workspace
                            .GetPaletteMaps()
                            .GetMap(planeType, planeWidth, planeHeight);

                        if (TOperation.WritesOutput)
                        {
                            writer.WritePaletteColorMap<TOperation>(ref output, paletteSize, planeType, rows, columns, colorIndexMap);
                        }
                        else
                        {
                            writer.TokenizePaletteColorMap(
                                paletteSize,
                                planeType,
                                rows,
                                columns,
                                colorIndexMap,
                                pcs.PaletteTokens.Span.Slice(entropyCodingContext.PaletteTokenOffset, tokenCount));
                        }
                    }

                    entropyCodingContext.PaletteTokenOffset += tokenCount;
                }
            }

            WriteTransformSize<TOperation>(ref output, pcs, writer, ref macroBlockModeInfo, macroBlock, blockSize, blockOrigin, in transformEdges);

            entropyCodingContext.MacroBlockModeInfo = macroBlockModeInfo;
            if (!skipWritingCoefficients)
            {
                EncodeTransformCoefficientRegions<TOperation>(
                    ref output,
                    pcs,
                    entropyCodingContext,
                    writer,
                    ref blk_ptr,
                    blockOrigin,
                    lumaMode,
                    blockSize,
                    coefficientBuffer,
                    tb_ptr.Index,
                    in lumaCoefficientEdges,
                    in blueCoefficientEdges,
                    in redCoefficientEdges,
                    TBlockEncoder.UsesRetainedDecisions);
            }
        }

        // Palette colors are published only after the current block's mode and map have consumed the preceding edges.
        if (frm_hdr.AllowScreenContentTools)
        {
            const Av1NeighborArrayUnit<Av1EncoderPaletteInfo>.UnitMask PaletteContextMask =
                Av1NeighborArrayUnit<Av1EncoderPaletteInfo>.UnitMask.Left |
                Av1NeighborArrayUnit<Av1EncoderPaletteInfo>.UnitMask.Top;

            paletteEdges.Write(paletteInfo, blockOrigin, new Size(blockSize.GetWidth(), blockSize.GetHeight()), PaletteContextMask);
        }

        // Coefficient neighbor state follows the same post-symbol ownership boundary.
        UpdateNeighbors(
            pcs,
            entropyCodingContext,
            blockOrigin,
            ref blk_ptr,
            ref macroBlockModeInfo,
            in lumaCoefficientEdges,
            in blueCoefficientEdges,
            in redCoefficientEdges,
            blockSize);
    }

    /// <summary>
    /// Derives the uniform intra transform-size context from the current above and left edges.
    /// </summary>
    /// <param name="transformContexts">The retained transform widths and heights, read once by the caller.</param>
    /// <param name="macroBlock">The reusable macroblock edge and neighbor state.</param>
    /// <param name="blockOrigin">The block origin in samples.</param>
    /// <param name="blockSize">The block size defining the maximum transform.</param>
    /// <returns>The uniform transform-size context.</returns>
    public static int GetTransformSizeContext(
        in Av1NeighborEdges<byte> transformContexts,
        Av1MacroBlockD macroBlock,
        Point blockOrigin,
        Av1BlockSize blockSize)
    {
        Av1TransformSize maximumTransformSize = blockSize.GetMaximumTransformSize();
        int above = transformContexts.Top[transformContexts.GetTopIndex(blockOrigin)] >= maximumTransformSize.GetWidth() ? 1 : 0;
        int left = transformContexts.Left[transformContexts.GetLeftIndex(blockOrigin)] >= maximumTransformSize.GetHeight() ? 1 : 0;

        // Inter neighbors contribute their coding-block extent, not their residual-transform extent.
        if (macroBlock.IsUpAvailable)
        {
            ref Av1MacroBlockModeInfo aboveModeInfo =
                ref macroBlock.GetRelativeModeInfo(-macroBlock.ModeInfoStride);

            if (aboveModeInfo.Block.ReferenceFrame > Av1ReferenceFrameType.Intra ||
                aboveModeInfo.Block.UseIntraBlockCopy)
            {
                above = aboveModeInfo.Block.BlockSize.GetWidth() >= maximumTransformSize.GetWidth() ? 1 : 0;
            }
        }

        if (macroBlock.IsLeftAvailable)
        {
            ref Av1MacroBlockModeInfo leftModeInfo = ref macroBlock.GetRelativeModeInfo(-1);
            if (leftModeInfo.Block.ReferenceFrame > Av1ReferenceFrameType.Intra ||
                leftModeInfo.Block.UseIntraBlockCopy)
            {
                left = leftModeInfo.Block.BlockSize.GetHeight() >= maximumTransformSize.GetHeight() ? 1 : 0;
            }
        }

        return macroBlock.IsUpAvailable
            ? macroBlock.IsLeftAvailable ? above + left : above
            : macroBlock.IsLeftAvailable ? left : 0;
    }

    /// <summary>
    /// Writes or derives the block transform size and publishes its edge contexts.
    /// </summary>
    /// <typeparam name="TOperation">Selects whether symbols are written or only adapt the probabilities.</typeparam>
    /// <param name="output">The tile buffer that the caller read once. A write that grows the buffer replaces it.</param>
    /// <param name="pcs">The picture coding state.</param>
    /// <param name="writer">The tile symbol encoder.</param>
    /// <param name="macroBlockModeInfo">The selected block modes.</param>
    /// <param name="macroBlock">The reusable macroblock edge and neighbor state.</param>
    /// <param name="blockSize">The block size.</param>
    /// <param name="blockOrigin">The block origin in samples.</param>
    /// <param name="transformEdges">The transform size context edges of the tile.</param>
    internal static void WriteTransformSize<TOperation>(
        ref Span<byte> output,
        Av1PictureControlSet pcs,
        Av1SymbolEncoder writer,
        ref Av1MacroBlockModeInfo macroBlockModeInfo,
        Av1MacroBlockD macroBlock,
        Av1BlockSize blockSize,
        Point blockOrigin,
        in Av1NeighborEdges<byte> transformEdges)
        where TOperation : struct, Av1SymbolEncoder.ISymbolOperation
    {
        ObuFrameHeader frameHeader = pcs.Parent.FrameHeader;
        bool isLossless = frameHeader.LosslessArray[macroBlockModeInfo.Block.SegmentId];
        bool isInter = macroBlockModeInfo.Block.ReferenceFrame > Av1ReferenceFrameType.Intra ||
            macroBlockModeInfo.Block.UseIntraBlockCopy;

        bool writesUniformTransformSize = !isLossless &&
            frameHeader.TransformMode == Av1TransformMode.Select &&
            !isInter &&
            blockSize > Av1BlockSize.Block4x4;

        bool writesVariableTransformSize = !isLossless &&
            frameHeader.TransformMode == Av1TransformMode.Select &&
            isInter &&
            !macroBlockModeInfo.Block.Skip &&
            blockSize > Av1BlockSize.Block4x4;

        Av1TransformSize transformSize = isLossless
            ? Av1TransformSize.Size4x4
            : writesUniformTransformSize || writesVariableTransformSize
                ? macroBlockModeInfo.Block.TransformSize
                : blockSize.GetMaximumTransformSize();

        if (!writesVariableTransformSize)
        {
            macroBlockModeInfo.Block.TransformSize = transformSize;
        }

        if (writesUniformTransformSize)
        {
            int context = GetTransformSizeContext(in transformEdges, macroBlock, blockOrigin, blockSize);
            writer.WriteTransformSize<TOperation>(ref output, blockSize, transformSize, context);
        }
        else if (writesVariableTransformSize)
        {
            int maximumBlocksWide = blockSize.Get4x4WideCount() + (Math.Min(0, macroBlock.ToRightEdge) >> 5);
            int maximumBlocksHigh = blockSize.Get4x4HighCount() + (Math.Min(0, macroBlock.ToBottomEdge) >> 5);
            Av1TransformSize rootSize = blockSize.GetMaximumTransformSize();
            for (int row = 0; row < maximumBlocksHigh; row += rootSize.Get4x4HighCount())
            {
                for (int column = 0; column < maximumBlocksWide; column += rootSize.Get4x4WideCount())
                {
                    WriteVariableTransformTree<TOperation>(
                        ref output,
                        writer,
                        in transformEdges,
                        blockOrigin,
                        blockSize,
                        rootSize,
                        ref macroBlockModeInfo.Block,
                        depth: 0,
                        blockRow: row,
                        blockColumn: column,
                        maximumBlocksWide,
                        maximumBlocksHigh);
                }
            }

            // Each coded leaf has already published its own edge dimensions. Replacing those
            // edges with the root size would change the next block's transform partition contexts.
            return;
        }

        Size blockDimensions = new(blockSize.GetWidth(), blockSize.GetHeight());

        // Above entries retain transform widths and left entries retain heights, including rectangular selections.
        transformEdges.Write((byte)transformSize.GetWidth(), blockOrigin, blockDimensions, Av1NeighborArrayUnit<byte>.UnitMask.Top);
        transformEdges.Write((byte)transformSize.GetHeight(), blockOrigin, blockDimensions, Av1NeighborArrayUnit<byte>.UnitMask.Left);
    }

    /// <summary>
    /// Writes the split flags of one node of a variable inter transform tree, then its children, and publishes the
    /// size of each coded leaf on the transform-size edges.
    /// </summary>
    /// <typeparam name="TOperation">Selects whether symbols are written or only adapt the probabilities.</typeparam>
    /// <param name="output">The tile buffer that the caller read once. A write that grows the buffer replaces it.</param>
    /// <param name="writer">The tile symbol encoder.</param>
    /// <param name="transformContexts">The transform-size edges of the tile, read once by the caller.</param>
    /// <param name="blockOrigin">The block origin in samples.</param>
    /// <param name="blockSize">The block size.</param>
    /// <param name="transformSize">The transform size of this node.</param>
    /// <param name="modeInfo">The mode information of the block, which holds the selected transform sizes.</param>
    /// <param name="depth">The split depth of this node.</param>
    /// <param name="blockRow">The node row in four-sample units inside the block.</param>
    /// <param name="blockColumn">The node column in four-sample units inside the block.</param>
    /// <param name="maximumBlocksWide">The visible block width in four-sample units.</param>
    /// <param name="maximumBlocksHigh">The visible block height in four-sample units.</param>
    private static void WriteVariableTransformTree<TOperation>(
        ref Span<byte> output,
        Av1SymbolEncoder writer,
        in Av1NeighborEdges<byte> transformContexts,
        Point blockOrigin,
        Av1BlockSize blockSize,
        Av1TransformSize transformSize,
        ref Av1EncoderBlockModeInfo modeInfo,
        int depth,
        int blockRow,
        int blockColumn,
        int maximumBlocksWide,
        int maximumBlocksHigh)
        where TOperation : struct, Av1SymbolEncoder.ISymbolOperation
    {
        if (blockRow >= maximumBlocksHigh || blockColumn >= maximumBlocksWide)
        {
            return;
        }

        Av1TransformSize selectedTransformSize =
            modeInfo.InterTransformSizes[modeInfo.GetInterTransformSizeIndex(blockRow, blockColumn)];

        bool split = transformSize != selectedTransformSize &&
            transformSize > Av1TransformSize.Size4x4 &&
            depth < Av1Constants.MaxVarTransform;

        int topIndex = transformContexts.GetTopIndex(blockOrigin) + blockColumn;
        int leftIndex = transformContexts.GetLeftIndex(blockOrigin) + blockRow;
        if (transformSize > Av1TransformSize.Size4x4 && depth < Av1Constants.MaxVarTransform)
        {
            int maximumDimension = Math.Max(blockSize.GetWidth(), blockSize.GetHeight());
            Av1TransformSize maximumSquareTransform = maximumDimension switch
            {
                >= 64 => Av1TransformSize.Size64x64,
                >= 32 => Av1TransformSize.Size32x32,
                >= 16 => Av1TransformSize.Size16x16,
                _ => Av1TransformSize.Size8x8
            };

            int category = ((transformSize.GetSquareUpSize() != maximumSquareTransform && maximumSquareTransform > Av1TransformSize.Size8x8) ? 1 : 0) +
                ((((int)Av1TransformSize.SquareSizes - 1) - (int)maximumSquareTransform) * 2);

            int above = transformContexts.Top[topIndex] < transformSize.GetWidth() ? 1 : 0;
            int left = transformContexts.Left[leftIndex] < transformSize.GetHeight() ? 1 : 0;
            writer.WriteTransformPartition<TOperation>(ref output, split, (category * 3) + above + left);
        }

        if (split)
        {
            Av1TransformSize subTransformSize = transformSize.GetSubSize();
            int subWidth = subTransformSize.Get4x4WideCount();
            int subHeight = subTransformSize.Get4x4HighCount();
            for (int row = 0; row < transformSize.Get4x4HighCount(); row += subHeight)
            {
                for (int column = 0; column < transformSize.Get4x4WideCount(); column += subWidth)
                {
                    WriteVariableTransformTree<TOperation>(
                        ref output,
                        writer,
                        in transformContexts,
                        blockOrigin,
                        blockSize,
                        subTransformSize,
                        ref modeInfo,
                        depth + 1,
                        blockRow + row,
                        blockColumn + column,
                        maximumBlocksWide,
                        maximumBlocksHigh);
                }
            }

            return;
        }

        int width = Math.Min(transformSize.Get4x4WideCount(), maximumBlocksWide - blockColumn);
        int height = Math.Min(transformSize.Get4x4HighCount(), maximumBlocksHigh - blockRow);
        transformContexts.Top.Slice(topIndex, width).Fill((byte)transformSize.GetWidth());
        transformContexts.Left.Slice(leftIndex, height).Fill((byte)transformSize.GetHeight());
    }

    /// <summary>
    /// Gets the chroma prediction-mode and directional-angle rate from the mode rates that the caller read once.
    /// </summary>
    /// <param name="modeCosts">The mode rates that the caller read once.</param>
    /// <param name="frameHeader">The current frame syntax and segment lossless state.</param>
    /// <param name="colorConfig">The sequence chroma subsampling configuration.</param>
    /// <param name="macroBlockModeInfo">The selected block modes.</param>
    /// <param name="blockSize">The luma block size.</param>
    /// <param name="lumaMode">The selected luma prediction mode.</param>
    /// <param name="chromaMode">The candidate chroma prediction mode.</param>
    /// <param name="angleDelta">The signed directional-angle adjustment.</param>
    /// <returns>The chroma mode and directional-angle rate in 1/512-bit units.</returns>
    public static int GetChromaModeCost(
        Av1ModeCosts modeCosts,
        ObuFrameHeader frameHeader,
        ObuColorConfig colorConfig,
        Av1MacroBlockModeInfo macroBlockModeInfo,
        Av1BlockSize blockSize,
        Av1PredictionMode lumaMode,
        Av1ChromaPredictionMode chromaMode,
        int angleDelta)
    {
        bool isChromaFromLumaAllowed = IsChromaFromLumaAllowed(
            frameHeader,
            colorConfig,
            macroBlockModeInfo,
            blockSize);

        int cost = Av1SymbolEncoder.GetChromaModeCost(modeCosts, chromaMode, isChromaFromLumaAllowed, lumaMode);
        if (blockSize >= Av1BlockSize.Block8x8 && chromaMode.IsDirectional())
        {
            cost += Av1SymbolEncoder.GetAngleDeltaCost(modeCosts, angleDelta + Av1Constants.MaxAngleDelta, chromaMode.ToLumaMode());
        }

        return cost;
    }

    /// <summary>
    /// Writes the chroma intra mode, chroma-from-luma alpha values, and directional angle adjustment for a block.
    /// </summary>
    /// <typeparam name="TOperation">Selects whether symbols are written or only adapt the probabilities.</typeparam>
    /// <param name="output">The tile buffer that the caller read once. A write that grows the buffer replaces it.</param>
    /// <param name="writer">The tile symbol encoder.</param>
    /// <param name="frameHeader">The current frame syntax and segment lossless state.</param>
    /// <param name="colorConfig">The sequence chroma subsampling configuration.</param>
    /// <param name="macroBlockModeInfo">The selected block modes.</param>
    /// <param name="blk_ptr">The encoder prediction-unit state.</param>
    /// <param name="blockSize">The luma block size.</param>
    /// <param name="lumaMode">The selected luma prediction mode.</param>
    /// <param name="chromaMode">The selected chroma prediction mode.</param>
    public static void EncodeIntraChromaMode<TOperation>(
        ref Span<byte> output,
        Av1SymbolEncoder writer,
        ObuFrameHeader frameHeader,
        ObuColorConfig colorConfig,
        Av1MacroBlockModeInfo macroBlockModeInfo,
        ref Av1EncoderBlockStruct blk_ptr,
        Av1BlockSize blockSize,
        Av1PredictionMode lumaMode,
        Av1ChromaPredictionMode chromaMode)
        where TOperation : struct, Av1SymbolEncoder.ISymbolOperation
    {
        bool isChromaFromLumaAllowed = IsChromaFromLumaAllowed(
            frameHeader,
            colorConfig,
            macroBlockModeInfo,
            blockSize);

        writer.WriteChromaMode<TOperation>(ref output, chromaMode, isChromaFromLumaAllowed, lumaMode);

        if (chromaMode == Av1ChromaPredictionMode.ChromaFromLuma)
        {
            writer.WriteChromaFromLumaAlphas<TOperation>(
                ref output,
                blk_ptr.PredictionUnit.ChromaFromLumaIndex,
                blk_ptr.PredictionUnit.ChromaFromLumaSigns);
        }

        if (blockSize >= Av1BlockSize.Block8x8 && macroBlockModeInfo.Block.UvMode.IsDirectional())
        {
            writer.WriteAngleDelta<TOperation>(
                ref output,
                blk_ptr.PredictionUnit.AngleDelta[(int)Av1PlaneType.Uv] + Av1Constants.MaxAngleDelta,
                chromaMode.ToLumaMode());
        }
    }

    private static bool IsChromaFromLumaAllowed(
        ObuFrameHeader frameHeader,
        ObuColorConfig colorConfig,
        Av1MacroBlockModeInfo macroBlockModeInfo,
        Av1BlockSize blockSize)
        => blockSize.AllowsChromaFromLuma(
            frameHeader.LosslessArray[macroBlockModeInfo.Block.SegmentId],
            colorConfig.SubSamplingX,
            colorConfig.SubSamplingY);

    /// <summary>
    /// Gets the above and left key-frame contexts used to write an intra luma mode.
    /// </summary>
    /// <param name="xd">The current macroblock and its mapped neighbors.</param>
    /// <param name="above_ctx">The context derived from the above luma mode.</param>
    /// <param name="left_ctx">The context derived from the left luma mode.</param>
    public static void GetYModeContext(Av1MacroBlockD xd, out byte above_ctx, out byte left_ctx)
    {
        Av1PredictionMode intraLumaLeftMode = Av1PredictionMode.DC;
        Av1PredictionMode intraLumaTopMode = Av1PredictionMode.DC;
        if (xd.IsLeftAvailable)
        {
            // Key-frame neighbors are intra blocks, so their luma modes directly select the context class.
            intraLumaLeftMode = xd.GetRelativeModeInfo(-1).Block.Mode;
        }

        if (xd.IsUpAvailable)
        {
            intraLumaTopMode = xd.GetRelativeModeInfo(-xd.ModeInfoStride).Block.Mode;
        }

        above_ctx = IntraModeContextLookup[(int)intraLumaTopMode];
        left_ctx = IntraModeContextLookup[(int)intraLumaLeftMode];
    }

    /// <summary>
    /// Gets the luma mode rate from the frame-appropriate distribution.
    /// </summary>
    /// <param name="modeCosts">The mode rates that the caller read once.</param>
    /// <param name="macroBlock">The current block's mapped neighbor state.</param>
    /// <param name="blockSize">The selected block size.</param>
    /// <param name="mode">The candidate luma mode.</param>
    /// <param name="angleDelta">The signed directional-angle adjustment.</param>
    /// <param name="isIntraFrame">Whether the frame uses key-frame neighbor-conditioned mode syntax.</param>
    /// <returns>The luma mode and directional-angle rate in 1/512-bit units.</returns>
    public static int GetLumaModeCost(
        Av1ModeCosts modeCosts,
        Av1MacroBlockD macroBlock,
        Av1BlockSize blockSize,
        Av1PredictionMode mode,
        int angleDelta,
        bool isIntraFrame)
    {
        byte topContext = 0;
        byte leftContext = 0;
        if (isIntraFrame)
        {
            GetYModeContext(macroBlock, out topContext, out leftContext);
        }

        return GetLumaModeCost(modeCosts, blockSize, mode, angleDelta, isIntraFrame, topContext, leftContext);
    }

    /// <summary>
    /// Gets the luma mode rate from the frame-appropriate distribution, with the neighbor contexts that the caller
    /// read once for all modes of the block.
    /// </summary>
    /// <param name="modeCosts">The mode rates that the caller read once.</param>
    /// <param name="blockSize">The selected block size.</param>
    /// <param name="mode">The candidate luma mode.</param>
    /// <param name="angleDelta">The signed directional angle adjustment.</param>
    /// <param name="isIntraFrame">Whether the frame codes luma modes with neighbor contexts.</param>
    /// <param name="topContext">The mode context of the block above, used only in an intra frame.</param>
    /// <param name="leftContext">The mode context of the block to the left, used only in an intra frame.</param>
    /// <returns>The rate of the luma mode and its angle adjustment.</returns>
    public static int GetLumaModeCost(
        Av1ModeCosts modeCosts,
        Av1BlockSize blockSize,
        Av1PredictionMode mode,
        int angleDelta,
        bool isIntraFrame,
        byte topContext,
        byte leftContext)
    {
        int cost = isIntraFrame
            ? Av1SymbolEncoder.GetLumaModeCost(modeCosts, mode, topContext, leftContext)
            : Av1SymbolEncoder.GetInterFrameLumaModeCost(modeCosts, mode, blockSize);

        if (blockSize >= Av1BlockSize.Block8x8 && mode.IsDirectional())
        {
            cost += Av1SymbolEncoder.GetAngleDeltaCost(modeCosts, angleDelta + Av1Constants.MaxAngleDelta, mode);
        }

        return cost;
    }

    /// <summary>
    /// Writes the frame-appropriate luma prediction mode and any directional angle adjustment.
    /// </summary>
    /// <param name="output">The tile buffer that the caller read once. A write that grows the buffer replaces it.</param>
    /// <param name="writer">The tile symbol encoder.</param>
    /// <param name="frameHeader">The frame header that selects the luma-mode probability model.</param>
    /// <param name="macroBlockModeInfo">The selected block modes.</param>
    /// <param name="macroBlock">The reusable macroblock edge and neighbor state.</param>
    /// <param name="blk_ptr">The encoder prediction-unit state.</param>
    /// <param name="blockSize">The block size.</param>
    /// <param name="lumaMode">The selected luma prediction mode.</param>
    /// <typeparam name="TOperation">Selects whether symbols are written or only adapt the probabilities.</typeparam>
    private static void EncodeIntraLumaMode<TOperation>(
        ref Span<byte> output,
        Av1SymbolEncoder writer,
        ObuFrameHeader frameHeader,
        Av1MacroBlockModeInfo macroBlockModeInfo,
        Av1MacroBlockD macroBlock,
        ref Av1EncoderBlockStruct blk_ptr,
        Av1BlockSize blockSize,
        Av1PredictionMode lumaMode)
        where TOperation : struct, Av1SymbolEncoder.ISymbolOperation
    {
        if (frameHeader.IsIntra)
        {
            GetYModeContext(macroBlock, out byte topContext, out byte leftContext);
            writer.WriteLumaMode<TOperation>(ref output, lumaMode, topContext, leftContext);
        }
        else
        {
            writer.WriteInterFrameLumaMode<TOperation>(ref output, lumaMode, blockSize);
        }

        if (blockSize >= Av1BlockSize.Block8x8 && macroBlockModeInfo.Block.Mode.IsDirectional())
        {
            writer.WriteAngleDelta<TOperation>(ref output, blk_ptr.PredictionUnit.AngleDelta[(int)Av1PlaneType.Y] + Av1Constants.MaxAngleDelta, lumaMode);
        }
    }

    /// <summary>
    /// Gets the prediction-domain context from the immediately above and left encoder blocks.
    /// </summary>
    /// <param name="macroBlock">The current block's mapped neighbor state.</param>
    /// <returns>The context in the inclusive range zero through three.</returns>
    public static int GetIntraInterContext(Av1MacroBlockD macroBlock)
    {
        bool hasAbove = macroBlock.IsUpAvailable;
        bool hasLeft = macroBlock.IsLeftAvailable;
        if (hasAbove && hasLeft)
        {
            bool aboveIsIntra = macroBlock
                .GetRelativeModeInfo(-macroBlock.ModeInfoStride)
                .Block.ReferenceFrame <= Av1ReferenceFrameType.Intra;

            bool leftIsIntra = macroBlock
                .GetRelativeModeInfo(-1)
                .Block.ReferenceFrame <= Av1ReferenceFrameType.Intra;

            if (aboveIsIntra && leftIsIntra)
            {
                return 3;
            }

            return aboveIsIntra || leftIsIntra ? 1 : 0;
        }

        if (hasAbove)
        {
            return macroBlock
                .GetRelativeModeInfo(-macroBlock.ModeInfoStride)
                .Block.ReferenceFrame <= Av1ReferenceFrameType.Intra
                    ? 2
                    : 0;
        }

        if (hasLeft)
        {
            return macroBlock
                .GetRelativeModeInfo(-1)
                .Block.ReferenceFrame <= Av1ReferenceFrameType.Intra
                    ? 2
                    : 0;
        }

        return 0;
    }

    /// <summary>
    /// Gets the masked compound context from the immediately above and left encoder blocks.
    /// </summary>
    public static int GetCompoundGroupIndexContext(Av1MacroBlockD macroBlock)
    {
        // Reference: get_comp_group_idx_context(). A single-reference ALTREF neighbor counts 3.
        int context = 0;
        if (macroBlock.IsUpAvailable)
        {
            Av1EncoderBlockModeInfo above = macroBlock.GetRelativeModeInfo(-macroBlock.ModeInfoStride).Block;
            context += above.SecondaryReferenceFrame > Av1ReferenceFrameType.Intra
                ? above.CompoundGroupIndex ? 1 : 0
                : above.ReferenceFrame == Av1ReferenceFrameType.Alternate ? 3 : 0;
        }

        if (macroBlock.IsLeftAvailable)
        {
            Av1EncoderBlockModeInfo left = macroBlock.GetRelativeModeInfo(-1).Block;
            context += left.SecondaryReferenceFrame > Av1ReferenceFrameType.Intra
                ? left.CompoundGroupIndex ? 1 : 0
                : left.ReferenceFrame == Av1ReferenceFrameType.Alternate ? 3 : 0;
        }

        return Math.Min(5, context);
    }

    /// <summary>
    /// Counts the single-reference labels used by the immediately above and left encoded blocks.
    /// </summary>
    /// <param name="macroBlock">The current block's mapped neighbor state.</param>
    /// <param name="referenceCounts">The eight-entry destination indexed by reference-frame label.</param>
    public static void CollectNeighborReferenceCounts(
        Av1MacroBlockD macroBlock,
        Span<byte> referenceCounts)
    {
        // The caller supplies short-lived fixed storage for one block. Clearing it here keeps unavailable
        // neighbors from retaining votes collected for a preceding block.
        referenceCounts.Clear();
        if (macroBlock.IsUpAvailable)
        {
            Av1ReferenceFrameType referenceFrame = macroBlock
                .GetRelativeModeInfo(-macroBlock.ModeInfoStride)
                .Block.ReferenceFrame;

            if (referenceFrame > Av1ReferenceFrameType.Intra)
            {
                referenceCounts[(int)referenceFrame]++;
            }

            Av1ReferenceFrameType secondaryReference = macroBlock
                .GetRelativeModeInfo(-macroBlock.ModeInfoStride)
                .Block.SecondaryReferenceFrame;

            if (secondaryReference > Av1ReferenceFrameType.Intra)
            {
                referenceCounts[(int)secondaryReference]++;
            }
        }

        if (macroBlock.IsLeftAvailable)
        {
            Av1ReferenceFrameType referenceFrame = macroBlock
                .GetRelativeModeInfo(-1)
                .Block.ReferenceFrame;

            if (referenceFrame > Av1ReferenceFrameType.Intra)
            {
                referenceCounts[(int)referenceFrame]++;
            }

            Av1ReferenceFrameType secondaryReference = macroBlock
                .GetRelativeModeInfo(-1)
                .Block.SecondaryReferenceFrame;

            if (secondaryReference > Av1ReferenceFrameType.Intra)
            {
                referenceCounts[(int)secondaryReference]++;
            }
        }
    }

    /// <summary>
    /// Writes luma and chroma palette-mode syntax for a block.
    /// </summary>
    /// <typeparam name="TOperation">Selects whether symbols are written or only adapt the probabilities.</typeparam>
    /// <param name="output">The tile buffer that the caller read once. A write that grows the buffer replaces it.</param>
    /// <param name="scs">The sequence coding state.</param>
    /// <param name="writer">The tile symbol encoder.</param>
    /// <param name="macroBlock">The current block's mapped neighbor state.</param>
    /// <param name="macroBlockModeInfo">The selected block modes.</param>
    /// <param name="paletteInfo">The selected palette sizes and colors.</param>
    /// <param name="blockSize">The block size.</param>
    /// <param name="blockOrigin">The absolute luma-sample origin.</param>
    /// <param name="paletteEdges">The palette color context edges of the tile.</param>
    /// <param name="hasChroma">Whether the block owns chroma syntax.</param>
    internal static void WritePaletteModeInfo<TOperation>(
        ref Span<byte> output,
        Av1SequenceControlSet scs,
        Av1SymbolEncoder writer,
        Av1MacroBlockD macroBlock,
        Av1MacroBlockModeInfo macroBlockModeInfo,
        ref Av1EncoderPaletteInfo paletteInfo,
        Av1BlockSize blockSize,
        Point blockOrigin,
        in Av1NeighborEdges<Av1EncoderPaletteInfo> paletteEdges,
        bool hasChroma)
        where TOperation : struct, Av1SymbolEncoder.ISymbolOperation
    {
        int blockSizeContext = GetPaletteBlockSizeContext(blockSize);
        int yPaletteSize = paletteInfo.PaletteSizes[0];

        // The encoder's own probability pass leaves the luma palette flag and size alone for a block that
        // carries no chroma, because its intra statistics return before the palette update when the block is
        // not a chroma reference. The bitstream codes them either way.
        // Reference: the is_chroma_ref return ahead of update_palette_cdf() in sum_intra_stats().
        ObuColorConfig colorConfig = scs.SequenceHeader.ColorConfig;
        bool updatesLumaPalette = TOperation.WritesOutput ||
            IsChromaReference(blockOrigin, blockSize, colorConfig.SubSamplingX, colorConfig.SubSamplingY);

        if (macroBlockModeInfo.Block.Mode == Av1PredictionMode.DC)
        {
            int neighborContext = GetPaletteYModeContext(in paletteEdges, macroBlock, blockOrigin);
            if (updatesLumaPalette)
            {
                writer.WritePaletteYMode<TOperation>(ref output, yPaletteSize != 0, blockSizeContext, neighborContext);
            }

            if (yPaletteSize != 0)
            {
                if (updatesLumaPalette)
                {
                    writer.WritePaletteSize<TOperation>(ref output, yPaletteSize, blockSizeContext, Av1PlaneType.Y);
                }

                Span<ushort> colorCache = stackalloc ushort[2 * Av1Constants.PaletteMaxSize];
                int cacheSize = GetPaletteCache(in paletteEdges, macroBlock, blockOrigin, Av1Plane.Y, colorCache);

                writer.WritePaletteYColors<TOperation>(
                    ref output,
                    colorCache[..cacheSize],
                    paletteInfo.GetColors(Av1Plane.Y),
                    scs.SequenceHeader.ColorConfig.BitDepth.GetBitCount());
            }
        }

        int uvPaletteSize = paletteInfo.PaletteSizes[1];
        if (scs.SequenceHeader.ColorConfig.PlaneCount > 1 &&
            macroBlockModeInfo.Block.UvMode == Av1ChromaPredictionMode.DC &&
            hasChroma)
        {
            writer.WritePaletteUvMode<TOperation>(ref output, uvPaletteSize != 0, yPaletteSize != 0);
            if (uvPaletteSize != 0)
            {
                writer.WritePaletteSize<TOperation>(ref output, uvPaletteSize, blockSizeContext, Av1PlaneType.Uv);
                Span<ushort> colorCache = stackalloc ushort[2 * Av1Constants.PaletteMaxSize];
                int cacheSize = GetPaletteCache(in paletteEdges, macroBlock, blockOrigin, Av1Plane.U, colorCache);

                writer.WritePaletteUvColors<TOperation>(
                    ref output,
                    colorCache[..cacheSize],
                    paletteInfo.GetColors(Av1Plane.U),
                    paletteInfo.GetColors(Av1Plane.V),
                    scs.SequenceHeader.ColorConfig.BitDepth.GetBitCount());
            }
        }
    }

    /// <summary>
    /// Reports whether a block is the one that carries the chroma of its subsampled area.
    /// Reference: is_chroma_reference().
    /// </summary>
    /// <param name="blockOrigin">The block origin in luma samples.</param>
    /// <param name="blockSize">The block size.</param>
    /// <param name="subsamplingX">Whether chroma is subsampled horizontally.</param>
    /// <param name="subsamplingY">Whether chroma is subsampled vertically.</param>
    /// <returns><see langword="true"/> when the block carries chroma; otherwise, <see langword="false"/>.</returns>
    internal static bool IsChromaReference(Point blockOrigin, Av1BlockSize blockSize, bool subsamplingX, bool subsamplingY)
    {
        int modeInfoRow = blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2;
        int modeInfoColumn = blockOrigin.X >> Av1Constants.ModeInfoSizeLog2;
        int width = blockSize.Get4x4WideCount();
        int height = blockSize.Get4x4HighCount();
        return ((modeInfoRow & 1) != 0 || (height & 1) == 0 || !subsamplingY) &&
            ((modeInfoColumn & 1) != 0 || (width & 1) == 0 || !subsamplingX);
    }

    /// <summary>
    /// Builds the sorted palette-color cache from the available above and left encoder edges.
    /// </summary>
    internal static int GetPaletteCache(
        in Av1NeighborEdges<Av1EncoderPaletteInfo> paletteContexts,
        Av1MacroBlockD macroBlock,
        Point blockOrigin,
        Av1Plane plane,
        Span<ushort> cache)
    {
        // AV1 excludes the above palette at each 64-sample row boundary, even with 128x128 superblocks.
        bool hasAbove = macroBlock.IsUpAvailable &&
            (blockOrigin.Y & (Av1BlockSize.Block64x64.GetHeight() - 1)) != 0;

        Av1EncoderPaletteInfo above = hasAbove
            ? paletteContexts.Top[paletteContexts.GetTopIndex(blockOrigin)]
            : default;

        Av1EncoderPaletteInfo left = macroBlock.IsLeftAvailable
            ? paletteContexts.Left[paletteContexts.GetLeftIndex(blockOrigin)]
            : default;

        ReadOnlySpan<ushort> aboveColors = hasAbove ? above.GetColors(plane) : [];
        ReadOnlySpan<ushort> leftColors = macroBlock.IsLeftAvailable ? left.GetColors(plane) : [];
        return Av1PaletteCache.Merge(aboveColors, leftColors, cache);
    }

    /// <summary>
    /// Gets the palette probability context derived from the logarithmic block area.
    /// </summary>
    internal static int GetPaletteBlockSizeContext(Av1BlockSize blockSize)
        => Av1Math.Log2(blockSize.GetWidth() * blockSize.GetHeight()) - 6;

    /// <summary>
    /// Counts the available above and left luma neighbors that selected palette mode.
    /// </summary>
    internal static int GetPaletteYModeContext(
        in Av1NeighborEdges<Av1EncoderPaletteInfo> paletteContexts,
        Av1MacroBlockD macroBlock,
        Point blockOrigin)
    {
        int neighborContext = 0;
        if (macroBlock.IsUpAvailable &&
            paletteContexts.Top[paletteContexts.GetTopIndex(blockOrigin)].PaletteSizes[0] != 0)
        {
            neighborContext++;
        }

        if (macroBlock.IsLeftAvailable &&
            paletteContexts.Left[paletteContexts.GetLeftIndex(blockOrigin)].PaletteSizes[0] != 0)
        {
            neighborContext++;
        }

        return neighborContext;
    }

    /// <summary>
    /// Determines whether filter-intra syntax is available for a block mode.
    /// </summary>
    /// <param name="enableFilterIntra">A value indicating whether the sequence enables filter-intra prediction.</param>
    /// <param name="blockSize">The block size.</param>
    /// <param name="paletteSize">The selected luma palette size.</param>
    /// <param name="mode">The selected luma prediction mode.</param>
    /// <returns><see langword="true"/> when the block can use filter-intra prediction; otherwise, <see langword="false"/>.</returns>
    private static bool IsFilterIntraAllowed(
        bool enableFilterIntra,
        Av1BlockSize blockSize,
        int paletteSize,
        Av1PredictionMode mode)
        => mode == Av1PredictionMode.DC && paletteSize == 0 && IsFilterIntraAllowedBlockSize(enableFilterIntra, blockSize);

    /// <summary>
    /// Determines whether filter-intra prediction is enabled for a block size.
    /// </summary>
    /// <param name="enableFilterIntra">A value indicating whether the sequence enables filter-intra prediction.</param>
    /// <param name="blockSize">The block size.</param>
    /// <returns><see langword="true"/> when filter-intra prediction supports the block dimensions; otherwise, <see langword="false"/>.</returns>
    internal static bool IsFilterIntraAllowedBlockSize(bool enableFilterIntra, Av1BlockSize blockSize)
    {
        if (!enableFilterIntra)
        {
            return false;
        }

        return blockSize.GetWidth() <= 32 && blockSize.GetHeight() <= 32;
    }

    /// <summary>
    /// Writes the intra-block-copy selection and displacement-vector syntax for a block.
    /// </summary>
    /// <typeparam name="TOperation">Selects whether symbols are written or only adapt the probabilities.</typeparam>
    /// <param name="output">The tile buffer that the caller read once. A write that grows the buffer replaces it.</param>
    /// <param name="picture">The frame-owned mode and displacement state.</param>
    /// <param name="writer">The tile symbol encoder.</param>
    /// <param name="macroBlock">The current block's frame edges and tile availability.</param>
    /// <param name="modeInfoPosition">The block origin in 4x4 mode-information units.</param>
    /// <param name="macroBlockModeInfo">The selected block modes.</param>
    /// <param name="useRetainedContext">Whether the reference vector context comes from completed block analysis.</param>
    public static void WriteIntraBlockCopyInfo<TOperation>(
        ref Span<byte> output,
        Av1PictureControlSet picture,
        Av1SymbolEncoder writer,
        Av1MacroBlockD macroBlock,
        Point modeInfoPosition,
        Av1MacroBlockModeInfo macroBlockModeInfo,
        bool useRetainedContext)
        where TOperation : struct, Av1SymbolEncoder.ISymbolOperation
    {
        bool useIntraBlockCopy = macroBlockModeInfo.Block.UseIntraBlockCopy;
        writer.WriteUseIntraBlockCopy<TOperation>(ref output, useIntraBlockCopy);
        if (useIntraBlockCopy)
        {
            int gridOffset = (modeInfoPosition.Y * picture.ModeInfoStride) + modeInfoPosition.X;
            int allocationOffset = picture.ModeInfoGrid.Span[gridOffset];
            Av1MotionVector reference;
            if (useRetainedContext)
            {
                Av1EncoderDisplacementVector retained = picture.ReferenceContexts.Span[allocationOffset].References[0];
                reference = new Av1MotionVector(retained.Row, retained.Column);
            }
            else
            {
                Span<Av1MotionVector> candidates = stackalloc Av1MotionVector[8];
                Span<int> weights = stackalloc int[8];
                reference = Av1IntraBlockCopy.FindReference(
                    picture,
                    macroBlock,
                    modeInfoPosition,
                    macroBlockModeInfo.Block.BlockSize,
                    macroBlockModeInfo.Block.PartitionType,
                    candidates,
                    weights);

                if (!TOperation.WritesOutput)
                {
                    picture.ReferenceContexts.Span[allocationOffset].References[0] = new Av1EncoderDisplacementVector
                    {
                        Row = (short)reference.Row,
                        Column = (short)reference.Column
                    };
                }
            }

            writer.WriteDisplacementVector<TOperation>(
                ref output,
                picture.GetDisplacementVector(modeInfoPosition),
                reference);
        }
    }

    /// <summary>
    /// Determines whether a single-reference encoder block carries switchable interpolation symbols.
    /// </summary>
    /// <param name="frameHeader">The current frame header.</param>
    /// <param name="modeInfo">The selected block syntax.</param>
    /// <returns>Whether the block writes a vertical filter and, when enabled, a horizontal filter.</returns>
    public static bool UsesSwitchableInterpolation(ObuFrameHeader frameHeader, Av1EncoderBlockModeInfo modeInfo)
    {
        // A warped block filters with the warp filter alone. Reference: av1_is_interp_needed().
        if (frameHeader.InterpolationFilter != Av1InterpolationFilter.Switchable || modeInfo.SkipMode ||
            modeInfo.MotionMode == Av1MotionMode.Warped)
        {
            return false;
        }

        // Global identity and affine models infer the regular filter on blocks at least 8x8, for GLOBALMV and for
        // GLOBAL_GLOBALMV. A translation model on any reference still carries filter symbols, including integer
        // translations. Residual skip does not suppress these symbols. Reference: is_nontrans_global_motion().
        if (modeInfo.Mode is not (Av1PredictionMode.GlobalMotionVector or Av1PredictionMode.GlobalGlobalMotionVector) ||
            Math.Min(modeInfo.BlockSize.GetWidth(), modeInfo.BlockSize.GetHeight()) < Av1BlockSize.Block8x8.GetWidth())
        {
            return true;
        }

        ReadOnlySpan<Av1GlobalMotionParameters> globalMotion = frameHeader.GetGlobalMotionParameters();
        if (globalMotion[(int)modeInfo.ReferenceFrame - 1].Type == Av1GlobalMotionType.Translation)
        {
            return true;
        }

        return modeInfo.SecondaryReferenceFrame > Av1ReferenceFrameType.Intra &&
            globalMotion[(int)modeInfo.SecondaryReferenceFrame - 1].Type == Av1GlobalMotionType.Translation;
    }

    /// <summary>
    /// Determines whether the current frame permits intra block copy.
    /// </summary>
    /// <param name="frameHeader">The current frame header.</param>
    /// <returns><see langword="true"/> when both screen-content tools and intra block copy are enabled; otherwise, <see langword="false"/>.</returns>
    private static bool IsIntraBlockCopyAllowed(ObuFrameHeader frameHeader)
        => frameHeader.AllowScreenContentTools && frameHeader.AllowIntraBlockCopy;

    /// <summary>
    /// Updates coefficient neighbor arrays after writing a block.
    /// </summary>
    /// <param name="pcs">The picture coding state.</param>
    /// <param name="entropyCodingContext">The entropy-coding position state for the superblock.</param>
    /// <param name="blockOrigin">The block origin in samples.</param>
    /// <param name="blk_ptr">The encoder block state.</param>
    /// <param name="mbmi">The selected modes of the block.</param>
    /// <param name="lumaCoefficientEdges">The luma coefficient context edges of the tile.</param>
    /// <param name="blueCoefficientEdges">The blue-difference coefficient context edges of the tile.</param>
    /// <param name="redCoefficientEdges">The red-difference coefficient context edges of the tile.</param>
    /// <param name="blockSize">The block size.</param>
    private static void UpdateNeighbors(
        Av1PictureControlSet pcs,
        Av1EntropyCodingContext entropyCodingContext,
        Point blockOrigin,
        ref Av1EncoderBlockStruct blk_ptr,
        ref Av1MacroBlockModeInfo mbmi,
        in Av1NeighborEdges<byte> lumaCoefficientEdges,
        in Av1NeighborEdges<byte> blueCoefficientEdges,
        in Av1NeighborEdges<byte> redCoefficientEdges,
        Av1BlockSize blockSize)
    {
        bool skip_coeff = mbmi.Block.Skip;

        Size size = new(blockSize.GetWidth(), blockSize.GetHeight());
        if (skip_coeff)
        {
            // The coefficient positions of a block belong to the transforms it codes, so a block that crosses
            // the frame edge owns fewer positions than its size. A skipped block reads no transform unit, and
            // therefore repeats the same count here that EncodeTransformCoefficientRegion would advance.
            Av1MacroBlockD macroBlock = entropyCodingContext.MacroBlock;
            int maximumBlocksWide = size.Width;
            int maximumBlocksHigh = size.Height;
            if (macroBlock.ToRightEdge < 0)
            {
                maximumBlocksWide += macroBlock.ToRightEdge >> 3;
            }

            if (macroBlock.ToBottomEdge < 0)
            {
                maximumBlocksHigh += macroBlock.ToBottomEdge >> 3;
            }

            maximumBlocksWide >>= Av1Constants.ModeInfoSizeLog2;
            maximumBlocksHigh >>= Av1Constants.ModeInfoSizeLog2;

            // A skipped block keeps one transform size over its whole area, in both the luma tree and chroma.
            bool lossless = pcs.Parent.FrameHeader.LosslessArray[mbmi.Block.SegmentId];
            Av1TransformSize lumaTransformSize = lossless ? Av1TransformSize.Size4x4 : mbmi.Block.TransformSize;

            // A skipped block has an all-zero residual, so publish a zero sign/level context over its edges
            // and advance coefficient positions without reading transform units.
            const Av1NeighborArrayUnit<byte>.UnitMask BothEdges = Av1NeighborArrayUnit<byte>.UnitMask.Left | Av1NeighborArrayUnit<byte>.UnitMask.Top;
            lumaCoefficientEdges.Write(0, blockOrigin, size, BothEdges);

            ObuColorConfig colorConfig = pcs.Sequence.SequenceHeader.ColorConfig;
            if (blk_ptr.HasChroma && !colorConfig.IsMonochrome)
            {
                int subsamplingX = colorConfig.SubSamplingX ? 1 : 0;
                int subsamplingY = colorConfig.SubSamplingY ? 1 : 0;
                Point chromaOrigin = GetChromaBlockOrigin(blockOrigin, subsamplingX, subsamplingY);
                Av1BlockSize chromaBlockSize = blockSize.GetSubsampled(colorConfig.SubSamplingX, colorConfig.SubSamplingY);
                Size chromaSize = new(chromaBlockSize.GetWidth(), chromaBlockSize.GetHeight());

                blueCoefficientEdges.Write(0, chromaOrigin, chromaSize, BothEdges);
                redCoefficientEdges.Write(0, chromaOrigin, chromaSize, BothEdges);

                Av1TransformSize chromaTransformSize = lossless
                    ? Av1TransformSize.Size4x4
                    : blockSize.GetMaxUvTransformSize(colorConfig.SubSamplingX, colorConfig.SubSamplingY);

                entropyCodingContext.CodedAreaSuperblockUv += GetCodedCoefficientArea(
                    maximumBlocksWide, maximumBlocksHigh, chromaTransformSize, subsamplingX, subsamplingY);
            }

            entropyCodingContext.CodedAreaSuperblock += GetCodedCoefficientArea(
                maximumBlocksWide, maximumBlocksHigh, lumaTransformSize, 0, 0);
        }
    }

    /// <summary>
    /// Calculates the coefficient positions that one plane of a block owns in the superblock buffer.
    /// </summary>
    /// <remarks>
    /// Only transforms whose origin lies inside the coded block extent are coded, and each one owns its
    /// complete sample count. This repeats the traversal bounds of
    /// <see cref="EncodeTransformCoefficientRegion{TOperation}"/> for one uniform transform size.
    /// </remarks>
    /// <param name="maximumBlocksWide">The coded luma block width in four-sample units.</param>
    /// <param name="maximumBlocksHigh">The coded luma block height in four-sample units.</param>
    /// <param name="transformSize">The transform size of the plane.</param>
    /// <param name="subsamplingX">The horizontal chroma subsampling shift, or zero for luma.</param>
    /// <param name="subsamplingY">The vertical chroma subsampling shift, or zero for luma.</param>
    /// <returns>The number of coefficient positions.</returns>
    private static int GetCodedCoefficientArea(
        int maximumBlocksWide,
        int maximumBlocksHigh,
        Av1TransformSize transformSize,
        int subsamplingX,
        int subsamplingY)
    {
        // A chroma-owning block that is narrower or shorter than its luma pair still codes its shared
        // transform, so the subsampled extent rounds upward.
        int planeBlocksWide = Av1Math.RoundPowerOf2(maximumBlocksWide, subsamplingX);
        int planeBlocksHigh = Av1Math.RoundPowerOf2(maximumBlocksHigh, subsamplingY);
        int transformBlocksWide = transformSize.Get4x4WideCount();
        int transformBlocksHigh = transformSize.Get4x4HighCount();
        int columns = (planeBlocksWide + transformBlocksWide - 1) / transformBlocksWide;
        int rows = (planeBlocksHigh + transformBlocksHigh - 1) / transformBlocksHigh;
        return columns * rows * transformSize.GetSize2d();
    }

    /// <summary>
    /// Determines whether screen-content tools and block dimensions permit palette mode.
    /// </summary>
    /// <param name="allowScreenContentTools">A value indicating whether screen-content tools are enabled.</param>
    /// <param name="blockSize">The block size.</param>
    /// <returns><see langword="true"/> when palette mode is available for the block; otherwise, <see langword="false"/>.</returns>
    internal static bool IsPaletteAllowed(bool allowScreenContentTools, Av1BlockSize blockSize)
        => allowScreenContentTools &&
            blockSize.GetWidth() <= 64 &&
            blockSize.GetHeight() <= 64 &&
            blockSize >= Av1BlockSize.Block8x8;

    /// <summary>
    /// Writes the constrained directional enhancement filter strength at its first coded block in a filter unit.
    /// </summary>
    /// <typeparam name="TOperation">Selects whether symbols are written or only adapt the probabilities.</typeparam>
    /// <param name="output">The tile buffer that the caller read once. A write that grows the buffer replaces it.</param>
    /// <param name="scs">The sequence coding state.</param>
    /// <param name="pcs">The picture coding state.</param>
    /// <param name="writer">The tile symbol encoder.</param>
    /// <param name="tileIndex">The zero-based tile index.</param>
    /// <param name="skip">A value indicating whether the current block omits residual coefficients.</param>
    /// <param name="modeInfoPosition">The block position in 4x4 mode-information units.</param>
    internal static void WriteCdef<TOperation>(
        ref Span<byte> output,
        Av1SequenceControlSet scs,
        Av1PictureControlSet pcs,
        Av1SymbolEncoder writer,
        int tileIndex,
        bool skip,
        Point modeInfoPosition)
        where TOperation : struct, Av1SymbolEncoder.ISymbolOperation
    {
        ObuFrameHeader frameHeader = pcs.Parent.FrameHeader;

        if (frameHeader.CodedLossless || frameHeader.AllowIntraBlockCopy)
        {
            return;
        }

        Span<int> cdefPreset = pcs.CdefPreset.Span.Slice(
            tileIndex * Av1Constants.CdefUnitsPerSuperblock,
            Av1Constants.CdefUnitsPerSuperblock);

        // Each superblock begins with all contained 64x64 filter units unassigned.
        if ((modeInfoPosition.Y & (scs.SequenceHeader.SuperblockModeInfoSize - 1)) == 0 &&
            (modeInfoPosition.X & (scs.SequenceHeader.SuperblockModeInfoSize - 1)) == 0)
        {
            cdefPreset.Fill(-1);
        }

        // The strength is coded once, at the first non-skipped block in each 64x64 CDEF filter unit.
        int cdefSize = 1 << (6 - Av1Constants.ModeInfoSizeLog2);
        int unitColumn = (modeInfoPosition.X & cdefSize) != 0 ? 1 : 0;
        int unitRow = (modeInfoPosition.Y & cdefSize) != 0 ? 1 : 0;
        int index = scs.SequenceHeader.Use128x128Superblock ? unitColumn + (2 * unitRow) : 0;

        if (cdefPreset[index] == -1 && !skip)
        {
            int firstBlockMask = ~(cdefSize - 1);
            Point firstBlockPosition = new(
                modeInfoPosition.X & firstBlockMask,
                modeInfoPosition.Y & firstBlockMask);

            ref Av1MacroBlockModeInfo firstBlock = ref pcs.GetFromModeInfoGrid(firstBlockPosition);

            // CDEF strength belongs to the first mode-info block in the 64x64 filter unit even when skipped
            // blocks delay transmission until a later coding block.
            writer.WriteCdefStrength<TOperation>(ref output, firstBlock.CdefStrength, frameHeader.CdefParameters.BitCount);
            cdefPreset[index] = firstBlock.CdefStrength;
        }
    }

    /// <summary>
    /// Populates a macroblock's frame edges, tile-neighbor availability, and rectangular-partition context.
    /// </summary>
    /// <param name="pcs">The picture coding state.</param>
    /// <param name="macroBlock">The macroblock state to populate.</param>
    /// <param name="tile">The active tile boundaries.</param>
    /// <param name="modeInfoPosition">The block position in 4x4 mode-information units.</param>
    /// <param name="blockSize">The block size.</param>
    /// <param name="modeInfoStride">The row stride of the mode-information grid.</param>
    /// <param name="modeInfoRowCount">The coded frame height in mode-information rows.</param>
    /// <param name="modeInfoColumnCount">The coded frame width in mode-information columns.</param>
    internal static void SetModeInfoRowAndColumn(
        Av1PictureControlSet pcs,
        Av1MacroBlockD macroBlock,
        Av1TileInfo tile,
        Point modeInfoPosition,
        Av1BlockSize blockSize,
        int modeInfoStride,
        int modeInfoRowCount,
        int modeInfoColumnCount)
    {
        macroBlock.ToTopEdge = -((modeInfoPosition.Y << Av1Constants.ModeInfoSizeLog2) << 3);
        int blockModeInfoHeight = blockSize.Get4x4HighCount();
        int blockModeInfoWidth = blockSize.Get4x4WideCount();
        macroBlock.ToBottomEdge = ((modeInfoRowCount - blockModeInfoHeight - modeInfoPosition.Y) << Av1Constants.ModeInfoSizeLog2) << 3;
        macroBlock.ToLeftEdge = -((modeInfoPosition.X << Av1Constants.ModeInfoSizeLog2) << 3);
        macroBlock.ToRightEdge = ((modeInfoColumnCount - blockModeInfoWidth - modeInfoPosition.X) << Av1Constants.ModeInfoSizeLog2) << 3;

        macroBlock.ModeInfoStride = modeInfoStride;

        // Prediction cannot cross tile boundaries even when frame mode information exists there.
        macroBlock.IsUpAvailable = modeInfoPosition.Y > tile.ModeInfoRowStart;
        macroBlock.IsLeftAvailable = modeInfoPosition.X > tile.ModeInfoColumnStart;
        int modeInfoIndex = (modeInfoPosition.Y * modeInfoStride) + modeInfoPosition.X;
        macroBlock.SetModeInfoGrid(pcs.ModeInfoGrid, pcs.ModeInfoAllocation, modeInfoIndex);
    }

    /// <summary>
    /// Writes or adapts to the luma and chroma transform coefficients of one block, in the 64x64 region order of
    /// the residual syntax.
    /// </summary>
    /// <typeparam name="TOperation">Selects whether symbols are written or only adapt the probabilities.</typeparam>
    /// <param name="output">The tile buffer that the caller read once. A write that grows the buffer replaces it.</param>
    /// <param name="pcs">The picture coding state.</param>
    /// <param name="entropyCodingContext">The entropy-coding position state for the superblock.</param>
    /// <param name="writer">The tile symbol encoder.</param>
    /// <param name="block">The encoder block state.</param>
    /// <param name="blockOrigin">The luma block origin in samples.</param>
    /// <param name="intraLumaMode">The luma prediction direction used by coefficient contexts.</param>
    /// <param name="blockSize">The luma block size.</param>
    /// <param name="coefficientBuffer">The transformed coefficients retained by raster-ordered superblock.</param>
    /// <param name="superblockIndex">The raster-ordered index of the containing superblock.</param>
    /// <param name="lumaCoefficientEdges">The luma coefficient context edges of the tile.</param>
    /// <param name="blueCoefficientEdges">The blue-difference coefficient context edges of the tile.</param>
    /// <param name="redCoefficientEdges">The red-difference coefficient context edges of the tile.</param>
    /// <param name="useRetainedContexts">Whether coefficient contexts come from completed block analysis.</param>
    private static void EncodeTransformCoefficientRegions<TOperation>(
        ref Span<byte> output,
        Av1PictureControlSet pcs,
        Av1EntropyCodingContext entropyCodingContext,
        Av1SymbolEncoder writer,
        ref Av1EncoderBlockStruct block,
        Point blockOrigin,
        Av1PredictionMode intraLumaMode,
        Av1BlockSize blockSize,
        Av1EncoderCoefficientBuffer coefficientBuffer,
        int superblockIndex,
        in Av1NeighborEdges<byte> lumaCoefficientEdges,
        in Av1NeighborEdges<byte> blueCoefficientEdges,
        in Av1NeighborEdges<byte> redCoefficientEdges,
        bool useRetainedContexts)
        where TOperation : struct, Av1SymbolEncoder.ISymbolOperation
    {
        Av1MacroBlockD macroBlock = entropyCodingContext.MacroBlock;
        int maximumBlocksWide = blockSize.GetWidth();
        int maximumBlocksHigh = blockSize.GetHeight();
        if (macroBlock.ToRightEdge < 0)
        {
            maximumBlocksWide += macroBlock.ToRightEdge >> 3;
        }

        if (macroBlock.ToBottomEdge < 0)
        {
            maximumBlocksHigh += macroBlock.ToBottomEdge >> 3;
        }

        maximumBlocksWide >>= Av1Constants.ModeInfoSizeLog2;
        maximumBlocksHigh >>= Av1Constants.ModeInfoSizeLog2;
        int maximumUnitBlocksWide = Math.Min(
            Av1BlockSize.Block64x64.Get4x4WideCount(),
            maximumBlocksWide);

        int maximumUnitBlocksHigh = Math.Min(
            Av1BlockSize.Block64x64.Get4x4HighCount(),
            maximumBlocksHigh);

        ObuColorConfig colorConfig = pcs.Sequence.SequenceHeader.ColorConfig;
        bool hasChroma = block.HasChroma && !colorConfig.IsMonochrome;
        int subsamplingX = colorConfig.SubSamplingX ? 1 : 0;
        int subsamplingY = colorConfig.SubSamplingY ? 1 : 0;
        Point chromaBlockOrigin = GetChromaBlockOrigin(blockOrigin, subsamplingX, subsamplingY);
        int chromaAreaStart = 0;

        // The adaptation pass visits each plane across the whole block before the next plane, unlike the
        // syntax below, which interleaves the planes of each 64x64 region. The order changes how a shared
        // distribution adapts once a block spans more than one region.
        // Reference: av1_update_txb_context(), which calls av1_foreach_transformed_block_in_plane() per plane.
        if (!TOperation.WritesOutput && (maximumBlocksWide > maximumUnitBlocksWide || maximumBlocksHigh > maximumUnitBlocksHigh))
        {
            for (int planeIndex = 0; planeIndex < (hasChroma ? 3 : 1); planeIndex++)
            {
                Av1Plane plane = (Av1Plane)planeIndex;
                if (plane == Av1Plane.V)
                {
                    entropyCodingContext.CodedAreaSuperblockUv = chromaAreaStart;
                }
                else if (plane == Av1Plane.U)
                {
                    chromaAreaStart = entropyCodingContext.CodedAreaSuperblockUv;
                }

                Av1NeighborEdges<byte> planeCoefficientEdges = plane switch
                {
                    Av1Plane.Y => lumaCoefficientEdges,
                    Av1Plane.U => blueCoefficientEdges,
                    _ => redCoefficientEdges
                };

                for (int regionRow = 0; regionRow < maximumBlocksHigh; regionRow += maximumUnitBlocksHigh)
                {
                    int unitBottom = Math.Min(regionRow + maximumUnitBlocksHigh, maximumBlocksHigh);
                    for (int regionColumn = 0; regionColumn < maximumBlocksWide; regionColumn += maximumUnitBlocksWide)
                    {
                        int unitRight = Math.Min(regionColumn + maximumUnitBlocksWide, maximumBlocksWide);
                        bool isLuma = plane == Av1Plane.Y;
                        EncodeTransformCoefficientRegion<TOperation>(
                            ref output,
                            pcs,
                            entropyCodingContext,
                            writer,
                            ref block,
                            isLuma ? blockOrigin : chromaBlockOrigin,
                            intraLumaMode,
                            blockSize,
                            plane,
                            coefficientBuffer,
                            superblockIndex,
                            planeCoefficientEdges,
                            isLuma ? regionRow : regionRow >> subsamplingY,
                            isLuma ? regionColumn : regionColumn >> subsamplingX,
                            isLuma ? unitBottom : Av1Math.RoundPowerOf2(unitBottom, subsamplingY),
                            isLuma ? unitRight : Av1Math.RoundPowerOf2(unitRight, subsamplingX),
                            useRetainedContexts,
                            advanceBlueArea: plane == Av1Plane.U);
                    }
                }
            }

            return;
        }

        // Residual syntax is region-major, then plane-major. Keeping the three plane calls together
        // prevents a 128x128 block from emitting later luma regions before earlier chroma regions.
        for (int regionRow = 0; regionRow < maximumBlocksHigh; regionRow += maximumUnitBlocksHigh)
        {
            int unitBottom = Math.Min(regionRow + maximumUnitBlocksHigh, maximumBlocksHigh);
            for (int regionColumn = 0; regionColumn < maximumBlocksWide; regionColumn += maximumUnitBlocksWide)
            {
                int unitRight = Math.Min(regionColumn + maximumUnitBlocksWide, maximumBlocksWide);
                EncodeTransformCoefficientRegion<TOperation>(
                    ref output,
                    pcs,
                    entropyCodingContext,
                    writer,
                    ref block,
                    blockOrigin,
                    intraLumaMode,
                    blockSize,
                    Av1Plane.Y,
                    coefficientBuffer,
                    superblockIndex,
                    lumaCoefficientEdges,
                    regionRow,
                    regionColumn,
                    unitBottom,
                    unitRight,
                    useRetainedContexts);

                if (hasChroma)
                {
                    int chromaRegionRow = regionRow >> subsamplingY;
                    int chromaRegionColumn = regionColumn >> subsamplingX;

                    // Region limits count 4x4 units. Round the subsampled end upward so a chroma-owning
                    // 4x4, 4x8, or 8x4 luma block still emits its shared 4x4 chroma transform.
                    int chromaUnitBottom = Av1Math.RoundPowerOf2(unitBottom, subsamplingY);
                    int chromaUnitRight = Av1Math.RoundPowerOf2(unitRight, subsamplingX);
                    EncodeTransformCoefficientRegion<TOperation>(
                        ref output,
                        pcs,
                        entropyCodingContext,
                        writer,
                        ref block,
                        chromaBlockOrigin,
                        intraLumaMode,
                        blockSize,
                        Av1Plane.U,
                        coefficientBuffer,
                        superblockIndex,
                        blueCoefficientEdges,
                        chromaRegionRow,
                        chromaRegionColumn,
                        chromaUnitBottom,
                        chromaUnitRight,
                        useRetainedContexts);

                    EncodeTransformCoefficientRegion<TOperation>(
                        ref output,
                        pcs,
                        entropyCodingContext,
                        writer,
                        ref block,
                        chromaBlockOrigin,
                        intraLumaMode,
                        blockSize,
                        Av1Plane.V,
                        coefficientBuffer,
                        superblockIndex,
                        redCoefficientEdges,
                        chromaRegionRow,
                        chromaRegionColumn,
                        chromaUnitBottom,
                        chromaUnitRight,
                        useRetainedContexts);
                }
            }
        }
    }

    /// <summary>
    /// Writes or adapts to the transform coefficients of one plane inside one 64x64 region of a block, and publishes
    /// the coefficient contexts of each transform block on the plane edges.
    /// </summary>
    /// <typeparam name="TOperation">Selects whether symbols are written or only adapt the probabilities.</typeparam>
    /// <param name="output">The tile buffer that the caller read once. A write that grows the buffer replaces it.</param>
    /// <param name="pcs">The picture coding state.</param>
    /// <param name="entropyCodingContext">The entropy-coding position state for the superblock.</param>
    /// <param name="writer">The tile symbol encoder.</param>
    /// <param name="block">The encoder block state.</param>
    /// <param name="planeBlockOrigin">The block origin in samples of the plane.</param>
    /// <param name="intraLumaMode">The luma prediction direction used by coefficient contexts.</param>
    /// <param name="lumaBlockSize">The luma block size.</param>
    /// <param name="plane">The plane to write.</param>
    /// <param name="coefficientBuffer">The transformed coefficients retained by raster-ordered superblock.</param>
    /// <param name="superblockIndex">The raster-ordered index of the containing superblock.</param>
    /// <param name="coefficientEdges">The coefficient context edges of the plane, read once by the caller.</param>
    /// <param name="regionRow">The first row of the region, in 4x4 units of the plane.</param>
    /// <param name="regionColumn">The first column of the region, in 4x4 units of the plane.</param>
    /// <param name="unitBottom">The row after the region, in 4x4 units of the plane.</param>
    /// <param name="unitRight">The column after the region, in 4x4 units of the plane.</param>
    /// <param name="useRetainedContexts">Whether coefficient contexts come from completed block analysis.</param>
    /// <param name="advanceBlueArea">Whether the blue plane advances the shared chroma coefficient position.</param>
    private static void EncodeTransformCoefficientRegion<TOperation>(
        ref Span<byte> output,
        Av1PictureControlSet pcs,
        Av1EntropyCodingContext entropyCodingContext,
        Av1SymbolEncoder writer,
        ref Av1EncoderBlockStruct block,
        Point planeBlockOrigin,
        Av1PredictionMode intraLumaMode,
        Av1BlockSize lumaBlockSize,
        Av1Plane plane,
        Av1EncoderCoefficientBuffer coefficientBuffer,
        int superblockIndex,
        in Av1NeighborEdges<byte> coefficientEdges,
        int regionRow,
        int regionColumn,
        int unitBottom,
        int unitRight,
        bool useRetainedContexts,
        bool advanceBlueArea = false)
        where TOperation : struct, Av1SymbolEncoder.ISymbolOperation
    {
        ObuFrameHeader frameHeader = pcs.Parent.FrameHeader;
        ObuColorConfig colorConfig = pcs.Sequence.SequenceHeader.ColorConfig;
        bool isLuma = plane == Av1Plane.Y;
        Av1BlockSize planeBlockSize = isLuma
            ? lumaBlockSize
            : lumaBlockSize.GetSubsampled(colorConfig.SubSamplingX, colorConfig.SubSamplingY);

        Av1TransformSize transformSize = isLuma
            ? entropyCodingContext.MacroBlockModeInfo.Block.TransformSize
            : frameHeader.LosslessArray[entropyCodingContext.MacroBlockModeInfo.Block.SegmentId]
                ? Av1TransformSize.Size4x4
                : lumaBlockSize.GetMaxUvTransformSize(colorConfig.SubSamplingX, colorConfig.SubSamplingY);

        Size frameContextSize = new(
            frameHeader.ModeInfoColumnCount >> (!isLuma && colorConfig.SubSamplingX ? 1 : 0),
            frameHeader.ModeInfoRowCount >> (!isLuma && colorConfig.SubSamplingY ? 1 : 0));

        bool usesInterTransformSet =
            entropyCodingContext.MacroBlockModeInfo.Block.ReferenceFrame > Av1ReferenceFrameType.Intra ||
            entropyCodingContext.MacroBlockModeInfo.Block.UseIntraBlockCopy;

        Av1ComponentType componentType = isLuma
            ? Av1ComponentType.Luminance
            : Av1ComponentType.Chroma;

        Span<int> planeCoefficients = coefficientBuffer.GetPlaneSpan(superblockIndex, plane);
        Span<Av1EncoderTransformBlockState> planeTransformBlocks =
            coefficientBuffer.GetTransformBlockSpan(superblockIndex, plane);

        int codedArea = isLuma
            ? entropyCodingContext.CodedAreaSuperblock
            : entropyCodingContext.CodedAreaSuperblockUv;

        Av1TransformSize rootSize = isLuma && usesInterTransformSet && !frameHeader.LosslessArray[
            entropyCodingContext.MacroBlockModeInfo.Block.SegmentId]
            ? lumaBlockSize.GetMaximumTransformSize()
            : transformSize;

        Av1EncoderBlockModeInfo retainedModeInfo = entropyCodingContext.MacroBlockModeInfo.Block;
        bool variableLuma = isLuma && usesInterTransformSet && !frameHeader.LosslessArray[
            entropyCodingContext.MacroBlockModeInfo.Block.SegmentId];

        Av1TransformSize traversalSize = variableLuma ? rootSize.GetSubSize().GetSubSize() : transformSize;
        int leafCount = rootSize.GetSize2d() / traversalSize.GetSize2d();

        // Split inter transforms follow their syntax tree. Intra and chroma roots each have one leaf,
        // preserving their raster traversal while all paths share coefficient and context ownership.
        for (int rootRow = regionRow; rootRow < unitBottom; rootRow += rootSize.Get4x4HighCount())
        {
            for (int rootColumn = regionColumn; rootColumn < unitRight; rootColumn += rootSize.Get4x4WideCount())
            {
                for (int leaf = 0; leaf < leafCount; leaf++)
                {
                    Point offset = rootSize.GetPartitionOrigin(traversalSize, leaf);
                    int blockRow = rootRow + (offset.Y >> Av1Constants.ModeInfoSizeLog2);
                    int blockColumn = rootColumn + (offset.X >> Av1Constants.ModeInfoSizeLog2);
                    if (blockRow >= unitBottom || blockColumn >= unitRight)
                    {
                        continue;
                    }

                    if (variableLuma)
                    {
                        transformSize = retainedModeInfo.InterTransformSizes[retainedModeInfo.GetInterTransformSizeIndex(blockRow, blockColumn)];
                        if ((offset.X % transformSize.GetWidth()) != 0 || (offset.Y % transformSize.GetHeight()) != 0)
                        {
                            continue;
                        }
                    }

                    int transformStateIndex =
                        codedArea / Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount;

                    ref Av1EncoderTransformBlockState transformBlock =
                        ref planeTransformBlocks[transformStateIndex];

                    Point transformOrigin = planeBlockOrigin + new Size(
                        blockColumn << Av1Constants.ModeInfoSizeLog2,
                        blockRow << Av1Constants.ModeInfoSizeLog2);

                    Span<int> coefficients = planeCoefficients[codedArea..];
                    Av1TransformBlockContext blockContext;
                    if (useRetainedContexts)
                    {
                        byte packedContext = transformBlock.EntropyContext;
                        blockContext = new Av1TransformBlockContext
                        {
                            SkipContext = packedContext & 15,
                            DcSignContext = packedContext >> 4
                        };
                    }
                    else
                    {
                        blockContext = GetTransformBlockContexts(
                            componentType,
                            in coefficientEdges,
                            transformOrigin,
                            planeBlockSize,
                            transformSize);

                        // Neighbor probabilities must describe the selected transform at analysis time, before
                        // final packing revisits the frame. Both context alphabets fit in the existing spare byte.
                        transformBlock.EntropyContext = (byte)(blockContext.SkipContext | (blockContext.DcSignContext << 4));
                    }

                    Av1TransformType transformType = transformBlock.TransformType;
                    if (isLuma && transformBlock.EndOfBlock == 0)
                    {
                        // Empty luma transforms carry no transform-type symbol, so retain the canonical state.
                        transformType = transformBlock.TransformType = Av1TransformType.DctDct;
                    }

                    if (TOperation.WritesOutput && transformBlock.EndOfBlock != 0 &&
                        pcs.Parent.SpeedSettings.TrackTransformTypeProbabilities)
                    {
                        // Only the final packing traversal counts selected transforms. Partition trials and
                        // coefficient analysis revisit the same samples and must not change frame history.
                        pcs.Parent.TransformTypeCounts.Span[
                            ((int)transformSize * Av1TransformTypeProbabilities.TypeCount) + (int)transformType]++;
                    }

                    int culLevel = writer.WriteCoefficients<TOperation>(
                        ref output,
                        transformSize,
                        transformType,
                        intraLumaMode,
                        coefficients,
                        componentType,
                        blockContext,
                        transformBlock.EndOfBlock,
                        frameHeader.UseReducedTransformSet,
                        block.FilterIntraMode,
                        usesInterTransformSet);

                    UpdateCoefficientContexts(
                        coefficientEdges.Top.Slice(coefficientEdges.GetTopIndex(transformOrigin), transformSize.Get4x4WideCount()),
                        coefficientEdges.Left.Slice(coefficientEdges.GetLeftIndex(transformOrigin), transformSize.Get4x4HighCount()),
                        (byte)culLevel,
                        transformOrigin,
                        frameContextSize);

                    codedArea += transformSize.GetSize2d();
                }
            }
        }

        if (isLuma)
        {
            entropyCodingContext.CodedAreaSuperblock = codedArea;
        }
        else if (plane == Av1Plane.V || advanceBlueArea)
        {
            // U and V share the same per-plane coded-area positions; advance only after V completes the region,
            // unless U runs across the whole block on its own.
            entropyCodingContext.CodedAreaSuperblockUv = codedArea;
        }
    }

    /// <summary>
    /// Converts a luma origin to the shared 4x4 chroma-block origin for the active subsampling.
    /// </summary>
    /// <param name="lumaOrigin">The luma sample position.</param>
    /// <param name="subsamplingX">The horizontal chroma subsampling shift.</param>
    /// <param name="subsamplingY">The vertical chroma subsampling shift.</param>
    /// <returns>The aligned origin in chroma samples.</returns>
    public static Point GetChromaBlockOrigin(Point lumaOrigin, int subsamplingX, int subsamplingY)
        => new(
            (lumaOrigin.X >> (Av1Constants.ModeInfoSizeLog2 + subsamplingX)) << Av1Constants.ModeInfoSizeLog2,
            (lumaOrigin.Y >> (Av1Constants.ModeInfoSizeLog2 + subsamplingY)) << Av1Constants.ModeInfoSizeLog2);

    /// <summary>
    /// Publishes coefficient activity on the coded edge and clears the part outside the frame.
    /// </summary>
    /// <param name="topContexts">The complete above edge of the transform.</param>
    /// <param name="leftContexts">The complete left edge of the transform.</param>
    /// <param name="context">The packed coefficient level and DC sign.</param>
    /// <param name="transformOrigin">The transform origin in plane samples.</param>
    /// <param name="frameContextSize">The coded plane extent in four-sample units.</param>
    public static void UpdateCoefficientContexts(
        Span<byte> topContexts,
        Span<byte> leftContexts,
        byte context,
        Point transformOrigin,
        Size frameContextSize)
    {
        // Transforms retain their full size at the frame edge, but padded samples cannot contribute
        // activity to later transforms. Clear the unused tail even when an earlier trial populated it.
        int topCount = Math.Min(topContexts.Length, frameContextSize.Width - (transformOrigin.X >> Av1Constants.ModeInfoSizeLog2));
        int leftCount = Math.Min(leftContexts.Length, frameContextSize.Height - (transformOrigin.Y >> Av1Constants.ModeInfoSizeLog2));
        topContexts[..topCount].Fill(context);
        topContexts[topCount..].Clear();
        leftContexts[..leftCount].Fill(context);
        leftContexts[leftCount..].Clear();
    }

    /// <summary>
    /// Derives coefficient skip and DC-sign contexts from the transform block's above and left neighbors.
    /// </summary>
    /// <param name="plane">The luma or chroma component class.</param>
    /// <param name="coefficientEdges">The packed DC-sign and coefficient-level edges of the plane, read once by the caller.</param>
    /// <param name="blockOrigin">The transform-block origin in samples of the target plane.</param>
    /// <param name="planeBlockSize">The containing block size on the target plane.</param>
    /// <param name="transformSize">The transform size.</param>
    /// <returns>The coefficient skip and DC-sign contexts selected by both transform edges.</returns>
    public static Av1TransformBlockContext GetTransformBlockContexts(
        Av1ComponentType plane,
        in Av1NeighborEdges<byte> coefficientEdges,
        Point blockOrigin,
        Av1BlockSize planeBlockSize,
        Av1TransformSize transformSize)
    {
        int leftIndex = coefficientEdges.GetLeftIndex(blockOrigin);
        int topIndex = coefficientEdges.GetTopIndex(blockOrigin);
        int transformBlockWidth = transformSize.Get4x4WideCount();
        int transformBlockHeight = transformSize.Get4x4HighCount();
        ReadOnlySpan<byte> topContexts = coefficientEdges.Top.Slice(topIndex, transformBlockWidth);
        ReadOnlySpan<byte> leftContexts = coefficientEdges.Left.Slice(leftIndex, transformBlockHeight);

        return GetTransformBlockContexts(plane, topContexts, leftContexts, planeBlockSize, transformSize);
    }

    /// <summary>
    /// Derives coefficient skip and DC-sign contexts from explicit transform-edge contexts.
    /// </summary>
    /// <param name="plane">The luma or chroma component class.</param>
    /// <param name="topContexts">The packed contexts immediately above the transform block.</param>
    /// <param name="leftContexts">The packed contexts immediately left of the transform block.</param>
    /// <param name="planeBlockSize">The containing block size on the target plane.</param>
    /// <param name="transformSize">The transform size.</param>
    /// <returns>The coefficient skip and DC-sign contexts selected by both transform edges.</returns>
    public static Av1TransformBlockContext GetTransformBlockContexts(
        Av1ComponentType plane,
        ReadOnlySpan<byte> topContexts,
        ReadOnlySpan<byte> leftContexts,
        Av1BlockSize planeBlockSize,
        Av1TransformSize transformSize)
    {
        int dcSign = 0;
        int top = 0;
        int left = 0;

        // Each context packs a coefficient-level class in the low bits and the DC sign class above it.
        // Accumulating both values in one traversal supplies every luma and chroma context without scratch storage.
        foreach (byte context in topContexts)
        {
            byte sign = (byte)(context >> Av1Constants.CoefficientContextBitCount);
            DebugGuard.MustBeLessThanOrEqualTo(sign, (byte)2, nameof(sign));
            if (sign == 1)
            {
                dcSign--;
            }
            else if (sign == 2)
            {
                dcSign++;
            }

            top |= context;
        }

        foreach (byte context in leftContexts)
        {
            byte sign = (byte)(context >> Av1Constants.CoefficientContextBitCount);
            DebugGuard.MustBeLessThanOrEqualTo(sign, (byte)2, nameof(sign));
            if (sign == 1)
            {
                dcSign--;
            }
            else if (sign == 2)
            {
                dcSign++;
            }

            left |= context;
        }

        Av1TransformBlockContext blockContext = default;
        blockContext.DcSignContext = dcSign > 0 ? 2 : dcSign < 0 ? 1 : 0;
        if (plane == Av1ComponentType.Luminance)
        {
            if (planeBlockSize == transformSize.ToBlockSize())
            {
                blockContext.SkipContext = 0;
            }
            else
            {
                top &= Av1Constants.CoefficientContextMask;
                left &= Av1Constants.CoefficientContextMask;
                blockContext.SkipContext = Av1SymbolContextHelper.GetTransformBlockSkipContext(top, left);
            }
        }
        else
        {
            // Chroma contexts use only the presence of nonzero levels on each edge, plus an offset
            // that distinguishes a transform smaller than its containing plane block.
            int contextBase = (left != 0 ? 1 : 0) + (top != 0 ? 1 : 0);
            int contextOffset = planeBlockSize.GetPelsLog2Count() > transformSize.ToBlockSize().GetPelsLog2Count() ? 10 : 7;
            blockContext.SkipContext = contextBase + contextOffset;
        }

        return blockContext;
    }

    /// <summary>
    /// Writes or predicts a block segment identifier and updates the frame segmentation map.
    /// </summary>
    /// <param name="output">The tile buffer that the caller read once. A write that grows the buffer replaces it.</param>
    /// <param name="pcs">The picture coding state.</param>
    /// <param name="writer">The tile symbol encoder.</param>
    /// <param name="blockSize">The block size.</param>
    /// <param name="blockOrigin">The block origin in samples.</param>
    /// <param name="macroBlock">The reusable macroblock edge and neighbor state.</param>
    /// <param name="block">The encoder block state.</param>
    /// <param name="skip">A value indicating whether residual coefficients are omitted.</param>
    /// <param name="beforeSkip">Whether the segment identifier is written before the skip flag.</param>
    /// <typeparam name="TOperation">Selects whether symbols are written or only adapt the probabilities.</typeparam>
    private static void WriteSegmentId<TOperation>(
        ref Span<byte> output,
        Av1PictureControlSet pcs,
        Av1SymbolEncoder writer,
        Av1BlockSize blockSize,
        Point blockOrigin,
        Av1MacroBlockD macroBlock,
        ref Av1EncoderBlockStruct block,
        bool skip,
        bool beforeSkip)
        where TOperation : struct, Av1SymbolEncoder.ISymbolOperation
    {
        ObuSegmentationParameters segmentation_params = pcs.Parent.FrameHeader.SegmentationParameters;
        if (!segmentation_params.Enabled || segmentation_params.SegmentationUpdateMap != 1)
        {
            return;
        }

        int spatial_pred = GetSpatialSegmentationPrediction(pcs, macroBlock, blockOrigin, out int cdf_num);
        if (!beforeSkip && skip)
        {
            // Post-skip segment syntax can infer the spatial predictor once the decoder already knows the block is skipped.
            // The bitstream also records it in the encoder's own map. Reference: the skip_txfm branch of
            // write_segment_id().
            pcs.UpdateSegmentation(blockSize, blockOrigin, spatial_pred);
            if (TOperation.WritesOutput)
            {
                pcs.UpdateSegmentation(pcs.Parent.EncoderSegmentMap.Span, blockSize, blockOrigin, spatial_pred);
            }

            block.SegmentId = spatial_pred;
            return;
        }

        // The segment syntax is only coded into the bitstream; the encoding pass never adapts its distributions.
        // Reference: update_stats(), which updates no segment distribution.
        if (TOperation.WritesOutput)
        {
            // An inter frame that codes its map against the primary reference first signals the prediction flag.
            // The encoder never sets the flag, so every identifier is then coded spatially. Reference: the
            // seg_id_predicted of write_inter_segment_id(), which only the skip path writes.
            if (!pcs.Parent.FrameHeader.IsIntra && segmentation_params.SegmentationTemporalUpdate == 1)
            {
                writer.WriteSegmentIdPredicted<TOperation>(ref output, false, 0);
            }

            int coded_id = Av1SymbolContextHelper.NegativeInterleave(block.SegmentId, spatial_pred, segmentation_params.LastActiveSegmentId + 1);
            writer.WriteSegmentId<TOperation>(ref output, coded_id, cdf_num);
        }

        pcs.UpdateSegmentation(blockSize, blockOrigin, block.SegmentId);
    }

    /// <summary>
    /// Derives a segment identifier predictor and entropy context from the upper-left, above, and left neighbors.
    /// </summary>
    /// <param name="pcs">The picture coding state.</param>
    /// <param name="xd">The current macroblock and its neighbor availability.</param>
    /// <param name="blockOrigin">The block origin in samples.</param>
    /// <param name="cdf_index">The entropy context selected by matching neighbor identifiers.</param>
    /// <returns>The spatially predicted segment identifier.</returns>
    private static int GetSpatialSegmentationPrediction(
        Av1PictureControlSet pcs,
        Av1MacroBlockD xd,
        Point blockOrigin,
        out int cdf_index)
        => GetSpatialSegmentationPrediction(pcs.Parent.Common, pcs.SegmentationNeighborMap.Span, xd, blockOrigin, out cdf_index);

    /// <summary>
    /// Derives a segment identifier predictor and entropy context from the upper-left, above, and left neighbors of a
    /// segment map. Reference: av1_get_spatial_seg_pred().
    /// </summary>
    /// <param name="cm">The frame dimensions in mode-information units.</param>
    /// <param name="segmentation_map">The segment map that holds the neighbors.</param>
    /// <param name="xd">The current macroblock and its neighbor availability.</param>
    /// <param name="blockOrigin">The block origin in samples.</param>
    /// <param name="cdf_index">The entropy context selected by matching neighbor identifiers.</param>
    /// <returns>The spatially predicted segment identifier.</returns>
    public static int GetSpatialSegmentationPrediction(
        Av1EncoderCommon cm,
        ReadOnlySpan<byte> segmentation_map,
        Av1MacroBlockD xd,
        Point blockOrigin,
        out int cdf_index)
    {
        const int unavailableSegmentId = -1;
        int prev_ul = unavailableSegmentId;
        int prev_l = unavailableSegmentId;
        int prev_u = unavailableSegmentId;

        int mi_col = blockOrigin.X >> Av1Constants.ModeInfoSizeLog2;
        int mi_row = blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2;
        bool left_available = xd.IsLeftAvailable;
        bool up_available = xd.IsUpAvailable;

        if (up_available && left_available)
        {
            prev_ul = Av1SymbolContextHelper.GetSegmentId(cm, segmentation_map, Av1BlockSize.Block4x4, new Point(mi_col - 1, mi_row - 1));
        }

        if (up_available)
        {
            prev_u = Av1SymbolContextHelper.GetSegmentId(cm, segmentation_map, Av1BlockSize.Block4x4, new Point(mi_col, mi_row - 1));
        }

        if (left_available)
        {
            prev_l = Av1SymbolContextHelper.GetSegmentId(cm, segmentation_map, Av1BlockSize.Block4x4, new Point(mi_col - 1, mi_row));
        }

        // The entropy context records whether zero, two, or all three neighboring IDs agree.
        // Any unavailable neighbor falls back to the least-specific context.
        if (prev_ul < 0 || prev_u < 0 || prev_l < 0)
        {
            cdf_index = 0;
        }
        else if ((prev_ul == prev_u) && (prev_ul == prev_l))
        {
            cdf_index = 2;
        }
        else if ((prev_ul == prev_u) || (prev_ul == prev_l) || (prev_u == prev_l))
        {
            cdf_index = 1;
        }
        else
        {
            cdf_index = 0;
        }

        // Select the majority value when possible; otherwise AV1 gives the left neighbor precedence.
        if (prev_u == unavailableSegmentId)
        {
            return prev_l == unavailableSegmentId ? 0 : prev_l;
        }

        if (prev_l == unavailableSegmentId)
        {
            return prev_u;
        }

        return (prev_ul == prev_u) ? prev_u : prev_l;
    }

    /// <summary>
    /// Gets the block skip context from the available above and left modes.
    /// </summary>
    /// <param name="macroBlock">The reusable macroblock edge and neighbor state.</param>
    /// <returns>The sum of the available above and left skip states.</returns>
    public static int GetSkipContext(Av1MacroBlockD macroBlock)
    {
        bool aboveSkipped = macroBlock.IsUpAvailable &&
            macroBlock.GetRelativeModeInfo(-macroBlock.ModeInfoStride).Block.Skip;

        bool leftSkipped = macroBlock.IsLeftAvailable && macroBlock.GetRelativeModeInfo(-1).Block.Skip;
        return (aboveSkipped ? 1 : 0) + (leftSkipped ? 1 : 0);
    }

    /// <summary>
    /// Gets the block skip-mode context from available above and left modes.
    /// </summary>
    public static int GetSkipModeContext(Av1MacroBlockD macroBlock)
    {
        bool aboveSkipMode = macroBlock.IsUpAvailable &&
            macroBlock.GetRelativeModeInfo(-macroBlock.ModeInfoStride).Block.SkipMode;

        bool leftSkipMode = macroBlock.IsLeftAvailable && macroBlock.GetRelativeModeInfo(-1).Block.SkipMode;
        return (aboveSkipMode ? 1 : 0) + (leftSkipMode ? 1 : 0);
    }

    /// <summary>
    /// Selects block skip when it is cheaper than retaining empty transform syntax.
    /// </summary>
    /// <param name="writer">The live tile symbol encoder.</param>
    /// <param name="skipContext">The neighboring block skip context.</param>
    /// <param name="emptyTransformRate">The complete coefficient rate for the empty transforms.</param>
    /// <returns>
    /// <see langword="true"/> when block skip has a strictly lower rate; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool ShouldSkipCoefficients(
        Av1SymbolEncoder writer,
        int skipContext,
        int emptyTransformRate)
    {
        Av1ModeCosts modeCosts = writer.ModeCosts;
        int skipRate = Av1SymbolEncoder.GetSkipCost(modeCosts, true, skipContext);
        int nonSkipRate = Av1SymbolEncoder.GetSkipCost(modeCosts, false, skipContext) + emptyTransformRate;

        // Current libaom keeps intra blocks non-skipped. Empty transforms make both choices
        // decoder-identical, so select skip only when its complete live rate is strictly lower.
        return skipRate < nonSkipRate;
    }

    /// <summary>
    /// Writes the block skip flag using the sum of available above and left skip states as its context.
    /// </summary>
    /// <typeparam name="TOperation">Selects whether symbols are written or only adapt the probabilities.</typeparam>
    /// <param name="output">The tile buffer that the caller read once. A write that grows the buffer replaces it.</param>
    /// <param name="writer">The tile symbol encoder.</param>
    /// <param name="macroBlock">The reusable macroblock edge and neighbor state.</param>
    /// <param name="skip">The skip value to write.</param>
    public static void EncodeSkipCoefficients<TOperation>(ref Span<byte> output, Av1SymbolEncoder writer, Av1MacroBlockD macroBlock, bool skip)
        where TOperation : struct, Av1SymbolEncoder.ISymbolOperation
        => writer.WriteSkip<TOperation>(ref output, skip, GetSkipContext(macroBlock));
}
