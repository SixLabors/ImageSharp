// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.



// ReSharper disable InconsistentNaming

namespace SixLabors.ImageSharp.Tests.Formats.Jpg
{
    using System.Collections.Generic;
    using System.IO;

    using SixLabors.ImageSharp.Formats.Bmp;
    using SixLabors.ImageSharp.Formats.Jpeg;
    using SixLabors.ImageSharp.PixelFormats;
    using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
    using SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs;

    using Xunit;
    using Xunit.Abstractions;

    public class JpegEncoderTests
    {
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

        [Theory]
        [WithFile(TestImages.Png.CalliphoraPartial, nameof(BitsPerPixel_Quality), PixelTypes.Rgba32)]
        [WithTestPatternImages(nameof(BitsPerPixel_Quality), 73, 71, PixelTypes.Rgba32)]
        [WithTestPatternImages(nameof(BitsPerPixel_Quality), 48, 24, PixelTypes.Rgba32)]
        [WithTestPatternImages(nameof(BitsPerPixel_Quality), 46, 8, PixelTypes.Rgba32)]
        [WithTestPatternImages(nameof(BitsPerPixel_Quality), 51, 7, PixelTypes.Rgba32)]
        [WithSolidFilledImages(nameof(BitsPerPixel_Quality), 1, 1, 255, 100, 50, 255, PixelTypes.Rgba32)]
        [WithTestPatternImages(nameof(BitsPerPixel_Quality), 7, 5, PixelTypes.Rgba32)]
        public void EncodeBaseline_WorksWithDifferentSizes<TPixel>(TestImageProvider<TPixel> provider, JpegSubsample subsample, int quality)
            where TPixel : struct, IPixel<TPixel>
        {
            TestJpegEncoderCore(provider, subsample, quality);
        }

        [Theory]
        [WithTestPatternImages(nameof(BitsPerPixel_Quality), 48, 48, PixelTypes.Rgba32 | PixelTypes.Bgra32)]
        public void EncodeBaseline_IsNotBoundToSinglePixelType<TPixel>(TestImageProvider<TPixel> provider, JpegSubsample subsample, int quality)
            where TPixel : struct, IPixel<TPixel>
        {
            TestJpegEncoderCore(provider, subsample, quality);
        }

        private static ImageComparer GetComparer(int quality)
        {
            if (quality > 90)
            {
                return ImageComparer.Tolerant(0.0005f / 100);
            }
            else if (quality > 50)
            {
                return ImageComparer.Tolerant(0.005f / 100);
            }
            else
            {
                return ImageComparer.Tolerant(0.01f / 100);
            }
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
                ImageComparer comparer = GetComparer(quality);

                // Does DebugSave & load reference CompareToReferenceInput():
                image.VerifyEncoder(provider, "jpeg", info, encoder, comparer, referenceImageExtension: "png");
            }
        }
        

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void IgnoreMetadata_ControlsIfExifProfileIsWritten(bool ignoreMetaData)
        {
            var encoder = new JpegEncoder()
            {
                IgnoreMetadata = ignoreMetaData
            };
            
            using (Image<Rgba32> input = TestFile.Create(TestImages.Jpeg.Baseline.Floorplan).CreateImage())
            {
                using (var memStream = new MemoryStream())
                {
                    input.Save(memStream, encoder);

                    memStream.Position = 0;
                    using (var output = Image.Load<Rgba32>(memStream))
                    {
                        if (ignoreMetaData)
                        {
                            Assert.Null(output.MetaData.ExifProfile);
                        }
                        else
                        {
                            Assert.NotNull(output.MetaData.ExifProfile);
                        }
                    }
                }
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
    }
}