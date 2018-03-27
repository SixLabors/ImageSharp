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
    }
}