// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Retains partition candidates' mode decisions while coefficient scratch is reused.
/// </summary>
internal sealed class Av1EncoderPartitionTree : IDisposable
{
    private const int ContextCount = 29;
    private const int NodeHeaderLength = ContextCount * sizeof(int);
    private const int Alignment = sizeof(long);
    private static readonly int ModeHeaderLength =
        (Unsafe.SizeOf<ModeSnapshot>() + Alignment - 1) & -Alignment;

    private readonly MemoryAllocator allocator;
    private IMemoryOwner<byte>? owner;
    private Memory<byte> storage;
    private Av1BlockSize superblockSize;
    private int width;
    private int height;
    private int planeCount;
    private bool subsamplingX;
    private bool subsamplingY;
    private bool allowPalette;
    private bool squareLeavesOnly;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1EncoderPartitionTree"/> class.
    /// </summary>
    /// <param name="allocator">The allocator for retained mode storage.</param>
    public Av1EncoderPartitionTree(MemoryAllocator allocator) => this.allocator = allocator;

    /// <summary>
    /// Starts a new superblock search with the specified coded extent.
    /// </summary>
    /// <param name="sequence">The sequence geometry and color configuration.</param>
    /// <param name="width">The coded width remaining in this superblock.</param>
    /// <param name="height">The coded height remaining in this superblock.</param>
    /// <param name="allowPalette">Whether palette syntax is available.</param>
    /// <param name="squareLeavesOnly">Whether only unsplit square decisions are retained for partition merging.</param>
    public void Reset(ObuSequenceHeader sequence, int width, int height, bool allowPalette, bool squareLeavesOnly)
    {
        this.superblockSize = sequence.SuperblockSize;
        this.width = width;
        this.height = height;
        this.planeCount = sequence.ColorConfig.IsMonochrome ? 1 : 3;
        this.subsamplingX = sequence.ColorConfig.SubSamplingX;
        this.subsamplingY = sequence.ColorConfig.SubSamplingY;
        this.allowPalette = allowPalette;
        this.squareLeavesOnly = squareLeavesOnly;
        int nodeCount = this.superblockSize == Av1BlockSize.Block128x128 ? 341 : 85;
        int firstNodeOffset = ((nodeCount * sizeof(int)) + Alignment - 1) & -Alignment;

        // Lay out only geometrically legal candidates that touch the coded frame. One pooled allocation
        // holds their mode headers, transform states and palette maps; coefficients remain worker scratch.
        int requiredLength = this.LayoutNode(default, false, 0, 0, 0, this.superblockSize, firstNodeOffset);

        // Compare with the capacity of the owner, not with the previous layout. A clipped superblock at a
        // frame edge needs less storage, and the next complete superblock must not allocate again.
        if (this.owner is null || this.owner.Memory.Length < requiredLength)
        {
            this.owner?.Dispose();
            this.owner = this.allocator.Allocate<byte>(requiredLength);
        }

        this.storage = this.owner.Memory[..requiredLength];

        Span<byte> storage = this.storage.Span;
        MemoryMarshal.Cast<byte, int>(storage[..(nodeCount * sizeof(int))]).Fill(-1);
        _ = this.LayoutNode(storage, true, 0, 0, 0, this.superblockSize, firstNodeOffset);
    }

    /// <summary>
    /// Gets the retained decision for one leaf of a partition candidate.
    /// </summary>
    /// <param name="nodeIndex">The parent quadtree index.</param>
    /// <param name="partition">The candidate partition.</param>
    /// <param name="leafIndex">The leaf index within that candidate.</param>
    /// <returns>The candidate's mode, transform and palette storage.</returns>
    public ModeContext GetContext(int nodeIndex, Av1PartitionType partition, int leafIndex)
    {
        Span<byte> storage = this.storage.Span;
        int nodeOffset = MemoryMarshal.Cast<byte, int>(storage)[nodeIndex];
        int contextIndex = GetFirstContextIndex(partition) + leafIndex;
        int contextOffset = MemoryMarshal.Cast<byte, int>(storage.Slice(nodeOffset, NodeHeaderLength))[contextIndex];
        return new ModeContext(storage[contextOffset..], this.planeCount);
    }

