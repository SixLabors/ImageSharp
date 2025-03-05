// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Ani;

internal enum AniChunkType : uint
{
    /// <summary>
    /// "anih"
    /// </summary>
    AniH = 0x68_69_6E_61,

    /// <summary>
    /// "seq "
    /// </summary>
    Seq = 0x20_71_65_73,

    /// <summary>
    /// "rate"
    /// </summary>
    Rate = 0x65_74_61_72,

    /// <summary>
    /// "LIST"
    /// </summary>
    List = 0x54_53_49_4C
}

/// <summary>
/// ListType
/// </summary>
internal enum AniListType : uint
{
    /// <summary>
    /// "INFO" (ListType)
    /// </summary>
    Info = 0x4F_46_4E_49,

    /// <summary>
    /// "fram"
    /// </summary>
    Fram = 0x6D_61_72_66
}

/// <summary>
/// in "INFO"
/// </summary>
internal enum AniListInfoType : uint
{
    /// <summary>
    /// "INAM"
    /// </summary>
    INam = 0x4D_41_4E_49,

    /// <summary>
    /// "IART"
    /// </summary>
    IArt = 0x54_52_41_49
}

/// <summary>
/// in "Fram"
/// </summary>
internal enum AniListFrameType : uint
{
    /// <summary>
    /// "icon"
    /// </summary>
    Icon = 0x6E_6F_63_69
}
