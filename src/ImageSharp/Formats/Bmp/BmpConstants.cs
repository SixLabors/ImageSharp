// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;

namespace SixLabors.ImageSharp.Formats.Bmp
{
    /// <summary>
    /// Defines constants relating to BMPs
    /// </summary>
    internal static class BmpConstants
    {
        /// <summary>
        /// The list of mimetypes that equate to a bmp.
        /// </summary>
        public static readonly IEnumerable<string> MimeTypes = new[] { "image/bmp", "image/x-windows-bmp" };

        /// <summary>
        /// The list of file extensions that equate to a bmp.
        /// </summary>
        public static readonly IEnumerable<string> FileExtensions = new[] { "bm", "bmp", "dip" };

        /// <summary>
        /// Valid magic bytes markers identifying a Bitmap file.
        /// </summary>
        internal static class TypeMarkers
        {
            /// <summary>
            /// Single-image BMP file that may have been created under Windows or OS/2.
            /// </summary>
            public const int Bitmap = 19778;

            /// <summary>
            /// OS/2 Bitmap Array.
            /// </summary>
            public const int BitmapArray = 16706;

            /// <summary>
            /// OS/2 Color Icon.
            /// </summary>
            public const int ColorIcon = 18755;

            /// <summary>
            /// OS/2 Color Pointer.
            /// </summary>
            public const int ColorPointer = 20547;

            /// <summary>
            /// OS/2 Icon.
            /// </summary>
            public const int Icon = 17225;

            /// <summary>
            /// OS/2 Pointer.
            /// </summary>
            public const int Pointer = 21584;
        }
    }
}