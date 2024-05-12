// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Symbol;

internal class Av1SymbolEncoder
{
    private readonly uint[] tileIntraBlockCopy = Av1DefaultDistributions.IntraBlockCopy;

    public void WriteUseIntraBlockCopySymbol(Av1SymbolWriter writer, bool value)
        => writer.WriteSymbol(value ? 1 : 0, this.tileIntraBlockCopy, 2);
}
