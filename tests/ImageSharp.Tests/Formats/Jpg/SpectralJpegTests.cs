// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Jpg
{
    using System;
    using System.IO;
    using System.Linq;

    using SixLabors.ImageSharp.Formats.Jpeg;
    using SixLabors.ImageSharp.Formats.Jpeg.GolangPort;
    using SixLabors.ImageSharp.Formats.Jpeg.PdfJsPort;
    using SixLabors.ImageSharp.PixelFormats;
    using SixLabors.ImageSharp.Tests.Formats.Jpg.Utils;

    using Xunit;
    using Xunit.Abstractions;

    public class SpectralJpegTests
    {
        public SpectralJpegTests(ITestOutputHelper output)
        {
            this.Output = output;
        }

        private ITestOutputHelper Output { get; }

        public static readonly string[] BaselineTestJpegs =
            {
                TestImages.Jpeg.Baseline.Calliphora, TestImages.Jpeg.Baseline.Cmyk, TestImages.Jpeg.Baseline.Jpeg400,
                TestImages.Jpeg.Baseline.Jpeg444, TestImages.Jpeg.Baseline.Testorig420,
                TestImages.Jpeg.Baseline.Jpeg420Small, TestImages.Jpeg.Baseline.Bad.BadEOF,
                TestImages.Jpeg.Baseline.Bad.ExifUndefType,
            };

        public static readonly string[] ProgressiveTestJpegs = 
            {
                TestImages.Jpeg.Progressive.Fb, TestImages.Jpeg.Progressive.Progress,
                TestImages.Jpeg.Progressive.Festzug, TestImages.Jpeg.Progressive.Bad.BadEOF
            };

        public static readonly string[] AllTestJpegs = BaselineTestJpegs.Concat(ProgressiveTestJpegs).ToArray();
        
        [Theory]
        [WithFileCollection(nameof(AllTestJpegs), PixelTypes.Rgba32)]
        public void PdfJsDecoder_ParseStream_SaveSpectralResult<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            PdfJsJpegDecoderCore decoder = new PdfJsJpegDecoderCore(Configuration.Default, new JpegDecoder());

            byte[] sourceBytes = TestFile.Create(provider.SourceFileOrDescription).Bytes;

            using (var ms = new MemoryStream(sourceBytes))
            {
                decoder.ParseStream(ms);

                var data = LibJpegTools.SpectralData.LoadFromImageSharpDecoder(decoder);
                VerifyJpeg.SaveSpectralImage(provider, data);
            }
        }

        [Theory]
        [WithFileCollection(nameof(AllTestJpegs), PixelTypes.Rgba32)]
        public void OriginalDecoder_ParseStream_SaveSpectralResult<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            OrigJpegDecoderCore decoder = new OrigJpegDecoderCore(Configuration.Default, new JpegDecoder());

            byte[] sourceBytes = TestFile.Create(provider.SourceFileOrDescription).Bytes;

            using (var ms = new MemoryStream(sourceBytes))
            {
                decoder.ParseStream(ms, false);

                var data = LibJpegTools.SpectralData.LoadFromImageSharpDecoder(decoder);
                VerifyJpeg.SaveSpectralImage(provider, data);
            }
        }

        private void VerifySpectralCorrectness<TPixel>(
            TestImageProvider<TPixel> provider,
            LibJpegTools.SpectralData imageSharpData)
            where TPixel : struct, IPixel<TPixel>
        {
            var libJpegData = LibJpegTools.ExtractSpectralData(provider.SourceFileOrDescription);

            bool equality = libJpegData.Equals(imageSharpData);
            this.Output.WriteLine("Spectral data equality: " + equality);

            int componentCount = imageSharpData.ComponentCount;
            if (libJpegData.ComponentCount != componentCount)
            {
                throw new Exception("libJpegData.ComponentCount != componentCount");
            }

            double averageDifference = 0;
            double totalDifference = 0;
            double tolerance = 0; 

            this.Output.WriteLine("*** Differences ***");
            for (int i = 0; i < componentCount; i++)
            {
                LibJpegTools.ComponentData libJpegComponent = libJpegData.Components[i];
                LibJpegTools.ComponentData imageSharpComponent = imageSharpData.Components[i];

                (double total, double average) diff = LibJpegTools.CalculateDifference(libJpegComponent, imageSharpComponent);

                this.Output.WriteLine($"Component{i}: {diff}");
                averageDifference += diff.average;
                totalDifference += diff.total;
                tolerance += libJpegComponent.SpectralBlocks.Length;
            }
            averageDifference /= componentCount;

            tolerance /= 64; // fair enough?

            this.Output.WriteLine($"AVERAGE: {averageDifference}");
            this.Output.WriteLine($"TOTAL: {totalDifference}");
            this.Output.WriteLine($"TOLERANCE = totalNumOfBlocks / 64 = {tolerance}");
            
            Assert.True(totalDifference < tolerance);
        }

        [Theory(Skip = "Debug/Comparison only")]
        [WithFileCollection(nameof(AllTestJpegs), PixelTypes.Rgba32)]
        public void VerifySpectralCorrectness_PdfJs<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            if (!TestEnvironment.IsWindows)
            {
                return;
            }

            PdfJsJpegDecoderCore decoder = new PdfJsJpegDecoderCore(Configuration.Default, new JpegDecoder());

            byte[] sourceBytes = TestFile.Create(provider.SourceFileOrDescription).Bytes;

            using (var ms = new MemoryStream(sourceBytes))
            {
                decoder.ParseStream(ms);
                var imageSharpData = LibJpegTools.SpectralData.LoadFromImageSharpDecoder(decoder);
                
                this.VerifySpectralCorrectness<TPixel>(provider, imageSharpData);
            }
        }

        [Theory]
        [WithFileCollection(nameof(AllTestJpegs), PixelTypes.Rgba32)]
        public void VerifySpectralResults_OriginalDecoder<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            if (!TestEnvironment.IsWindows)
            {
                return;
            }

            OrigJpegDecoderCore decoder = new OrigJpegDecoderCore(Configuration.Default, new JpegDecoder());

            byte[] sourceBytes = TestFile.Create(provider.SourceFileOrDescription).Bytes;

            using (var ms = new MemoryStream(sourceBytes))
            {
                decoder.ParseStream(ms);
                var imageSharpData = LibJpegTools.SpectralData.LoadFromImageSharpDecoder(decoder);

                this.VerifySpectralCorrectness<TPixel>(provider, imageSharpData);
            }
        }
    }
}