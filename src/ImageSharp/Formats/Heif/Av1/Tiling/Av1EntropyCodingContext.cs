// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

internal partial class Av1TileWriter
{
    internal class Av1EntropyCodingContext
    {
        public required Av1MacroBlockModeInfo MacroBlockModeInfo { get; internal set; }

        public Point SuperblockOrigin { get; internal set; }
    }
}
