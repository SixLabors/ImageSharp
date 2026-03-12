// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif;

/// <summary>
/// Specifies Exif data types.
/// </summary>
public enum ExifDataType
{
    /// <summary>
    /// Unknown
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// An 8-bit unsigned integer.
    /// </summary>
    Byte = 1,

    /// <summary>
    /// An 8-bit byte containing one 7-bit ASCII code. The final byte is terminated with NULL.
    /// <remarks>
    /// Although the standard defines ASCII this has commonly been ignored as
    /// ASCII cannot properly encode text in many languages.
    /// </remarks>
    /// </summary>
    Ascii = 2,

    /// <summary>
    /// A 16-bit (2-byte) unsigned integer.
    /// </summary>
    Short = 3,

    /// <summary>
    /// A 32-bit (4-byte) unsigned integer.
    /// </summary>
    Long = 4,

    /// <summary>
    /// Two LONGs. The first LONG is the numerator and the second LONG expresses the denominator.
    /// </summary>
    Rational = 5,

    /// <summary>
    /// An 8-bit signed integer.
    /// </summary>
    SignedByte = 6,

    /// <summary>
    /// An 8-bit byte that can take any value depending on the field definition.
    /// </summary>
    Undefined = 7,

    /// <summary>
    /// A 16-bit (2-byte) signed integer.
    /// </summary>
    SignedShort = 8,

    /// <summary>
    /// A 32-bit (4-byte) signed integer (2's complement notation).
    /// </summary>
    SignedLong = 9,

    /// <summary>
    /// Two SLONGs. The first SLONG is the numerator and the second SLONG is the denominator.
    /// </summary>
    SignedRational = 10,

    /// <summary>
    /// A 32-bit single precision floating point value.
    /// </summary>
    SingleFloat = 11,

    /// <summary>
    /// A 64-bit double precision floating point value.
    /// </summary>
    DoubleFloat = 12,

    /// <summary>
    /// Reference to an IFD (32-bit (4-byte) unsigned integer).
    /// </summary>
    Ifd = 13,

    /// <summary>
    /// A 64-bit (8-byte) unsigned integer.
    /// </summary>
    Long8 = 16,

    /// <summary>
    /// A 64-bit (8-byte) signed integer (2's complement notation).
    /// </summary>
    SignedLong8 = 17,

    /// <summary>
    /// Reference to an IFD (64-bit (8-byte) unsigned integer).
    /// </summary>
    Ifd8 = 18,
}
