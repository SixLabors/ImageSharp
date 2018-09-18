// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.MetaData;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Jpg
{
    public class JpegEncoderTests
    {
        public static readonly TheoryData<string, int> QualityFiles =
        new TheoryData<string, int>
        {
            { TestImages.Jpeg.Baseline.Calliphora, 80},
            { TestImages.Jpeg.Progressive.Fb, 75 }
        };

        public static readonly TheoryData<JpegSubsample, int> BitsPerPixel_Quality =
        new TheoryData<JpegSubsample, int>
        {
            { JpegSubsample.Ratio420, 40 },
            { JpegSubsample.Ratio420, 60 },
            { JpegSubsample.Ratio420, 100 },

            { JpegSubsample.Ratio444, 40 },
            { JpegSubsample.Ratio444, 60 },
            { JpegSubsample.Ratio444, 100 },
        };

        public static readonly TheoryData<string, int, int, PixelResolutionUnit> RatioFiles =
        new TheoryData<string, int, int, PixelResolutionUnit>
        {
            { TestImages.Jpeg.Baseline.Ratio1x1, 1, 1 , PixelResolutionUnit.AspectRatio},
            { TestImages.Jpeg.Baseline.Snake, 300, 300 , PixelResolutionUnit.PixelsPerInch},
            { TestImages.Jpeg.Baseline.GammaDalaiLamaGray, 72, 72, PixelResolutionUnit.PixelsPerInch }
        };

        [Theory]
        [MemberData(nameof(QualityFiles))]
        public void Encode_PreserveQuality(string imagePath, int quality)
        {
            var options = new JpegEncoder();

            var testFile = TestFile.Create(imagePath);
            using (Image<Rgba32> input = testFile.CreateImage())
            {
                using (var memStream = new MemoryStream())
                {
                    input.Save(memStream, options);

                    memStream.Position = 0;
                    using (var output = Image.Load<Rgba32>(memStream))
                    {
                        JpegMetaData meta = output.MetaData.GetFormatMetaData(JpegFormat.Instance);
                        Assert.Equal(quality, meta.Quality);
                    }
                }
            }
        }

        [Theory]
        [WithFile(TestImages.Png.CalliphoraPartial, nameof(BitsPerPixel_Quality), PixelTypes.Rgba32)]
        [WithTestPatternImages(nameof(BitsPerPixel_Quality), 73, 71, PixelTypes.Rgba32)]
        [WithTestPatternImages(nameof(BitsPerPixel_Quality), 48, 24, PixelTypes.Rgba32)]
        [WithTestPatternImages(nameof(BitsPerPixel_Quality), 46, 8, PixelTypes.Rgba32)]
        [WithTestPatternImages(nameof(BitsPerPixel_Quality), 51, 7, PixelTypes.Rgba32)]
        [WithSolidFilledImages(nameof(BitsPerPixel_Quality), 1, 1, 255, 100, 50, 255, PixelTypes.Rgba32)]
        [WithTestPatternImages(nameof(BitsPerPixel_Quality), 7, 5, PixelTypes.Rgba32)]
        public void EncodeBaseline_WorksWithDifferentSizes<TPixel>(TestImageProvider<TPixel> provider, JpegSubsample subsample, int quality)
            where TPixel : struct, IPixel<TPixel> => TestJpegEncoderCore(provider, subsample, quality);

        [Theory]
        [WithTestPatternImages(nameof(BitsPerPixel_Quality), 48, 48, PixelTypes.Rgba32 | PixelTypes.Bgra32)]
        public void EncodeBaseline_IsNotBoundToSinglePixelType<TPixel>(TestImageProvider<TPixel> provider, JpegSubsample subsample, int quality)
            where TPixel : struct, IPixel<TPixel> => TestJpegEncoderCore(provider, subsample, quality);

        /// <summary>
        /// Anton's SUPER-SCIENTIFIC tolerance threshold calculation
        /// </summary>
        private static ImageComparer GetComparer(int quality, JpegSubsample subsample)
        {
            float tolerance = 0.015f; // ~1.5%

            if (quality < 50)
            {
                tolerance *= 10f;
            }
            else if (quality < 75 || subsample == JpegSubsample.Ratio420)
            {
                tolerance *= 5f;
                if (subsample == JpegSubsample.Ratio420)
                {
                    tolerance *= 2f;
                }
            }

            return ImageComparer.Tolerant(tolerance);
        }

        private static void TestJpegEncoderCore<TPixel>(
            TestImageProvider<TPixel> provider,
            JpegSubsample subsample,
            int quality = 100)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                // There is no alpha in Jpeg!
                image.Mutate(c => c.MakeOpaque());

                var encoder = new JpegEncoder()
                {
                    Subsample = subsample,
                    Quality = quality
                };
                string info = $"{subsample}-Q{quality}";
                ImageComparer comparer = GetComparer(quality, subsample);

                // Does DebugSave & load reference CompareToReferenceInput():
                image.VerifyEncoder(provider, "jpeg", info, encoder, comparer, referenceImageExtension: "png");
            }
        }

        [Fact]
        public void Quality_0_And_1_Are_Identical()
        {
            var options = new JpegEncoder
            {
                Quality = 0
            };

            var testFile = TestFile.Create(TestImages.Jpeg.Baseline.Calliphora);

            using (Image<Rgba32> input = testFile.CreateImage())
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

            using (Image<Rgba32> input = testFile.CreateImage())
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
            var options = new JpegEncoder();

            var testFile = TestFile.Create(imagePath);
            using (Image<Rgba32> input = testFile.CreateImage())
            {
                using (var memStream = new MemoryStream())
                {
                    input.Save(memStream, options);

                    memStream.Position = 0;
                    using (var output = Image.Load<Rgba32>(memStream))
                    {
                        ImageMetaData meta = output.MetaData;
                        Assert.Equal(xResolution, meta.HorizontalResolution);
                        Assert.Equal(yResolution, meta.VerticalResolution);
                        Assert.Equal(resolutionUnit, meta.ResolutionUnits);
                    }
                }
            }
        }
    }
}