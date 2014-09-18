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
    using System.Diagnostics.CodeAnalysis;
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
        public void TestRgbaEquality()
        {
            RgbaColor first = RgbaColor.FromColor(Color.White);
            RgbaColor second = RgbaColor.FromColor(Color.White);

            Assert.AreEqual(first, second);
        }

        /// <summary>
        /// Tests the <see cref="HslaColor"/> struct equality operators.
        /// </summary>
        [Test]
        public void TestHslaEquality()
        {
            HslaColor first = HslaColor.FromColor(Color.White);
            HslaColor second = HslaColor.FromColor(Color.White);

            Assert.AreEqual(first, second);
        }

        /// <summary>
        /// Tests the <see cref="YCbCrColor"/> struct equality operators.
        /// </summary>
        [Test]
        public void TestYCbCrEquality()
        {
            YCbCrColor first = YCbCrColor.FromColor(Color.White);
            YCbCrColor second = YCbCrColor.FromColor(Color.White);

            Assert.AreEqual(first, second);
        }

        /// <summary>
        /// Test conversion to and from a <see cref="HslaColor"/>.
        /// </summary>
        /// <param name="expected">
        /// The expected output.
        /// </param>
        [Test]
        [TestCase("#FFFFFF")]
        [TestCase("#FEFFFE")]
        [TestCase("#F0F8FF")]
        [TestCase("#000000")]
        [TestCase("#CCFF33")]
        [TestCase("#00FF00")]
        [TestCase("#FF00FF")]
        [TestCase("#990000")]
        [TestCase("#5C955C")]
        [TestCase("#5C5C95")]
        [TestCase("#3F3F66")]
        [TestCase("#FFFFBB")]
        [TestCase("#FF002B")]
        [TestCase("#00ABFF")]
        public void TestHslaConversion(string expected)
        {
            Color color = ColorTranslator.FromHtml(expected);
            HslaColor hslaColor = HslaColor.FromColor(color);
            string result = ColorTranslator.ToHtml(hslaColor);
            Assert.AreEqual(expected, result);
        }

        /// <summary>
        /// Test conversion to and from a <see cref="RgbaColor"/>.
        /// </summary>
        /// <param name="expected">
        /// The expected output.
        /// </param>
        [Test]
        [TestCase("#FFFFFF")]
        [TestCase("#FEFFFE")]
        [TestCase("#F0F8FF")]
        [TestCase("#000000")]
        [TestCase("#CCFF33")]
        [TestCase("#00FF00")]
        [TestCase("#FF00FF")]
        [TestCase("#990000")]
        [TestCase("#5C955C")]
        [TestCase("#5C5C95")]
        [TestCase("#3F3F66")]
        [TestCase("#FFFFBB")]
        [TestCase("#FF002B")]
        [TestCase("#00ABFF")]
        public void TestRgbaConversion(string expected)
        {
            Color color = ColorTranslator.FromHtml(expected);
            RgbaColor rgbaColor = RgbaColor.FromColor(color);
            string result = ColorTranslator.ToHtml(rgbaColor);
            Assert.AreEqual(expected, result);
        }

        /// <summary>
        /// Test conversion to and from a <see cref="RgbaColor"/>.
        /// </summary>
        /// <param name="expected">
        /// The expected output.
        /// </param>
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1305:FieldNamesMustNotUseHungarianNotation", Justification = "Reviewed. Suppression is OK here."), Test]
        [TestCase("#FFFFFF")]
        [TestCase("#FEFFFE")]
        [TestCase("#F0F8FF")]
        [TestCase("#000000")]
        [TestCase("#CCFF33")]
        [TestCase("#00FF00")]
        [TestCase("#FF00FF")]
        [TestCase("#990000")]
        [TestCase("#5C955C")]
        [TestCase("#5C5C95")]
        [TestCase("#3F3F66")]
        [TestCase("#FFFFBB")]
        [TestCase("#FF002B")]
        [TestCase("#00ABFF")]
        public void TestYCbCrConversion(string expected)
        {
            Color color = ColorTranslator.FromHtml(expected);
            YCbCrColor yCbCrColor = YCbCrColor.FromColor(color);
            string result = ColorTranslator.ToHtml(yCbCrColor);
            Assert.AreEqual(expected, result);
        }
    }
}
