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
    /// Verifies representative and complete multi-symbol defaults against pinned libaom's forward Q15 tables.
    /// </summary>
    [Fact]
    public void DefaultsMatchPinnedLibaom()
    {
        AssertForwardThresholds(Av1DefaultDistributions.InterIntraMode[1], [1875, 11082, 27332]);
        AssertForwardThresholds(Av1DefaultDistributions.WedgeInterIntra[(int)Av1BlockSize.Block8x8], [20036]);
        AssertForwardThresholds(Av1DefaultDistributions.CompoundType[(int)Av1BlockSize.Block8x8], [23431]);
        AssertForwardThresholds(
            Av1DefaultDistributions.WedgeIndex[(int)Av1BlockSize.Block8x8],
            [2438, 4440, 6599, 8663, 11005, 12874, 15751, 18094, 20359, 22362, 24127, 25702, 27752, 29450, 31171]);

        ReadOnlySpan<uint> compoundIndex = [18244, 12865, 7053, 13259, 9334, 4644];
        ReadOnlySpan<uint> compoundGroupIndex = [26607, 22891, 18840, 24594, 19934, 22674];

        for (int context = 0; context < compoundIndex.Length; context++)
        {
            AssertForwardThresholds(Av1DefaultDistributions.CompoundIndex[context], [compoundIndex[context]]);
            AssertForwardThresholds(Av1DefaultDistributions.CompoundGroupIndex[context], [compoundGroupIndex[context]]);
        }
    }

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

    /// <summary>
    /// Verifies copying, resetting, and snapshot publication for every selectable-compound distribution family.
    /// </summary>
    [Fact]
    public void FrameEntropyLifecycleIncludesSelectableCompoundFamilies()
    {
        Av1FrameEntropyContext source = new(0);
        Av1FrameEntropyContext copy = new(0);
        Av1FrameEntropyContext snapshot = new(0);
        Av1FrameEntropyContext defaults = new(0);
        Av1Distribution[] sourceDistributions =
        [
            source.InterIntraMode[1],
            source.WedgeInterIntra[(int)Av1BlockSize.Block8x8],
            source.CompoundType[(int)Av1BlockSize.Block8x8],
            source.WedgeIndex[(int)Av1BlockSize.Block8x8],
            source.CompoundIndex[2],
            source.CompoundGroupIndex[4],
        ];

        Av1Distribution[] copyDistributions =
        [
            copy.InterIntraMode[1],
            copy.WedgeInterIntra[(int)Av1BlockSize.Block8x8],
            copy.CompoundType[(int)Av1BlockSize.Block8x8],
            copy.WedgeIndex[(int)Av1BlockSize.Block8x8],
            copy.CompoundIndex[2],
            copy.CompoundGroupIndex[4],
        ];

        Av1Distribution[] snapshotDistributions =
        [
            snapshot.InterIntraMode[1],
            snapshot.WedgeInterIntra[(int)Av1BlockSize.Block8x8],
            snapshot.CompoundType[(int)Av1BlockSize.Block8x8],
            snapshot.WedgeIndex[(int)Av1BlockSize.Block8x8],
            snapshot.CompoundIndex[2],
            snapshot.CompoundGroupIndex[4],
        ];

        Av1Distribution[] defaultDistributions =
        [
            defaults.InterIntraMode[1],
            defaults.WedgeInterIntra[(int)Av1BlockSize.Block8x8],
            defaults.CompoundType[(int)Av1BlockSize.Block8x8],
            defaults.WedgeIndex[(int)Av1BlockSize.Block8x8],
            defaults.CompoundIndex[2],
            defaults.CompoundGroupIndex[4],
        ];

        foreach (Av1Distribution distribution in sourceDistributions)
        {
            distribution.Update(distribution.NumberOfSymbols - 1);
        }

        copy.CopyFrom(source);
        source.SnapshotTo(snapshot);

        for (int index = 0; index < sourceDistributions.Length; index++)
        {
            Assert.NotSame(sourceDistributions[index], copyDistributions[index]);
            Assert.NotSame(sourceDistributions[index], snapshotDistributions[index]);
            Assert.Equal(sourceDistributions[index][0], copyDistributions[index][0]);
            Assert.Equal(sourceDistributions[index][0], snapshotDistributions[index][0]);
        }

        source.ResetToDefaults(0);

        for (int index = 0; index < sourceDistributions.Length; index++)
        {
            Assert.Equal(defaultDistributions[index][0], sourceDistributions[index][0]);
        }
    }

    /// <summary>
    /// Compares one inverse-cumulative distribution with pinned forward thresholds.
    /// </summary>
    private static void AssertForwardThresholds(Av1Distribution distribution, ReadOnlySpan<uint> forwardThresholds)
    {
        Assert.Equal(forwardThresholds.Length + 1, distribution.NumberOfSymbols);
        for (int index = 0; index < forwardThresholds.Length; index++)
        {
            Assert.Equal((uint)Av1Distribution.ProbabilityTop - forwardThresholds[index], distribution[index]);
        }
    }
}
