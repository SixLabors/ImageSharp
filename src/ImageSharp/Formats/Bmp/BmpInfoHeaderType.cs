// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Bmp
{
    /// <summary>
    /// Enum value for the different bitmap info header types. The enum value is the number of bytes for the specific bitmap header.
    /// </summary>
    public enum BmpInfoHeaderType
    {
        /// <summary>
        /// Bitmap Core or BMP Version 2 header (Microsoft Windows 2.x).
        /// </summary>
        WinVersion2 = 12,

        /// <summary>
        /// Short variant of the OS/2 Version 2 bitmap header.
        /// </summary>
        Os2Version2Short = 16,

        /// <summary>
        /// BMP Version 3 header (Microsoft Windows 3.x or Microsoft Windows NT).
        /// </summary>
        WinVersion3 = 40,

        /// <summary>
        /// Adobe variant of the BMP Version 3 header.
        /// </summary>
        AdobeVersion3 = 52,

        /// <summary>
        /// Adobe variant of the BMP Version 3 header with an alpha mask.
        /// </summary>
        AdobeVersion3WithAlpha = 56,

        /// <summary>
        /// BMP Version 2.x header (IBM OS/2 2.x).
        /// </summary>
        Os2Version2 = 64,

        /// <summary>
        /// BMP Version 4 header (Microsoft Windows 95).
        /// </summary>
        WinVersion4 = 108,

        /// <summary>
        /// BMP Version 5 header (Windows NT 5.0, 98 or later).
        /// </summary>
        WinVersion5 = 124,
    }
}
