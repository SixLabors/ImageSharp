// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO;

using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp
{
    internal interface IImageVisitor
    {
        void Visit<TPixel>(Image<TPixel> image)
            where TPixel : struct, IPixel<TPixel>;
    }
    
    public abstract partial class Image : IImage, IConfigurable
    {
        protected readonly Configuration configuration;

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
        Configuration IConfigurable.Configuration => this.configuration;

        protected Image(Configuration configuration, PixelTypeInfo pixelType, ImageMetadata metadata)
        {
            this.configuration = configuration ?? Configuration.Default;
            this.PixelType = pixelType;
            this.Metadata = metadata ?? new ImageMetadata();
        }

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

        class EncodeVisitor : IImageVisitor
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