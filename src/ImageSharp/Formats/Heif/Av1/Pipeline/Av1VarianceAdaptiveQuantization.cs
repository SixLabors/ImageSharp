// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Places each block in one of eight segments by its source variance, and gives each segment a quantizer that scales
/// the expected rate by a fixed ratio. Reference: aq_variance.c.
/// </summary>
internal static class Av1VarianceAdaptiveQuantization
{
    /// <summary>
    /// Gets the rate ratio of each segment. Reference: rate_ratio.
    /// </summary>
    private static ReadOnlySpan<double> RateRatio => [2.2, 1.7, 1.3, 1.0, 0.9, .8, .7, .6];

    /// <summary>
    /// Sets the segment quantizers of a frame that refreshes the segment map: an intra frame, an alternate reference,
    /// or a golden frame that is not coded from an alternate reference. The segment of average energy codes at the
    /// rate of the frame quantizer. Reference: av1_vaq_frame_setup().
    /// </summary>
    /// <param name="segmentation">The segmentation state of the frame.</param>
    /// <param name="keyFrame">Whether the frame is a key frame. Reference: current_frame.frame_type.</param>
    /// <param name="screenContent">Whether the frame is screen content. Reference: is_screen_content_type.</param>
    /// <param name="baseQIndex">The frame quantizer index.</param>
    /// <param name="averageEnergy">The log of the frame's first-pass intra error. Reference: mb_av_energy.</param>
    /// <param name="bitDepth">The coded bit depth.</param>
    /// <param name="bestQIndex">The lowest allowed quantizer index. Reference: rc->best_quality.</param>
    /// <param name="worstQIndex">The highest allowed quantizer index. Reference: rc->worst_quality.</param>
    public static void SetupRefreshFrame(
        ObuSegmentationParameters segmentation,
        bool keyFrame,
        bool screenContent,
        int baseQIndex,
        double averageEnergy,
        Av1BitDepth bitDepth,
        int bestQIndex,
        int worstQIndex)
    {
        int energyIndex = Math.Clamp((int)(averageEnergy - 2), 0, 7);
        double averageRatio = RateRatio[energyIndex];

        // Reference: av1_enable_segmentation() and av1_clearall_segfeatures().
        segmentation.Enabled = true;
        segmentation.SegmentationUpdateMap = 1;
        segmentation.SegmentationUpdateData = 1;
        segmentation.SegmentationTemporalUpdate = 0;
        segmentation.ClearFeatures();
        for (int segment = 0; segment < Av1Constants.MaxSegmentCount; segment++)
        {
            int qIndexDelta = Av1RateControl.GetQDeltaByRate(
                keyFrame, screenContent, baseQIndex, RateRatio[segment] / averageRatio, bitDepth, bestQIndex, worstQIndex);

            // A lossy frame never gives a segment quantizer zero, which would make the segment lossless.
            if (baseQIndex != 0 && baseQIndex + qIndexDelta == 0)
            {
                qIndexDelta = -baseQIndex + 1;
            }

            segmentation.SetFeatureData(segment, (int)ObuSegmentationLevelFeature.AlternativeQuantizer, qIndexDelta);
            segmentation.SetFeatureEnabled(segment, (int)ObuSegmentationLevelFeature.AlternativeQuantizer, true);
        }
    }
}