    /// <inheritdoc/>
    public void Dispose() => this.owner?.Dispose();

    private static int GetFirstContextIndex(Av1PartitionType partition)
        => partition switch
        {
            Av1PartitionType.None => 0,
            Av1PartitionType.Horizontal => 1,
            Av1PartitionType.Vertical => 3,
            Av1PartitionType.Split => 5,
            Av1PartitionType.HorizontalA => 9,
            Av1PartitionType.HorizontalB => 12,
            Av1PartitionType.VerticalA => 15,
            Av1PartitionType.VerticalB => 18,
            Av1PartitionType.Horizontal4 => 21,
            _ => 25
        };

    private int LayoutNode(
        Span<byte> storage,
        bool initialize,
        int nodeIndex,
        int x,
        int y,
        Av1BlockSize blockSize,
        int offset)
    {
        int nodeOffset = offset;
        offset += NodeHeaderLength;
        if (initialize)
        {
            MemoryMarshal.Cast<byte, int>(storage)[nodeIndex] = nodeOffset;
            MemoryMarshal.Cast<byte, int>(storage.Slice(nodeOffset, NodeHeaderLength)).Fill(-1);
        }

        int side = blockSize.GetWidth();
        int half = side / 2;
        bool hasRows = y + half < this.height;
        bool hasColumns = x + half < this.width;
        for (Av1PartitionType partition = Av1PartitionType.None; partition <= Av1PartitionType.Vertical4; partition++)
        {
            if ((this.squareLeavesOnly && partition != Av1PartitionType.None) ||
                (partition == Av1PartitionType.Split && blockSize != Av1BlockSize.Block8x8))
            {
                continue;
            }

            if ((partition == Av1PartitionType.None && (!hasRows || !hasColumns) &&
                    !(this.squareLeavesOnly && blockSize == Av1BlockSize.Block8x8)) ||
                (partition == Av1PartitionType.Horizontal && !hasColumns) ||
                (partition == Av1PartitionType.Vertical && !hasRows) ||
                (partition >= Av1PartitionType.HorizontalA && (!hasRows || !hasColumns)) ||
                (blockSize == Av1BlockSize.Block128x128 &&
                    partition is Av1PartitionType.Horizontal4 or Av1PartitionType.Vertical4))
            {
                continue;
            }

            Av1BlockSize subSize = partition.GetBlockSubSize(blockSize);
            if (subSize == Av1BlockSize.Invalid)
            {
                continue;
            }

            int count = partition switch
            {
                Av1PartitionType.None => 1,
                Av1PartitionType.Horizontal or Av1PartitionType.Vertical => 2,
                >= Av1PartitionType.HorizontalA and <= Av1PartitionType.VerticalB => 3,
                _ => 4
            };

            for (int leaf = 0; leaf < count; leaf++)
            {
                Av1BlockSize leafSize = subSize;
                if ((partition == Av1PartitionType.HorizontalA && leaf < 2) ||
                    (partition == Av1PartitionType.HorizontalB && leaf > 0) ||
                    (partition == Av1PartitionType.VerticalA && leaf < 2) ||
                    (partition == Av1PartitionType.VerticalB && leaf > 0))
                {
                    leafSize = Av1PartitionType.Split.GetBlockSubSize(blockSize);
                }

                if (this.planeCount > 1 && leafSize.GetSubsampled(this.subsamplingX, this.subsamplingY) == Av1BlockSize.Invalid)
                {
                    continue;
                }

                int sampleCount = leafSize.GetWidth() * leafSize.GetHeight();
                int stateBytes = this.planeCount * (sampleCount / Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount) *
                    Unsafe.SizeOf<Av1EncoderTransformBlockState>();

                int paletteBytes = this.allowPalette && Av1TileWriter.IsPaletteAllowed(true, leafSize) ? 2 * sampleCount : 0;
                offset = (offset + Alignment - 1) & -Alignment;
                if (initialize)
                {
                    int contextIndex = GetFirstContextIndex(partition) + leaf;
                    MemoryMarshal.Cast<byte, int>(storage.Slice(nodeOffset, NodeHeaderLength))[contextIndex] = offset;
                    ref ModeSnapshot snapshot = ref MemoryMarshal.AsRef<ModeSnapshot>(storage.Slice(offset, ModeHeaderLength));
                    snapshot.Ready = false;
                    snapshot.ModeInfo.Block.BlockSize = leafSize;
                }

                offset += ModeHeaderLength + stateBytes + paletteBytes;
            }
        }

        if (blockSize > Av1BlockSize.Block8x8)
        {
            Av1BlockSize childSize = Av1PartitionType.Split.GetBlockSubSize(blockSize);
            for (int child = 0; child < 4; child++)
            {
                int childX = x + ((child & 1) * half);
                int childY = y + ((child >> 1) * half);
                if (childX < this.width && childY < this.height)
                {
                    offset = this.LayoutNode(storage, initialize, (nodeIndex * 4) + child + 1, childX, childY, childSize, offset);
                }
            }
        }

        return offset;
    }

