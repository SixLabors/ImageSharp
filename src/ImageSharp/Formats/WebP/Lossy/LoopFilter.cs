// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Webp.Lossy
{
    /// <summary>
    /// Enum for the different loop filters used. VP8 supports two types of loop filters.
    /// </summary>
    internal enum LoopFilter
    {
        /// <summary>
        /// No filter is used.
        /// </summary>
        None = 0,

        /// <summary>
        /// Simple loop filter.
        /// </summary>
        Simple = 1,

        /// <summary>
        /// Complex loop filter.
        /// </summary>
        Complex = 2,
    }
}
