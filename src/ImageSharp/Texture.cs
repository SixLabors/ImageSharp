// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Encapsulates an array of images, which consists of the pixel data for a graphics image and its attributes.
    /// </summary>
    public partial class Texture : IDisposable
    {
        private bool isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="Texture"/> class.
        /// </summary>
        /// <param name="textureType">The <see cref="TextureType"/>.</param>
        public Texture(TextureType textureType)
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
        protected void Dispose(bool disposing)
        {
            if (this.isDisposed)
            {
                return;
            }

            if (disposing)
            {
                for (int i = 0; i < this.Images.Length; i++)
                {
                    for (int j = 0; j < this.Images[i].Length; j++)
                    {
                        this.Images[i][j].Dispose();
                    }
                }
            }

            this.isDisposed = true;
        }

        /// <summary>
        /// Throws <see cref="ObjectDisposedException"/> if the image is disposed.
        /// </summary>
        internal void EnsureNotDisposed()
        {
            if (this.isDisposed)
            {
                throw new ObjectDisposedException("Trying to execute an operation on a disposed texture.");
            }
        }
    }
}
