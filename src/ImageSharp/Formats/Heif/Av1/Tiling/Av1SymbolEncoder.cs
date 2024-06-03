// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Symbol;

internal class Av1SymbolEncoder : IDisposable
{
    private readonly Av1Distribution tileIntraBlockCopy = Av1DefaultDistributions.IntraBlockCopy;
    private readonly Av1Distribution[] tilePartitionTypes = Av1DefaultDistributions.PartitionTypes;

    private Av1SymbolWriter? writer;

    public Av1SymbolEncoder(Configuration configuration, int initialSize)
        => this.writer = new(configuration, initialSize);

    public void WriteUseIntraBlockCopy(bool value)
        => this.writer!.WriteSymbol(value ? 1 : 0, this.tileIntraBlockCopy);

    public void WritePartitionType(Av1PartitionType value, int context)
        => this.writer!.WriteSymbol((int)value, this.tilePartitionTypes[context]);

    public IMemoryOwner<byte> Exit() => this.writer!.Exit();

    public void Dispose()
    {
        this.writer?.Dispose();
        this.writer = null;
    }
}
