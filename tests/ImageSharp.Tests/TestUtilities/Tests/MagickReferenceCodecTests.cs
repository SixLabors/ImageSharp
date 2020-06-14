// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using Xunit;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.TestUtilities.Tests
{
    using SixLabors.ImageSharp.PixelFormats;
    using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
    using SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs;

    using Xunit.Abstractions;

    public class MagickReferenceCodecTests
    {
        public MagickReferenceCodecTests(ITestOutputHelper output) => this.Output = output;

        private ITestOutputHelper Output { get; }

        public const PixelTypes PixelTypesToTest32 = PixelTypes.Rgba32 | PixelTypes.Bgra32 | PixelTypes.Rgb24;

        public const PixelTypes PixelTypesToTest64 =
            PixelTypes.Rgba32 | PixelTypes.Rgb24 | PixelTypes.Rgba64 | PixelTypes.Rgb48;

        public const PixelTypes PixelTypesToTest48 =
            PixelTypes.Rgba32 | PixelTypes.Rgba64 | PixelTypes.Rgb48;

        [Theory]
        [WithBlankImages(1, 1, PixelTypesToTest32, TestImages.Png.Splash)]
        [WithBlankImages(1, 1, PixelTypesToTest32, TestImages.Png.Indexed)]
        public void MagickDecode_8BitDepthImage_IsEquivalentTo_SystemDrawingResult<TPixel>(TestImageProvider<TPixel> dummyProvider, string testImage)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            string path = TestFile.GetInputFileFullPath(testImage);

            var magickDecoder = new MagickReferenceDecoder();
            var sdDecoder = new SystemDrawingReferenceDecoder();

            ImageComparer comparer = ImageComparer.Exact;

            using (var mImage = Image.Load<TPixel>(path, magickDecoder))
            using (var sdImage = Image.Load<TPixel>(path, sdDecoder))
            {
                ImageSimilarityReport<TPixel, TPixel> report = comparer.CompareImagesOrFrames(mImage, sdImage);

                mImage.DebugSave(dummyProvider);

                if (TestEnvironment.IsWindows)
                {
                    Assert.True(report.IsEmpty);
                }
            }
        }

        [Theory]
        [WithBlankImages(1, 1, PixelTypesToTest64, TestImages.Png.Rgba64Bpp)]
        [WithBlankImages(1, 1, PixelTypesToTest48, TestImages.Png.Rgb48Bpp)]
        [WithBlankImages(1, 1, PixelTypesToTest48, TestImages.Png.Rgb48BppInterlaced)]
        [WithBlankImages(1, 1, PixelTypesToTest48, TestImages.Png.Rgb48BppTrans)]
        [WithBlankImages(1, 1, PixelTypesToTest48, TestImages.Png.L16Bit)]
        public void MagickDecode_16BitDepthImage_IsApproximatelyEquivalentTo_SystemDrawingResult<TPixel>(TestImageProvider<TPixel> dummyProvider, string testImage)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            string path = TestFile.GetInputFileFullPath(testImage);

            var magickDecoder = new MagickReferenceDecoder();
            var sdDecoder = new SystemDrawingReferenceDecoder();

            // 1020 == 4 * 255 (Equivalent to manhattan distance of 1+1+1+1=4 in Rgba32 space)
            var comparer = ImageComparer.TolerantPercentage(1, 1020);

            using (var mImage = Image.Load<TPixel>(path, magickDecoder))
            using (var sdImage = Image.Load<TPixel>(path, sdDecoder))
            {
                ImageSimilarityReport<TPixel, TPixel> report = comparer.CompareImagesOrFrames(mImage, sdImage);

                mImage.DebugSave(dummyProvider);

                if (TestEnvironment.IsWindows)
                {
                    Assert.True(report.IsEmpty);
                }
            }
        }
    }
}
