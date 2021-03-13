// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Text;

using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Jpeg.Components;
using SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder;
using SixLabors.ImageSharp.Tests.Formats.Jpg.Utils;

using Xunit;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests.Formats.Jpg
{
    [Trait("Format", "Jpg")]
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
        public void ColorSpace_IsDeducedCorrectly(string imageFile, object expectedColorSpaceValue)
        {
            var expectedColorSpace = (JpegColorSpace)expectedColorSpaceValue;

            using (JpegDecoderCore decoder = JpegFixture.ParseJpegStream(imageFile))
            {
                Assert.Equal(expectedColorSpace, decoder.ColorSpace);
            }
        }

        [Fact]
        public void ComponentScalingIsCorrect_1ChannelJpeg()
        {
            using (JpegDecoderCore decoder = JpegFixture.ParseJpegStream(TestImages.Jpeg.Baseline.Jpeg400))
            {
                Assert.Equal(1, decoder.ComponentCount);
                Assert.Equal(1, decoder.Components.Length);

                Size expectedSizeInBlocks = decoder.ImageSizeInPixels.DivideRoundUp(8);

                Assert.Equal(expectedSizeInBlocks, decoder.ImageSizeInMCU);

                var uniform1 = new Size(1, 1);
                JpegComponent c0 = decoder.Components[0];
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
        public void PrintComponentData(string imageFile)
        {
            var sb = new StringBuilder();

            using (JpegDecoderCore decoder = JpegFixture.ParseJpegStream(imageFile))
            {
                sb.AppendLine(imageFile);
                sb.AppendLine($"Size:{decoder.ImageSizeInPixels} MCU:{decoder.ImageSizeInMCU}");
                JpegComponent c0 = decoder.Components[0];
                JpegComponent c1 = decoder.Components[1];

                sb.AppendLine($"Luma: SAMP: {c0.SamplingFactors} BLOCKS: {c0.SizeInBlocks}");
                sb.AppendLine($"Chroma: {c1.SamplingFactors} BLOCKS: {c1.SizeInBlocks}");
            }

            this.Output.WriteLine(sb.ToString());
        }

        public static readonly TheoryData<string, int, object, object> ComponentVerificationData = new TheoryData<string, int, object, object>
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
        public void ComponentScalingIsCorrect_MultiChannelJpeg(
            string imageFile,
            int componentCount,
            object expectedLumaFactors,
            object expectedChromaFactors)
        {
            var fLuma = (Size)expectedLumaFactors;
            var fChroma = (Size)expectedChromaFactors;

            using (JpegDecoderCore decoder = JpegFixture.ParseJpegStream(imageFile))
            {
                Assert.Equal(componentCount, decoder.ComponentCount);
                Assert.Equal(componentCount, decoder.Components.Length);

                JpegComponent c0 = decoder.Components[0];
                JpegComponent c1 = decoder.Components[1];
                JpegComponent c2 = decoder.Components[2];

                var uniform1 = new Size(1, 1);

                Size expectedLumaSizeInBlocks = decoder.ImageSizeInMCU.MultiplyBy(fLuma);

                Size divisor = fLuma.DivideBy(fChroma);

                Size expectedChromaSizeInBlocks = expectedLumaSizeInBlocks.DivideRoundUp(divisor);

                VerifyJpeg.VerifyComponent(c0, expectedLumaSizeInBlocks, fLuma, uniform1);
                VerifyJpeg.VerifyComponent(c1, expectedChromaSizeInBlocks, fChroma, divisor);
                VerifyJpeg.VerifyComponent(c2, expectedChromaSizeInBlocks, fChroma, divisor);

                if (componentCount == 4)
                {
                    JpegComponent c3 = decoder.Components[2];
                    VerifyJpeg.VerifyComponent(c3, expectedLumaSizeInBlocks, fLuma, uniform1);
                }
            }
        }
    }
}
