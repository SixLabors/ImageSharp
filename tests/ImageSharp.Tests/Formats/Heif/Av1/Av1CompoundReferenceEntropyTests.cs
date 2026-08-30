// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies AV1 compound-reference selection and compound inter-mode entropy against current official libaom main.
/// </summary>
[Trait("Format", "Avif")]
public class Av1CompoundReferenceEntropyTests
{
    /// <summary>
    /// Verifies every binary compound-reference default against libaom's forward Q15 tables.
    /// </summary>
    [Fact]
    public void CompoundReferenceDefaultsMatchLibaom()
    {
        AssertBinaryDefaults(
            [1198, 2070, 9166, 7499, 22475],
            Av1DefaultDistributions.CompoundReferenceType);

        AssertBinaryDefaults(
            [
                [5284, 3865, 3128],
                [23152, 14173, 15270],
                [31774, 25120, 26710],
            ],
            Av1DefaultDistributions.UnidirectionalCompoundReference);

        AssertBinaryDefaults(
            [
                [4946, 9468, 1503],
                [19891, 22441, 15160],
                [30731, 31059, 27544],
            ],
            Av1DefaultDistributions.CompoundReference);

        AssertBinaryDefaults(
            [
                [2235, 1423],
                [17182, 15175],
                [30606, 30489],
            ],
            Av1DefaultDistributions.CompoundBackwardReference);
    }

    /// <summary>
    /// Verifies all eight compound inter-mode defaults against libaom's forward Q15 tables.
    /// </summary>
    [Fact]
    public void InterCompoundModeDefaultsMatchLibaom()
    {
        uint[][] expected =
        [
            [7760, 13823, 15808, 17641, 19156, 20666, 26891],
            [10730, 19452, 21145, 22749, 24039, 25131, 28724],
            [10664, 20221, 21588, 22906, 24295, 25387, 28436],
            [13298, 16984, 20471, 24182, 25067, 25736, 26422],
            [18904, 23325, 25242, 27432, 27898, 28258, 30758],
            [10725, 17454, 20124, 22820, 24195, 25168, 26046],
            [17125, 24273, 25814, 27492, 28214, 28704, 30592],
            [13046, 23214, 24505, 25942, 27435, 28442, 29330],
        ];

        Av1Distribution[] actual = Av1DefaultDistributions.InterCompoundMode;

        Assert.Equal(expected.Length, actual.Length);
        for (int context = 0; context < expected.Length; context++)
        {
            Assert.Equal(8, actual[context].NumberOfSymbols);
            for (int threshold = 0; threshold < expected[context].Length; threshold++)
            {
                Assert.Equal((uint)Av1Distribution.ProbabilityTop - expected[context][threshold], actual[context][threshold]);
            }
        }
    }

    /// <summary>
    /// Verifies that each semantic reference reader selects its requested context row and tree decision.
    /// </summary>
    [Fact]
    public void CompoundReferenceReadersUseRequestedDistributions()
    {
        bool[] values = [false, true, true, false, true, false];

        for (int context = 0; context < 5; context++)
        {
            AssertBinaryReader(
                Av1DefaultDistributions.CompoundReferenceType[context],
                values,
                (ref Av1SymbolDecoder decoder) => decoder.ReadCompoundReferenceIsBidirectional(context));
        }

        for (int context = 0; context < 3; context++)
        {
            for (int decision = 0; decision < 3; decision++)
            {
                AssertBinaryReader(
                    Av1DefaultDistributions.UnidirectionalCompoundReference[context][decision],
                    values,
                    (ref Av1SymbolDecoder decoder) => decoder.ReadUnidirectionalCompoundReference(context, decision));

                AssertBinaryReader(
                    Av1DefaultDistributions.CompoundReference[context][decision],
                    values,
                    (ref Av1SymbolDecoder decoder) => decoder.ReadCompoundForwardReference(context, decision));
            }

            for (int decision = 0; decision < 2; decision++)
            {
                AssertBinaryReader(
                    Av1DefaultDistributions.CompoundBackwardReference[context][decision],
                    values,
                    (ref Av1SymbolDecoder decoder) => decoder.ReadCompoundBackwardReference(context, decision));
            }
        }
    }

