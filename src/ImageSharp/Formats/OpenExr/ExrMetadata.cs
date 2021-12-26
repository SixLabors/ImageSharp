// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.OpenExr
{
    /// <summary>
    /// Provides OpenExr specific metadata information for the image.
    /// </summary>
    public class ExrMetadata : IDeepCloneable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExrMetadata"/> class.
        /// </summary>
        public ExrMetadata()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExrMetadata"/> class.
        /// </summary>
        /// <param name="other">The metadata to create an instance from.</param>
        private ExrMetadata(ExrMetadata other)
        {
        }

        /// <inheritdoc/>
        public IDeepCloneable DeepClone() => new ExrMetadata(this);
    }
}
