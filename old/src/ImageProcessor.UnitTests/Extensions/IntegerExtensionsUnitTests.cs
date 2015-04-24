// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IntegerExtensionsUnitTests.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Runs unit tests on the <see cref="IntegerExtensions" /> extension methods
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.UnitTests.Extensions
{
    using Common.Extensions;
    using NUnit.Framework;

    /// <summary>
    /// Provides a test harness for the integer extension class
    /// </summary>
    [TestFixture]
    public class IntegerExtensionsUnitTests
    {
        /// <summary>
        /// Tests the "ToByte" extension
        /// </summary>
        /// <param name="input">Integer input</param>
        /// <param name="expected">Expected result</param>
        [Test]
        [TestCase(21, 0x15)]
        [TestCase(190, 0xBE)]
        [TestCase(3156, 0xFF)]
        public void IntegerIsConvertedToByte(int input, byte expected)
        {
            byte result = input.ToByte();
            Assert.AreEqual(expected, result);
        }
    }
}