// --------------------------------------------------------------------------------------------------------------------
// <copyright file="CropLayerUnitTests.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------
namespace ImageProcessor.UnitTests.Imaging
{
    using ImageProcessor.Imaging;
    using FluentAssertions;
    using NUnit.Framework;

    /// <summary>
    /// Test harness for the <see cref="CropLayer" /> class
    /// </summary>
    [TestFixture]
    public class CropLayerUnitTests
    {
        /// <summary>
        /// Tests that the constructor saves the provided data
        /// </summary>
        /// <param name="left">
        /// The left position.
        /// </param>
        /// <param name="top">
        /// The top position.
        /// </param>
        /// <param name="right">
        /// The right position.
        /// </param>
        /// <param name="bottom">
        /// The bottom position.
        /// </param>
        /// <param name="mode">
        /// The <see cref="CropMode"/>.
        /// </param>
        [Test]
        [TestCase(10.5F, 11.2F, 15.6F, 108.9F, CropMode.Percentage)]
        [TestCase(15.1F, 20.7F, 65.8F, 156.7F, CropMode.Pixels)]
        public void ConstructorSavesData(float left, float top, float right, float bottom, CropMode mode)
        {
            CropLayer cl = new CropLayer(left, top, right, bottom, mode);

            cl.Left.Should().Be(left);
            cl.Top.Should().Be(top);
            cl.Right.Should().Be(right);
            cl.Bottom.Should().Be(bottom);
            cl.CropMode.Should().Be(mode);
        }
    }
}