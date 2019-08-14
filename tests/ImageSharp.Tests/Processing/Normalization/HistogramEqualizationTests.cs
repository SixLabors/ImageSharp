// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Normalization;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Normalization
{
    public class HistogramEqualizationTests
    {
        private static readonly ImageComparer ValidatorComparer = ImageComparer.TolerantPercentage(0.0456F);

        [Theory]
        [InlineData(256)]
        [InlineData(65536)]
        public void HistogramEqualizationTest(int luminanceLevels)
        {
            // Arrange
            var pixels = new byte[]
            {
                52,  55,  61,  59,  70,  61,  76,  61,
                62,  59,  55, 104,  94,  85,  59,  71,
                63,  65,  66, 113, 144, 104,  63,  72,
                64,  70,  70, 126, 154, 109,  71,  69,
                67,  73,  68, 106, 122,  88,  68,  68,
                68,  79,  60,  79,  77,  66,  58,  75,
                69,  85,  64,  58,  55,  61,  65,  83,
                70,  87,  69,  68,  65,  73,  78,  90
            };

            using (var image = new Image<Rgba32>(8, 8))
            {
                for (int y = 0; y < 8; y++)
                {
                    for (int x = 0; x < 8; x++)
                    {
                        byte luminance = pixels[(y * 8) + x];
                        image[x, y] = new Rgba32(luminance, luminance, luminance);
                    }
                }

                var expected = new byte[]
                {
                0,    12,   53,   32,  146,   53,  174,   53,
                57,   32,   12,  227,  219,  202,   32,  154,
                65,   85,   93,  239,  251,  227,   65,  158,
                73,  146,  146,  247,  255,  235,  154,  130,
                97,  166,  117,  231,  243,  210,  117,  117,
                117, 190,   36,  190,  178,   93,   20,  170,
                130, 202,   73,   20,   12,   53,   85,  194,
                146, 206,  130,  117,   85,  166,  182,  215
                };

                // Act
                image.Mutate(x => x.HistogramEqualization(new HistogramEqualizationOptions
                {
                    LuminanceLevels = luminanceLevels
                }));

                // Assert
                for (int y = 0; y < 8; y++)
                {
                    for (int x = 0; x < 8; x++)
                    {
                        Rgba32 actual = image[x, y];
                        Assert.Equal(expected[(y * 8) + x], actual.R);
                        Assert.Equal(expected[(y * 8) + x], actual.G);
                        Assert.Equal(expected[(y * 8) + x], actual.B);
                    }
                }
            }
        }

        [Theory]
        [WithFile(TestImages.Jpeg.Baseline.LowContrast, PixelTypes.Rgba32)]
        public void Adaptive_SlidingWindow_15Tiles_WithClipping<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                var options = new HistogramEqualizationOptions
                {
                    Method = HistogramEqualizationMethod.AdaptiveSlidingWindow,
                    LuminanceLevels = 256,
                    ClipHistogram = true,
                    NumberOfTiles = 15
                };
                image.Mutate(x => x.HistogramEqualization(options));
                image.DebugSave(provider);
                image.CompareToReferenceOutput(ValidatorComparer, provider);
            }
        }

        [Theory]
        [WithFile(TestImages.Jpeg.Baseline.LowContrast, PixelTypes.Rgba32)]
        public void Adaptive_TileInterpolation_10Tiles_WithClipping<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                var options = new HistogramEqualizationOptions
                {
                    Method = HistogramEqualizationMethod.AdaptiveTileInterpolation,
                    LuminanceLevels = 256,
                    ClipHistogram = true,
                    NumberOfTiles = 10
                };
                image.Mutate(x => x.HistogramEqualization(options));
                image.DebugSave(provider);
                image.CompareToReferenceOutput(ValidatorComparer, provider);
            }
        }
    }
}
