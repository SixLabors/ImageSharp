// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Tga
{
    /// <summary>
    /// Provides TGA specific metadata information for the image.
    /// </summary>
    public class TgaMetadata : IDeepCloneable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TgaMetadata"/> class.
        /// </summary>
        public TgaMetadata()
        {
        }

        /// <inheritdoc/>
        public IDeepCloneable DeepClone() => throw new System.NotImplementedException();
    }
}
