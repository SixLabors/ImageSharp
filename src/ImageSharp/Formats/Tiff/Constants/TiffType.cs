// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// Enumeration representing the data types understood by the Tiff file-format.
    /// </summary>
    internal enum TiffType
    {
        /// <summary>
        /// Unsigned 8-bit integer.
        /// </summary>
        Byte = 1,

        /// <summary>
        /// ASCII formatted text.
        /// </summary>
        Ascii = 2,

        /// <summary>
        /// Unsigned 16-bit integer.
        /// </summary>
        Short = 3,

        /// <summary>
        /// Unsigned 32-bit integer.
        /// </summary>
        Long = 4,

        /// <summary>
        /// Unsigned rational number.
        /// </summary>
        Rational = 5,

        /// <summary>
        /// Signed 8-bit integer.
        /// </summary>
        SByte = 6,

        /// <summary>
        /// Undefined data type.
        /// </summary>
        Undefined = 7,

        /// <summary>
        /// Signed 16-bit integer.
        /// </summary>
        SShort = 8,

        /// <summary>
        /// Signed 32-bit integer.
        /// </summary>
        SLong = 9,

        /// <summary>
        /// Signed rational number.
        /// </summary>
        SRational = 10,

        /// <summary>
        /// Single precision (4-byte) IEEE format.
        /// </summary>
        Float = 11,

        /// <summary>
        /// Double precision (8-byte) IEEE format.
        /// </summary>
        Double = 12,

        /// <summary>
        /// Reference to an IFD.
        /// </summary>
        Ifd = 13
    }
}