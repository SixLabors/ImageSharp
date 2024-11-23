// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

internal class Av1Common
{
    public int ModeInfoRowCount { get; internal set; }

    public int ModeInfoColumnCount { get; internal set; }

    public int ModeInfoStride { get; internal set; }

    public required ObuFrameSize FrameSize { get; internal set; }

    public required ObuTileGroupHeader TilesInfo { get; internal set; }
}
