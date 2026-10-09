// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Owns the raster-ordered transform coefficients retained for every superblock in one encoded frame.
/// </summary>
internal sealed class Av1EncoderCoefficientBuffer : IDisposable
{
    /// <summary>
    /// The number of coefficients in one 4x4 unit. Each 4x4 unit has one transform block state entry.
    /// </summary>
    public const int TransformBlockUnitCoefficientCount = 1 << (Av1Constants.ModeInfoSizeLog2 * 2);

    /// <summary>
    /// The number of coefficient positions one packed transform-block state occupies.
    /// </summary>
    private static readonly int StorageElementsPerTransformBlock =
        Unsafe.SizeOf<Av1EncoderTransformBlockState>() / sizeof(int);

    /// <summary>
    /// Stores the coefficients and the packed transform block states of one superblock in each row.
    /// </summary>
    private readonly Buffer2D<int> storage;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1EncoderCoefficientBuffer"/> class.
    /// </summary>
    /// <param name="configuration">The configuration providing the frame allocator.</param>
    /// <param name="sequenceHeader">The sequence header defining superblock and chroma geometry.</param>
    /// <param name="width">The coded luma width.</param>
    /// <param name="height">The coded luma height.</param>
    public Av1EncoderCoefficientBuffer(
        Configuration configuration,
        ObuSequenceHeader sequenceHeader,
        int width,
        int height)
    {
        int superblockSizeLog2 = sequenceHeader.SuperblockSizeLog2;
        int superblockSize = 1 << superblockSizeLog2;
        this.SuperblockColumnCount = Av1Math.DivideLog2Ceiling(width, superblockSizeLog2);
        this.SuperblockRowCount = Av1Math.DivideLog2Ceiling(height, superblockSizeLog2);
        this.SuperblockCount = this.SuperblockColumnCount * this.SuperblockRowCount;
        this.LumaCoefficientCount = superblockSize * superblockSize;

        ObuColorConfig colorConfig = sequenceHeader.ColorConfig;
        int chromaSubsampling = (colorConfig.SubSamplingX ? 1 : 0) + (colorConfig.SubSamplingY ? 1 : 0);
        this.ChromaCoefficientCount = colorConfig.IsMonochrome ? 0 : this.LumaCoefficientCount >> chromaSubsampling;
        this.CoefficientsPerSuperblock = this.LumaCoefficientCount + (2 * this.ChromaCoefficientCount);
        this.LumaTransformBlockCount = this.LumaCoefficientCount / TransformBlockUnitCoefficientCount;
        this.ChromaTransformBlockCount = this.ChromaCoefficientCount / TransformBlockUnitCoefficientCount;
        this.TransformBlocksPerSuperblock = this.LumaTransformBlockCount + (2 * this.ChromaTransformBlockCount);

        // One state occupies more than a single coefficient position, so the packed region reserves as many
        // positions as the state needs.
        int storageElementsPerSuperblock = this.CoefficientsPerSuperblock +
            (this.TransformBlocksPerSuperblock * StorageElementsPerTransformBlock);

        // Each row holds the final coefficients of one superblock, in raster order. The end-of-block and transform type states share
        // that row, so the frame needs no separate allocations for them. The clean allocation makes a transform block that the search
        // never reached read as DCT_DCT with no coefficients, which is the zero value of a transform type map.
        this.storage = configuration.MemoryAllocator.Allocate2D<int>(
            storageElementsPerSuperblock,
            this.SuperblockCount,
            AllocationOptions.Clean);
    }

    /// <summary>
    /// Gets the number of superblock columns covering the coded frame.
    /// </summary>
    public int SuperblockColumnCount { get; }

    /// <summary>
    /// Gets the number of superblock rows covering the coded frame.
    /// </summary>
    public int SuperblockRowCount { get; }

    /// <summary>
    /// Gets the number of superblocks covering the coded frame.
    /// </summary>
    public int SuperblockCount { get; }

    /// <summary>
    /// Gets the number of luma coefficient positions reserved for each superblock.
    /// </summary>
    public int LumaCoefficientCount { get; }

    /// <summary>
    /// Gets the number of coefficient positions reserved for each chroma plane in each superblock.
    /// </summary>
    public int ChromaCoefficientCount { get; }

    /// <summary>
    /// Gets the number of coefficient positions reserved for each complete superblock.
    /// </summary>
    public int CoefficientsPerSuperblock { get; }

    /// <summary>
    /// Gets the number of luma transform-block positions reserved for each superblock.
    /// </summary>
    public int LumaTransformBlockCount { get; }

