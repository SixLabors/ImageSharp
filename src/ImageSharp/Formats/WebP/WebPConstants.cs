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
        /// Signature byte which identifies a VP8L header.
        /// </summary>
        public static byte Vp8LMagicByte = 0x2F;

        /// <summary>
        /// 3 bits reserved for version.
        /// </summary>
        public static int Vp8LVersionBits = 3;

        /// <summary>
        /// Bits for width and height infos of a VPL8 image.
        /// </summary>
        public static int Vp8LImageSizeBits = 14;

        /// <summary>
        /// Maximum number of color cache bits.
        /// </summary>
        public static int MaxColorCacheBits = 11;

        /// <summary>
        /// The maximum number of allowed transforms in a bitstream.
        /// </summary>
        public static int MaxNumberOfTransforms = 4;

        public static int MaxAllowedCodeLength = 15;

        public static int DefaultCodeLength = 8;

        public static int HuffmanCodesPerMetaCode = 5;

        public static int NumLiteralCodes = 256;

        public static int NumLengthCodes = 24;

        public static int NumDistanceCodes = 40;

        public static int NumCodeLengthCodes = 19;

        public static int LengthTableBits = 7;

        public static int kCodeLengthLiterals = 16;

        public static int kCodeLengthRepeatCode = 16;

        public static int[] kCodeLengthExtraBits = { 2, 3, 7 };

        public static int[] kCodeLengthRepeatOffsets = { 3, 3, 11 };

        public static byte[] KCodeLengthCodeOrder = { 17, 18, 0, 1, 2, 3, 4, 5, 16, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 };

        public static int[] kAlphabetSize = {
                                                NumLiteralCodes + NumLengthCodes,
                                                NumLiteralCodes, NumLiteralCodes, NumLiteralCodes,
                                                NumDistanceCodes
                                            };
    }
}
