// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Binarization;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Binarization
{
    public class AdaptiveThresholdTests : BaseImageOperationsExtensionTest
    {
        [Fact]
        public void AdaptiveThreshold_UsesDefaults_Works()
        {
            // arrange
            var expectedThresholdLimit = .85f;
            Color expectedUpper = Color.White;
            Color expectedLower = Color.Black;

            // act
            this.operations.AdaptiveThreshold();

            // assert
            AdaptiveThresholdProcessor p = this.Verify<AdaptiveThresholdProcessor>();
            Assert.Equal(expectedThresholdLimit, p.ThresholdLimit);
            Assert.Equal(expectedUpper, p.Upper);
            Assert.Equal(expectedLower, p.Lower);
        }

        [Fact]
        public void AdaptiveThreshold_SettingThresholdLimit_Works()
        {
            // arrange
            var expectedThresholdLimit = .65f;

            // act
            this.operations.AdaptiveThreshold(expectedThresholdLimit);

            // assert
            AdaptiveThresholdProcessor p = this.Verify<AdaptiveThresholdProcessor>();
            Assert.Equal(expectedThresholdLimit, p.ThresholdLimit);
            Assert.Equal(Color.White, p.Upper);
            Assert.Equal(Color.Black, p.Lower);
        }

        [Fact]
        public void AdaptiveThreshold_SettingUpperLowerThresholds_Works()
        {
            // arrange
            Color expectedUpper = Color.HotPink;
            Color expectedLower = Color.Yellow;

            // act
            this.operations.AdaptiveThreshold(expectedUpper, expectedLower);

            // assert
            AdaptiveThresholdProcessor p = this.Verify<AdaptiveThresholdProcessor>();
            Assert.Equal(expectedUpper, p.Upper);
            Assert.Equal(expectedLower, p.Lower);
        }

        [Fact]
        public void AdaptiveThreshold_SettingUpperLowerWithThresholdLimit_Works()
        {
            // arrange
            var expectedThresholdLimit = .77f;
            Color expectedUpper = Color.HotPink;
            Color expectedLower = Color.Yellow;

            // act
            this.operations.AdaptiveThreshold(expectedUpper, expectedLower, expectedThresholdLimit);

            // assert
            AdaptiveThresholdProcessor p = this.Verify<AdaptiveThresholdProcessor>();
            Assert.Equal(expectedThresholdLimit, p.ThresholdLimit);
            Assert.Equal(expectedUpper, p.Upper);
            Assert.Equal(expectedLower, p.Lower);
        }

        [Fact]
        public void AdaptiveThreshold_SettingUpperLowerWithThresholdLimit_WithRectangle_Works()
        {
            // arrange
            var expectedThresholdLimit = .77f;
            Color expectedUpper = Color.HotPink;
            Color expectedLower = Color.Yellow;

            // act
            this.operations.AdaptiveThreshold(expectedUpper, expectedLower, expectedThresholdLimit, this.rect);

            // assert
            AdaptiveThresholdProcessor p = this.Verify<AdaptiveThresholdProcessor>(this.rect);
            Assert.Equal(expectedThresholdLimit, p.ThresholdLimit);
            Assert.Equal(expectedUpper, p.Upper);
            Assert.Equal(expectedLower, p.Lower);
        }

        [Theory]
        [WithFile(TestImages.Png.Bradley01, PixelTypes.Rgba32)]
        [WithFile(TestImages.Png.Bradley02, PixelTypes.Rgba32)]
        [WithFile(TestImages.Png.Ducky, PixelTypes.Rgba32)]
        public void AdaptiveThreshold_Works<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Mutate(img => img.AdaptiveThreshold());
                image.DebugSave(provider);
                image.CompareToReferenceOutput(ImageComparer.Exact, provider);
            }
        }

        [Theory]
        [WithFile(TestImages.Png.Bradley02, PixelTypes.Rgba32)]
        public void AdaptiveThreshold_WithRectangle_Works<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Mutate(img => img.AdaptiveThreshold(Color.White, Color.Black, new Rectangle(60, 90, 200, 30)));
                image.DebugSave(provider);
                image.CompareToReferenceOutput(ImageComparer.Exact, provider);
            }
        }
    }
}
