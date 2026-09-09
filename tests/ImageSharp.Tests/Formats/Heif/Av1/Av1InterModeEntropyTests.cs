// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies the adaptive distributions and packed contexts used to select an AV1 single-reference inter mode.
/// </summary>
[Trait("Format", "Avif")]
public class Av1InterModeEntropyTests
{
    /// <summary>
    /// Verifies the normative single-reference inter-mode distributions against the reference decoder's forward Q15 defaults.
    /// </summary>
    [Fact]
    public void InterModeDefaultsMatchReference()
    {
        AssertBinaryDefaults(Av1DefaultDistributions.NewMv, [24035, 16630, 15339, 8386, 12222, 4676]);
        AssertBinaryDefaults(Av1DefaultDistributions.ZeroMv, [2175, 1054]);
        AssertBinaryDefaults(Av1DefaultDistributions.RefMv, [23974, 24188, 17848, 28622, 24312, 19923]);
        AssertBinaryDefaults(Av1DefaultDistributions.Drl, [13104, 24560, 18945]);
    }

    /// <summary>
    /// Verifies that each inter-mode context occupies the normative field in the packed mode context.
    /// </summary>
    /// <param name="modeContext">The packed mode context.</param>
    /// <param name="expectedNewMv">The expected newly decoded motion-vector context.</param>
    /// <param name="expectedZeroMv">The expected global-motion context.</param>
    /// <param name="expectedRefMv">The expected spatial reference-motion-vector context.</param>
    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(77, 5, 1, 4)]
    [InlineData(93, 5, 1, 5)]
    public void PackedInterModeContextMatchesReference(int modeContext, int expectedNewMv, int expectedZeroMv, int expectedRefMv)
    {
        Assert.Equal(expectedNewMv, Av1SymbolContextHelper.GetNewMvContext(modeContext));
        Assert.Equal(expectedZeroMv, Av1SymbolContextHelper.GetZeroMvContext(modeContext));
        Assert.Equal(expectedRefMv, Av1SymbolContextHelper.GetRefMvContext(modeContext));
    }

    /// <summary>
    /// Verifies the exact short-circuit order and symbol polarity of the single-reference inter-mode tree.
    /// </summary>
    /// <param name="expectedMode">The expected prediction mode.</param>
    /// <param name="newMvSymbol">The new-motion-vector decision.</param>
    /// <param name="zeroMvSymbol">The global-motion decision, or negative when the leaf precedes it.</param>
    /// <param name="refMvSymbol">The spatial reference-motion-vector decision, or negative when the leaf precedes it.</param>
    [Theory]
    [InlineData((int)Av1PredictionMode.NewMotionVector, 0, -1, -1)]
    [InlineData((int)Av1PredictionMode.GlobalMotionVector, 1, 0, -1)]
    [InlineData((int)Av1PredictionMode.NearestMotionVector, 1, 1, 0)]
    [InlineData((int)Av1PredictionMode.NearMotionVector, 1, 1, 1)]
    public void ReadInterModeMatchesReference(int expectedMode, int newMvSymbol, int zeroMvSymbol, int refMvSymbol)
    {
        const int modeContext = 77;
        Av1Distribution newMv = Av1DefaultDistributions.NewMv[5];
        Av1Distribution zeroMv = Av1DefaultDistributions.ZeroMv[1];
        Av1Distribution refMv = Av1DefaultDistributions.RefMv[4];
        Av1Distribution drl = Av1DefaultDistributions.Drl[2];
        using Av1SymbolWriter writer = new(Configuration.Default, 8, updateCdf: true);

        writer.WriteSymbol(newMvSymbol, newMv);
        if (zeroMvSymbol >= 0)
        {
            writer.WriteSymbol(zeroMvSymbol, zeroMv);
        }

        if (refMvSymbol >= 0)
        {
            writer.WriteSymbol(refMvSymbol, refMv);
        }

        // A symbol after the selected leaf proves that the decoder consumed exactly the decisions on that branch.
        writer.WriteSymbol(true, drl);

        using IMemoryOwner<byte> encoded = writer.Exit();
        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.Memory.Span, 0, updateCdf: true);

        Assert.Equal((Av1PredictionMode)expectedMode, decoder.ReadInterMode(modeContext));
        Assert.True(decoder.ReadDrl(2));
    }

    /// <summary>
    /// Verifies that the dynamic reference-list reader selects each requested context distribution.
    /// </summary>
    /// <param name="context">The dynamic reference-list context.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ReadDrlUsesRequestedContext(int context)
    {
        bool[] expected = [false, true, true, false, true, false, false, true];
        Av1Distribution writerDistribution = Av1DefaultDistributions.Drl[context];
        using Av1SymbolWriter writer = new(Configuration.Default, 8, updateCdf: true);

        foreach (bool value in expected)
        {
            writer.WriteSymbol(value, writerDistribution);
        }

        using IMemoryOwner<byte> encoded = writer.Exit();
        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.Memory.Span, 0, updateCdf: true);

        foreach (bool value in expected)
        {
            Assert.Equal(value, decoder.ReadDrl(context));
        }
    }

    /// <summary>
    /// Verifies the four candidate-weight pairings used to select a dynamic reference-list context.
    /// </summary>
    /// <param name="currentWeight">The current candidate's weight.</param>
    /// <param name="nextWeight">The next candidate's weight.</param>
    /// <param name="expected">The expected dynamic reference-list context.</param>
    [Theory]
    [InlineData(640, 640, 0)]
    [InlineData(640, 639, 1)]
    [InlineData(639, 639, 2)]
    [InlineData(639, 640, 0)]
    public void DrlContextMatchesCandidateWeightCategories(ushort currentWeight, ushort nextWeight, int expected)
    {
        ushort[] referenceWeights = [currentWeight, nextWeight];

        Assert.Equal(expected, Av1SymbolContextHelper.GetDrlContext(referenceWeights, 0));
    }

    /// <summary>
    /// Verifies that frame-context copies retain inter-mode adaptation without sharing mutable distributions.
    /// </summary>
    [Fact]
    public void FrameEntropyCopyRetainsIndependentInterModeState()
    {
        Av1FrameEntropyContext source = new(0);
        Av1FrameEntropyContext destination = new(0);
        source.NewMv[5].Update(1);
        source.ZeroMv[1].Update(1);
        source.RefMv[4].Update(1);
        source.Drl[2].Update(1);

        destination.CopyFrom(source);

        Assert.Equal(source.NewMv[5][0], destination.NewMv[5][0]);
        Assert.Equal(source.ZeroMv[1][0], destination.ZeroMv[1][0]);
        Assert.Equal(source.RefMv[4][0], destination.RefMv[4][0]);
        Assert.Equal(source.Drl[2][0], destination.Drl[2][0]);

        source.NewMv[5].Update(0);
        source.ZeroMv[1].Update(0);
        source.RefMv[4].Update(0);
        source.Drl[2].Update(0);

        Assert.NotEqual(source.NewMv[5][0], destination.NewMv[5][0]);
        Assert.NotEqual(source.ZeroMv[1][0], destination.ZeroMv[1][0]);
        Assert.NotEqual(source.RefMv[4][0], destination.RefMv[4][0]);
        Assert.NotEqual(source.Drl[2][0], destination.Drl[2][0]);
    }

    /// <summary>
    /// Verifies that publishing frame state resets the inter-mode distributions' update-rate history.
    /// </summary>
    [Fact]
    public void FrameEntropySnapshotResetsInterModeUpdateCounts()
    {
        const int updateCount = 20;
        Av1FrameEntropyContext source = new(0);
        Av1FrameEntropyContext snapshot = new(0);

        for (int i = 0; i < updateCount; i++)
        {
            source.NewMv[5].Update(1);
            source.ZeroMv[1].Update(1);
            source.RefMv[4].Update(1);
            source.Drl[2].Update(1);
        }

        source.SnapshotTo(snapshot);

        Assert.Equal(source.NewMv[5][0], snapshot.NewMv[5][0]);
        Assert.Equal(source.ZeroMv[1][0], snapshot.ZeroMv[1][0]);
        Assert.Equal(source.RefMv[4][0], snapshot.RefMv[4][0]);
        Assert.Equal(source.Drl[2][0], snapshot.Drl[2][0]);

        // The source retains twenty observations while the snapshot restarts at zero. Applying the same next symbol
        // therefore moves identical thresholds by different amounts only when the new distributions participate in reset.
        source.NewMv[5].Update(0);
        snapshot.NewMv[5].Update(0);
        source.ZeroMv[1].Update(0);
        snapshot.ZeroMv[1].Update(0);
        source.RefMv[4].Update(0);
        snapshot.RefMv[4].Update(0);
        source.Drl[2].Update(0);
        snapshot.Drl[2].Update(0);

        Assert.NotEqual(source.NewMv[5][0], snapshot.NewMv[5][0]);
        Assert.NotEqual(source.ZeroMv[1][0], snapshot.ZeroMv[1][0]);
        Assert.NotEqual(source.RefMv[4][0], snapshot.RefMv[4][0]);
        Assert.NotEqual(source.Drl[2][0], snapshot.Drl[2][0]);
    }

    /// <summary>
    /// Verifies binary distribution defaults after their conversion to the inverse cumulative representation.
    /// </summary>
    /// <param name="distributions">The distributions under test.</param>
    /// <param name="forwardThresholds">The normative forward Q15 thresholds.</param>
    private static void AssertBinaryDefaults(Av1Distribution[] distributions, ReadOnlySpan<uint> forwardThresholds)
    {
        Assert.Equal(forwardThresholds.Length, distributions.Length);
        for (int context = 0; context < distributions.Length; context++)
        {
            uint expected = (uint)Av1Distribution.ProbabilityTop - forwardThresholds[context];

            Assert.Equal(expected, distributions[context][0]);
            Assert.Equal(2, distributions[context].NumberOfSymbols);
        }
    }
}
