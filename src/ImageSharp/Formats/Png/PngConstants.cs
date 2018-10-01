// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Text;

namespace SixLabors.ImageSharp.Formats.Png
{
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

        public static readonly byte[] HeaderBytes = {
             0x89, // Set the high bit.
             0x50, // P
             0x4E, // N
             0x47, // G
             0x0D, // Line ending CRLF
             0x0A, // Line ending CRLF
             0x1A, // EOF
             0x0A // LF
        };

        /// <summary>
        /// The header bytes as a big endian coded ulong.
        /// </summary>
        public const ulong HeaderValue = 0x89504E470D0A1A0AUL;

        /// <summary>
        /// The dictionary of available color types.
        /// </summary>
        public static readonly Dictionary<PngColorType, byte[]> ColorTypes = new Dictionary<PngColorType, byte[]>()
        {
            [PngColorType.Grayscale] = new byte[] { 1, 2, 4, 8, 16 },
            [PngColorType.Rgb] = new byte[] { 8, 16 },
            [PngColorType.Palette] = new byte[] { 1, 2, 4, 8 },
            [PngColorType.GrayscaleWithAlpha] = new byte[] { 8, 16 },
            [PngColorType.RgbWithAlpha] = new byte[] { 8, 16 }
        };
    }
}