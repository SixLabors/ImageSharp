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
        /// Luminance qunatization table derived from jpeg image.
        /// </summary>
        /// <remarks>
        /// Would be null if jpeg was encoded using table from ITU spec
        /// </remarks>
        internal Block8x8? lumaQuantizationTable;

        /// <summary>
        /// Luminance qunatization table derived from jpeg image.
        /// </summary>
        /// <remarks>
        /// Would be null if jpeg was encoded using table from ITU spec
        /// </remarks>
        internal Block8x8? chromaQuantizationTable;

        internal double LumaQuality;

        internal double ChromaQuality;

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
            this.lumaQuantizationTable = other.lumaQuantizationTable;
            this.chromaQuantizationTable = other.chromaQuantizationTable;
        }

        /// <summary>
        /// Gets or sets the encoded quality.
        /// </summary>
        public int Quality
        {
            get => (int)Math.Round((this.LumaQuality + this.ChromaQuality) / 2f);
            set
            {
                double halfValue = value / 2.0;
                this.LumaQuality = halfValue;
                this.ChromaQuality = halfValue;
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