    /// <summary>
    /// Verifies the packed-mode-context mapping and all eight compound mode symbols.
    /// </summary>
    [Fact]
    public void CompoundModeReaderUsesMappedDistribution()
    {
        ReadOnlySpan<int> packedContexts = [0, 1, 33, 34, 35, 66, 67, 68];

        for (int context = 0; context < packedContexts.Length; context++)
        {
            using Av1SymbolWriter writer = new(Configuration.Default, 3, updateCdf: true);
            Av1Distribution writerDistribution = Av1DefaultDistributions.InterCompoundMode[context];
            writer.WriteSymbol(0, writerDistribution);
            writer.WriteSymbol(7, writerDistribution);
            writer.WriteSymbol(3, writerDistribution);

            using IMemoryOwner<byte> encoded = writer.Exit();
            Av1SymbolDecoder decoder = new(Configuration.Default, encoded.Memory.Span, 0, updateCdf: true);

            Assert.Equal(Av1PredictionMode.NearestNearestMotionVector, decoder.ReadInterCompoundMode(packedContexts[context]));
            Assert.Equal(Av1PredictionMode.NewNewMotionVector, decoder.ReadInterCompoundMode(packedContexts[context]));
            Assert.Equal(Av1PredictionMode.NewNearestMotionVector, decoder.ReadInterCompoundMode(packedContexts[context]));
        }
    }

    /// <summary>
    /// Verifies the compound-reference type context across intra, single, bidirectional, and unidirectional neighbors.
    /// </summary>
    [Fact]
    public void CompoundReferenceTypeContextMatchesLibaom()
    {
        Av1BlockModeInfo intra = CreateModeInfo(Av1ReferenceFrameType.Intra, Av1ReferenceFrameType.None);
        Av1BlockModeInfo singleForward = CreateModeInfo(Av1ReferenceFrameType.Last, Av1ReferenceFrameType.None);
        Av1BlockModeInfo singleBackward = CreateModeInfo(Av1ReferenceFrameType.Backward, Av1ReferenceFrameType.None);
        Av1BlockModeInfo bidirectional = CreateModeInfo(Av1ReferenceFrameType.Last, Av1ReferenceFrameType.Backward);
        Av1BlockModeInfo forwardUnidirectional = CreateModeInfo(Av1ReferenceFrameType.Last, Av1ReferenceFrameType.Last2);
        Av1BlockModeInfo backwardUnidirectional = CreateModeInfo(Av1ReferenceFrameType.Backward, Av1ReferenceFrameType.Alternate);

        Assert.Equal(2, Av1SymbolContextHelper.GetCompoundReferenceTypeContext(null, null));
        Assert.Equal(2, Av1SymbolContextHelper.GetCompoundReferenceTypeContext(intra, null));
        Assert.Equal(2, Av1SymbolContextHelper.GetCompoundReferenceTypeContext(singleForward, null));
        Assert.Equal(0, Av1SymbolContextHelper.GetCompoundReferenceTypeContext(bidirectional, null));
        Assert.Equal(4, Av1SymbolContextHelper.GetCompoundReferenceTypeContext(forwardUnidirectional, null));
        Assert.Equal(2, Av1SymbolContextHelper.GetCompoundReferenceTypeContext(intra, singleForward));
        Assert.Equal(1, Av1SymbolContextHelper.GetCompoundReferenceTypeContext(intra, bidirectional));
        Assert.Equal(3, Av1SymbolContextHelper.GetCompoundReferenceTypeContext(intra, forwardUnidirectional));
        Assert.Equal(3, Av1SymbolContextHelper.GetCompoundReferenceTypeContext(singleForward, singleForward));
        Assert.Equal(1, Av1SymbolContextHelper.GetCompoundReferenceTypeContext(singleForward, singleBackward));
        Assert.Equal(0, Av1SymbolContextHelper.GetCompoundReferenceTypeContext(bidirectional, bidirectional));
        Assert.Equal(2, Av1SymbolContextHelper.GetCompoundReferenceTypeContext(bidirectional, forwardUnidirectional));
        Assert.Equal(4, Av1SymbolContextHelper.GetCompoundReferenceTypeContext(forwardUnidirectional, forwardUnidirectional));
        Assert.Equal(3, Av1SymbolContextHelper.GetCompoundReferenceTypeContext(forwardUnidirectional, backwardUnidirectional));
    }

    /// <summary>
    /// Verifies the exact neighboring-vote groups used by every compound reference-tree decision.
    /// </summary>
    [Fact]
    public void CompoundReferenceContextsAggregateNormativeGroups()
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

