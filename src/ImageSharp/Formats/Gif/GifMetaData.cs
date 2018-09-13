// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Gif
{
    /// <summary>
    /// Provides Gif specific metadata information for the image.
    /// </summary>
    public class GifMetaData
    {
        /// <summary>
        /// Gets or sets the number of times any animation is repeated.
        /// <remarks>
        /// 0 means to repeat indefinitely, count is set as play n + 1 times
        /// </remarks>
        /// </summary>
        public ushort RepeatCount { get; set; }

        /// <summary>
        /// Gets or sets the color table mode.
        /// </summary>
        public GifColorTableMode ColorTableMode { get; set; }

        /// <summary>
        /// Gets or sets the length of the global color table if present.
        /// </summary>
        public int GlobalColorTableLength { get; set; }
    }
}