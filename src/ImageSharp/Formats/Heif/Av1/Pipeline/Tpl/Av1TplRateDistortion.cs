// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Tpl;

/// <summary>
/// The rate multiplier and quantizer conversions that the model and its consumers share.
/// </summary>
internal static class Av1TplRateDistortion
{
    /// <summary>
    /// Gets the rate multiplier boost by golden boost, in units of 1/128. Reference: rd_boost_factor.
    /// </summary>
    private static ReadOnlySpan<byte> BoostFactors => [64, 32, 32, 32, 24, 16, 12, 12, 8, 8, 4, 4, 2, 2, 1, 0];

    /// <summary>
    /// Gets the rate multiplier scale by layer depth, in units of 1/128. Reference: rd_layer_depth_factor.
    /// </summary>
    private static ReadOnlySpan<byte> LayerDepthFactors => [160, 160, 160, 160, 192, 208, 224];

    /// <summary>
    /// Returns the rate multiplier of a quantizer, with the layer depth and golden boost adjustments of statistics
    /// consuming encodes. Reference: av1_compute_rd_mult().
    /// </summary>
    /// <param name="qIndex">The quantizer index, including any luma DC delta.</param>
    /// <param name="bitDepth">The sample precision.</param>
    /// <param name="updateType">The update type of the frame.</param>
    /// <param name="layerDepth">The layer depth, at most six.</param>
    /// <param name="boostIndex">The golden boost divided by 100, at most fifteen.</param>
    /// <param name="keyFrame">Whether the frame is a key frame.</param>
    /// <param name="useFixedQpOffsets">The fixed quantizer offset mode.</param>
    /// <param name="isStatConsumptionStage">Whether first-pass statistics are consumed.</param>
    /// <param name="tuning">The tune metric.</param>
    /// <returns>The rate multiplier, at least one.</returns>
    public static int GetRateMultiplier(
        int qIndex,
        Av1BitDepth bitDepth,
        Av1FrameUpdateType updateType,
        int layerDepth,
        int boostIndex,
        bool keyFrame,
        int useFixedQpOffsets,
        bool isStatConsumptionStage,
        Av1Tuning tuning)
    {
        long multiplier = Av1RateDistortion.GetRateMultiplier(qIndex, bitDepth, updateType, tuning, false);
        if (isStatConsumptionStage && useFixedQpOffsets == 0 && !keyFrame)
        {
            multiplier = (multiplier * LayerDepthFactors[layerDepth]) >> 7;
            multiplier += (multiplier * BoostFactors[boostIndex]) >> 7;
        }

        return multiplier > 0 ? (int)Math.Min(multiplier, int.MaxValue) : 1;
    }

    /// <summary>
    /// Returns the golden boost index of the rate multiplier. Reference: AOMMIN(15, (p_rc->gfu_boost / 100)).
    /// </summary>
    /// <param name="goldenBoost">The golden boost.</param>
    /// <returns>The boost index.</returns>
    public static int GetBoostIndex(int goldenBoost) => Math.Min(15, goldenBoost / 100);

    /// <summary>
    /// Converts a quantizer index to the real quantizer on the 8-bit scale. Reference: av1_convert_qindex_to_q().
    /// </summary>
    /// <param name="qIndex">The quantizer index.</param>
    /// <param name="bitDepth">The sample precision.</param>
    /// <returns>The real quantizer.</returns>
    public static double ConvertQIndexToQ(int qIndex, Av1BitDepth bitDepth)
    {
        int acQuantizer = Av1QuantizationLookup.GetAcQuant(qIndex, 0, bitDepth);
        return bitDepth switch
        {
            Av1BitDepth.EightBit => acQuantizer / 4.0,
            Av1BitDepth.TenBit => acQuantizer / 16.0,
            _ => acQuantizer / 64.0
        };
    }

    /// <summary>
    /// Returns the change of quantizer index between two real quantizers inside the allowed range.
    /// Reference: av1_compute_qdelta().
    /// </summary>
    /// <param name="qStart">The starting real quantizer.</param>
    /// <param name="qTarget">The target real quantizer.</param>
    /// <param name="bitDepth">The sample precision.</param>
    /// <param name="bestQuality">The lowest quantizer index. Reference: rc->best_quality.</param>
    /// <param name="worstQuality">The highest quantizer index. Reference: rc->worst_quality.</param>
    /// <returns>The quantizer index change.</returns>
    public static int ComputeQDelta(double qStart, double qTarget, Av1BitDepth bitDepth, int bestQuality, int worstQuality)
        => FindQIndex(qTarget, bitDepth, bestQuality, worstQuality) - FindQIndex(qStart, bitDepth, bestQuality, worstQuality);

    /// <summary>
    /// Returns the first quantizer index whose real quantizer reaches a value, or the highest index.
    /// Reference: av1_find_qindex().
    /// </summary>
    /// <param name="desiredQ">The real quantizer.</param>
    /// <param name="bitDepth">The sample precision.</param>
    /// <param name="bestQIndex">The lowest quantizer index.</param>
    /// <param name="worstQIndex">The highest quantizer index.</param>
    /// <returns>The quantizer index.</returns>
    public static int FindQIndex(double desiredQ, Av1BitDepth bitDepth, int bestQIndex, int worstQIndex)
    {
        int low = bestQIndex;
        int high = worstQIndex;
        while (low < high)
        {
            int middle = (low + high) >> 1;
            if (ConvertQIndexToQ(middle, bitDepth) < desiredQ)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }
}
