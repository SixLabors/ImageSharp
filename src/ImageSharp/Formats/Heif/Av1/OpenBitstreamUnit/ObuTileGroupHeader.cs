// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

internal class ObuTileGroupHeader
{
    internal int MaxTileWidthSuperblock { get; set; }

    internal int MaxTileHeightSuperblock { get; set; }

    internal int MinLog2TileColumnCount { get; set; }

    internal int MaxLog2TileColumnCount { get; set; }

    internal int MaxLog2TileRowCount { get; set; }

    internal int MinLog2TileCount { get; set; }

    public bool HasUniformTileSpacing { get; set; }

    internal int TileColumnCountLog2 { get; set; }

    internal int TileColumnCount { get; set; }

    internal int[] TileColumnStartModeInfo { get; set; } = new int[Av1Constants.MaxTileRowCount + 1];

    internal int MinLog2TileRowCount { get; set; }

    internal int TileRowCountLog2 { get; set; }

    internal int[] TileRowStartModeInfo { get; set; } = new int[Av1Constants.MaxTileColumnCount + 1];

    internal int TileRowCount { get; set; }

    internal uint ContextUpdateTileId { get; set; }

    internal int TileSizeBytes { get; set; }
}
