// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

/// <summary>
/// Owns the native-precision luma and chroma sample planes for one reconstructed HEVC still picture.
/// </summary>
internal sealed class HevcPictureBuffer : IDisposable
{
    /// <summary>
    /// The horizontal chroma subsampling shift.
    /// </summary>
    private readonly int chromaSubsamplingX;

    /// <summary>
    /// The vertical chroma subsampling shift.
    /// </summary>
    private readonly int chromaSubsamplingY;

    /// <summary>
    /// Initializes a new instance of the <see cref="HevcPictureBuffer"/> class.
    /// </summary>
    /// <param name="configuration">The configuration providing the image memory allocator.</param>
    /// <param name="sequenceParameterSet">The coded dimensions, precision, and chroma layout.</param>
    public HevcPictureBuffer(Configuration configuration, HevcSequenceParameterSet sequenceParameterSet)
    {
        this.Width = sequenceParameterSet.Width;
        this.Height = sequenceParameterSet.Height;
        this.BitDepthLuma = sequenceParameterSet.BitDepthLuma;
        this.BitDepthChroma = sequenceParameterSet.BitDepthChroma;
        this.ChromaFormat = sequenceParameterSet.ChromaFormat;
        this.SeparateColorPlane = sequenceParameterSet.SeparateColorPlaneFlag;

        // Separate color planes are independently coded at full resolution even though chroma_format_idc is 4:4:4.
        this.chromaSubsamplingX = !this.SeparateColorPlane && this.ChromaFormat is 1 or 2 ? 1 : 0;
        this.chromaSubsamplingY = !this.SeparateColorPlane && this.ChromaFormat == 1 ? 1 : 0;
        this.Luma = configuration.MemoryAllocator.Allocate2D<ushort>(this.Width, this.Height);
        if (this.ChromaFormat != 0)
        {
            int chromaWidth = DivideCeilingByPowerOfTwo(this.Width, this.chromaSubsamplingX);
            int chromaHeight = DivideCeilingByPowerOfTwo(this.Height, this.chromaSubsamplingY);

            this.ChromaBlue = configuration.MemoryAllocator.Allocate2D<ushort>(chromaWidth, chromaHeight);
            this.ChromaRed = configuration.MemoryAllocator.Allocate2D<ushort>(chromaWidth, chromaHeight);
        }
    }

    /// <summary>
    /// Gets the coded luma width in samples.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Gets the coded luma height in samples.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Gets the luma sample precision in bits.
    /// </summary>
    public int BitDepthLuma { get; }

    /// <summary>
    /// Gets the chroma sample precision in bits.
    /// </summary>
    public int BitDepthChroma { get; }

    /// <summary>
    /// Gets the HEVC chroma-format identifier.
    /// </summary>
    public byte ChromaFormat { get; }

    /// <summary>
    /// Gets a value indicating whether the three planes are coded as independent full-resolution color planes.
    /// </summary>
    public bool SeparateColorPlane { get; }

    /// <summary>
    /// Gets the luma or first separate-color-plane allocation.
    /// </summary>
    public Buffer2D<ushort> Luma { get; }

    /// <summary>
    /// Gets the blue-difference chroma or second separate-color-plane allocation.
    /// </summary>
    public Buffer2D<ushort>? ChromaBlue { get; }

    /// <summary>
    /// Gets the red-difference chroma or third separate-color-plane allocation.
    /// </summary>
    public Buffer2D<ushort>? ChromaRed { get; }

    /// <summary>
    /// Gets the horizontal chroma subsampling shift for the selected plane.
    /// </summary>
    /// <param name="plane">The reconstruction plane.</param>
    /// <returns>Zero for luma and full-resolution planes; otherwise, the chroma shift.</returns>
    public int GetSubsamplingX(HevcPlane plane) => plane == HevcPlane.Y ? 0 : this.chromaSubsamplingX;

    /// <summary>
    /// Gets the vertical chroma subsampling shift for the selected plane.
    /// </summary>
    /// <param name="plane">The reconstruction plane.</param>
    /// <returns>Zero for luma and full-resolution planes; otherwise, the chroma shift.</returns>
    public int GetSubsamplingY(HevcPlane plane) => plane == HevcPlane.Y ? 0 : this.chromaSubsamplingY;

    /// <summary>
    /// Gets the selected plane width in samples.
    /// </summary>
    /// <param name="plane">The reconstruction plane.</param>
    /// <returns>The coded plane width.</returns>
    public int GetWidth(HevcPlane plane) => DivideCeilingByPowerOfTwo(this.Width, this.GetSubsamplingX(plane));

    /// <summary>
    /// Gets the selected plane height in samples.
    /// </summary>
    /// <param name="plane">The reconstruction plane.</param>
    /// <returns>The coded plane height.</returns>
    public int GetHeight(HevcPlane plane) => DivideCeilingByPowerOfTwo(this.Height, this.GetSubsamplingY(plane));

    /// <summary>
    /// Gets one coded row from the selected reconstruction plane.
    /// </summary>
    /// <param name="plane">The reconstruction plane.</param>
    /// <param name="row">The zero-based row index in plane samples.</param>
    /// <returns>The complete coded plane row.</returns>
    public Span<ushort> GetRowSpan(HevcPlane plane, int row)
        => plane switch
        {
            HevcPlane.Y => this.Luma.DangerousGetRowSpan(row),
            HevcPlane.Cb => this.ChromaBlue!.DangerousGetRowSpan(row),
            _ => this.ChromaRed!.DangerousGetRowSpan(row),
        };

    /// <summary>
    /// Releases the owned luma and chroma plane allocations.
    /// </summary>
    public void Dispose()
    {
        this.Luma.Dispose();
        this.ChromaBlue?.Dispose();
        this.ChromaRed?.Dispose();
    }

    /// <summary>
    /// Divides a nonnegative sample count by a power of two with upward rounding.
    /// </summary>
    /// <param name="value">The sample count.</param>
    /// <param name="shift">The base-two divisor logarithm.</param>
    /// <returns>The upward-rounded quotient.</returns>
    private static int DivideCeilingByPowerOfTwo(int value, int shift) => (value + (1 << shift) - 1) >> shift;
}
