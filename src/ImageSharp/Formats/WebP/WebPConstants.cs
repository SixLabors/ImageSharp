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
            0x58, // X
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

        /// <summary>
        /// Chunk contains information about the alpha channel.
        /// </summary>
        public static readonly byte[] AlphaHeader =
        {
            0x41, // A
            0x4C, // L
            0x50, // P
            0x48, // H
        };

        /// <summary>
        /// Chunk which contains a color profile.
        /// </summary>
        public static readonly byte[] IccpHeader =
        {
            0x49, // I
            0x43, // C
            0x43, // C
            0x50, // P
        };

        /// <summary>
        /// Chunk which contains EXIF metadata about the image.
        /// </summary>
        public static readonly byte[] ExifHeader =
        {
            0x45, // E
            0x58, // X
            0x49, // I
            0x46, // F
        };

        /// <summary>
        /// Chunk contains XMP metadata about the image.
        /// </summary>
        public static readonly byte[] XmpHeader =
        {
            0x58, // X
            0x4D, // M
            0x50, // P
            0x20, // Space
        };

        /// <summary>
        /// For an animated image, this chunk contains the global parameters of the animation.
        /// </summary>
        public static readonly byte[] AnimationParameterHeader =
        {
            0x41, // A
            0x4E, // N
            0x49, // I
            0x4D, // M
        };

        /// <summary>
        /// For animated images, this chunk contains information about a single frame. If the Animation flag is not set, then this chunk SHOULD NOT be present.
        /// </summary>
        public static readonly byte[] AnimationHeader =
        {
            0x41, // A
            0x4E, // N
            0x4D, // M
            0x46, // F
        };
    }
}
