// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp;

/// <summary>
/// Encapsulates an image, which consists of the pixel data for a graphics image and its attributes.
/// For the non-generic <see cref="Image"/> type, the pixel type is only known at runtime.
/// <see cref="Image"/> is always implemented by a pixel-specific <see cref="Image{TPixel}"/> instance.
/// </summary>
public abstract partial class Image : IDisposable, IConfigurationProvider
{
    private bool isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="Image"/> class.
    /// </summary>
    /// <param name="configuration">The global configuration..</param>
    /// <param name="pixelType">The pixel type information.</param>
    /// <param name="metadata">The image metadata.</param>
    /// <param name="size">The size in px units.</param>
    protected Image(Configuration configuration, PixelTypeInfo pixelType, ImageMetadata metadata, Size size)
    {
        this.Configuration = configuration;
        this.PixelType = pixelType;
        this.Size = size;
        this.Metadata = metadata;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Image"/> class.
    /// </summary>
    /// <param name="configuration">The global configuration.</param>
    /// <param name="pixelType">The <see cref="PixelTypeInfo"/>.</param>
    /// <param name="metadata">The <see cref="ImageMetadata"/>.</param>
    /// <param name="width">The width in px units.</param>
    /// <param name="height">The height in px units.</param>
    internal Image(
        Configuration configuration,
        PixelTypeInfo pixelType,
        ImageMetadata metadata,
        int width,
        int height)
        : this(configuration, pixelType, metadata, new(width, height))
    {
    }

    /// <inheritdoc/>
    public Configuration Configuration { get; }

    /// <summary>
    /// Gets information about the image pixels.
    /// </summary>
    public PixelTypeInfo PixelType { get; }

    /// <summary>
    /// Gets the image width in px units.
    /// </summary>
    public int Width => this.Size.Width;

    /// <summary>
    /// Gets the image height in px units.
    /// </summary>
    public int Height => this.Size.Height;

    /// <summary>
    /// Gets any metadata associated with the image.
    /// </summary>
    public ImageMetadata Metadata { get; private set; }

    /// <summary>
    /// Gets the size of the image in px units.
    /// </summary>
    public Size Size { get; private set; }

    /// <summary>
    /// Gets the bounds of the image.
    /// </summary>
    public Rectangle Bounds => new(0, 0, this.Width, this.Height);

    /// <summary>
    /// Gets the <see cref="ImageFrameCollection"/> implementing the public <see cref="Frames"/> property.
    /// </summary>
    protected abstract ImageFrameCollection NonGenericFrameCollection { get; }

    /// <summary>
    /// Gets the frames of the image as (non-generic) <see cref="ImageFrameCollection"/>.
    /// </summary>
    public ImageFrameCollection Frames => this.NonGenericFrameCollection;

    /// <inheritdoc />
    public void Dispose()
    {
        if (this.isDisposed)
        {
            return;
        }

        this.Dispose(true);
        GC.SuppressFinalize(this);

        this.isDisposed = true;
    }

    /// <summary>
    /// Saves the image to the given stream using the given image encoder.
    /// </summary>
    /// <param name="stream">The stream to save the image to.</param>
    /// <param name="encoder">The encoder to save the image with.</param>
    /// <exception cref="ArgumentNullException">Thrown if the stream or encoder is null.</exception>
    public void Save(Stream stream, IImageEncoder encoder)
    {
        Guard.NotNull(stream, nameof(stream));
        Guard.NotNull(encoder, nameof(encoder));
        this.EnsureNotDisposed();

        this.AcceptVisitor(new EncodeVisitor(encoder, stream));
    }

    /// <summary>
    /// Saves the image to the given stream using the given image encoder.
    /// </summary>
    /// <param name="stream">The stream to save the image to.</param>
    /// <param name="encoder">The encoder to save the image with.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <exception cref="ArgumentNullException">Thrown if the stream or encoder is null.</exception>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public Task SaveAsync(Stream stream, IImageEncoder encoder, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(stream, nameof(stream));
        Guard.NotNull(encoder, nameof(encoder));
        this.EnsureNotDisposed();

        return this.AcceptVisitorAsync(new EncodeVisitor(encoder, stream), cancellationToken);
    }

