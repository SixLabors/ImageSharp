// --------------------------------------------------------------------------------------------------------------------
// <copyright file="StringExtensionsUnitTests.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Test harness for the string extensions
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Web.UnitTests.Extensions
{
    using System;
    using System.Collections.Generic;

    using ImageProcessor.Web.Extensions;
    using NUnit.Framework;

    /// <summary>
    /// Test harness for the string extensions
    /// </summary>
    [TestFixture]
    public class StringExtensionsUnitTests
    {
        /// <summary>
        /// Tests the passing to an integer array
        /// </summary>
        [Test]
        public void TestToIntegerArray()
        {
            Dictionary<string, int[]> data = new Dictionary<string, int[]>
            {
                {
                    "123-456,78-90",
                    new[] { 123, 456, 78, 90 }
                },
                {
                    "87390174,741897498,74816,748297,57355",
                    new[]
                        {
                            87390174, 741897498, 74816,
                            748297, 57355
                        }
                },
                { "1-2-3", new[] { 1, 2, 3 } }
            };

            foreach (KeyValuePair<string, int[]> item in data)
            {
                int[] result = item.Key.ToPositiveIntegerArray();
                Assert.AreEqual(item.Value, result);
            }
        }

        /// <summary>
        /// Tests the passing to an float array
        /// </summary>
        [Test]
        public void TestToFloatArray()
        {
            Dictionary<string, float[]> data = new Dictionary<string, float[]>
            {
                {
                    "12.3-4.56,78-9.0",
                    new[] { 12.3F, 4.56F, 78, 9 }
                },
                {
                    "87390.174,7.41897498,748.16,748297,5.7355",
                    new[]
                        {
                            87390.174F, 7.41897498F,
                            748.16F, 748297, 5.7355F
                        }
                },
                { "1-2-3", new float[] { 1, 2, 3 } }
            };

            foreach (KeyValuePair<string, float[]> item in data)
            {
                float[] result = item.Key.ToPositiveFloatArray();
                Assert.AreEqual(item.Value, result);
            }
        }

        /// <summary>
        /// Tests the MD5 fingerprint
        /// </summary>
        /// <param name="input">The input value</param>
        /// <param name="expected">The expected output of the hash</param>
        [Test]
        [TestCase("test input", "2e7f7a62eabf0993239ca17c78c464d9")]
        [TestCase("lorem ipsum dolor", "96ee002fee25e8b675a477c9750fa360")]
        [TestCase("LoReM IpSuM DoLoR", "41e201da794c7fbdb8ce5526a71c8c83")]
        [TestCase("1234567890", "e15e31c3d8898c92ab172a4311be9e84")]
        public void TestToMd5Fingerprint(string input, string expected)
        {
            string result = input.ToMD5Fingerprint();
            bool comparison = result.Equals(expected, StringComparison.InvariantCultureIgnoreCase);
            Assert.True(comparison);
        }

        /// <summary>
        /// Tests the SHA-1 fingerprint
        /// </summary>
        /// <param name="input">The input value</param>
        /// <param name="expected">The expected output of the hash</param>
        [Test]
        [TestCase("test input", "49883b34e5a0f48224dd6230f471e9dc1bdbeaf5")]
        [TestCase("lorem ipsum dolor", "75899ad8827a32493928903aecd6e931bf36f967")]
        [TestCase("LoReM IpSuM DoLoR", "2f44519afae72fc0837b72c6b53cb11338a1f916")]
        [TestCase("1234567890", "01b307acba4f54f55aafc33bb06bbbf6ca803e9a")]
        public void TestToSHA1Fingerprint(string input, string expected)
        {
            string result = input.ToSHA1Fingerprint();
            bool comparison = result.Equals(expected, StringComparison.InvariantCultureIgnoreCase);
            Assert.True(comparison);
        }

        /// <summary>
        /// Tests if the value is a valid URI path name. I.E the path part of a uri.
        /// </summary>
        /// <param name="input">The value to test</param>
        /// <param name="expected">Whether the value is correct</param>
        /// <remarks>
        /// The full RFC3986 does not seem to pass the test with the square brackets
        /// ':' is failing for some reason in VS but not elsewhere. Could be a build issue.
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
        [TestCase("#", false)]
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
        public void TestIsValidUriPathName(string input, bool expected)
        {
            bool result = input.IsValidVirtualPathName();
            Assert.AreEqual(expected, result);
        }
    }
}
