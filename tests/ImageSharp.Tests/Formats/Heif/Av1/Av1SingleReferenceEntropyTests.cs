// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies the adaptive distributions and spatial contexts used to select an AV1 inter block's reference mode and frame.
/// </summary>
[Trait("Format", "Avif")]
public class Av1SingleReferenceEntropyTests
{
    /// <summary>
    /// Verifies all eighteen normative single-reference distributions against libaom's forward Q15 defaults.
    /// </summary>
    [Fact]
    public void SingleReferenceDefaultsMatchLibaom()
    {
        uint[][] forwardThresholds =
        [
            [4897, 1555, 4236, 8650, 904, 1444],
            [16973, 16751, 19647, 24773, 11014, 15087],
            [29744, 30279, 31194, 31895, 26875, 30304],
        ];

        Av1Distribution[][] distributions = Av1DefaultDistributions.SingleReference;

        Assert.Equal(forwardThresholds.Length, distributions.Length);
        for (int context = 0; context < distributions.Length; context++)
        {
            Assert.Equal(forwardThresholds[context].Length, distributions[context].Length);
            for (int decision = 0; decision < distributions[context].Length; decision++)
            {
                // Av1Distribution stores inverse cumulative thresholds. Convert each published forward default by the
                // same Q15 complement used by production construction before comparing the exact value.
                uint expected = (uint)Av1Distribution.ProbabilityTop - forwardThresholds[context][decision];

                Assert.Equal(expected, distributions[context][decision][0]);
                Assert.Equal(2, distributions[context][decision].NumberOfSymbols);
            }
        }
    }

    /// <summary>
    /// Verifies the five normative block reference-mode distributions against libaom's forward Q15 defaults.
    /// </summary>
    [Fact]
    public void CompInterDefaultsMatchLibaom()
    {
        uint[] forwardThresholds = [26828, 24035, 12031, 10640, 2901];
        Av1Distribution[] distributions = Av1DefaultDistributions.CompInter;

        Assert.Equal(forwardThresholds.Length, distributions.Length);
        for (int context = 0; context < distributions.Length; context++)
        {
            uint expected = (uint)Av1Distribution.ProbabilityTop - forwardThresholds[context];

            Assert.Equal(expected, distributions[context][0]);
            Assert.Equal(2, distributions[context].NumberOfSymbols);
        }
    }

    /// <summary>
    /// Verifies that every semantic reader selects its exact context row and single-reference tree column.
    /// </summary>
    /// <param name="decision">The zero-based single-reference tree decision.</param>
    /// <param name="context">The neighboring reference-vote context.</param>
    [Theory]
    [MemberData(nameof(GetReaderCases))]
    public void SingleReferenceReadersUseRequestedDistribution(int decision, int context)
    {
        bool[] expected = [false, true, true, false, true, false, false, true];
        Av1Distribution writerDistribution = Av1DefaultDistributions.SingleReference[context][decision];
        using Av1SymbolWriter writer = new(Configuration.Default, 8, updateCdf: true);

        foreach (bool value in expected)
        {
            writer.WriteSymbol(value, writerDistribution);
        }

        using IMemoryOwner<byte> encoded = writer.Exit();
        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.Memory.Span, 0, updateCdf: true);

