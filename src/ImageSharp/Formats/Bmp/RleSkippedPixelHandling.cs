// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Bmp
{
    /// <summary>
    /// Defines possible options, how skipped pixels during decoding of run length encoded bitmaps should be treated.
    /// </summary>
    public enum RleSkippedPixelHandling : int
    {
        /// <summary>
        /// Undefined pixels should be black. This is the default behavior and equal to how System.Drawing handles undefined pixels.
        /// </summary>
        Black = 0,

        /// <summary>
        /// Undefined pixels should be transparent.
        /// </summary>
        Transparent = 1,

        /// <summary>
        /// Undefined pixels should have the first color of the palette.
        /// </summary>
        FirstColorOfPalette = 2
    }
}
