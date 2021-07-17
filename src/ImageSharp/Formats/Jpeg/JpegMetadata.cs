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
            this.LumaQuality = other.LumaQuality;
            this.ChromaQuality = other.ChromaQuality;
        }

        /// <summary>
        /// Gets or sets the encoded quality.
        /// </summary>
        public int Quality
        {
            get => (int)Math.Round(this.LumaQuality);
            set => this.LumaQuality = value;
        }

        /// <summary>
        /// Gets a value indicating whether jpeg was encoded using ITU section spec K.1 quantization tables
        /// </summary>
        public bool ItuSpecQuantization => !this.lumaQuantizationTable.HasValue && !this.chromaQuantizationTable.HasValue;

        /// <summary>
        /// Gets or sets the encoded quality.
        /// </summary>
        public JpegColorType? ColorType { get; set; }

        /// <inheritdoc/>
        public IDeepCloneable DeepClone() => new JpegMetadata(this);
    }
}
