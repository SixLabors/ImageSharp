// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;

namespace SixLabors.ImageSharp.Formats.WebP
{
    internal static class WebPConstants
    {
        /// <summary>
        /// The list of file extensions that equate to WebP.
        /// </summary>
        public static readonly IEnumerable<string> FileExtensions = new[] { "webp" };

        /// <summary>
        /// The list of mimetypes that equate to a jpeg.
        /// </summary>
        public static readonly IEnumerable<string> MimeTypes = new[] { "image/webp", };

        /// <summary>
        /// Signature which identifies a VP8 header.
        /// </summary>
        public static readonly byte[] Vp8MagicBytes =
        {
            0x9D,
            0x01,
            0x2A
        };

        /// <summary>
        /// Signature byte which identifies a VP8L header.
        /// </summary>
        public static byte Vp8LMagicByte = 0x2F;

        /// <summary>
        /// Bits for width and height infos of a VPL8 image.
        /// </summary>
        public static int Vp8LImageSizeBits = 14;

        /// <summary>
        /// The header bytes identifying RIFF file.
        /// </summary>
        public static readonly byte[] RiffFourCc =
        {
            0x52, // R
            0x49, // I
            0x46, // F
            0x46 // F
        };

        /// <summary>
        /// The header bytes identifying a WebP.
        /// </summary>
        public static readonly byte[] WebPHeader =
        {
            0x57, // W
            0x45, // E
            0x42, // B
            0x50 // P
        };
    }
}
