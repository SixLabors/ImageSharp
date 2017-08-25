// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;

    using SixLabors.ImageSharp.Formats;
    using SixLabors.ImageSharp.Formats.Jpeg;
    using SixLabors.ImageSharp.Formats.Jpeg.Common;
    using SixLabors.ImageSharp.Formats.Jpeg.GolangPort;
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

        [Fact]
        public void RunDumpJpegCoeffsTool()
        {
            string inputFile = TestFile.GetInputFileFullPath(TestImages.Jpeg.Progressive.Progress);
            
        }

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
        //[WithFileCollection(nameof(BaselineTestJpegs), PixelTypes.Rgba32)]
        [WithFile(TestImages.Jpeg.Progressive.Progress, PixelTypes.Rgba32)]
        public void HelloSerializedSpectralData<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            byte[] sourceBytes = TestFile.Create(provider.SourceFileOrDescription).Bytes;

            using (var ms = new MemoryStream(sourceBytes))
            {
                //LibJpegTools.SpectralData data = LibJpegTools.SpectralData.Load(ms);
                OldJpegDecoderCore dec = new OldJpegDecoderCore(Configuration.Default, new JpegDecoder());
                dec.ParseStream(new MemoryStream(sourceBytes));

                LibJpegTools.SpectralData data = LibJpegTools.SpectralData.LoadFromImageSharpDecoder(dec);
                Assert.True(data.ComponentCount > 0);
                this.Output.WriteLine($"ComponentCount: {data.ComponentCount}");

                string comp0FileName = TestFile.GetInputFileFullPath(provider.SourceFileOrDescription + ".comp0");
                if (!File.Exists(comp0FileName))
                {
                    this.Output.WriteLine("Missing file: " + comp0FileName);
                }

                byte[] stuff = File.ReadAllBytes(comp0FileName);

                ref Block8x8 actual = ref Unsafe.As<byte, Block8x8>(ref stuff[0]);
                ref Block8x8 expected = ref data.Components[0].Blocks[0];

                Assert.Equal(actual, expected);
                //this.SaveSpectralImage(provider, data);
            }
        }

        [Theory]
        [WithFileCollection(nameof(AllTestJpegs), PixelTypes.Rgba32)]
        public void PdfJsDecoder_ParseStream_SaveSpectralResult<TPixel>(TestImageProvider<TPixel> provider)
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
        public void OriginalDecoder_ParseStream_SaveSpectralResult<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            OldJpegDecoderCore decoder = new OldJpegDecoderCore(Configuration.Default, new JpegDecoder());

            byte[] sourceBytes = TestFile.Create(provider.SourceFileOrDescription).Bytes;

            using (var ms = new MemoryStream(sourceBytes))
            {
                decoder.ParseStream(ms, false);

                var data = LibJpegTools.SpectralData.LoadFromImageSharpDecoder(decoder);
                this.SaveSpectralImage(provider, data);
            }
        }

        private void VerifySpectralCorrectness<TPixel>(
            MemoryStream ms,
            LibJpegTools.SpectralData imageSharpData)
            where TPixel : struct, IPixel<TPixel>
        {
            ms.Seek(0, SeekOrigin.Begin);
            var libJpegData = LibJpegTools.SpectralData.Load(ms);

            bool equality = libJpegData.Equals(imageSharpData);
            this.Output.WriteLine("Spectral data equality: " + equality);
            //if (!equality)
            //{
                int componentCount = imageSharpData.ComponentCount;
                if (libJpegData.ComponentCount != componentCount)
                {
                    throw new Exception("libJpegData.ComponentCount != componentCount");
                }

                double totalDifference = 0;
                this.Output.WriteLine("*** Differences ***");
                for (int i = 0; i < componentCount; i++)
                {
                    LibJpegTools.ComponentData libJpegComponent = libJpegData.Components[i];
                    LibJpegTools.ComponentData imageSharpComponent = imageSharpData.Components[i];

                    double d = LibJpegTools.CalculateAverageDifference(libJpegComponent, imageSharpComponent);

                    this.Output.WriteLine($"Component{i}: {d}");
                    totalDifference += d;
                }
                totalDifference /= componentCount;
                
                this.Output.WriteLine($"AVERAGE: {totalDifference}");
            //}

            Assert.Equal(libJpegData, imageSharpData);
        }

        [Theory]
        [WithFileCollection(nameof(AllTestJpegs), PixelTypes.Rgba32)]
        public void VerifySpectralCorrectness_PdfJs<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            JpegDecoderCore decoder = new JpegDecoderCore(Configuration.Default, new JpegDecoder());

            byte[] sourceBytes = TestFile.Create(provider.SourceFileOrDescription).Bytes;

            using (var ms = new MemoryStream(sourceBytes))
            {
                decoder.ParseStream(ms);
                var imageSharpData = LibJpegTools.SpectralData.LoadFromImageSharpDecoder(decoder);
                
                this.VerifySpectralCorrectness<TPixel>(ms, imageSharpData);
            }
        }

        [Theory]
        [WithFileCollection(nameof(AllTestJpegs), PixelTypes.Rgba32)]
        public void VerifySpectralResults_OriginalDecoder<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            OldJpegDecoderCore decoder = new OldJpegDecoderCore(Configuration.Default, new JpegDecoder());

            byte[] sourceBytes = TestFile.Create(provider.SourceFileOrDescription).Bytes;

            using (var ms = new MemoryStream(sourceBytes))
            {
                decoder.ParseStream(ms);
                var imageSharpData = LibJpegTools.SpectralData.LoadFromImageSharpDecoder(decoder);

                this.VerifySpectralCorrectness<TPixel>(ms, imageSharpData);
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