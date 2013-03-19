namespace ImageProcessor.Tests
{
    #region Using
    using System;
    using System.Diagnostics;
    using System.Drawing;
    using System.IO;
    using System.Text.RegularExpressions;
    using ImageProcessor.Imaging;
    using ImageProcessor.Processors;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    #endregion

    [TestClass]
    public class RegularExpressionUnitTests
    {
        #region Regular Expression Tests
        [TestMethod]
        public void TestAlphaRegex()
        {
            const string querystring = "alpha=56";
            const int expected = 56;

            Alpha alpha = new Alpha();
            alpha.MatchRegexIndex(querystring);

            int actual = alpha.DynamicParameter;

            Assert.AreEqual<int>(expected, actual);
        }

        [TestMethod]
        public void TestFormatRegex()
        {
            string querystring = "format=gif";
            string expected = "gif";

            Format format = new Format();
            format.MatchRegexIndex(querystring);

            string actual = format.DynamicParameter;

            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void TestQualityRegex()
        {
            string querystring = "quality=56";
            int expected = 56;

            Quality quality = new Quality();
            quality.MatchRegexIndex(querystring);

            int actual = quality.DynamicParameter;

            Assert.AreEqual<int>(expected, actual);
        }

        [TestMethod]
        public void TestRotateRegex()
        {
            // Why does this fail?
            string querystring = "rotate=270";
            RotateLayer expected = new RotateLayer
            {
                Angle = 270,
                BackgroundColor = Color.Transparent
            };

            Rotate rotate = new Rotate();
            rotate.MatchRegexIndex(querystring);

            RotateLayer actual = rotate.DynamicParameter;

            Assert.AreEqual<RotateLayer>(expected, actual);
        }
        #endregion
    }
}
