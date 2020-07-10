// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Bmp
{
    /// <summary>
    /// Provides Bmp specific metadata information for the image.
    /// </summary>
    public class BmpMetadata : IDeepCloneable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BmpMetadata"/> class.
        /// </summary>
        public BmpMetadata()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BmpMetadata"/> class.
        /// </summary>
        /// <param name="other">The metadata to create an instance from.</param>
        private BmpMetadata(BmpMetadata other)
        {
            this.BitsPerPixel = other.BitsPerPixel;
            this.InfoHeaderType = other.InfoHeaderType;
        }

        /// <summary>
        /// Gets or sets the bitmap info header type.
        /// </summary>
        public BmpInfoHeaderType InfoHeaderType { get; set; }

        /// <summary>
        /// Gets or sets the number of bits per pixel.
        /// </summary>
        public BmpBitsPerPixel BitsPerPixel { get; set; } = BmpBitsPerPixel.Pixel24;

        /// <inheritdoc/>
        public IDeepCloneable DeepClone() => new BmpMetadata(this);

        // TODO: Colors used once we support encoding palette bmps.
    }
}