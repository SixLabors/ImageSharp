// --------------------------------------------------------------------------------------------------------------------
// <copyright file="DoubleExtensionsUnitTests.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Runs unit tests on the <see cref="DoubleExtensions" /> extension methods
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.UnitTests
{
    using System;
    using System.Collections.Generic;
    using Common.Extensions;
    using NUnit.Framework;

    /// <summary>
    /// Test harness for the DoubleExtensions extension methods
    /// </summary>
    [TestFixture]
    public class DoubleExtensionsUnitTests
    {
        /// <summary>
        /// Stores the values to test for the ToByte() extension method
        /// </summary>
        private Dictionary<double, byte> doubleToByteTests;

        /// <summary>
        /// Sets up the values for the tests
        /// </summary>
        [TestFixtureSetUp]
        public void Init()
        {
            this.doubleToByteTests = new Dictionary<double, byte>();
            this.doubleToByteTests.Add(-10, 0x0);
            this.doubleToByteTests.Add(1.5, 0x1);
            this.doubleToByteTests.Add(25.7, 0x19);
            this.doubleToByteTests.Add(1289047, 0xFF);
        }

        /// <summary>
        /// Tests the double to byte conversion
        /// </summary>
        [Test]
        public void TestDoubleToByte()
        {
            foreach (var item in this.doubleToByteTests)
            {
                var result = item.Key.ToByte();
                Assert.AreEqual(item.Value, result);
            }
        }
    }
}