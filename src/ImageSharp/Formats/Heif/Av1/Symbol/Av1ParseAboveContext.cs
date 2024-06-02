// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Symbol;

internal class Av1ParseAboveContext
{
    public int PartitionWidth { get; set; }

    public int[][] AboveContext { get; set; } = [];

    internal void Clear(int startColumnIndex, int endColumnIndex)
    {
        this.PartitionWidth = -1;
        this.AboveContext = [];
    }
}
