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
        /// Default JPEG quality for both luminance and chominance tables.
        /// </summary>
        private const int DefaultQualityValue = 75;

        private Block8x8F? lumaQuantTable;
        private Block8x8F? chromaQuantTable;

        private int? lumaQuality;
        private int? chromaQuality;

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

                return Quantization.ScaleLuminanceTable(this.LuminanceQuality);
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

                return Quantization.ScaleChrominanceTable(this.ChrominanceQuality);
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
        public int LuminanceQuality
        {
            get => this.lumaQuality ?? DefaultQualityValue;
            set => this.lumaQuality = value;
        }

        /// <summary>
        /// Gets or sets the jpeg chrominance quality.
        /// </summary>
        /// <remarks>
        /// This value might not be accurate if it was calculated during jpeg decoding
        /// with non-complient ITU quantization tables.
        /// </remarks>
        public int ChrominanceQuality
        {
            get => this.chromaQuality ?? DefaultQualityValue;
            set => this.chromaQuality = value;
        }

        /// <summary>
        /// Gets or sets the encoded quality.
        /// </summary>
        /// <remarks>
        /// Note that jpeg image can have different quality for luminance and chrominance components.
        /// This property return average for both qualities and sets both qualities to the given value.
        /// </remarks>
        public int Quality
        {
            get
            {
                int lumaQuality = this.lumaQuality ?? DefaultQualityValue;
                int chromaQuality = this.chromaQuality ?? lumaQuality;
                return (int)Math.Round((lumaQuality + chromaQuality) / 2f);
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
