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
/// Represents a pixel-specific image frame containing all pixel data and <see cref="ImageFrameMetadata"/>.
/// In case of animated formats like gif, it contains the single frame in a animation.
/// In all other cases it is the only frame of the image.
/// </summary>
/// <typeparam name="TPixel">The pixel format.</typeparam>
public sealed class ImageFrame<TPixel> : ImageFrame, IPixelSource<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private bool isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageFrame{TPixel}" /> class.
    /// </summary>
    /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
    /// <param name="size">The <see cref="Size"/> of the frame.</param>
    internal ImageFrame(Configuration configuration, Size size)
        : this(configuration, size.Width, size.Height, new ImageFrameMetadata())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageFrame{TPixel}" /> class.
    /// </summary>
    /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
    /// <param name="width">The width of the image in pixels.</param>
    /// <param name="height">The height of the image in pixels.</param>
    internal ImageFrame(Configuration configuration, int width, int height)
        : this(configuration, width, height, new ImageFrameMetadata())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageFrame{TPixel}" /> class.
    /// </summary>
    /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
    /// <param name="size">The <see cref="Size"/> of the frame.</param>
    /// <param name="metadata">The metadata.</param>
    internal ImageFrame(Configuration configuration, Size size, ImageFrameMetadata metadata)
        : this(configuration, size.Width, size.Height, metadata)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageFrame{TPixel}" /> class.
    /// </summary>
    /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
    /// <param name="width">The width of the image in pixels.</param>
    /// <param name="height">The height of the image in pixels.</param>
    /// <param name="metadata">The metadata.</param>
    internal ImageFrame(Configuration configuration, int width, int height, ImageFrameMetadata metadata)
        : base(configuration, width, height, metadata)
    {
        Guard.MustBeGreaterThan(width, 0, nameof(width));
        Guard.MustBeGreaterThan(height, 0, nameof(height));

        this.PixelBuffer = this.Configuration.MemoryAllocator.Allocate2D<TPixel>(
            width,
            height,
            configuration.PreferContiguousImageBuffers,
            AllocationOptions.Clean);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageFrame{TPixel}" /> class.
    /// </summary>
    /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
    /// <param name="width">The width of the image in pixels.</param>
    /// <param name="height">The height of the image in pixels.</param>
    /// <param name="backgroundColor">The color to clear the image with.</param>
    internal ImageFrame(Configuration configuration, int width, int height, TPixel backgroundColor)
        : this(configuration, width, height, backgroundColor, new ImageFrameMetadata())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageFrame{TPixel}" /> class.
    /// </summary>
    /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
    /// <param name="width">The width of the image in pixels.</param>
    /// <param name="height">The height of the image in pixels.</param>
    /// <param name="backgroundColor">The color to clear the image with.</param>
    /// <param name="metadata">The metadata.</param>
    internal ImageFrame(Configuration configuration, int width, int height, TPixel backgroundColor, ImageFrameMetadata metadata)
        : base(configuration, width, height, metadata)
    {
        Guard.MustBeGreaterThan(width, 0, nameof(width));
        Guard.MustBeGreaterThan(height, 0, nameof(height));

        this.PixelBuffer = this.Configuration.MemoryAllocator.Allocate2D<TPixel>(
            width,
            height,
            configuration.PreferContiguousImageBuffers);
        this.Clear(backgroundColor);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageFrame{TPixel}" /> class wrapping an existing buffer.
    /// </summary>
    /// <param name="configuration">The configuration providing initialization code which allows extending the library.</param>
    /// <param name="width">The width of the image in pixels.</param>
    /// <param name="height">The height of the image in pixels.</param>
    /// <param name="memorySource">The memory source.</param>
    internal ImageFrame(Configuration configuration, int width, int height, MemoryGroup<TPixel> memorySource)
        : this(configuration, width, height, memorySource, new ImageFrameMetadata())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageFrame{TPixel}" /> class wrapping an existing buffer.
    /// </summary>
    /// <param name="configuration">The configuration providing initialization code which allows extending the library.</param>
    /// <param name="width">The width of the image in pixels.</param>
    /// <param name="height">The height of the image in pixels.</param>
    /// <param name="memorySource">The memory source.</param>
    /// <param name="metadata">The metadata.</param>
    internal ImageFrame(Configuration configuration, int width, int height, MemoryGroup<TPixel> memorySource, ImageFrameMetadata metadata)
        : base(configuration, width, height, metadata)
    {
        Guard.MustBeGreaterThan(width, 0, nameof(width));
        Guard.MustBeGreaterThan(height, 0, nameof(height));

        this.PixelBuffer = new Buffer2D<TPixel>(memorySource, width, height);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageFrame{TPixel}" /> class.
    /// </summary>
    /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
    /// <param name="source">The source.</param>
    internal ImageFrame(Configuration configuration, ImageFrame<TPixel> source)
        : base(configuration, source.Width, source.Height, source.Metadata.DeepClone())
    {
        Guard.NotNull(configuration, nameof(configuration));
        Guard.NotNull(source, nameof(source));

        this.PixelBuffer = this.Configuration.MemoryAllocator.Allocate2D<TPixel>(
            source.PixelBuffer.Width,
            source.PixelBuffer.Height,
            configuration.PreferContiguousImageBuffers);
        source.PixelBuffer.FastMemoryGroup.CopyTo(this.PixelBuffer.FastMemoryGroup);
    }

    /// <inheritdoc/>
    public Buffer2D<TPixel> PixelBuffer { get; }

    /// <summary>
    /// Gets or sets the pixel at the specified position.
    /// </summary>
    /// <param name="x">The x-coordinate of the pixel. Must be greater than or equal to zero and less than the width of the image.</param>
    /// <param name="y">The y-coordinate of the pixel. Must be greater than or equal to zero and less than the height of the image.</param>
    /// <returns>The <see typeparam="TPixel"/> at the specified position.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the provided (x,y) coordinates are outside the image boundary.</exception>
    public TPixel this[int x, int y]
    {
        [MethodImpl(InliningOptions.ShortMethod)]
        get
        {
            this.VerifyCoords(x, y);
            return this.PixelBuffer.GetElementUnsafe(x, y);
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        set
        {
            this.VerifyCoords(x, y);
            this.PixelBuffer.GetElementUnsafe(x, y) = value;
        }
    }

    /// <summary>
    /// Execute <paramref name="processPixels"/> to process image pixels in a safe and efficient manner.
    /// </summary>
    /// <param name="processPixels">The <see cref="PixelAccessorAction{TPixel}"/> defining the pixel operations.</param>
    public void ProcessPixelRows(PixelAccessorAction<TPixel> processPixels)
    {
        Guard.NotNull(processPixels, nameof(processPixels));

        this.PixelBuffer.FastMemoryGroup.IncreaseRefCounts();

        try
        {
            PixelAccessor<TPixel> accessor = new(this.PixelBuffer);
            processPixels(accessor);
        }
        finally
        {
            this.PixelBuffer.FastMemoryGroup.DecreaseRefCounts();
        }
    }

    /// <summary>
    /// Execute <paramref name="processPixels"/> to process pixels of multiple image frames in a safe and efficient manner.
    /// </summary>
    /// <param name="frame2">The second image frame.</param>
    /// <param name="processPixels">The <see cref="PixelAccessorAction{TPixel, TPixel2}"/> defining the pixel operations.</param>
    /// <typeparam name="TPixel2">The pixel type of the second image frame.</typeparam>
    public void ProcessPixelRows<TPixel2>(
        ImageFrame<TPixel2> frame2,
        PixelAccessorAction<TPixel, TPixel2> processPixels)
        where TPixel2 : unmanaged, IPixel<TPixel2>
    {
        Guard.NotNull(frame2, nameof(frame2));
        Guard.NotNull(processPixels, nameof(processPixels));

        this.PixelBuffer.FastMemoryGroup.IncreaseRefCounts();
        frame2.PixelBuffer.FastMemoryGroup.IncreaseRefCounts();

        try
        {
            PixelAccessor<TPixel> accessor1 = new(this.PixelBuffer);
            PixelAccessor<TPixel2> accessor2 = new(frame2.PixelBuffer);
            processPixels(accessor1, accessor2);
        }
        finally
        {
            frame2.PixelBuffer.FastMemoryGroup.DecreaseRefCounts();
            this.PixelBuffer.FastMemoryGroup.DecreaseRefCounts();
        }
    }

    /// <summary>
    /// Execute <paramref name="processPixels"/> to process pixels of multiple image frames in a safe and efficient manner.
    /// </summary>
    /// <param name="frame2">The second image frame.</param>
    /// <param name="frame3">The third image frame.</param>
    /// <param name="processPixels">The <see cref="PixelAccessorAction{TPixel, TPixel2, TPixel3}"/> defining the pixel operations.</param>
    /// <typeparam name="TPixel2">The pixel type of the second image frame.</typeparam>
    /// <typeparam name="TPixel3">The pixel type of the third image frame.</typeparam>
    public void ProcessPixelRows<TPixel2, TPixel3>(
        ImageFrame<TPixel2> frame2,
        ImageFrame<TPixel3> frame3,
        PixelAccessorAction<TPixel, TPixel2, TPixel3> processPixels)
        where TPixel2 : unmanaged, IPixel<TPixel2>
        where TPixel3 : unmanaged, IPixel<TPixel3>
    {
        Guard.NotNull(frame2, nameof(frame2));
        Guard.NotNull(frame3, nameof(frame3));
        Guard.NotNull(processPixels, nameof(processPixels));

        this.PixelBuffer.FastMemoryGroup.IncreaseRefCounts();
        frame2.PixelBuffer.FastMemoryGroup.IncreaseRefCounts();
        frame3.PixelBuffer.FastMemoryGroup.IncreaseRefCounts();

        try
        {
            PixelAccessor<TPixel> accessor1 = new(this.PixelBuffer);
            PixelAccessor<TPixel2> accessor2 = new(frame2.PixelBuffer);
            PixelAccessor<TPixel3> accessor3 = new(frame3.PixelBuffer);
            processPixels(accessor1, accessor2, accessor3);
        }
        finally
        {
            frame3.PixelBuffer.FastMemoryGroup.DecreaseRefCounts();
            frame2.PixelBuffer.FastMemoryGroup.DecreaseRefCounts();
            this.PixelBuffer.FastMemoryGroup.DecreaseRefCounts();
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
    /// Gets a reference to the pixel at the specified position.
    /// </summary>
    /// <param name="x">The x-coordinate of the pixel. Must be greater than or equal to zero and less than the width of the image.</param>
    /// <param name="y">The y-coordinate of the pixel. Must be greater than or equal to zero and less than the height of the image.</param>
    /// <returns>The <see typeparam="TPixel"/> at the specified position.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ref TPixel GetPixelReference(int x, int y) => ref this.PixelBuffer[x, y];

    /// <summary>
    /// Copies the pixels to a <see cref="Buffer2D{TPixel}"/> of the same size.
    /// </summary>
    /// <param name="target">The target pixel buffer accessor.</param>
    /// <exception cref="ArgumentException">ImageFrame{TPixel}.CopyTo(): target must be of the same size!</exception>
    internal void CopyTo(Buffer2D<TPixel> target)
    {
        if (this.Size != target.Size())
        {
            throw new ArgumentException("ImageFrame<TPixel>.CopyTo(): target must be of the same size!", nameof(target));
        }

        this.PixelBuffer.FastMemoryGroup.CopyTo(target.FastMemoryGroup);
    }

    /// <summary>
    /// Switches the buffers used by the image and the pixel source meaning that the Image will "own" the buffer
    /// from the pixelSource and the pixel source will now own the Image buffer.
    /// </summary>
    /// <param name="source">The pixel source.</param>
    internal void SwapOrCopyPixelsBufferFrom(ImageFrame<TPixel> source)
    {
        Guard.NotNull(source, nameof(source));

        Buffer2D<TPixel>.SwapOrCopyContent(this.PixelBuffer, source.PixelBuffer);
        this.UpdateSize(this.PixelBuffer.Size());
    }

    /// <summary>
    /// Copies the metadata from the source image.
    /// </summary>
    /// <param name="source">The metadata source.</param>
    internal void CopyMetadataFrom(ImageFrame<TPixel> source)
    {
        Guard.NotNull(source, nameof(source));

        this.UpdateMetadata(source.Metadata);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (this.isDisposed)
        {
            return;
        }

        if (disposing)
        {
            this.PixelBuffer.Dispose();
        }

        this.isDisposed = true;
    }

    internal override void CopyPixelsTo<TDestinationPixel>(MemoryGroup<TDestinationPixel> destination)
    {
        if (typeof(TPixel) == typeof(TDestinationPixel))
        {
            this.PixelBuffer.FastMemoryGroup.TransformTo(destination, (s, d) =>
            {
                Span<TPixel> d1 = MemoryMarshal.Cast<TDestinationPixel, TPixel>(d);
                s.CopyTo(d1);
            });
            return;
        }

        this.PixelBuffer.FastMemoryGroup.TransformTo(destination, (s, d)
            => PixelOperations<TPixel>.Instance.To(this.Configuration, s, d));
    }

    /// <inheritdoc/>
    public override string ToString() => $"ImageFrame<{typeof(TPixel).Name}>({this.Width}x{this.Height})";

    /// <summary>
    /// Clones the current instance.
    /// </summary>
    /// <returns>The <see cref="ImageFrame{TPixel}"/></returns>
    internal ImageFrame<TPixel> Clone() => this.Clone(this.Configuration);

    /// <summary>
    /// Clones the current instance.
    /// </summary>
    /// <param name="configuration">The configuration providing initialization code which allows extending the library.</param>
    /// <returns>The <see cref="ImageFrame{TPixel}"/></returns>
    internal ImageFrame<TPixel> Clone(Configuration configuration) => new(configuration, this);

    /// <summary>
    /// Returns a copy of the image frame in the given pixel format.
    /// </summary>
    /// <typeparam name="TPixel2">The pixel format.</typeparam>
    /// <returns>The <see cref="ImageFrame{TPixel2}"/></returns>
    internal ImageFrame<TPixel2>? CloneAs<TPixel2>()
        where TPixel2 : unmanaged, IPixel<TPixel2> => this.CloneAs<TPixel2>(this.Configuration);

    /// <summary>
    /// Returns a copy of the image frame in the given pixel format.
    /// </summary>
    /// <typeparam name="TPixel2">The pixel format.</typeparam>
    /// <param name="configuration">The configuration providing initialization code which allows extending the library.</param>
    /// <returns>The <see cref="ImageFrame{TPixel2}"/></returns>
    internal ImageFrame<TPixel2> CloneAs<TPixel2>(Configuration configuration)
        where TPixel2 : unmanaged, IPixel<TPixel2>
    {
        if (typeof(TPixel2) == typeof(TPixel))
        {
            return (this.Clone(configuration) as ImageFrame<TPixel2>)!;
        }

        ImageFrame<TPixel2> target = new(configuration, this.Width, this.Height, this.Metadata.DeepClone());
        RowIntervalOperation<TPixel2> operation = new(this.PixelBuffer, target.PixelBuffer, configuration);

        ParallelRowIterator.IterateRowIntervals(
            configuration,
            this.Bounds,
            in operation);

        return target;
    }

    /// <summary>
    /// Clears the bitmap.
    /// </summary>
    /// <param name="value">The value to initialize the bitmap with.</param>
    internal void Clear(TPixel value)
    {
        MemoryGroup<TPixel> group = this.PixelBuffer.FastMemoryGroup;

        if (value.Equals(default))
        {
            group.Clear();
        }
        else
        {
            group.Fill(value);
        }
    }

    [MethodImpl(InliningOptions.ShortMethod)]
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

    [MethodImpl(InliningOptions.ColdPath)]
    private static void ThrowArgumentOutOfRangeException(string paramName) => throw new ArgumentOutOfRangeException(paramName);

    /// <summary>
    /// A <see langword="struct"/> implementing the clone logic for <see cref="ImageFrame{TPixel}"/>.
    /// </summary>
    /// <typeparam name="TPixel2">The type of the target pixel format.</typeparam>
    private readonly struct RowIntervalOperation<TPixel2> : IRowIntervalOperation
        where TPixel2 : unmanaged, IPixel<TPixel2>
    {
        private readonly Buffer2D<TPixel> source;
        private readonly Buffer2D<TPixel2> target;
        private readonly Configuration configuration;

        [MethodImpl(InliningOptions.ShortMethod)]
        public RowIntervalOperation(
            Buffer2D<TPixel> source,
            Buffer2D<TPixel2> target,
            Configuration configuration)
        {
            this.source = source;
            this.target = target;
            this.configuration = configuration;
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void Invoke(in RowInterval rows)
        {
            for (int y = rows.Min; y < rows.Max; y++)
            {
                Span<TPixel> sourceRow = this.source.DangerousGetRowSpan(y);
                Span<TPixel2> targetRow = this.target.DangerousGetRowSpan(y);
                PixelOperations<TPixel>.Instance.To(this.configuration, sourceRow, targetRow);
            }
        }
    }
}
