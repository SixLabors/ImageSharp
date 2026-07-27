// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Ani;

/// <summary>
/// Identifies top-level ANI RIFF chunks.
/// </summary>
internal enum AniChunkType : uint
{
    /// <summary>
    /// The animation header chunk, "anih".
    /// </summary>
    Header = 0x68_69_6E_61,

    /// <summary>
    /// The frame sequence chunk, "seq ".
    /// </summary>
    Sequence = 0x20_71_65_73,

    /// <summary>
    /// The per-step display-rate chunk, "rate".
    /// </summary>
    Rate = 0x65_74_61_72,

    /// <summary>
    /// A RIFF list chunk, "LIST".
    /// </summary>
    List = 0x54_53_49_4C
}

/// <summary>
/// Identifies ANI RIFF list types.
/// </summary>
internal enum AniListType : uint
{
    /// <summary>
    /// The information list, "INFO".
    /// </summary>
    Info = 0x4F_46_4E_49,

    /// <summary>
    /// The embedded frame-resource list, "fram".
    /// </summary>
    Frames = 0x6D_61_72_66
}

/// <summary>
/// Identifies chunks stored in an ANI information list.
/// </summary>
internal enum AniInfoChunkType : uint
{
    /// <summary>
    /// The animation name, "INAM".
    /// </summary>
    Name = 0x4D_41_4E_49,

    /// <summary>
    /// The animation artist, "IART".
    /// </summary>
    Artist = 0x54_52_41_49
}

/// <summary>
/// Identifies chunks stored in an ANI frame list.
/// </summary>
internal enum AniFrameChunkType : uint
{
    /// <summary>
    /// An embedded frame resource, "icon".
    /// </summary>
    Icon = 0x6E_6F_63_69
}
