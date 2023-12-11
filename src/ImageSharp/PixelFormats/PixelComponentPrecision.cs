// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Provides enumeration of the maximum precision of individual components within a pixel format.
/// </summary>
public enum PixelComponentPrecision
{
    /// <summary>
    /// 8-bit signed integer.
    /// </summary>
    SByte,

    /// <summary>
    /// 8-bit unsigned integer.
    /// </summary>
    Byte,

    /// <summary>
    /// 16-bit signed integer.
    /// </summary>
    Short,

    /// <summary>
    /// 16-bit unsigned integer.
    /// </summary>
    UShort,

    /// <summary>
    /// 32-bit signed integer.
    /// </summary>
    Int,

    /// <summary>
    /// 32-bit unsigned integer.
    /// </summary>
    UInt,

    /// <summary>
    /// 16-bit floating point.
    /// </summary>
    Half,

    /// <summary>
    /// 32-bit floating point.
    /// </summary>
    Float
}
