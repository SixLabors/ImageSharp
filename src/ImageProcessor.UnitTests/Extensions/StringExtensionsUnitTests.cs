// --------------------------------------------------------------------------------------------------------------------
// <copyright file="StringExtensionsUnitTests.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Provides a test harness for the string extensions
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.UnitTests.Extensions
{
    using System.Collections.Generic;
    using ImageProcessor.Common.Extensions;
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
    }
}