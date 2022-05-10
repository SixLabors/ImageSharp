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
        /// Gets the encoded quality.
        /// </summary>
        /// <remarks>
        /// Note that jpeg image can have different quality for luminance and chrominance components.
        /// This property returns maximum value of luma/chroma qualities if both are present.
        /// </remarks>
        public int Quality
        {
            get
            {
                if (this.luminanceQuality.HasValue)
                {
                    if (this.chrominanceQuality.HasValue)
                    {
                        return Math.Max(this.luminanceQuality.Value, this.chrominanceQuality.Value);
                    }

                    return this.luminanceQuality.Value;
                }
                else
                {
                    if (this.chrominanceQuality.HasValue)
                    {
                        return this.chrominanceQuality.Value;
                    }

                    return Quantization.DefaultQualityFactor;
                }
            }
        }

        /// <summary>
        /// Gets or sets the color type.
        /// </summary>
        public JpegEncodingColor? ColorType { get; set; }

        /// <inheritdoc/>
        public IDeepCloneable DeepClone() => new JpegMetadata(this);
    }
}
