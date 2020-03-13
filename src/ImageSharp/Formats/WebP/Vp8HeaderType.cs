// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.WebP
{
    /// <summary>
    /// Enum for the different VP8 chunk header types.
    /// </summary>
    public enum Vp8HeaderType
    {
        /// <summary>
        /// Invalid VP8 header.
        /// </summary>
        Invalid = 0,

        /// <summary>
        /// A VP8 header.
        /// </summary>
        Vp8 = 1,

        /// <summary>
        /// VP8 header, signaling the use of VP8L lossless format.
        /// </summary>
        Vp8L = 2,

        /// <summary>
        /// Header for a extended-VP8 chunk.
        /// </summary>
        Vp8X = 3,
    }
}
