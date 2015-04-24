// --------------------------------------------------------------------------------------------------------------------
// <copyright file="QueryParamParserUnitTests.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   The query parameter parser unit tests.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Web.UnitTests
{
    using System;
    using System.Collections.Specialized;
    using System.Drawing;
    using System.Web;

    using ImageProcessor.Imaging;
    using ImageProcessor.Web.Helpers;

    using NUnit.Framework;

    /// <summary>
    /// The query parameter parser unit tests.
    /// </summary>
    [TestFixture]
    public class QueryParamParserUnitTests
    {
        [Test]
        public void SingleParamOutputNotNull()
        {
            const string Param = "query=1";
            NameValueCollection query = HttpUtility.ParseQueryString(Param);
            string result = QueryParamParser.Instance.ParseValue<string>(query["query"]);
            Assert.IsNotNull(result);
        }

        [Test]
        [TestCase("foo=4.5&foo=2.3", "foo")]
        [TestCase("query=1.5&query=2.5", "query")]
        public void MultipleParamOutputNotNull(string queryString, string parameter)
        {
            NameValueCollection query = HttpUtility.ParseQueryString(queryString);
            float[] result = QueryParamParser.Instance.ParseValue<float[]>(query[parameter]);
            Assert.IsNotNull(result);
        }

        [TestCase("", "entropycrop")]
        [TestCase("entropycrop=0", "entropycrop")]
        [TestCase("entropycrop=128", "entropycrop")]
        public void IntNotNull(string queryString, string parameter)
        {
            NameValueCollection query = HttpUtility.ParseQueryString(queryString);
            int result = QueryParamParser.Instance.ParseValue<int>(query[parameter]);
            Assert.IsNotNull(result);
        }

        [TestCase("", "entropycrop", 0)]
        [TestCase("entropycrop=0", "entropycrop", 0)]
        [TestCase("entropycrop=128", "entropycrop", 128)]
        [TestCase("entropycrop=128.4", "entropycrop", 128)]
        [TestCase("entropycrop=128.5", "entropycrop", 129)]
        public void IntRounded(string queryString, string parameter, int expected)
        {
            NameValueCollection query = HttpUtility.ParseQueryString(queryString);
            int result = (int)Math.Round(QueryParamParser.Instance.ParseValue<float>(query[parameter]), MidpointRounding.AwayFromZero);
            Assert.AreEqual(result, expected);
        }

        [TestCase("", "entropycrop")]
        [TestCase("entropycrop=0", "entropycrop")]
        [TestCase("entropycrop=128", "entropycrop")]
        [TestCase("entropycrop=256", "entropycrop")]
        public void ByteNotNull(string queryString, string parameter)
        {
            NameValueCollection query = HttpUtility.ParseQueryString(queryString);
            byte result = QueryParamParser.Instance.ParseValue<byte>(query[parameter]);
            Assert.IsNotNull(result);
        }

        [TestCase("", "greyscale")]
        [TestCase("greyscale=false", "greyscale")]
        [TestCase("greyscale=true", "greyscale")]
        public void BoolNotNull(string queryString, string parameter)
        {
            NameValueCollection query = HttpUtility.ParseQueryString(queryString);
            bool result = QueryParamParser.Instance.ParseValue<bool>(query[parameter]);
            Assert.IsNotNull(result);
        }

        //[TestCase("", "rect")]
        [TestCase("rect=0,0,100,100", "rect")]
        public void RectangleNotNull(string queryString, string parameter)
        {
            NameValueCollection query = HttpUtility.ParseQueryString(queryString);
            Rectangle result = QueryParamParser.Instance.ParseValue<Rectangle>(query[parameter]);

            Assert.IsNotNull(result);
        }

        [TestCase("", "size")]
        [TestCase("size=1,1", "size")]
        [TestCase("size=1", "size")]
        public void SizeNotNull(string queryString, string parameter)
        {
            NameValueCollection query = HttpUtility.ParseQueryString(queryString);
            Size result = QueryParamParser.Instance.ParseValue<Size>(query[parameter]);

            Assert.IsNotNull(result);
        }

        [TestCase("", "point")]
        [TestCase("point=1,1", "point")]
        public void PointNotNull(string queryString, string parameter)
        {
            NameValueCollection query = HttpUtility.ParseQueryString(queryString);
            Point result = QueryParamParser.Instance.ParseValue<Point>(query[parameter]);
            Assert.IsNotNull(result);
        }

        [TestCase("point=1.5,1.5", "point")]
        public void PointFNotNull(string queryString, string parameter)
        {
            NameValueCollection query = HttpUtility.ParseQueryString(queryString);
            PointF result = QueryParamParser.Instance.ParseValue<PointF>(query[parameter]);
            Assert.IsNotNull(result);
        }

        [TestCase("resizemode=foo", "resizemode")]
        [TestCase("resizemode=Max", "resizemode")]
        [TestCase("resizemode=max", "resizemode")]
        public void EnumNotNull(string queryString, string parameter)
        {
            NameValueCollection query = HttpUtility.ParseQueryString(queryString);
            ResizeMode result = QueryParamParser.Instance.ParseValue<ResizeMode>(query[parameter]);
            Assert.IsNotNull(result);
        }

        [TestCase("", "resizemode")]
        [TestCase("resizemode=foo", "resizemode")]
        public void EnumIsDefault(string queryString, string parameter)
        {
            NameValueCollection query = HttpUtility.ParseQueryString(queryString);
            ResizeMode result = QueryParamParser.Instance.ParseValue<ResizeMode>(query[parameter]);
            Assert.AreEqual(result, ResizeMode.Pad);
        }

        [TestCase("resizemode=Max", "resizemode", ResizeMode.Max)]
        [TestCase("resizemode=max", "resizemode", ResizeMode.Max)]
        [TestCase("resizemode=crop", "resizemode", ResizeMode.Crop)]
        public void EnumMatch(string queryString, string parameter, ResizeMode expected)
        {
            NameValueCollection query = HttpUtility.ParseQueryString(queryString);
            ResizeMode result = QueryParamParser.Instance.ParseValue<ResizeMode>(query[parameter]);
            Assert.AreEqual(result, expected);
        }

        [TestCase("color=white", "color")]
        public void ColorNotNull(string queryString, string parameter)
        {
            NameValueCollection query = HttpUtility.ParseQueryString(queryString);
            Color result = QueryParamParser.Instance.ParseValue<Color>(query[parameter]);
            Assert.IsNotNull(result);
        }

        [TestCase("color=255,255,255,255", "color")]
        [TestCase("color=#fff", "color")]
        [TestCase("color=#ffffff", "color")]
        [TestCase("color=fff", "color")]
        [TestCase("color=ffffff", "color")]
        [TestCase("color=white", "color")]
        [TestCase("color=White", "color")]
        public void ColorMatch(string queryString, string parameter)
        {
            Color expected = Color.White;
            NameValueCollection query = HttpUtility.ParseQueryString(queryString);
            Color result = QueryParamParser.Instance.ParseValue<Color>(query[parameter]);
            Assert.AreEqual(result.ToArgb(), expected.ToArgb());
        }
    }
}
