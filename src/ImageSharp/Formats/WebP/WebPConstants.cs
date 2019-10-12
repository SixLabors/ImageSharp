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
        /// The header bytes identifying RIFF file.
        /// </summary>
        public static readonly byte[] FourCcBytes =
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

        /// <summary>
        /// Header signaling the use of VP8 video format.
        /// </summary>
        public static readonly byte[] Vp8Header =
        {
            0x56, // V
            0x50, // P
            0x38, // 8
            0x20, // Space
        };

        /// <summary>
        /// Header for a extended-VP8 chunk.
        /// </summary>
        public static readonly byte[] Vp8XHeader =
        {
            0x56, // V
            0x50, // P
            0x38, // 8
            0x88, // X
        };

        public static readonly byte LossLessFlag = 0x4C; // L

        /// <summary>
        /// VP8 header, signaling the use of VP8L lossless format.
        /// </summary>
        public static readonly byte[] Vp8LHeader =
        {
            0x56, // V
            0x50, // P
            0x38, // 8
            LossLessFlag // L
        };

        public static readonly byte[] AlphaHeader =
        {
            0x41, // A
            0x4C, // L
            0x50, // P
            0x48, // H
        };
    }
}
