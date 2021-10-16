// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Formats.Jpeg.Components;
using Xunit;
using JpegQuantization = SixLabors.ImageSharp.Formats.Jpeg.Components.Quantization;

namespace SixLabors.ImageSharp.Tests.Formats.Jpg
{
    [Trait("Format", "Jpg")]
    public class QuantizationTests
    {
        [Fact]
        public void QualityEstimationFromStandardEncoderTables_Luminance()
        {
            int firstIndex = JpegQuantization.QualityEstimationConfidenceLowerThreshold;
            int lastIndex = JpegQuantization.QualityEstimationConfidenceUpperThreshold;
            for (int quality = firstIndex; quality <= lastIndex; quality++)
            {
                Block8x8F table = JpegQuantization.ScaleLuminanceTable(quality);
                int estimatedQuality = JpegQuantization.EstimateLuminanceQuality(ref table);

                Assert.True(
                    quality.Equals(estimatedQuality),
                    $"Failed to estimate luminance quality for standard table at quality level {quality}");
            }
        }

        [Fact]
        public void QualityEstimationFromStandardEncoderTables_Chrominance()
        {
            int firstIndex = JpegQuantization.QualityEstimationConfidenceLowerThreshold;
            int lastIndex = JpegQuantization.QualityEstimationConfidenceUpperThreshold;
            for (int quality = firstIndex; quality <= lastIndex; quality++)
            {
                Block8x8F table = JpegQuantization.ScaleChrominanceTable(quality);
                int estimatedQuality = JpegQuantization.EstimateChrominanceQuality(ref table);

                Assert.True(
                    quality.Equals(estimatedQuality),
                    $"Failed to estimate chrominance quality for standard table at quality level {quality}");
            }
        }
    }
}
