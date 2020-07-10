// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;

namespace SixLabors.ImageSharp.Formats.Tga
{
    internal static class TgaConstants
    {
        /// <summary>
        /// The list of mimetypes that equate to a targa file.
        /// </summary>
        public static readonly IEnumerable<string> MimeTypes = new[] { "image/x-tga", "image/x-targa" };

        /// <summary>
        /// The list of file extensions that equate to a targa file.
        /// </summary>
        public static readonly IEnumerable<string> FileExtensions = new[] { "tga", "vda", "icb", "vst" };

        /// <summary>
        /// The file header length of a tga image in bytes.
        /// </summary>
        public const int FileHeaderLength = 18;
    }
}
