// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

internal class ObuTileInfo
{
    internal int MaxTileWidthSuperBlock { get; set; }

    internal int MaxTileHeightSuperBlock { get; set; }

    internal int MinLog2TileColumnCount { get; set; }

    internal int MaxLog2TileColumnCount { get; set; }

    internal int MaxLog2TileRowCount { get; set; }

    internal int MinLog2TileCount { get; set; }

    public bool HasUniformTileSpacing { get; set; }

    internal int TileColumnCountLog2 { get; set; }

    internal int TileColumnCount { get; set; }

    internal int[] TileColumnStartModeInfo { get; set; }

    internal int MinLog2TileRowCount { get; set; }

    internal int TileRowCountLog2 { get; set; }

    internal int[] TileRowStartModeInfo { get; set; }

    internal int TileRowCount { get; set; }

    internal uint ContextUpdateTileId { get; set; }

    internal int TileSizeBytes { get; set; }
}
