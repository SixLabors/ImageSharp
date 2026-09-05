// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies the adaptive distributions and spatial contexts used by AV1 switchable interpolation filters.
/// </summary>
[Trait("Format", "Avif")]
public class Av1InterpolationFilterEntropyTests
{
    /// <summary>
    /// The number of reference, direction, and neighbor-state combinations represented by the distribution table.
    /// </summary>
    private const int SwitchableInterpolationContextCount = 16;

    /// <summary>
    /// The interpolation-filter direction index for vertical prediction.
    /// </summary>
    private const int VerticalDirection = 0;

    /// <summary>
    /// The interpolation-filter direction index for horizontal prediction.
    /// </summary>
    private const int HorizontalDirection = 1;

    /// <summary>
    /// Gets the reference decoder's forward Q15 switchable interpolation-filter thresholds in context order.
    /// </summary>
    private static ReadOnlySpan<ushort> ForwardThresholds =>
    [
        31935, 32720,
        5568, 32719,
        422, 2938,
        28244, 32608,
        31206, 31953,
        4862, 32121,
        770, 1152,
        20889, 25637,
        31910, 32724,
        4120, 32712,
        305, 2247,
        27403, 32636,
        31022, 32009,
        2963, 32093,
        601, 943,
        14969, 21398,
    ];

    /// <summary>
    /// Verifies every normative switchable interpolation-filter distribution against the reference decoder's forward Q15 defaults.
    /// </summary>
    [Fact]
    public void DefaultsMatchReference()
    {
        const int thresholdCount = 2;
        ReadOnlySpan<ushort> forwardThresholds = ForwardThresholds;
        Av1Distribution[] distributions = Av1DefaultDistributions.SwitchableInterpolation;

        Assert.Equal(SwitchableInterpolationContextCount, distributions.Length);
        for (int context = 0; context < distributions.Length; context++)
        {
            Assert.Equal(thresholdCount + 1, distributions[context].NumberOfSymbols);

            for (int threshold = 0; threshold < thresholdCount; threshold++)
            {
                // Av1Distribution stores inverse cumulative thresholds. Complement each published forward value by
                // the same Q15 probability top used during production construction before comparing exact state.
                uint expected = (uint)Av1Distribution.ProbabilityTop - forwardThresholds[(context * thresholdCount) + threshold];

                Assert.Equal(expected, distributions[context][threshold]);
            }
        }
    }

    /// <summary>
    /// Verifies that the symbol reader selects and adapts each of the sixteen switchable interpolation contexts.
    /// </summary>
    /// <param name="context">The reference, direction, and neighbor filter context.</param>
    [Theory]
    [MemberData(nameof(GetContexts))]
    public void ReaderUsesRequestedContext(int context)
    {
        Av1InterpolationFilter[] expected =
        [
            Av1InterpolationFilter.Regular,
            Av1InterpolationFilter.Sharp,
            Av1InterpolationFilter.Smooth,
            Av1InterpolationFilter.Regular,
            Av1InterpolationFilter.Smooth,
            Av1InterpolationFilter.Sharp,
        ];

        Av1Distribution writerDistribution = Av1DefaultDistributions.SwitchableInterpolation[context];
        using Av1SymbolWriter writer = new(Configuration.Default, expected.Length, updateCdf: true);

        foreach (Av1InterpolationFilter filter in expected)
        {
            writer.WriteSymbol((int)filter, writerDistribution);
        }

        using IMemoryOwner<byte> encoded = writer.Exit();
        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.Memory.Span, 0, updateCdf: true);

