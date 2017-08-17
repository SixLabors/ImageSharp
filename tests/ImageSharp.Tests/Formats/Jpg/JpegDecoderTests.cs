// <copyright file="JpegDecoderTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

// ReSharper disable InconsistentNaming
namespace ImageSharp.Tests
{
    using System;
    using System.IO;
    using System.Linq;

    using ImageSharp.Formats;
    using ImageSharp.Formats.Jpeg.PdfJsPort;
    using ImageSharp.PixelFormats;
    using ImageSharp.Tests.TestUtilities.ImageComparison;

    using Xunit;
    using Xunit.Abstractions;

    public class JpegDecoderTests
    {
        public static string[] BaselineTestJpegs =
            {
                TestImages.Jpeg.Baseline.Calliphora, TestImages.Jpeg.Baseline.Cmyk,
                TestImages.Jpeg.Baseline.Jpeg400, TestImages.Jpeg.Baseline.Jpeg444,
                TestImages.Jpeg.Baseline.Testimgorig
            };

        public static string[] ProgressiveTestJpegs = TestImages.Jpeg.Progressive.All;

        // TODO: We should make this comparer less tolerant ...
        private static readonly ImageComparer VeryTolerantJpegComparer =
            ImageComparer.Tolerant(0.005f, pixelThresholdInPixelByteSum: 4);

        public JpegDecoderTests(ITestOutputHelper output)
        {
            this.Output = output;
        }

        private ITestOutputHelper Output { get; }

        private float GetDifferenceInPercents<TPixel>(Image<TPixel> image, TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            var reportingComparer = ImageComparer.Tolerant(0, 0);

            ImageSimilarityReport report = image.GetReferenceOutputSimilarityReports(
                provider,
                reportingComparer,
                appendPixelTypeToFileName: false).SingleOrDefault();

            if (report != null && report.TotalNormalizedDifference.HasValue)
            {
                return report.TotalNormalizedDifference.Value * 100;
            }

            return 0;
        }

        [Theory]
        [WithFileCollection(nameof(BaselineTestJpegs), PixelTypes.Rgba32)]
        public void CompareJpegDecoders<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            this.Output.WriteLine(provider.SourceFileOrDescription);
            provider.Utility.TestName = nameof(this.DecodeBaselineJpeg);

            using (Image<TPixel> image = provider.GetImage())
            {
                double d = this.GetDifferenceInPercents(image, provider);
                this.Output.WriteLine($"Difference using ORIGINAL decoder: {d:0.0000}%");
            }

            using (Image<TPixel> image = provider.GetImage(new PdfJsJpegDecoder()))
            {
                double d = this.GetDifferenceInPercents(image, provider);
                this.Output.WriteLine($"Difference using PDFJS decoder: {d:0.0000}%");
            }
        }

