// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies the segment setup and the segment choice of complexity adaptive quantization.
/// </summary>
[Trait("Format", "Avif")]
public class Av1ComplexityAdaptiveQuantizationTests
{
    private const int AlternativeQuantizer = (int)ObuSegmentationLevelFeature.AlternativeQuantizer;

    [Fact]
    public void LowTargetRateCodesWithoutSegments()
    {
        ObuSegmentationParameters segmentation = new();
        segmentation.SetFeatureEnabled(1, AlternativeQuantizer, true);
        segmentation.SetFeatureData(1, AlternativeQuantizer, -20);
        byte[] map = new byte[12];

        Av1ComplexityAdaptiveQuantization.SetupRefreshFrame(
            segmentation, map, keyFrame: true, screenContent: false, 128, 255, Av1BitDepth.EightBit, 0, 255);

        Assert.False(segmentation.Enabled);
        Assert.Equal(0, segmentation.SegmentationUpdateMap);
        Assert.Equal(0, segmentation.SegmentationUpdateData);
        Assert.False(segmentation.IsFeatureActive(1, ObuSegmentationLevelFeature.AlternativeQuantizer));
        Assert.All(map, segment => Assert.Equal(3, segment));
    }

    [Fact]
    public void TargetRateEnablesFiveSegmentsAroundTheFrameQuantizer()
    {
        ObuSegmentationParameters segmentation = new();
        byte[] map = new byte[12];

        Av1ComplexityAdaptiveQuantization.SetupRefreshFrame(
            segmentation, map, keyFrame: false, screenContent: false, 128, 256, Av1BitDepth.EightBit, 0, 255);

        Assert.True(segmentation.Enabled);
        Assert.Equal(1, segmentation.SegmentationUpdateMap);
        Assert.Equal(1, segmentation.SegmentationUpdateData);
        Assert.All(map, segment => Assert.Equal(3, segment));

        // Segment 3 keeps the frame quantizer. The lower segments spend more bits at a finer quantizer, the last one
        // fewer at a coarser quantizer, and the eight-segment range stays unused.
        Assert.False(segmentation.IsFeatureActive(3, ObuSegmentationLevelFeature.AlternativeQuantizer));
        for (int segment = 0; segment < 3; segment++)
        {
            Assert.True(segmentation.IsFeatureActive(segment, ObuSegmentationLevelFeature.AlternativeQuantizer));
            Assert.True(segmentation.GetFeatureData(segment, AlternativeQuantizer) < 0);
        }

        Assert.True(segmentation.GetFeatureData(0, AlternativeQuantizer) < segmentation.GetFeatureData(1, AlternativeQuantizer));
        Assert.True(segmentation.IsFeatureActive(4, ObuSegmentationLevelFeature.AlternativeQuantizer));
        Assert.True(segmentation.GetFeatureData(4, AlternativeQuantizer) > 0);
        Assert.False(segmentation.IsFeatureActive(5, ObuSegmentationLevelFeature.AlternativeQuantizer));
    }

    [Theory]
    [InlineData(100_000, 5.0, 0)]
    [InlineData(100_000, 7.0, 1)]
    [InlineData(200_000, 6.0, 1)]
    [InlineData(300_000, 6.0, 2)]
    [InlineData(600_000, 7.0, 3)]
    [InlineData(2_000_000, 1.0, 4)]
    public void SegmentFollowsRateAndVariance(int projectedRate, double logVariance, int expected)
    {
        // A whole 64x64 block at a target of 1000 has a target rate of 512000 cost units. At the coarsest quantizer
        // the bounds are a quarter, a half and three quarters of it, with log variances below 7, 8 and 9.
        byte segment = Av1ComplexityAdaptiveQuantization.SelectSegment(
            projectedRate, 1000, 256, 16, logVariance, 255, Av1BitDepth.EightBit);

        Assert.Equal(expected, segment);
    }
}
