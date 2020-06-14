// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Represents a pixel-agnostic image frame containing all pixel data and <see cref="ImageFrameMetadata"/>.
    /// In case of animated formats like gif, it contains the single frame in a animation.
    /// In all other cases it is the only frame of the image.
    /// </summary>
    public abstract partial class ImageFrame : IConfigurationProvider, IDisposable
    {
        private readonly Configuration configuration;

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageFrame"/> class.
        /// </summary>
        /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
        /// <param name="width">The frame width.</param>
        /// <param name="height">The frame height.</param>
        /// <param name="metadata">The <see cref="ImageFrameMetadata"/>.</param>
        protected ImageFrame(Configuration configuration, int width, int height, ImageFrameMetadata metadata)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.NotNull(metadata, nameof(metadata));

            this.configuration = configuration ?? Configuration.Default;
            this.Width = width;
            this.Height = height;
            this.Metadata = metadata;
        }

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

        /// <inheritdoc/>
        Configuration IConfigurationProvider.Configuration => this.configuration;

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
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the object and frees resources for the Garbage Collector.
        /// </summary>
        /// <param name="disposing">Whether to dispose of managed and unmanaged objects.</param>
        protected abstract void Dispose(bool disposing);

        internal abstract void CopyPixelsTo<TDestinationPixel>(MemoryGroup<TDestinationPixel> destination)
            where TDestinationPixel : unmanaged, IPixel<TDestinationPixel>;

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
