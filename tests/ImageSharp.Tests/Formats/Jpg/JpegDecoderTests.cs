// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests
{
    using System;
    using System.IO;
    using System.Linq;

    using SixLabors.ImageSharp.Formats;
    using SixLabors.ImageSharp.Formats.Jpeg.GolangPort;
    using SixLabors.ImageSharp.PixelFormats;
    using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

    using Xunit;
    using Xunit.Abstractions;

    public class JpegDecoderTests
    {
        public static string[] BaselineTestJpegs =
            {
                TestImages.Jpeg.Baseline.Calliphora, TestImages.Jpeg.Baseline.Cmyk,
                TestImages.Jpeg.Baseline.Jpeg400, TestImages.Jpeg.Baseline.Jpeg444,
                TestImages.Jpeg.Baseline.Testimgorig,
                TestImages.Jpeg.Baseline.Bad.BadEOF,
                TestImages.Jpeg.Baseline.Bad.ExifUndefType,
            };

        public static string[] ProgressiveTestJpegs =
            {
                TestImages.Jpeg.Progressive.Fb, TestImages.Jpeg.Progressive.Progress,
                TestImages.Jpeg.Progressive.Festzug, TestImages.Jpeg.Progressive.Bad.BadEOF
            };

        public const PixelTypes CommonNonDefaultPixelTypes = PixelTypes.Rgba32 | PixelTypes.Argb32 | PixelTypes.RgbaVector;

        // TODO: We should make this comparer less tolerant ...
        private static readonly ImageComparer VeryTolerantJpegComparer =
            ImageComparer.Tolerant(0.005f, pixelThresholdHammingDistance: 4);

        public JpegDecoderTests(ITestOutputHelper output)
        {
            this.Output = output;
        }

        private ITestOutputHelper Output { get; }

        private static IImageDecoder OldJpegDecoder => new OldJpegDecoder();

        private static IImageDecoder PdfJsJpegDecoder => new JpegDecoder();
        
        [Theory]
        [WithFileCollection(nameof(BaselineTestJpegs), PixelTypes.Rgba32)]
        public void DecodeBaselineJpeg<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(PdfJsJpegDecoder))
            {
                image.DebugSave(provider);

                image.CompareToReferenceOutput(provider, VeryTolerantJpegComparer, appendPixelTypeToFileName: false);
            }
        }

        [Theory]
        [WithFile(TestImages.Jpeg.Baseline.Calliphora, CommonNonDefaultPixelTypes, false)]
        [WithFile(TestImages.Jpeg.Baseline.Calliphora, CommonNonDefaultPixelTypes, true)]
        public void JpegDecoder_IsNotBoundToSinglePixelType<TPixel>(TestImageProvider<TPixel> provider, bool useOldDecoder)
            where TPixel : struct, IPixel<TPixel>
        {
            IImageDecoder decoder = useOldDecoder ? OldJpegDecoder : PdfJsJpegDecoder;
            using (Image<TPixel> image = provider.GetImage(decoder))
            {
                image.DebugSave(provider);

                provider.Utility.TestName = nameof(this.DecodeBaselineJpeg);
                image.CompareToReferenceOutput(provider, VeryTolerantJpegComparer, appendPixelTypeToFileName: false);
            }
        }

        [Theory]
        [WithFileCollection(nameof(BaselineTestJpegs), PixelTypes.Rgba32)]
        public void DecodeBaselineJpeg_Old<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(OldJpegDecoder))
            {
                image.DebugSave(provider);

                provider.Utility.TestName = nameof(this.DecodeBaselineJpeg);
                image.CompareToReferenceOutput(provider, VeryTolerantJpegComparer, appendPixelTypeToFileName: false);
            }
        }
        
        [Theory]
        [WithFileCollection(nameof(ProgressiveTestJpegs), PixelTypes.Rgba32)]
        public void DecodeProgressiveJpeg<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(PdfJsJpegDecoder))
            {
                image.DebugSave(provider);

                image.CompareToReferenceOutput(provider, VeryTolerantJpegComparer, appendPixelTypeToFileName: false);
            }
        }

        [Theory]
        [WithFileCollection(nameof(ProgressiveTestJpegs), PixelTypes.Rgba32)]
        public void DecodeProgressiveJpeg_Old<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(OldJpegDecoder))
            {
                image.DebugSave(provider);

                provider.Utility.TestName = nameof(this.DecodeProgressiveJpeg);
                image.CompareToReferenceOutput(provider, VeryTolerantJpegComparer, appendPixelTypeToFileName: false);
            }
        }
        
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

        private void CompareJpegDecodersImpl<TPixel>(TestImageProvider<TPixel> provider, string testName)
            where TPixel : struct, IPixel<TPixel>
        {
            this.Output.WriteLine(provider.SourceFileOrDescription);
            provider.Utility.TestName = testName;

            using (Image<TPixel> image = provider.GetImage(OldJpegDecoder))
            {
                double d = this.GetDifferenceInPercents(image, provider);
                this.Output.WriteLine($"Difference using ORIGINAL decoder: {d:0.0000}%");
            }

            using (Image<TPixel> image = provider.GetImage(PdfJsJpegDecoder))
            {
                double d = this.GetDifferenceInPercents(image, provider);
                this.Output.WriteLine($"Difference using PDFJS decoder: {d:0.0000}%");
            }
        }

        [Theory]
        [WithFileCollection(nameof(BaselineTestJpegs), PixelTypes.Rgba32)]
        public void CompareJpegDecoders_Baseline<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            this.CompareJpegDecodersImpl(provider, nameof(this.DecodeBaselineJpeg));
        }

        [Theory]
        [WithFileCollection(nameof(ProgressiveTestJpegs), PixelTypes.Rgba32)]
        public void CompareJpegDecoders_Progressive<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            this.CompareJpegDecodersImpl(provider, nameof(this.DecodeProgressiveJpeg));
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
                var encoder = new JpegEncoder { Subsample = subsample, Quality = quality };

                data = new byte[65536];
                using (var ms = new MemoryStream(data))
                {
                    image.Save(ms, encoder);
                }
            }

            var mirror = Image.Load<TPixel>(data);
            mirror.DebugSave(provider, $"_{subsample}_Q{quality}");
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