// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

using Moq;

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    public class TestImageExtensionsTests
    {
        [Theory]
        [WithSolidFilledImages(10, 10, 0, 0, 255, PixelTypes.Rgba32)]
        public void CompareToReferenceOutput_WhenReferenceOutputMatches_ShouldNotThrow<TPixel>(
            TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.CompareToReferenceOutput(provider);
            }
        }

        [Theory]
        [WithSolidFilledImages(10, 10, 0, 0, 255, PixelTypes.Rgba32)]
        public void CompareToReferenceOutput_WhenReferenceOutputDoesNotMatch_Throws<TPixel>(
            TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                Assert.ThrowsAny<Exception>(() => image.CompareToReferenceOutput(provider));
            }
        }

        [Theory]
        [WithSolidFilledImages(10, 10, 0, 0, 255, PixelTypes.Rgba32)]
        public void CompareToReferenceOutput_DoNotAppendPixelType<TPixel>(
            TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.DebugSave(provider, appendPixelTypeToFileName: false);
                image.CompareToReferenceOutput(provider, appendPixelTypeToFileName: false);
            }
        }

        [Theory]
        [WithSolidFilledImages(10, 10, 0, 0, 255, PixelTypes.Rgba32)]
        public void CompareToReferenceOutput_WhenReferenceFileMissing_Throws<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                Assert.ThrowsAny<Exception>(() => image.CompareToReferenceOutput(provider));
            }
        }

        [Theory]
        [WithFile(TestImages.Png.CalliphoraPartial, PixelTypes.Rgba32)]
        public void CompareToOriginal_WhenSimilar<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                using (Image<TPixel> clone = image.Clone())
                {
                    clone.CompareToOriginal(provider, ImageComparer.Exact);
                }
            }
        }

        [Theory]
        [WithFile(TestImages.Png.CalliphoraPartial, PixelTypes.Rgba32)]
        public void CompareToOriginal_WhenDifferent_Throws<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                ImagingTestCaseUtility.ModifyPixel(image, 3, 1, 1);

                Assert.ThrowsAny<ImageDifferenceIsOverThresholdException>(() =>
                {
                    image.CompareToOriginal(provider, ImageComparer.Exact);
                });
            }
        }

        [Theory]
        [WithBlankImages(10, 10, PixelTypes.Rgba32)]
        public void CompareToOriginal_WhenInputIsNotFromFile_Throws<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                Assert.ThrowsAny<Exception>(() =>
                {
                    image.CompareToOriginal(provider, Mock.Of<ImageComparer>());
                });
            }
        }
    }
}
