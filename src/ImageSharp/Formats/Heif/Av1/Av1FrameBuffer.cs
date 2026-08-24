// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

/// <summary>
/// Buffer for the pixels of a single frame.
/// </summary>
internal class Av1FrameBuffer<T> : IDisposable
    where T : unmanaged
{
    private const int DecoderPaddingValue = 72;
    private const int PictureBufferYFlag = 1 << 0;
    private const int PictureBufferCbFlag = 1 << 1;
    private const int PictureBufferCrFlag = 1 << 2;
    private const int PictureBufferLumaMask = PictureBufferYFlag;
    private const int PictureBufferFullMask = PictureBufferYFlag | PictureBufferCbFlag | PictureBufferCrFlag;
    private readonly int storageElementsPerSample;

    public Av1FrameBuffer(Configuration configuration, ObuSequenceHeader sequenceHeader, Av1ColorFormat maxColorFormat, bool is16BitPipeline)
    {
        Av1ColorFormat colorFormat = sequenceHeader.ColorConfig.IsMonochrome ? Av1ColorFormat.Yuv400 : maxColorFormat;
        this.MaxWidth = sequenceHeader.MaxFrameWidth;
        this.MaxHeight = sequenceHeader.MaxFrameHeight;
        this.BitDepth = sequenceHeader.ColorConfig.BitDepth;
        this.ColorConfig = sequenceHeader.ColorConfig;
        this.BytesPerSample = this.BitDepth > Av1BitDepth.EightBit || is16BitPipeline ? 2 : 1;
        this.storageElementsPerSample = Math.Max(
            (this.BytesPerSample + Unsafe.SizeOf<T>() - 1) / Unsafe.SizeOf<T>(),
            1);

        this.ColorFormat = colorFormat;
        this.Is16BitPipeline = is16BitPipeline;
        this.BufferEnableMask = sequenceHeader.ColorConfig.IsMonochrome ? PictureBufferLumaMask : PictureBufferFullMask;

        int leftPadding = DecoderPaddingValue;
        int rightPadding = DecoderPaddingValue;
        int topPadding = DecoderPaddingValue;
        int bottomPadding = DecoderPaddingValue;

        this.StartPosition = new Point(leftPadding, topPadding);

        this.Width = this.MaxWidth;
        this.Height = this.MaxHeight;
        int strideY = this.MaxWidth + leftPadding + rightPadding;
        int heightY = this.MaxHeight + topPadding + bottomPadding;
        this.OriginX = leftPadding;
        this.OriginY = topPadding;
        this.OriginOriginY = bottomPadding;
        int strideChroma = 0;
        int heightChroma = 0;
        switch (this.ColorFormat)
        {
            case Av1ColorFormat.Yuv420:
                strideChroma = (strideY + 1) >> 1;
                heightChroma = (heightY + 1) >> 1;
                break;
            case Av1ColorFormat.Yuv422:
                strideChroma = (strideY + 1) >> 1;
                heightChroma = heightY;
                break;
            case Av1ColorFormat.Yuv444:
                strideChroma = strideY;
                heightChroma = heightY;
                break;
        }

        this.PackedFlag = false;

        this.BufferY = null;
        this.BufferCb = null;
        this.BufferCr = null;
        if ((this.BufferEnableMask & PictureBufferYFlag) != 0)
        {
            this.BufferY = configuration.MemoryAllocator.Allocate2D<T>(strideY * this.storageElementsPerSample, heightY);
        }

        if ((this.BufferEnableMask & PictureBufferCbFlag) != 0)
        {
            this.BufferCb = configuration.MemoryAllocator.Allocate2D<T>(strideChroma * this.storageElementsPerSample, heightChroma);
        }

        if ((this.BufferEnableMask & PictureBufferCrFlag) != 0)
        {
            this.BufferCr = configuration.MemoryAllocator.Allocate2D<T>(strideChroma * this.storageElementsPerSample, heightChroma);
        }

        this.BitIncrementY = null;
        this.BitIncrementCb = null;
        this.BitIncrementCr = null;
        this.BitIncrementY = null;
        this.BitIncrementCb = null;
        this.BitIncrementCr = null;
    }

    public Point StartPosition { get; private set; }

    /// <summary>
    /// Gets the Y luma buffer.
    /// </summary>
    public Buffer2D<T>? BufferY { get; private set; }

    /// <summary>
    /// Gets the U chroma buffer.
    /// </summary>
    public Buffer2D<T>? BufferCb { get; private set; }

    /// <summary>
    /// Gets the V chroma buffer.
    /// </summary>
    public Buffer2D<T>? BufferCr { get; private set; }

    public Buffer2D<byte>? BitIncrementY { get; private set; }

    public Buffer2D<byte>? BitIncrementCb { get; private set; }

    public Buffer2D<byte>? BitIncrementCr { get; private set; }

    /// <summary>
    /// Gets or sets the horizontal padding distance.
    /// </summary>
    public int OriginX { get; set; }

    /// <summary>
    /// Gets or sets the vertical padding distance.
    /// </summary>
    public int OriginY { get; set; }

    /// <summary>
    /// Gets or sets the vertical bottom padding distance
    /// </summary>
    public int OriginOriginY { get; set; }

    /// <summary>
    /// Gets or sets the Luma picture width, which excludes the padding.
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Gets or sets the Luma picture height, which excludes the padding.
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// Gets or sets the Lume picture width.
    /// </summary>
    public int MaxWidth { get; set; }

    /// <summary>
    /// Gets or sets the pixel bit depth.
    /// </summary>
    public Av1BitDepth BitDepth { get; set; }

    /// <summary>
    /// Gets the number of bytes used to store each reconstructed sample.
    /// </summary>
    public int BytesPerSample { get; }

    /// <summary>
    /// Gets the color configuration signaled by the AV1 sequence header.
    /// </summary>
    public ObuColorConfig ColorConfig { get; }

    /// <summary>
    /// Gets or sets the chroma subsampling.
    /// </summary>
    public Av1ColorFormat ColorFormat { get; set; }

    /// <summary>
    /// Gets or sets the Luma picture height.
    /// </summary>
    public int MaxHeight { get; set; }

    public int LumaSize { get; }

    public int ChromaSize { get; }

    /// <summary>
    /// Gets or sets a value indicating whether the bytes of the buffers are packed.
    /// </summary>
    public bool PackedFlag { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether film grain parameters are present for this frame.
    /// </summary>
    public bool FilmGrainFlag { get; set; }

    public int BufferEnableMask { get; set; }

    public bool Is16BitPipeline { get; set; }

    public void Dispose()
    {
        this.BufferY?.Dispose();
        this.BufferY = null;
        this.BufferCb?.Dispose();
        this.BufferCb = null;
        this.BufferCr?.Dispose();
        this.BufferCr = null;
        this.BitIncrementY?.Dispose();
        this.BitIncrementY = null;
        this.BitIncrementCb?.Dispose();
        this.BitIncrementCb = null;
        this.BitIncrementCr?.Dispose();
        this.BitIncrementCr = null;
    }

    /// <summary>
    /// Returns a <see cref="Span{T}"/> starting at 1 row before this blocks pixels.
    /// </summary>
    /// <remarks>
    /// SVT: svt_aom_derive_blk_pointers
    /// </remarks>
    public Span<T> DeriveBlockPointer(Av1Plane plane, Point locationInPixels, int subX, int subY, out int stride)
    {
        this.GetPlaneLayout(
            plane,
            subX,
            subY,
            out Buffer2D<T> buffer,
            out int originX,
            out int originY,
            out _,
            out _);

        int elementStride = buffer.Width;
        stride = elementStride / this.storageElementsPerSample;
        int blockOffset = (((originY + locationInPixels.Y) * stride) + originX + locationInPixels.X) *
            this.storageElementsPerSample;

        // Deviation from SVT, return PREVIOUS row in Block Reconstruction Buffer.
        blockOffset -= elementStride;
        Guard.MustBeGreaterThanOrEqualTo(blockOffset, 0, nameof(blockOffset));

        return buffer.DangerousGetSingleSpan()[blockOffset..];
    }

    /// <summary>
    /// Returns a 16-bit sample span starting one row before the specified block.
    /// </summary>
    /// <remarks>
    /// SVT: svt_aom_derive_blk_pointers
    /// </remarks>
    public Span<short> DeriveBlockPointer16(Av1Plane plane, Point locationInPixels, int subX, int subY, out int stride)
    {
        this.GetPlaneLayout(
            plane,
            subX,
            subY,
            out Buffer2D<T> buffer,
            out int originX,
            out int originY,
            out _,
            out _);

        stride = buffer.Width / this.storageElementsPerSample;
        int blockOffset = ((originY + locationInPixels.Y - 1) * stride) + originX + locationInPixels.X;
        Guard.MustBeGreaterThanOrEqualTo(blockOffset, 0, nameof(blockOffset));

        // High-bit-depth reconstruction uses native 16-bit samples in the byte-backed frame planes.
        return MemoryMarshal.Cast<T, short>(buffer.DangerousGetSingleSpan())[blockOffset..];
    }

    /// <summary>
    /// Returns a <see cref="Buffer2DRegion{T}"/> starting at top left pixel of the block of the specified plane.
    /// </summary>
    /// <remarks>
    /// SVT: svt_aom_derive_blk_pointers
    /// </remarks>
    public Buffer2DRegion<T> DeriveBlockPointer(Av1Plane plane, int subX, int subY)
    {
        this.GetPlaneLayout(
            plane,
            subX,
            subY,
            out Buffer2D<T> buffer,
            out int originX,
            out int originY,
            out int width,
            out int height);

        Rectangle region = new(
            originX * this.storageElementsPerSample,
            originY,
            width * this.storageElementsPerSample,
            height);

        return new Buffer2DRegion<T>(buffer, region);
    }

    /// <summary>
    /// Returns one logical row of 16-bit samples from the specified plane.
    /// </summary>
    public Span<ushort> GetHighBitDepthRowSpan(Av1Plane plane, int row, int subX, int subY)
    {
        this.GetPlaneLayout(
            plane,
            subX,
            subY,
            out Buffer2D<T> buffer,
            out int originX,
            out int originY,
            out int width,
            out _);

        Span<ushort> samples = MemoryMarshal.Cast<T, ushort>(buffer.DangerousGetRowSpan(originY + row));
        return samples.Slice(originX, width);
    }

    private void GetPlaneLayout(
        Av1Plane plane,
        int subX,
        int subY,
        out Buffer2D<T> buffer,
        out int originX,
        out int originY,
        out int width,
        out int height)
    {
        switch (plane)
        {
            case Av1Plane.Y:
                Guard.NotNull(this.BufferY);
                buffer = this.BufferY;
                originX = this.OriginX;
                originY = this.OriginY;
                width = this.Width;
                height = this.Height;
                break;
            case Av1Plane.U:
                Guard.NotNull(this.BufferCb);
                buffer = this.BufferCb;
                originX = this.OriginX >> subX;
                originY = this.OriginY >> subY;
                width = Av1Math.DivideLog2Ceiling(this.Width, subX);
                height = Av1Math.DivideLog2Ceiling(this.Height, subY);
                break;
            case Av1Plane.V:
            default:
                Guard.NotNull(this.BufferCr);
                buffer = this.BufferCr;
                originX = this.OriginX >> subX;
                originY = this.OriginY >> subY;
                width = Av1Math.DivideLog2Ceiling(this.Width, subX);
                height = Av1Math.DivideLog2Ceiling(this.Height, subY);
                break;
        }
    }
}
