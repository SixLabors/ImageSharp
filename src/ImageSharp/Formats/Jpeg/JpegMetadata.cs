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
        private Block8x8F? lumaQuantTable;
        private Block8x8F? chromaQuantTable;

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
            this.ColorType = other.ColorType;

            this.LuminanceQuantizationTable = other.LuminanceQuantizationTable;
            this.ChromaQuantizationTable = other.ChromaQuantizationTable;
            this.LuminanceQuality = other.LuminanceQuality;
            this.ChrominanceQuality = other.ChrominanceQuality;
        }

        /// <summary>
        /// Gets or sets luminance qunatization table for jpeg image.
        /// </summary>
        internal Block8x8F LuminanceQuantizationTable
        {
            get
            {
                if (this.lumaQuantTable.HasValue)
                {
                    return this.lumaQuantTable.Value;
                }

                return Quantization.ScaleLuminanceTable(this.LuminanceQuality ?? Quantization.DefaultQualityFactor);
            }

            set => this.lumaQuantTable = value;
        }

        /// <summary>
        /// Gets or sets chrominance qunatization table for jpeg image.
        /// </summary>
        internal Block8x8F ChromaQuantizationTable
        {
            get
            {
                if (this.chromaQuantTable.HasValue)
                {
                    return this.chromaQuantTable.Value;
                }

                return Quantization.ScaleChrominanceTable(this.ChrominanceQuality ?? Quantization.DefaultQualityFactor);
            }

            set => this.chromaQuantTable = value;
        }

        /// <summary>
        /// Gets or sets the jpeg luminance quality.
        /// </summary>
        /// <remarks>
        /// This value might not be accurate if it was calculated during jpeg decoding
        /// with non-complient ITU quantization tables.
        /// </remarks>
        internal int? LuminanceQuality { get; set; }

        /// <summary>
        /// Gets or sets the jpeg chrominance quality.
        /// </summary>
        /// <remarks>
        /// This value might not be accurate if it was calculated during jpeg decoding
        /// with non-complient ITU quantization tables.
        /// </remarks>
        internal int? ChrominanceQuality { get; set; }

        /// <summary>
        /// Gets or sets the encoded quality.
        /// </summary>
        /// <remarks>
        /// Note that jpeg image can have different quality for luminance and chrominance components.
        /// This property returns maximum value of luma/chroma qualities.
        /// </remarks>
        public int? Quality
        {
            get
            {
                // Jpeg always has a luminance table thus it must have a luminance quality derived from it
                if (!this.LuminanceQuality.HasValue)
                {
                    return null;
                }

                // Jpeg might not have a chrominance table
                if (!this.ChrominanceQuality.HasValue)
                {
                    return this.LuminanceQuality.Value;
                }

                // Theoretically, luma quality would always be greater or equal to chroma quality
                // But we've already encountered images which can have higher quality of chroma components
                return Math.Max(this.LuminanceQuality.Value, this.ChrominanceQuality.Value);
            }

            set
            {
                this.LuminanceQuality = value;
                this.ChrominanceQuality = value;
            }
        }

        /// <summary>
        /// Gets or sets the color type.
        /// </summary>
        public JpegColorType? ColorType { get; set; }

        /// <inheritdoc/>
        public IDeepCloneable DeepClone() => new JpegMetadata(this);
    }
}