    /// <summary>
    /// Holds a selected mode's scalar syntax and measured cost.
    /// </summary>
    internal struct ModeSnapshot
    {
        public Av1MacroBlockModeInfo ModeInfo;
        public Av1EncoderBlockStruct Block;
        public Av1EncoderPaletteInfo Palette;
        public Av1RateDistortionStatistics Statistics;
        public Av1MotionVector Displacement;
        public Av1MotionVector SecondaryDisplacement;
        public bool Ready;
    }

    /// <summary>
    /// Provides the stored syntax, transform states and palette indices for one candidate.
    /// </summary>
    internal readonly ref struct ModeContext
    {
        private readonly Span<byte> storage;
        private readonly int planeCount;

        public ModeContext(Span<byte> storage, int planeCount)
        {
            this.storage = storage;
            this.planeCount = planeCount;
        }

        public ref ModeSnapshot Snapshot => ref MemoryMarshal.AsRef<ModeSnapshot>(this.storage[..ModeHeaderLength]);

        public void CopyFrom(ModeContext source, Av1PartitionType partition)
        {
            this.Snapshot = source.Snapshot;
            this.Snapshot.ModeInfo.Block.PartitionType = partition;
            for (int plane = 0; plane < this.planeCount; plane++)
            {
                source.GetTransformStates((Av1Plane)plane).CopyTo(this.GetTransformStates((Av1Plane)plane));
            }
        }

        public Span<Av1EncoderTransformBlockState> GetTransformStates(Av1Plane plane)
        {
            Av1BlockSize blockSize = this.Snapshot.ModeInfo.Block.BlockSize;
            int count = blockSize.GetWidth() * blockSize.GetHeight() / Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount;
            int planeBytes = count * Unsafe.SizeOf<Av1EncoderTransformBlockState>();
            return MemoryMarshal.Cast<byte, Av1EncoderTransformBlockState>(
                this.storage.Slice(ModeHeaderLength + ((int)plane * planeBytes), planeBytes));
        }

        public Span<byte> GetPaletteIndices(Av1PlaneType plane)
        {
            Av1BlockSize blockSize = this.Snapshot.ModeInfo.Block.BlockSize;
            int count = blockSize.GetWidth() * blockSize.GetHeight();
            int stateBytes = this.planeCount * (count / Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount) *
                Unsafe.SizeOf<Av1EncoderTransformBlockState>();

            return this.storage.Slice(ModeHeaderLength + stateBytes + ((int)plane * count), count);
        }
    }
}
