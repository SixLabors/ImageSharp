// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;

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
    /// Verifies the encoder's context selection, read-only costing, cost refresh, and adaptive output against
    /// independently seeded distributions.
    /// </summary>
    [Fact]
    public void EncoderUsesRequestedContextAndRefreshedCosts()
    {
        foreach (ITheoryDataRow row in GetContexts())
        {
            object[] values = row.GetData();
            EncoderUsesRequestedContextAndRefreshedCostsCase((int)values[0]);
        }
    }

    private static void EncoderUsesRequestedContextAndRefreshedCostsCase(int context)
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
            // Costs are a snapshot of the tile distributions. The encoder publishes adapted probabilities when
            // its owner refreshes them, not after each written symbol.
            encoder.RefreshCosts();
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
}
