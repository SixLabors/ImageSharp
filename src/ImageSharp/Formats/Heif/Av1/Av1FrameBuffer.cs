// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
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
    /// The number of <typeparamref name="T"/> elements occupied by one logical sample.
    /// </summary>
    private int storageElementsPerSample;

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
        : this(
            configuration.MemoryAllocator,
            sequenceHeader,
            maxColorFormat,
            is16BitPipeline,
            allocationWidth,
            allocationHeight,
            FrameBufferKind.Reconstruction)
    {
    }

    private Av1FrameBuffer(
        MemoryAllocator allocator,
        ObuSequenceHeader sequenceHeader,
        Av1ColorFormat maxColorFormat,
        bool is16BitPipeline,
        int allocationWidth,
        int allocationHeight,
        FrameBufferKind kind)
    {
        ValidateDimensions(sequenceHeader, maxColorFormat, is16BitPipeline, allocationWidth, allocationHeight);

        this.MemoryAllocator = allocator;
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
        int border = kind switch
        {
            FrameBufferKind.Presentation => 0,
            FrameBufferKind.Restoration => 32,
            _ => DecoderPaddingValue
        };

        this.StartPosition = new Point(border, border);

        this.Width = this.MaxWidth;
        this.Height = this.MaxHeight;
        this.OriginX = border;
        this.OriginY = border;

        FrameBufferLayout layout = CreateFrameBufferLayout(
            allocationWidth,
            allocationHeight,
            colorFormat,
            this.storageElementsPerSample,
            kind);

        // One allocation owns every component plane. Restoration retains its capacity across frames;
        // new storage starts cleared so unwritten alignment and border slots cannot expose pooled data.
        AllocationOptions options = kind == FrameBufferKind.Restoration ? AllocationOptions.Clean : AllocationOptions.None;
        this.planes = FramePlanes.Allocate(allocator, layout, colorFormat, options);
    }

    /// <summary>
    /// Selects the border and alignment required by the frame's use.
    /// </summary>
    private enum FrameBufferKind
    {
        Reconstruction,
        Presentation,
        Restoration
    }

    /// <summary>
    /// Gets the padded luma-coordinate origin of the visible frame.
    /// </summary>
    public Point StartPosition { get; private set; }

    /// <summary>
    /// Gets the complete padded luma plane, or <see langword="null"/> after disposal.
    /// </summary>
    public Av1PlaneRegion<T>? BufferY => this.planes?.Luma;

    /// <summary>
    /// Gets the complete padded blue-difference plane, or <see langword="null"/> for monochrome or after disposal.
    /// </summary>
    public Av1PlaneRegion<T>? BufferCb => this.planes?.Blue;

    /// <summary>
    /// Gets the complete padded red-difference plane, or <see langword="null"/> for monochrome or after disposal.
    /// </summary>
    public Av1PlaneRegion<T>? BufferCr => this.planes?.Red;

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
    public int BytesPerSample { get; private set; }

    /// <summary>
    /// Gets the color configuration signaled by the AV1 sequence header.
    /// </summary>
    public ObuColorConfig ColorConfig { get; private set; }

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
    public bool Is16BitPipeline { get; private set; }

    /// <summary>
    /// Gets the allocator used for frame-owned and frame-scoped working buffers.
    /// </summary>
    public MemoryAllocator MemoryAllocator { get; }

    /// <summary>
    /// Validates that the requested frame planes fit the decoder's contiguous ownership contract.
    /// </summary>
    /// <param name="sequenceHeader">The sequence header defining bit depth and chroma layout.</param>
    /// <param name="maxColorFormat">The color format required by the frame.</param>
    /// <param name="is16BitPipeline">Indicates whether reconstruction uses native 16-bit sample storage.</param>
    /// <param name="allocationWidth">The active luma width required by the allocation.</param>
    /// <param name="allocationHeight">The active luma height required by the allocation.</param>
    public static void ValidateDimensions(
        ObuSequenceHeader sequenceHeader,
        Av1ColorFormat maxColorFormat,
        bool is16BitPipeline,
        int allocationWidth,
        int allocationHeight)
    {
        int bytesPerSample = sequenceHeader.ColorConfig.BitDepth > Av1BitDepth.EightBit || is16BitPipeline ? 2 : 1;
        int storageElementsPerSample = Math.Max(
            (bytesPerSample + Unsafe.SizeOf<T>() - 1) / Unsafe.SizeOf<T>(),
            1);

        Av1ColorFormat colorFormat = sequenceHeader.ColorConfig.IsMonochrome ? Av1ColorFormat.Yuv400 : maxColorFormat;
        _ = CreateFrameBufferLayout(
            allocationWidth,
            allocationHeight,
            colorFormat,
            storageElementsPerSample,
            FrameBufferKind.Reconstruction);
    }

    /// <summary>
    /// Creates an empty presentation frame for the source's visible picture.
    /// </summary>
    /// <param name="configuration">The configuration providing the plane allocator.</param>
    /// <param name="sequenceHeader">The sequence describing the source samples.</param>
    /// <param name="source">The frame whose visible dimensions and sample format are required.</param>
    /// <returns>A frame ready to receive the source's visible samples.</returns>
    public static Av1FrameBuffer<T> CreatePresentation(
        Configuration configuration,
        ObuSequenceHeader sequenceHeader,
        Av1FrameBuffer<T> source)
        => new(
            configuration.MemoryAllocator,
            sequenceHeader,
            source.ColorFormat,
            source.Is16BitPipeline,
            source.Width,
            source.Height,
            FrameBufferKind.Presentation);

    /// <summary>
    /// Creates a compact monochrome plane for a composed auxiliary image.
    /// </summary>
    /// <param name="configuration">The configuration providing sample storage.</param>
    /// <param name="colorConfig">The sample precision and range of the auxiliary items.</param>
    /// <param name="size">The complete auxiliary image extent.</param>
    /// <returns>The compact writable auxiliary plane.</returns>
    public static Av1FrameBuffer<T> CreateAuxiliary(
        Configuration configuration,
        ObuColorConfig colorConfig,
        Size size)
        => new(
            configuration.MemoryAllocator,
            new ObuSequenceHeader { ColorConfig = colorConfig },
            Av1ColorFormat.Yuv400,
            colorConfig.BitDepth > Av1BitDepth.EightBit,
            size.Width,
            size.Height,
            FrameBufferKind.Presentation);

    /// <summary>
    /// Creates an empty restoration frame with the source's visible dimensions and sample format.
    /// </summary>
    /// <param name="allocator">The allocator for the frame planes.</param>
    /// <param name="sequenceHeader">The sequence describing the source samples.</param>
    /// <param name="source">The reconstructed frame whose output geometry is required.</param>
    /// <returns>A frame ready to receive restored samples.</returns>
    public static Av1FrameBuffer<T> CreateRestoration(
        MemoryAllocator allocator,
        ObuSequenceHeader sequenceHeader,
        Av1FrameBuffer<T> source)
        => new(allocator, sequenceHeader, source.ColorFormat, source.Is16BitPipeline, source.Width, source.Height, FrameBufferKind.Restoration);

    /// <summary>
    /// Prepares this restoration frame for the source's visible dimensions and sample format.
    /// </summary>
    /// <param name="sequenceHeader">The sequence describing the source samples.</param>
    /// <param name="source">The reconstructed frame whose output geometry is required.</param>
    public void ResizeRestoration(ObuSequenceHeader sequenceHeader, Av1FrameBuffer<T> source)
    {
        // The decoder owns this live restoration frame for its entire session. A different layout may
        // need new views without needing a new allocation; capacity grows only when the new planes exceed it.
        FramePlanes? retainedPlanes = this.planes;
        if (retainedPlanes is null || this.Width != source.Width || this.Height != source.Height ||
            this.ColorFormat != source.ColorFormat || this.BytesPerSample != source.BytesPerSample)
        {
            FrameBufferLayout layout = CreateFrameBufferLayout(
                source.Width,
                source.Height,
                source.ColorFormat,
                source.storageElementsPerSample,
                FrameBufferKind.Restoration);

            FramePlanes activePlanes = retainedPlanes.GetValueOrDefault();
            if (retainedPlanes is null || !activePlanes.CanHold(layout))
            {
                // Restoration needs none of the previous target's samples. Release its old allocation
                // before growing so two complete output frames never overlap in memory. A failed rent
                // leaves an empty target that session disposal or the next resize can handle.
                this.Dispose();
                this.planes = FramePlanes.Allocate(this.MemoryAllocator, layout, source.ColorFormat, AllocationOptions.Clean);
            }
            else
            {
                this.planes = activePlanes.WithLayout(layout, source.ColorFormat);
            }
        }

        this.Width = this.MaxWidth = source.Width;
        this.Height = this.MaxHeight = source.Height;
        this.BitDepth = source.BitDepth;
        this.ColorConfig = sequenceHeader.ColorConfig;
        this.ColorFormat = source.ColorFormat;
        this.BytesPerSample = source.BytesPerSample;
        this.storageElementsPerSample = source.storageElementsPerSample;
        this.Is16BitPipeline = source.Is16BitPipeline;
    }

    /// <summary>
    /// Gets the padded storage allocation for one component plane.
    /// </summary>
    /// <param name="plane">The requested component plane.</param>
    /// <returns>The requested plane allocation.</returns>
    public Av1PlaneRegion<T> GetPlaneBuffer(Av1Plane plane)
    {
        this.GetPlaneLayout(plane, 0, 0, out Av1PlaneRegion<T> buffer, out _, out _, out _, out _);
        return buffer;
    }

    /// <summary>
    /// Copies the visible sample planes and active picture geometry to another compatible frame buffer.
    /// </summary>
    /// <param name="destination">The frame buffer receiving the copied reconstruction.</param>
    public void CopyVisibleTo(Av1FrameBuffer<T> destination)
    {
        // Origins, strides, and capacity belong to the destination allocation. Only the active picture extent
        // transfers: a presentation copy may have no border and need much less storage than its source.
        destination.Width = this.Width;
        destination.Height = this.Height;
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
                out Av1PlaneRegion<T> sourceBuffer,
                out int sourceOriginX,
                out int sourceOriginY,
                out int width,
                out int height);

            destination.GetPlaneLayout(
                plane,
                subX,
                subY,
                out Av1PlaneRegion<T> destinationBuffer,
                out int destinationOriginX,
                out int destinationOriginY,
                out _,
                out _);

            int storageWidth = width * this.storageElementsPerSample;
            int sourceStorageX = sourceOriginX * this.storageElementsPerSample;
            int destinationStorageX = destinationOriginX * this.storageElementsPerSample;
            ReadOnlySpan<T> sourceSamples = sourceBuffer.Samples;
            Span<T> destinationSamples = destinationBuffer.Samples;
            int sourceOffset = (sourceOriginY * sourceBuffer.Stride) + sourceStorageX;
            int destinationOffset = (destinationOriginY * destinationBuffer.Stride) + destinationStorageX;

            // A film-grain presentation owns only the active picture. Grain synthesis creates its odd-edge
            // extension before reading it, so copying reference borders or unused sequence-sized storage is waste.
            for (int row = 0; row < height; row++)
            {
                sourceSamples.Slice(sourceOffset, storageWidth).CopyTo(destinationSamples.Slice(destinationOffset, storageWidth));
                sourceOffset += sourceBuffer.Stride;
                destinationOffset += destinationBuffer.Stride;
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

        ownedPlanes.Value.Dispose();
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
            out Av1PlaneRegion<T> buffer,
            out int originX,
            out int originY,
            out _,
            out _);

        int elementStride = buffer.Stride;
        stride = elementStride / this.storageElementsPerSample;
        int blockOffset = (((originY + locationInPixels.Y) * stride) + originX + locationInPixels.X) *
            this.storageElementsPerSample;

        // Intra prediction addresses above neighbors relative to the destination span, so index zero is the previous row.
        blockOffset -= elementStride;
        Guard.MustBeGreaterThanOrEqualTo(blockOffset, 0, nameof(blockOffset));

        return buffer.Samples[blockOffset..];
    }

    /// <summary>
    /// Gets the samples of a complete plane allocation, or an empty span for a chroma plane of a monochrome frame. A caller
    /// that addresses many blocks reads them once and passes them to the block pointer overloads that take plane samples.
    /// </summary>
    /// <param name="plane">The luma or chroma plane.</param>
    /// <returns>The plane samples, including decoder padding.</returns>
    public Span<T> GetPlaneSamples(Av1Plane plane)
    {
        FramePlanes? ownedPlanes = this.planes;
        ObjectDisposedException.ThrowIf(ownedPlanes is null, this);

        FramePlanes activePlanes = ownedPlanes.Value;
        Av1PlaneRegion<T>? region = plane switch
        {
            Av1Plane.Y => activePlanes.Luma,
            Av1Plane.U => activePlanes.Blue,
            _ => activePlanes.Red
        };

        return region is null ? default : region.Value.Samples;
    }

    /// <summary>
    /// Gets a span beginning one logical row before a block, from plane samples that the caller read once with
    /// <see cref="GetPlaneSamples"/>.
    /// </summary>
    /// <param name="planeSamples">The samples of <paramref name="plane"/>, from <see cref="GetPlaneSamples"/>.</param>
    /// <param name="plane">The luma or chroma plane.</param>
    /// <param name="locationInPixels">The block origin in plane samples.</param>
    /// <param name="subX">The horizontal chroma subsampling shift.</param>
    /// <param name="subY">The vertical chroma subsampling shift.</param>
    /// <param name="stride">Receives the logical samples between adjacent rows.</param>
    /// <returns>The span beginning one logical row before the block.</returns>
    public Span<T> DeriveBlockPointer(Span<T> planeSamples, Av1Plane plane, Point locationInPixels, int subX, int subY, out int stride)
    {
        this.GetPlaneLayout(plane, subX, subY, out Av1PlaneRegion<T> buffer, out int originX, out int originY, out _, out _);
        int elementStride = buffer.Stride;
        stride = elementStride / this.storageElementsPerSample;
        int blockOffset = (((originY + locationInPixels.Y) * stride) + originX + locationInPixels.X) *
            this.storageElementsPerSample;

        // Intra prediction addresses above neighbors relative to the destination span, so index zero is the previous row.
        blockOffset -= elementStride;
        Guard.MustBeGreaterThanOrEqualTo(blockOffset, 0, nameof(blockOffset));

        return planeSamples[blockOffset..];
    }

    /// <summary>
    /// Gets a native 16-bit sample span beginning one logical row before a block, from plane samples that the caller read
    /// once with <see cref="GetPlaneSamples"/>.
    /// </summary>
    /// <param name="planeSamples">The samples of <paramref name="plane"/>, from <see cref="GetPlaneSamples"/>.</param>
    /// <param name="plane">The luma or chroma plane.</param>
    /// <param name="locationInPixels">The block origin in plane samples.</param>
    /// <param name="subX">The horizontal chroma subsampling shift.</param>
    /// <param name="subY">The vertical chroma subsampling shift.</param>
    /// <param name="stride">Receives the logical samples between adjacent rows.</param>
    /// <returns>The 16-bit span beginning one logical row before the block.</returns>
    public Span<short> DeriveBlockPointer16(Span<T> planeSamples, Av1Plane plane, Point locationInPixels, int subX, int subY, out int stride)
    {
        this.GetPlaneLayout(plane, subX, subY, out Av1PlaneRegion<T> buffer, out int originX, out int originY, out _, out _);
        stride = buffer.Stride / this.storageElementsPerSample;
        int blockOffset = ((originY + locationInPixels.Y - 1) * stride) + originX + locationInPixels.X;
        Guard.MustBeGreaterThanOrEqualTo(blockOffset, 0, nameof(blockOffset));

        // High-bit-depth reconstruction uses native 16-bit samples in the byte-backed frame planes.
        return MemoryMarshal.Cast<T, short>(planeSamples)[blockOffset..];
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
            out Av1PlaneRegion<T> buffer,
            out int originX,
            out int originY,
            out _,
            out _);

        stride = buffer.Stride / this.storageElementsPerSample;
        int blockOffset = ((originY + locationInPixels.Y - 1) * stride) + originX + locationInPixels.X;
        Guard.MustBeGreaterThanOrEqualTo(blockOffset, 0, nameof(blockOffset));

        // High-bit-depth reconstruction uses native 16-bit samples in the byte-backed frame planes.
        return MemoryMarshal.Cast<T, short>(buffer.Samples)[blockOffset..];
    }

    /// <summary>
    /// Gets the visible sample region for one plane.
    /// </summary>
    /// <param name="plane">The luma or chroma plane.</param>
    /// <param name="subX">The horizontal chroma subsampling shift.</param>
    /// <param name="subY">The vertical chroma subsampling shift.</param>
    /// <returns>The plane region excluding decoder padding.</returns>
    public Av1PlaneRegion<T> DeriveBlockPointer(Av1Plane plane, int subX, int subY)
    {
        this.GetPlaneLayout(
            plane,
            subX,
            subY,
            out Av1PlaneRegion<T> buffer,
            out int originX,
            out int originY,
            out int width,
            out int height);

        Rectangle region = new(
            originX * this.storageElementsPerSample,
            originY,
            width * this.storageElementsPerSample,
            height);

        return buffer.GetSubRegion(region);
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
            out Av1PlaneRegion<T> buffer,
            out int originX,
            out int originY,
            out int width,
            out _);

        Span<ushort> samples = MemoryMarshal.Cast<T, ushort>(buffer.GetPlaneRowSpan(originY + row));
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
            out Av1PlaneRegion<T> buffer,
            out int originX,
            out int originY,
            out _,
            out _);

        stride = buffer.Stride / this.storageElementsPerSample;
        origin = new(originX, originY);
        return buffer.Samples;
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
        out Av1PlaneRegion<T> buffer,
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
                buffer = activePlanes.Blue
                    ?? throw new InvalidOperationException("A monochrome AV1 frame has no blue-difference plane.");

                originX = this.OriginX >> subX;
                originY = this.OriginY >> subY;
                width = Av1Math.DivideLog2Ceiling(this.Width, subX);
                height = Av1Math.DivideLog2Ceiling(this.Height, subY);
                break;
            case Av1Plane.V:
            default:
                buffer = activePlanes.Red
                    ?? throw new InvalidOperationException("A monochrome AV1 frame has no red-difference plane.");

                originX = this.OriginX >> subX;
                originY = this.OriginY >> subY;
                width = Av1Math.DivideLog2Ceiling(this.Width, subX);
                height = Av1Math.DivideLog2Ceiling(this.Height, subY);
                break;
        }
    }

    /// <summary>
    /// Calculates the aligned physical plane layout retained by one frame owner.
    /// </summary>
    private static FrameBufferLayout CreateFrameBufferLayout(
        int width,
        int height,
        Av1ColorFormat colorFormat,
        int storageElementsPerSample,
        FrameBufferKind kind)
    {
        // Reconstruction and restoration share eight-sample coded alignment but require different borders.
        // Grain presentation extends only an odd final row/column and aligns rows in bytes at either bit depth.
        bool isPresentation = kind == FrameBufferKind.Presentation;
        long dimensionMask = isPresentation ? 1 : 7;
        long border = kind switch
        {
            FrameBufferKind.Presentation => 0,
            FrameBufferKind.Restoration => 32,
            _ => DecoderPaddingValue
        };

        long rowAlignment = isPresentation ? Math.Max(16 / (storageElementsPerSample * Unsafe.SizeOf<T>()), 1) : 32;
        long alignedWidth = (width + dimensionMask) & ~dimensionMask;
        long alignedHeight = (height + dimensionMask) & ~dimensionMask;
        long lumaStride = (alignedWidth + (2 * border) + rowAlignment - 1) & ~(rowAlignment - 1);
        long lumaHeight = alignedHeight + (2 * border);
        int subsamplingX = colorFormat is Av1ColorFormat.Yuv420 or Av1ColorFormat.Yuv422 ? 1 : 0;
        int subsamplingY = colorFormat == Av1ColorFormat.Yuv420 ? 1 : 0;
        long chromaStride = colorFormat == Av1ColorFormat.Yuv400 ? 0 : lumaStride >> subsamplingX;
        long chromaHeight = colorFormat == Av1ColorFormat.Yuv400
            ? 0
            : (alignedHeight >> subsamplingY) + (2 * (border >> subsamplingY));

        long lumaStorageWidth = lumaStride * storageElementsPerSample;
        long chromaStorageWidth = chromaStride * storageElementsPerSample;
        long lumaElementCount = lumaStorageWidth * lumaHeight;
        long chromaElementCount = chromaStorageWidth * chromaHeight;
        long planeAlignment = isPresentation ? 1 : Math.Max(32 / Unsafe.SizeOf<T>(), 1);
        long chromaBlueOffset = ((lumaElementCount + planeAlignment - 1) / planeAlignment) * planeAlignment;
        long chromaRedOffset = ((chromaBlueOffset + chromaElementCount + planeAlignment - 1) / planeAlignment) * planeAlignment;
        long storageLength = colorFormat == Av1ColorFormat.Yuv400
            ? lumaElementCount
            : chromaRedOffset + chromaElementCount;

        if (storageLength >= int.MaxValue)
        {
            // Reconstruction operators require one contiguous owner so every padded row remains directly addressable.
            throw new InvalidImageContentException("The AV1 frame dimensions exceed the contiguous decoder frame limit.");
        }

        return new FrameBufferLayout(
            (int)lumaStorageWidth,
            (int)lumaElementCount,
            (int)chromaStorageWidth,
            (int)chromaElementCount,
            (int)chromaBlueOffset,
            (int)chromaRedOffset,
            (int)storageLength);
    }

    /// <summary>
    /// Carries the one frame allocation and the component planes laid over it.
    /// </summary>
    private readonly struct FramePlanes
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FramePlanes"/> struct.
        /// </summary>
        /// <param name="owner">The complete frame allocation.</param>
        /// <param name="layout">The plane layout inside the allocation.</param>
        /// <param name="colorFormat">The sampling layout, which decides whether chroma planes exist.</param>
        public FramePlanes(IMemoryOwner<T> owner, FrameBufferLayout layout, Av1ColorFormat colorFormat)
        {
            Memory<T> storage = owner.Memory;
            this.Owner = owner;
            this.Luma = CreatePlane(storage[..layout.LumaElementCount], layout.LumaStorageWidth);
            if (colorFormat != Av1ColorFormat.Yuv400)
            {
                this.Blue = CreatePlane(storage.Slice(layout.ChromaBlueOffset, layout.ChromaElementCount), layout.ChromaStorageWidth);
                this.Red = CreatePlane(storage.Slice(layout.ChromaRedOffset, layout.ChromaElementCount), layout.ChromaStorageWidth);
            }
        }

        /// <summary>
        /// Gets the complete frame allocation.
        /// </summary>
        public IMemoryOwner<T> Owner { get; }

        /// <summary>
        /// Gets the padded luma plane.
        /// </summary>
        public Av1PlaneRegion<T> Luma { get; }

        /// <summary>
        /// Gets the padded blue-difference plane when the frame contains chroma.
        /// </summary>
        public Av1PlaneRegion<T>? Blue { get; }

        /// <summary>
        /// Gets the padded red-difference plane when the frame contains chroma.
        /// </summary>
        public Av1PlaneRegion<T>? Red { get; }

        /// <summary>
        /// Allocates the frame storage of a layout.
        /// </summary>
        /// <param name="allocator">The frame allocator.</param>
        /// <param name="layout">The plane layout.</param>
        /// <param name="colorFormat">The sampling layout, which decides whether chroma planes exist.</param>
        /// <param name="options">The allocation options.</param>
        /// <returns>The allocated planes.</returns>
        public static FramePlanes Allocate(
            MemoryAllocator allocator,
            FrameBufferLayout layout,
            Av1ColorFormat colorFormat,
            AllocationOptions options)
            => new(allocator.Allocate<T>(layout.StorageLength, options), layout, colorFormat);

        /// <summary>
        /// Determines whether the current allocation is large enough for a layout.
        /// </summary>
        /// <param name="layout">The required plane layout.</param>
        /// <returns><see langword="true"/> when the layout fits the current allocation.</returns>
        public bool CanHold(FrameBufferLayout layout)
            => this.Owner.Memory.Length >= layout.StorageLength;

        /// <summary>
        /// Lays a layout that fits over the current allocation.
        /// </summary>
        /// <param name="layout">The new plane layout.</param>
        /// <param name="colorFormat">The new sampling layout.</param>
        /// <returns>The planes over the retained allocation.</returns>
        public FramePlanes WithLayout(FrameBufferLayout layout, Av1ColorFormat colorFormat)
            => new(this.Owner, layout, colorFormat);

        /// <summary>
        /// Releases the frame allocation.
        /// </summary>
        public void Dispose() => this.Owner.Dispose();

        /// <summary>
        /// Lays a complete padded plane over its slice of the frame allocation.
        /// </summary>
        private static Av1PlaneRegion<T> CreatePlane(Memory<T> plane, int stride)
            => new(plane, stride, new Rectangle(0, 0, stride, plane.Length / stride));
    }

    /// <summary>
    /// Describes the physical storage slices of the component planes.
    /// </summary>
    private readonly struct FrameBufferLayout
    {
        public FrameBufferLayout(
            int lumaStorageWidth,
            int lumaElementCount,
            int chromaStorageWidth,
            int chromaElementCount,
            int chromaBlueOffset,
            int chromaRedOffset,
            int storageLength)
        {
            this.LumaStorageWidth = lumaStorageWidth;
            this.LumaElementCount = lumaElementCount;
            this.ChromaStorageWidth = chromaStorageWidth;
            this.ChromaElementCount = chromaElementCount;
            this.ChromaBlueOffset = chromaBlueOffset;
            this.ChromaRedOffset = chromaRedOffset;
            this.StorageLength = storageLength;
        }

        public int LumaStorageWidth { get; }

        public int LumaElementCount { get; }

        public int ChromaStorageWidth { get; }

        public int ChromaElementCount { get; }

        public int ChromaBlueOffset { get; }

        public int ChromaRedOffset { get; }

        public int StorageLength { get; }
    }
}
