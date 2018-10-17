// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Jpeg
{
    /// <summary>
    /// Provides Jpeg specific metadata information for the image.
    /// </summary>
    public class JpegMetaData : IDeepCloneable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="JpegMetaData"/> class.
        /// </summary>
        public JpegMetaData()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JpegMetaData"/> class.
        /// </summary>
        /// <param name="other">The metadata to create an instance from.</param>
        private JpegMetaData(JpegMetaData other) => this.Quality = other.Quality;

        /// <summary>
        /// Gets or sets the encoded quality.
        /// </summary>
        public int Quality { get; set; } = 75;

        /// <inheritdoc/>
        public IDeepCloneable DeepClone() => new JpegMetaData(this);
    }
}