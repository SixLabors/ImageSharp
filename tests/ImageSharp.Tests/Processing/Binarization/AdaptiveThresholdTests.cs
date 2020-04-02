// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Formats.Tga;
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
            var expectedThresholdLimit = .85f;
            Color expectedUpper = Color.White;
            Color expectedLower = Color.Black;
            this.operations.AdaptiveThreshold();
            AdaptiveThresholdProcessor p = this.Verify<AdaptiveThresholdProcessor>();
            Assert.Equal(expectedThresholdLimit, p.ThresholdLimit);
            Assert.Equal(expectedUpper, p.Upper);
            Assert.Equal(expectedLower, p.Lower);
        }

        [Fact]
        public void AdaptiveThreshold_SettingThresholdLimit_Works()
        {
            var expectedThresholdLimit = .65f;
            this.operations.AdaptiveThreshold(expectedThresholdLimit);
            AdaptiveThresholdProcessor p = this.Verify<AdaptiveThresholdProcessor>();
            Assert.Equal(expectedThresholdLimit, p.ThresholdLimit);
            Assert.Equal(Color.White, p.Upper);
            Assert.Equal(Color.Black, p.Lower);
        }

        [Fact]
        public void AdaptiveThreshold_SettingUpperLowerThresholds_Works()
        {
            Color expectedUpper = Color.HotPink;
            Color expectedLower = Color.Yellow;
            this.operations.AdaptiveThreshold(expectedUpper, expectedLower);
            AdaptiveThresholdProcessor p = this.Verify<AdaptiveThresholdProcessor>();
            Assert.Equal(expectedUpper, p.Upper);
            Assert.Equal(expectedLower, p.Lower);
        }

        [Fact]
        public void AdaptiveThreshold_SettingUpperLowerWithThresholdLimit_Works()
        {
            var expectedThresholdLimit = .77f;
            Color expectedUpper = Color.HotPink;
            Color expectedLower = Color.Yellow;
            this.operations.AdaptiveThreshold(expectedUpper, expectedLower, expectedThresholdLimit);
            AdaptiveThresholdProcessor p = this.Verify<AdaptiveThresholdProcessor>();
            Assert.Equal(expectedThresholdLimit, p.ThresholdLimit);
            Assert.Equal(expectedUpper, p.Upper);
            Assert.Equal(expectedLower, p.Lower);
        }

        [Theory]
        [WithFile(TestImages.Png.Bradley01, PixelTypes.Rgba32)]
        [WithFile(TestImages.Png.Bradley02, PixelTypes.Rgba32)]
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
    }
}
