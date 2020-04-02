// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace SixLabors.ImageSharp.Formats.Png
{
    /// <summary>
    /// The options structure for the <see cref="PngEncoderCore"/>.
    /// </summary>
    internal class PngEncoderOptions : IPngEncoderOptions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PngEncoderOptions"/> class.
        /// </summary>
        /// <param name="source">The source.</param>
        public PngEncoderOptions(IPngEncoderOptions source)
        {
            this.BitDepth = source.BitDepth;
            this.ColorType = source.ColorType;

            // Specification recommends default filter method None for paletted images and Paeth for others.
            this.FilterMethod = source.FilterMethod ?? (source.ColorType == PngColorType.Palette
                ? PngFilterMethod.None
                : PngFilterMethod.Paeth);
            this.CompressionLevel = source.CompressionLevel;
            this.TextCompressionThreshold = source.TextCompressionThreshold;
            this.Gamma = source.Gamma;
            this.Quantizer = source.Quantizer;
            this.Threshold = source.Threshold;
            this.InterlaceMethod = source.InterlaceMethod;
        }

        /// <summary>
        /// Gets or sets the number of bits per sample or per palette index (not per pixel).
        /// Not all values are allowed for all <see cref="P:SixLabors.ImageSharp.Formats.Png.IPngEncoderOptions.ColorType" /> values.
        /// </summary>
        public PngBitDepth? BitDepth { get; set; }

        /// <summary>
        /// Gets or sets the color type.
        /// </summary>
        public PngColorType? ColorType { get; set; }

        /// <summary>
        /// Gets the filter method.
        /// </summary>
        public PngFilterMethod? FilterMethod { get; }

        /// <summary>
        /// Gets the compression level 1-9.
        /// <remarks>Defaults to 6.</remarks>
        /// </summary>
        public int CompressionLevel { get; }

        /// <inheritdoc/>
        public int TextCompressionThreshold { get; }

        /// <summary>
        /// Gets or sets the gamma value, that will be written the image.
        /// </summary>
        /// <value>
        /// The gamma value of the image.
        /// </value>
        public float? Gamma { get; set; }

        /// <summary>
        /// Gets or sets the quantizer for reducing the color count.
        /// </summary>
        public IQuantizer Quantizer { get; set; }

        /// <summary>
        /// Gets the transparency threshold.
        /// </summary>
        public byte Threshold { get; }

        /// <summary>
        /// Gets or sets a value indicating whether this instance should write an Adam7 interlaced image.
        /// </summary>
        public PngInterlaceMode? InterlaceMethod { get; set; }
    }
}