        foreach (bool value in expected)
        {
            Assert.Equal(value, ReadDecision(ref decoder, decision, context));
        }
    }

    /// <summary>
    /// Verifies that the reference-mode reader selects each of the five spatial-context distributions.
    /// </summary>
    /// <param name="context">The block reference-mode context.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void ReferenceModeReaderUsesRequestedContext(int context)
    {
        bool[] expected = [false, true, true, false, true, false, false, true];
        Av1Distribution writerDistribution = Av1DefaultDistributions.CompInter[context];
        using Av1SymbolWriter writer = new(Configuration.Default, 8, updateCdf: true);

        foreach (bool value in expected)
        {
            writer.WriteSymbol(value, writerDistribution);
        }

        using IMemoryOwner<byte> encoded = writer.Exit();
        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.Memory.Span, 0, updateCdf: true);

        foreach (bool value in expected)
        {
            Assert.Equal(value, decoder.ReadIsCompoundReference(context));
        }
    }

    /// <summary>
    /// Verifies one-pass neighbor collection, compound-neighbor votes, clearing, and intra-neighbor exclusion.
    /// </summary>
    [Fact]
    public void CollectNeighborReferenceCountsMatchesLibaom()
    {
        Av1BlockModeInfo above = CreateModeInfo(Av1ReferenceFrameType.Last, Av1ReferenceFrameType.None);
        Av1BlockModeInfo left = CreateModeInfo(Av1ReferenceFrameType.Backward, Av1ReferenceFrameType.Alternate);
        InlineArray8<byte> referenceCountStorage = default;
        Span<byte> referenceCounts = referenceCountStorage;
        referenceCounts.Fill(7);

        Av1SymbolContextHelper.CollectNeighborReferenceCounts(above, left, referenceCounts);

        ReadOnlySpan<byte> expected = [0, 1, 0, 0, 0, 1, 0, 1];

        for (int reference = 0; reference < referenceCounts.Length; reference++)
        {
            Assert.Equal(expected[reference], referenceCounts[reference]);
        }

        Av1BlockModeInfo intra = CreateModeInfo(Av1ReferenceFrameType.Intra, Av1ReferenceFrameType.None);
        Av1SymbolContextHelper.CollectNeighborReferenceCounts(intra, null, referenceCounts);

        for (int reference = 0; reference < referenceCounts.Length; reference++)
        {
            Assert.Equal((byte)0, referenceCounts[reference]);
        }
    }

    /// <summary>
    /// Verifies that the six context functions aggregate the exact reference groups used by libaom.
    /// </summary>
    [Fact]
    public void SingleReferenceContextsAggregateNormativeReferenceGroups()
    {
        InlineArray8<byte> referenceCountStorage = default;
        Span<byte> referenceCounts = referenceCountStorage;
        referenceCounts[(int)Av1ReferenceFrameType.Last] = 5;
        referenceCounts[(int)Av1ReferenceFrameType.Last2] = 1;
        referenceCounts[(int)Av1ReferenceFrameType.Last3] = 2;
        referenceCounts[(int)Av1ReferenceFrameType.Golden] = 2;
        referenceCounts[(int)Av1ReferenceFrameType.Backward] = 3;
        referenceCounts[(int)Av1ReferenceFrameType.Alternate2] = 3;
        referenceCounts[(int)Av1ReferenceFrameType.Alternate] = 6;

        Assert.Equal(0, Av1SymbolContextHelper.GetSingleReferenceBackwardContext(referenceCounts));
        Assert.Equal(1, Av1SymbolContextHelper.GetSingleReferenceAlternateContext(referenceCounts));
        Assert.Equal(2, Av1SymbolContextHelper.GetSingleReferenceLast3OrGoldenContext(referenceCounts));
        Assert.Equal(2, Av1SymbolContextHelper.GetSingleReferenceLast2Context(referenceCounts));
        Assert.Equal(1, Av1SymbolContextHelper.GetSingleReferenceGoldenContext(referenceCounts));
        Assert.Equal(1, Av1SymbolContextHelper.GetSingleReferenceAlternate2Context(referenceCounts));
    }

    /// <summary>
    /// Verifies every branch of libaom's five-state single-versus-compound reference-mode context.
    /// </summary>
    [Fact]
    public void ReferenceModeContextMatchesLibaom()
    {
        Av1BlockModeInfo singleForward = CreateModeInfo(Av1ReferenceFrameType.Last, Av1ReferenceFrameType.None);
        Av1BlockModeInfo singleBackward = CreateModeInfo(Av1ReferenceFrameType.Backward, Av1ReferenceFrameType.None);
        Av1BlockModeInfo intra = CreateModeInfo(Av1ReferenceFrameType.Intra, Av1ReferenceFrameType.None);
        Av1BlockModeInfo compound = CreateModeInfo(Av1ReferenceFrameType.Last, Av1ReferenceFrameType.Backward);
        Av1BlockModeInfo secondCompound = CreateModeInfo(Av1ReferenceFrameType.Last2, Av1ReferenceFrameType.Alternate);

        Assert.Equal(1, Av1SymbolContextHelper.GetReferenceModeContext(null, null));
        Assert.Equal(0, Av1SymbolContextHelper.GetReferenceModeContext(singleForward, null));
        Assert.Equal(1, Av1SymbolContextHelper.GetReferenceModeContext(singleBackward, null));
        Assert.Equal(3, Av1SymbolContextHelper.GetReferenceModeContext(compound, null));
        Assert.Equal(0, Av1SymbolContextHelper.GetReferenceModeContext(singleForward, singleForward));
        Assert.Equal(1, Av1SymbolContextHelper.GetReferenceModeContext(singleForward, singleBackward));
        Assert.Equal(2, Av1SymbolContextHelper.GetReferenceModeContext(singleForward, compound));
        Assert.Equal(3, Av1SymbolContextHelper.GetReferenceModeContext(intra, compound));
        Assert.Equal(2, Av1SymbolContextHelper.GetReferenceModeContext(compound, singleForward));
        Assert.Equal(3, Av1SymbolContextHelper.GetReferenceModeContext(compound, singleBackward));
        Assert.Equal(4, Av1SymbolContextHelper.GetReferenceModeContext(compound, secondCompound));
    }

    /// <summary>
    /// Verifies the tied, symbol-one-majority, and symbol-zero-majority context states.
    /// </summary>
    /// <param name="forwardCount">The votes for the forward branch represented by symbol zero.</param>
    /// <param name="backwardCount">The votes for the backward branch represented by symbol one.</param>
    /// <param name="expected">The expected context.</param>
    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(1, 2, 0)]
    [InlineData(2, 1, 2)]
    public void SingleReferenceContextReflectsNeighborVoteBalance(byte forwardCount, byte backwardCount, int expected)
    {
        InlineArray8<byte> referenceCountStorage = default;
        Span<byte> referenceCounts = referenceCountStorage;
        referenceCounts[(int)Av1ReferenceFrameType.Last] = forwardCount;
        referenceCounts[(int)Av1ReferenceFrameType.Backward] = backwardCount;

        int actual = Av1SymbolContextHelper.GetSingleReferenceBackwardContext(referenceCounts);

        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// Verifies that frame-context copies retain reference-selection adaptation without sharing mutable distributions.
    /// </summary>
    [Fact]
    public void FrameEntropyCopyRetainsIndependentReferenceSelectionState()
    {
        Av1FrameEntropyContext source = new(0);
        Av1FrameEntropyContext destination = new(0);
        source.SingleReference[2][5].Update(1);
        source.CompInter[4].Update(1);

        destination.CopyFrom(source);

        Assert.Equal(source.SingleReference[2][5][0], destination.SingleReference[2][5][0]);
        Assert.Equal(source.CompInter[4][0], destination.CompInter[4][0]);

        source.SingleReference[2][5].Update(0);
        source.CompInter[4].Update(0);

        Assert.NotEqual(source.SingleReference[2][5][0], destination.SingleReference[2][5][0]);
        Assert.NotEqual(source.CompInter[4][0], destination.CompInter[4][0]);
    }

    /// <summary>
    /// Verifies that publishing frame state resets the reference-selection distributions' update-rate history.
    /// </summary>
    [Fact]
    public void FrameEntropySnapshotResetsReferenceSelectionUpdateCounts()
    {
        const int updateCount = 20;
        Av1FrameEntropyContext source = new(0);
        Av1FrameEntropyContext snapshot = new(0);

        for (int i = 0; i < updateCount; i++)
        {
            source.SingleReference[1][3].Update(1);
            source.CompInter[2].Update(1);
        }

        source.SnapshotTo(snapshot);

        Assert.Equal(source.SingleReference[1][3][0], snapshot.SingleReference[1][3][0]);
        Assert.Equal(source.CompInter[2][0], snapshot.CompInter[2][0]);

        // The source retains twenty observations while the snapshot restarts at zero. The same next symbol therefore
        // moves identical thresholds by different amounts only when the new distribution participates in reset.
        source.SingleReference[1][3].Update(0);
        snapshot.SingleReference[1][3].Update(0);
        source.CompInter[2].Update(0);
        snapshot.CompInter[2].Update(0);

        Assert.NotEqual(source.SingleReference[1][3][0], snapshot.SingleReference[1][3][0]);
        Assert.NotEqual(source.CompInter[2][0], snapshot.CompInter[2][0]);
    }

    /// <summary>
    /// Provides every context and decision pairing in the single-reference distribution matrix.
    /// </summary>
    /// <returns>The eighteen context and decision combinations.</returns>
    public static TheoryData<int, int> GetReaderCases()
    {
        TheoryData<int, int> result = [];

        for (int decision = 0; decision < 6; decision++)
        {
            for (int context = 0; context < 3; context++)
            {
                result.Add(decision, context);
            }
        }

        return result;
    }

    /// <summary>
    /// Reads one semantic single-reference decision through its production entry point.
    /// </summary>
    /// <param name="decoder">The tile symbol decoder.</param>
    /// <param name="decision">The zero-based single-reference tree decision.</param>
    /// <param name="context">The neighboring reference-vote context.</param>
    /// <returns>The decoded binary decision.</returns>
    private static bool ReadDecision(ref Av1SymbolDecoder decoder, int decision, int context)
        => decision switch
        {
            0 => decoder.ReadSingleReferenceIsBackward(context),
            1 => decoder.ReadSingleReferenceIsAlternate(context),
            2 => decoder.ReadSingleReferenceIsLast3OrGolden(context),
            3 => decoder.ReadSingleReferenceIsLast2(context),
            4 => decoder.ReadSingleReferenceIsGolden(context),
            _ => decoder.ReadSingleReferenceIsAlternate2(context),
        };

    /// <summary>
    /// Creates decoded block-mode state with the requested primary and secondary reference labels.
    /// </summary>
    /// <param name="primary">The primary reference label.</param>
    /// <param name="secondary">The optional secondary reference label.</param>
    /// <returns>The initialized block mode state.</returns>
    private static Av1BlockModeInfo CreateModeInfo(Av1ReferenceFrameType primary, Av1ReferenceFrameType secondary)
    {
        Av1BlockModeInfo modeInfo = new(Av1BlockSize.Block4x4, Point.Empty);
        modeInfo.ReferenceFrames[0] = primary;
        modeInfo.ReferenceFrames[1] = secondary;
        return modeInfo;
    }
}
