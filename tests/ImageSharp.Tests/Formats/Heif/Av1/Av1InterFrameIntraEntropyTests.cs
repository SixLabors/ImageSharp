// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies the entropy state used by intra-coded blocks inside AV1 inter frames.
/// </summary>
[Trait("Format", "Avif")]
public class Av1InterFrameIntraEntropyTests
{
    /// <summary>
    /// Verifies that inter-frame intra luma modes use the normative size group for every AV1 block size.
    /// </summary>
    [Fact]
    public void ReadInterFrameYModeUsesNormativeSizeGroup()
    {
        foreach (ITheoryDataRow row in GetBlockSizeGroups())
        {
            object?[] values = row.GetData();
            this.ReadInterFrameYModeUsesNormativeSizeGroupCase((int)values[0]!, (int)values[1]!);
        }
    }

    private void ReadInterFrameYModeUsesNormativeSizeGroupCase(int blockSizeValue, int sizeGroup)
    {
        Av1BlockSize blockSize = (Av1BlockSize)blockSizeValue;
        Av1PredictionMode[] expected =
        [
            Av1PredictionMode.DC,
            Av1PredictionMode.Directional45Degrees,
            Av1PredictionMode.Smooth,
            Av1PredictionMode.Paeth,
            Av1PredictionMode.Horizontal,
            Av1PredictionMode.Directional157Degrees,
        ];

        Av1Distribution writerDistribution = Av1DefaultDistributions.FrameYMode[sizeGroup];
        using Av1SymbolWriter writer = new(Configuration.Default, 8, updateCdf: true);

        foreach (Av1PredictionMode mode in expected)
        {
            writer.WriteSymbol((int)mode, writerDistribution);
        }

        using IMemoryOwner<byte> encoded = writer.Exit();
        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.GetSpan(), 0, updateCdf: true);

        foreach (Av1PredictionMode mode in expected)
        {
            Assert.Equal(mode, decoder.ReadInterFrameYMode(blockSize));
        }
    }

    /// <summary>
    /// Provides the normative AV1 size-group table in block-size enumeration order.
    /// </summary>
    /// <returns>Every decoded block size paired with its luma-mode size group.</returns>
    public static TheoryData<int, int> GetBlockSizeGroups()
    {
        // This is size_group_lookup from AV1 section 9.3 and the normative lookup table. Keeping expected values explicit
        // ensures that the test does not reproduce the production formula it is intended to verify.
        int[] sizeGroups = [0, 0, 0, 1, 1, 1, 2, 2, 2, 3, 3, 3, 3, 3, 3, 3, 0, 0, 1, 1, 2, 2];
        TheoryData<int, int> result = [];

        for (int blockSize = 0; blockSize < sizeGroups.Length; blockSize++)
        {
            result.Add(blockSize, sizeGroups[blockSize]);
        }

        return result;
    }
}
