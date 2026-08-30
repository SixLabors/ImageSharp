// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

/// <summary>
/// Owns the padded luma and chroma sample planes for one decoded AV1 frame.
/// </summary>
/// <typeparam name="T">The unmanaged storage-element type used by the plane allocations.</typeparam>
internal class Av1FrameBuffer<T> : IDisposable
    where T : unmanaged
{
    /// <summary>
    /// The number of luma border samples reserved for prediction and in-loop filtering.
    /// </summary>
    // Scaled prediction may start 284 luma samples outside a retained frame and then consume three preceding filter
    // taps. The normative 288-sample border keeps that entire source window directly addressable without block copies.
    public const int DecoderPaddingValue = 288;

    /// <summary>
    /// The allocation-mask bit for the luma plane.
    /// </summary>
    private const int PictureBufferYFlag = 1 << 0;

    /// <summary>
    /// The allocation-mask bit for the first chroma plane.
    /// </summary>
    private const int PictureBufferCbFlag = 1 << 1;

    /// <summary>
    /// The allocation-mask bit for the second chroma plane.
    /// </summary>
    private const int PictureBufferCrFlag = 1 << 2;

    /// <summary>
    /// The allocation mask for a monochrome frame.
    /// </summary>
    private const int PictureBufferLumaMask = PictureBufferYFlag;

    /// <summary>
    /// The allocation mask for a frame containing all three planes.
    /// </summary>
    private const int PictureBufferFullMask = PictureBufferYFlag | PictureBufferCbFlag | PictureBufferCrFlag;

    /// <summary>
    /// The number of <typeparamref name="T"/> elements occupied by one logical sample.
    /// </summary>
    private readonly int storageElementsPerSample;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1FrameBuffer{T}"/> class.
    /// </summary>
    /// <param name="configuration">The configuration providing the plane allocator.</param>
    /// <param name="sequenceHeader">The sequence header defining maximum dimensions, bit depth, and chroma layout.</param>
    /// <param name="maxColorFormat">The maximum color format to allocate for a non-monochrome sequence.</param>
    /// <param name="is16BitPipeline">Indicates whether reconstruction uses native 16-bit sample storage.</param>
    /// <exception cref="InvalidImageContentException">The padded frame planes cannot be represented as contiguous allocations.</exception>
    public Av1FrameBuffer(Configuration configuration, ObuSequenceHeader sequenceHeader, Av1ColorFormat maxColorFormat, bool is16BitPipeline)
    {
        this.MemoryAllocator = configuration.MemoryAllocator;
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
        int bufferEnableMask = sequenceHeader.ColorConfig.IsMonochrome ? PictureBufferLumaMask : PictureBufferFullMask;

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

        long lumaElementCount = (long)strideY * this.storageElementsPerSample * heightY;
        long chromaElementCount = (long)strideChroma * this.storageElementsPerSample * heightChroma;
        bool planesExceedContiguousLimit =
            lumaElementCount >= int.MaxValue ||
            (bufferEnableMask == PictureBufferFullMask && chromaElementCount >= int.MaxValue);

        if (planesExceedContiguousLimit)
        {
            // The reconstruction operators use one span plus a constant stride to address padded neighbors. Reject an
            // external geometry that cannot satisfy that ownership contract before Allocate2D falls back to groups.
            throw new InvalidImageContentException("The AV1 frame dimensions exceed the contiguous decoder plane limit.");
        }

        this.BufferY = null;
        this.BufferCb = null;
        this.BufferCr = null;

        // Block reconstruction and the SIMD predictors address decoder padding through one span plus a constant row
        // stride. Establish that invariant at the plane owner instead of copying fragmented groups in every hot path.
        try
        {
            if ((bufferEnableMask & PictureBufferYFlag) != 0)
            {
                this.BufferY = configuration.MemoryAllocator.Allocate2D<T>(strideY * this.storageElementsPerSample, heightY, preferContiguosImageBuffers: true);
            }

            if ((bufferEnableMask & PictureBufferCbFlag) != 0)
            {
                this.BufferCb = configuration.MemoryAllocator.Allocate2D<T>(strideChroma * this.storageElementsPerSample, heightChroma, preferContiguosImageBuffers: true);
            }

            if ((bufferEnableMask & PictureBufferCrFlag) != 0)
            {
                this.BufferCr = configuration.MemoryAllocator.Allocate2D<T>(strideChroma * this.storageElementsPerSample, heightChroma, preferContiguosImageBuffers: true);
            }
        }
        catch
        {
            // Construction publishes the owner only after every required plane has been rented. Release earlier planes
            // here because a later allocation failure leaves no constructed frame buffer for the caller to dispose.
            this.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Gets the padded luma-coordinate origin of the visible frame.
    /// </summary>
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

    /// <summary>
    /// Gets or sets the horizontal padding distance.
    /// </summary>
    public int OriginX { get; set; }

    /// <summary>
    /// Gets or sets the vertical padding distance.
    /// </summary>
    public int OriginY { get; set; }

    /// <summary>
    /// Gets or sets the luma picture width, excluding padding.
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Gets or sets the luma picture height, excluding padding.
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// Gets or sets the maximum luma picture width.
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
    /// Gets or sets the luma and chroma plane sampling layout.
    /// </summary>
    public Av1ColorFormat ColorFormat { get; set; }

    /// <summary>
    /// Gets or sets the maximum luma picture height.
    /// </summary>
    public int MaxHeight { get; set; }

    /// <summary>
    /// Gets a value indicating whether reconstruction uses native 16-bit samples.
    /// </summary>
    public bool Is16BitPipeline { get; }

    /// <summary>
    /// Gets the allocator used for frame-owned and frame-scoped working buffers.
    /// </summary>
    public MemoryAllocator MemoryAllocator { get; }

    /// <summary>
    /// Copies the complete padded sample planes and active picture geometry to another compatible frame buffer.
    /// </summary>
    /// <param name="destination">The frame buffer receiving the copied reconstruction.</param>
    public void CopyTo(Av1FrameBuffer<T> destination)
    {
        // Copy each contiguous allocation so the runtime can use its optimized bulk-memory path. Film-grain
        // presentation consumes right and bottom padding for odd dimensions, so copying only visible rows would leave
        // part of the independently owned presentation surface undefined.
        this.BufferY!.DangerousGetSingleSpan().CopyTo(destination.BufferY!.DangerousGetSingleSpan());
        Buffer2D<T>? chromaBlue = this.BufferCb;
        if (chromaBlue is not null)
        {
            chromaBlue.DangerousGetSingleSpan().CopyTo(destination.BufferCb!.DangerousGetSingleSpan());
            this.BufferCr!.DangerousGetSingleSpan().CopyTo(destination.BufferCr!.DangerousGetSingleSpan());
        }

        destination.StartPosition = this.StartPosition;
        destination.OriginX = this.OriginX;
        destination.OriginY = this.OriginY;
        destination.Width = this.Width;
        destination.Height = this.Height;
        destination.MaxWidth = this.MaxWidth;
        destination.MaxHeight = this.MaxHeight;
        destination.BitDepth = this.BitDepth;
        destination.ColorFormat = this.ColorFormat;
    }

    /// <summary>
    /// Releases the owned luma and chroma plane allocations.
    /// </summary>
    public void Dispose()
    {
        this.BufferY?.Dispose();
        this.BufferY = null;
        this.BufferCb?.Dispose();
        this.BufferCb = null;
        this.BufferCr?.Dispose();
        this.BufferCr = null;
    }

    /// <summary>
    /// Gets a storage-element span beginning one logical row before a block.
    /// </summary>
    /// <param name="plane">The luma or chroma plane.</param>
    /// <param name="locationInPixels">The block origin in plane samples.</param>
    /// <param name="subX">The horizontal chroma subsampling shift.</param>
    /// <param name="subY">The vertical chroma subsampling shift.</param>
    /// <param name="stride">Receives the logical samples between adjacent rows.</param>
    /// <returns>The span beginning one logical row before the block.</returns>
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

        // Intra prediction addresses above neighbors relative to the destination span, so index zero is the previous row.
        blockOffset -= elementStride;
        Guard.MustBeGreaterThanOrEqualTo(blockOffset, 0, nameof(blockOffset));

        return buffer.DangerousGetSingleSpan()[blockOffset..];
    }

    /// <summary>
    /// Gets a native 16-bit sample span beginning one logical row before a block.
    /// </summary>
    /// <param name="plane">The luma or chroma plane.</param>
    /// <param name="locationInPixels">The block origin in plane samples.</param>
    /// <param name="subX">The horizontal chroma subsampling shift.</param>
    /// <param name="subY">The vertical chroma subsampling shift.</param>
    /// <param name="stride">Receives the logical samples between adjacent rows.</param>
    /// <returns>The 16-bit span beginning one logical row before the block.</returns>
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
    /// Gets the visible sample region for one plane.
    /// </summary>
    /// <param name="plane">The luma or chroma plane.</param>
    /// <param name="subX">The horizontal chroma subsampling shift.</param>
    /// <param name="subY">The vertical chroma subsampling shift.</param>
    /// <returns>The plane region excluding decoder padding.</returns>
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
    /// Gets one visible row of native 16-bit samples from a plane.
    /// </summary>
    /// <param name="plane">The luma or chroma plane.</param>
    /// <param name="row">The zero-based visible row index.</param>
    /// <param name="subX">The horizontal chroma subsampling shift.</param>
    /// <param name="subY">The vertical chroma subsampling shift.</param>
    /// <returns>The visible row without decoder padding.</returns>
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

    /// <summary>
    /// Gets the complete padded storage allocation for one plane.
    /// </summary>
    /// <param name="plane">The luma or chroma plane.</param>
    /// <param name="subX">The horizontal chroma subsampling shift.</param>
    /// <param name="subY">The vertical chroma subsampling shift.</param>
    /// <param name="stride">Receives the number of logical samples between adjacent rows.</param>
    /// <param name="origin">Receives the visible plane origin within the padded allocation.</param>
    /// <returns>The complete plane allocation, including decoder padding.</returns>
    public Span<T> GetPaddedPlaneSpan(Av1Plane plane, int subX, int subY, out int stride, out Point origin)
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
        origin = new(originX, originY);
        return buffer.DangerousGetSingleSpan();
    }

    /// <summary>
    /// Gets the complete padded storage allocation for one native 16-bit plane.
    /// </summary>
    /// <param name="plane">The luma or chroma plane.</param>
    /// <param name="subX">The horizontal chroma subsampling shift.</param>
    /// <param name="subY">The vertical chroma subsampling shift.</param>
    /// <param name="stride">Receives the number of logical samples between adjacent rows.</param>
    /// <param name="origin">Receives the visible plane origin within the padded allocation.</param>
    /// <returns>The complete plane allocation, including decoder padding.</returns>
    public Span<ushort> GetPaddedPlaneSpan16(Av1Plane plane, int subX, int subY, out int stride, out Point origin)
        => MemoryMarshal.Cast<T, ushort>(this.GetPaddedPlaneSpan(plane, subX, subY, out stride, out origin));

    /// <summary>
    /// Resolves a plane allocation and its visible padded layout.
    /// </summary>
    /// <param name="plane">The luma or chroma plane.</param>
    /// <param name="subX">The horizontal chroma subsampling shift.</param>
    /// <param name="subY">The vertical chroma subsampling shift.</param>
    /// <param name="buffer">Receives the selected plane allocation.</param>
    /// <param name="originX">Receives the horizontal visible origin in plane samples.</param>
    /// <param name="originY">Receives the vertical visible origin in plane samples.</param>
    /// <param name="width">Receives the visible plane width.</param>
    /// <param name="height">Receives the visible plane height.</param>
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
