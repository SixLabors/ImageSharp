namespace ImageProcessor.UnitTests.Imaging
{
    using System.Drawing;
    using FluentAssertions;
    using ImageProcessor.Imaging;
    using NUnit.Framework;

    public class RotationUnitTests
    {
        [Test]
        [TestCase(100, 100, 45, 141, 141)]
        [TestCase(100, 100, 30, 137, 137)]
        [TestCase(100, 200, 50, 217, 205)]
        public void NewSizeAfterRotationIsCalculated(int width, int height, float angle, int expectedWidth, int expectedHeight)
        {
            Size result = Rotation.NewSizeAfterRotation(width, height, angle);

            result.Width.Should().Be(expectedWidth, "because the rotated width should have been calculated");
            result.Height.Should().Be(expectedHeight, "because the rotated height should have been calculated");
        }

        [Test]
        [TestCase(100, 100, 45, 1.41f)]
        [TestCase(100, 100, 15, 1.22f)]
        [TestCase(100, 200, 45, 2.12f)]
        public void RotationZoomIsCalculated(int imageWidth, int imageHeight, float angle, float expected)
        {
            float result = Rotation.ZoomAfterRotation(imageWidth, imageHeight, angle);

            result.Should().BeApproximately(expected, 0.01f, "because the zoom level after rotation should have been calculated");
        }
    }
}