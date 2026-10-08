// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Owns the allocation-index grid and mode-information values for one encoded AV1 frame.
/// </summary>
internal sealed class Av1EncoderModeInfoBuffer : IDisposable
{
    private const int CodedDimensionAlignmentLog2 = 3;
    private const int ModeInfoAlignmentLog2 = Av1Constants.MaxSuperBlockSizeLog2 - Av1Constants.ModeInfoSizeLog2;
    private IMemoryOwner<byte>? owner;
    private readonly ByteMemoryManager<int> grid;
    private readonly ByteMemoryManager<Av1MacroBlockModeInfo> allocation;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1EncoderModeInfoBuffer"/> class.
    /// </summary>
    /// <param name="configuration">The configuration providing the memory allocator.</param>
    /// <param name="frameWidth">The visible frame width in luma samples.</param>
    /// <param name="frameHeight">The visible frame height in luma samples.</param>
    /// <param name="disallow4x4AllFrames">Whether each allocated mode-information value represents an 8x8 region.</param>
    public Av1EncoderModeInfoBuffer(
        Configuration configuration,
        int frameWidth,
        int frameHeight,
        bool disallow4x4AllFrames)
    {
        this.ModeInfoColumnCount = Av1Math.AlignPowerOf2(frameWidth, CodedDimensionAlignmentLog2) >> Av1Constants.ModeInfoSizeLog2;
        this.ModeInfoRowCount = Av1Math.AlignPowerOf2(frameHeight, CodedDimensionAlignmentLog2) >> Av1Constants.ModeInfoSizeLog2;
        this.ModeInfoStride = Av1Math.AlignPowerOf2(this.ModeInfoColumnCount, ModeInfoAlignmentLog2);
        int alignedModeInfoRowCount = Av1Math.AlignPowerOf2(this.ModeInfoRowCount, ModeInfoAlignmentLog2);
        int allocationShift = disallow4x4AllFrames ? 1 : 0;
        int gridLength = checked(this.ModeInfoStride * alignedModeInfoRowCount);
        int allocationLength = checked((this.ModeInfoStride >> allocationShift) * (alignedModeInfoRowCount >> allocationShift));
        int gridByteLength = checked(gridLength * sizeof(int));
        int allocationByteLength = checked(allocationLength * Unsafe.SizeOf<Av1MacroBlockModeInfo>());
        int storageLength = checked(gridByteLength + allocationByteLength);

        // The pointer grid and value allocation share one frame lifetime. Packing both regions into one clean
        // owner retains libaom's independent typed layouts without its separate allocation and cleanup paths.
        this.owner = configuration.MemoryAllocator.Allocate<byte>(storageLength, AllocationOptions.Clean);
        Memory<byte> storage = this.owner.Memory[..storageLength];
        this.grid = new ByteMemoryManager<int>(storage[..gridByteLength]);
        this.allocation = new ByteMemoryManager<Av1MacroBlockModeInfo>(storage.Slice(gridByteLength, allocationByteLength));
        this.Disallow4x4AllFrames = disallow4x4AllFrames;
    }

    /// <summary>
    /// Gets the visible frame height in 4x4 mode-information units.
    /// </summary>
    public int ModeInfoRowCount { get; }

    /// <summary>
    /// Gets the visible frame width in 4x4 mode-information units.
    /// </summary>
    public int ModeInfoColumnCount { get; }

    /// <summary>
    /// Gets the aligned row stride of the allocation-index grid in 4x4 mode-information units.
    /// </summary>
    public int ModeInfoStride { get; }

    /// <summary>
    /// Gets a value indicating whether each allocated mode-information value represents an 8x8 region.
    /// </summary>
    public bool Disallow4x4AllFrames { get; }

    /// <summary>
    /// Gets the frame grid that maps each 4x4 position to its mode-information allocation index.
    /// </summary>
    public Memory<int> Grid => this.grid.Memory;

    /// <summary>
    /// Gets the contiguous mode-information values addressed by <see cref="Grid"/>.
    /// </summary>
    public Memory<Av1MacroBlockModeInfo> Allocation => this.allocation.Memory;

    /// <summary>
    /// Returns the packed frame storage to the configured memory allocator.
    /// </summary>
    public void Dispose()
    {
        this.owner?.Dispose();
        this.owner = null;
    }
}
