// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;

namespace SixLabors.ImageSharp.Formats.Tiff
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
        /// The list of mimetypes that equate to a tiff.
        /// </summary>
        public static readonly IEnumerable<string> MimeTypes = new[] { "image/tiff", "image/tiff-fx" };

        /// <summary>
        /// The list of file extensions that equate to a tiff.
        /// </summary>
        public static readonly IEnumerable<string> FileExtensions = new[] { "tiff", "tif" };
    }
}
