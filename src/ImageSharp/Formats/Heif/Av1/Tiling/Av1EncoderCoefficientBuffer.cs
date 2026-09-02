// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Owns the raster-ordered transform coefficients retained for every superblock in one encoded frame.
/// </summary>
internal sealed class Av1EncoderCoefficientBuffer : IDisposable
{
    /// <summary>
    /// Stores one complete superblock's luma and chroma coefficients in each row.
    /// </summary>
    private readonly Buffer2D<int> coefficients;

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

        // libaom stores finalized coefficients by raster-ordered superblock. A two-dimensional owner preserves that
        // layout while allowing ImageSharp's allocator to segment the frame instead of demanding one giant rental.
        this.coefficients = configuration.MemoryAllocator.Allocate2D<int>(
            this.CoefficientsPerSuperblock,
            this.SuperblockCount);
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
    /// Gets the total number of coefficient positions retained for the frame.
    /// </summary>
    public long TotalCoefficientCount => (long)this.CoefficientsPerSuperblock * this.SuperblockCount;

    /// <summary>
    /// Gets one component plane's coefficient span for a raster-ordered superblock.
    /// </summary>
    /// <param name="superblockIndex">The raster-ordered superblock index.</param>
    /// <param name="plane">The requested component plane.</param>
    /// <returns>The complete coefficient span reserved for that plane and superblock.</returns>
    public Span<int> GetPlaneSpan(int superblockIndex, Av1Plane plane)
    {
        Span<int> superblock = this.coefficients.DangerousGetRowSpan(superblockIndex);
        return plane switch
        {
            Av1Plane.Y => superblock[..this.LumaCoefficientCount],
            Av1Plane.U => superblock.Slice(this.LumaCoefficientCount, this.ChromaCoefficientCount),
            _ => superblock.Slice(
                this.LumaCoefficientCount + this.ChromaCoefficientCount,
                this.ChromaCoefficientCount)
        };
    }

    /// <summary>
    /// Releases the frame coefficient storage.
    /// </summary>
    public void Dispose() => this.coefficients.Dispose();
}
