// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Provides enumeration of the precision in bits of individual components within a pixel format.
/// </summary>
public enum PixelComponentPrecision
{
    /// <summary>
    /// 8-bit signed integer.
    /// </summary>
    SByte = sizeof(sbyte) * 8,

    /// <summary>
    /// 8-bit unsigned integer.
    /// </summary>
    Byte = sizeof(byte) * 8,

    /// <summary>
    /// 16-bit signed integer.
    /// </summary>
    Short = sizeof(short) * 8,

    /// <summary>
    /// 16-bit unsigned integer.
    /// </summary>
    UShort = sizeof(ushort) * 8,

    /// <summary>
    /// 32-bit signed integer.
    /// </summary>
    Int = sizeof(int) * 8,

    /// <summary>
    /// 32-bit unsigned integer.
    /// </summary>
    UInt = sizeof(uint) * 8,

    /// <summary>
    /// 64-bit signed integer.
    /// </summary>
    Long = sizeof(long) * 8,

    /// <summary>
    /// 64-bit unsigned integer.
    /// </summary>
    ULong = sizeof(ulong) * 8,

    /// <summary>
    /// 16-bit floating point.
    /// </summary>
    Half = (sizeof(float) * 8) / 2,

    /// <summary>
    /// 32-bit floating point.
    /// </summary>
    Float = sizeof(float) * 8,

    /// <summary>
    /// 64-bit floating point.
    /// </summary>
    Double = sizeof(double) * 8,

    /// <summary>
    /// 128-bit floating point.
    /// </summary>
    Decimal = sizeof(decimal) * 8,
}
