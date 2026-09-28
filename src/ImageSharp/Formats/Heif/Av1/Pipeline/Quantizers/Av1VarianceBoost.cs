// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;

/// <summary>
/// Chooses the superblock quantizers of the Variance Boost delta quantizer mode, which lowers the quantizer of
/// superblocks with low variance. Reference: av1_get_sbq_variance_boost().
/// </summary>
internal static class Av1VarianceBoost
{
    /// <summary>
    /// The side of a Variance Boost superblock. Reference: the 64x64 superblock that Variance Boost requires.
    /// </summary>
    public const int SuperblockSize = 64;

    /// <summary>
    /// The side of the sub-blocks whose variances are sampled.
    /// </summary>
    public const int SubblockSize = 8;

    /// <summary>
    /// The number of sub-blocks in a superblock.
    /// </summary>
    public const int SubblockCount = (SuperblockSize / SubblockSize) * (SuperblockSize / SubblockSize);

    /// <summary>
    /// The largest quantizer step ratio. Reference: VAR_BOOST_MAX_BOOST.
    /// </summary>
    private const double MaximumBoost = 8.0;

    /// <summary>
    /// The largest quantizer index reduction. Reference: VAR_BOOST_MAX_DELTAQ_RANGE.
    /// </summary>
    private const int MaximumDeltaQRange = 80;

    /// <summary>
    /// The default strength in percent. Reference: the deltaq_strength default.
    /// </summary>
    private const int DefaultStrength = 100;

    /// <summary>
    /// Gets the delta quantizer resolution of a frame. Reference: aom_get_variance_boost_delta_q_res().
    /// </summary>
    /// <param name="qIndex">The frame quantizer index.</param>
    /// <returns>The resolution.</returns>
    public static int GetDeltaQResolution(int qIndex)
        => qIndex >= 160 ? 8 : qIndex >= 120 ? 4 : qIndex >= 80 ? 2 : 1;

    /// <summary>
    /// Gets the representative variance of a superblock: the 1:2:1 weighted mean of the sub-block variances at the
    /// ends of the fourth, fifth and sixth octiles. Reference: av1_get_variance_boost_block_variance().
    /// </summary>
    /// <param name="variances">The 64 sub-block variances, each divided by the sub-block area; sorted in place.</param>
    /// <returns>The superblock variance.</returns>
    public static uint GetBlockVariance(Span<uint> variances)
    {
        // An octile of 5 balances quality and consistency for still pictures.
        const int octile = 5;
        const int octileSize = SubblockCount / 8;
        variances.Sort();
        const int middle = (octile * octileSize) - 1;
        int lower = Math.Max(octileSize - 1, middle - octileSize);
        int upper = Math.Min(SubblockCount - 1, middle + octileSize);
        return (variances[lower] + (2 * variances[middle]) + variances[upper] + 2) / 4;
    }

    /// <summary>
    /// Gets the quantizer index of a superblock. Reference: av1_get_sbq_variance_boost().
    /// </summary>
    /// <param name="variance">The superblock variance.</param>
    /// <param name="baseQIndex">The frame quantizer index.</param>
    /// <param name="bitDepth">The coded sample precision.</param>
    /// <returns>The superblock quantizer index before the delta resolution.</returns>
    public static int GetSuperblockQIndex(uint variance, int baseQIndex, Av1BitDepth bitDepth)
    {
        double strength = Math.Clamp(DefaultStrength / 100.0 * 3.0, 0.0, 6.0);

        // Flat patches and very fine gradients are boosted as if their variance were one.
        if (variance == 0)
        {
            variance = 1;
        }

        // High and medium variance superblocks get essentially no boost, lower variances increasingly more.
        double ratio = (0.15 * strength * (-Math.Log2(variance) + 10.0)) + 1.0;
        ratio = Math.Clamp(ratio, 1.0, MaximumBoost);

        double baseQ = ConvertQIndexToQ(baseQIndex, bitDepth);
        int targetQIndex = ConvertQToQIndex(baseQ / ratio, bitDepth);

        // The boost shrinks for lower base quantizers.
        int boost = (int)Math.Round((baseQIndex + 544.0) * (baseQIndex - targetQIndex) / 1279.0, MidpointRounding.AwayFromZero);
        boost = Math.Min(MaximumDeltaQRange, boost);
        return Math.Max(baseQIndex - boost, 1);
    }

    /// <summary>
    /// Rounds a superblock quantizer index to the delta resolution relative to the previous one.
    /// Reference: av1_adjust_q_from_delta_q_res().
    /// </summary>
    /// <param name="resolution">The delta quantizer resolution.</param>
    /// <param name="previousQIndex">The quantizer index of the previous coded superblock.</param>
    /// <param name="currentQIndex">The wanted quantizer index.</param>
    /// <returns>The coded quantizer index.</returns>
    public static int AdjustToResolution(int resolution, int previousQIndex, int currentQIndex)
    {
        currentQIndex = Math.Clamp(currentQIndex, resolution, 256 - resolution);
        int sign = currentQIndex - previousQIndex >= 0 ? 1 : -1;
        int deadzone = resolution / 4;
        int mask = ~(resolution - 1);
        int absoluteDelta = (Math.Abs(currentQIndex - previousQIndex) + deadzone) & mask;
        return Math.Max(previousQIndex + (sign * absoluteDelta), 1);
    }

    /// <summary>
    /// Converts a quantizer index to the real quantizer. Reference: av1_convert_qindex_to_q().
    /// </summary>
    private static double ConvertQIndexToQ(int qIndex, Av1BitDepth bitDepth)
    {
        int step = Av1QuantizationLookup.GetAcQuant(qIndex, 0, bitDepth);
        return bitDepth switch
        {
            Av1BitDepth.EightBit => step / 4.0,
            Av1BitDepth.TenBit => step / 16.0,
            _ => step / 64.0
        };
    }

    /// <summary>
    /// Finds the first quantizer index whose real quantizer reaches a value. Reference: av1_convert_q_to_qindex().
    /// </summary>
    private static int ConvertQToQIndex(double q, Av1BitDepth bitDepth)
    {
        int qIndex = 0;
        while (qIndex < Av1Constants.MaxQ && ConvertQIndexToQ(qIndex, bitDepth) < q)
        {
            qIndex++;
        }

        return qIndex;
    }
}