        [Theory]
        [WithFileCollection(nameof(BaselineTestJpegs), PixelTypes.Rgba32 | PixelTypes.Rgba32 | PixelTypes.Argb32)]
        public void DecodeBaselineJpeg<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.DebugSave(provider);
                image.CompareToReferenceOutput(provider, VeryTolerantJpegComparer, appendPixelTypeToFileName: false);
            }
        }
        
        [Theory]
        [WithFileCollection(nameof(BaselineTestJpegs), PixelTypes.Rgba32 | PixelTypes.Rgba32 | PixelTypes.Argb32)]
        public void DecodeBaselineJpeg_PdfJs<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new PdfJsJpegDecoder()))
            {
                image.DebugSave(provider);
                
                provider.Utility.TestName = nameof(this.DecodeBaselineJpeg);
                image.CompareToReferenceOutput(provider, VeryTolerantJpegComparer, appendPixelTypeToFileName: false);
            }
        }

        [Theory]
        [WithFileCollection(nameof(ProgressiveTestJpegs), PixelTypes.Rgba32 | PixelTypes.Rgba32 | PixelTypes.Argb32)]
        public void DecodeProgressiveJpeg<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.DebugSave(provider, VeryTolerantJpegComparer);
            }
        }

        [Theory]
        [WithSolidFilledImages(16, 16, 255, 0, 0, PixelTypes.Rgba32, JpegSubsample.Ratio420, 75)]
        [WithSolidFilledImages(16, 16, 255, 0, 0, PixelTypes.Rgba32, JpegSubsample.Ratio420, 100)]
        [WithSolidFilledImages(16, 16, 255, 0, 0, PixelTypes.Rgba32, JpegSubsample.Ratio444, 75)]
        [WithSolidFilledImages(16, 16, 255, 0, 0, PixelTypes.Rgba32, JpegSubsample.Ratio444, 100)]
        [WithSolidFilledImages(8, 8, 255, 0, 0, PixelTypes.Rgba32, JpegSubsample.Ratio444, 100)]
        public void DecodeGenerated<TPixel>(
            TestImageProvider<TPixel> provider,
            JpegSubsample subsample,
            int quality)
            where TPixel : struct, IPixel<TPixel>
        {
            byte[] data;
            using (Image<TPixel> image = provider.GetImage())
            {
                JpegEncoder encoder = new JpegEncoder { Subsample = subsample, Quality = quality };

                data = new byte[65536];
                using (MemoryStream ms = new MemoryStream(data))
                {
                    image.Save(ms, encoder);
                }
            }
            
            Image<TPixel> mirror = provider.Factory.CreateImage(data);
            mirror.DebugSave(provider, $"_{subsample}_Q{quality}");
        }

        [Theory]
        [WithSolidFilledImages(42, 88, 255, 0, 0, PixelTypes.Rgba32)]
        public void DecodeGenerated_MetadataOnly<TPixel>(
            TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    image.Save(ms, new JpegEncoder());
                    ms.Seek(0, SeekOrigin.Begin);
                    
                    using (JpegDecoderCore decoder = new JpegDecoderCore(null, new JpegDecoder()))
                    {
                        Image<TPixel> mirror = decoder.Decode<TPixel>(ms);

                        Assert.Equal(decoder.ImageWidth, image.Width);
                        Assert.Equal(decoder.ImageHeight, image.Height);
                    }
                }
            }
        }

        [Fact]
        public void Decoder_Reads_Correct_Resolution_From_Jfif()
        {
            using (Image<Rgba32> image = TestFile.Create(TestImages.Jpeg.Baseline.Floorplan).CreateImage())
            {
                Assert.Equal(300, image.MetaData.HorizontalResolution);
                Assert.Equal(300, image.MetaData.VerticalResolution);
            }
        }

        [Fact]
        public void Decoder_Reads_Correct_Resolution_From_Exif()
        {
            using (Image<Rgba32> image = TestFile.Create(TestImages.Jpeg.Baseline.Jpeg420).CreateImage())
            {
                Assert.Equal(72, image.MetaData.HorizontalResolution);
                Assert.Equal(72, image.MetaData.VerticalResolution);
            }
        }

        [Fact]
        public void Decode_IgnoreMetadataIsFalse_ExifProfileIsRead()
        {
            JpegDecoder decoder = new JpegDecoder()
            {
                IgnoreMetadata = false
            };

            TestFile testFile = TestFile.Create(TestImages.Jpeg.Baseline.Floorplan);

            using (Image<Rgba32> image = testFile.CreateImage(decoder))
            {
                Assert.NotNull(image.MetaData.ExifProfile);
            }
        }

        [Fact]
        public void Decode_IgnoreMetadataIsTrue_ExifProfileIgnored()
        {
            JpegDecoder options = new JpegDecoder()
            {
                IgnoreMetadata = true
            };

            TestFile testFile = TestFile.Create(TestImages.Jpeg.Baseline.Floorplan);

            using (Image<Rgba32> image = testFile.CreateImage(options))
            {
                Assert.Null(image.MetaData.ExifProfile);
            }
        }
    }
}