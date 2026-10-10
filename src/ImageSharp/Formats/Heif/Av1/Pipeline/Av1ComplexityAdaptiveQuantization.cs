// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Places each block of 16x16 or larger in one of five segments. The segment depends on the coded rate of the block against the target rate
/// of the frame, and on the source variance of the block. Each segment gets a quantizer that scales the expected rate by a fixed ratio.
/// Segment 3 keeps the frame quantizer.
/// </summary>
internal static class Av1ComplexityAdaptiveQuantization
{
    /// <summary>
    /// The number of segments that this mode uses.
    /// </summary>
    private const int SegmentCount = 5;

    /// <summary>
    /// The segment that keeps the frame quantizer. Every block is in this segment before the search places it.
    /// </summary>
    private const byte DefaultSegment = 3;

    /// <summary>
    /// The lowest superblock target rate that enables the segments. Below this rate, the segment syntax usually costs more than it saves.
    /// </summary>
    private const int MinimumSuperblockTargetRate = 256;

    /// <summary>
    /// The log variance threshold of a sequence without first-pass statistics.
    /// </summary>
    private const double DefaultLowVarianceThreshold = 10.0;

    /// <summary>
    /// Gets the rate ratio of each segment, five per strength.
    /// </summary>
    private static ReadOnlySpan<double> QuantizerAdjustmentFactors =>
    [
        1.75, 1.25, 1.05, 1.00, 0.90,
        2.00, 1.50, 1.15, 1.00, 0.85,
        2.50, 1.75, 1.25, 1.00, 0.80
    ];

    /// <summary>
    /// Gets the upper bound of the projected rate of each segment as a fraction of the target rate, five per strength.
    /// </summary>
    private static ReadOnlySpan<double> Transitions =>
    [
        0.15, 0.30, 0.55, 2.00, 100.0,
        0.20, 0.40, 0.65, 2.00, 100.0,
        0.25, 0.50, 0.75, 2.00, 100.0
    ];

    /// <summary>
    /// Gets the upper bound of the log variance of each segment as an offset from the low variance threshold, five per strength.
    /// </summary>
    private static ReadOnlySpan<double> VarianceThresholds =>
    [
        -4.0, -3.0, -2.0, 100.00, 100.0,
        -3.5, -2.5, -1.5, 100.00, 100.0,
        -3.0, -2.0, -1.0, 100.00, 100.0
    ];

    /// <summary>
    /// Returns whether a frame has enough target rate per superblock for its segments to pay off.
    /// </summary>
    /// <param name="superblockTargetRate">The target rate of the frame per 64x64 area.</param>
    /// <returns>Whether the segments are enabled.</returns>
    public static bool IsSuperblockEnabled(int superblockTargetRate) => superblockTargetRate >= MinimumSuperblockTargetRate;

