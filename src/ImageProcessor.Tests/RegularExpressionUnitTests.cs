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
    using ImageProcessor.Imaging.Filters;
    using ImageProcessor.Imaging.Formats;
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

            Web.Processors.Alpha alpha = new Web.Processors.Alpha();
            alpha.MatchRegexIndex(Querystring);

            int actual = alpha.Processor.DynamicParameter;

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

            Web.Processors.Brightness brightness = new Web.Processors.Brightness();
            brightness.MatchRegexIndex(Querystring);

            int actual = brightness.Processor.DynamicParameter;

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

            Web.Processors.Contrast contrast = new Web.Processors.Contrast();
            contrast.MatchRegexIndex(Querystring);

            int actual = contrast.Processor.DynamicParameter;

            Assert.AreEqual(Expected, actual);
        }

        /// <summary>
        /// The rotate regex unit test.
        /// </summary>
        [TestMethod]
        public void TestCropRegex()
        {
            const string Querystring = "crop=0,0,150,300";
            CropLayer expected = new CropLayer(0, 0, 150, 300, CropMode.Pixels);

            Web.Processors.Crop crop = new Web.Processors.Crop();
            crop.MatchRegexIndex(Querystring);

            CropLayer actual = crop.Processor.DynamicParameter;
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
            IMatrixFilter expected = MatrixFilters.Lomograph;

            Web.Processors.Filter filter = new Web.Processors.Filter();
            filter.MatchRegexIndex(Querystring);

            IMatrixFilter actual = filter.Processor.DynamicParameter;

            Assert.AreEqual(expected, actual);
        }

        /// <summary>
        /// The format regex unit test.
        /// </summary>
        [TestMethod]
        public void TestFormatRegex()
        {
            const string Querystring = "format=gif";
            ISupportedImageFormat expected = new GifFormat();

            Web.Processors.Format format = new Web.Processors.Format();
            format.MatchRegexIndex(Querystring);

            ISupportedImageFormat actual = format.Processor.DynamicParameter;

            Assert.AreEqual(expected, actual);
        }

        /// <summary>
        /// The quality regex unit test.
        /// </summary>
        [TestMethod]
        public void TestQualityRegex()
        {
            const string Querystring = "quality=56";
            const int Expected = 56;

            Web.Processors.Quality quality = new Web.Processors.Quality();
            quality.MatchRegexIndex(Querystring);

            int actual = quality.Processor.DynamicParameter;

            Assert.AreEqual(Expected, actual);
        }

        /// <summary>
        /// The resize regex unit test.
        /// </summary>
        [TestMethod]
        public void TestResizeRegex()
        {
            const string Querystring = "width=300";
            ResizeLayer expected = new ResizeLayer(new Size(300, 0));

            Web.Processors.Resize resize = new Web.Processors.Resize();

            resize.MatchRegexIndex(Querystring);
            ResizeLayer actual = resize.Processor.DynamicParameter;

            Assert.AreEqual(expected, actual);
        }

        /// <summary>
        /// The rotate regex unit test.
        /// </summary>
        [TestMethod]
        public void TestRotateRegex()
        {
            const string Querystring = "rotate=270";
            const int Expected = 270;

            Web.Processors.Rotate rotate = new Web.Processors.Rotate();
            rotate.MatchRegexIndex(Querystring);

            int actual = rotate.Processor.DynamicParameter;

            Assert.AreEqual(Expected, actual);
        }

        /// <summary>
        /// The rounded corners regex unit test.
        /// </summary>
        [TestMethod]
        public void TestRoundedCornersRegex()
        {
            const string Querystring = "roundedcorners=30";
            RoundedCornerLayer expected = new RoundedCornerLayer(30, true, true, true, true);
            Web.Processors.RoundedCorners roundedCorners = new Web.Processors.RoundedCorners();
            roundedCorners.MatchRegexIndex(Querystring);

            RoundedCornerLayer actual = roundedCorners.Processor.DynamicParameter;

            Assert.AreEqual(expected, actual);
        }

        /// <summary>
        /// The tint regex unit test.
        /// </summary>
        [TestMethod]
        public void TestTintRegex()
        {
            const string HexQuerystring = "tint=6aa6cc";
            const string RgbaQuerystring = "tint=106,166,204,255";
            Color expectedHex = ColorTranslator.FromHtml("#" + "6aa6cc");
            Color expectedRgba = Color.FromArgb(255, 106, 166, 204);

            Web.Processors.Tint tint = new Web.Processors.Tint();
            tint.MatchRegexIndex(HexQuerystring);
            Color actualHex = tint.Processor.DynamicParameter;
            Assert.AreEqual(expectedHex, actualHex);

            tint = new Web.Processors.Tint();
            tint.MatchRegexIndex(RgbaQuerystring);
            Color actualRgba = tint.Processor.DynamicParameter;
            Assert.AreEqual(expectedRgba, actualRgba);
        }
        #endregion
    }
}