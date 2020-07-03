// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Png
{
    /// <summary>
    /// Provides Png specific metadata information for the image.
    /// </summary>
    public class PngMetadata : IDeepCloneable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PngMetadata"/> class.
        /// </summary>
        public PngMetadata()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PngMetadata"/> class.
        /// </summary>
        /// <param name="other">The metadata to create an instance from.</param>
        private PngMetadata(PngMetadata other)
        {
            this.BitDepth = other.BitDepth;
            this.ColorType = other.ColorType;
            this.Gamma = other.Gamma;
            this.InterlaceMethod = other.InterlaceMethod;
            this.HasTransparency = other.HasTransparency;
            this.TransparentL8 = other.TransparentL8;
            this.TransparentL16 = other.TransparentL16;
            this.TransparentRgb24 = other.TransparentRgb24;
            this.TransparentRgb48 = other.TransparentRgb48;

            for (int i = 0; i < other.TextData.Count; i++)
            {
                this.TextData.Add(other.TextData[i]);
            }
        }

        /// <summary>
        /// Gets or sets the number of bits per sample or per palette index (not per pixel).
        /// Not all values are allowed for all <see cref="ColorType"/> values.
        /// </summary>
        public PngBitDepth? BitDepth { get; set; }

        /// <summary>
        /// Gets or sets the color type.
        /// </summary>
        public PngColorType? ColorType { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this instance should write an Adam7 interlaced image.
        /// </summary>
        public PngInterlaceMode? InterlaceMethod { get; set; } = PngInterlaceMode.None;

        /// <summary>
        /// Gets or sets the gamma value for the image.
        /// </summary>
        public float Gamma { get; set; }

        /// <summary>
        /// Gets or sets the Rgb24 transparent color.
        /// This represents any color in an 8 bit Rgb24 encoded png that should be transparent.
        /// </summary>
        public Rgb24? TransparentRgb24 { get; set; }

        /// <summary>
        /// Gets or sets the Rgb48 transparent color.
        /// This represents any color in a 16 bit Rgb24 encoded png that should be transparent.
        /// </summary>
        public Rgb48? TransparentRgb48 { get; set; }

        /// <summary>
        /// Gets or sets the 8 bit grayscale transparent color.
        /// This represents any color in an 8 bit grayscale encoded png that should be transparent.
        /// </summary>
        public L8? TransparentL8 { get; set; }

        /// <summary>
        /// Gets or sets the 16 bit grayscale transparent color.
        /// This represents any color in a 16 bit grayscale encoded png that should be transparent.
        /// </summary>
        public L16? TransparentL16 { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the image contains a transparency chunk and markers were decoded.
        /// </summary>
        public bool HasTransparency { get; set; }

        /// <summary>
        /// Gets or sets the collection of text data stored within the iTXt, tEXt, and zTXt chunks.
        /// Used for conveying textual information associated with the image.
        /// </summary>
        public IList<PngTextData> TextData { get; set; } = new List<PngTextData>();

        /// <inheritdoc/>
        public IDeepCloneable DeepClone() => new PngMetadata(this);
    }
}