    /// <summary>
    /// Gets the number of transform-block positions reserved for each chroma plane in each superblock.
    /// </summary>
    public int ChromaTransformBlockCount { get; }

    /// <summary>
    /// Gets the number of transform-block positions reserved for each complete superblock.
    /// </summary>
    public int TransformBlocksPerSuperblock { get; }

    /// <summary>
    /// Gets the total number of coefficient positions retained for the frame.
    /// </summary>
    public long TotalCoefficientCount => (long)this.CoefficientsPerSuperblock * this.SuperblockCount;

    /// <summary>
    /// Gets the coefficient storage of a raster-ordered superblock. A caller reads it once per superblock and slices
    /// the planes from it with <see cref="GetPlaneSpan(Span{int}, Av1Plane)"/> and
    /// <see cref="GetTransformBlockSpan(Span{int}, Av1Plane)"/>.
    /// </summary>
    /// <param name="superblockIndex">The raster-ordered superblock index.</param>
    /// <returns>The coefficients of every plane, followed by the transform block states of every plane.</returns>
    public Span<int> GetSuperblockSpan(int superblockIndex) => this.storage.DangerousGetRowSpan(superblockIndex);

    /// <summary>
    /// Gets one component plane's coefficient span for a raster-ordered superblock.
    /// </summary>
    /// <param name="superblockIndex">The raster-ordered superblock index.</param>
    /// <param name="plane">The requested component plane.</param>
    /// <returns>The complete coefficient span reserved for that plane and superblock.</returns>
    public Span<int> GetPlaneSpan(int superblockIndex, Av1Plane plane)
        => this.GetPlaneSpan(this.storage.DangerousGetRowSpan(superblockIndex), plane);

    /// <summary>
    /// Gets one component plane's coefficient span from the coefficient storage of a superblock.
    /// </summary>
    /// <param name="superblock">The coefficient storage of the superblock, from <see cref="GetSuperblockSpan(int)"/>.</param>
    /// <param name="plane">The requested component plane.</param>
    /// <returns>The complete coefficient span reserved for that plane and superblock.</returns>
    public Span<int> GetPlaneSpan(Span<int> superblock, Av1Plane plane)
        => plane switch
        {
            Av1Plane.Y => superblock[..this.LumaCoefficientCount],
            Av1Plane.U => superblock.Slice(this.LumaCoefficientCount, this.ChromaCoefficientCount),
            _ => superblock.Slice(this.LumaCoefficientCount + this.ChromaCoefficientCount, this.ChromaCoefficientCount)
        };

    /// <summary>
    /// Gets one component plane's transform-block state for a raster-ordered superblock.
    /// </summary>
    /// <param name="superblockIndex">The raster-ordered superblock index.</param>
    /// <param name="plane">The requested component plane.</param>
    /// <returns>One state entry for every 4x4 coefficient unit in the plane.</returns>
    public Span<Av1EncoderTransformBlockState> GetTransformBlockSpan(int superblockIndex, Av1Plane plane)
        => this.GetTransformBlockSpan(this.storage.DangerousGetRowSpan(superblockIndex), plane);

    /// <summary>
    /// Gets one component plane's transform-block state from the coefficient storage of a superblock.
    /// </summary>
    /// <param name="superblock">The coefficient storage of the superblock, from <see cref="GetSuperblockSpan(int)"/>.</param>
    /// <param name="plane">The requested component plane.</param>
    /// <returns>One state entry for every 4x4 coefficient unit in the plane.</returns>
    public Span<Av1EncoderTransformBlockState> GetTransformBlockSpan(Span<int> superblock, Av1Plane plane)
    {
        // The transform block states follow the coefficients of all three planes in the same storage row.
        Span<Av1EncoderTransformBlockState> transformBlocks =
            MemoryMarshal.Cast<int, Av1EncoderTransformBlockState>(superblock[this.CoefficientsPerSuperblock..]);

        return plane switch
        {
            Av1Plane.Y => transformBlocks[..this.LumaTransformBlockCount],
            Av1Plane.U => transformBlocks.Slice(this.LumaTransformBlockCount, this.ChromaTransformBlockCount),
            _ => transformBlocks.Slice(
                this.LumaTransformBlockCount + this.ChromaTransformBlockCount,
                this.ChromaTransformBlockCount)
        };
    }

    /// <summary>
    /// Releases the frame coefficient storage.
    /// </summary>
    public void Dispose() => this.storage.Dispose();
}
