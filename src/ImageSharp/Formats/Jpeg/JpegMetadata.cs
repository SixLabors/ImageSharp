// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

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
            this.ColorType = other.ColorType;

            this.LuminanceQuality = other.LuminanceQuality;
            this.ChrominanceQuality = other.ChrominanceQuality;
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
                if (this.LuminanceQuality.HasValue)
                {
                    if (this.ChrominanceQuality.HasValue)
                    {
                        return Math.Max(this.LuminanceQuality.Value, this.ChrominanceQuality.Value);
                    }

                    return this.LuminanceQuality.Value;
                }
                else
                {
                    if (this.ChrominanceQuality.HasValue)
                    {
                        return this.ChrominanceQuality.Value;
                    }

                    return Quantization.DefaultQualityFactor;
                }
            }
        }

        /// <summary>
        /// Gets the color type.
        /// </summary>
        public JpegEncodingColor? ColorType { get; internal set; }

        /// <summary>
        /// Gets the component encoding mode.
        /// </summary>
        /// <remarks>
        /// Interleaved encoding mode encodes all color components in a single scan.
        /// Non-interleaved encoding mode encodes each color component in a separate scan.
        /// </remarks>
        public bool? Interleaved { get; internal set; }

        /// <summary>
        /// Gets the scan encoding mode.
        /// </summary>
        /// <remarks>
        /// Progressive jpeg images encode component data across multiple scans.
        /// </remarks>
        public bool? Progressive { get; internal set; }

        /// <inheritdoc/>
        public IDeepCloneable DeepClone() => new JpegMetadata(this);
    }
}