        Assert.Equal(0, Av1SymbolContextHelper.GetUnidirectionalCompoundBackwardContext(referenceCounts));
        Assert.Equal(0, Av1SymbolContextHelper.GetUnidirectionalCompoundLast3OrGoldenContext(referenceCounts));
        Assert.Equal(1, Av1SymbolContextHelper.GetUnidirectionalCompoundGoldenContext(referenceCounts));
        Assert.Equal(2, Av1SymbolContextHelper.GetCompoundForwardLast3OrGoldenContext(referenceCounts));
        Assert.Equal(2, Av1SymbolContextHelper.GetCompoundForwardLast2Context(referenceCounts));
        Assert.Equal(1, Av1SymbolContextHelper.GetCompoundForwardGoldenContext(referenceCounts));
        Assert.Equal(1, Av1SymbolContextHelper.GetCompoundBackwardAlternateContext(referenceCounts));
        Assert.Equal(1, Av1SymbolContextHelper.GetCompoundBackwardAlternate2Context(referenceCounts));
    }

    /// <summary>
    /// Verifies compound CDF copying and snapshot update-count reset without sharing mutable state.
    /// </summary>
    [Fact]
    public void FrameEntropyLifecycleIncludesCompoundDistributions()
    {
        const int updateCount = 20;
        Av1FrameEntropyContext source = new(0);
        Av1FrameEntropyContext copy = new(0);
        Av1FrameEntropyContext snapshot = new(0);

        for (int i = 0; i < updateCount; i++)
        {
            source.CompoundReferenceType[4].Update(1);
            source.UnidirectionalCompoundReference[2][2].Update(1);
            source.CompoundReference[1][1].Update(1);
            source.CompoundBackwardReference[0][1].Update(1);
            source.InterCompoundMode[7].Update(6);
        }

        copy.CopyFrom(source);
        source.SnapshotTo(snapshot);

        Assert.Equal(source.CompoundReferenceType[4][0], copy.CompoundReferenceType[4][0]);
        Assert.Equal(source.UnidirectionalCompoundReference[2][2][0], copy.UnidirectionalCompoundReference[2][2][0]);
        Assert.Equal(source.CompoundReference[1][1][0], copy.CompoundReference[1][1][0]);
        Assert.Equal(source.CompoundBackwardReference[0][1][0], copy.CompoundBackwardReference[0][1][0]);
        Assert.Equal(source.InterCompoundMode[7][6], copy.InterCompoundMode[7][6]);

        source.CompoundReferenceType[4].Update(0);
        snapshot.CompoundReferenceType[4].Update(0);
        source.InterCompoundMode[7].Update(0);
        snapshot.InterCompoundMode[7].Update(0);

        Assert.NotEqual(source.CompoundReferenceType[4][0], snapshot.CompoundReferenceType[4][0]);
        Assert.NotEqual(source.InterCompoundMode[7][0], snapshot.InterCompoundMode[7][0]);
    }

    /// <summary>
    /// Verifies one binary reader against a separately adapted writer distribution.
    /// </summary>
    private static void AssertBinaryReader(Av1Distribution distribution, ReadOnlySpan<bool> values, SymbolReader reader)
    {
        using Av1SymbolWriter writer = new(Configuration.Default, values.Length, updateCdf: true);
        foreach (bool value in values)
        {
            writer.WriteSymbol(value, distribution);
        }

        using IMemoryOwner<byte> encoded = writer.Exit();
        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.Memory.Span, 0, updateCdf: true);
        foreach (bool value in values)
        {
            Assert.Equal(value, reader(ref decoder));
        }
    }

    /// <summary>
    /// Verifies one array of binary defaults stored in inverse-cumulative form.
    /// </summary>
    private static void AssertBinaryDefaults(ReadOnlySpan<uint> expected, Av1Distribution[] actual)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Equal((uint)Av1Distribution.ProbabilityTop - expected[i], actual[i][0]);
            Assert.Equal(2, actual[i].NumberOfSymbols);
        }
    }

    /// <summary>
    /// Verifies a matrix of binary defaults stored in inverse-cumulative form.
    /// </summary>
    private static void AssertBinaryDefaults(uint[][] expected, Av1Distribution[][] actual)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (int row = 0; row < expected.Length; row++)
        {
            AssertBinaryDefaults(expected[row], actual[row]);
        }
    }

    /// <summary>
    /// Creates decoded block-mode state with the requested primary and secondary references.
    /// </summary>
    private static Av1BlockModeInfo CreateModeInfo(Av1ReferenceFrameType primary, Av1ReferenceFrameType secondary)
    {
        Av1BlockModeInfo modeInfo = new(Av1BlockSize.Block8x8, Point.Empty);
        modeInfo.ReferenceFrames[0] = primary;
        modeInfo.ReferenceFrames[1] = secondary;
        return modeInfo;
    }

    /// <summary>
    /// Invokes one semantic binary symbol reader.
    /// </summary>
    private delegate bool SymbolReader(ref Av1SymbolDecoder decoder);
}