    /// <summary>
    /// Sets the segmentation of a frame that refreshes its segments. Every block starts in the default segment.
    /// If the target rate is too low, the frame codes without segments. The frame size of a sequence never changes, so no reset for a resolution change occurs.
    /// </summary>
    /// <param name="segmentation">The segmentation state of the frame.</param>
    /// <param name="encoderSegmentMap">The segment map that the encoder keeps across frames.</param>
    /// <param name="keyFrame">Whether the frame is a key frame.</param>
    /// <param name="screenContent">Whether the frame is screen content.</param>
    /// <param name="baseQIndex">The frame quantizer index.</param>
    /// <param name="superblockTargetRate">The target rate of the frame per 64x64 area.</param>
    /// <param name="bitDepth">The coded bit depth.</param>
    /// <param name="bestQIndex">The lowest allowed quantizer index.</param>
    /// <param name="worstQIndex">The highest allowed quantizer index.</param>
    public static void SetupRefreshFrame(
        ObuSegmentationParameters segmentation,
        Span<byte> encoderSegmentMap,
        bool keyFrame,
        bool screenContent,
        int baseQIndex,
        int superblockTargetRate,
        Av1BitDepth bitDepth,
        int bestQIndex,
        int worstQIndex)
    {
        int strength = GetStrength(baseQIndex, bitDepth);
        encoderSegmentMap.Fill(DefaultSegment);
        segmentation.ClearFeatures();
        if (!IsSuperblockEnabled(superblockTargetRate))
        {
            // Disable segmentation. The frame sends no segment map and no segment data.
            segmentation.Enabled = false;
            segmentation.SegmentationUpdateMap = 0;
            segmentation.SegmentationUpdateData = 0;
            segmentation.SegmentationTemporalUpdate = 0;
            return;
        }

        // Enable segmentation with a new map and new data. The default segment keeps the frame quantizer, so its quantizer feature stays off.
        segmentation.Enabled = true;
        segmentation.SegmentationUpdateMap = 1;
        segmentation.SegmentationUpdateData = 1;
        segmentation.SegmentationTemporalUpdate = 0;
        segmentation.SetFeatureEnabled(DefaultSegment, (int)ObuSegmentationLevelFeature.AlternativeQuantizer, false);
        for (int segment = 0; segment < SegmentCount; segment++)
        {
            if (segment == DefaultSegment)
            {
                continue;
            }

            int qIndexDelta = Av1RateControl.GetQDeltaByRate(
                keyFrame,
                screenContent,
                baseQIndex,
                QuantizerAdjustmentFactors[(strength * SegmentCount) + segment],
                bitDepth,
                bestQIndex,
                worstQIndex);

            // A lossy frame never gives a segment the quantizer index zero. Index zero makes the segment lossless.
            if (baseQIndex != 0 && baseQIndex + qIndexDelta == 0)
            {
                qIndexDelta = -baseQIndex + 1;
            }

            if (baseQIndex + qIndexDelta > 0)
            {
                segmentation.SetFeatureEnabled(segment, (int)ObuSegmentationLevelFeature.AlternativeQuantizer, true);
                segmentation.SetFeatureData(segment, (int)ObuSegmentationLevelFeature.AlternativeQuantizer, qIndexDelta);
            }
        }
    }

    /// <summary>
    /// Selects the segment of a searched block. The result is the first segment whose rate bound and variance bound are both above the block values.
    /// If no segment matches, the result is the last segment. A higher segment holds a more costly and more complex block.
    /// </summary>
    /// <param name="projectedRate">The searched rate of the block.</param>
    /// <param name="superblockTargetRate">The target rate of the frame per 64x64 area.</param>
    /// <param name="visibleModeInfoCount">The number of 4x4 units of the block inside the frame.</param>
    /// <param name="superblockModeInfoSize">The superblock size in 4x4 units.</param>
    /// <param name="logVariance">The mean log source variance of the block.</param>
    /// <param name="baseQIndex">The frame quantizer index.</param>
    /// <param name="bitDepth">The coded bit depth.</param>
    /// <returns>The segment of the block.</returns>
    public static byte SelectSegment(
        int projectedRate,
        int superblockTargetRate,
        int visibleModeInfoCount,
        int superblockModeInfoSize,
        double logVariance,
        int baseQIndex,
        Av1BitDepth bitDepth)
    {
        // The target is the share of the superblock rate that the visible part of the block covers.
        // The shift by 9 converts bits to rate cost units of 1/512 bit.
        long targetRate = (((long)superblockTargetRate * visibleModeInfoCount) << 9) /
            (superblockModeInfoSize * superblockModeInfoSize);

        int strength = GetStrength(baseQIndex, bitDepth);

        // The encoder reads no statistics from a separate first pass, so the threshold is always the fixed value.
        const double lowVarianceThreshold = DefaultLowVarianceThreshold;
        for (int segment = 0; segment < SegmentCount; segment++)
        {
            int index = (strength * SegmentCount) + segment;
            if (projectedRate < targetRate * Transitions[index] &&
                logVariance < lowVarianceThreshold + VarianceThresholds[index])
            {
                return (byte)segment;
            }
        }

        return SegmentCount - 1;
    }

    /// <summary>
    /// Gets the strength of the segment adjustments from the frame quantizer. The strength is 0 at fine quantizers and increases to 2 at coarse quantizers.
    /// </summary>
    /// <param name="qIndex">The frame quantizer index.</param>
    /// <param name="bitDepth">The coded bit depth.</param>
    /// <returns>The strength, from 0 to 2.</returns>
    private static int GetStrength(int qIndex, Av1BitDepth bitDepth)
    {
        // Each threshold on the quarter AC step adds one level of strength.
        int baseQuantizer = Av1QuantizationLookup.GetAcQuant(qIndex, 0, bitDepth) / 4;
        return (baseQuantizer > 10 ? 1 : 0) + (baseQuantizer > 25 ? 1 : 0);
    }
}
