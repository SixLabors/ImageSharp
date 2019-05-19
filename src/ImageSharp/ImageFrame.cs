// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

using SixLabors.ImageSharp.Metadata;
using SixLabors.Memory;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp
{
    public abstract partial class ImageFrame : IDisposable
    {
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
        public int Width { get; }

        /// <summary>
        /// Gets the height.
        /// </summary>
        public int Height { get; }

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

        public abstract void Dispose();
    }
}