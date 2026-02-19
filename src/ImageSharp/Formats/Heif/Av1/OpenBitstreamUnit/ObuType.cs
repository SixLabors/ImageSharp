// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

internal enum ObuType
{
    None = 0,
    SequenceHeader = 1,
    TemporalDelimiter = 2,
    FrameHeader = 3,
    RedundantFrameHeader = 7,
    TileGroup = 4,
    Metadata = 5,
    Frame = 6,
    TileList = 8,
    Padding = 15,
}
