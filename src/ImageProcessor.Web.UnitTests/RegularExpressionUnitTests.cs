// --------------------------------------------------------------------------------------------------------------------
// <copyright file="RegularExpressionUnitTests.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Unit tests for the ImageProcessor regular expressions
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Web.UnitTests
{
    using System.Drawing;
    using ImageProcessor.Imaging;
    using ImageProcessor.Imaging.Filters;
    using ImageProcessor.Imaging.Formats;
    using NUnit.Framework;

    /// <summary>
    /// Test harness for the regular expressions
    /// </summary>
    [TestFixture]
    public class RegularExpressionUnitTests
    {
        /// <summary>
        /// The alpha regex unit test.
        /// </summary>
        [Test]
        public void TestAlphaRegex()
        {
            const string Querystring = "alpha=56";
            const int Expected = 56;

            Processors.Alpha alpha = new Processors.Alpha();
            alpha.MatchRegexIndex(Querystring);

            int actual = alpha.Processor.DynamicParameter;

            Assert.AreEqual(Expected, actual);
        }

        /// <summary>
        /// The brightness regex unit test.
        /// </summary>
        [Test]
        public void TestBrightnessRegex()
        {
            const string Querystring = "brightness=56";
            const int Expected = 56;

            Processors.Brightness brightness = new Processors.Brightness();
            brightness.MatchRegexIndex(Querystring);

            int actual = brightness.Processor.DynamicParameter;

            Assert.AreEqual(Expected, actual);
        }

        /// <summary>
        /// The contrast regex unit test.
        /// </summary>
        [Test]
        public void TestContrastRegex()
        {
            const string Querystring = "contrast=56";
            const int Expected = 56;

            Processors.Contrast contrast = new Processors.Contrast();
            contrast.MatchRegexIndex(Querystring);

            int actual = contrast.Processor.DynamicParameter;

            Assert.AreEqual(Expected, actual);
        }

        /// <summary>
        /// The rotate regex unit test.
        /// </summary>
        [Test]
        public void TestCropRegex()
        {
            const string Querystring = "crop=0,0,150,300";
            CropLayer expected = new CropLayer(0, 0, 150, 300, CropMode.Pixels);

            Processors.Crop crop = new Processors.Crop();
            crop.MatchRegexIndex(Querystring);

            CropLayer actual = crop.Processor.DynamicParameter;
            Assert.AreEqual(expected, actual);
        }

        /// <summary>
        /// The filter regex unit test.
        /// </summary>
        [Test]
        public void TestFilterRegex()
        {
            // Should really write more for the other filters.
            const string Querystring = "filter=lomograph";
            IMatrixFilter expected = MatrixFilters.Lomograph;

            Processors.Filter filter = new Processors.Filter();
            filter.MatchRegexIndex(Querystring);

            IMatrixFilter actual = filter.Processor.DynamicParameter;

            Assert.AreEqual(expected, actual);
        }

        /// <summary>
        /// The format regex unit test.
        /// </summary>
        [Test]
        public void TestFormatRegex()
        {
            const string Querystring = "format=gif";
            ISupportedImageFormat expected = new GifFormat();

            Processors.Format format = new Processors.Format();
            format.MatchRegexIndex(Querystring);

            ISupportedImageFormat actual = format.Processor.DynamicParameter;

            Assert.AreEqual(expected, actual);
        }

        /// <summary>
        /// The quality regex unit test.
        /// </summary>
        [Test]
        public void TestQualityRegex()
        {
            const string Querystring = "quality=56";
            const int Expected = 56;

            Processors.Quality quality = new Processors.Quality();
            quality.MatchRegexIndex(Querystring);

            int actual = quality.Processor.DynamicParameter;

            Assert.AreEqual(Expected, actual);
        }

        /// <summary>
        /// The resize regex unit test.
        /// </summary>
        [Test]
        public void TestResizeRegex()
        {
            const string Querystring = "width=300";
            ResizeLayer expected = new ResizeLayer(new Size(300, 0));

            Processors.Resize resize = new Processors.Resize();

            resize.MatchRegexIndex(Querystring);
            ResizeLayer actual = resize.Processor.DynamicParameter;

            Assert.AreEqual(expected, actual);
        }

        /// <summary>
        /// The rotate regex unit test.
        /// </summary>
        [Test]
        public void TestRotateRegex()
        {
            const string Querystring = "rotate=270";
            const int Expected = 270;

            Processors.Rotate rotate = new Processors.Rotate();
            rotate.MatchRegexIndex(Querystring);

            int actual = rotate.Processor.DynamicParameter;

            Assert.AreEqual(Expected, actual);
        }

        /// <summary>
        /// The rounded corners regex unit test.
        /// </summary>
        [Test]
        public void TestRoundedCornersRegex()
        {
            const string Querystring = "roundedcorners=30";
            RoundedCornerLayer expected = new RoundedCornerLayer(30, true, true, true, true);
            Processors.RoundedCorners roundedCorners = new Processors.RoundedCorners();
            roundedCorners.MatchRegexIndex(Querystring);

            RoundedCornerLayer actual = roundedCorners.Processor.DynamicParameter;

            Assert.AreEqual(expected, actual);
        }

        /// <summary>
        /// The tint regex unit test.
        /// </summary>
        [Test]
        public void TestTintRegex()
        {
            const string HexQuerystring = "tint=6aa6cc";
            const string RgbaQuerystring = "tint=106,166,204,255";
            Color expectedHex = ColorTranslator.FromHtml("#" + "6aa6cc");
            Color expectedRgba = Color.FromArgb(255, 106, 166, 204);

            Processors.Tint tint = new Processors.Tint();
            tint.MatchRegexIndex(HexQuerystring);
            Color actualHex = tint.Processor.DynamicParameter;
            Assert.AreEqual(expectedHex, actualHex);

            tint = new Processors.Tint();
            tint.MatchRegexIndex(RgbaQuerystring);
            Color actualRgba = tint.Processor.DynamicParameter;
            Assert.AreEqual(expectedRgba, actualRgba);
        }
    }
}