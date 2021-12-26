// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;

namespace SixLabors.ImageSharp.Formats.OpenExr
{
    /// <summary>
    /// Defines constants relating to OpenExr images.
    /// </summary>
    internal static class ExrConstants
    {
        /// <summary>
        /// The list of mimetypes that equate to a OpenExr image.
        /// </summary>
        public static readonly IEnumerable<string> MimeTypes = new[] { "image/x-exr" };

        /// <summary>
        /// The list of file extensions that equate to a OpenExr image.
        /// </summary>
        public static readonly IEnumerable<string> FileExtensions = new[] { "exr" };

        /// <summary>
        /// The magick bytes identifying an OpenExr image.
        /// </summary>
        public static readonly int MagickBytes = 20000630;
    }
}
