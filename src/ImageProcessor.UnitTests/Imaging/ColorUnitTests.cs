// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ColorUnitTests.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Test harness for the color classes.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.UnitTests.Imaging
{
    using System.Drawing;
    using ImageProcessor.Imaging.Colors;
    using NUnit.Framework;

    /// <summary>
    /// Test harness for the color classes.
    /// </summary>
    [TestFixture]
    public class ColorUnitTests
    {
        /// <summary>
        /// Tests the <see cref="RgbaColor"/> struct equality operators.
        /// </summary>
        [Test]
        public void TestRGBAEquality()
        {
            RgbaColor first = RgbaColor.FromColor(Color.White);
            RgbaColor second = RgbaColor.FromColor(Color.White);

            Assert.AreEqual(first, second);
        }

        /// <summary>
        /// Tests the <see cref="RgbaColor"/> struct equality operators.
        /// </summary>
        [Test]
        public void TestHSLAEquality()
        {
            HslaColor first = HslaColor.FromColor(Color.White);
            HslaColor second = HslaColor.FromColor(Color.White);

            Assert.AreEqual(first, second);
        }

        /// <summary>
        /// Test conversion to and from a HSLA color.
        /// </summary>
        [Test]
        public void TestHSLAConversion()
        {
            const string Hex = "#FEFFFE";

            Color color = ColorTranslator.FromHtml(Hex);
            HslaColor hslaColor = HslaColor.FromColor(color);
            string outPut = ColorTranslator.ToHtml(hslaColor);
            Assert.AreEqual(Hex, outPut);
        }
    }
}
