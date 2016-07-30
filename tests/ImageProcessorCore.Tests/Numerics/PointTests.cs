// <copyright file="PointTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Tests
{
    using Xunit;

    /// <summary>
    /// Tests the <see cref="Point"/> struct.
    /// </summary>
    public class PointTests
    {
        /// <summary>
        /// Tests the equality operators for equality.
        /// </summary>
        [Fact]
        public void AreEqual()
        {
            Point first = new Point(100, 100);
            Point second = new Point(100, 100);

            Assert.Equal(first, second);
        }

        /// <summary>
        /// Tests the equality operators for inequality.
        /// </summary>
        [Fact]
        public void AreNotEqual()
        {
            Point first = new Point(0, 100);
            Point second = new Point(100, 100);

            Assert.NotEqual(first, second);
        }

        /// <summary>
        /// Tests whether the point constructor correctly assign properties.
        /// </summary>
        [Fact]
        public void ConstructorAssignsProperties()
        {
            Point first = new Point(4, 5);
            Assert.Equal(4, first.X);
            Assert.Equal(5, first.Y);
        }
    }
}