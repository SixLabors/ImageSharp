// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
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
        : this(
            configuration,
            sequenceParameterSet.Width,
            sequenceParameterSet.Height,
            sequenceParameterSet.BitDepthLuma,
            sequenceParameterSet.BitDepthChroma,
            sequenceParameterSet.ChromaFormat,
            sequenceParameterSet.SeparateColorPlaneFlag,
            1 << sequenceParameterSet.MinCodingBlockLog2)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HevcPictureBuffer"/> class for encoder-owned component planes.
    /// </summary>
    /// <param name="configuration">The configuration providing the image memory allocator.</param>
    /// <param name="width">The coded luma width.</param>
    /// <param name="height">The coded luma height.</param>
    /// <param name="bitDepthLuma">The luma sample precision.</param>
    /// <param name="bitDepthChroma">The chroma sample precision.</param>
    /// <param name="chromaFormat">The HEVC chroma-format identifier.</param>
    /// <param name="separateColorPlane">Whether 4:4:4 components are coded as separate color planes.</param>
    /// <param name="storageAlignment">The luma sample alignment applied to the owned reconstruction planes.</param>
    public HevcPictureBuffer(
        Configuration configuration,
        int width,
        int height,
        int bitDepthLuma,
        int bitDepthChroma,
        byte chromaFormat,
        bool separateColorPlane,
        int storageAlignment = 1)
    {
        this.Width = width;
        this.Height = height;
        this.BitDepthLuma = bitDepthLuma;
        this.BitDepthChroma = bitDepthChroma;
        this.ChromaFormat = chromaFormat;
        this.SeparateColorPlane = separateColorPlane;

        // Separate color planes are independently coded at full resolution even though chroma_format_idc is 4:4:4.
        this.chromaSubsamplingX = !this.SeparateColorPlane && this.ChromaFormat is 1 or 2 ? 1 : 0;
        this.chromaSubsamplingY = !this.SeparateColorPlane && this.ChromaFormat == 1 ? 1 : 0;
        int storageWidth = DivideCeilingByPowerOfTwo(this.Width, BitOperations.Log2((uint)storageAlignment)) * storageAlignment;
        int storageHeight = DivideCeilingByPowerOfTwo(this.Height, BitOperations.Log2((uint)storageAlignment)) * storageAlignment;
        Buffer2D<ushort>? luma = null;
        Buffer2D<ushort>? chromaBlue = null;
        Buffer2D<ushort>? chromaRed = null;
        try
        {
            luma = configuration.MemoryAllocator.Allocate2D<ushort>(storageWidth, storageHeight);
            if (this.ChromaFormat != 0)
            {
                int chromaWidth = DivideCeilingByPowerOfTwo(storageWidth, this.chromaSubsamplingX);
                int chromaHeight = DivideCeilingByPowerOfTwo(storageHeight, this.chromaSubsamplingY);

                chromaBlue = configuration.MemoryAllocator.Allocate2D<ushort>(chromaWidth, chromaHeight);
                chromaRed = configuration.MemoryAllocator.Allocate2D<ushort>(chromaWidth, chromaHeight);
            }

            this.Luma = luma;
            this.ChromaBlue = chromaBlue;
            this.ChromaRed = chromaRed;
        }
        catch
        {
            // Construction transfers no plane ownership when a later rent fails, so unwind the unpublished owners
            // here instead of relying on Dispose being reachable through a fully constructed picture buffer.
            chromaRed?.Dispose();
            chromaBlue?.Dispose();
            luma?.Dispose();
            throw;
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
    /// Gets the sample precision for the selected reconstruction plane.
    /// </summary>
    /// <param name="plane">The reconstruction plane.</param>
    /// <returns>The plane sample precision in bits.</returns>
    public int GetBitDepth(HevcPlane plane) => plane == HevcPlane.Y ? this.BitDepthLuma : this.BitDepthChroma;

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
    /// Copies the complete coded component planes to another picture buffer with the same dimensions and chroma layout.
    /// </summary>
    /// <param name="destination">The destination picture buffer.</param>
    public void CopyTo(HevcPictureBuffer destination)
    {
        int planeCount = this.ChromaFormat == 0 ? 1 : 3;
        for (int planeIndex = 0; planeIndex < planeCount; planeIndex++)
        {
            HevcPlane plane = (HevcPlane)planeIndex;
            int width = this.GetWidth(plane);
            int height = this.GetHeight(plane);
            for (int row = 0; row < height; row++)
            {
                this.GetRowSpan(plane, row)[..width].CopyTo(destination.GetRowSpan(plane, row));
            }
        }
    }

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
