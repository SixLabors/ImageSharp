// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Tga
{
    /// <summary>
    /// Provides TGA specific metadata information for the image.
    /// </summary>
    public class TgaMetadata : IDeepCloneable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TgaMetadata"/> class.
        /// </summary>
        public TgaMetadata()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TgaMetadata"/> class.
        /// </summary>
        /// <param name="other">The metadata to create an instance from.</param>
        private TgaMetadata(TgaMetadata other)
        {
            this.BitsPerPixel = other.BitsPerPixel;
        }

        /// <summary>
        /// Gets or sets the number of bits per pixel.
        /// </summary>
        public TgaBitsPerPixel BitsPerPixel { get; set; } = TgaBitsPerPixel.Pixel24;

        /// <summary>
        /// Gets or sets the the number of alpha bits per pixel.
        /// </summary>
        public byte AlphaChannelBits { get; set; } = 0;

        /// <inheritdoc/>
        public IDeepCloneable DeepClone() => new TgaMetadata(this);
    }
}
