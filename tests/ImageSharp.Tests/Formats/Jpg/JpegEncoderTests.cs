// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.Metadata.Profiles.Iptc;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests.TestUtilities;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Jpg
{
    [Collection("RunSerial")]
    [Trait("Format", "Jpg")]
    public class JpegEncoderTests
    {
        private static JpegEncoder JpegEncoder => new JpegEncoder();

        private static JpegDecoder JpegDecoder => new JpegDecoder();

        public static readonly TheoryData<string, int> QualityFiles =
            new TheoryData<string, int>
            {
                { TestImages.Jpeg.Baseline.Calliphora, 80 },
                { TestImages.Jpeg.Progressive.Fb, 75 }
            };

        public static readonly TheoryData<JpegColorType, int> BitsPerPixel_Quality =
            new TheoryData<JpegColorType, int>
            {
                { JpegColorType.YCbCrRatio420, 40 },
                { JpegColorType.YCbCrRatio420, 60 },
                { JpegColorType.YCbCrRatio420, 100 },
                { JpegColorType.YCbCrRatio444, 40 },
                { JpegColorType.YCbCrRatio444, 60 },
                { JpegColorType.YCbCrRatio444, 100 },
                { JpegColorType.Rgb, 40 },
                { JpegColorType.Rgb, 60 },
                { JpegColorType.Rgb, 100 }
            };

        public static readonly TheoryData<int> Grayscale_Quality =
            new TheoryData<int>
            {
                { 40 },
                { 60 },
                { 100 }
            };

        public static readonly TheoryData<string, int, int, PixelResolutionUnit> RatioFiles =
            new TheoryData<string, int, int, PixelResolutionUnit>
            {
                { TestImages.Jpeg.Baseline.Ratio1x1, 1, 1, PixelResolutionUnit.AspectRatio },
                { TestImages.Jpeg.Baseline.Snake, 300, 300, PixelResolutionUnit.PixelsPerInch },
                { TestImages.Jpeg.Baseline.GammaDalaiLamaGray, 72, 72, PixelResolutionUnit.PixelsPerInch }
            };

        [Theory]
        [WithFile(TestImages.Jpeg.Baseline.Floorplan, PixelTypes.Rgba32, JpegColorType.Luminance)]
        [WithFile(TestImages.Jpeg.Baseline.Jpeg444, PixelTypes.Rgba32, JpegColorType.YCbCrRatio444)]
        [WithFile(TestImages.Jpeg.Baseline.Jpeg420Small, PixelTypes.Rgba32, JpegColorType.YCbCrRatio420)]
        [WithFile(TestImages.Jpeg.Baseline.JpegRgb, PixelTypes.Rgba32, JpegColorType.Rgb)]
        public void Encode_PreservesColorType<TPixel>(TestImageProvider<TPixel> provider, JpegColorType expectedColorType)
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
        [WithFile(TestImages.Jpeg.Baseline.Cmyk, PixelTypes.Rgba32)]
        [WithFile(TestImages.Jpeg.Baseline.Jpeg410, PixelTypes.Rgba32)]
        [WithFile(TestImages.Jpeg.Baseline.Jpeg411, PixelTypes.Rgba32)]
        [WithFile(TestImages.Jpeg.Baseline.Jpeg422, PixelTypes.Rgba32)]
        public void Encode_WithUnsupportedColorType_FromInputImage_DefaultsToYCbCr420<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            // arrange
            using Image<TPixel> input = provider.GetImage(JpegDecoder);
            using var memoryStream = new MemoryStream();

            // act
            input.Save(memoryStream, new JpegEncoder()
            {
                Quality = 75
            });

            // assert
            memoryStream.Position = 0;
            using var output = Image.Load<Rgba32>(memoryStream);
            JpegMetadata meta = output.Metadata.GetJpegMetadata();
            Assert.Equal(JpegColorType.YCbCrRatio420, meta.ColorType);
        }

        [Theory]
        [InlineData(JpegColorType.Cmyk)]
        [InlineData(JpegColorType.YCbCrRatio410)]
        [InlineData(JpegColorType.YCbCrRatio411)]
        [InlineData(JpegColorType.YCbCrRatio422)]
        public void Encode_WithUnsupportedColorType_DefaultsToYCbCr420(JpegColorType colorType)
        {
            // arrange
            var jpegEncoder = new JpegEncoder() { ColorType = colorType };
            using var input = new Image<Rgb24>(10, 10);
            using var memoryStream = new MemoryStream();

            // act
            input.Save(memoryStream, jpegEncoder);

            // assert
            memoryStream.Position = 0;
            using var output = Image.Load<Rgba32>(memoryStream);
            JpegMetadata meta = output.Metadata.GetJpegMetadata();
            Assert.Equal(JpegColorType.YCbCrRatio420, meta.ColorType);
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
        public void EncodeBaseline_CalliphoraPartial<TPixel>(TestImageProvider<TPixel> provider, JpegColorType colorType, int quality)
            where TPixel : unmanaged, IPixel<TPixel> => TestJpegEncoderCore(provider, colorType, quality);

        [Theory]
        [WithFile(TestImages.Png.CalliphoraPartial, nameof(BitsPerPixel_Quality), PixelTypes.Rgba32)]
        [WithTestPatternImages(nameof(BitsPerPixel_Quality), 158, 24, PixelTypes.Rgba32)]
        [WithTestPatternImages(nameof(BitsPerPixel_Quality), 153, 21, PixelTypes.Rgba32)]
        [WithTestPatternImages(nameof(BitsPerPixel_Quality), 600, 400, PixelTypes.Rgba32)]
        [WithTestPatternImages(nameof(BitsPerPixel_Quality), 138, 24, PixelTypes.Rgba32)]
        public void EncodeBaseline_WorksWithDifferentSizes<TPixel>(TestImageProvider<TPixel> provider, JpegColorType colorType, int quality)
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
        public void EncodeBaseline_WithSmallImages_WorksWithDifferentSizes<TPixel>(TestImageProvider<TPixel> provider, JpegColorType colorType, int quality)
            where TPixel : unmanaged, IPixel<TPixel> => TestJpegEncoderCore(provider, colorType, quality, comparer: ImageComparer.Tolerant(0.12f));

        [Theory]
        [WithFile(TestImages.Png.BikeGrayscale, nameof(Grayscale_Quality), PixelTypes.L8)]
        [WithSolidFilledImages(1, 1, 100, 100, 100, 255, PixelTypes.Rgba32, 100)]
        [WithSolidFilledImages(1, 1, 100, 100, 100, 255, PixelTypes.L8, 100)]
        [WithSolidFilledImages(1, 1, 100, 100, 100, 255, PixelTypes.L16, 100)]
        [WithSolidFilledImages(1, 1, 100, 100, 100, 255, PixelTypes.La16, 100)]
        [WithSolidFilledImages(1, 1, 100, 100, 100, 255, PixelTypes.La32, 100)]
        public void EncodeBaseline_Grayscale<TPixel>(TestImageProvider<TPixel> provider, int quality)
            where TPixel : unmanaged, IPixel<TPixel> => TestJpegEncoderCore(provider, JpegColorType.Luminance, quality);

        [Theory]
        [WithTestPatternImages(nameof(BitsPerPixel_Quality), 96, 96, PixelTypes.Rgba32 | PixelTypes.Bgra32)]
        public void EncodeBaseline_IsNotBoundToSinglePixelType<TPixel>(TestImageProvider<TPixel> provider, JpegColorType colorType, int quality)
            where TPixel : unmanaged, IPixel<TPixel> => TestJpegEncoderCore(provider, colorType, quality);

        [Theory]
        [WithTestPatternImages(nameof(BitsPerPixel_Quality), 48, 48, PixelTypes.Rgba32 | PixelTypes.Bgra32)]
        public void EncodeBaseline_WithSmallImages_IsNotBoundToSinglePixelType<TPixel>(TestImageProvider<TPixel> provider, JpegColorType colorType, int quality)
            where TPixel : unmanaged, IPixel<TPixel> => TestJpegEncoderCore(provider, colorType, quality, comparer: ImageComparer.Tolerant(0.06f));

        [Theory]
        [WithFile(TestImages.Png.CalliphoraPartial, PixelTypes.Rgba32, JpegColorType.YCbCrRatio444)]
        [WithTestPatternImages(587, 821, PixelTypes.Rgba32, JpegColorType.YCbCrRatio444)]
        [WithTestPatternImages(677, 683, PixelTypes.Bgra32, JpegColorType.YCbCrRatio420)]
        [WithSolidFilledImages(400, 400, "Red", PixelTypes.Bgr24, JpegColorType.YCbCrRatio420)]
        public void EncodeBaseline_WorksWithDiscontiguousBuffers<TPixel>(TestImageProvider<TPixel> provider, JpegColorType colorType)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            ImageComparer comparer = colorType == JpegColorType.YCbCrRatio444
                ? ImageComparer.TolerantPercentage(0.1f)
                : ImageComparer.TolerantPercentage(5f);

            provider.LimitAllocatorBufferCapacity().InBytesSqrt(200);
            TestJpegEncoderCore(provider, colorType, 100, comparer);
        }

        /// <summary>
        /// Anton's SUPER-SCIENTIFIC tolerance threshold calculation
        /// </summary>
        private static ImageComparer GetComparer(int quality, JpegColorType? colorType)
        {
            float tolerance = 0.015f; // ~1.5%

            if (quality < 50)
            {
                tolerance *= 4.5f;
            }
            else if (quality < 75 || colorType == JpegColorType.YCbCrRatio420)
            {
                tolerance *= 2.0f;
                if (colorType == JpegColorType.YCbCrRatio420)
                {
                    tolerance *= 2.0f;
                }
            }

            return ImageComparer.Tolerant(tolerance);
        }

        private static void TestJpegEncoderCore<TPixel>(
            TestImageProvider<TPixel> provider,
            JpegColorType colorType = JpegColorType.YCbCrRatio420,
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

        [Fact]
        public void Quality_0_And_1_Are_Identical()
        {
            var options = new JpegEncoder
            {
                Quality = 0
            };

            var testFile = TestFile.Create(TestImages.Jpeg.Baseline.Calliphora);

            using (Image<Rgba32> input = testFile.CreateRgba32Image())
            using (var memStream0 = new MemoryStream())
            using (var memStream1 = new MemoryStream())
            {
                input.SaveAsJpeg(memStream0, options);

                options.Quality = 1;
                input.SaveAsJpeg(memStream1, options);

                Assert.Equal(memStream0.ToArray(), memStream1.ToArray());
            }
        }

        [Fact]
        public void Quality_0_And_100_Are_Not_Identical()
        {
            var options = new JpegEncoder
            {
                Quality = 0
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

        [Fact]
        public void Encode_PreservesIptcProfile()
        {
            // arrange
            using var input = new Image<Rgba32>(1, 1);
            input.Metadata.IptcProfile = new IptcProfile();
            input.Metadata.IptcProfile.SetValue(IptcTag.Byline, "unit_test");

            // act
            using var memStream = new MemoryStream();
            input.Save(memStream, JpegEncoder);

            // assert
            memStream.Position = 0;
            using var output = Image.Load<Rgba32>(memStream);
            IptcProfile actual = output.Metadata.IptcProfile;
            Assert.NotNull(actual);
            IEnumerable<IptcValue> values = input.Metadata.IptcProfile.Values;
            Assert.Equal(values, actual.Values);
        }

        [Fact]
        public void Encode_PreservesExifProfile()
        {
            // arrange
            using var input = new Image<Rgba32>(1, 1);
            input.Metadata.ExifProfile = new ExifProfile();
            input.Metadata.ExifProfile.SetValue(ExifTag.Software, "unit_test");

            // act
            using var memStream = new MemoryStream();
            input.Save(memStream, JpegEncoder);

            // assert
            memStream.Position = 0;
            using var output = Image.Load<Rgba32>(memStream);
            ExifProfile actual = output.Metadata.ExifProfile;
            Assert.NotNull(actual);
            IReadOnlyList<IExifValue> values = input.Metadata.ExifProfile.Values;
            Assert.Equal(values, actual.Values);
        }

        [Fact]
        public void Encode_PreservesIccProfile()
        {
            // arrange
            using var input = new Image<Rgba32>(1, 1);
            input.Metadata.IccProfile = new IccProfile(IccTestDataProfiles.Profile_Random_Array);

            // act
            using var memStream = new MemoryStream();
            input.Save(memStream, JpegEncoder);

            // assert
            memStream.Position = 0;
            using var output = Image.Load<Rgba32>(memStream);
            IccProfile actual = output.Metadata.IccProfile;
            Assert.NotNull(actual);
            IccProfile values = input.Metadata.IccProfile;
            Assert.Equal(values.Entries, actual.Entries);
        }

        [Theory]
        [InlineData(JpegColorType.YCbCrRatio420)]
        [InlineData(JpegColorType.YCbCrRatio444)]
        public async Task Encode_IsCancellable(JpegColorType colorType)
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
    }
}
