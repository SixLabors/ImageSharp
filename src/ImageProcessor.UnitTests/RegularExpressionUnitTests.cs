namespace ImageProcessor.UnitTests
{
    using System;
    using System.Drawing;
    using ImageProcessor.Imaging;
    using ImageProcessor.Processors;
    using NUnit.Framework;

    [TestFixture ()]
    public class RegularExpressionUnitTests
    {
        /// <summary>
        /// The alpha regex unit test.
        /// </summary>
        [Test ()]
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
        [Test ()]
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
        [Test ()]
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
        /// The rotate regex unit test.
        /// </summary>
        [Test ()]
        public void TestCropRegex()
        {
            const string Querystring = "crop=0,0,150,300";
            CropLayer expected = new CropLayer(0, 0, 150, 300, CropMode.Pixels);

            Crop crop = new Crop();
            crop.MatchRegexIndex(Querystring);

            CropLayer actual = crop.DynamicParameter;
            Assert.AreEqual(expected, actual);
        }

        /// <summary>
        /// The filter regex unit test.
        /// </summary>
        [Test ()]
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
        [Test ()]
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
        [Test ()]
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
        [Test ()]
        public void TestResizeRegex()
        {
            const string Querystring = "width=300";
            ResizeLayer expected = new ResizeLayer(new Size(300, 0));

            Resize resize = new Resize();

            resize.MatchRegexIndex(Querystring);
            ResizeLayer actual = resize.DynamicParameter;

            Assert.AreEqual(expected, actual);
        }

        /// <summary>
        /// The rotate regex unit test.
        /// </summary>
        [Test ()]
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
        [Test ()]
        public void TestRoundedCornersRegex()
        {
            const string Querystring = "roundedcorners=30";
            RoundedCornerLayer expected = new RoundedCornerLayer(30, true, true, true, true);

            RoundedCorners roundedCorners = new RoundedCorners();
            roundedCorners.MatchRegexIndex(Querystring);

            RoundedCornerLayer actual = roundedCorners.DynamicParameter;

            Assert.AreEqual(expected, actual);
        }

        /// <summary>
        /// The rounded corners regex unit test.
        /// </summary>
        [Test ()]
        public void TestTintRegex()
        {
            const string HexQuerystring = "tint=6aa6cc";
            const string RgbaQuerystring = "tint=106,166,204,255";
            Color expectedHex = ColorTranslator.FromHtml("#" + "6aa6cc");
            Color expectedRgba = Color.FromArgb(255, 106, 166, 204);

            Tint tint = new Tint();
            tint.MatchRegexIndex(HexQuerystring);
            Color actualHex = tint.DynamicParameter;
            Assert.AreEqual(expectedHex, actualHex);

            tint = new Tint();
            tint.MatchRegexIndex(RgbaQuerystring);
            Color actualRgba = tint.DynamicParameter;
            Assert.AreEqual(expectedRgba, actualRgba);
        }
    }
}

