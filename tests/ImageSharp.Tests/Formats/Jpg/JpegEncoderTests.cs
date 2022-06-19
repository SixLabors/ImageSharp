// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests.TestUtilities;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Jpg
{
    [Trait("Format", "Jpg")]
    public partial class JpegEncoderTests
    {
        private static JpegEncoder JpegEncoder => new();

        private static JpegDecoder JpegDecoder => new();

        public static readonly TheoryData<string, int> QualityFiles =
            new()
            {
                { TestImages.Jpeg.Baseline.Calliphora, 80 },
                { TestImages.Jpeg.Progressive.Fb, 75 }
            };

        public static readonly TheoryData<JpegEncodingColor, int> BitsPerPixel_Quality =
            new()
            {
                { JpegEncodingColor.YCbCrRatio420, 40 },
                { JpegEncodingColor.YCbCrRatio420, 60 },
                { JpegEncodingColor.YCbCrRatio420, 100 },
                { JpegEncodingColor.YCbCrRatio444, 40 },
                { JpegEncodingColor.YCbCrRatio444, 60 },
                { JpegEncodingColor.YCbCrRatio444, 100 },
                { JpegEncodingColor.Rgb, 40 },
                { JpegEncodingColor.Rgb, 60 },
                { JpegEncodingColor.Rgb, 100 }
            };

        public static readonly TheoryData<int> Grayscale_Quality =
            new()
            {
                { 40 },
                { 60 },
                { 100 }
            };

        public static readonly TheoryData<string, int, int, PixelResolutionUnit> RatioFiles =
            new()
            {
                { TestImages.Jpeg.Baseline.Ratio1x1, 1, 1, PixelResolutionUnit.AspectRatio },
                { TestImages.Jpeg.Baseline.Snake, 300, 300, PixelResolutionUnit.PixelsPerInch },
                { TestImages.Jpeg.Baseline.GammaDalaiLamaGray, 72, 72, PixelResolutionUnit.PixelsPerInch }
            };

        [Fact]
        public void Quality_1_And_100_Are_Not_Identical()
        {
            var options = new JpegEncoder
            {
                Quality = 1
            };

            var testFile = TestFile.Create(TestImages.Jpeg.Baseline.Calliphora);

            using (Image<Rgba32> input = testFile.CreateRgba32Image())
            using (var memStream0 = new MemoryStream())
            using (var memStream1 = new MemoryStream())
            {
                input.SaveAsJpeg(memStream0, options);

                options.Quality = 100;
                input.SaveAsJpeg(memStream1, options);

                Assert.NotEqual(memStream0.ToArray(), memStream1.ToArray());
            }
        }

        [Theory]
        [WithFile(TestImages.Jpeg.Baseline.Floorplan, PixelTypes.Rgba32, JpegEncodingColor.Luminance)]
        [WithFile(TestImages.Jpeg.Baseline.Jpeg444, PixelTypes.Rgba32, JpegEncodingColor.YCbCrRatio444)]
        [WithFile(TestImages.Jpeg.Baseline.Jpeg420Small, PixelTypes.Rgba32, JpegEncodingColor.YCbCrRatio420)]
        [WithFile(TestImages.Jpeg.Baseline.JpegRgb, PixelTypes.Rgba32, JpegEncodingColor.Rgb)]
        public void Encode_PreservesColorType<TPixel>(TestImageProvider<TPixel> provider, JpegEncodingColor expectedColorType)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            // arrange
            using Image<TPixel> input = provider.GetImage(JpegDecoder);
            using var memoryStream = new MemoryStream();

            // act
            input.Save(memoryStream, JpegEncoder);

            // assert
            memoryStream.Position = 0;
            using var output = Image.Load<Rgba32>(memoryStream);
            JpegMetadata meta = output.Metadata.GetJpegMetadata();
            Assert.Equal(expectedColorType, meta.ColorType);
        }

        [Theory]
        [MemberData(nameof(QualityFiles))]
        public void Encode_PreservesQuality(string imagePath, int quality)
        {
            var testFile = TestFile.Create(imagePath);
            using (Image<Rgba32> input = testFile.CreateRgba32Image())
            {
                using (var memStream = new MemoryStream())
                {
                    input.Save(memStream, JpegEncoder);

                    memStream.Position = 0;
                    using (var output = Image.Load<Rgba32>(memStream))
                    {
                        JpegMetadata meta = output.Metadata.GetJpegMetadata();
                        Assert.Equal(quality, meta.Quality);
                    }
                }
            }
        }

        [Theory]
        [WithFile(TestImages.Png.CalliphoraPartial, nameof(BitsPerPixel_Quality), PixelTypes.Rgba32)]
        public void EncodeBaseline_CalliphoraPartial<TPixel>(TestImageProvider<TPixel> provider, JpegEncodingColor colorType, int quality)
            where TPixel : unmanaged, IPixel<TPixel> => TestJpegEncoderCore(provider, colorType, quality);

        [Theory]
        [WithFile(TestImages.Png.CalliphoraPartial, nameof(BitsPerPixel_Quality), PixelTypes.Rgba32)]
        [WithTestPatternImages(nameof(BitsPerPixel_Quality), 158, 24, PixelTypes.Rgba32)]
        [WithTestPatternImages(nameof(BitsPerPixel_Quality), 153, 21, PixelTypes.Rgba32)]
        [WithTestPatternImages(nameof(BitsPerPixel_Quality), 600, 400, PixelTypes.Rgba32)]
        [WithTestPatternImages(nameof(BitsPerPixel_Quality), 138, 24, PixelTypes.Rgba32)]
        public void EncodeBaseline_WorksWithDifferentSizes<TPixel>(TestImageProvider<TPixel> provider, JpegEncodingColor colorType, int quality)
            where TPixel : unmanaged, IPixel<TPixel> => TestJpegEncoderCore(provider, colorType, quality);

        [Theory]
        [WithSolidFilledImages(nameof(BitsPerPixel_Quality), 1, 1, 100, 100, 100, 255, PixelTypes.L8)]
        [WithSolidFilledImages(nameof(BitsPerPixel_Quality), 1, 1, 255, 100, 50, 255, PixelTypes.Rgba32)]
        [WithTestPatternImages(nameof(BitsPerPixel_Quality), 143, 81, PixelTypes.Rgba32)]
        [WithTestPatternImages(nameof(BitsPerPixel_Quality), 7, 5, PixelTypes.Rgba32)]
        [WithTestPatternImages(nameof(BitsPerPixel_Quality), 96, 48, PixelTypes.Rgba32)]
        [WithTestPatternImages(nameof(BitsPerPixel_Quality), 73, 71, PixelTypes.Rgba32)]
        [WithTestPatternImages(nameof(BitsPerPixel_Quality), 48, 24, PixelTypes.Rgba32)]
        [WithTestPatternImages(nameof(BitsPerPixel_Quality), 46, 8, PixelTypes.Rgba32)]
        [WithTestPatternImages(nameof(BitsPerPixel_Quality), 51, 7, PixelTypes.Rgba32)]
        public void EncodeBaseline_WithSmallImages_WorksWithDifferentSizes<TPixel>(TestImageProvider<TPixel> provider, JpegEncodingColor colorType, int quality)
            where TPixel : unmanaged, IPixel<TPixel> => TestJpegEncoderCore(provider, colorType, quality, comparer: ImageComparer.Tolerant(0.15f));

        [Theory]
        [WithFile(TestImages.Png.BikeGrayscale, nameof(Grayscale_Quality), PixelTypes.L8)]
        [WithSolidFilledImages(1, 1, 100, 100, 100, 255, PixelTypes.Rgba32, 100)]
        [WithSolidFilledImages(1, 1, 100, 100, 100, 255, PixelTypes.L8, 100)]
        [WithSolidFilledImages(1, 1, 100, 100, 100, 255, PixelTypes.L16, 100)]
        [WithSolidFilledImages(1, 1, 100, 100, 100, 255, PixelTypes.La16, 100)]
        [WithSolidFilledImages(1, 1, 100, 100, 100, 255, PixelTypes.La32, 100)]
        public void EncodeBaseline_Grayscale<TPixel>(TestImageProvider<TPixel> provider, int quality)
            where TPixel : unmanaged, IPixel<TPixel> => TestJpegEncoderCore(provider, JpegEncodingColor.Luminance, quality);

        [Theory]
        [WithTestPatternImages(nameof(BitsPerPixel_Quality), 96, 96, PixelTypes.Rgba32 | PixelTypes.Bgra32)]
        public void EncodeBaseline_IsNotBoundToSinglePixelType<TPixel>(TestImageProvider<TPixel> provider, JpegEncodingColor colorType, int quality)
            where TPixel : unmanaged, IPixel<TPixel> => TestJpegEncoderCore(provider, colorType, quality);

        [Theory]
        [WithTestPatternImages(nameof(BitsPerPixel_Quality), 48, 48, PixelTypes.Rgba32 | PixelTypes.Bgra32)]
        public void EncodeBaseline_WithSmallImages_IsNotBoundToSinglePixelType<TPixel>(TestImageProvider<TPixel> provider, JpegEncodingColor colorType, int quality)
            where TPixel : unmanaged, IPixel<TPixel> => TestJpegEncoderCore(provider, colorType, quality, comparer: ImageComparer.Tolerant(0.06f));

        [Theory]
        [WithFile(TestImages.Png.CalliphoraPartial, PixelTypes.Rgba32, JpegEncodingColor.YCbCrRatio444)]
        [WithTestPatternImages(587, 821, PixelTypes.Rgba32, JpegEncodingColor.YCbCrRatio444)]
        [WithTestPatternImages(677, 683, PixelTypes.Bgra32, JpegEncodingColor.YCbCrRatio420)]
        [WithSolidFilledImages(400, 400, "Red", PixelTypes.Bgr24, JpegEncodingColor.YCbCrRatio420)]
        public void EncodeBaseline_WorksWithDiscontiguousBuffers<TPixel>(TestImageProvider<TPixel> provider, JpegEncodingColor colorType)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            ImageComparer comparer = colorType == JpegEncodingColor.YCbCrRatio444
                ? ImageComparer.TolerantPercentage(0.1f)
                : ImageComparer.TolerantPercentage(5f);

            provider.LimitAllocatorBufferCapacity().InBytesSqrt(200);
            TestJpegEncoderCore(provider, colorType, 100, comparer);
        }

        [Theory]
        [WithFile(TestImages.Jpeg.Baseline.Calliphora, PixelTypes.L8, JpegEncodingColor.Luminance)]
        [WithFile(TestImages.Jpeg.Baseline.Calliphora, PixelTypes.Rgb24, JpegEncodingColor.Rgb)]
        [WithFile(TestImages.Jpeg.Baseline.Calliphora, PixelTypes.Rgb24, JpegEncodingColor.Cmyk)]
        [WithFile(TestImages.Jpeg.Baseline.Calliphora, PixelTypes.Rgb24, JpegEncodingColor.YCbCrRatio444)]
        [WithFile(TestImages.Jpeg.Baseline.Calliphora, PixelTypes.Rgb24, JpegEncodingColor.YCbCrRatio422)]
        [WithFile(TestImages.Jpeg.Baseline.Calliphora, PixelTypes.Rgb24, JpegEncodingColor.YCbCrRatio420)]
        [WithFile(TestImages.Jpeg.Baseline.Calliphora, PixelTypes.Rgb24, JpegEncodingColor.YCbCrRatio411)]
        [WithFile(TestImages.Jpeg.Baseline.Calliphora, PixelTypes.Rgb24, JpegEncodingColor.YCbCrRatio410)]
        public void EncodeBaseline_WorksWithAllColorTypes<TPixel>(TestImageProvider<TPixel> provider, JpegEncodingColor colorType)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            // all reference output images are saved with quality=100
            const int quality = 100;

            using Image<TPixel> image = provider.GetImage();

            // There is no alpha in Jpeg!
            image.Mutate(c => c.MakeOpaque());

            var encoder = new JpegEncoder
            {
                Quality = quality,
                ColorType = colorType
            };
            string info = $"{colorType}-Q{quality}";

            ImageComparer comparer = GetComparer(quality, colorType);

            // Does DebugSave & load reference CompareToReferenceInput():
            image.VerifyEncoder(provider, "jpeg", info, encoder, comparer, referenceImageExtension: "jpg");
        }

        [Theory]
        [MemberData(nameof(RatioFiles))]
        public void Encode_PreserveRatio(string imagePath, int xResolution, int yResolution, PixelResolutionUnit resolutionUnit)
        {
            var testFile = TestFile.Create(imagePath);
            using (Image<Rgba32> input = testFile.CreateRgba32Image())
            {
                using (var memStream = new MemoryStream())
                {
                    input.Save(memStream, JpegEncoder);

                    memStream.Position = 0;
                    using (var output = Image.Load<Rgba32>(memStream))
                    {
                        ImageMetadata meta = output.Metadata;
                        Assert.Equal(xResolution, meta.HorizontalResolution);
                        Assert.Equal(yResolution, meta.VerticalResolution);
                        Assert.Equal(resolutionUnit, meta.ResolutionUnits);
                    }
                }
            }
        }

        [Theory]
        [InlineData(JpegEncodingColor.YCbCrRatio420)]
        [InlineData(JpegEncodingColor.YCbCrRatio444)]
        public async Task Encode_IsCancellable(JpegEncodingColor colorType)
        {
            var cts = new CancellationTokenSource();
            using var pausedStream = new PausedStream(new MemoryStream());
            pausedStream.OnWaiting(s =>
            {
                // after some writing
                if (s.Position >= 500)
                {
                    cts.Cancel();
                    pausedStream.Release();
                }
                else
                {
                    // allows this/next wait to unblock
                    pausedStream.Next();
                }
            });

            using var image = new Image<Rgba32>(5000, 5000);
            await Assert.ThrowsAsync<TaskCanceledException>(async () =>
            {
                var encoder = new JpegEncoder() { ColorType = colorType };
                await image.SaveAsync(pausedStream, encoder, cts.Token);
            });
        }

        /// <summary>
        /// Anton's SUPER-SCIENTIFIC tolerance threshold calculation
        /// </summary>
        private static ImageComparer GetComparer(int quality, JpegEncodingColor? colorType)
        {
            float tolerance = 0.015f; // ~1.5%

            if (quality < 50)
            {
                tolerance *= 4.5f;
            }
            else if (quality < 75 || colorType == JpegEncodingColor.YCbCrRatio420)
            {
                tolerance *= 2.0f;
                if (colorType == JpegEncodingColor.YCbCrRatio420)
                {
                    tolerance *= 2.0f;
                }
            }

            return ImageComparer.Tolerant(tolerance);
        }

        private static void TestJpegEncoderCore<TPixel>(
            TestImageProvider<TPixel> provider,
            JpegEncodingColor colorType = JpegEncodingColor.YCbCrRatio420,
            int quality = 100,
            ImageComparer comparer = null)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using Image<TPixel> image = provider.GetImage();

            // There is no alpha in Jpeg!
            image.Mutate(c => c.MakeOpaque());

            var encoder = new JpegEncoder
            {
                Quality = quality,
                ColorType = colorType
            };
            string info = $"{colorType}-Q{quality}";

            comparer ??= GetComparer(quality, colorType);

            // Does DebugSave & load reference CompareToReferenceInput():
            image.VerifyEncoder(provider, "jpeg", info, encoder, comparer, referenceImageExtension: "png");
        }
    }
}
