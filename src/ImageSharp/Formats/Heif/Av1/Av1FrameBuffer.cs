// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

/// <summary>
/// Buffer for the pixels of a single frame.
/// </summary>
internal class Av1FrameBuffer<T> : IDisposable
    where T : struct
{
    private const int DecoderPaddingValue = 72;
    private const int PictureBufferYFlag = 1 << 0;
    private const int PictureBufferCbFlag = 1 << 1;
    private const int PictureBufferCrFlag = 1 << 2;
    private const int PictureBufferLumaMask = PictureBufferYFlag;
    private const int PictureBufferFullMask = PictureBufferYFlag | PictureBufferCbFlag | PictureBufferCrFlag;

    public Av1FrameBuffer(Configuration configuration, ObuSequenceHeader sequenceHeader, Av1ColorFormat maxColorFormat, bool is16BitPipeline)
    {
        Av1ColorFormat colorFormat = sequenceHeader.ColorConfig.IsMonochrome ? Av1ColorFormat.Yuv400 : maxColorFormat;
        this.MaxWidth = sequenceHeader.MaxFrameWidth;
        this.MaxHeight = sequenceHeader.MaxFrameHeight;
        this.BitDepth = sequenceHeader.ColorConfig.BitDepth;
        int bitsPerPixel = this.BitDepth > Av1BitDepth.EightBit || is16BitPipeline ? 2 : 1;
        this.ColorFormat = colorFormat;
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
                heightChroma = (this.Height + 1) >> 1;
                break;
            case Av1ColorFormat.Yuv422:
                strideChroma = (strideY + 1) >> 1;
                heightChroma = this.Height;
                break;
            case Av1ColorFormat.Yuv444:
                strideChroma = strideY;
                heightChroma = this.Height;
                break;
        }

        this.PackedFlag = false;

        this.BufferY = null;
        this.BufferCb = null;
        this.BufferCr = null;
        if ((this.BufferEnableMask & PictureBufferYFlag) != 0)
        {
            this.BufferY = configuration.MemoryAllocator.Allocate2D<T>(strideY * bitsPerPixel, heightY);
        }

        if ((this.BufferEnableMask & PictureBufferCbFlag) != 0)
        {
            this.BufferCb = configuration.MemoryAllocator.Allocate2D<T>(strideChroma * bitsPerPixel, heightChroma);
        }

        if ((this.BufferEnableMask & PictureBufferCrFlag) != 0)
        {
            this.BufferCr = configuration.MemoryAllocator.Allocate2D<T>(strideChroma * bitsPerPixel, heightChroma);
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
}
