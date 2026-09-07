// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopFilter;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Encodes one range-coded AV1 tile payload.
/// </summary>
internal readonly struct Av1TileEncoder : IAv1TileWriter
{
    private readonly ReadOnlyMemory<byte> tileData;
    private readonly Av1PictureControlSet picture;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1TileEncoder"/> struct for eight-bit samples.
    /// </summary>
    /// <param name="writer">The symbol encoder that retains tile output through the enclosing frame write.</param>
    /// <param name="source">The coded source frame.</param>
    /// <param name="reconstruction">The reconstructed frame updated during encoding.</param>
    /// <param name="picture">The frame coding and mode-information state.</param>
    /// <param name="coefficientBuffer">The frame-owned quantized coefficient and transform state.</param>
    /// <param name="superblockWorkspace">The reusable partition and final-block decision workspace.</param>
    /// <param name="blockWorkspace">The reusable block arithmetic workspace.</param>
    /// <param name="effort">The mode-search effort in the inclusive range zero through ten.</param>
    public Av1TileEncoder(
        Av1SymbolEncoder writer,
        Av1EncoderFrame<byte> source,
        Av1EncoderFrame<byte> reconstruction,
        Av1PictureControlSet picture,
        Av1EncoderCoefficientBuffer coefficientBuffer,
        Av1EncoderSuperblockWorkspace superblockWorkspace,
        Av1EncoderBlockWorkspace blockWorkspace,
        int effort)
    {
        this.picture = picture;
        this.tileData = Encode<byte, Av1IntraSuperblockEncoder.ByteOperator,
            Av1DeblockingFilter.VerticalByteEdgeOperator, Av1DeblockingFilter.HorizontalByteEdgeOperator>(
            writer,
            source,
            reconstruction,
            reconstruction,
            picture,
            coefficientBuffer,
            new Av1EncoderTileWorkspace(picture.Parent.FrameHeader, superblockWorkspace),
            blockWorkspace,
            effort);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1TileEncoder"/> struct for an eight-bit inter frame.
    /// </summary>
    /// <param name="writer">The symbol encoder that retains tile output through the enclosing frame write.</param>
    /// <param name="source">The coded source frame.</param>
    /// <param name="reference">The reconstructed reference frame.</param>
    /// <param name="reconstruction">The reconstructed frame updated during encoding.</param>
    /// <param name="picture">The frame coding and mode-information state.</param>
    /// <param name="coefficientBuffer">The frame-owned quantized coefficient and transform state.</param>
    /// <param name="superblockWorkspace">The reusable partition and final-block decision workspace.</param>
    /// <param name="blockWorkspace">The reusable block arithmetic workspace.</param>
    /// <param name="effort">The mode-search effort in the inclusive range zero through ten.</param>
    public Av1TileEncoder(
        Av1SymbolEncoder writer,
        Av1EncoderFrame<byte> source,
        Av1EncoderFrame<byte> reference,
        Av1EncoderFrame<byte> reconstruction,
        Av1PictureControlSet picture,
        Av1EncoderCoefficientBuffer coefficientBuffer,
        Av1EncoderSuperblockWorkspace superblockWorkspace,
        Av1EncoderBlockWorkspace blockWorkspace,
        int effort)
    {
        this.picture = picture;
        this.tileData = Encode<byte, Av1IntraSuperblockEncoder.ByteOperator,
            Av1DeblockingFilter.VerticalByteEdgeOperator, Av1DeblockingFilter.HorizontalByteEdgeOperator>(
            writer,
            source,
            reference,
            reconstruction,
            picture,
            coefficientBuffer,
            new Av1EncoderTileWorkspace(picture.Parent.FrameHeader, superblockWorkspace),
            blockWorkspace,
            effort);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1TileEncoder"/> struct for an eight-bit inter frame.
    /// </summary>
    /// <param name="writer">The symbol encoder that retains tile output through the enclosing frame write.</param>
    /// <param name="source">The coded source frame.</param>
    /// <param name="reference">The reconstructed reference frame.</param>
    /// <param name="reconstruction">The reconstructed frame updated during encoding.</param>
    /// <param name="picture">The frame coding and mode-information state.</param>
    /// <param name="coefficientBuffer">The frame-owned quantized coefficient and transform state.</param>
    /// <param name="tileWorkspace">The retained tile, superblock, and entropy cursor graph.</param>
    /// <param name="blockWorkspace">The reusable block arithmetic workspace.</param>
    /// <param name="effort">The mode-search effort in the inclusive range zero through ten.</param>
    public Av1TileEncoder(
        Av1SymbolEncoder writer,
        Av1EncoderFrame<byte> source,
        Av1EncoderFrame<byte> reference,
        Av1EncoderFrame<byte> reconstruction,
        Av1PictureControlSet picture,
        Av1EncoderCoefficientBuffer coefficientBuffer,
        Av1EncoderTileWorkspace tileWorkspace,
        Av1EncoderBlockWorkspace blockWorkspace,
        int effort)
    {
        this.picture = picture;
        this.tileData = Encode<byte, Av1IntraSuperblockEncoder.ByteOperator,
            Av1DeblockingFilter.VerticalByteEdgeOperator, Av1DeblockingFilter.HorizontalByteEdgeOperator>(
            writer,
            source,
            reference,
            reconstruction,
            picture,
            coefficientBuffer,
            tileWorkspace,
            blockWorkspace,
            effort);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1TileEncoder"/> struct for high-bit-depth samples.
    /// </summary>
    /// <param name="writer">The symbol encoder that retains tile output through the enclosing frame write.</param>
    /// <param name="source">The coded source frame.</param>
    /// <param name="reconstruction">The reconstructed frame updated during encoding.</param>
    /// <param name="picture">The frame coding and mode-information state.</param>
    /// <param name="coefficientBuffer">The frame-owned quantized coefficient and transform state.</param>
    /// <param name="superblockWorkspace">The reusable partition and final-block decision workspace.</param>
    /// <param name="blockWorkspace">The reusable block arithmetic workspace.</param>
    /// <param name="effort">The mode-search effort in the inclusive range zero through ten.</param>
    public Av1TileEncoder(
        Av1SymbolEncoder writer,
        Av1EncoderFrame<ushort> source,
        Av1EncoderFrame<ushort> reconstruction,
        Av1PictureControlSet picture,
        Av1EncoderCoefficientBuffer coefficientBuffer,
        Av1EncoderSuperblockWorkspace superblockWorkspace,
        Av1EncoderBlockWorkspace blockWorkspace,
        int effort)
    {
        this.picture = picture;
        this.tileData = Encode<ushort, Av1IntraSuperblockEncoder.UInt16Operator,
            Av1DeblockingFilter.VerticalUInt16EdgeOperator, Av1DeblockingFilter.HorizontalUInt16EdgeOperator>(
            writer,
            source,
            reconstruction,
            reconstruction,
            picture,
            coefficientBuffer,
            new Av1EncoderTileWorkspace(picture.Parent.FrameHeader, superblockWorkspace),
            blockWorkspace,
            effort);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1TileEncoder"/> struct for a high-bit-depth inter frame.
    /// </summary>
    /// <param name="writer">The symbol encoder that retains tile output through the enclosing frame write.</param>
    /// <param name="source">The coded source frame.</param>
    /// <param name="reference">The reconstructed reference frame.</param>
    /// <param name="reconstruction">The reconstructed frame updated during encoding.</param>
    /// <param name="picture">The frame coding and mode-information state.</param>
    /// <param name="coefficientBuffer">The frame-owned quantized coefficient and transform state.</param>
    /// <param name="superblockWorkspace">The reusable partition and final-block decision workspace.</param>
    /// <param name="blockWorkspace">The reusable block arithmetic workspace.</param>
    /// <param name="effort">The mode-search effort in the inclusive range zero through ten.</param>
    public Av1TileEncoder(
        Av1SymbolEncoder writer,
        Av1EncoderFrame<ushort> source,
        Av1EncoderFrame<ushort> reference,
        Av1EncoderFrame<ushort> reconstruction,
        Av1PictureControlSet picture,
        Av1EncoderCoefficientBuffer coefficientBuffer,
        Av1EncoderSuperblockWorkspace superblockWorkspace,
        Av1EncoderBlockWorkspace blockWorkspace,
        int effort)
    {
        this.picture = picture;
        this.tileData = Encode<ushort, Av1IntraSuperblockEncoder.UInt16Operator,
            Av1DeblockingFilter.VerticalUInt16EdgeOperator, Av1DeblockingFilter.HorizontalUInt16EdgeOperator>(
            writer,
            source,
            reference,
            reconstruction,
            picture,
            coefficientBuffer,
            new Av1EncoderTileWorkspace(picture.Parent.FrameHeader, superblockWorkspace),
            blockWorkspace,
            effort);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1TileEncoder"/> struct for a high-bit-depth inter frame.
    /// </summary>
    /// <param name="writer">The symbol encoder that retains tile output through the enclosing frame write.</param>
    /// <param name="source">The coded source frame.</param>
    /// <param name="reference">The reconstructed reference frame.</param>
    /// <param name="reconstruction">The reconstructed frame updated during encoding.</param>
    /// <param name="picture">The frame coding and mode-information state.</param>
    /// <param name="coefficientBuffer">The frame-owned quantized coefficient and transform state.</param>
    /// <param name="tileWorkspace">The retained tile, superblock, and entropy cursor graph.</param>
    /// <param name="blockWorkspace">The reusable block arithmetic workspace.</param>
    /// <param name="effort">The mode-search effort in the inclusive range zero through ten.</param>
    public Av1TileEncoder(
        Av1SymbolEncoder writer,
        Av1EncoderFrame<ushort> source,
        Av1EncoderFrame<ushort> reference,
        Av1EncoderFrame<ushort> reconstruction,
        Av1PictureControlSet picture,
        Av1EncoderCoefficientBuffer coefficientBuffer,
        Av1EncoderTileWorkspace tileWorkspace,
        Av1EncoderBlockWorkspace blockWorkspace,
        int effort)
    {
        this.picture = picture;
        this.tileData = Encode<ushort, Av1IntraSuperblockEncoder.UInt16Operator,
            Av1DeblockingFilter.VerticalUInt16EdgeOperator, Av1DeblockingFilter.HorizontalUInt16EdgeOperator>(
            writer,
            source,
            reference,
            reconstruction,
            picture,
            coefficientBuffer,
            tileWorkspace,
            blockWorkspace,
            effort);
    }

    /// <inheritdoc/>
    public ReadOnlySpan<byte> GetTileData(int tileNum)
    {
        int offset = this.picture.TileDataOffsets.Span[tileNum];
        int length = this.picture.TileDataLengths.Span[tileNum];
        return this.tileData.Span.Slice(offset, length);
    }

    private static ReadOnlyMemory<byte> Encode<TSample, TOperator, TVerticalOperator, THorizontalOperator>(
        Av1SymbolEncoder writer,
        Av1EncoderFrame<TSample> source,
        Av1EncoderFrame<TSample> reference,
        Av1EncoderFrame<TSample> reconstruction,
        Av1PictureControlSet picture,
        Av1EncoderCoefficientBuffer coefficientBuffer,
        Av1EncoderTileWorkspace tileWorkspace,
        Av1EncoderBlockWorkspace blockWorkspace,
        int effort)
        where TSample : unmanaged
        where TOperator : struct, Av1IntraSuperblockEncoder.IBlockEncodingOperator<TSample>
        where TVerticalOperator : struct, Av1DeblockingFilter.IEdgeOperator<TSample>
        where THorizontalOperator : struct, Av1DeblockingFilter.IEdgeOperator<TSample>
    {
        Av1PictureParentControlSet parent = picture.Parent;
        ObuFrameHeader frameHeader = parent.FrameHeader;
        Av1MotionSearchSettings motionSettings = new(
            parent.EncodingSpeed,
            picture.Sequence.SequenceHeader.IsStillPicture,
            new Size(source.Width, source.Height),
            frameHeader.QuantizationParameters.BaseQIndex,
            frameHeader.IsIntra,
            parent.IsScreenContent);

        parent.MotionSearchSettings = motionSettings;
        int maximumDimension = Math.Max(source.Width, source.Height);
        int stepParameter = Av1MotionSearchBase.GetInitialStepParameter(maximumDimension);
        if (frameHeader.IsIntra)
        {
            // A key frame seeds the following inter frame with the complete frame range.
            parent.MaximumMotionVectorMagnitude = maximumDimension;
        }
        else if (motionSettings.AutomaticStepSizeLevel != 0)
        {
            if (frameHeader.ShowFrame && motionSettings.AutomaticStepSizeLevel >= 2 && parent.MaximumMotionVectorMagnitude != -1)
            {
                int range = Math.Min(maximumDimension, 2 * parent.MaximumMotionVectorMagnitude);
                stepParameter = Av1MotionSearchBase.GetInitialStepParameter(range);
            }

            // The packing pass accumulates actual NEWMV magnitudes. Trial candidates and inherited vectors
            // do not contribute; a frame with no written NEWMV leaves a zero maximum for the next frame.
            parent.MaximumMotionVectorMagnitude = 0;
        }

        parent.MotionSearchStepParameter = stepParameter;
        _ = ProcessTiles<TSample, TOperator, Av1SymbolEncoder.SymbolUpdateOperation>(
            writer, source, reference, reconstruction, picture, coefficientBuffer, tileWorkspace, blockWorkspace, effort);

        Av1LoopFilterEncoder.ApplyFrame<TSample, TVerticalOperator, THorizontalOperator>(picture, reconstruction);

        // Analysis retains the selected modes, coefficients, palette tokens, and motion contexts. Packing starts
        // from the same entropy edges and probabilities while the completed frame decisions remain available.
        picture.ResetEntropyContexts();
        return ProcessTiles<TSample, TOperator, Av1SymbolEncoder.SymbolWriteOperation>(
            writer, source, reference, reconstruction, picture, coefficientBuffer, tileWorkspace, blockWorkspace, effort);
    }

    private static ReadOnlyMemory<byte> ProcessTiles<TSample, TOperator, TSymbolOperation>(
        Av1SymbolEncoder writer,
        Av1EncoderFrame<TSample> source,
        Av1EncoderFrame<TSample> reference,
        Av1EncoderFrame<TSample> reconstruction,
        Av1PictureControlSet picture,
        Av1EncoderCoefficientBuffer coefficientBuffer,
        Av1EncoderTileWorkspace tileWorkspace,
        Av1EncoderBlockWorkspace blockWorkspace,
        int effort)
        where TSample : unmanaged
        where TOperator : struct, Av1IntraSuperblockEncoder.IBlockEncodingOperator<TSample>
        where TSymbolOperation : struct, Av1SymbolEncoder.ISymbolOperation
    {
        ObuFrameHeader frameHeader = picture.Parent.FrameHeader;
        ObuSequenceHeader sequenceHeader = picture.Sequence.SequenceHeader;
        Av1TileInfo tile = tileWorkspace.Tile;
        Av1Superblock superblock = tileWorkspace.Superblock;
        Av1TileWriter.Av1EntropyCodingContext entropyContext = tileWorkspace.EntropyContext;

        int superblockModeInfoSize = sequenceHeader.SuperblockModeInfoSize;
        int superblockShift = sequenceHeader.SuperblockSizeLog2 - Av1Constants.ModeInfoSizeLog2;
        ObuTileGroupHeader tileLayout = frameHeader.TilesInfo;
        Span<int> tileDataOffsets = picture.TileDataOffsets.Span;
        Span<int> tileDataLengths = picture.TileDataLengths.Span;
        if (!TSymbolOperation.WritesOutput && frameHeader.AllowIntraBlockCopy)
        {
            // Hash the visible source once before reconstruction begins so candidate discovery never depends
            // on coding order and the workspace can be reused as compact bucket links afterward.
            picture.IntraBlockCopySearch.Initialize<TSample, TOperator>(
                source.View.GetPlane(Av1Plane.Y));
        }

        int tileIndex = 0;
        int tileDataEnd = 0;
        for (int tileRow = 0; tileRow < tileLayout.TileRowCount; tileRow++)
        {
            tile.SetTileRow(tileLayout, frameHeader.ModeInfoRowCount, tileRow);
            for (int tileColumn = 0; tileColumn < tileLayout.TileColumnCount; tileColumn++)
            {
                tile.SetTileColumn(tileLayout, frameHeader.ModeInfoColumnCount, tileColumn);

                // Each pass begins every tile from the same frame probabilities. Only the packing pass
                // advances the output offset; the analysis operation does not touch range-coder state.
                writer.Reset(tileDataEnd);

                Point firstModeInfoPosition = new(tile.ModeInfoColumnStart, tile.ModeInfoRowStart);
                entropyContext.MacroBlockModeInfo = picture.GetMacroBlockModeInfo(firstModeInfoPosition);
                for (int modeInfoRow = tile.ModeInfoRowStart;
                    modeInfoRow < tile.ModeInfoRowEnd;
                    modeInfoRow += superblockModeInfoSize)
                {
                    for (int modeInfoColumn = tile.ModeInfoColumnStart;
                        modeInfoColumn < tile.ModeInfoColumnEnd;
                        modeInfoColumn += superblockModeInfoSize)
                    {
                        int superblockRow = modeInfoRow >> superblockShift;
                        int superblockColumn = modeInfoColumn >> superblockShift;
                        superblock.Index = (superblockRow * coefficientBuffer.SuperblockColumnCount) + superblockColumn;
                        entropyContext.SuperblockOrigin = new Point(
                            modeInfoColumn << Av1Constants.ModeInfoSizeLog2,
                            modeInfoRow << Av1Constants.ModeInfoSizeLog2);

                        if (TSymbolOperation.WritesOutput)
                        {
                            Av1TileWriter.RetainedBlockEncodingHandler blockEncoder = new(picture);
                            Av1TileWriter.WriteSuperblock<TSymbolOperation, Av1TileWriter.RetainedBlockEncodingHandler>(
                                picture,
                                entropyContext,
                                writer,
                                superblock,
                                coefficientBuffer,
                                (ushort)tileIndex,
                                ref blockEncoder);
                        }
                        else
                        {
                            if (!frameHeader.IsIntra)
                            {
                                // Candidates within a superblock share one entropy snapshot. Updating while
                                // trying partitions would make the search depend on discarded alternatives.
                                writer.FillMotionVectorCosts(blockWorkspace.GetMotionVectorCosts(frameHeader.MotionVectorPrecision));
                            }

                            Av1IntraSuperblockEncoder.Prepare(
                                picture,
                                superblock,
                                entropyContext.SuperblockOrigin);

                            Av1IntraSuperblockEncoder.ModeDecision<TSample, TOperator> blockEncoder = new(
                                source,
                                reference,
                                reconstruction,
                                picture,
                                superblock,
                                coefficientBuffer,
                                blockWorkspace,
                                effort);

                            Av1TileWriter.WriteSuperblock<
                                TSymbolOperation,
                                Av1IntraSuperblockEncoder.ModeDecision<TSample, TOperator>>(
                                picture,
                                entropyContext,
                                writer,
                                superblock,
                                coefficientBuffer,
                                (ushort)tileIndex,
                                ref blockEncoder);
                        }
                    }
                }

                if (TSymbolOperation.WritesOutput)
                {
                    _ = writer.Exit(out int tileDataLength);
                    tileDataOffsets[tileIndex] = tileDataEnd;
                    tileDataLengths[tileIndex] = tileDataLength;
                    tileDataEnd += tileDataLength;
                }

                tileIndex++;
            }
        }

        return writer.GetOutput(tileDataEnd);
    }
}

/// <summary>
/// Retains the mutable tile, superblock, and entropy cursor graph reused by serial frame encoding.
/// </summary>
internal readonly struct Av1EncoderTileWorkspace
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Av1EncoderTileWorkspace"/> struct.
    /// </summary>
    /// <param name="frameHeader">The fixed-geometry frame header defining tile boundaries.</param>
    /// <param name="superblockWorkspace">The retained superblock decision storage.</param>
    public Av1EncoderTileWorkspace(
        ObuFrameHeader frameHeader,
        Av1EncoderSuperblockWorkspace superblockWorkspace)
    {
        this.Tile = new Av1TileInfo(0, 0, frameHeader);
        this.Superblock = new Av1Superblock
        {
            Workspace = superblockWorkspace,
            TileInfo = this.Tile
        };

        this.EntropyContext = new Av1TileWriter.Av1EntropyCodingContext
        {
            MacroBlock = new Av1MacroBlockD { Tile = this.Tile },
            MacroBlockModeInfo = default
        };
    }

    /// <summary>
    /// Gets the mutable tile boundaries selected during raster traversal.
    /// </summary>
    public Av1TileInfo Tile { get; }

    /// <summary>
    /// Gets the mutable superblock cursor connected to the retained decision workspace.
    /// </summary>
    public Av1Superblock Superblock { get; }

    /// <summary>
    /// Gets the mutable entropy cursor shared by successive superblocks.
    /// </summary>
    public Av1TileWriter.Av1EntropyCodingContext EntropyContext { get; }
}
