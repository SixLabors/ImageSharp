namespace ImageSharp.Tests
{
    using ImageSharp.Formats.Jpg;

    using Xunit;
    using Xunit.Abstractions;

    public class YCbCrImageTests
    {
        public YCbCrImageTests(ITestOutputHelper output)
        {
            this.Output = output;
        }

        private ITestOutputHelper Output { get; }

        private void PrintChannel(string name, JpegPixelArea channel)
        {
            this.Output.WriteLine($"{name}: Stride={channel.Stride}");
        }

        [Theory]
        [InlineData(YCbCrImage.YCbCrSubsampleRatio.YCbCrSubsampleRatio410, 4, 2)]
        [InlineData(YCbCrImage.YCbCrSubsampleRatio.YCbCrSubsampleRatio411, 4, 1)]
        [InlineData(YCbCrImage.YCbCrSubsampleRatio.YCbCrSubsampleRatio420, 2, 2)]
        [InlineData(YCbCrImage.YCbCrSubsampleRatio.YCbCrSubsampleRatio422, 2, 1)]
        [InlineData(YCbCrImage.YCbCrSubsampleRatio.YCbCrSubsampleRatio440, 1, 2)]
        [InlineData(YCbCrImage.YCbCrSubsampleRatio.YCbCrSubsampleRatio444, 1, 1)]
        public void CalculateChrominanceSize(int ratioValue, int expectedDivX, int expectedDivY)
        {
            YCbCrImage.YCbCrSubsampleRatio ratio = (YCbCrImage.YCbCrSubsampleRatio)ratioValue;

            //this.Output.WriteLine($"RATIO: {ratio}");
            Size size = YCbCrImage.CalculateChrominanceSize(400, 400, ratio);
            //this.Output.WriteLine($"Ch Size: {size}");

            Assert.Equal(new Size(400/expectedDivX, 400/expectedDivY), size);
        }

        [Theory]
        [InlineData(YCbCrImage.YCbCrSubsampleRatio.YCbCrSubsampleRatio410, 4)]
        [InlineData(YCbCrImage.YCbCrSubsampleRatio.YCbCrSubsampleRatio411, 4)]
        [InlineData(YCbCrImage.YCbCrSubsampleRatio.YCbCrSubsampleRatio420, 2)]
        [InlineData(YCbCrImage.YCbCrSubsampleRatio.YCbCrSubsampleRatio422, 2)]
        [InlineData(YCbCrImage.YCbCrSubsampleRatio.YCbCrSubsampleRatio440, 1)]
        [InlineData(YCbCrImage.YCbCrSubsampleRatio.YCbCrSubsampleRatio444, 1)]
        public void Create(int ratioValue, int expectedCStrideDiv)
        {
            YCbCrImage.YCbCrSubsampleRatio ratio = (YCbCrImage.YCbCrSubsampleRatio)ratioValue;

            this.Output.WriteLine($"RATIO: {ratio}");

            var img = new YCbCrImage(400, 400, ratio);

            //this.PrintChannel("Y", img.YChannel);
            //this.PrintChannel("Cb", img.CbChannel);
            //this.PrintChannel("Cr", img.CrChannel);

            Assert.Equal(img.YChannel.Stride, 400);
            Assert.Equal(img.CbChannel.Stride, 400 / expectedCStrideDiv);
            Assert.Equal(img.CrChannel.Stride, 400 / expectedCStrideDiv);
        }

    }
}