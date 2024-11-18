// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

internal partial class Av1TileWriter
{
    internal class Av1MacroBlockD
    {
        public required Av1BlockModeInfo ModeInfo { get; internal set; }

        public required Av1TileInfo Tile { get; internal set; }

        public bool IsUpAvailable { get; }

        public bool IsLeftAvailable { get; }

        public Av1MacroBlockModeInfo? AboveMacroBlock { get; internal set; }

        public Av1MacroBlockModeInfo? LeftMacroBlock { get; internal set; }
    }
}
