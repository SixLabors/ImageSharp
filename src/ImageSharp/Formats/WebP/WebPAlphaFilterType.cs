// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.WebP
{
    /// <summary>
    /// Enum for the different alpha filter types.
    /// </summary>
    internal enum WebPAlphaFilterType
    {
        /// <summary>
        /// No filtering.
        /// </summary>
        None = 0,

        /// <summary>
        /// Horizontal filter.
        /// </summary>
        Horizontal = 1,

        /// <summary>
        /// Vertical filter.
        /// </summary>
        Vertical = 2,

        /// <summary>
        /// Gradient filter.
        /// </summary>
        Gradient = 3,
    }
}
