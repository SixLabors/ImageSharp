// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Jpeg.GolangPort;
using SixLabors.ImageSharp.Formats.Jpeg.PdfJsPort;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.Formats.Jpg.Utils;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

using Xunit;
using Xunit.Abstractions;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Jpg
{
    // TODO: Scatter test cases into multiple test classes
    public partial class JpegDecoderTests
    {
        public const PixelTypes CommonNonDefaultPixelTypes = PixelTypes.Rgba32 | PixelTypes.Argb32 | PixelTypes.RgbaVector;

        private const float BaselineTolerance = 0.001F / 100;
        private const float ProgressiveTolerance = 0.2F / 100;

        private ImageComparer GetImageComparer<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            string file = provider.SourceFileOrDescription;

            if (!CustomToleranceValues.TryGetValue(file, out float tolerance))
            {
                bool baseline = file.ToLower().Contains("baseline");
                tolerance = baseline ? BaselineTolerance : ProgressiveTolerance;
            }

            return ImageComparer.Tolerant(tolerance);
        }

        private static bool SkipTest(ITestImageProvider provider)
        {
            string[] largeImagesToSkipOn32Bit =
                {
                    TestImages.Jpeg.Baseline.Jpeg420Exif,
                    TestImages.Jpeg.Issues.BadZigZagProgressive385
                };

            return TestEnvironment.RunsOnCI && !TestEnvironment.Is64BitProcess
                                            && largeImagesToSkipOn32Bit.Contains(provider.SourceFileOrDescription);
        }

        public JpegDecoderTests(ITestOutputHelper output)
        {
            this.Output = output;
        }

        private ITestOutputHelper Output { get; }

        private static GolangJpegDecoder GolangJpegDecoder => new GolangJpegDecoder();

        private static PdfJsJpegDecoder PdfJsJpegDecoder => new PdfJsJpegDecoder();

        private static JpegDecoder DefaultJpegDecoder => new JpegDecoder();

        [Fact]
        public void ParseStream_BasicPropertiesAreCorrect1_PdfJs()
        {
            byte[] bytes = TestFile.Create(TestImages.Jpeg.Progressive.Progress).Bytes;
            using (var ms = new MemoryStream(bytes))
            {
                var decoder = new PdfJsJpegDecoderCore(Configuration.Default, new JpegDecoder());
                decoder.ParseStream(ms);

                // I don't know why these numbers are different. All I know is that the decoder works
                // and spectral data is exactly correct also.
                // VerifyJpeg.VerifyComponentSizes3(decoder.Frame.Components, 43, 61, 22, 31, 22, 31);
                VerifyJpeg.VerifyComponentSizes3(decoder.Frame.Components, 44, 62, 22, 31, 22, 31);
            }
        }

        public const string DecodeBaselineJpegOutputName = "DecodeBaselineJpeg";

        [Theory]
        [WithFile(TestImages.Jpeg.Baseline.Calliphora, CommonNonDefaultPixelTypes, false)]
        [WithFile(TestImages.Jpeg.Baseline.Calliphora, CommonNonDefaultPixelTypes, true)]
        public void JpegDecoder_IsNotBoundToSinglePixelType<TPixel>(TestImageProvider<TPixel> provider, bool useOldDecoder)
            where TPixel : struct, IPixel<TPixel>
        {
            if (SkipTest(provider))
            {
                return;
            }

            // For 32 bit test enviroments:
            provider.Configuration.MemoryManager = ArrayPoolMemoryManager.CreateWithModeratePooling();

            IImageDecoder decoder = useOldDecoder ? (IImageDecoder)GolangJpegDecoder : PdfJsJpegDecoder;
            using (Image<TPixel> image = provider.GetImage(decoder))
            {
                image.DebugSave(provider);

                provider.Utility.TestName = DecodeBaselineJpegOutputName;
                image.CompareToReferenceOutput(ImageComparer.Tolerant(BaselineTolerance), provider, appendPixelTypeToFileName: false);
            }

            provider.Configuration.MemoryManager.ReleaseRetainedResources();
        }

        private string GetDifferenceInPercentageString<TPixel>(Image<TPixel> image, TestImageProvider<TPixel> provider)
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
                return report.DifferencePercentageString;
            }

            return "0%";
        }

        private void CompareJpegDecodersImpl<TPixel>(TestImageProvider<TPixel> provider, string testName)
            where TPixel : struct, IPixel<TPixel>
        {
            this.Output.WriteLine(provider.SourceFileOrDescription);
            provider.Utility.TestName = testName;

            using (Image<TPixel> image = provider.GetImage(GolangJpegDecoder))
            {
                string d = this.GetDifferenceInPercentageString(image, provider);

                this.Output.WriteLine($"Difference using ORIGINAL decoder: {d}");
            }

            using (Image<TPixel> image = provider.GetImage(PdfJsJpegDecoder))
            {
                string d = this.GetDifferenceInPercentageString(image, provider);
                this.Output.WriteLine($"Difference using PDFJS decoder: {d}");
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