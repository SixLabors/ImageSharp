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
        /// Gets or sets a value indicating whether this image uses a lossless compression.
        /// </summary>
        public bool IsLossLess { get; set; }

        /// <summary>
        /// Gets or sets additional features present in a VP8X image.
        /// </summary>
        public WebPFeatures Features { get; set; }

        /// <summary>
        /// Gets or sets the bytes of the image payload.
        /// </summary>
        public uint ImageDataSize { get; set; }

        /// <summary>
        /// Gets or sets Vp8L bitreader. Will be null if its not lossless image.
        /// </summary>
        public Vp8LBitReader Vp9LBitReader { get; set; } = null;
    }
}
