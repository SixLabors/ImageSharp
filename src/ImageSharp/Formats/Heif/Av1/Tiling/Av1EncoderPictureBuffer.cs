// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Owns reusable mode information, segmentation data, and tile-neighbor contexts for fixed-geometry AV1 pictures.
/// </summary>
internal sealed class Av1EncoderPictureBuffer : IDisposable
{
    private readonly Av1EncoderModeInfoBuffer modeInfo;
    private readonly IMemoryOwner<byte> stateStorage;

    /// <summary>
    /// The exact packed state region cleared between frames without touching excess pool capacity.
    /// </summary>
    private readonly Memory<byte> stateMemory;

    private readonly ByteMemoryManager<Av1PartitionContext> partitionContextMemory;
    private readonly Av1NeighborArrayUnit<Av1PartitionContext>[] partitionContexts;
    private readonly Av1NeighborArrayUnit<byte>[] lumaCoefficientContexts;
    private readonly Av1NeighborArrayUnit<byte>[] blueCoefficientContexts;
    private readonly Av1NeighborArrayUnit<byte>[] redCoefficientContexts;
    private readonly Av1NeighborArrayUnit<byte>[] transformContexts;
    private readonly Av1NeighborArrayUnit<Av1EncoderPaletteInfo>[] paletteContexts;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1EncoderPictureBuffer"/> class.
    /// </summary>
    /// <param name="configuration">The configuration providing picture-lifetime memory.</param>
    /// <param name="sequenceHeader">The sequence header defining superblock and chroma geometry.</param>
    /// <param name="frameHeader">The frame header defining dimensions and tiles.</param>
    /// <param name="width">The visible luma width.</param>
    /// <param name="height">The visible luma height.</param>
    /// <param name="disallow4x4AllFrames">Whether each allocated mode-information value represents an 8x8 region.</param>
    public Av1EncoderPictureBuffer(
        Configuration configuration,
        ObuSequenceHeader sequenceHeader,
        ObuFrameHeader frameHeader,
        int width,
        int height,
        bool disallow4x4AllFrames)
        : this(
            configuration,
            sequenceHeader,
            frameHeader,
            width,
            height,
            disallow4x4AllFrames,
            frameHeader.AllowScreenContentTools,
            frameHeader.AllowIntraBlockCopy || !frameHeader.IsIntra,
            frameHeader.AllowIntraBlockCopy)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1EncoderPictureBuffer"/> class with the maximum state
    /// required by a fixed-geometry sequence.
    /// </summary>
    /// <param name="configuration">The configuration providing picture-lifetime memory.</param>
    /// <param name="sequenceHeader">The sequence header defining superblock and chroma geometry.</param>
    /// <param name="frameHeader">The initial frame header defining dimensions and tiles.</param>
    /// <param name="width">The visible luma width.</param>
    /// <param name="height">The visible luma height.</param>
    /// <param name="disallow4x4AllFrames">Whether each allocated mode-information value represents an 8x8 region.</param>
    /// <param name="allocateScreenContentState">Whether palette neighbor state can be required by any frame.</param>
    /// <param name="allocateMotionVectorState">Whether inter or intra-block-copy vectors can be required by any frame.</param>
    /// <param name="allocateIntraBlockCopySearch">Whether intra-block-copy search state can be required by any frame.</param>
    public Av1EncoderPictureBuffer(
        Configuration configuration,
        ObuSequenceHeader sequenceHeader,
        ObuFrameHeader frameHeader,
        int width,
        int height,
        bool disallow4x4AllFrames,
        bool allocateScreenContentState,
        bool allocateMotionVectorState,
        bool allocateIntraBlockCopySearch)
    {
        const int ContextAlignmentLog2 = Av1Constants.MaxSuperBlockSizeLog2 - Av1Constants.ModeInfoSizeLog2;
        this.modeInfo = new Av1EncoderModeInfoBuffer(
            configuration,
            width,
            height,
            disallow4x4AllFrames);

        try
        {
            int alignedModeInfoRowCount = Av1Math.AlignPowerOf2(this.modeInfo.ModeInfoRowCount, ContextAlignmentLog2);
            int lumaLeftLength = alignedModeInfoRowCount;
            int lumaTopLength = this.modeInfo.ModeInfoStride;
            ObuColorConfig colorConfig = sequenceHeader.ColorConfig;
            int chromaLeftLength = colorConfig.IsMonochrome
                ? 0
                : lumaLeftLength >> (colorConfig.SubSamplingY ? 1 : 0);

            int chromaTopLength = colorConfig.IsMonochrome
                ? 0
                : lumaTopLength >> (colorConfig.SubSamplingX ? 1 : 0);

            int tileCount = frameHeader.TilesInfo.TileColumnCount * frameHeader.TilesInfo.TileRowCount;
            int lumaContextLength = checked(lumaLeftLength + lumaTopLength);
            int chromaContextLength = checked(chromaLeftLength + chromaTopLength);
            int byteContextLengthPerTile = checked((2 * lumaContextLength) + (2 * chromaContextLength));
            int partitionContextLength = checked(tileCount * lumaContextLength);
            int segmentationLength = checked(this.modeInfo.ModeInfoColumnCount * this.modeInfo.ModeInfoRowCount);
            int partitionContextSize = Unsafe.SizeOf<Av1PartitionContext>();
            int partitionStorageOffset = checked(
                ((segmentationLength + partitionContextSize - 1) / partitionContextSize) * partitionContextSize);

            int partitionStorageLength = checked(
                partitionContextLength * Unsafe.SizeOf<Av1PartitionContext>());

            int byteContextStorageOffset = checked(partitionStorageOffset + partitionStorageLength);
            int byteContextStorageLength = checked(tileCount * byteContextLengthPerTile);
            int byteContextStorageEnd = checked(byteContextStorageOffset + byteContextStorageLength);
            int paletteLeftLength = alignedModeInfoRowCount;
            int paletteTopLength = this.modeInfo.ModeInfoStride;
            int paletteContextLength = checked(paletteLeftLength + paletteTopLength);
            int paletteStorageOffset = allocateScreenContentState
                ? Av1Math.AlignPowerOf2(byteContextStorageEnd, 1)
                : byteContextStorageEnd;

            int paletteStorageLength = allocateScreenContentState
                ? checked(tileCount * paletteContextLength * Unsafe.SizeOf<Av1EncoderPaletteInfo>())
                : 0;

            int paletteStorageEnd = checked(paletteStorageOffset + paletteStorageLength);
            int displacementVectorLength = allocateMotionVectorState ? this.modeInfo.Allocation.Length : 0;
            int displacementVectorStorageOffset = allocateMotionVectorState
                ? Av1Math.AlignPowerOf2(paletteStorageEnd, 1)
                : paletteStorageEnd;

            int displacementVectorStorageLength = checked(
                displacementVectorLength * Unsafe.SizeOf<Av1EncoderDisplacementVector>());

            int displacementVectorStorageEnd = checked(displacementVectorStorageOffset + displacementVectorStorageLength);
            int intraBlockCopySearchStorageOffset = allocateIntraBlockCopySearch
                ? Av1Math.AlignPowerOf2(displacementVectorStorageEnd, 2)
                : displacementVectorStorageEnd;

            int intraBlockCopySearchStorageLength = allocateIntraBlockCopySearch
                ? Av1IntraBlockCopySearchIndex.GetStorageLength(width, height)
                : 0;

            int intraBlockCopySearchStorageEnd = checked(
                intraBlockCopySearchStorageOffset + intraBlockCopySearchStorageLength);

            int tileStateStorageOffset = Av1Math.AlignPowerOf2(intraBlockCopySearchStorageEnd, 2);
            int cdefPresetLength = tileCount * Av1Constants.CdefUnitsPerSuperblock;
            int tileStateLength = cdefPresetLength + (3 * tileCount);
            int tileStateStorageLength = tileStateLength * sizeof(int);
            int stateStorageLength = checked(tileStateStorageOffset + tileStateStorageLength);

            // Segmentation and every tile edge share one clean picture lifetime. The partition region begins at its
            // native alignment. CDEF, quantizer, and encoded-tile bounds occupy one aligned trailing integer region
            // instead of allocating separate managed arrays for every picture.
            this.stateStorage = configuration.MemoryAllocator.Allocate<byte>(
                stateStorageLength,
                AllocationOptions.Clean);

            this.stateMemory = this.stateStorage.Memory[..stateStorageLength];
            Memory<byte> stateStorage = this.stateMemory;
            this.partitionContextMemory = new ByteMemoryManager<Av1PartitionContext>(
                stateStorage.Slice(partitionStorageOffset, partitionStorageLength));

            Memory<Av1PartitionContext> partitionStorage = this.partitionContextMemory.Memory;
            Memory<byte> byteContextStorage = stateStorage.Slice(byteContextStorageOffset, byteContextStorageLength);
            this.partitionContexts = new Av1NeighborArrayUnit<Av1PartitionContext>[tileCount];
            this.lumaCoefficientContexts = new Av1NeighborArrayUnit<byte>[tileCount];
            this.blueCoefficientContexts = new Av1NeighborArrayUnit<byte>[tileCount];
            this.redCoefficientContexts = new Av1NeighborArrayUnit<byte>[tileCount];
            this.transformContexts = new Av1NeighborArrayUnit<byte>[tileCount];
            Memory<Av1EncoderPaletteInfo> paletteStorage = Memory<Av1EncoderPaletteInfo>.Empty;
            if (allocateScreenContentState)
            {
                // Palette entries contain 16-bit colors, so their packed typed region begins at an even byte offset.
                ByteMemoryManager<Av1EncoderPaletteInfo> paletteMemory = new(
                    stateStorage.Slice(paletteStorageOffset, paletteStorageLength));

                paletteStorage = paletteMemory.Memory;
                this.paletteContexts = new Av1NeighborArrayUnit<Av1EncoderPaletteInfo>[tileCount];
            }
            else
            {
                this.paletteContexts = [];
            }

            Memory<Av1EncoderDisplacementVector> displacementVectors = Memory<Av1EncoderDisplacementVector>.Empty;
            if (allocateMotionVectorState)
            {
                // Each component lies strictly inside plus or minus 16384. Two signed 16-bit fields preserve both
                // inter and intra-block-copy vectors without expanding every compact mode-information entry.
                ByteMemoryManager<Av1EncoderDisplacementVector> displacementVectorMemory = new(
                    stateStorage.Slice(displacementVectorStorageOffset, displacementVectorStorageLength));

                displacementVectors = displacementVectorMemory.Memory;
            }

            Av1IntraBlockCopySearchIndex intraBlockCopySearch = default;
            if (allocateIntraBlockCopySearch)
            {
                // The search index casts its packed workspace to 32-bit links, so its non-owning region begins at
                // a four-byte boundary inside the existing picture-state rent.
                intraBlockCopySearch = new Av1IntraBlockCopySearchIndex(
                    stateStorage.Slice(intraBlockCopySearchStorageOffset, intraBlockCopySearchStorageLength),
                    width,
                    height);
            }

            ByteMemoryManager<int> tileStateMemory = new(
                stateStorage.Slice(tileStateStorageOffset, tileStateStorageLength));

            Memory<int> tileState = tileStateMemory.Memory;
            Memory<int> cdefPreset = tileState[..cdefPresetLength];
            Memory<int> previousQIndex = tileState.Slice(cdefPresetLength, tileCount);
            Memory<int> tileDataOffsets = tileState.Slice(cdefPresetLength + tileCount, tileCount);
            Memory<int> tileDataLengths = tileState.Slice(cdefPresetLength + (2 * tileCount), tileCount);
            cdefPreset.Span.Fill(-1);
            for (int tileIndex = 0; tileIndex < tileCount; tileIndex++)
            {
                this.partitionContexts[tileIndex] = new Av1NeighborArrayUnit<Av1PartitionContext>(
                    partitionStorage.Slice(tileIndex * lumaContextLength, lumaContextLength),
                    lumaLeftLength,
                    lumaTopLength)
                {
                    GranularityNormalLog2 = Av1Constants.ModeInfoSizeLog2
                };

                int byteContextOffset = tileIndex * byteContextLengthPerTile;
                this.lumaCoefficientContexts[tileIndex] = new Av1NeighborArrayUnit<byte>(
                    byteContextStorage.Slice(byteContextOffset, lumaContextLength),
                    lumaLeftLength,
                    lumaTopLength)
                {
                    GranularityNormalLog2 = Av1Constants.ModeInfoSizeLog2
                };

                byteContextOffset += lumaContextLength;
                this.blueCoefficientContexts[tileIndex] = new Av1NeighborArrayUnit<byte>(
                    byteContextStorage.Slice(byteContextOffset, chromaContextLength),
                    chromaLeftLength,
                    chromaTopLength)
                {
                    GranularityNormalLog2 = Av1Constants.ModeInfoSizeLog2
                };

                byteContextOffset += chromaContextLength;
                this.redCoefficientContexts[tileIndex] = new Av1NeighborArrayUnit<byte>(
                    byteContextStorage.Slice(byteContextOffset, chromaContextLength),
                    chromaLeftLength,
                    chromaTopLength)
                {
                    GranularityNormalLog2 = Av1Constants.ModeInfoSizeLog2
                };

                byteContextOffset += chromaContextLength;
                this.transformContexts[tileIndex] = new Av1NeighborArrayUnit<byte>(
                    byteContextStorage.Slice(byteContextOffset, lumaContextLength),
                    lumaLeftLength,
                    lumaTopLength)
                {
                    GranularityNormalLog2 = Av1Constants.ModeInfoSizeLog2
                };

                // Variable-transform contexts consult both edges without separate availability flags. The largest
                // transform makes an unavailable edge compare as unsplit until a coded neighbor publishes its size.
                this.transformContexts[tileIndex].Left.Fill((byte)Av1Constants.MaxTransformSize);
                this.transformContexts[tileIndex].Top.Fill((byte)Av1Constants.MaxTransformSize);

                if (allocateScreenContentState)
                {
                    this.paletteContexts[tileIndex] = new Av1NeighborArrayUnit<Av1EncoderPaletteInfo>(
                        paletteStorage.Slice(tileIndex * paletteContextLength, paletteContextLength),
                        paletteLeftLength,
                        paletteTopLength)
                    {
                        GranularityNormalLog2 = Av1Constants.ModeInfoSizeLog2
                    };
                }

                previousQIndex.Span[tileIndex] = frameHeader.QuantizationParameters.BaseQIndex;
            }

            this.Picture = new Av1PictureControlSet
            {
                PartitionContexts = this.partitionContexts,
                LuminanceDcSignLevelCoefficientNeighbors = this.lumaCoefficientContexts,
                CbDcSignLevelCoefficientNeighbors = this.blueCoefficientContexts,
                CrDcSignLevelCoefficientNeighbors = this.redCoefficientContexts,
                TransformFunctionContexts = this.transformContexts,
                PaletteContexts = this.paletteContexts,
                Sequence = new Av1SequenceControlSet { SequenceHeader = sequenceHeader },
                Parent = new Av1PictureParentControlSet
                {
                    Common = new Av1EncoderCommon
                    {
                        ModeInfoColumnCount = this.modeInfo.ModeInfoColumnCount,
                        ModeInfoRowCount = this.modeInfo.ModeInfoRowCount,
                        ModeInfoStride = this.modeInfo.ModeInfoStride,
                        FrameSize = frameHeader.FrameSize,
                        TilesInfo = frameHeader.TilesInfo
                    },
                    FrameHeader = frameHeader,
                    PreviousQIndex = previousQIndex
                },
                SegmentationNeighborMap = stateStorage[..segmentationLength],
                ModeInfoGrid = this.modeInfo.Grid,
                ModeInfoAllocation = this.modeInfo.Allocation,
                DisplacementVectors = displacementVectors,
                IntraBlockCopySearch = intraBlockCopySearch,
                ModeInfoStride = this.modeInfo.ModeInfoStride,
                Disallow4x4AllFrames = this.modeInfo.Disallow4x4AllFrames,
                CdefPreset = cdefPreset,
                TileDataOffsets = tileDataOffsets,
                TileDataLengths = tileDataLengths
            };
        }
        catch
        {
            // The context objects only borrow these two owners. A failed constructor must release the
            // completed allocations itself because the enclosing sequence never receives this picture.
            this.stateStorage?.Dispose();
            this.modeInfo.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Gets the non-owning picture state consumed by superblock analysis and tile writing.
    /// </summary>
    public Av1PictureControlSet Picture { get; }

    /// <summary>
    /// Restores clean per-frame state while retaining every fixed-geometry allocation.
    /// </summary>
    /// <param name="frameHeader">The frame header consumed by the next encoding pass.</param>
    public void Reset(ObuFrameHeader frameHeader)
    {
        this.modeInfo.Grid.Span.Clear();
        this.modeInfo.Allocation.Span.Clear();
        this.stateMemory.Span.Clear();

        // Transform contexts begin at the largest transform size until an encoded neighbor publishes its
        // selected size. This sentinel must be restored after the packed state owner is cleared.
        foreach (Av1NeighborArrayUnit<byte> context in this.transformContexts)
        {
            context.Left.Fill((byte)Av1Constants.MaxTransformSize);
            context.Top.Fill((byte)Av1Constants.MaxTransformSize);
        }

        this.Picture.CdefPreset.Span.Fill(-1);
        this.Picture.Parent.PreviousQIndex.Span.Fill(frameHeader.QuantizationParameters.BaseQIndex);
        this.Picture.Parent.FrameHeader = frameHeader;
        this.Picture.Parent.Common.FrameSize = frameHeader.FrameSize;
        this.Picture.Parent.Common.TilesInfo = frameHeader.TilesInfo;
    }

    /// <summary>
    /// Returns every picture-lifetime allocation to the configured allocator.
    /// </summary>
    public void Dispose()
    {
        foreach (Av1NeighborArrayUnit<Av1PartitionContext> context in this.partitionContexts)
        {
            context.Dispose();
        }

        foreach (Av1NeighborArrayUnit<Av1EncoderPaletteInfo> context in this.paletteContexts)
        {
            context.Dispose();
        }

        for (int tileIndex = 0; tileIndex < this.lumaCoefficientContexts.Length; tileIndex++)
        {
            this.lumaCoefficientContexts[tileIndex].Dispose();
            this.blueCoefficientContexts[tileIndex].Dispose();
            this.redCoefficientContexts[tileIndex].Dispose();
            this.transformContexts[tileIndex].Dispose();
        }

        this.stateStorage.Dispose();
        this.modeInfo.Dispose();
    }
}
