// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Encapsulates an array of images, which consists of the pixel data for a graphics image and its attributes.
    /// For the non-generic <see cref="Texture"/> type, the pixel type is only known at runtime.
    /// <see cref="Texture"/> is always implemented by a pixel-specific <see cref="Texture{TPixel}"/> instance.
    /// </summary>
    public abstract partial class Texture : IDisposable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Texture"/> class.
        /// </summary>
        /// <param name="textureType">The <see cref="TextureType"/>.</param>
        protected Texture(TextureType textureType)
        {
            this.TextureType = textureType;
        }

        /// <summary>
        /// Gets or sets array of images
        /// </summary>
        public Image[][] Images { get; set; }

        /// <summary>
        /// Gets type of texture.
        /// </summary>
        public TextureType TextureType { get; }

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

        /// <summary>
        /// Throws <see cref="ObjectDisposedException"/> if the image is disposed.
        /// </summary>
        internal abstract void EnsureNotDisposed();
    }
}
