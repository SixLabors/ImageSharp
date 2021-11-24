// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Pbm
{
    /// <summary>
    /// Provides PBM specific metadata information for the image.
    /// </summary>
    public class PbmMetadata : IDeepCloneable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PbmMetadata"/> class.
        /// </summary>
        public PbmMetadata()
        {
            this.MaxPixelValue = this.ColorType == PbmColorType.BlackAndWhite ? 1 : 255;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PbmMetadata"/> class.
        /// </summary>
        /// <param name="other">The metadata to create an instance from.</param>
        private PbmMetadata(PbmMetadata other)
        {
            this.Encoding = other.Encoding;
            this.ColorType = other.ColorType;
            this.MaxPixelValue = other.MaxPixelValue;
        }

        /// <summary>
        /// Gets or sets the encoding of the pixels.
        /// </summary>
        public PbmEncoding Encoding { get; set; } = PbmEncoding.Plain;

        /// <summary>
        /// Gets or sets the color type.
        /// </summary>
        public PbmColorType ColorType { get; set; } = PbmColorType.Grayscale;

        /// <summary>
        /// Gets or sets the maximum pixel value.
        /// </summary>
        public int MaxPixelValue { get; set; }

        /// <inheritdoc/>
        public IDeepCloneable DeepClone() => new PbmMetadata(this);
    }
}
