// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Memory;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Represents a pixel-agnostic image frame containing all pixel data and <see cref="ImageFrameMetadata"/>.
    /// In case of animated formats like gif, it contains the single frame in a animation.
    /// In all other cases it is the only frame of the image.
    /// </summary>
    public abstract partial class ImageFrame : IDisposable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ImageFrame"/> class.
        /// </summary>
        /// <param name="configuration">The <see cref="Configuration"/>.</param>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        /// <param name="metadata">The <see cref="ImageFrameMetadata"/>.</param>
        protected ImageFrame(Configuration configuration, int width, int height, ImageFrameMetadata metadata)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.NotNull(metadata, nameof(metadata));

            this.Configuration = configuration;
            this.MemoryAllocator = configuration.MemoryAllocator;
            this.Width = width;
            this.Height = height;
            this.Metadata = metadata;
        }

        /// <summary>
        /// Gets the <see cref="MemoryAllocator" /> to use for buffer allocations.
        /// </summary>
        public MemoryAllocator MemoryAllocator { get; }

        /// <summary>
        /// Gets the <see cref="Configuration"/> instance associated with this <see cref="ImageFrame{TPixel}"/>.
        /// </summary>
        internal Configuration Configuration { get; }

        /// <summary>
        /// Gets the width.
        /// </summary>
        public int Width { get; private set; }

        /// <summary>
        /// Gets the height.
        /// </summary>
        public int Height { get; private set; }

        /// <summary>
        /// Gets the metadata of the frame.
        /// </summary>
        public ImageFrameMetadata Metadata { get; }

        /// <summary>
        /// Gets the size of the frame.
        /// </summary>
        /// <returns>The <see cref="Size"/></returns>
        public Size Size() => new Size(this.Width, this.Height);

        /// <summary>
        /// Gets the bounds of the frame.
        /// </summary>
        /// <returns>The <see cref="Rectangle"/></returns>
        public Rectangle Bounds() => new Rectangle(0, 0, this.Width, this.Height);

        /// <inheritdoc />
        public abstract void Dispose();

        internal abstract void CopyPixelsTo<TDestinationPixel>(Span<TDestinationPixel> destination)
            where TDestinationPixel : struct, IPixel<TDestinationPixel>;

        /// <summary>
        /// Updates the size of the image frame.
        /// </summary>
        internal void UpdateSize(Size size)
        {
            this.Width = size.Width;
            this.Height = size.Height;
        }
    }
}
