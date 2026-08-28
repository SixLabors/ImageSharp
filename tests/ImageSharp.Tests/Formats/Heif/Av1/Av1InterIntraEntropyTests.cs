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
    /// Verifies the four block-size-group distributions against libaom's forward Q15 defaults.
    /// </summary>
    [Fact]
    public void DefaultsMatchLibaom()
    {
        ReadOnlySpan<uint> forwardThresholds = [16384, 26887, 27597, 30237];
        Av1Distribution[] distributions = Av1DefaultDistributions.InterIntra;

        Assert.Equal(forwardThresholds.Length, distributions.Length);
        for (int group = 0; group < distributions.Length; group++)
        {
            // Av1Distribution stores inverse cumulative thresholds, so convert libaom's forward threshold before
            // comparing the exact Q15 state consumed by the range decoder.
            uint expected = (uint)Av1Distribution.ProbabilityTop - forwardThresholds[group];

            Assert.Equal(expected, distributions[group][0]);
            Assert.Equal(2, distributions[group].NumberOfSymbols);
        }
    }

    /// <summary>
    /// Verifies every AV1 block size against the normative size-group conversion table.
    /// </summary>
    /// <param name="blockSizeValue">The AV1 block-size enumeration value.</param>
    /// <param name="expectedGroup">The normative zero-based size group.</param>
    [Theory]
    [MemberData(nameof(GetBlockSizeGroups))]
    public void GetSizeGroupMatchesNormativeTable(int blockSizeValue, int expectedGroup)
    {
        Av1BlockSize blockSize = (Av1BlockSize)blockSizeValue;

        Assert.Equal(expectedGroup, blockSize.GetSizeGroup());
    }

    /// <summary>
    /// Verifies that the inter-intra flag reader selects and adapts the distribution for each size group.
    /// </summary>
    /// <param name="blockSizeValue">A block-size enumeration value representing one size group.</param>
    /// <param name="sizeGroup">The expected zero-based size group.</param>
    [Theory]
    [InlineData((int)Av1BlockSize.Block4x4, 0)]
    [InlineData((int)Av1BlockSize.Block8x8, 1)]
    [InlineData((int)Av1BlockSize.Block16x16, 2)]
    [InlineData((int)Av1BlockSize.Block32x32, 3)]
    public void ReaderUsesBlockSizeGroup(int blockSizeValue, int sizeGroup)
    {
        bool[] expected = [false, true, true, false, true, false, false, true];
        Av1Distribution writerDistribution = Av1DefaultDistributions.InterIntra[sizeGroup];
        using Av1SymbolWriter writer = new(Configuration.Default, expected.Length, updateCdf: true);

        foreach (bool value in expected)
        {
            writer.WriteSymbol(value, writerDistribution);
        }

        using IMemoryOwner<byte> encoded = writer.Exit();
        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.GetSpan(), 0, updateCdf: true);
        Av1BlockSize blockSize = (Av1BlockSize)blockSizeValue;

        foreach (bool value in expected)
        {
            Assert.Equal(value, decoder.ReadIsInterIntra(blockSize));
        }
    }

    /// <summary>
    /// Verifies that frame-context copies retain adapted inter-intra state without sharing mutable distributions.
    /// </summary>
    [Fact]
    public void FrameEntropyCopyRetainsIndependentState()
    {
        Av1FrameEntropyContext source = new(0);
        Av1FrameEntropyContext destination = new(0);
        source.InterIntra[2].Update(1);

        destination.CopyFrom(source);

        Assert.NotSame(source.InterIntra[2], destination.InterIntra[2]);
        Assert.Equal(source.InterIntra[2][0], destination.InterIntra[2][0]);

        source.InterIntra[2].Update(0);

        Assert.NotEqual(source.InterIntra[2][0], destination.InterIntra[2][0]);
    }

    /// <summary>
    /// Verifies that resetting a frame context restores the default threshold and adaptation state.
    /// </summary>
    [Fact]
    public void FrameEntropyResetRestoresDefaultState()
    {
        Av1FrameEntropyContext context = new(0);
        Av1FrameEntropyContext expected = new(0);
        context.InterIntra[3].Update(1);

        context.ResetToDefaults(0);

        Assert.Equal(expected.InterIntra[3][0], context.InterIntra[3][0]);

        // Applying the same next observation proves that reset restored the update-rate history as well as the visible
        // threshold; otherwise two equal thresholds would diverge because their adaptation rates differ.
        context.InterIntra[3].Update(0);
        expected.InterIntra[3].Update(0);

        Assert.Equal(expected.InterIntra[3][0], context.InterIntra[3][0]);
    }

    /// <summary>
    /// Verifies that a published frame snapshot preserves adapted thresholds and resets their update-rate history.
    /// </summary>
    [Fact]
    public void FrameEntropySnapshotResetsUpdateCount()
    {
        const int updateCount = 20;
        Av1FrameEntropyContext source = new(0);
        Av1FrameEntropyContext snapshot = new(0);

        for (int i = 0; i < updateCount; i++)
        {
            source.InterIntra[1].Update(1);
        }

        source.SnapshotTo(snapshot);

        Assert.Equal(source.InterIntra[1][0], snapshot.InterIntra[1][0]);

        // The source retains twenty observations while the published snapshot restarts at zero. Their next identical
        // observation must therefore move the shared starting threshold by different update rates.
        source.InterIntra[1].Update(0);
        snapshot.InterIntra[1].Update(0);

        Assert.NotEqual(source.InterIntra[1][0], snapshot.InterIntra[1][0]);
    }

    /// <summary>
    /// Provides the normative AV1 size-group table in block-size enumeration order.
    /// </summary>
    /// <returns>Every decoded block size paired with its size group.</returns>
    public static TheoryData<int, int> GetBlockSizeGroups()
    {
        // These are the explicit Size_Group values from AV1 section 9.3 and libaom common_data.h. The test keeps the
        // expected table independent from the production geometry formula so a shared calculation cannot mask errors.
        int[] sizeGroups = [0, 0, 0, 1, 1, 1, 2, 2, 2, 3, 3, 3, 3, 3, 3, 3, 0, 0, 1, 1, 2, 2];
        TheoryData<int, int> result = [];

        for (int blockSize = 0; blockSize < sizeGroups.Length; blockSize++)
        {
            result.Add(blockSize, sizeGroups[blockSize]);
        }

        return result;
    }
}
