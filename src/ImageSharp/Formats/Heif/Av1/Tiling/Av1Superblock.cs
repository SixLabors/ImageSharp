// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using static SixLabors.ImageSharp.Formats.Heif.Av1.Tiling.Av1TileWriter;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

internal class Av1Superblock
{
    public required Av1EncoderBlockStruct[] FinalBlocks { get; set; }

    public required Av1TileInfo TileInfo { get; set; }

    public required Av1PartitionType[] CodingUnitPartitionTypes { get; internal set; }

    public int Index { get; internal set; }
}
