// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Jpeg
{
    /// <summary>
    /// Provides Jpeg specific metadata information for the image.
    /// </summary>
    public class JpegMetaData
    {
        /// <summary>
        /// Gets or sets the encoded quality.
        /// </summary>
        public int Quality { get; set; } = 75;
    }
}