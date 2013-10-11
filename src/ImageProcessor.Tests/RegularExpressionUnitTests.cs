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
        /// The brightness regex unit test.
        /// </summary>
        [TestMethod]
        public void TestBrightnessRegex()
        {
            const string Querystring = "brightness=56";
            const int Expected = 56;

            Brightness brightness = new Brightness();
            brightness.MatchRegexIndex(Querystring);

            int actual = brightness.DynamicParameter;

            Assert.AreEqual(Expected, actual);
        }

		/// <summary>
		/// The contrast regex unit test.
		/// </summary>
		[TestMethod]
		public void TestContrastRegex()
		{
			const string Querystring = "contrast=56";
			const int Expected = 56;

			Contrast contrast = new Contrast();
			contrast.MatchRegexIndex(Querystring);

			int actual = contrast.DynamicParameter;

			Assert.AreEqual(Expected, actual);
		}

		/// <summary>
		/// The constrain regex unit test.
		/// </summary>
		[TestMethod]
		public void TestConstrainRegex()
		{
			const string Querystring = "constrain=100,200";
			const int ExpectedWidth = 100;
			const int ExpectedHeight = 200;

			Constrain contrast = new Constrain();
			contrast.MatchRegexIndex(Querystring);

			int actualWidth = contrast.DynamicParameter.Width;
			int actualHeight = contrast.DynamicParameter.Height;

			Assert.AreEqual(ExpectedWidth, actualWidth);
			Assert.AreEqual(ExpectedHeight, actualHeight);
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

            Assert.AreEqual(expected, actual);
        }

        /// <summary>
        /// The rounded corners regex unit test.
        /// </summary>
        [TestMethod]
        public void TestRoundedCornersRegex()
        {
            const string Querystring = "roundedcorners=30";
            RoundedCornerLayer expected = new RoundedCornerLayer(30, true, true, true, true);

            RoundedCorners roundedCorners = new RoundedCorners();
            roundedCorners.MatchRegexIndex(Querystring);

            RoundedCornerLayer actual = roundedCorners.DynamicParameter;

            Assert.AreEqual(expected, actual);
        }
        #endregion
    }
}
