// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

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
        private JpegMetadata(JpegMetadata other) => this.Quality = other.Quality;

        /// <summary>
        /// Gets or sets the encoded quality.
        /// </summary>
        public int Quality { get; set; } = 75;

        /// <inheritdoc/>
        public IDeepCloneable DeepClone() => new JpegMetadata(this);
    }
}