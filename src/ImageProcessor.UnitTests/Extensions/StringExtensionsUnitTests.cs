// --------------------------------------------------------------------------------------------------------------------
// <copyright file="StringExtensionsUnitTests.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Provides a test harness for the string extensions
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.UnitTests
{
    using System;
    using System.Collections.Generic;
    using Common.Extensions;
    using NUnit.Framework;

    /// <summary>
    /// Test harness for the string extensions
    /// </summary>
    [TestFixture]
    public class StringExtensionsUnitTests
    {
        /// <summary>
        /// Tests the MD5 fingerprint
        /// </summary>
        /// <param name="input">The input value</param>
        /// <param name="expected">The expexted output of the hash</param>
        [Test]
        [TestCase("test input", "2e7f7a62eabf0993239ca17c78c464d9")]
        [TestCase("lorem ipsum dolor", "96ee002fee25e8b675a477c9750fa360")]
        [TestCase("LoReM IpSuM DoLoR", "41e201da794c7fbdb8ce5526a71c8c83")]
        [TestCase("1234567890", "e15e31c3d8898c92ab172a4311be9e84")]
        public void TestToMd5Fingerprint(string input, string expected)
        {
            var result = input.ToMD5Fingerprint();
            var comparison = result.Equals(expected, StringComparison.InvariantCultureIgnoreCase);
            Assert.True(comparison);
        }

        /// <summary>
        /// Tests the SHA-1 fingerprint
        /// </summary>
        /// <param name="input">The input value</param>
        /// <param name="expected">The expexted output of the hash</param>
        [Test]
        [TestCase("test input", "49883b34e5a0f48224dd6230f471e9dc1bdbeaf5")]
        [TestCase("lorem ipsum dolor", "75899ad8827a32493928903aecd6e931bf36f967")]
        [TestCase("LoReM IpSuM DoLoR", "2f44519afae72fc0837b72c6b53cb11338a1f916")]
        [TestCase("1234567890", "01b307acba4f54f55aafc33bb06bbbf6ca803e9a")]
        public void TestToSHA1Fingerprint(string input, string expected)
        {
            var result = input.ToSHA1Fingerprint();
            var comparison = result.Equals(expected, StringComparison.InvariantCultureIgnoreCase);
            Assert.True(comparison);
        }

        /// <summary>
        /// Tests the SHA-256 fingerprint
        /// </summary>
        /// <param name="input">The input value</param>
        /// <param name="expected">The expexted output of the hash</param>
        [Test]
        [TestCase("test input", "9dfe6f15d1ab73af898739394fd22fd72a03db01834582f24bb2e1c66c7aaeae")]
        [TestCase("lorem ipsum dolor", "ed03353266c993ea9afb9900a3ca688ddec1656941b1ca15ee1650a022616dfa")]
        [TestCase("LoReM IpSuM DoLoR", "55f6cb90ba5cd8eeb6f5f16f083ebcd48ea06c34cc5aed8e33246fc3153d3898")]
        [TestCase("1234567890", "c775e7b757ede630cd0aa1113bd102661ab38829ca52a6422ab782862f268646")]
        public void TestToSHA256Fingerprint(string input, string expected)
        {
            var result = input.ToSHA256Fingerprint();
            var comparison = result.Equals(expected, StringComparison.InvariantCultureIgnoreCase);
            Assert.True(comparison);
        }

        /// <summary>
        /// Tests the SHA-512 fingerprint
        /// </summary>
        /// <param name="input">The input value</param>
        /// <param name="expected">The expexted output of the hash</param>
        [Test]
        [TestCase("test input", "40aa1b203c9d8ee150b21c3c7cda8261492e5420c5f2b9f7380700e094c303b48e62f319c1da0e32eb40d113c5f1749cc61aeb499167890ab82f2cc9bb706971")]
        [TestCase("lorem ipsum dolor", "cd813e13d1d3919cdccc31c19d8f8b70bd25e9819f8770a011c8c7a6228536e6c9427b338cd732f2da3c0444dfebef838b745cdaf3fd5dcba8db24fc83a3f6ef")]
        [TestCase("LoReM IpSuM DoLoR", "3e4704d31f838456c0a5f0892afd392fbc79649a029d017b8104ebd00e2816d94ab4629f731765bf655088b130c51f6f47ca2f8b047749dbd992cf45e89ff431")]
        [TestCase("1234567890", "12b03226a6d8be9c6e8cd5e55dc6c7920caaa39df14aab92d5e3ea9340d1c8a4d3d0b8e4314f1f6ef131ba4bf1ceb9186ab87c801af0d5c95b1befb8cedae2b9")]
        public void TestToSHA512Fingerprint(string input, string expected)
        {
            var result = input.ToSHA512Fingerprint();
            var comparison = result.Equals(expected, StringComparison.InvariantCultureIgnoreCase);
            Assert.True(comparison);
        }

        /// <summary>
        /// Tests the pasing to an integer array
        /// </summary>
        [Test]
        public void TestToIntegerArray()
        {
            Dictionary<string, int[]> data = new Dictionary<string, int[]>();
            data.Add("123-456,78-90", new int[] { 123, 456, 78, 90 });
            data.Add("87390174,741897498,74816,748297,57355", new int[] { 87390174, 741897498, 74816, 748297, 57355 });
            data.Add("1-2-3", new int[] { 1, 2, 3 });

            foreach (var item in data)
            {
                var result = item.Key.ToPositiveIntegerArray();
                Assert.AreEqual(item.Value, result);
            }
        }

        /// <summary>
        /// Tests the pasing to an float array
        /// </summary>
        [Test]
        public void TestToFloatArray()
        {
            Dictionary<string, float[]> data = new Dictionary<string, float[]>();
            data.Add("12.3-4.56,78-9.0", new float[] { 12.3F, 4.56F, 78, 9 });
            data.Add("87390.174,7.41897498,748.16,748297,5.7355", new float[] { 87390.174F, 7.41897498F, 748.16F, 748297, 5.7355F });
            data.Add("1-2-3", new float[] { 1, 2, 3 });

            foreach (var item in data)
            {
                var result = item.Key.ToPositiveFloatArray();
                Assert.AreEqual(item.Value, result);
            }
        }

        /// <summary>
        /// Tests if the value is a valid URI
        /// </summary>
        /// <param name="input">The value to test</param>
        /// <param name="expected">Whether the value is correct</param>
        /// <remarks>
        /// The full RFC3986 does not seem to pass the test with the square brackets
        /// </remarks>
        [Test]
        [TestCase("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789", true)]
        [TestCase("-", true)]
        [TestCase(".", true)]
        [TestCase("_", true)]
        [TestCase("~", true)]
        [TestCase(":", true)]
        [TestCase("/", true)]
        [TestCase("?", true)]
        [TestCase("#", true)]
        [TestCase("[", false)]
        [TestCase("]", false)]
        [TestCase("@", true)]
        [TestCase("!", true)]
        [TestCase("$", true)]
        [TestCase("&", true)]
        [TestCase("'", true)]
        [TestCase("(", true)]
        [TestCase(")", true)]
        [TestCase("*", true)]
        [TestCase("+", true)]
        [TestCase(",", true)]
        [TestCase(";", true)]
        [TestCase("=", true)]
        [TestCase("lorem ipsum", false)]
        [TestCase("é", false)]
        public void TestIsValidUri(string input, bool expected)
        {
            var result = input.IsValidVirtualPathName();
            Assert.AreEqual(expected, result);
        }
    }
}