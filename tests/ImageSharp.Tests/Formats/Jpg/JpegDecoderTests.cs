// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.



// ReSharper disable InconsistentNaming

using System;

namespace SixLabors.ImageSharp.Tests.Formats.Jpg
{
    using System.IO;
    using System.Linq;

    using SixLabors.ImageSharp.Formats;
    using SixLabors.ImageSharp.Formats.Jpeg;
    using SixLabors.ImageSharp.Formats.Jpeg.GolangPort;
    using SixLabors.ImageSharp.Formats.Jpeg.PdfJsPort;
    using SixLabors.ImageSharp.PixelFormats;
    using SixLabors.ImageSharp.Tests.Formats.Jpg.Utils;
    using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
    using SixLabors.Primitives;

    using Xunit;
    using Xunit.Abstractions;

    public class JpegDecoderTests
    {
        public static string[] BaselineTestJpegs =
            {
                TestImages.Jpeg.Baseline.Calliphora,
                TestImages.Jpeg.Baseline.Cmyk,
                TestImages.Jpeg.Baseline.Ycck,
                TestImages.Jpeg.Baseline.Jpeg400,
                TestImages.Jpeg.Baseline.Testorig420,
                
                // BUG: The following image has a high difference compared to the expected output:
                //TestImages.Jpeg.Baseline.Jpeg420Small,

                TestImages.Jpeg.Baseline.Jpeg444,
                TestImages.Jpeg.Baseline.Bad.BadEOF,
                TestImages.Jpeg.Baseline.Bad.ExifUndefType,
            };

        public static string[] ProgressiveTestJpegs =
            {
                TestImages.Jpeg.Progressive.Fb, TestImages.Jpeg.Progressive.Progress,
                TestImages.Jpeg.Progressive.Festzug, TestImages.Jpeg.Progressive.Bad.BadEOF,
                TestImages.Jpeg.Issues.BadCoeffsProgressive178,
                TestImages.Jpeg.Issues.MissingFF00ProgressiveGirl159,
            };

        public const PixelTypes CommonNonDefaultPixelTypes = PixelTypes.Rgba32 | PixelTypes.Argb32 | PixelTypes.RgbaVector;

        // TODO: We should make this comparer less tolerant ...
        private static readonly ImageComparer VeryTolerantJpegComparer =
            ImageComparer.Tolerant(0.005f, perPixelManhattanThreshold: 4);

        // BUG: PDF.js output is wrong on spectral level!
        private static readonly ImageComparer PdfJsProgressiveComparer =
            ImageComparer.Tolerant(0.015f, perPixelManhattanThreshold: 4);


        public JpegDecoderTests(ITestOutputHelper output)
        {
            this.Output = output;
        }

        private ITestOutputHelper Output { get; }

        private static IImageDecoder OrigJpegDecoder => new OrigJpegDecoder();

        private static IImageDecoder PdfJsJpegDecoder => new PdfJsJpegDecoder();
        
        [Fact]
        public void ParseStream_BasicPropertiesAreCorrect1_PdfJs()
        {
            byte[] bytes = TestFile.Create(TestImages.Jpeg.Progressive.Progress).Bytes;
            using (var ms = new MemoryStream(bytes))
            {
                var decoder = new PdfJsJpegDecoderCore(Configuration.Default, new JpegDecoder());
                decoder.ParseStream(ms);

                VerifyJpeg.VerifyComponentSizes3(decoder.Frame.Components, 43, 61, 22, 31, 22, 31);
            }
        }
        
        public const string DecodeBaselineJpegOutputName = "DecodeBaselineJpeg";

