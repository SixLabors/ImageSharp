// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Metadata;

namespace SixLabors.ImageSharp
{
    public abstract partial class Image : IImage
    {
        /// <inheritdoc/>
        public PixelTypeInfo PixelType { get; }

        /// <inheritdoc />
        public abstract int Width { get; }

        /// <inheritdoc />
        public abstract int Height { get; }
        
        /// <inheritdoc/>
        public ImageMetadata Metadata { get; }

        protected Image(PixelTypeInfo pixelType, ImageMetadata metadata)
        {
            this.PixelType = pixelType;
            this.Metadata = metadata ?? new ImageMetadata();
        }

        public abstract void Dispose();
    }
}