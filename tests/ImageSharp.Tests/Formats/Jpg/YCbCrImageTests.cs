// <copyright file="YCbCrImageTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using ImageSharp.Formats.Jpeg.GolangPort.Components.Decoder;
    using SixLabors.Primitives;
    using Xunit;
    using Xunit.Abstractions;

    public class YCbCrImageTests
    {
        public YCbCrImageTests(ITestOutputHelper output)
        {
            this.Output = output;
        }

        private ITestOutputHelper Output { get; }

        [Theory]
        [InlineData(YCbCrImage.YCbCrSubsampleRatio.YCbCrSubsampleRatio410, 4, 2)]
        [InlineData(YCbCrImage.YCbCrSubsampleRatio.YCbCrSubsampleRatio411, 4, 1)]
        [InlineData(YCbCrImage.YCbCrSubsampleRatio.YCbCrSubsampleRatio420, 2, 2)]
        [InlineData(YCbCrImage.YCbCrSubsampleRatio.YCbCrSubsampleRatio422, 2, 1)]
        [InlineData(YCbCrImage.YCbCrSubsampleRatio.YCbCrSubsampleRatio440, 1, 2)]
        [InlineData(YCbCrImage.YCbCrSubsampleRatio.YCbCrSubsampleRatio444, 1, 1)]
        internal void CalculateChrominanceSize(
            YCbCrImage.YCbCrSubsampleRatio ratioValue,
            int expectedDivX,
            int expectedDivY)
        {
            YCbCrImage.YCbCrSubsampleRatio ratio = (YCbCrImage.YCbCrSubsampleRatio)ratioValue;

            //this.Output.WriteLine($"RATIO: {ratio}");
            Size size = YCbCrImage.CalculateChrominanceSize(400, 400, ratio);
            //this.Output.WriteLine($"Ch Size: {size}");

            Assert.Equal(new Size(400 / expectedDivX, 400 / expectedDivY), size);
        }

        [Theory]
        [InlineData(YCbCrImage.YCbCrSubsampleRatio.YCbCrSubsampleRatio410, 4)]
        [InlineData(YCbCrImage.YCbCrSubsampleRatio.YCbCrSubsampleRatio411, 4)]
        [InlineData(YCbCrImage.YCbCrSubsampleRatio.YCbCrSubsampleRatio420, 2)]
        [InlineData(YCbCrImage.YCbCrSubsampleRatio.YCbCrSubsampleRatio422, 2)]
        [InlineData(YCbCrImage.YCbCrSubsampleRatio.YCbCrSubsampleRatio440, 1)]
        [InlineData(YCbCrImage.YCbCrSubsampleRatio.YCbCrSubsampleRatio444, 1)]
        internal void Create(YCbCrImage.YCbCrSubsampleRatio ratioValue, int expectedCStrideDiv)
        {
            YCbCrImage.YCbCrSubsampleRatio ratio = (YCbCrImage.YCbCrSubsampleRatio)ratioValue;

            this.Output.WriteLine($"RATIO: {ratio}");

            YCbCrImage img = new YCbCrImage(400, 400, ratio);

            //this.PrintChannel("Y", img.YChannel);
            //this.PrintChannel("Cb", img.CbChannel);
            //this.PrintChannel("Cr", img.CrChannel);

            Assert.Equal(400, img.YChannel.Width);
            Assert.Equal(img.CbChannel.Width, 400 / expectedCStrideDiv);
            Assert.Equal(img.CrChannel.Width, 400 / expectedCStrideDiv);
        }

        private void PrintChannel(string name, OldJpegPixelArea channel)
        {
            this.Output.WriteLine($"{name}: Stride={channel.Stride}");
        }
    }
}