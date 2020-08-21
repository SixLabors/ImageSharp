// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.PixelFormats
{
    /// <summary>
    /// Enumerates the various alpha composition modes.
    /// </summary>
    public enum PixelAlphaCompositionMode
    {
        /// <summary>
        /// Returns the destination over the source.
        /// </summary>
        SrcOver = 0,

        /// <summary>
        /// Returns the source colors.
        /// </summary>
        Src,

        /// <summary>
        /// Returns the source over the destination.
        /// </summary>
        SrcAtop,

        /// <summary>
        /// The source where the destination and source overlap.
        /// </summary>
        SrcIn,

        /// <summary>
        /// The destination where the destination and source overlap.
        /// </summary>
        SrcOut,

        /// <summary>
        /// The destination where the source does not overlap it.
        /// </summary>
        Dest,

        /// <summary>
        /// The source where they don't overlap otherwise dest in overlapping parts.
        /// </summary>
        DestAtop,

        /// <summary>
        /// The destination over the source.
        /// </summary>
        DestOver,

        /// <summary>
        /// The destination where the destination and source overlap.
        /// </summary>
        DestIn,

        /// <summary>
        /// The source where the destination and source overlap.
        /// </summary>
        DestOut,

        /// <summary>
        /// The clear.
        /// </summary>
        Clear,

        /// <summary>
        /// Clear where they overlap.
        /// </summary>
        Xor
    }
}
