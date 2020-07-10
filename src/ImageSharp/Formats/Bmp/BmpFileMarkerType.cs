// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Bmp
{
    /// <summary>
    /// Indicates which bitmap file marker was read.
    /// </summary>
    public enum BmpFileMarkerType
    {
        /// <summary>
        /// Single-image BMP file that may have been created under Windows or OS/2.
        /// </summary>
        Bitmap,

        /// <summary>
        /// OS/2 Bitmap Array.
        /// </summary>
        BitmapArray,

        /// <summary>
        /// OS/2 Color Icon.
        /// </summary>
        ColorIcon,

        /// <summary>
        /// OS/2 Color Pointer.
        /// </summary>
        ColorPointer,

        /// <summary>
        /// OS/2 Icon.
        /// </summary>
        Icon,

        /// <summary>
        /// OS/2 Pointer.
        /// </summary>
        Pointer
    }
}
