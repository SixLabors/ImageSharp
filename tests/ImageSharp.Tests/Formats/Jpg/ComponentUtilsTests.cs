// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Jpg
{
    using SixLabors.ImageSharp.Formats.Jpeg.Common;
    using SixLabors.ImageSharp.Formats.Jpeg.GolangPort;
    using SixLabors.ImageSharp.Formats.Jpeg.GolangPort.Components.Decoder;
    using SixLabors.ImageSharp.Tests.Formats.Jpg.Utils;
    using SixLabors.Primitives;

    using Xunit;
    using Xunit.Abstractions;

    public class ComponentUtilsTests
    {
        public ComponentUtilsTests(ITestOutputHelper output)
        {
            this.Output = output;
        }

        private ITestOutputHelper Output { get; }

        [Theory]
        [InlineData(SubsampleRatio.Ratio410, 4, 2)]
        [InlineData(SubsampleRatio.Ratio411, 4, 1)]
        [InlineData(SubsampleRatio.Ratio420, 2, 2)]
        [InlineData(SubsampleRatio.Ratio422, 2, 1)]
        [InlineData(SubsampleRatio.Ratio440, 1, 2)]
        [InlineData(SubsampleRatio.Ratio444, 1, 1)]
        internal void CalculateChrominanceSize(
            SubsampleRatio ratio,
            int expectedDivX,
            int expectedDivY)
        {
            //this.Output.WriteLine($"RATIO: {ratio}");
            Size size = ratio.CalculateChrominanceSize(400, 400);
            //this.Output.WriteLine($"Ch Size: {size}");

            Assert.Equal(new Size(400 / expectedDivX, 400 / expectedDivY), size);
        }

        [Theory]
        [InlineData(SubsampleRatio.Ratio410, 4)]
        [InlineData(SubsampleRatio.Ratio411, 4)]
        [InlineData(SubsampleRatio.Ratio420, 2)]
        [InlineData(SubsampleRatio.Ratio422, 2)]
        [InlineData(SubsampleRatio.Ratio440, 1)]
        [InlineData(SubsampleRatio.Ratio444, 1)]
        internal void Create(SubsampleRatio ratio, int expectedCStrideDiv)
        {
            this.Output.WriteLine($"RATIO: {ratio}");

            YCbCrImage img = new YCbCrImage(400, 400, ratio);

            //this.PrintChannel("Y", img.YChannel);
            //this.PrintChannel("Cb", img.CbChannel);
            //this.PrintChannel("Cr", img.CrChannel);

            Assert.Equal(400, img.YChannel.Width);
            Assert.Equal(img.CbChannel.Width, 400 / expectedCStrideDiv);
            Assert.Equal(img.CrChannel.Width, 400 / expectedCStrideDiv);
        }

        private void PrintChannel(string name, OrigJpegPixelArea channel)
        {
            this.Output.WriteLine($"{name}: Stride={channel.Stride}");
        }

        [Fact]
        public void CalculateJpegChannelSizes_Grayscale()
        {
            using (OrigJpegDecoderCore decoder = JpegFixture.ParseStream(TestImages.Jpeg.Baseline.Jpeg400))
            {
                Size[] sizes = ComponentUtils.CalculateJpegChannelSizes(decoder.Components, decoder.SubsampleRatio);

                Assert.Equal(1, sizes.Length);

                Size expected = decoder.Components[0].SizeInBlocks() * 8;

                Assert.Equal(expected, sizes[0]);
            }
        }

        [Theory]
        [InlineData(TestImages.Jpeg.Baseline.Calliphora, 1)]
        [InlineData(TestImages.Jpeg.Baseline.Jpeg444, 1)]
        [InlineData(TestImages.Jpeg.Baseline.Jpeg420, 2)]
        public void CalculateJpegChannelSizes_YCbCr(
            string imageFile,
            int chromaDiv)
        {
            using (OrigJpegDecoderCore decoder = JpegFixture.ParseStream(imageFile))
            {
                Size[] s = ComponentUtils.CalculateJpegChannelSizes(decoder.Components, decoder.SubsampleRatio);

                Assert.Equal(3, s.Length);

                Size ySize = decoder.Components[0].SizeInBlocks() * 8;
                Size cSize = ySize / chromaDiv;

                Assert.Equal(ySize, s[0]);
                Assert.Equal(cSize, s[1]);
                Assert.Equal(cSize, s[2]);
            }
        }

        [Theory]
        [InlineData(TestImages.Jpeg.Baseline.Ycck)]
        [InlineData(TestImages.Jpeg.Baseline.Cmyk)]
        public void CalculateJpegChannelSizes_4Chan(string imageFile)
        {
            using (OrigJpegDecoderCore decoder = JpegFixture.ParseStream(imageFile))
            {
                Size[] sizes = ComponentUtils.CalculateJpegChannelSizes(decoder.Components, decoder.SubsampleRatio);
                Assert.Equal(4, sizes.Length);

                Size expected = decoder.Components[0].SizeInBlocks() * 8;

                foreach (Size s in sizes)
                {
                    Assert.Equal(expected, s);
                }
            }
        }
    }
}