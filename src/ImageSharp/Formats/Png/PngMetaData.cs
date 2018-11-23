// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Png
{
    /// <summary>
    /// Provides Png specific metadata information for the image.
    /// </summary>
    public class PngMetaData : IDeepCloneable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PngMetaData"/> class.
        /// </summary>
        public PngMetaData()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PngMetaData"/> class.
        /// </summary>
        /// <param name="other">The metadata to create an instance from.</param>
        private PngMetaData(PngMetaData other)
        {
            this.BitDepth = other.BitDepth;
            this.ColorType = other.ColorType;
            this.Gamma = other.Gamma;
            this.HasTrans = other.HasTrans;
            this.TransparentGray8 = other.TransparentGray8;
            this.TransparentGray16 = other.TransparentGray16;
            this.TransparentRgb24 = other.TransparentRgb24;
            this.TransparentRgb48 = other.TransparentRgb48;
        }

        /// <summary>
        /// Gets or sets the number of bits per sample or per palette index (not per pixel).
        /// Not all values are allowed for all <see cref="ColorType"/> values.
        /// </summary>
        public PngBitDepth BitDepth { get; set; } = PngBitDepth.Bit8;

        /// <summary>
        /// Gets or sets the color type.
        /// </summary>
        public PngColorType ColorType { get; set; } = PngColorType.RgbWithAlpha;

        /// <summary>
        /// Gets or sets the gamma value for the image.
        /// </summary>
        public float Gamma { get; set; }

        /// <summary>
        /// Gets or sets the Rgb 24 transparent color. This represents any color in an 8 bit Rgb24 encoded png that should be transparent
        /// </summary>
        public Rgb24? TransparentRgb24 { get; set; }

        /// <summary>
        /// Gets or sets the Rgb 48 transparent color. This represents any color in a 16 bit Rgb24 encoded png that should be transparent
        /// </summary>
        public Rgb48? TransparentRgb48 { get; set; }

        /// <summary>
        /// Gets or sets the 8 bit grayscale transparent color. This represents any color in an 8 bit grayscale encoded png that should be transparent
        /// </summary>
        public Gray8? TransparentGray8 { get; set; }

        /// <summary>
        /// Gets or sets the 16 bit grayscale transparent color. This represents any color in a 16 bit grayscale encoded png that should be transparent
        /// </summary>
        public Gray16? TransparentGray16 { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the image has transparency chunk and markers were decoded
        /// </summary>
        public bool HasTrans { get; set; }

        /// <inheritdoc/>
        public IDeepCloneable DeepClone() => new PngMetaData(this);
    }
}