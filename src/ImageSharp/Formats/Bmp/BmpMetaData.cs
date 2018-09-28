// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Bmp
{
    /// <summary>
    /// Provides Bmp specific metadata information for the image.
    /// </summary>
    public class BmpMetaData : IDeepCloneable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BmpMetaData"/> class.
        /// </summary>
        public BmpMetaData()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BmpMetaData"/> class.
        /// </summary>
        /// <param name="other">The metadata to create an instance from.</param>
        private BmpMetaData(BmpMetaData other) => this.BitsPerPixel = other.BitsPerPixel;

        /// <summary>
        /// Gets or sets the number of bits per pixel.
        /// </summary>
        public BmpBitsPerPixel BitsPerPixel { get; set; } = BmpBitsPerPixel.Pixel24;

        /// <inheritdoc/>
        public IDeepCloneable DeepClone() => new BmpMetaData(this);

        // TODO: Colors used once we support encoding palette bmps.
    }
}