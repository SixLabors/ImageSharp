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
internal sealed class Av1FrameBuffer<T> : IDisposable
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
    /// The complete plane ownership state, or <see langword="null"/> after disposal.
    /// </summary>
    private FramePlanes? planes;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1FrameBuffer{T}"/> class at sequence-maximum dimensions.
    /// </summary>
    /// <param name="configuration">The configuration providing the plane allocator.</param>
    /// <param name="sequenceHeader">The sequence header defining maximum dimensions, bit depth, and chroma layout.</param>
    /// <param name="maxColorFormat">The maximum color format to allocate for a non-monochrome sequence.</param>
    /// <param name="is16BitPipeline">Indicates whether reconstruction uses native 16-bit sample storage.</param>
    /// <exception cref="InvalidImageContentException">The padded frame planes cannot be represented as contiguous allocations.</exception>
    public Av1FrameBuffer(Configuration configuration, ObuSequenceHeader sequenceHeader, Av1ColorFormat maxColorFormat, bool is16BitPipeline)
        : this(
            configuration,
            sequenceHeader,
            maxColorFormat,
            is16BitPipeline,
            sequenceHeader.MaxFrameWidth,
            sequenceHeader.MaxFrameHeight)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1FrameBuffer{T}"/> class for one active frame allocation.
    /// </summary>
    /// <param name="configuration">The configuration providing the plane allocator.</param>
    /// <param name="sequenceHeader">The sequence header defining bit depth and chroma layout.</param>
    /// <param name="maxColorFormat">The color format to allocate for a non-monochrome sequence.</param>
    /// <param name="is16BitPipeline">Indicates whether reconstruction uses native 16-bit sample storage.</param>
    /// <param name="allocationWidth">The padded plane's active luma width before decoder borders.</param>
    /// <param name="allocationHeight">The padded plane's active luma height before decoder borders.</param>
    /// <exception cref="InvalidImageContentException">The padded frame planes cannot be represented as contiguous allocations.</exception>
    public Av1FrameBuffer(
        Configuration configuration,
        ObuSequenceHeader sequenceHeader,
        Av1ColorFormat maxColorFormat,
        bool is16BitPipeline,
        int allocationWidth,
        int allocationHeight)
    {
        ValidateDimensions(sequenceHeader, maxColorFormat, is16BitPipeline);

        this.MemoryAllocator = configuration.MemoryAllocator;
        Av1ColorFormat colorFormat = sequenceHeader.ColorConfig.IsMonochrome ? Av1ColorFormat.Yuv400 : maxColorFormat;
        this.MaxWidth = allocationWidth;
        this.MaxHeight = allocationHeight;
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

        // Block reconstruction and the SIMD predictors address decoder padding through one span plus a constant row
        // stride. Establish that invariant at the plane owner instead of copying fragmented groups in every hot path.
        Buffer2D<T> luma = configuration.MemoryAllocator.Allocate2D<T>(
            strideY * this.storageElementsPerSample,
            heightY,
            preferContiguosImageBuffers: true);

        Buffer2D<T>? chromaBlue = null;
        Buffer2D<T>? chromaRed = null;
        try
        {
            ChromaPlanes? chroma = null;
            if (bufferEnableMask == PictureBufferFullMask)
            {
                chromaBlue = configuration.MemoryAllocator.Allocate2D<T>(
                    strideChroma * this.storageElementsPerSample,
                    heightChroma,
                    preferContiguosImageBuffers: true);

                chromaRed = configuration.MemoryAllocator.Allocate2D<T>(
                    strideChroma * this.storageElementsPerSample,
                    heightChroma,
                    preferContiguosImageBuffers: true);

                chroma = new ChromaPlanes(chromaBlue, chromaRed);
            }

            this.planes = new(luma, chroma);
        }
        catch
        {
            // Construction publishes the owner only after every required plane has been rented. Release earlier planes
            // here because a later allocation failure leaves no constructed frame buffer for the caller to dispose.
            chromaRed?.Dispose();
            chromaBlue?.Dispose();
            luma.Dispose();
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
    public Buffer2D<T>? BufferY => this.planes?.Luma;

    /// <summary>
    /// Gets the U chroma buffer.
    /// </summary>
    public Buffer2D<T>? BufferCb => this.planes?.Chroma?.Blue;

    /// <summary>
    /// Gets the V chroma buffer.
    /// </summary>
    public Buffer2D<T>? BufferCr => this.planes?.Chroma?.Red;

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
    /// Validates that the maximum sequence planes fit the decoder's contiguous ownership contract.
    /// </summary>
    /// <param name="sequenceHeader">The sequence header defining maximum dimensions, bit depth, and chroma layout.</param>
    /// <param name="maxColorFormat">The maximum color format required by the sequence.</param>
    /// <param name="is16BitPipeline">Indicates whether reconstruction uses native 16-bit sample storage.</param>
    public static void ValidateDimensions(
        ObuSequenceHeader sequenceHeader,
        Av1ColorFormat maxColorFormat,
        bool is16BitPipeline)
    {
        int bytesPerSample = sequenceHeader.ColorConfig.BitDepth > Av1BitDepth.EightBit || is16BitPipeline ? 2 : 1;
        int storageElementsPerSample = Math.Max(
            (bytesPerSample + Unsafe.SizeOf<T>() - 1) / Unsafe.SizeOf<T>(),
            1);

        long strideY = (long)sequenceHeader.MaxFrameWidth + (DecoderPaddingValue * 2L);
        long heightY = (long)sequenceHeader.MaxFrameHeight + (DecoderPaddingValue * 2L);
        Av1ColorFormat colorFormat = sequenceHeader.ColorConfig.IsMonochrome ? Av1ColorFormat.Yuv400 : maxColorFormat;
        long strideChroma = 0;
        long heightChroma = 0;

        switch (colorFormat)
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

        long lumaElementCount = strideY * storageElementsPerSample * heightY;
        long chromaElementCount = strideChroma * storageElementsPerSample * heightChroma;
        if (lumaElementCount >= int.MaxValue ||
            (!sequenceHeader.ColorConfig.IsMonochrome && chromaElementCount >= int.MaxValue))
        {
            // Every decoder operator addresses padding through one contiguous span. Reject external sequence geometry
            // before frame-wide syntax state is allocated so hostile dimensions cannot bypass allocator limits.
            throw new InvalidImageContentException("The AV1 frame dimensions exceed the contiguous decoder plane limit.");
        }
    }

    /// <summary>
    /// Gets the padded storage allocation for one component plane.
    /// </summary>
    /// <param name="plane">The requested component plane.</param>
    /// <returns>The requested plane allocation.</returns>
    public Buffer2D<T> GetPlaneBuffer(Av1Plane plane)
    {
        this.GetPlaneLayout(plane, 0, 0, out Buffer2D<T> buffer, out _, out _, out _, out _);
        return buffer;
    }

    /// <summary>
    /// Copies the visible sample planes and active picture geometry to another compatible frame buffer.
    /// </summary>
    /// <param name="destination">The frame buffer receiving the copied reconstruction.</param>
    public void CopyVisibleTo(Av1FrameBuffer<T> destination)
    {
        destination.StartPosition = this.StartPosition;
        destination.OriginX = this.OriginX;
        destination.OriginY = this.OriginY;
        destination.Width = this.Width;
        destination.Height = this.Height;
        destination.MaxWidth = this.MaxWidth;
        destination.MaxHeight = this.MaxHeight;
        destination.BitDepth = this.BitDepth;
        destination.ColorFormat = this.ColorFormat;

        int planeCount = this.ColorFormat == Av1ColorFormat.Yuv400 ? 1 : 3;
        for (int planeIndex = 0; planeIndex < planeCount; planeIndex++)
        {
            Av1Plane plane = (Av1Plane)planeIndex;
            int subX = plane != Av1Plane.Y && this.ColorFormat is Av1ColorFormat.Yuv420 or Av1ColorFormat.Yuv422 ? 1 : 0;
            int subY = plane != Av1Plane.Y && this.ColorFormat == Av1ColorFormat.Yuv420 ? 1 : 0;

            this.GetPlaneLayout(
                plane,
                subX,
                subY,
                out Buffer2D<T> sourceBuffer,
                out int sourceOriginX,
                out int sourceOriginY,
                out int width,
                out int height);

            destination.GetPlaneLayout(
                plane,
                subX,
                subY,
                out Buffer2D<T> destinationBuffer,
                out int destinationOriginX,
                out int destinationOriginY,
                out _,
                out _);

            int storageWidth = width * this.storageElementsPerSample;
            int sourceStorageX = sourceOriginX * this.storageElementsPerSample;
            int destinationStorageX = destinationOriginX * this.storageElementsPerSample;

            // A film-grain presentation owns only the active picture. Grain synthesis creates its odd-edge
            // extension before reading it, so copying reference borders or unused sequence-sized storage is waste.
            for (int row = 0; row < height; row++)
            {
                sourceBuffer.DangerousGetRowSpan(sourceOriginY + row)
                    .Slice(sourceStorageX, storageWidth)
                    .CopyTo(destinationBuffer.DangerousGetRowSpan(destinationOriginY + row).Slice(destinationStorageX, storageWidth));
            }
        }
    }

    /// <summary>
    /// Releases the owned luma and chroma plane allocations.
    /// </summary>
    public void Dispose()
    {
        FramePlanes? ownedPlanes = this.planes;
        this.planes = null;
        if (ownedPlanes is null)
        {
            return;
        }

        FramePlanes activePlanes = ownedPlanes.Value;
        activePlanes.Luma.Dispose();
        ChromaPlanes? chroma = activePlanes.Chroma;
        if (chroma is not null)
        {
            chroma.Value.Blue.Dispose();
            chroma.Value.Red.Dispose();
        }
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
        FramePlanes? ownedPlanes = this.planes;
        ObjectDisposedException.ThrowIf(ownedPlanes is null, this);

        FramePlanes activePlanes = ownedPlanes.Value;
        switch (plane)
        {
            case Av1Plane.Y:
                buffer = activePlanes.Luma;
                originX = this.OriginX;
                originY = this.OriginY;
                width = this.Width;
                height = this.Height;
                break;
            case Av1Plane.U:
                buffer = activePlanes.Chroma?.Blue
                    ?? throw new InvalidOperationException("A monochrome AV1 frame has no blue-difference plane.");

                originX = this.OriginX >> subX;
                originY = this.OriginY >> subY;
                width = Av1Math.DivideLog2Ceiling(this.Width, subX);
                height = Av1Math.DivideLog2Ceiling(this.Height, subY);
                break;
            case Av1Plane.V:
            default:
                buffer = activePlanes.Chroma?.Red
                    ?? throw new InvalidOperationException("A monochrome AV1 frame has no red-difference plane.");

                originX = this.OriginX >> subX;
                originY = this.OriginY >> subY;
                width = Av1Math.DivideLog2Ceiling(this.Width, subX);
                height = Av1Math.DivideLog2Ceiling(this.Height, subY);
                break;
        }
    }

    /// <summary>
    /// Carries the mandatory luma owner and the optional complete chroma pair as one state.
    /// </summary>
    private readonly struct FramePlanes(Buffer2D<T> luma, ChromaPlanes? chroma)
    {
        /// <summary>
        /// Gets the padded luma plane.
        /// </summary>
        public Buffer2D<T> Luma { get; } = luma;

        /// <summary>
        /// Gets the padded chroma planes when the frame contains chroma.
        /// </summary>
        public ChromaPlanes? Chroma { get; } = chroma;
    }

    private readonly struct ChromaPlanes(Buffer2D<T> blue, Buffer2D<T> red)
    {
        /// <summary>
        /// Gets the padded blue-difference plane.
        /// </summary>
        public Buffer2D<T> Blue { get; } = blue;

        /// <summary>
        /// Gets the padded red-difference plane.
        /// </summary>
        public Buffer2D<T> Red { get; } = red;
    }
}
