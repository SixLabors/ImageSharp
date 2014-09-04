// --------------------------------------------------------------------------------------------------------------------
// <copyright file="DoubleExtensionsUnitTests.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Runs unit tests on the <see cref="DoubleExtensions" /> extension methods
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.UnitTests.Extensions
{
    using Common.Extensions;
    using NUnit.Framework;

    /// <summary>
    /// Test harness for the DoubleExtensions extension methods
    /// </summary>
    [TestFixture]
    public class DoubleExtensionsUnitTests
    {
        /// <summary>
        /// Tests the double to byte conversion
        /// </summary>
        /// <param name="input">Double input</param>
        /// <param name="expected">Expected result</param>
        [Test]
        [TestCase(-10, 0x0)]
        [TestCase(1.5, 0x1)]
        [TestCase(25.7, 0x19)]
        [TestCase(1289047, 0xFF)]
        public void TestDoubleToByte(double input, byte expected)
        {
            byte result = input.ToByte();
            Assert.AreEqual(expected, result);
        }
    }
}