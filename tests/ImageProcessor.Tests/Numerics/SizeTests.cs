// --------------------------------------------------------------------------------------------------------------------
// <copyright file="SizeTests.cs" company="James South">
//   Copyright © James South and contributors.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Tests the <see cref="Size" /> struct.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Tests
{
    using Xunit;

    /// <summary>
    /// Tests the <see cref="Size"/> struct.
    /// </summary>
    public class SizeTests
    {
        /// <summary>
        /// Tests the equality operators for equality.
        /// </summary>
        [Fact]
        public void AreEqual()
        {
            Size first = new Size(100, 100);
            Size second = new Size(100, 100);

            Assert.Equal(first, second);
        }

        /// <summary>
        /// Tests the equality operators for inequality.
        /// </summary>
        [Fact]
        public void AreNotEqual()
        {
            Size first = new Size(0, 100);
            Size second = new Size(100, 100);

            Assert.NotEqual(first, second);
        }

        /// <summary>
        /// Tests whether the size constructor correctly assign properties.
        /// </summary>
        [Fact]
        public void ConstructorAssignsProperties()
        {
            Size first = new Size(4, 5);
            Assert.Equal(4, first.Width);
            Assert.Equal(5, first.Height);
        }
    }
}
