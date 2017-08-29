// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

// ReSharper disable InconsistentNaming

using SixLabors.ImageSharp.Formats.Jpeg.Common;
using SixLabors.ImageSharp.Formats.Jpeg.Common.Decoder;
using SixLabors.ImageSharp.Formats.Jpeg.GolangPort.Components.Decoder;
using SixLabors.Primitives;

using Xunit;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests.Formats.Jpg
{
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
        internal void CalculateChrominanceSize(SubsampleRatio ratio, int expectedDivX, int expectedDivY)
        {
            //this.Output.WriteLine($"RATIO: {ratio}");
            Size size = ratio.CalculateChrominanceSize(400, 400);
            //this.Output.WriteLine($"Ch Size: {size}");

            Assert.Equal(new Size(400 / expectedDivX, 400 / expectedDivY), size);
        }

        [Theory]
        [InlineData(SubsampleRatio.Ratio410, 4, 2)]
        [InlineData(SubsampleRatio.Ratio411, 4, 1)]
        [InlineData(SubsampleRatio.Ratio420, 2, 2)]
        [InlineData(SubsampleRatio.Ratio422, 2, 1)]
        [InlineData(SubsampleRatio.Ratio440, 1, 2)]
        [InlineData(SubsampleRatio.Ratio444, 1, 1)]
        [InlineData(SubsampleRatio.Undefined, 1, 1)]
        internal void GetChrominanceSubSampling(SubsampleRatio ratio, int expectedDivX, int expectedDivY)
        {
            (int divX, int divY) = ratio.GetChrominanceSubSampling();

            Assert.Equal(expectedDivX, divX);
            Assert.Equal(expectedDivY, divY);
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
    }
}