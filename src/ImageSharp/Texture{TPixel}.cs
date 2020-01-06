// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Represents a pixel-specific array of images.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    public sealed class Texture<TPixel> : Texture
        where TPixel : struct, IPixel<TPixel>
    {
        private bool isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="Texture{TPixel}"/> class.
        /// </summary>
        /// <param name="textureType"><see cref="TextureType"/></param>
        public Texture(TextureType textureType)
            : base(textureType)
        {
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (this.isDisposed)
            {
                return;
            }

            if (disposing)
            {
                for (int i = 0; i < this.Images.Length; i++)
                {
                    for (int j = 0; j < this.Images[i].Length; i++)
                    {
                        this.Images[i][j].Dispose();
                    }
                }
            }

            this.isDisposed = true;
        }

        /// <inheritdoc/>
        internal override void EnsureNotDisposed()
        {
            if (this.isDisposed)
            {
                throw new ObjectDisposedException("Trying to execute an operation on a disposed texture.");
            }
        }
    }
}
