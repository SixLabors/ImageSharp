// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies the entropy state and spatial contexts used by intra-coded blocks inside AV1 inter frames.
/// </summary>
[Trait("Format", "Avif")]
public class Av1InterFrameIntraEntropyTests
{
    /// <summary>
    /// Gets the reference decoder's four forward Q15 luma-mode CDF rows in block-size-group order.
    /// </summary>
    private static ReadOnlySpan<ushort> FrameYModeForwardThresholds =>
    [
        22801, 23489, 24293, 24756, 25601, 26123, 26606, 27418, 27945, 29228, 29685, 30349,
        18673, 19845, 22631, 23318, 23950, 24649, 25527, 27364, 28152, 29701, 29984, 30852,
        19770, 20979, 23396, 23939, 24241, 24654, 25136, 27073, 27830, 29360, 29730, 30659,
        20155, 21301, 22838, 23178, 23261, 23533, 23703, 24804, 25352, 26575, 27016, 28049,
    ];

    /// <summary>
    /// Verifies the four normative intra/inter distributions against the reference decoder's forward Q15 defaults.
    /// </summary>
    [Fact]
    public void IntraInterDefaultsMatchReference()
    {
        uint[] forwardThresholds = [806, 16662, 20186, 26538];
        Av1Distribution[] distributions = Av1DefaultDistributions.IntraInter;

        Assert.Equal(forwardThresholds.Length, distributions.Length);
        for (int context = 0; context < distributions.Length; context++)
        {
            // Av1Distribution stores inverse cumulative thresholds, so compare each forward default after the same
            // forward-to-inverse conversion performed by its constructor.
            Assert.Equal((uint)Av1Distribution.ProbabilityTop - forwardThresholds[context], distributions[context][0]);
            Assert.Equal(2, distributions[context].NumberOfSymbols);
        }
    }

    /// <summary>
    /// Verifies every inter-frame intra luma-mode threshold against the reference decoder's forward Q15 defaults.
    /// </summary>
    [Fact]
    public void FrameYModeDefaultsMatchReference()
    {
        const int thresholdsPerGroup = 12;
        ReadOnlySpan<ushort> forwardThresholds = FrameYModeForwardThresholds;
        Av1Distribution[] distributions = Av1DefaultDistributions.FrameYMode;

        Assert.Equal(4, distributions.Length);
        for (int group = 0; group < distributions.Length; group++)
        {
            Assert.Equal(thresholdsPerGroup + 1, distributions[group].NumberOfSymbols);

            for (int threshold = 0; threshold < thresholdsPerGroup; threshold++)
            {
                uint expected = (uint)Av1Distribution.ProbabilityTop - forwardThresholds[(group * thresholdsPerGroup) + threshold];
                Assert.Equal(expected, distributions[group][threshold]);
            }
        }
    }

    /// <summary>
    /// Verifies that the intra/inter reader selects and adapts each of the four spatial-context distributions.
    /// </summary>
    /// <param name="context">The intra/inter spatial context.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void ReadIsInterUsesRequestedContext(int context)
    {
        bool[] expected = [false, true, true, false, true, false, false, true];
        Av1Distribution writerDistribution = Av1DefaultDistributions.IntraInter[context];
        using Av1SymbolWriter writer = new(Configuration.Default, 8, updateCdf: true);

        foreach (bool value in expected)
        {
            writer.WriteSymbol(value, writerDistribution);
        }

        using IMemoryOwner<byte> encoded = writer.Exit();
        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.GetSpan(), 0, updateCdf: true);

