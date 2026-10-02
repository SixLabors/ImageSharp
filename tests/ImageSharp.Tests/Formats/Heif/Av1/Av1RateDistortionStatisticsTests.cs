// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies the sharpness 3 smoothing charges against libaom's adjust_rdcost() and adjust_cost().
/// </summary>
[Trait("Format", "Avif")]
public class Av1RateDistortionStatisticsTests
{
    /// <summary>
    /// The rate multiplier of every case.
    /// </summary>
    private const int RateMultiplier = 300;

    /// <summary>
    /// The smoothing offset of every case.
    /// </summary>
    private const long Offset = 40;

    /// <summary>
    /// Verifies that the adjust_rdcost() charge adds the offset to the distortion and prices the cost again.
    /// </summary>
    [Fact]
    public void SmoothingOffsetAddsDistortionAndPricesCostAgain()
    {
        Av1RateDistortionStatistics statistics = new(RateMultiplier, 1000, 5000);

        statistics.AddSmoothingOffset(RateMultiplier, Offset);

        Assert.Equal(5040, statistics.Distortion);
        Assert.Equal(RdCost(RateMultiplier, 1000, 5040), statistics.Cost);
    }

    /// <summary>
    /// Verifies that the motion_mode_rd() charge prices the cost again and adds the priced offset to a valid luma
    /// cost only.
    /// </summary>
    /// <param name="lumaCost">The luma cost before the charge.</param>
    /// <param name="expectedLumaCost">The luma cost after the charge.</param>
    [Theory]
    [InlineData(12345L, 12345L + (Offset << 7))]
    [InlineData(long.MaxValue, long.MaxValue)]
    public void PredictionSmoothingOffsetChargesValidLumaCost(long lumaCost, long expectedLumaCost)
    {
        Av1RateDistortionStatistics statistics = new(RateMultiplier, 1000, 5000) { LumaCost = lumaCost };

        statistics.AddPredictionSmoothingOffset(RateMultiplier, Offset);

        Assert.Equal(5040, statistics.Distortion);
        Assert.Equal(RdCost(RateMultiplier, 1000, 5040), statistics.Cost);
        Assert.Equal(expectedLumaCost, statistics.LumaCost);
    }

    /// <summary>
    /// Verifies that the av1_rd_pick_inter_mode() charge adds the priced offset to the mode cost as it stands, which
    /// need not be the price of the rate and distortion.
    /// </summary>
    [Fact]
    public void ModeSmoothingOffsetAddsPricedOffsetToCost()
    {
        Av1RateDistortionStatistics statistics = new(RateMultiplier, 1000, 5000) { Cost = 999999 };

        statistics.AddModeSmoothingOffset(RateMultiplier, Offset);

        Assert.Equal(5040, statistics.Distortion);
        Assert.Equal(999999 + RdCost(RateMultiplier, 0, Offset), statistics.Cost);
    }

    /// <summary>
    /// Mirrors the RDCOST macro: the rate scaled by the multiplier and rounded off by AV1_PROB_COST_SHIFT bits, plus
    /// the distortion scaled up by RDDIV_BITS bits.
    /// </summary>
    /// <param name="rateMultiplier">The rate multiplier.</param>
    /// <param name="rate">The rate.</param>
    /// <param name="distortion">The distortion.</param>
    /// <returns>The cost.</returns>
    private static long RdCost(long rateMultiplier, int rate, long distortion)
        => (((rate * rateMultiplier) + (1 << 8)) >> 9) + (distortion << 7);
}
