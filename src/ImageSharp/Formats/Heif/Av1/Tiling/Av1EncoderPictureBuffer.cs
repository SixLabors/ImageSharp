// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Owns mode information, segmentation data, and tile-neighbor contexts for one encoded AV1 picture.
/// </summary>
internal sealed class Av1EncoderPictureBuffer : IDisposable
{
    private readonly Av1EncoderModeInfoBuffer modeInfo;
    private readonly IMemoryOwner<byte> stateStorage;
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
    public Av1EncoderPictureBuffer(
        Configuration configuration,
        ObuSequenceHeader sequenceHeader,
        ObuFrameHeader frameHeader,
        int width,
        int height)
    {
        const int ContextAlignmentLog2 = Av1Constants.MaxSuperBlockSizeLog2 - Av1Constants.ModeInfoSizeLog2;
        this.modeInfo = new Av1EncoderModeInfoBuffer(
            configuration,
            width,
            height,
            disallow4x4AllFrames: true);

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
        int paletteStorageOffset = frameHeader.AllowScreenContentTools
            ? Av1Math.AlignPowerOf2(byteContextStorageEnd, 1)
            : byteContextStorageEnd;

        int paletteStorageLength = frameHeader.AllowScreenContentTools
            ? checked(tileCount * paletteContextLength * Unsafe.SizeOf<Av1EncoderPaletteInfo>())
            : 0;

        int paletteStorageEnd = checked(paletteStorageOffset + paletteStorageLength);
        int displacementVectorLength = frameHeader.AllowIntraBlockCopy ? this.modeInfo.Allocation.Length : 0;
        int displacementVectorStorageOffset = frameHeader.AllowIntraBlockCopy
            ? Av1Math.AlignPowerOf2(paletteStorageEnd, 1)
            : paletteStorageEnd;

        int displacementVectorStorageLength = checked(
            displacementVectorLength * Unsafe.SizeOf<Av1EncoderDisplacementVector>());

        int stateStorageLength = checked(displacementVectorStorageOffset + displacementVectorStorageLength);

        // Segmentation and every tile edge share one clean picture lifetime. The partition region begins at its
        // native alignment, while typed views keep the entropy writer independent from the packed byte owner.
        this.stateStorage = configuration.MemoryAllocator.Allocate<byte>(
            stateStorageLength,
            AllocationOptions.Clean);

        Memory<byte> stateStorage = this.stateStorage.Memory[..stateStorageLength];
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
        if (frameHeader.AllowScreenContentTools)
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
        if (frameHeader.AllowIntraBlockCopy)
        {
            // Each component lies strictly inside plus or minus 16384, so two signed 16-bit fields preserve the
            // complete syntax domain without expanding every frame's compact mode-information allocation.
            ByteMemoryManager<Av1EncoderDisplacementVector> displacementVectorMemory = new(
                stateStorage.Slice(displacementVectorStorageOffset, displacementVectorStorageLength));

            displacementVectors = displacementVectorMemory.Memory;
        }

        int[][] cdefPreset = new int[tileCount][];
        int[] previousQIndex = new int[tileCount];
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

            if (frameHeader.AllowScreenContentTools)
            {
                this.paletteContexts[tileIndex] = new Av1NeighborArrayUnit<Av1EncoderPaletteInfo>(
                    paletteStorage.Slice(tileIndex * paletteContextLength, paletteContextLength),
                    paletteLeftLength,
                    paletteTopLength)
                {
                    GranularityNormalLog2 = Av1Constants.ModeInfoSizeLog2
                };
            }

            cdefPreset[tileIndex] = [-1, -1, -1, -1];
            previousQIndex[tileIndex] = frameHeader.QuantizationParameters.BaseQIndex;
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
            ModeInfoStride = this.modeInfo.ModeInfoStride,
            Disallow4x4AllFrames = this.modeInfo.Disallow4x4AllFrames,
            CdefPreset = cdefPreset
        };
    }

    /// <summary>
    /// Gets the non-owning picture state consumed by superblock analysis and tile writing.
    /// </summary>
    public Av1PictureControlSet Picture { get; }

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