        foreach (bool value in expected)
        {
            Assert.Equal(value, decoder.ReadIsInter(context));
        }
    }

    /// <summary>
    /// Verifies that inter-frame intra luma modes use the normative size group for every AV1 block size.
    /// </summary>
    /// <param name="blockSizeValue">The AV1 block-size enumeration value.</param>
    /// <param name="sizeGroup">The normative size group from AV1 section 9.3.</param>
    [Theory]
    [MemberData(nameof(GetBlockSizeGroups))]
    public void ReadInterFrameYModeUsesNormativeSizeGroup(int blockSizeValue, int sizeGroup)
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
    /// Verifies that all four intra/inter contexts follow the normative above-and-left neighbor classification.
    /// </summary>
    /// <param name="hasAbove">Whether the above block is available.</param>
    /// <param name="aboveIsInter">Whether the available above block uses inter prediction.</param>
    /// <param name="hasLeft">Whether the left block is available.</param>
    /// <param name="leftIsInter">Whether the available left block uses inter prediction.</param>
    /// <param name="expected">The expected intra/inter context.</param>
    [Theory]
    [InlineData(false, false, false, false, 0)]
    [InlineData(true, true, false, false, 0)]
    [InlineData(true, false, false, false, 2)]
    [InlineData(false, false, true, true, 0)]
    [InlineData(false, false, true, false, 2)]
    [InlineData(true, true, true, true, 0)]
    [InlineData(true, false, true, true, 1)]
    [InlineData(true, true, true, false, 1)]
    [InlineData(true, false, true, false, 3)]
    public void IntraInterContextMatchesNeighborPredictionTypes(
        bool hasAbove,
        bool aboveIsInter,
        bool hasLeft,
        bool leftIsInter,
        int expected)
    {
        Av1BlockModeInfo? above = hasAbove ? CreateModeInfo(aboveIsInter) : null;
        Av1BlockModeInfo? left = hasLeft ? CreateModeInfo(leftIsInter) : null;

        int actual = Av1SymbolContextHelper.GetIntraInterContext(above, left);

        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// Verifies that frame-context copies retain adapted inter-frame intra state without sharing mutable distributions.
    /// </summary>
    [Fact]
    public void FrameEntropyCopyRetainsIndependentInterFrameIntraState()
    {
        Av1FrameEntropyContext source = new(0);
        Av1FrameEntropyContext destination = new(0);
        source.FrameYMode[2].Update((int)Av1PredictionMode.Smooth);
        source.IntraInter[3].Update(1);

        destination.CopyFrom(source);

        Assert.Equal(source.FrameYMode[2][0], destination.FrameYMode[2][0]);
        Assert.Equal(source.IntraInter[3][0], destination.IntraInter[3][0]);

        source.FrameYMode[2].Update((int)Av1PredictionMode.Paeth);
        source.IntraInter[3].Update(0);

        Assert.NotEqual(source.FrameYMode[2][0], destination.FrameYMode[2][0]);
        Assert.NotEqual(source.IntraInter[3][0], destination.IntraInter[3][0]);
    }

    /// <summary>
    /// Verifies that a published frame snapshot preserves adapted thresholds but resets their update-rate history.
    /// </summary>
    [Fact]
    public void FrameEntropySnapshotResetsInterFrameIntraUpdateCounts()
    {
        const int updateCount = 20;
        Av1FrameEntropyContext source = new(0);
        Av1FrameEntropyContext snapshot = new(0);

        for (int i = 0; i < updateCount; i++)
        {
            source.FrameYMode[1].Update((int)Av1PredictionMode.Vertical);
            source.IntraInter[1].Update(1);
        }

        source.SnapshotTo(snapshot);

        Assert.Equal(source.FrameYMode[1][0], snapshot.FrameYMode[1][0]);
        Assert.Equal(source.IntraInter[1][0], snapshot.IntraInter[1][0]);

        // The source retains twenty observations while the published snapshot restarts at zero. Applying the same
        // symbol therefore moves identical thresholds by different update rates only when reset wiring is complete.
        source.FrameYMode[1].Update((int)Av1PredictionMode.DC);
        snapshot.FrameYMode[1].Update((int)Av1PredictionMode.DC);
        source.IntraInter[1].Update(0);
        snapshot.IntraInter[1].Update(0);

        Assert.NotEqual(source.FrameYMode[1][0], snapshot.FrameYMode[1][0]);
        Assert.NotEqual(source.IntraInter[1][0], snapshot.IntraInter[1][0]);
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

    /// <summary>
    /// Creates decoded neighbor state with either an intra or inter primary reference.
    /// </summary>
    /// <param name="isInter">Whether the neighbor uses inter prediction.</param>
    /// <returns>The initialized block mode state.</returns>
    private static Av1BlockModeInfo CreateModeInfo(bool isInter)
    {
        Av1BlockModeInfo modeInfo = new(Av1BlockSize.Block4x4, Point.Empty);
        modeInfo.ReferenceFrames[0] = isInter ? Av1ReferenceFrameType.Last : Av1ReferenceFrameType.Intra;
        modeInfo.ReferenceFrames[1] = Av1ReferenceFrameType.None;
        return modeInfo;
    }
}
