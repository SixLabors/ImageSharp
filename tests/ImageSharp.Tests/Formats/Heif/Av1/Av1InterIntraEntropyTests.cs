// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies the entropy state and block-size groups used by the AV1 inter-intra prediction flag.
/// </summary>
[Trait("Format", "Avif")]
public class Av1InterIntraEntropyTests
{
    /// <summary>
    /// Verifies that the inter-intra flag reader selects and adapts the distribution for each size group.
    /// </summary>
    [Fact]
    public void ReaderUsesBlockSizeGroup()
    {
        ReaderUsesBlockSizeGroupCase((int)Av1BlockSize.Block4x4, 0);
        ReaderUsesBlockSizeGroupCase((int)Av1BlockSize.Block8x8, 1);
        ReaderUsesBlockSizeGroupCase((int)Av1BlockSize.Block16x16, 2);
        ReaderUsesBlockSizeGroupCase((int)Av1BlockSize.Block32x32, 3);
    }

    private static void ReaderUsesBlockSizeGroupCase(int blockSizeValue, int sizeGroup)
    {
        bool[] expected = [false, true, true, false, true, false, false, true];
        Av1Distribution writerDistribution = Av1DefaultDistributions.InterIntra[sizeGroup];
        using Av1SymbolWriter writer = new(Configuration.Default, updateCdf: true);
        Span<byte> output = writer.GetTileBuffer();

        foreach (bool value in expected)
        {
            writer.WriteSymbol(ref output, value, writerDistribution);
        }

        using IMemoryOwner<byte> encoded = writer.Exit();
        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.GetSpan(), 0, updateCdf: true);
        Av1BlockSize blockSize = (Av1BlockSize)blockSizeValue;

        foreach (bool value in expected)
        {
            Assert.Equal(value, decoder.ReadIsInterIntra(blockSize));
        }
    }
}
