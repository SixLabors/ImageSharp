// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Symbol;

internal class Av1SymbolDecoder
{
    private readonly uint[] tileIntraBlockCopy = Av1DefaultDistributions.IntraBlockCopy;

    public bool ReadUseIntraBlockCopySymbol(ref Av1SymbolReader reader)
        => reader.ReadSymbol(this.tileIntraBlockCopy, 2) > 0;
}
