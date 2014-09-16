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
        /// Tests the <see cref="RGBAColor"/> struct equality operators.
        /// </summary>
        [Test]
        public void TestRGBAEquality()
        {
            RGBAColor first = new RGBAColor(Color.White);
            RGBAColor second = new RGBAColor(Color.White);

            Assert.AreEqual(first, second);
        }

        /// <summary>
        /// Tests the <see cref="RGBAColor"/> struct equality operators.
        /// </summary>
        [Test]
        public void TestHSLAEquality()
        {
            HSLAColor first = new HSLAColor(Color.White);
            HSLAColor second = new HSLAColor(Color.White);

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
            HSLAColor hslaColor = new HSLAColor(color);
            string outPut = ColorTranslator.ToHtml(hslaColor);
            Assert.AreEqual(Hex, outPut);
        }
    }
}
