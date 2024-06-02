// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Symbol;

internal class Av1SuperblockInfo
{
    public int[] SuperblockDeltaQ { get; internal set; } = [];

    public Av1BlockModeInfo GetModeInfo(int rowIndex, int columnIndex) => throw new NotImplementedException();
}
