// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

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
    }
}