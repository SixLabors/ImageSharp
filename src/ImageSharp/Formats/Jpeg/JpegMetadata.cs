// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Formats.Jpeg.Components;

namespace SixLabors.ImageSharp.Formats.Jpeg
{
    /// <summary>
    /// Provides Jpeg specific metadata information for the image.
    /// </summary>
    public class JpegMetadata : IDeepCloneable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="JpegMetadata"/> class.
        /// </summary>
        public JpegMetadata()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JpegMetadata"/> class.
        /// </summary>
        /// <param name="other">The metadata to create an instance from.</param>
        private JpegMetadata(JpegMetadata other)
        {
            this.Quality = other.Quality;
            this.ColorType = other.ColorType;

            this.LumaQuantizationTable = other.LumaQuantizationTable;
            this.ChromaQuantizationTable = other.ChromaQuantizationTable;
            this.LuminanceQuality = other.LuminanceQuality;
            this.ChrominanceQuality = other.ChrominanceQuality;
        }

        /// <summary>
        /// Gets or sets luminance qunatization table derived from jpeg image.
        /// </summary>
        /// <remarks>
        /// Would be null if jpeg was encoded using table from ITU spec
        /// </remarks>
        internal Block8x8? LumaQuantizationTable { get; set; }

        /// <summary>
        /// Gets or sets chrominance qunatization table derived from jpeg image.
        /// </summary>
        /// <remarks>
        /// Would be null if jpeg was encoded using table from ITU spec
        /// </remarks>
        internal Block8x8? ChromaQuantizationTable { get; set; }

        /// <summary>
        /// Gets or sets the jpeg luminance quality.
        /// </summary>
        /// <remarks>
        /// This value might not be accurate if it was calculated during jpeg decoding
        /// with non-complient ITU quantization tables.
        /// </remarks>
        public int LuminanceQuality { get; set; }

        /// <summary>
        /// Gets or sets the jpeg chrominance quality.
        /// </summary>
        /// <remarks>
        /// This value might not be accurate if it was calculated during jpeg decoding
        /// with non-complient ITU quantization tables.
        /// </remarks>
        public int ChrominanceQuality { get; set; }

        /// <summary>
        /// Gets a value indicating whether jpeg luminance data was encoded using ITU complient quantization table.
        /// </summary>
        public bool UsesStandardLuminanceTable => !this.LumaQuantizationTable.HasValue;

        /// <summary>
        /// Gets a value indicating whether jpeg luminance data was encoded using ITU complient quantization table.
        /// </summary>
        public bool UsesStandardChrominanceTable => !this.ChromaQuantizationTable.HasValue;

        /// <summary>
        /// Gets or sets the encoded quality.
        /// </summary>
        public int Quality
        {
            get => (int)Math.Round((this.LuminanceQuality + this.ChrominanceQuality) / 2f);
            set
            {
                this.LuminanceQuality = value;
                this.ChrominanceQuality = value;
            }
        }

        /// <summary>
        /// Gets or sets the encoded quality.
        /// </summary>
        public JpegColorType? ColorType { get; set; }

        /// <inheritdoc/>
        public IDeepCloneable DeepClone() => new JpegMetadata(this);
    }
}
