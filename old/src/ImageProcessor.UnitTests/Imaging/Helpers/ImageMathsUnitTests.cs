// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ImageMathsUnitTests.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Test harness for the image math unit tests
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.UnitTests.Imaging.Helpers
{
    using System.Drawing;
    using FluentAssertions;
    using ImageProcessor.Imaging.Helpers;
    using NUnit.Framework;

    /// <summary>
    /// Test harness for the image math unit tests
    /// </summary>
    public class ImageMathsUnitTests
    {
        /// <summary>
        /// Tests that the bounding rectangle of a rotated image is calculated
        /// </summary>
        /// <param name="width">The width of the image.</param>
        /// <param name="height">The height of the image.</param>
        /// <param name="angle">The rotation angle.</param>
        /// <param name="expectedWidth">The expected width.</param>
        /// <param name="expectedHeight">The expected height.</param>
        [Test]
        [TestCase(100, 100, 45, 141, 141)]
        [TestCase(100, 100, 30, 137, 137)]
        [TestCase(100, 200, 50, 217, 205)]
        [TestCase(100, 200, -50, 217, 205)]
        public void BoundingRotatedRectangleIsCalculated(int width, int height, float angle, int expectedWidth, int expectedHeight)
        {
            Rectangle result = ImageMaths.GetBoundingRotatedRectangle(width, height, angle);

            result.Width.Should().Be(expectedWidth, "because the rotated width should have been calculated");
            result.Height.Should().Be(expectedHeight, "because the rotated height should have been calculated");
        }

        /// <summary>
        /// Tests that the zoom needed for an "inside" rotation is calculated
        /// </summary>
        /// <param name="imageWidth">Width of the image.</param>
        /// <param name="imageHeight">Height of the image.</param>
        /// <param name="angle">The rotation angle.</param>
        /// <param name="expected">The expected zoom.</param>
        [Test]
        [TestCase(100, 100, 45, 1.41f)]
        [TestCase(100, 100, 15, 1.22f)]
        [TestCase(100, 200, 45, 2.12f)]
        [TestCase(200, 100, 45, 2.12f)]
        [TestCase(600, 450, 20, 1.39f)]
        [TestCase(600, 450, 45, 1.64f)]
        [TestCase(100, 200, -45, 2.12f)]
        public void RotationZoomIsCalculated(int imageWidth, int imageHeight, float angle, float expected)
        {
            float result = ImageMaths.ZoomAfterRotation(imageWidth, imageHeight, angle);

            result.Should().BeApproximately(expected, 0.01f, "because the zoom level after rotation should have been calculated");

            result.Should().BePositive("because we're always zooming in so the zoom level should always be positive");

            result.Should().BeGreaterOrEqualTo(1, "because the zoom should always increase the size and not reduce it");
        }
    }
}