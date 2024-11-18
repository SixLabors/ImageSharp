// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

internal partial class Av1TileWriter
{
    internal class Av1EncoderBlockStruct
    {
        public required Av1MacroBlockD av1xd { get; internal set; }

        public required int[] PaletteSize { get; internal set; }

        public int QIndex { get; internal set; }
    }
}
