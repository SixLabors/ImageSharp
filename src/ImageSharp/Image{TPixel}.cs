// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp;

/// <summary>
/// Encapsulates an image, which consists of the pixel data for a graphics image and its attributes.
/// For generic <see cref="Image{TPixel}"/>-s the pixel type is known at compile time.
/// </summary>
/// <typeparam name="TPixel">The pixel format.</typeparam>
public sealed class Image<TPixel> : Image
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly ImageFrameCollection<TPixel> frames;

    /// <summary>
    /// Initializes a new instance of the <see cref="Image{TPixel}"/> class
    /// with the height and the width of the image.
    /// </summary>
    /// <param name="configuration">The configuration providing initialization code which allows extending the library.</param>
    /// <param name="width">The width of the image in pixels.</param>
    /// <param name="height">The height of the image in pixels.</param>
    public Image(Configuration configuration, int width, int height)
        : this(configuration, width, height, new ImageMetadata())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Image{TPixel}"/> class
    /// with the height and the width of the image.
    /// </summary>
    /// <param name="configuration">The configuration providing initialization code which allows extending the library.</param>
    /// <param name="width">The width of the image in pixels.</param>
    /// <param name="height">The height of the image in pixels.</param>
    /// <param name="backgroundColor">The color to initialize the pixels with.</param>
    public Image(Configuration configuration, int width, int height, TPixel backgroundColor)
        : this(configuration, width, height, backgroundColor, new())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Image{TPixel}"/> class
    /// with the height and the width of the image.
    /// </summary>
    /// <param name="width">The width of the image in pixels.</param>
    /// <param name="height">The height of the image in pixels.</param>
    /// <param name="backgroundColor">The color to initialize the pixels with.</param>
    public Image(int width, int height, TPixel backgroundColor)
        : this(Configuration.Default, width, height, backgroundColor, new())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Image{TPixel}"/> class
    /// with the height and the width of the image.
    /// </summary>
    /// <param name="width">The width of the image in pixels.</param>
    /// <param name="height">The height of the image in pixels.</param>
    public Image(int width, int height)
        : this(Configuration.Default, width, height)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Image{TPixel}"/> class
    /// with the height and the width of the image.
    /// </summary>
    /// <param name="configuration">The configuration providing initialization code which allows extending the library.</param>
    /// <param name="width">The width of the image in pixels.</param>
    /// <param name="height">The height of the image in pixels.</param>
    /// <param name="metadata">The images metadata.</param>
    internal Image(Configuration configuration, int width, int height, ImageMetadata? metadata)
        : base(configuration, TPixel.GetPixelTypeInfo(), metadata ?? new(), width, height)
        => this.frames = new(this, width, height, default(TPixel));

    /// <summary>
    /// Initializes a new instance of the <see cref="Image{TPixel}"/> class
    /// wrapping an external <see cref="Buffer2D{TPixel}"/> pixel buffer.
    /// </summary>
    /// <param name="configuration">The configuration providing initialization code which allows extending the library.</param>
    /// <param name="pixelBuffer">Pixel buffer.</param>
    /// <param name="metadata">The images metadata.</param>
    internal Image(
        Configuration configuration,
        Buffer2D<TPixel> pixelBuffer,
        ImageMetadata metadata)
        : this(configuration, pixelBuffer.FastMemoryGroup, pixelBuffer.Width, pixelBuffer.Height, metadata)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Image{TPixel}"/> class
    /// wrapping an external <see cref="MemoryGroup{T}"/>.
    /// </summary>
    /// <param name="configuration">The configuration providing initialization code which allows extending the library.</param>
    /// <param name="memoryGroup">The memory source.</param>
    /// <param name="width">The width of the image in pixels.</param>
    /// <param name="height">The height of the image in pixels.</param>
    /// <param name="metadata">The images metadata.</param>
    internal Image(
        Configuration configuration,
        MemoryGroup<TPixel> memoryGroup,
        int width,
        int height,
        ImageMetadata metadata)
        : base(configuration, TPixel.GetPixelTypeInfo(), metadata, width, height)
        => this.frames = new(this, width, height, memoryGroup);

    /// <summary>
    /// Initializes a new instance of the <see cref="Image{TPixel}"/> class
    /// with the height and the width of the image.
    /// </summary>
    /// <param name="configuration">The configuration providing initialization code which allows extending the library.</param>
    /// <param name="width">The width of the image in pixels.</param>
    /// <param name="height">The height of the image in pixels.</param>
    /// <param name="backgroundColor">The color to initialize the pixels with.</param>
    /// <param name="metadata">The images metadata.</param>
    internal Image(
        Configuration configuration,
        int width,
        int height,
        TPixel backgroundColor,
        ImageMetadata? metadata)
        : base(configuration, TPixel.GetPixelTypeInfo(), metadata ?? new(), width, height)
        => this.frames = new(this, width, height, backgroundColor);

    /// <summary>
    /// Initializes a new instance of the <see cref="Image{TPixel}" /> class
    /// with the height and the width of the image.
    /// </summary>
    /// <param name="configuration">The configuration providing initialization code which allows extending the library.</param>
    /// <param name="metadata">The images metadata.</param>
    /// <param name="frames">The frames that will be owned by this image instance.</param>
    internal Image(Configuration configuration, ImageMetadata metadata, IEnumerable<ImageFrame<TPixel>> frames)
        : base(configuration, TPixel.GetPixelTypeInfo(), metadata, ValidateFramesAndGetSize(frames))
        => this.frames = new(this, frames);

    /// <inheritdoc />
    protected override ImageFrameCollection NonGenericFrameCollection => this.Frames;

    /// <summary>
    /// Gets the collection of image frames.
    /// </summary>
    public new ImageFrameCollection<TPixel> Frames
    {
        get
        {
            this.EnsureNotDisposed();
            return this.frames;
        }
    }

    /// <summary>
    /// Gets the root frame.
    /// </summary>
    private ImageFrame<TPixel> PixelSourceUnsafe => this.frames.RootFrameUnsafe;

    /// <summary>
    /// Gets or sets the pixel at the specified position.
    /// </summary>
    /// <param name="x">The x-coordinate of the pixel. Must be greater than or equal to zero and less than the width of the image.</param>
    /// <param name="y">The y-coordinate of the pixel. Must be greater than or equal to zero and less than the height of the image.</param>
    /// <returns>The <see typeparam="TPixel"/> at the specified position.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the provided (x,y) coordinates are outside the image boundary.</exception>
    public TPixel this[int x, int y]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            this.EnsureNotDisposed();

            this.VerifyCoords(x, y);
            return this.PixelSourceUnsafe.PixelBuffer.GetElementUnsafe(x, y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            this.EnsureNotDisposed();

            this.VerifyCoords(x, y);
            this.PixelSourceUnsafe.PixelBuffer.GetElementUnsafe(x, y) = value;
        }
    }

    /// <summary>
    /// Execute <paramref name="processPixels"/> to process image pixels in a safe and efficient manner.
    /// </summary>
    /// <param name="processPixels">The <see cref="PixelAccessorAction{TPixel}"/> defining the pixel operations.</param>
    public void ProcessPixelRows(PixelAccessorAction<TPixel> processPixels)
    {
        Guard.NotNull(processPixels, nameof(processPixels));
        Buffer2D<TPixel> buffer = this.Frames.RootFrame.PixelBuffer;
        buffer.FastMemoryGroup.IncreaseRefCounts();

        try
        {
            PixelAccessor<TPixel> accessor = new(buffer);
            processPixels(accessor);
        }
        finally
        {
            buffer.FastMemoryGroup.DecreaseRefCounts();
        }
    }

    /// <summary>
    /// Execute <paramref name="processPixels"/> to process pixels of multiple images in a safe and efficient manner.
    /// </summary>
    /// <param name="image2">The second image.</param>
    /// <param name="processPixels">The <see cref="PixelAccessorAction{TPixel, TPixel2}"/> defining the pixel operations.</param>
    /// <typeparam name="TPixel2">The pixel type of the second image.</typeparam>
    public void ProcessPixelRows<TPixel2>(
        Image<TPixel2> image2,
        PixelAccessorAction<TPixel, TPixel2> processPixels)
        where TPixel2 : unmanaged, IPixel<TPixel2>
    {
        Guard.NotNull(image2, nameof(image2));
        Guard.NotNull(processPixels, nameof(processPixels));

        Buffer2D<TPixel> buffer1 = this.Frames.RootFrame.PixelBuffer;
        Buffer2D<TPixel2> buffer2 = image2.Frames.RootFrame.PixelBuffer;

        buffer1.FastMemoryGroup.IncreaseRefCounts();
        buffer2.FastMemoryGroup.IncreaseRefCounts();

        try
        {
            PixelAccessor<TPixel> accessor1 = new(buffer1);
            PixelAccessor<TPixel2> accessor2 = new(buffer2);
            processPixels(accessor1, accessor2);
        }
        finally
        {
            buffer2.FastMemoryGroup.DecreaseRefCounts();
            buffer1.FastMemoryGroup.DecreaseRefCounts();
        }
    }

    /// <summary>
    /// Execute <paramref name="processPixels"/> to process pixels of multiple images in a safe and efficient manner.
    /// </summary>
    /// <param name="image2">The second image.</param>
    /// <param name="image3">The third image.</param>
    /// <param name="processPixels">The <see cref="PixelAccessorAction{TPixel, TPixel2, TPixel3}"/> defining the pixel operations.</param>
    /// <typeparam name="TPixel2">The pixel type of the second image.</typeparam>
    /// <typeparam name="TPixel3">The pixel type of the third image.</typeparam>
    public void ProcessPixelRows<TPixel2, TPixel3>(
        Image<TPixel2> image2,
        Image<TPixel3> image3,
        PixelAccessorAction<TPixel, TPixel2, TPixel3> processPixels)
        where TPixel2 : unmanaged, IPixel<TPixel2>
        where TPixel3 : unmanaged, IPixel<TPixel3>
    {
        Guard.NotNull(image2, nameof(image2));
        Guard.NotNull(image3, nameof(image3));
        Guard.NotNull(processPixels, nameof(processPixels));

        Buffer2D<TPixel> buffer1 = this.Frames.RootFrame.PixelBuffer;
        Buffer2D<TPixel2> buffer2 = image2.Frames.RootFrame.PixelBuffer;
        Buffer2D<TPixel3> buffer3 = image3.Frames.RootFrame.PixelBuffer;

        buffer1.FastMemoryGroup.IncreaseRefCounts();
        buffer2.FastMemoryGroup.IncreaseRefCounts();
        buffer3.FastMemoryGroup.IncreaseRefCounts();

        try
        {
            PixelAccessor<TPixel> accessor1 = new(buffer1);
            PixelAccessor<TPixel2> accessor2 = new(buffer2);
            PixelAccessor<TPixel3> accessor3 = new(buffer3);
            processPixels(accessor1, accessor2, accessor3);
        }
        finally
        {
            buffer3.FastMemoryGroup.DecreaseRefCounts();
            buffer2.FastMemoryGroup.DecreaseRefCounts();
            buffer1.FastMemoryGroup.DecreaseRefCounts();
        }
    }

    /// <summary>
    /// Copy image pixels to <paramref name="destination"/>.
    /// </summary>
    /// <param name="destination">The <see cref="Span{TPixel}"/> to copy image pixels to.</param>
    public void CopyPixelDataTo(Span<TPixel> destination) => this.GetPixelMemoryGroup().CopyTo(destination);

    /// <summary>
    /// Copy image pixels to <paramref name="destination"/>.
    /// </summary>
    /// <param name="destination">The <see cref="Span{T}"/> of <see cref="byte"/> to copy image pixels to.</param>
    public void CopyPixelDataTo(Span<byte> destination) => this.GetPixelMemoryGroup().CopyTo(MemoryMarshal.Cast<byte, TPixel>(destination));

    /// <summary>
    /// Gets the representation of the pixels as a <see cref="Memory{T}"/> in the source image's pixel format
    /// stored in row major order, if the backing buffer is contiguous.
    /// <para />
    /// To ensure the memory is contiguous, <see cref="Configuration.PreferContiguousImageBuffers"/> should be set
    /// to true, preferably on a non-global configuration instance (not <see cref="Configuration.Default"/>).
    /// <para />
    /// WARNING: Disposing or leaking the underlying image while still working with the <paramref name="memory"/>'s <see cref="Span{T}"/>
    /// might lead to memory corruption.
    /// </summary>
    /// <param name="memory">The <see cref="Memory{T}"/> referencing the image buffer.</param>
    /// <returns>The <see cref="bool"/> indicating the success.</returns>
    public bool DangerousTryGetSinglePixelMemory(out Memory<TPixel> memory)
    {
        IMemoryGroup<TPixel> mg = this.GetPixelMemoryGroup();
        if (mg.Count > 1)
        {
            memory = default;
            return false;
        }

        memory = mg.Single();
        return true;
    }

    /// <summary>
    /// Clones the current image.
    /// </summary>
    /// <returns>Returns a new image with all the same metadata as the original.</returns>
    public Image<TPixel> Clone() => this.Clone(this.Configuration);

    /// <summary>
    /// Clones the current image with the given configuration.
    /// </summary>
    /// <param name="configuration">The configuration providing initialization code which allows extending the library.</param>
    /// <returns>Returns a new <see cref="Image{TPixel}"/> with all the same pixel data as the original.</returns>
    public Image<TPixel> Clone(Configuration configuration)
    {
        this.EnsureNotDisposed();

        ImageFrame<TPixel>[] clonedFrames = new ImageFrame<TPixel>[this.frames.Count];
        for (int i = 0; i < clonedFrames.Length; i++)
        {
            clonedFrames[i] = this.frames[i].Clone(configuration);
        }

        return new(configuration, this.Metadata.DeepClone(), clonedFrames);
    }

    /// <summary>
    /// Returns a copy of the image in the given pixel format.
    /// </summary>
    /// <typeparam name="TPixel2">The pixel format.</typeparam>
    /// <param name="configuration">The configuration providing initialization code which allows extending the library.</param>
    /// <returns>The <see cref="Image{TPixel2}"/>.</returns>
    public override Image<TPixel2> CloneAs<TPixel2>(Configuration configuration)
    {
        this.EnsureNotDisposed();

        ImageFrame<TPixel2>[] clonedFrames = new ImageFrame<TPixel2>[this.frames.Count];
        for (int i = 0; i < clonedFrames.Length; i++)
        {
            clonedFrames[i] = this.frames[i].CloneAs<TPixel2>(configuration);
        }

        return new(configuration, this.Metadata.DeepClone(), clonedFrames);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            this.frames.Dispose();
        }
    }

    /// <inheritdoc/>
    public override string ToString() => $"Image<{typeof(TPixel).Name}>: {this.Width}x{this.Height}";

    /// <inheritdoc />
    internal override void Accept(IImageVisitor visitor)
    {
        this.EnsureNotDisposed();

        visitor.Visit(this);
    }

    /// <inheritdoc />
    internal override Task AcceptAsync(IImageVisitorAsync visitor, CancellationToken cancellationToken)
    {
        this.EnsureNotDisposed();

        return visitor.VisitAsync(this, cancellationToken);
    }

    /// <summary>
    /// Switches the buffers used by the image and the pixel source meaning that the Image will
    /// "own" the buffer from the pixelSource and the pixel source will now own the Image buffer.
    /// </summary>
    /// <param name="source">The pixel source.</param>
    internal void SwapOrCopyPixelsBuffersFrom(Image<TPixel> source)
    {
        Guard.NotNull(source, nameof(source));

        this.EnsureNotDisposed();

        ImageFrameCollection<TPixel> sourceFrames = source.Frames;
        for (int i = 0; i < this.frames.Count; i++)
        {
            this.frames[i].SwapOrCopyPixelsBufferFrom(sourceFrames[i]);
        }

        this.UpdateSize(source.Size);
    }

    /// <summary>
    /// Copies the metadata from the source image.
    /// </summary>
    /// <param name="source">The metadata source.</param>
    internal void CopyMetadataFrom(Image<TPixel> source)
    {
        Guard.NotNull(source, nameof(source));

        this.EnsureNotDisposed();

        ImageFrameCollection<TPixel> sourceFrames = source.Frames;
        for (int i = 0; i < this.frames.Count; i++)
        {
            this.frames[i].CopyMetadataFrom(sourceFrames[i]);
        }

        this.UpdateMetadata(source.Metadata);
    }

    private static Size ValidateFramesAndGetSize(IEnumerable<ImageFrame<TPixel>> frames)
    {
        Guard.NotNull(frames, nameof(frames));

        ImageFrame<TPixel>? rootFrame = frames.FirstOrDefault() ?? throw new ArgumentException("Must not be empty.", nameof(frames));

        Size rootSize = rootFrame.Size;

        if (frames.Any(f => f.Size != rootSize))
        {
            throw new ArgumentException("The provided frames must be of the same size.", nameof(frames));
        }

        return rootSize;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void VerifyCoords(int x, int y)
    {
        if ((uint)x >= (uint)this.Width)
        {
            ThrowArgumentOutOfRangeException(nameof(x));
        }

        if ((uint)y >= (uint)this.Height)
        {
            ThrowArgumentOutOfRangeException(nameof(y));
        }
    }

    private static void ThrowArgumentOutOfRangeException(string paramName)
        => throw new ArgumentOutOfRangeException(paramName);
}
