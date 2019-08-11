// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO;

using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Encapsulates an image, which consists of the pixel data for a graphics image and its attributes.
    /// For the non-generic <see cref="Image"/> type, the pixel type is only known at runtime.
    /// <see cref="Image"/> is always implemented by a pixel-specific <see cref="Image{TPixel}"/> instance.
    /// </summary>
    public abstract partial class Image : IImage, IConfigurable
    {
        private Size size;

        /// <summary>
        /// Initializes a new instance of the <see cref="Image"/> class.
        /// </summary>
        /// <param name="configuration">The <see cref="Configuration"/>.</param>
        /// <param name="pixelType">The <see cref="PixelTypeInfo"/>.</param>
        /// <param name="metadata">The <see cref="ImageMetadata"/>.</param>
        /// <param name="size">The <see cref="size"/>.</param>
        protected Image(Configuration configuration, PixelTypeInfo pixelType, ImageMetadata metadata, Size size)
        {
            this.Configuration = configuration ?? Configuration.Default;
            this.PixelType = pixelType;
            this.size = size;
            this.Metadata = metadata ?? new ImageMetadata();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Image"/> class.
        /// </summary>
        internal Image(
            Configuration configuration,
            PixelTypeInfo pixelType,
            ImageMetadata metadata,
            int width,
            int height)
            : this(configuration, pixelType, metadata, new Size(width, height))
        {
        }

        /// <summary>
        /// Gets the <see cref="Configuration"/>.
        /// </summary>
        protected Configuration Configuration { get; }

        /// <summary>
        /// Gets the <see cref="ImageFrameCollection"/> implementing the public <see cref="Frames"/> property.
        /// </summary>
        protected abstract ImageFrameCollection NonGenericFrameCollection { get; }

        /// <inheritdoc/>
        public PixelTypeInfo PixelType { get; }

        /// <inheritdoc />
        public int Width => this.size.Width;

        /// <inheritdoc />
        public int Height => this.size.Height;

        /// <inheritdoc/>
        public ImageMetadata Metadata { get; }

        /// <summary>
        /// Gets the frames of the image as (non-generic) <see cref="ImageFrameCollection"/>.
        /// </summary>
        public ImageFrameCollection Frames => this.NonGenericFrameCollection;

        /// <summary>
        /// Gets the pixel buffer.
        /// </summary>
        Configuration IConfigurable.Configuration => this.Configuration;

        /// <summary>
        /// Gets a value indicating whether the image instance is disposed.
        /// </summary>
        public bool IsDisposed { get; private set; }

        /// <inheritdoc />
        public void Dispose()
        {
            if (this.IsDisposed)
            {
                return;
            }

            this.IsDisposed = true;
            this.DisposeImpl();
        }

        /// <summary>
        /// Saves the image to the given stream using the given image encoder.
        /// </summary>
        /// <param name="stream">The stream to save the image to.</param>
        /// <param name="encoder">The encoder to save the image with.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the stream or encoder is null.</exception>
        public void Save(Stream stream, IImageEncoder encoder)
        {
            Guard.NotNull(stream, nameof(stream));
            Guard.NotNull(encoder, nameof(encoder));
            this.EnsureNotDisposed();

            EncodeVisitor visitor = new EncodeVisitor(encoder, stream);
            this.AcceptVisitor(visitor);
        }

        /// <summary>
        /// Returns a copy of the image in the given pixel format.
        /// </summary>
        /// <typeparam name="TPixel2">The pixel format.</typeparam>
        /// <returns>The <see cref="Image{TPixel2}"/></returns>
        public Image<TPixel2> CloneAs<TPixel2>()
            where TPixel2 : struct, IPixel<TPixel2> => this.CloneAs<TPixel2>(this.Configuration);

        /// <summary>
        /// Returns a copy of the image in the given pixel format.
        /// </summary>
        /// <typeparam name="TPixel2">The pixel format.</typeparam>
        /// <param name="configuration">The configuration providing initialization code which allows extending the library.</param>
        /// <returns>The <see cref="Image{TPixel2}"/>.</returns>
        public abstract Image<TPixel2> CloneAs<TPixel2>(Configuration configuration)
            where TPixel2 : struct, IPixel<TPixel2>;

        /// <summary>
        /// Accept a <see cref="IImageVisitor"/>.
        /// Implemented by <see cref="Image{TPixel}"/> invoking <see cref="IImageVisitor.Visit{TPixel}"/>
        /// with the pixel type of the image.
        /// </summary>
        internal abstract void AcceptVisitor(IImageVisitor visitor);

        /// <summary>
        /// Update the size of the image after mutation.
        /// </summary>
        /// <param name="size">The <see cref="Size"/>.</param>
        protected void UpdateSize(Size size) => this.size = size;

        /// <summary>
        /// Implements the Dispose logic.
        /// </summary>
        protected abstract void DisposeImpl();

        private class EncodeVisitor : IImageVisitor
        {
            private readonly IImageEncoder encoder;

            private readonly Stream stream;

            public EncodeVisitor(IImageEncoder encoder, Stream stream)
            {
                this.encoder = encoder;
                this.stream = stream;
            }

            public void Visit<TPixel>(Image<TPixel> image)
                where TPixel : struct, IPixel<TPixel>
            {
                this.encoder.Encode(image, this.stream);
            }
        }
    }
}
