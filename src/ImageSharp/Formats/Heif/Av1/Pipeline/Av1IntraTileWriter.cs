// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Encodes and owns one range-coded all-intra tile payload.
/// </summary>
internal sealed partial class Av1IntraTileWriter : IAv1TileWriter, IDisposable
{
    private IMemoryOwner<byte>? tileData;
    private readonly int tileDataLength;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1IntraTileWriter"/> class for eight-bit samples.
    /// </summary>
    /// <param name="configuration">The configuration providing tile output memory.</param>
    /// <param name="source">The coded source frame.</param>
    /// <param name="reconstruction">The reconstructed frame updated during encoding.</param>
    /// <param name="picture">The frame coding and mode-information state.</param>
    /// <param name="coefficientBuffer">The frame-owned quantized coefficient and transform state.</param>
    /// <param name="superblockWorkspace">The reusable partition and final-block decision workspace.</param>
    /// <param name="blockWorkspace">The reusable block arithmetic workspace.</param>
    /// <param name="initialSize">The estimated encoded tile size in bytes.</param>
    public Av1IntraTileWriter(
        Configuration configuration,
        Av1EncoderFrame<byte> source,
        Av1EncoderFrame<byte> reconstruction,
        Av1PictureControlSet picture,
        Av1EncoderCoefficientBuffer coefficientBuffer,
        Av1EncoderSuperblockWorkspace superblockWorkspace,
        Av1EncoderBlockWorkspace blockWorkspace,
        int initialSize)
    {
        this.tileData = Encode<byte, ByteOperator>(
            configuration,
            source,
            reconstruction,
            picture,
            coefficientBuffer,
            superblockWorkspace,
            blockWorkspace,
            initialSize,
            out this.tileDataLength);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1IntraTileWriter"/> class for high-bit-depth samples.
    /// </summary>
    /// <param name="configuration">The configuration providing tile output memory.</param>
    /// <param name="source">The coded source frame.</param>
    /// <param name="reconstruction">The reconstructed frame updated during encoding.</param>
    /// <param name="picture">The frame coding and mode-information state.</param>
    /// <param name="coefficientBuffer">The frame-owned quantized coefficient and transform state.</param>
    /// <param name="superblockWorkspace">The reusable partition and final-block decision workspace.</param>
    /// <param name="blockWorkspace">The reusable block arithmetic workspace.</param>
    /// <param name="initialSize">The estimated encoded tile size in bytes.</param>
    public Av1IntraTileWriter(
        Configuration configuration,
        Av1EncoderFrame<ushort> source,
        Av1EncoderFrame<ushort> reconstruction,
        Av1PictureControlSet picture,
        Av1EncoderCoefficientBuffer coefficientBuffer,
        Av1EncoderSuperblockWorkspace superblockWorkspace,
        Av1EncoderBlockWorkspace blockWorkspace,
        int initialSize)
    {
        this.tileData = Encode<ushort, UInt16Operator>(
            configuration,
            source,
            reconstruction,
            picture,
            coefficientBuffer,
            superblockWorkspace,
            blockWorkspace,
            initialSize,
            out this.tileDataLength);
    }

    /// <inheritdoc/>
    public ReadOnlySpan<byte> GetTileData(int tileNum)
    {
        ObjectDisposedException.ThrowIf(this.tileData is null, this);
        return this.tileData.Memory.Span[..this.tileDataLength];
    }

    /// <summary>
    /// Returns the detached range-coded tile allocation to the configured allocator.
    /// </summary>
    public void Dispose()
    {
        this.tileData?.Dispose();
        this.tileData = null;
    }

    private static IMemoryOwner<byte> Encode<TSample, TOperator>(
        Configuration configuration,
        Av1EncoderFrame<TSample> source,
        Av1EncoderFrame<TSample> reconstruction,
        Av1PictureControlSet picture,
        Av1EncoderCoefficientBuffer coefficientBuffer,
        Av1EncoderSuperblockWorkspace superblockWorkspace,
        Av1EncoderBlockWorkspace blockWorkspace,
        int initialSize,
        out int tileDataLength)
        where TSample : unmanaged
        where TOperator : struct, ITileEncodingOperator<TSample>
    {
        ObuFrameHeader frameHeader = picture.Parent.FrameHeader;
        ObuSequenceHeader sequenceHeader = picture.Sequence.SequenceHeader;
        const ushort TileIndex = 0;
        Av1TileInfo tile = new(0, 0, frameHeader);
        Av1Superblock superblock = new()
        {
            Workspace = superblockWorkspace,
            TileInfo = tile
        };

        Point firstModeInfoPosition = new(tile.ModeInfoColumnStart, tile.ModeInfoRowStart);
        Av1TileWriter.Av1EntropyCodingContext entropyContext = new()
        {
            MacroBlock = new Av1MacroBlockD { Tile = tile },
            MacroBlockModeInfo = picture.GetMacroBlockModeInfo(firstModeInfoPosition)
        };

        using Av1SymbolEncoder writer = new(
            configuration,
            initialSize,
            frameHeader.QuantizationParameters.BaseQIndex,
            updateCdf: !frameHeader.DisableCdfUpdate);

        int superblockModeInfoSize = sequenceHeader.SuperblockModeInfoSize;
        int superblockShift = sequenceHeader.SuperblockSizeLog2 - Av1Constants.ModeInfoSizeLog2;
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

                // Analyze immediately before entropy coding so the reusable decision workspace and reconstructed
                // neighbors remain synchronized without a second superblock-sized decision allocation.
                TOperator.EncodeSuperblock(
                    source,
                    reconstruction,
                    picture,
                    superblock,
                    coefficientBuffer,
                    blockWorkspace);

                Av1TileWriter.WriteSuperblock(
                    picture,
                    entropyContext,
                    writer,
                    superblock,
                    coefficientBuffer,
                    TileIndex);
            }
        }

        return writer.Exit(out tileDataLength);
    }
}
