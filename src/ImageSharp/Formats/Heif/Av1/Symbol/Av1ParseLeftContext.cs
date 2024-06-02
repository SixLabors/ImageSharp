// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Symbol;

internal class Av1ParseLeftContext
{
    public int PartitionHeight { get; set; }

    public int[][] LeftContext { get; set; } = [];

    internal void Clear() => throw new NotImplementedException();
}
