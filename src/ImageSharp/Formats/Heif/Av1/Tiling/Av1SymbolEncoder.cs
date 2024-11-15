// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

internal class Av1SymbolEncoder : IDisposable
{
    private readonly Av1Distribution tileIntraBlockCopy = Av1DefaultDistributions.IntraBlockCopy;
    private readonly Av1Distribution[] tilePartitionTypes = Av1DefaultDistributions.PartitionTypes;

    private bool isDisposed;
    private Av1SymbolWriter writer;

    public Av1SymbolEncoder(Configuration configuration, int initialSize)
        => this.writer = new(configuration, initialSize);

    public void WriteUseIntraBlockCopy(bool value)
    {
        ref Av1SymbolWriter w = ref this.writer;
        w.WriteSymbol(value ? 1 : 0, this.tileIntraBlockCopy);
    }

    public void WritePartitionType(Av1PartitionType partitionType, int context)
    {
        ref Av1SymbolWriter w = ref this.writer;
        w.WriteSymbol((int)partitionType, this.tilePartitionTypes[context]);
    }

    public void WriteSplitOrHorizontal(Av1PartitionType partitionType, Av1BlockSize blockSize, int context)
    {
        Av1Distribution distribution = Av1SymbolDecoder.GetSplitOrHorizontalDistribution(this.tilePartitionTypes, blockSize, context);
        int value = (partitionType == Av1PartitionType.Split) ? 1 : 0;
        ref Av1SymbolWriter w = ref this.writer;
        w.WriteSymbol(value, distribution);
    }

    public void WriteSplitOrVertical(Av1PartitionType partitionType, Av1BlockSize blockSize, int context)
    {
        Av1Distribution distribution = Av1SymbolDecoder.GetSplitOrVerticalDistribution(this.tilePartitionTypes, blockSize, context);
        int value = (partitionType == Av1PartitionType.Split) ? 1 : 0;
        ref Av1SymbolWriter w = ref this.writer;
        w.WriteSymbol(value, distribution);
    }

    public IMemoryOwner<byte> Exit()
    {
        ref Av1SymbolWriter w = ref this.writer;
        return w.Exit();
    }

    public void Dispose()
    {
        if (!this.isDisposed)
        {
            this.writer.Dispose();
            this.isDisposed = true;
        }
    }
}