        foreach (Av1InterpolationFilter filter in expected)
        {
            Assert.Equal(filter, decoder.ReadSwitchableInterpolationFilter(context));
        }
    }

    /// <summary>
    /// Verifies the encoder's context selection, read-only costing, and adaptive output against independently seeded distributions.
    /// </summary>
    [Theory]
    [MemberData(nameof(GetContexts))]
    public void EncoderUsesRequestedContextAndLiveCosts(int context)
    {
        ReadOnlySpan<Av1InterpolationFilter> filters =
        [
            Av1InterpolationFilter.Sharp,
            Av1InterpolationFilter.Smooth,
            Av1InterpolationFilter.Regular,
            Av1InterpolationFilter.Sharp,
            Av1InterpolationFilter.Regular,
            Av1InterpolationFilter.Smooth,
        ];

        // The constructor converts forward thresholds to inverse CDF storage. Supply the published forward
        // values directly, independently of the production context factory.
        Av1Distribution distribution = new(
            ForwardThresholds[context * 2],
            ForwardThresholds[(context * 2) + 1]);

        using Av1SymbolWriter expectedWriter = new(Configuration.Default, 64, updateCdf: true);
        using Av1SymbolEncoder encoder = new(Configuration.Default, 64, qIndex: 0, updateCdf: true);
        foreach (Av1InterpolationFilter filter in filters)
        {
            int expectedCost = Av1ProbabilityCost.GetSymbolCost(distribution, (int)filter);
            Assert.Equal(expectedCost, encoder.GetSwitchableInterpolationFilterCost(filter, context));
            Assert.Equal(expectedCost, encoder.GetSwitchableInterpolationFilterCost(filter, context));
            expectedWriter.WriteSymbol((int)filter, distribution);
            encoder.WriteSwitchableInterpolationFilter(filter, context);
        }

        using IMemoryOwner<byte> expected = expectedWriter.Exit();
        using IMemoryOwner<byte> actual = encoder.Exit();
        Assert.Equal(expected.Memory.Span, actual.Memory.Span);
        Av1SymbolDecoder decoder = new(Configuration.Default, actual.Memory.Span, 0, updateCdf: true);
        foreach (Av1InterpolationFilter filter in filters)
        {
            Assert.Equal(filter, decoder.ReadSwitchableInterpolationFilter(context));
        }
    }

    /// <summary>
    /// Verifies all sixteen combinations of reference type, direction, and contributing neighbor filter state.
    /// </summary>
    [Fact]
    public void ContextLayoutMatchesReference()
    {
        Av1BlockModeInfo single = CreateModeInfo(
            Av1ReferenceFrameType.Last,
            Av1ReferenceFrameType.None,
            Av1InterpolationFilter.Regular,
            Av1InterpolationFilter.Regular);

        Av1BlockModeInfo compound = CreateModeInfo(
            Av1ReferenceFrameType.Last,
            Av1ReferenceFrameType.Backward,
            Av1InterpolationFilter.Regular,
            Av1InterpolationFilter.Regular);

        Av1BlockModeInfo regular = CreateModeInfo(
            Av1ReferenceFrameType.Last,
            Av1ReferenceFrameType.None,
            Av1InterpolationFilter.Regular,
            Av1InterpolationFilter.Regular);

        Av1BlockModeInfo smooth = CreateModeInfo(
            Av1ReferenceFrameType.Last,
            Av1ReferenceFrameType.None,
            Av1InterpolationFilter.Smooth,
            Av1InterpolationFilter.Smooth);

        Av1BlockModeInfo sharp = CreateModeInfo(
            Av1ReferenceFrameType.Last,
            Av1ReferenceFrameType.None,
            Av1InterpolationFilter.Sharp,
            Av1InterpolationFilter.Sharp);

        // Contexts zero through three are single-reference vertical contexts. Compound prediction adds four, while
        // horizontal prediction adds eight. The mixed Regular/Smooth pair selects the fourth neighbor state.
        Assert.Equal(0, Av1SymbolContextHelper.GetSwitchableInterpolationContext(single, regular, null, VerticalDirection));
        Assert.Equal(1, Av1SymbolContextHelper.GetSwitchableInterpolationContext(single, smooth, null, VerticalDirection));
        Assert.Equal(2, Av1SymbolContextHelper.GetSwitchableInterpolationContext(single, sharp, null, VerticalDirection));
        Assert.Equal(3, Av1SymbolContextHelper.GetSwitchableInterpolationContext(single, regular, smooth, VerticalDirection));
        Assert.Equal(4, Av1SymbolContextHelper.GetSwitchableInterpolationContext(compound, regular, null, VerticalDirection));
        Assert.Equal(5, Av1SymbolContextHelper.GetSwitchableInterpolationContext(compound, smooth, null, VerticalDirection));
        Assert.Equal(6, Av1SymbolContextHelper.GetSwitchableInterpolationContext(compound, sharp, null, VerticalDirection));
        Assert.Equal(7, Av1SymbolContextHelper.GetSwitchableInterpolationContext(compound, regular, smooth, VerticalDirection));
        Assert.Equal(8, Av1SymbolContextHelper.GetSwitchableInterpolationContext(single, regular, null, HorizontalDirection));
        Assert.Equal(9, Av1SymbolContextHelper.GetSwitchableInterpolationContext(single, smooth, null, HorizontalDirection));
        Assert.Equal(10, Av1SymbolContextHelper.GetSwitchableInterpolationContext(single, sharp, null, HorizontalDirection));
        Assert.Equal(11, Av1SymbolContextHelper.GetSwitchableInterpolationContext(single, regular, smooth, HorizontalDirection));
        Assert.Equal(12, Av1SymbolContextHelper.GetSwitchableInterpolationContext(compound, regular, null, HorizontalDirection));
        Assert.Equal(13, Av1SymbolContextHelper.GetSwitchableInterpolationContext(compound, smooth, null, HorizontalDirection));
        Assert.Equal(14, Av1SymbolContextHelper.GetSwitchableInterpolationContext(compound, sharp, null, HorizontalDirection));
        Assert.Equal(15, Av1SymbolContextHelper.GetSwitchableInterpolationContext(compound, regular, smooth, HorizontalDirection));
    }

    /// <summary>
    /// Verifies that only neighbors sharing the current primary reference contribute their directional filter.
    /// </summary>
    [Fact]
    public void ContextUsesMatchingPrimaryOrSecondaryNeighborReference()
    {
        Av1BlockModeInfo current = CreateModeInfo(
            Av1ReferenceFrameType.Last,
            Av1ReferenceFrameType.None,
            Av1InterpolationFilter.Regular,
            Av1InterpolationFilter.Regular);

        Av1BlockModeInfo secondaryMatch = CreateModeInfo(
            Av1ReferenceFrameType.Golden,
            Av1ReferenceFrameType.Last,
            Av1InterpolationFilter.Smooth,
            Av1InterpolationFilter.Sharp);

        Av1BlockModeInfo mismatch = CreateModeInfo(
            Av1ReferenceFrameType.Golden,
            Av1ReferenceFrameType.None,
            Av1InterpolationFilter.Regular,
            Av1InterpolationFilter.Regular);

        Assert.Equal(1, Av1SymbolContextHelper.GetSwitchableInterpolationContext(current, secondaryMatch, mismatch, VerticalDirection));
        Assert.Equal(10, Av1SymbolContextHelper.GetSwitchableInterpolationContext(current, secondaryMatch, mismatch, HorizontalDirection));
        Assert.Equal(3, Av1SymbolContextHelper.GetSwitchableInterpolationContext(current, mismatch, null, VerticalDirection));
        Assert.Equal(11, Av1SymbolContextHelper.GetSwitchableInterpolationContext(current, null, null, HorizontalDirection));
    }

    /// <summary>
    /// Verifies that frame-context copies retain interpolation adaptation without sharing mutable distributions.
    /// </summary>
    [Fact]
    public void FrameEntropyCopyRetainsIndependentInterpolationState()
    {
        Av1FrameEntropyContext source = new(0);
        Av1FrameEntropyContext destination = new(0);
        source.SwitchableInterpolation[15].Update((int)Av1InterpolationFilter.Sharp);

        destination.CopyFrom(source);

        Assert.Equal(source.SwitchableInterpolation[15][0], destination.SwitchableInterpolation[15][0]);

        source.SwitchableInterpolation[15].Update((int)Av1InterpolationFilter.Regular);

        Assert.NotEqual(source.SwitchableInterpolation[15][0], destination.SwitchableInterpolation[15][0]);
    }

    /// <summary>
    /// Verifies that restoring frame defaults replaces adapted interpolation thresholds and update history.
    /// </summary>
    [Fact]
    public void FrameEntropyResetRestoresInterpolationDefaults()
    {
        const int updateCount = 20;
        Av1FrameEntropyContext context = new(0);

        for (int i = 0; i < updateCount; i++)
        {
            context.SwitchableInterpolation[5].Update((int)Av1InterpolationFilter.Sharp);
        }

        context.ResetToDefaults(0);

        Av1Distribution expected = Av1DefaultDistributions.SwitchableInterpolation[5];

        Assert.Equal(expected[0], context.SwitchableInterpolation[5][0]);
        Assert.Equal(expected[1], context.SwitchableInterpolation[5][1]);

        expected.Update((int)Av1InterpolationFilter.Smooth);
        context.SwitchableInterpolation[5].Update((int)Av1InterpolationFilter.Smooth);

        Assert.Equal(expected[0], context.SwitchableInterpolation[5][0]);
        Assert.Equal(expected[1], context.SwitchableInterpolation[5][1]);
    }

    /// <summary>
    /// Verifies that publishing frame state resets interpolation update history while retaining adapted thresholds.
    /// </summary>
    [Fact]
    public void FrameEntropySnapshotResetsInterpolationUpdateCounts()
    {
        const int updateCount = 20;
        Av1FrameEntropyContext source = new(0);
        Av1FrameEntropyContext snapshot = new(0);

        for (int i = 0; i < updateCount; i++)
        {
            source.SwitchableInterpolation[7].Update((int)Av1InterpolationFilter.Smooth);
        }

        source.SnapshotTo(snapshot);

        Assert.Equal(source.SwitchableInterpolation[7][0], snapshot.SwitchableInterpolation[7][0]);

        // The source retains its observations while the snapshot restarts at zero. Applying the same next symbol moves
        // identical thresholds by different amounts only when the new distribution participates in snapshot reset.
        source.SwitchableInterpolation[7].Update((int)Av1InterpolationFilter.Regular);
        snapshot.SwitchableInterpolation[7].Update((int)Av1InterpolationFilter.Regular);

        Assert.NotEqual(source.SwitchableInterpolation[7][0], snapshot.SwitchableInterpolation[7][0]);
    }

    /// <summary>
    /// Provides every switchable interpolation-filter context.
    /// </summary>
    /// <returns>The sixteen zero-based context indices.</returns>
    public static TheoryData<int> GetContexts()
    {
        TheoryData<int> result = [];

        for (int context = 0; context < SwitchableInterpolationContextCount; context++)
        {
            result.Add(context);
        }

        return result;
    }

    /// <summary>
    /// Creates decoded block state with the requested references and directional interpolation filters.
    /// </summary>
    /// <param name="primaryReference">The primary reference label.</param>
    /// <param name="secondaryReference">The optional secondary reference label.</param>
    /// <param name="verticalFilter">The vertical interpolation filter.</param>
    /// <param name="horizontalFilter">The horizontal interpolation filter.</param>
    /// <returns>The initialized block mode state.</returns>
    private static Av1BlockModeInfo CreateModeInfo(
        Av1ReferenceFrameType primaryReference,
        Av1ReferenceFrameType secondaryReference,
        Av1InterpolationFilter verticalFilter,
        Av1InterpolationFilter horizontalFilter)
    {
        Av1BlockModeInfo modeInfo = new(Av1BlockSize.Block8x8, Point.Empty);
        modeInfo.ReferenceFrames[0] = primaryReference;
        modeInfo.ReferenceFrames[1] = secondaryReference;
        modeInfo.InterpolationFilters[0] = verticalFilter;
        modeInfo.InterpolationFilters[1] = horizontalFilter;
        return modeInfo;
    }
}
