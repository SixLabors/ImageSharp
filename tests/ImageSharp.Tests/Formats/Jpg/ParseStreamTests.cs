// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Text;
using SixLabors.ImageSharp.Formats.Jpeg.Components;
using SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder;
using SixLabors.ImageSharp.Formats.Jpeg.GolangPort;
using SixLabors.ImageSharp.Formats.Jpeg.GolangPort.Components.Decoder;
using SixLabors.ImageSharp.Formats.Jpeg.PdfJsPort;
using SixLabors.ImageSharp.Formats.Jpeg.PdfJsPort.Components;
using SixLabors.ImageSharp.Tests.Formats.Jpg.Utils;
using SixLabors.Primitives;

using Xunit;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests.Formats.Jpg
{
    public class ParseStreamTests
    {
        private ITestOutputHelper Output { get; }

        public ParseStreamTests(ITestOutputHelper output)
        {
            this.Output = output;
        }

        [Theory]
        [InlineData(TestImages.Jpeg.Baseline.Testorig420, JpegColorSpace.YCbCr)]
        [InlineData(TestImages.Jpeg.Baseline.Jpeg400, JpegColorSpace.Grayscale)]
        [InlineData(TestImages.Jpeg.Baseline.Ycck, JpegColorSpace.Ycck)]
        [InlineData(TestImages.Jpeg.Baseline.Cmyk, JpegColorSpace.Cmyk)]
        public void ColorSpace_IsDeducedCorrectlyGolang(string imageFile, object expectedColorSpaceValue)
        {
            var expecteColorSpace = (JpegColorSpace)expectedColorSpaceValue;

            using (GolangJpegDecoderCore decoder = JpegFixture.ParseGolangStream(imageFile))
            {
                Assert.Equal(expecteColorSpace, decoder.ColorSpace);
            }
        }

        [Theory]
        [InlineData(TestImages.Jpeg.Baseline.Testorig420, JpegColorSpace.YCbCr)]
        [InlineData(TestImages.Jpeg.Baseline.Jpeg400, JpegColorSpace.Grayscale)]
        [InlineData(TestImages.Jpeg.Baseline.Ycck, JpegColorSpace.Ycck)]
        [InlineData(TestImages.Jpeg.Baseline.Cmyk, JpegColorSpace.Cmyk)]
        public void ColorSpace_IsDeducedCorrectlyPdfJs(string imageFile, object expectedColorSpaceValue)
        {
            var expecteColorSpace = (JpegColorSpace)expectedColorSpaceValue;

            using (PdfJsJpegDecoderCore decoder = JpegFixture.ParsePdfJsStream(imageFile))
            {
                Assert.Equal(expecteColorSpace, decoder.ColorSpace);
            }
        }

        [Fact]
        public void ComponentScalingIsCorrect_1ChannelJpegGolang()
        {
            using (GolangJpegDecoderCore decoder = JpegFixture.ParseGolangStream(TestImages.Jpeg.Baseline.Jpeg400))
            {
                Assert.Equal(1, decoder.ComponentCount);
                Assert.Equal(1, decoder.Components.Length);

                Size expectedSizeInBlocks = decoder.ImageSizeInPixels.DivideRoundUp(8);

                Assert.Equal(expectedSizeInBlocks, decoder.ImageSizeInMCU);

                var uniform1 = new Size(1, 1);
                GolangComponent c0 = decoder.Components[0];
                VerifyJpeg.VerifyComponent(c0, expectedSizeInBlocks, uniform1, uniform1);
            }
        }

        [Fact]
        public void ComponentScalingIsCorrect_1ChannelJpegPdfJs()
        {
            using (PdfJsJpegDecoderCore decoder = JpegFixture.ParsePdfJsStream(TestImages.Jpeg.Baseline.Jpeg400))
            {
                Assert.Equal(1, decoder.ComponentCount);
                Assert.Equal(1, decoder.Components.Length);

                Size expectedSizeInBlocks = decoder.ImageSizeInPixels.DivideRoundUp(8);

                Assert.Equal(expectedSizeInBlocks, decoder.ImageSizeInMCU);

                var uniform1 = new Size(1, 1);
                PdfJsFrameComponent c0 = decoder.Components[0];
                VerifyJpeg.VerifyComponent(c0, expectedSizeInBlocks, uniform1, uniform1);
            }
        }

        [Theory]
        [InlineData(TestImages.Jpeg.Baseline.Jpeg444)]
        [InlineData(TestImages.Jpeg.Baseline.Jpeg420Exif)]
        [InlineData(TestImages.Jpeg.Baseline.Jpeg420Small)]
        [InlineData(TestImages.Jpeg.Baseline.Testorig420)]
        [InlineData(TestImages.Jpeg.Baseline.Ycck)]
        [InlineData(TestImages.Jpeg.Baseline.Cmyk)]
        public void PrintComponentDataGolang(string imageFile)
        {
            var sb = new StringBuilder();

            using (GolangJpegDecoderCore decoder = JpegFixture.ParseGolangStream(imageFile))
            {
                sb.AppendLine(imageFile);
                sb.AppendLine($"Size:{decoder.ImageSizeInPixels} MCU:{decoder.ImageSizeInMCU}");
                GolangComponent c0 = decoder.Components[0];
                GolangComponent c1 = decoder.Components[1];

                sb.AppendLine($"Luma: SAMP: {c0.SamplingFactors} BLOCKS: {c0.SizeInBlocks}");
                sb.AppendLine($"Chroma: {c1.SamplingFactors} BLOCKS: {c1.SizeInBlocks}");
            }
            this.Output.WriteLine(sb.ToString());
        }

        [Theory]
        [InlineData(TestImages.Jpeg.Baseline.Jpeg444)]
        [InlineData(TestImages.Jpeg.Baseline.Jpeg420Exif)]
        [InlineData(TestImages.Jpeg.Baseline.Jpeg420Small)]
        [InlineData(TestImages.Jpeg.Baseline.Testorig420)]
        [InlineData(TestImages.Jpeg.Baseline.Ycck)]
        [InlineData(TestImages.Jpeg.Baseline.Cmyk)]
        public void PrintComponentDataPdfJs(string imageFile)
        {
            var sb = new StringBuilder();

            using (PdfJsJpegDecoderCore decoder = JpegFixture.ParsePdfJsStream(imageFile))
            {
                sb.AppendLine(imageFile);
                sb.AppendLine($"Size:{decoder.ImageSizeInPixels} MCU:{decoder.ImageSizeInMCU}");
                PdfJsFrameComponent c0 = decoder.Components[0];
                PdfJsFrameComponent c1 = decoder.Components[1];

                sb.AppendLine($"Luma: SAMP: {c0.SamplingFactors} BLOCKS: {c0.SizeInBlocks}");
                sb.AppendLine($"Chroma: {c1.SamplingFactors} BLOCKS: {c1.SizeInBlocks}");
            }
            this.Output.WriteLine(sb.ToString());
        }

        public static readonly TheoryData<string, int, object, object> ComponentVerificationData = new TheoryData<string, int, object, object>()
            {
                { TestImages.Jpeg.Baseline.Jpeg444, 3, new Size(1, 1), new Size(1, 1) },
                { TestImages.Jpeg.Baseline.Jpeg420Exif, 3, new Size(2, 2), new Size(1, 1) },
                { TestImages.Jpeg.Baseline.Jpeg420Small, 3, new Size(2, 2), new Size(1, 1) },
                { TestImages.Jpeg.Baseline.Testorig420, 3, new Size(2, 2), new Size(1, 1) },
                // TODO: Find Ycck or Cmyk images with different subsampling
                { TestImages.Jpeg.Baseline.Ycck, 4, new Size(1, 1), new Size(1, 1) },
                { TestImages.Jpeg.Baseline.Cmyk, 4, new Size(1, 1), new Size(1, 1) },
            };

        [Theory]
        [MemberData(nameof(ComponentVerificationData))]
        public void ComponentScalingIsCorrect_MultiChannelJpegGolang(
            string imageFile,
            int componentCount,
            object expectedLumaFactors,
            object expectedChromaFactors)
        {
            var fLuma = (Size)expectedLumaFactors;
            var fChroma = (Size)expectedChromaFactors;

            using (GolangJpegDecoderCore decoder = JpegFixture.ParseGolangStream(imageFile))
            {
                Assert.Equal(componentCount, decoder.ComponentCount);
                Assert.Equal(componentCount, decoder.Components.Length);

                GolangComponent c0 = decoder.Components[0];
                GolangComponent c1 = decoder.Components[1];
                GolangComponent c2 = decoder.Components[2];

                var uniform1 = new Size(1, 1);

                Size expectedLumaSizeInBlocks = decoder.ImageSizeInMCU.MultiplyBy(fLuma);

                Size divisor = fLuma.DivideBy(fChroma);

                Size expectedChromaSizeInBlocks = expectedLumaSizeInBlocks.DivideRoundUp(divisor);

                VerifyJpeg.VerifyComponent(c0, expectedLumaSizeInBlocks, fLuma, uniform1);
                VerifyJpeg.VerifyComponent(c1, expectedChromaSizeInBlocks, fChroma, divisor);
                VerifyJpeg.VerifyComponent(c2, expectedChromaSizeInBlocks, fChroma, divisor);

                if (componentCount == 4)
                {
                    GolangComponent c3 = decoder.Components[2];
                    VerifyJpeg.VerifyComponent(c3, expectedLumaSizeInBlocks, fLuma, uniform1);
                }
            }
        }


        [Theory]
        [MemberData(nameof(ComponentVerificationData))]
        public void ComponentScalingIsCorrect_MultiChannelJpegPdfJs(
            string imageFile,
            int componentCount,
            object expectedLumaFactors,
            object expectedChromaFactors)
        {
            var fLuma = (Size)expectedLumaFactors;
            var fChroma = (Size)expectedChromaFactors;

            using (PdfJsJpegDecoderCore decoder = JpegFixture.ParsePdfJsStream(imageFile))
            {
                Assert.Equal(componentCount, decoder.ComponentCount);
                Assert.Equal(componentCount, decoder.Components.Length);

                PdfJsFrameComponent c0 = decoder.Components[0];
                PdfJsFrameComponent c1 = decoder.Components[1];
                PdfJsFrameComponent c2 = decoder.Components[2];

                var uniform1 = new Size(1, 1);

                Size expectedLumaSizeInBlocks = decoder.ImageSizeInMCU.MultiplyBy(fLuma);

                Size divisor = fLuma.DivideBy(fChroma);

                Size expectedChromaSizeInBlocks = expectedLumaSizeInBlocks.DivideRoundUp(divisor);

                VerifyJpeg.VerifyComponent(c0, expectedLumaSizeInBlocks, fLuma, uniform1);
                VerifyJpeg.VerifyComponent(c1, expectedChromaSizeInBlocks, fChroma, divisor);
                VerifyJpeg.VerifyComponent(c2, expectedChromaSizeInBlocks, fChroma, divisor);

                if (componentCount == 4)
                {
                    PdfJsFrameComponent c3 = decoder.Components[2];
                    VerifyJpeg.VerifyComponent(c3, expectedLumaSizeInBlocks, fLuma, uniform1);
                }
            }
        }
    }
}