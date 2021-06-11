// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;

namespace SixLabors.ImageSharp.Formats.Tiff.Constants
{
    /// <summary>
    /// Defines constants defined in the TIFF specification.
    /// </summary>
    internal static class TiffConstants
    {
        /// <summary>
        /// Byte order markers for indicating little endian encoding.
        /// </summary>
        public const byte ByteOrderLittleEndian = 0x49;

        /// <summary>
        /// Byte order markers for indicating big endian encoding.
        /// </summary>
        public const byte ByteOrderBigEndian = 0x4D;

        /// <summary>
        /// Byte order markers for indicating little endian encoding.
        /// </summary>
        public const ushort ByteOrderLittleEndianShort = 0x4949;

        /// <summary>
        /// Byte order markers for indicating big endian encoding.
        /// </summary>
        public const ushort ByteOrderBigEndianShort = 0x4D4D;

        /// <summary>
        /// Magic number used within the image file header to identify a TIFF format file.
        /// </summary>
        public const ushort HeaderMagicNumber = 42;

        /// <summary>
        /// RowsPerStrip default value, which is effectively infinity.
        /// </summary>
        public const int RowsPerStripInfinity = 2147483647;

        /// <summary>
        /// Size (in bytes) of the TIFF file header.
        /// </summary>
        public const int SizeOfTiffHeader = 8;

        /// <summary>
        /// Size (in bytes) of each individual TIFF IFD entry
        /// </summary>
        public const int SizeOfIfdEntry = 12;

        /// <summary>
        /// Size (in bytes) of the Short and SShort data types
        /// </summary>
        public const int SizeOfShort = 2;

        /// <summary>
        /// Size (in bytes) of the Long and SLong data types
        /// </summary>
        public const int SizeOfLong = 4;

        /// <summary>
        /// Size (in bytes) of the Rational and SRational data types
        /// </summary>
        public const int SizeOfRational = 8;

        /// <summary>
        /// Size (in bytes) of the Float data type
        /// </summary>
        public const int SizeOfFloat = 4;

        /// <summary>
        /// Size (in bytes) of the Double data type
        /// </summary>
        public const int SizeOfDouble = 8;

        /// <summary>
        /// The default strip size is 8k.
        /// </summary>
        public const int DefaultStripSize = 8 * 1024;

        /// <summary>
        /// The bits per sample for 1 bit bicolor images.
        /// </summary>
        public static readonly ushort[] BitsPerSample1Bit = { 1 };

        /// <summary>
        /// The bits per sample for images with a 2 color palette.
        /// </summary>
        public static readonly ushort[] BitsPerSample2Bit = { 2 };

        /// <summary>
        /// The bits per sample for images with a 4 color palette.
        /// </summary>
        public static readonly ushort[] BitsPerSample4Bit = { 4 };

        /// <summary>
        /// The bits per sample for 6 bit gray images.
        /// </summary>
        public static readonly ushort[] BitsPerSample6Bit = { 6 };

        /// <summary>
        /// The bits per sample for 8 bit images.
        /// </summary>
        public static readonly ushort[] BitsPerSample8Bit = { 8 };

        /// <summary>
        /// The bits per sample for 10 bit gray images.
        /// </summary>
        public static readonly ushort[] BitsPerSample10Bit = { 10 };

        /// <summary>
        /// The bits per sample for 12 bit gray images.
        /// </summary>
        public static readonly ushort[] BitsPerSample12Bit = { 12 };

        /// <summary>
        /// The bits per sample for 14 bit gray images.
        /// </summary>
        public static readonly ushort[] BitsPerSample14Bit = { 14 };

        /// <summary>
        /// The bits per sample for 16 bit gray images.
        /// </summary>
        public static readonly ushort[] BitsPerSample16Bit = { 16 };

        /// <summary>
        /// The bits per sample for color images with 2 bits for each color channel.
        /// </summary>
        public static readonly ushort[] BitsPerSampleRgb2Bit = { 2, 2, 2 };

        /// <summary>
        /// The bits per sample for color images with 4 bits for each color channel.
        /// </summary>
        public static readonly ushort[] BitsPerSampleRgb4Bit = { 4, 4, 4 };

        /// <summary>
        /// The bits per sample for color images with 8 bits for each color channel.
        /// </summary>
        public static readonly ushort[] BitsPerSampleRgb8Bit = { 8, 8, 8 };

        /// <summary>
        /// The bits per sample for color images with 10 bits for each color channel.
        /// </summary>
        public static readonly ushort[] BitsPerSampleRgb10Bit = { 10, 10, 10 };

        /// <summary>
        /// The bits per sample for color images with 12 bits for each color channel.
        /// </summary>
        public static readonly ushort[] BitsPerSampleRgb12Bit = { 12, 12, 12 };

        /// <summary>
        /// The bits per sample for color images with 14 bits for each color channel.
        /// </summary>
        public static readonly ushort[] BitsPerSampleRgb14Bit = { 14, 14, 14 };

        /// <summary>
        /// The bits per sample for color images with 14 bits for each color channel.
        /// </summary>
        public static readonly ushort[] BitsPerSampleRgb16Bit = { 16, 16, 16 };

        /// <summary>
        /// The list of mimetypes that equate to a tiff.
        /// </summary>
        public static readonly IEnumerable<string> MimeTypes = new[] { "image/tiff", "image/tiff-fx" };

        /// <summary>
        /// The list of file extensions that equate to a tiff.
        /// </summary>
        public static readonly IEnumerable<string> FileExtensions = new[] { "tiff", "tif" };
    }
}
