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
        /// Backing field for <see cref="LuminanceQuality"/>
        /// </summary>
        private int? luminanceQuality;

        /// <summary>
        /// Backing field for <see cref="ChrominanceQuality"/>
        /// </summary>
        private int? chrominanceQuality;

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

            this.luminanceQuality = other.luminanceQuality;
            this.chrominanceQuality = other.chrominanceQuality;
        }

        /// <summary>
        /// Gets or sets the jpeg luminance quality.
        /// </summary>
        /// <remarks>
        /// This value might not be accurate if it was calculated during jpeg decoding
        /// with non-complient ITU quantization tables.
        /// </remarks>
        internal int LuminanceQuality
        {
            get => this.luminanceQuality ?? Quantization.DefaultQualityFactor;
            set => this.luminanceQuality = value;
        }

        /// <summary>
        /// Gets or sets the jpeg chrominance quality.
        /// </summary>
        /// <remarks>
        /// This value might not be accurate if it was calculated during jpeg decoding
        /// with non-complient ITU quantization tables.
        /// </remarks>
        internal int ChrominanceQuality
        {
            get => this.chrominanceQuality ?? Quantization.DefaultQualityFactor;
            set => this.chrominanceQuality = value;
        }

        /// <summary>
        /// Gets or sets the encoded quality.
        /// </summary>
        /// <remarks>
        /// Note that jpeg image can have different quality for luminance and chrominance components.
        /// This property returns maximum value of luma/chroma qualities.
        /// </remarks>
        public int Quality
        {
            get
            {
                // Jpeg always has a luminance table thus it must have a luminance quality derived from it
                if (!this.luminanceQuality.HasValue)
                {
                    return Quantization.DefaultQualityFactor;
                }

                int lumaQuality = this.luminanceQuality.Value;

                // Jpeg might not have a chrominance table - return luminance quality (grayscale images)
                if (!this.chrominanceQuality.HasValue)
                {
                    return lumaQuality;
                }

                int chromaQuality = this.chrominanceQuality.Value;

                // Theoretically, luma quality would always be greater or equal to chroma quality
                // But we've already encountered images which can have higher quality of chroma components
                return Math.Max(lumaQuality, chromaQuality);
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
        public JpegEncodingMode? ColorType { get; set; }

        /// <inheritdoc/>
        public IDeepCloneable DeepClone() => new JpegMetadata(this);
    }
}
