// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.WebP
{
    internal class WebPImageInfo
    {
        /// <summary>
        /// Gets or sets the bitmap width in pixels (signed integer).
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// Gets or sets the bitmap height in pixels (signed integer).
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// Gets or sets whether this image uses a lossless compression.
        /// </summary>
        public bool IsLossLess { get; set; }

        /// <summary>
        /// The bytes of the image payload.
        /// </summary>
        public uint ImageDataSize { get; set; }
    }
}
