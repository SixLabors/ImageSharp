// -----------------------------------------------------------------------
// <copyright file="RegularExpressionUnitTests.cs" company="James South">
//     Copyright (c) James South.
//     Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------
namespace ImageProcessor.Tests
{
    #region Using
    using System.Drawing;
    using ImageProcessor.Imaging;
    using ImageProcessor.Processors;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    #endregion

    /// <summary>
    /// The regular expression unit tests.
    /// This is a work in progress. YAWN!
    /// </summary>
    [TestClass]
    public class RegularExpressionUnitTests
    {
        #region Regular Expression Tests

        /// <summary>
        /// The alpha regex unit test.
        /// </summary>
        [TestMethod]
        public void TestAlphaRegex()
        {
            const string Querystring = "alpha=56";
            const int Expected = 56;

            Alpha alpha = new Alpha();
            alpha.MatchRegexIndex(Querystring);

            int actual = alpha.DynamicParameter;

            Assert.AreEqual(Expected, actual);
        }

        /// <summary>
        /// The rotate regex unit test.
        /// </summary>
        [TestMethod]
        public void TestCropRegex()
        {
            const string Querystring = "crop=0-0-150-300";
            Rectangle expected = new Rectangle(0, 0, 150, 300);

            Crop crop = new Crop();
            crop.MatchRegexIndex(Querystring);

            Rectangle actual = crop.DynamicParameter;

            Assert.AreEqual(expected, actual);
        }

        /// <summary>
        /// The filter regex unit test.
        /// </summary>
        [TestMethod]
        public void TestFilterRegex()
        {
            // Should really write more for the other filters.
            const string Querystring = "filter=lomograph";
            const string Expected = "lomograph";

            Filter filter = new Filter();
            filter.MatchRegexIndex(Querystring);

            string actual = filter.DynamicParameter;

            Assert.AreEqual(Expected, actual);
        }

        /// <summary>
        /// The format regex unit test.
        /// </summary>
        [TestMethod]
        public void TestFormatRegex()
        {
            const string Querystring = "format=gif";
            const string Expected = "gif";

            Format format = new Format();
            format.MatchRegexIndex(Querystring);

            string actual = format.DynamicParameter;

            Assert.AreEqual(Expected, actual);
        }

        /// <summary>
        /// The quality regex unit test.
        /// </summary>
        [TestMethod]
        public void TestQualityRegex()
        {
            const string Querystring = "quality=56";
            const int Expected = 56;

            Quality quality = new Quality();
            quality.MatchRegexIndex(Querystring);

            int actual = quality.DynamicParameter;

            Assert.AreEqual(Expected, actual);
        }

        /// <summary>
        /// The resize regex unit test.
        /// </summary>
        [TestMethod]
        public void TestResizeRegex()
        {
            const string Querystring = "width=300";
            Size expected = new Size(300, 0);

            Resize resize = new Resize();

            resize.MatchRegexIndex(Querystring);
            Size actual = resize.DynamicParameter;

            Assert.AreEqual(expected, actual);
        }

        /// <summary>
        /// The rotate regex unit test.
        /// </summary>
        [TestMethod]
        public void TestRotateRegex()
        {
            const string Querystring = "rotate=270";
            RotateLayer expected = new RotateLayer(270, Color.Transparent);

            Rotate rotate = new Rotate();
            rotate.MatchRegexIndex(Querystring);

            RotateLayer actual = rotate.DynamicParameter;

            // Can't use are equal on rotatelayer for some reason so test the two properties.
            Assert.AreEqual(expected.Angle, actual.Angle);
            Assert.AreEqual(expected.BackgroundColor, actual.BackgroundColor);
        }
        #endregion
    }
}