    /// <summary>
    /// Returns a copy of the image in the given pixel format.
    /// </summary>
    /// <typeparam name="TPixel2">The pixel format.</typeparam>
    /// <returns>The <see cref="Image{TPixel2}"/></returns>
    public Image<TPixel2> CloneAs<TPixel2>()
        where TPixel2 : unmanaged, IPixel<TPixel2> => this.CloneAs<TPixel2>(this.Configuration);

    /// <summary>
    /// Returns a copy of the image in the given pixel format.
    /// </summary>
    /// <typeparam name="TPixel2">The pixel format.</typeparam>
    /// <param name="configuration">The configuration providing initialization code which allows extending the library.</param>
    /// <returns>The <see cref="Image{TPixel2}"/>.</returns>
    public abstract Image<TPixel2> CloneAs<TPixel2>(Configuration configuration)
        where TPixel2 : unmanaged, IPixel<TPixel2>;

    /// <summary>
    /// Synchronizes any embedded metadata profiles with the current image properties.
    /// </summary>
    public void SynchronizeMetadata()
    {
        this.Metadata.SynchronizeProfiles();
        foreach (ImageFrame frame in this.Frames)
        {
            frame.Metadata.SynchronizeProfiles();
        }
    }

    /// <summary>
    /// Synchronizes any embedded metadata profiles with the current image properties.
    /// </summary>
    /// <param name="action">A synchronization action to run in addition to the default process.</param>
    public void SynchronizeMetadata(Action<Image> action)
    {
        this.SynchronizeMetadata();
        action(this);
    }

    /// <summary>
    /// Update the size of the image after mutation.
    /// </summary>
    /// <param name="size">The <see cref="Size"/>.</param>
    protected void UpdateSize(Size size) => this.Size = size;

    /// <summary>
    /// Updates the metadata of the image after mutation.
    /// </summary>
    /// <param name="metadata">The <see cref="Metadata"/>.</param>
    protected void UpdateMetadata(ImageMetadata metadata) => this.Metadata = metadata;

    /// <summary>
    /// Disposes the object and frees resources for the Garbage Collector.
    /// </summary>
    /// <param name="disposing">Whether to dispose of managed and unmanaged objects.</param>
    protected abstract void Dispose(bool disposing);

    /// <summary>
    /// Throws <see cref="ObjectDisposedException"/> if the image is disposed.
    /// </summary>
    internal void EnsureNotDisposed()
    {
        if (this.isDisposed)
        {
            ThrowObjectDisposedException(this.GetType());
        }
    }

    /// <summary>
    /// Accepts a <see cref="IImageVisitor"/>.
    /// Implemented by <see cref="Image{TPixel}"/> invoking <see cref="IImageVisitor.Visit{TPixel}"/>
    /// with the pixel type of the image.
    /// </summary>
    /// <param name="visitor">The visitor.</param>
    internal abstract void Accept(IImageVisitor visitor);

    /// <summary>
    /// Accepts a <see cref="IImageVisitor"/>.
    /// Implemented by <see cref="Image{TPixel}"/> invoking <see cref="IImageVisitor.Visit{TPixel}"/>
    /// with the pixel type of the image.
    /// </summary>
    /// <param name="visitor">The visitor.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    internal abstract Task AcceptAsync(IImageVisitorAsync visitor, CancellationToken cancellationToken);

    [MethodImpl(InliningOptions.ColdPath)]
    private static void ThrowObjectDisposedException(Type type) => throw new ObjectDisposedException(type.Name);

    private class EncodeVisitor : IImageVisitor, IImageVisitorAsync
    {
        private readonly IImageEncoder encoder;

        private readonly Stream stream;

        public EncodeVisitor(IImageEncoder encoder, Stream stream)
        {
            this.encoder = encoder;
            this.stream = stream;
        }

        public void Visit<TPixel>(Image<TPixel> image)
            where TPixel : unmanaged, IPixel<TPixel> => this.encoder.Encode(image, this.stream);

        public Task VisitAsync<TPixel>(Image<TPixel> image, CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel> => this.encoder.EncodeAsync(image, this.stream, cancellationToken);
    }
}
