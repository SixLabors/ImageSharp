using SixLabors.ImageSharp.Formats.Jpeg.Common;
using SixLabors.ImageSharp.Formats.Jpeg.GolangPort;
using SixLabors.ImageSharp.Formats.Jpeg.GolangPort.Components.Decoder;
using SixLabors.ImageSharp.Tests.Formats.Jpg.Utils;
using SixLabors.Primitives;
using Xunit;
using Xunit.Abstractions;
// ReSharper disable InconsistentNaming

namespace SixLabors.ImageSharp.Tests.Formats.Jpg
{    
    public class ParseStreamTests
    {
        private ITestOutputHelper Output { get; }

        public ParseStreamTests(ITestOutputHelper output)
        {
            this.Output = output;
        }

        [Fact]
        public void ComponentScalingIsCorrect_1ChannelJpeg()
        {
            using (OrigJpegDecoderCore decoder = JpegFixture.ParseStream(TestImages.Jpeg.Baseline.Jpeg400))
            {
                Assert.Equal(1, decoder.ComponentCount);
                Assert.Equal(1, decoder.Components.Length);

                Size sizeInBlocks = decoder.ImageSizeInBlocks;

                Size expectedSizeInBlocks = decoder.ImageSizeInPixels.GetSubSampledSize(8);

                Assert.Equal(expectedSizeInBlocks, sizeInBlocks);
                Assert.Equal(sizeInBlocks, decoder.ImageSizeInMCU);

                var uniform1 = new Size(1, 1);
                OrigComponent c0 = decoder.Components[0];
                VerifyJpeg.VerifyComponent(c0, expectedSizeInBlocks, uniform1, uniform1);
            }
        }
        
        [Theory]
        [InlineData(TestImages.Jpeg.Baseline.Jpeg444, 3, 1, 1)]
        [InlineData(TestImages.Jpeg.Baseline.Jpeg420Exif, 3, 2, 2)]
        [InlineData(TestImages.Jpeg.Baseline.Jpeg420Small, 3, 2, 2)]
        [InlineData(TestImages.Jpeg.Baseline.Ycck, 4, 1, 1)]  // TODO: Find Ycck or Cmyk images with different subsampling
        [InlineData(TestImages.Jpeg.Baseline.Cmyk, 4, 1, 1)]
        public void ComponentScalingIsCorrect_MultiChannelJpeg(
            string imageFile,
            int componentCount,
            int hDiv,
            int vDiv)
        {
            Size divisor = new Size(hDiv, vDiv);

            using (OrigJpegDecoderCore decoder = JpegFixture.ParseStream(imageFile))
            {
                Assert.Equal(componentCount, decoder.ComponentCount);
                Assert.Equal(componentCount, decoder.Components.Length);
                
                OrigComponent c0 = decoder.Components[0];
                OrigComponent c1 = decoder.Components[1];
                OrigComponent c2 = decoder.Components[2];

                var uniform1 = new Size(1, 1);
                Size expectedLumaSizeInBlocks = decoder.ImageSizeInPixels.GetSubSampledSize(8);
                Size expectedChromaSizeInBlocks = expectedLumaSizeInBlocks.DivideBy(divisor);

                Size expectedLumaSamplingFactors = expectedLumaSizeInBlocks.DivideBy(decoder.ImageSizeInMCU);
                Size expectedChromaSamplingFactors = expectedLumaSamplingFactors.DivideBy(divisor);

                VerifyJpeg.VerifyComponent(c0, expectedLumaSizeInBlocks, expectedLumaSamplingFactors, uniform1);
                VerifyJpeg.VerifyComponent(c1, expectedChromaSizeInBlocks, expectedChromaSamplingFactors, divisor);
                VerifyJpeg.VerifyComponent(c2, expectedChromaSizeInBlocks, expectedChromaSamplingFactors, divisor);

                if (componentCount == 4)
                {
                    OrigComponent c3 = decoder.Components[2];
                    VerifyJpeg.VerifyComponent(c3, expectedLumaSizeInBlocks, expectedLumaSamplingFactors, uniform1);
                }
            }
        }
    }
}