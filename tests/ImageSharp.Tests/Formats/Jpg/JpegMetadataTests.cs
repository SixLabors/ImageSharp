// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Formats.Jpeg;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Jpg
{
    [Trait("Format", "Jpg")]
    public class JpegMetadataTests
    {
        [Fact]
        public void CloneIsDeep()
        {
            var meta = new JpegMetadata { Quality = 50, ColorType = JpegColorType.Luminance };
            var clone = (JpegMetadata)meta.DeepClone();

            clone.Quality = 99;
            clone.ColorType = JpegColorType.YCbCrRatio420;

            Assert.False(meta.Quality.Equals(clone.Quality));
            Assert.False(meta.ColorType.Equals(clone.ColorType));
        }

        [Fact]
        public void Quality_DefaultQuality()
        {
            var meta = new JpegMetadata();

            Assert.Equal(meta.Quality, ImageSharp.Formats.Jpeg.Components.Quantization.DefaultQualityFactor);
        }

        [Fact]
        public void Quality_LuminanceOnlyQuality()
        {
            int quality = 50;

            var meta = new JpegMetadata { LuminanceQuality = quality };

            Assert.Equal(meta.Quality, quality);
        }

        [Fact]
        public void Quality_BothComponentsQuality()
        {
            int quality = 50;

            var meta = new JpegMetadata { LuminanceQuality = quality, ChrominanceQuality = quality };

            Assert.Equal(meta.Quality, quality);
        }

        [Fact]
        public void Quality_ReturnsMaxQuality()
        {
            int qualityLuma = 50;
            int qualityChroma = 30;

            var meta = new JpegMetadata { LuminanceQuality = qualityLuma, ChrominanceQuality = qualityChroma };

            Assert.Equal(meta.Quality, qualityLuma);
        }
    }
}
