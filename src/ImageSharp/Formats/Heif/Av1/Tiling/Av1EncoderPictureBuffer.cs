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
    /// The exact packed state region. The buffer clears this region between frames and does not touch the excess pool capacity.
    /// </summary>
    private readonly Memory<byte> stateMemory;

    /// <summary>
    /// The owner of the intra-block-copy search buffer of each square block size, from 4x4 at index 0. A size without a buffer has no owner.
    /// </summary>
    private InlineArray6<IMemoryOwner<byte>> intraBlockCopySearchStorage;

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
    /// <param name="maximumHashBlockSize">The largest square block eligible for hash search.</param>
    /// <param name="disallow4x4AllFrames">Whether each allocated mode-information value represents an 8x8 region.</param>
    public Av1EncoderPictureBuffer(
        Configuration configuration,
        ObuSequenceHeader sequenceHeader,
        ObuFrameHeader frameHeader,
        int width,
        int height,
        int maximumHashBlockSize,
        bool disallow4x4AllFrames)
        : this(
            configuration,
            sequenceHeader,
            frameHeader,
            width,
            height,
            maximumHashBlockSize,
            disallow4x4AllFrames,
            frameHeader.AllowScreenContentTools,
            frameHeader.AllowIntraBlockCopy || !frameHeader.IsIntra,
            frameHeader.AllowIntraBlockCopy)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1EncoderPictureBuffer"/> class with the maximum state that a fixed-geometry sequence needs.
    /// </summary>
    /// <param name="configuration">The configuration providing picture-lifetime memory.</param>
    /// <param name="sequenceHeader">The sequence header defining superblock and chroma geometry.</param>
    /// <param name="frameHeader">The initial frame header defining dimensions and tiles.</param>
    /// <param name="width">The visible luma width.</param>
    /// <param name="height">The visible luma height.</param>
    /// <param name="maximumHashBlockSize">The largest square block eligible for hash search.</param>
    /// <param name="disallow4x4AllFrames">Whether each allocated mode-information value represents an 8x8 region.</param>
    /// <param name="allocateScreenContentState">Whether any frame can need palette neighbor state.</param>
    /// <param name="allocateMotionVectorState">Whether any frame can need inter or intra-block-copy vectors.</param>
    /// <param name="allocateIntraBlockCopySearch">Whether any frame can need intra-block-copy search state.</param>
    public Av1EncoderPictureBuffer(
        Configuration configuration,
        ObuSequenceHeader sequenceHeader,
        ObuFrameHeader frameHeader,
        int width,
        int height,
        int maximumHashBlockSize,
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
            int blockPaletteStorageLength = allocateScreenContentState
                ? checked(this.modeInfo.Allocation.Length * Unsafe.SizeOf<Av1EncoderPaletteInfo>())
                : 0;

            int paletteTokenStorageOffset = checked(paletteStorageEnd + blockPaletteStorageLength);

            // Each of the two palette planes needs at most one packed token per sample. The capacity rounds both dimensions up to the maximum
            // superblock size, so it is large enough for both supported superblock sizes.
            int paletteTokenStorageLength = allocateScreenContentState
                ? checked(
                    Av1Math.AlignPowerOf2(width, Av1Constants.MaxSuperBlockSizeLog2) *
                    Av1Math.AlignPowerOf2(height, Av1Constants.MaxSuperBlockSizeLog2) *
                    Math.Min(2, colorConfig.PlaneCount))
                : 0;

            int paletteTokenStorageEnd = checked(paletteTokenStorageOffset + paletteTokenStorageLength);
            int blockEncodingStorageLength = checked(this.modeInfo.Allocation.Length * Av1EncoderBlockStruct.StorageSize);
            int blockEncodingStorageEnd = checked(paletteTokenStorageEnd + blockEncodingStorageLength);
            int displacementVectorLength = allocateMotionVectorState ? this.modeInfo.Allocation.Length : 0;
            int displacementVectorStorageOffset = allocateMotionVectorState
                ? Av1Math.AlignPowerOf2(blockEncodingStorageEnd, 1)
                : blockEncodingStorageEnd;

            int displacementVectorStorageLength = checked(
                displacementVectorLength * Unsafe.SizeOf<Av1EncoderDisplacementVector>());

            int displacementVectorStorageEnd = checked(displacementVectorStorageOffset + displacementVectorStorageLength);
            int referenceContextStorageLength = checked(
                displacementVectorLength * Unsafe.SizeOf<Av1EncoderReferenceContext>());

            int referenceContextStorageEnd = checked(displacementVectorStorageEnd + referenceContextStorageLength);
            int tileStateStorageOffset = Av1Math.AlignPowerOf2(referenceContextStorageEnd, 2);
            int cdefPresetLength = tileCount * Av1Constants.CdefUnitsPerSuperblock;
            int tileStateLength = cdefPresetLength + (2 * tileCount);
            int tileStateStorageLength = tileStateLength * sizeof(int);
            int restorationStorageOffset = checked(tileStateStorageOffset + tileStateStorageLength);
            InlineArray3<int> restorationLengthStorage = default;
            Span<int> restorationLengths = restorationLengthStorage;
            int restorationUnitCount = 0;
            int restorationReferenceCount = 0;
            if (sequenceHeader.EnableRestoration)
            {
                int minimumUnitSize = 1 << sequenceHeader.SuperblockSizeLog2;
                for (int plane = 0; plane < colorConfig.PlaneCount; plane++)
                {
                    int subX = plane != 0 && colorConfig.SubSamplingX ? 1 : 0;
                    int subY = plane != 0 && colorConfig.SubSamplingY ? 1 : 0;
                    int planeWidth = Av1Math.DivideLog2Ceiling(width, subX);
                    int planeHeight = Av1Math.DivideLog2Ceiling(height, subY);
                    int columns = Math.Max(1, (planeWidth + (minimumUnitSize >> 1)) / minimumUnitSize);
                    int rows = Math.Max(1, (planeHeight + (minimumUnitSize >> 1)) / minimumUnitSize);
                    restorationLengths[plane] = checked(columns * rows);
                    restorationUnitCount = checked(restorationUnitCount + restorationLengths[plane]);
                }

                restorationReferenceCount = checked(tileCount * colorConfig.PlaneCount);
            }

            int restorationStorageLength = checked((restorationUnitCount + restorationReferenceCount) * Unsafe.SizeOf<Av1LoopRestorationUnit>());
            int stateStorageLength = checked(restorationStorageOffset + restorationStorageLength);

            // One clean allocation holds the segmentation map and every tile edge for the life of the picture. The partition region begins at
            // its native alignment. The CDEF presets, the previous quantizers and the encoded tile lengths share one aligned integer region at
            // the end. This avoids separate managed arrays for every picture.
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
            Memory<Av1EncoderPaletteInfo> blockPalettes = Memory<Av1EncoderPaletteInfo>.Empty;
            if (allocateScreenContentState)
            {
                // Palette entries contain 16-bit colors, so their packed typed region begins at an even byte offset.
                ByteMemoryManager<Av1EncoderPaletteInfo> paletteMemory = new(
                    stateStorage.Slice(paletteStorageOffset, paletteStorageLength));

                paletteStorage = paletteMemory.Memory;
                ByteMemoryManager<Av1EncoderPaletteInfo> blockPaletteMemory = new(
                    stateStorage.Slice(paletteStorageEnd, blockPaletteStorageLength));

                blockPalettes = blockPaletteMemory.Memory;
                this.paletteContexts = new Av1NeighborArrayUnit<Av1EncoderPaletteInfo>[tileCount];
            }
            else
            {
                this.paletteContexts = [];
            }

            Memory<Av1EncoderDisplacementVector> displacementVectors = Memory<Av1EncoderDisplacementVector>.Empty;

            // The final syntax parameters use their own eight-byte entries, so frequent neighbor lookups keep the compact mode-information
            // layout. This typed view borrows the same owner of the picture state.
            ByteMemoryManager<Av1EncoderBlockStruct> blockEncodingMemory = new(
                stateStorage.Slice(paletteTokenStorageEnd, blockEncodingStorageLength));

            Memory<Av1EncoderReferenceContext> referenceContexts = Memory<Av1EncoderReferenceContext>.Empty;
            if (allocateMotionVectorState)
            {
                // Each component lies strictly inside plus or minus 16384. Two signed 16-bit fields hold both inter and intra-block-copy vectors
                // without a larger compact mode-information entry.
                ByteMemoryManager<Av1EncoderDisplacementVector> displacementVectorMemory = new(
                    stateStorage.Slice(displacementVectorStorageOffset, displacementVectorStorageLength));

                displacementVectors = displacementVectorMemory.Memory;
                ByteMemoryManager<Av1EncoderReferenceContext> referenceContextMemory = new(
                    stateStorage.Slice(displacementVectorStorageEnd, referenceContextStorageLength));

                referenceContexts = referenceContextMemory.Memory;
            }

            Av1IntraBlockCopySearchIndex intraBlockCopySearch = default;
            if (allocateIntraBlockCopySearch)
            {
                // Each block size has its own buffer outside the packed picture state. One buffer for all sizes grows past the pool block size on
                // small pictures. A larger buffer comes from native memory, and its memory pressure starts a full collection for each picture.
                // Before each frame, the index writes every value that it reads, so the buffers need no clear.
                InlineArray6<Memory<byte>> levels = default;
                int maximumStoredSize = Av1IntraBlockCopySearchIndex.GetMaximumStoredSize(width, height, maximumHashBlockSize);
                for (int size = 4, level = 0; size <= maximumStoredSize; size <<= 1, level++)
                {
                    int length = Av1IntraBlockCopySearchIndex.GetLevelStorageLength(width, height, size);
                    this.intraBlockCopySearchStorage[level] = configuration.MemoryAllocator.Allocate<byte>(length);
                    levels[level] = this.intraBlockCopySearchStorage[level].Memory[..length];
                }

                intraBlockCopySearch = new Av1IntraBlockCopySearchIndex(levels, width, height, maximumHashBlockSize);
            }

            ByteMemoryManager<int> tileStateMemory = new(
                stateStorage.Slice(tileStateStorageOffset, tileStateStorageLength));

            Memory<int> tileState = tileStateMemory.Memory;
            Memory<int> cdefPreset = tileState[..cdefPresetLength];
            Memory<int> previousQIndex = tileState.Slice(cdefPresetLength, tileCount);
            Memory<int> tileDataLengths = tileState.Slice(cdefPresetLength + tileCount, tileCount);
            Memory<Av1LoopRestorationUnit> restorationUnits = default;
            InlineArray3<int> restorationUnitOffsets = default;
            Memory<Av1LoopRestorationUnit> restorationReferences = default;
            if (sequenceHeader.EnableRestoration)
            {
                // The unit decisions and the tile histories share the existing picture owner. The smallest allowed unit size sets the capacity.
                // Larger units use a shorter prefix.
                ByteMemoryManager<Av1LoopRestorationUnit> restorationMemory = new(
                    stateStorage.Slice(restorationStorageOffset, restorationStorageLength));

                Memory<Av1LoopRestorationUnit> restorationStorage = restorationMemory.Memory;
                Span<int> unitOffsets = restorationUnitOffsets;
                int offset = 0;
                for (int plane = 0; plane < colorConfig.PlaneCount; plane++)
                {
                    unitOffsets[plane] = offset;
                    offset += restorationLengths[plane];
                }

                restorationUnits = restorationStorage[..offset];
                restorationReferences = restorationStorage.Slice(offset, restorationReferenceCount);
                restorationReferences.Span.Fill(Av1LoopRestorationUnit.CreateDefault());
            }

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

                // Variable-transform contexts read both edges without separate availability flags. The largest transform size makes an
                // unavailable edge compare as unsplit until a coded neighbor writes its size.
                this.transformContexts[tileIndex].Fill((byte)Av1Constants.MaxTransformSize);

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
                ReferenceContexts = referenceContexts,
                BlockEncodings = blockEncodingMemory.Memory,
                BlockPalettes = blockPalettes,
                PaletteTokens = stateStorage.Slice(paletteTokenStorageOffset, paletteTokenStorageLength),
                IntraBlockCopySearch = intraBlockCopySearch,
                ModeInfoStride = this.modeInfo.ModeInfoStride,
                Disallow4x4AllFrames = this.modeInfo.Disallow4x4AllFrames,
                CdefPreset = cdefPreset,
                TileDataLengths = tileDataLengths,
                RestorationUnits = restorationUnits,
                RestorationUnitOffsets = restorationUnitOffsets,
                RestorationReferences = restorationReferences
            };
        }
        catch
        {
            // The context objects only borrow these owners. A failed constructor releases the completed allocations itself, because the
            // enclosing sequence never receives this picture.
            this.stateStorage?.Dispose();
            this.DisposeIntraBlockCopySearchStorage();
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

        // Transform contexts begin at the largest transform size until an encoded neighbor writes its selected size. The clear of the packed
        // state removes this sentinel, so the loop writes it again.
        foreach (Av1NeighborArrayUnit<byte> context in this.transformContexts)
        {
            context.Fill((byte)Av1Constants.MaxTransformSize);
        }

        this.Picture.CdefPreset.Span.Fill(-1);
        this.Picture.RestorationReferences.Span.Fill(Av1LoopRestorationUnit.CreateDefault());
        this.Picture.Parent.PreviousQIndex.Span.Fill(frameHeader.QuantizationParameters.BaseQIndex);
        this.Picture.Parent.FrameHeader = frameHeader;
        this.Picture.Parent.Common.FrameSize = frameHeader.FrameSize;
        this.Picture.Parent.Common.TilesInfo = frameHeader.TilesInfo;

        // A frame of a scaled layer is smaller than the allocation. It uses the first columns of the first grid rows, at the stride of the
        // allocation.
        this.Picture.Parent.Common.ModeInfoColumnCount = frameHeader.ModeInfoColumnCount;
        this.Picture.Parent.Common.ModeInfoRowCount = frameHeader.ModeInfoRowCount;

        // Each frame measures its own SSIM factors unless the sequence encoder measures them for it.
        this.Picture.Parent.HasPrecomputedSsimRateMultiplierFactors = false;
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
        this.DisposeIntraBlockCopySearchStorage();
        this.modeInfo.Dispose();
    }

    /// <summary>
    /// Returns the allocated intra-block-copy search buffers to the configured allocator.
    /// </summary>
    private void DisposeIntraBlockCopySearchStorage()
    {
        // Sizes larger than the picture, and every size when the search is not allocated, have no owner.
        foreach (IMemoryOwner<byte> owner in this.intraBlockCopySearchStorage)
        {
            owner?.Dispose();
        }
    }
}
