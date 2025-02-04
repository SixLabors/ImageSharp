// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Ani;

internal enum AniChunkType : uint
{
    Seq = 0x73_65_71_20,
    Rate = 0x72_61_74_65,
    List = 0x4C_49_53_54
}
