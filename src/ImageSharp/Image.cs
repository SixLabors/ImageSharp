// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO;

using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Encapsulates an image, which consists of the pixel data for a graphics image and its attributes.
    /// For the non-generic <see cref="Image"/> type, the pixel type is only known at runtime.
    /// <see cref="Image"/> is always implemented by a pixel-specific <see cref="Image{TPixel}"/> instance.
    /// </summary>
    public abstract partial class Image : IImage, IConfigurable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Image"/> class.
        /// </summary>
        /// <param name="configuration">The <see cref="Configuration"/>.</param>
        /// <param name="pixelType">The <see cref="PixelTypeInfo"/>.</param>
        /// <param name="metadata">The <see cref="ImageMetadata"/>.</param>
        protected Image(Configuration configuration, PixelTypeInfo pixelType, ImageMetadata metadata)
        {
            this.Configuration = configuration ?? Configuration.Default;
            this.PixelType = pixelType;
            this.Metadata = metadata ?? new ImageMetadata();
        }

        /// <summary>
        /// Gets the <see cref="Configuration"/>.
        /// </summary>
        protected Configuration Configuration { get; }

        /// <inheritdoc/>
        public PixelTypeInfo PixelType { get; }

        /// <inheritdoc />
        public abstract int Width { get; }

        /// <inheritdoc />
        public abstract int Height { get; }

        /// <inheritdoc/>
        public ImageMetadata Metadata { get; }

        /// <summary>
        /// Gets the pixel buffer.
        /// </summary>
        Configuration IConfigurable.Configuration => this.Configuration;

        /// <inheritdoc />
        public abstract void Dispose();

        internal abstract void AcceptVisitor(IImageVisitor visitor);

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

            EncodeVisitor visitor = new EncodeVisitor(encoder, stream);
            this.AcceptVisitor(visitor);
        }

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