        [Theory]
        [WithFileCollection(nameof(BaselineTestJpegs), PixelTypes.Rgba32)]
        public void DecodeBaselineJpeg_PdfJs<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(PdfJsJpegDecoder))
            {
                image.DebugSave(provider);

                provider.Utility.TestName = DecodeBaselineJpegOutputName;
                image.CompareToReferenceOutput(provider, VeryTolerantJpegComparer, appendPixelTypeToFileName: false);
            }
        }

        [Theory]
        [WithFile(TestImages.Jpeg.Baseline.Calliphora, CommonNonDefaultPixelTypes, false)]
        [WithFile(TestImages.Jpeg.Baseline.Calliphora, CommonNonDefaultPixelTypes, true)]
        public void JpegDecoder_IsNotBoundToSinglePixelType<TPixel>(TestImageProvider<TPixel> provider, bool useOldDecoder)
            where TPixel : struct, IPixel<TPixel>
        {
            IImageDecoder decoder = useOldDecoder ? OrigJpegDecoder : PdfJsJpegDecoder;
            using (Image<TPixel> image = provider.GetImage(decoder))
            {
                image.DebugSave(provider);

                provider.Utility.TestName = DecodeBaselineJpegOutputName;
                image.CompareToReferenceOutput(provider, VeryTolerantJpegComparer, appendPixelTypeToFileName: false);
            }
        }
        
        [Theory]
        [WithFileCollection(nameof(BaselineTestJpegs), PixelTypes.Rgba32)]
        public void DecodeBaselineJpeg_Orig<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(OrigJpegDecoder))
            {
                image.DebugSave(provider);

                provider.Utility.TestName = DecodeBaselineJpegOutputName;
                image.CompareToReferenceOutput(provider, VeryTolerantJpegComparer, appendPixelTypeToFileName: false);
            }
        }

        [Theory]
        [WithFile(TestImages.Jpeg.Issues.CriticalEOF214, PixelTypes.Rgba32)]
        public void DecodeBaselineJpeg_CriticalEOF_ShouldThrow_Orig<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            // TODO: We need a public ImageDecoderException class in ImageSharp!
            Assert.ThrowsAny<Exception>(() => provider.GetImage(OrigJpegDecoder));
        }

        public const string DecodeProgressiveJpegOutputName = "DecodeProgressiveJpeg";

        [Theory]
        [WithFileCollection(nameof(ProgressiveTestJpegs), PixelTypes.Rgba32)]
        public void DecodeProgressiveJpeg_PdfJs<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(PdfJsJpegDecoder))
            {
                image.DebugSave(provider);

                provider.Utility.TestName = DecodeProgressiveJpegOutputName;
                image.CompareToReferenceOutput(provider, PdfJsProgressiveComparer, appendPixelTypeToFileName: false);
            }
        }

        [Theory]
        [WithFileCollection(nameof(ProgressiveTestJpegs), PixelTypes.Rgba32)]
        public void DecodeProgressiveJpeg_Orig<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(OrigJpegDecoder))
            {
                image.DebugSave(provider);

                provider.Utility.TestName = DecodeProgressiveJpegOutputName;
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
                appendPixelTypeToFileName: false
                ).SingleOrDefault();

            if (report != null && report.TotalNormalizedDifference.HasValue)
            {
                return report.TotalNormalizedDifference.Value * 100;
            }

            return 0;
        }

        private void CompareJpegDecodersImpl<TPixel>(TestImageProvider<TPixel> provider, string testName)
            where TPixel : struct, IPixel<TPixel>
        {
            if (TestEnvironment.RunsOnCI) // Debug only test
            {
                return;
            }

            this.Output.WriteLine(provider.SourceFileOrDescription);
            provider.Utility.TestName = testName;

            using (Image<TPixel> image = provider.GetImage(OrigJpegDecoder))
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
            this.CompareJpegDecodersImpl(provider, DecodeBaselineJpegOutputName);
        }

        [Theory]
        [WithFileCollection(nameof(ProgressiveTestJpegs), PixelTypes.Rgba32)]
        public void CompareJpegDecoders_Progressive<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            this.CompareJpegDecodersImpl(provider, DecodeProgressiveJpegOutputName);
        }
        
        [Theory]
        [WithSolidFilledImages(16, 16, 255, 0, 0, PixelTypes.Rgba32, JpegSubsample.Ratio420, 75)]
        [WithSolidFilledImages(16, 16, 255, 0, 0, PixelTypes.Rgba32, JpegSubsample.Ratio420, 100)]
        [WithSolidFilledImages(16, 16, 255, 0, 0, PixelTypes.Rgba32, JpegSubsample.Ratio444, 75)]
        [WithSolidFilledImages(16, 16, 255, 0, 0, PixelTypes.Rgba32, JpegSubsample.Ratio444, 100)]
        [WithSolidFilledImages(8, 8, 255, 0, 0, PixelTypes.Rgba32, JpegSubsample.Ratio444, 100)]
        public void DecodeGenerated_Orig<TPixel>(
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

            var mirror = Image.Load<TPixel>(data, OrigJpegDecoder);
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
            using (Image<Rgba32> image = TestFile.Create(TestImages.Jpeg.Baseline.Jpeg420Exif).CreateImage())
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

        // DEBUG ONLY!
        // The PDF.js output should be saved by "tests\ImageSharp.Tests\Formats\Jpg\pdfjs\jpeg-converter.htm"
        // into "\tests\Images\ActualOutput\JpegDecoderTests\"
        //[Theory]
        //[WithFile(TestImages.Jpeg.Progressive.Progress, PixelTypes.Rgba32, "PdfJsOriginal_progress.png")]
        public void ValidateProgressivePdfJsOutput<TPixel>(TestImageProvider<TPixel> provider, 
            string pdfJsOriginalResultImage)
            where TPixel : struct, IPixel<TPixel>
        {
            // tests\ImageSharp.Tests\Formats\Jpg\pdfjs\jpeg-converter.htm
            string pdfJsOriginalResultPath = Path.Combine(
                provider.Utility.GetTestOutputDir(),
                pdfJsOriginalResultImage);

            byte[] sourceBytes = TestFile.Create(TestImages.Jpeg.Progressive.Progress).Bytes;

            provider.Utility.TestName = nameof(DecodeProgressiveJpegOutputName);

            var comparer = ImageComparer.Tolerant(0, 0);

            using (Image<TPixel> expectedImage = provider.GetReferenceOutputImage<TPixel>(appendPixelTypeToFileName: false))
            using (var pdfJsOriginalResult = Image.Load(pdfJsOriginalResultPath))
            using (var pdfJsPortResult = Image.Load(sourceBytes, PdfJsJpegDecoder))
            {
                ImageSimilarityReport originalReport = comparer.CompareImagesOrFrames(expectedImage, pdfJsOriginalResult);
                ImageSimilarityReport portReport = comparer.CompareImagesOrFrames(expectedImage, pdfJsPortResult);
                
                this.Output.WriteLine($"Difference for PDF.js ORIGINAL: {originalReport.DifferencePercentageString}");
                this.Output.WriteLine($"Difference for PORT: {portReport.DifferencePercentageString}");
            }
        }
    }
}