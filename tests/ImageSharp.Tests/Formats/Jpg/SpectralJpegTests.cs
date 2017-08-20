// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using SixLabors.ImageSharp.Formats;
    using SixLabors.ImageSharp.Formats.Jpeg.PdfJsPort;
    using SixLabors.ImageSharp.PixelFormats;
    using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

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
                TestImages.Jpeg.Baseline.Calliphora, TestImages.Jpeg.Baseline.Cmyk,
                TestImages.Jpeg.Baseline.Jpeg400, TestImages.Jpeg.Baseline.Jpeg444,
                TestImages.Jpeg.Baseline.Testimgorig,
                TestImages.Jpeg.Baseline.Bad.BadEOF,
                TestImages.Jpeg.Baseline.Bad.ExifUndefType,
            };

        public static readonly string[] ProgressiveTestJpegs = 
            {
                TestImages.Jpeg.Progressive.Fb, TestImages.Jpeg.Progressive.Progress,
                TestImages.Jpeg.Progressive.Festzug, TestImages.Jpeg.Progressive.Bad.BadEOF
            };

        public static readonly string[] AllTestJpegs = BaselineTestJpegs.Concat(ProgressiveTestJpegs).ToArray();

        [Theory]
        [WithFileCollection(nameof(BaselineTestJpegs), PixelTypes.Rgba32)]
        public void BuildLibJpegSpectralResult<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            byte[] sourceBytes = TestFile.Create(provider.SourceFileOrDescription).Bytes;

            using (var ms = new MemoryStream(sourceBytes))
            {
                LibJpegTools.SpectralData data = LibJpegTools.SpectralData.Load(ms);
                Assert.True(data.ComponentCount > 0);
                this.Output.WriteLine($"ComponentCount: {data.ComponentCount}");

                this.SaveSpectralImage(provider, data);
            }   
        }

        [Theory]
        [WithFileCollection(nameof(BaselineTestJpegs), PixelTypes.Rgba32)]
        public void JpegDecoderCore_ParseStream_SaveSpectralResult<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            JpegDecoderCore decoder = new JpegDecoderCore(Configuration.Default, new JpegDecoder());

            byte[] sourceBytes = TestFile.Create(provider.SourceFileOrDescription).Bytes;

            using (var ms = new MemoryStream(sourceBytes))
            {
                decoder.ParseStream(ms);

                var data = LibJpegTools.SpectralData.LoadFromImageSharpDecoder(decoder);
                this.SaveSpectralImage(provider, data);
            }
        }

        [Theory]
        [WithFileCollection(nameof(AllTestJpegs), PixelTypes.Rgba32)]
        public void CompareSpectralResults<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            JpegDecoderCore decoder = new JpegDecoderCore(Configuration.Default, new JpegDecoder());

            byte[] sourceBytes = TestFile.Create(provider.SourceFileOrDescription).Bytes;

            using (var ms = new MemoryStream(sourceBytes))
            {
                decoder.ParseStream(ms);
                var imageSharpData = LibJpegTools.SpectralData.LoadFromImageSharpDecoder(decoder);
                
                ms.Seek(0, SeekOrigin.Begin);
                var libJpegData = LibJpegTools.SpectralData.Load(ms);

                Assert.Equal(libJpegData, imageSharpData);
            }
        }


        private void SaveSpectralImage<TPixel>(TestImageProvider<TPixel> provider, LibJpegTools.SpectralData data)
            where TPixel : struct, IPixel<TPixel>
        {
            foreach (LibJpegTools.ComponentData comp in data.Components)
            {
                this.Output.WriteLine("Min: " + comp.MinVal);
                this.Output.WriteLine("Max: " + comp.MaxVal);

                using (Image<Rgba32> image = comp.CreateGrayScaleImage())
                {
                    string details = $"C{comp.Index}";
                    image.DebugSave(provider, details, appendPixelTypeToFileName: false);
                }
            }

            Image<Rgba32> fullImage = data.TryCreateRGBSpectralImage();

            if (fullImage != null)
            {
                fullImage.DebugSave(provider, "FULL", appendPixelTypeToFileName: false);
                fullImage.Dispose();
            }
        }

    }
}