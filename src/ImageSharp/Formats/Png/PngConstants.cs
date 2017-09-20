// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Png
{
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Defines png constants defined in the specification.
    /// </summary>
    internal static class PngConstants
    {
        /// <summary>
        /// The default encoding for text metadata.
        /// </summary>
        public static readonly Encoding DefaultEncoding = Encoding.GetEncoding("ASCII");

        /// <summary>
        /// The list of mimetypes that equate to a png.
        /// </summary>
        public static readonly IEnumerable<string> MimeTypes = new[] { "image/png" };

        /// <summary>
        /// The list of file extensions that equate to a png.
        /// </summary>
        public static readonly IEnumerable<string> FileExtensions = new[] { "png" };
    }
}