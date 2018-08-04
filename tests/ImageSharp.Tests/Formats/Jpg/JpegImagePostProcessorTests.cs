// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.Formats.Jpg.Utils;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

using Xunit;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests.Formats.Jpg
{
    public class JpegImagePostProcessorTests
    {
        public static string[] BaselineTestJpegs =
            {
                TestImages.Jpeg.Baseline.Calliphora,
                TestImages.Jpeg.Baseline.Cmyk,
                TestImages.Jpeg.Baseline.Ycck,
                TestImages.Jpeg.Baseline.Jpeg400,
                TestImages.Jpeg.Baseline.Testorig420,
                TestImages.Jpeg.Baseline.Jpeg444,
            };

        public JpegImagePostProcessorTests(ITestOutputHelper output)
        {
            this.Output = output;
        }

        private ITestOutputHelper Output { get; }

        private static void SaveBuffer<TPixel>(JpegComponentPostProcessor cp, TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<Rgba32> image = cp.ColorBuffer.ToGrayscaleImage(1f / 255f))
            {
                image.DebugSave(provider, $"-C{cp.Component.Index}-");
            }
        }

        [Theory]
        [WithFile(TestImages.Jpeg.Baseline.Calliphora, PixelTypes.Rgba32)]
        [WithFile(TestImages.Jpeg.Baseline.Testorig420, PixelTypes.Rgba32)]
        public void DoProcessorStep<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            string imageFile = provider.SourceFileOrDescription;
            using (JpegDecoderCore decoder = JpegFixture.ParseJpegStream(imageFile))
            using (var pp = new JpegImagePostProcessor(Configuration.Default.MemoryAllocator, decoder))
            using (var imageFrame = new ImageFrame<Rgba32>(Configuration.Default, decoder.ImageWidth, decoder.ImageHeight))
            {
                pp.DoPostProcessorStep(imageFrame);

                JpegComponentPostProcessor[] cp = pp.ComponentProcessors;

                SaveBuffer(cp[0], provider);
                SaveBuffer(cp[1], provider);
                SaveBuffer(cp[2], provider);
            }
        }
        
        [Theory]
        [WithFileCollection(nameof(BaselineTestJpegs), PixelTypes.Rgba32)]
        public void PostProcess<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            string imageFile = provider.SourceFileOrDescription;
            using (JpegDecoderCore decoder = JpegFixture.ParseJpegStream(imageFile))
            using (var pp = new JpegImagePostProcessor(Configuration.Default.MemoryAllocator, decoder))
            using (var image = new Image<Rgba32>(decoder.ImageWidth, decoder.ImageHeight))
            {
                pp.PostProcess(image.Frames.RootFrame);

                image.DebugSave(provider);

                ImagingTestCaseUtility testUtil = provider.Utility;
                testUtil.TestGroupName = nameof(JpegDecoderTests);
                testUtil.TestName = JpegDecoderTests.DecodeBaselineJpegOutputName;

                using (Image<TPixel> referenceImage =
                    provider.GetReferenceOutputImage<TPixel>(appendPixelTypeToFileName: false))
                {
                    ImageSimilarityReport report = ImageComparer.Exact.CompareImagesOrFrames(referenceImage, image);

                    this.Output.WriteLine($"*** {imageFile} ***");
                    this.Output.WriteLine($"Difference: {report.DifferencePercentageString}");

                    // ReSharper disable once PossibleInvalidOperationException
                    Assert.True(report.TotalNormalizedDifference.Value < 0.005f);
                }
            }
        }
    }
}