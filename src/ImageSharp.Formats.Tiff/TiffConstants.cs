// <copyright file="TiffConstants.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    using System.Text;

    /// <summary>
    /// Defines constants defined in the TIFF specification.
    /// </summary>
    internal static class GifConstants
    {
        /// <summary>
        /// Byte order markers for indicating little endian encoding.
        /// </summary>
        public const ushort ByteOrderLittleEndian = 0x4949;

        /// <summary>
        /// Byte order markers for indicating big endian encoding.
        /// </summary>
        public const ushort ByteOrderBigEndian = 0x4D4D;

        /// <summary>
        /// Magic number used within the image file header to identify a TIFF format file.
        /// </summary>
        public const ushort HeaderMagicNumber = 42;
    }
}
