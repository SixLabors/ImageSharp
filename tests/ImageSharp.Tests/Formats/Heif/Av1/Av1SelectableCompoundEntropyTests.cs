// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies the adaptive syntax distributions used by selectable compound and inter-intra prediction.
/// </summary>
[Trait("Format", "Avif")]
public class Av1SelectableCompoundEntropyTests
{
    /// <summary>
    /// Verifies that every new reader selects and adapts its intended context without consuming adjacent syntax.
    /// </summary>
    [Fact]
    public void ReadersRoundTripInNormativeOrder()
    {
        const int groupContext = 4;
        const int compoundIndexContext = 2;
        Av1BlockSize blockSize = Av1BlockSize.Block8x8;
        using Av1SymbolWriter writer = new(Configuration.Default, 32, updateCdf: true);
        writer.WriteSymbol((int)Av1InterIntraMode.Horizontal, Av1DefaultDistributions.InterIntraMode[blockSize.GetSizeGroup()]);
        writer.WriteSymbol(true, Av1DefaultDistributions.WedgeInterIntra[(int)blockSize]);
        writer.WriteSymbol(13, Av1DefaultDistributions.WedgeIndex[(int)blockSize]);
        writer.WriteSymbol(true, Av1DefaultDistributions.CompoundGroupIndex[groupContext]);
        writer.WriteSymbol(false, Av1DefaultDistributions.CompoundIndex[compoundIndexContext]);
        writer.WriteSymbol(1, Av1DefaultDistributions.CompoundType[(int)blockSize]);

        using IMemoryOwner<byte> encoded = writer.Exit();
        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.GetSpan(), 0, updateCdf: true);

        Assert.Equal(Av1InterIntraMode.Horizontal, decoder.ReadInterIntraMode(blockSize));
        Assert.True(decoder.ReadUseInterIntraWedge(blockSize));
        Assert.Equal(13, decoder.ReadWedgeIndex(blockSize));
        Assert.True(decoder.ReadCompoundGroupIndex(groupContext));
        Assert.False(decoder.ReadCompoundIndex(compoundIndexContext));
        Assert.Equal(Av1CompoundType.DifferenceWeighted, decoder.ReadMaskedCompoundType(blockSize));
    }
}
