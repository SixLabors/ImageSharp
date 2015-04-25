// --------------------------------------------------------------------------------------------------------------------
// <copyright file="RectangleTests.cs" company="James South">
//   Copyright © James South and contributors.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Tests the <see cref="Rectangle" /> struct.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Tests
{
    using Xunit;

    /// <summary>
    /// Tests the <see cref="Rectangle"/> struct.
    /// </summary>
    public class RectangleTests
    {
        /// <summary>
        /// Tests the equality operators for equality.
        /// </summary>
        [Fact]
        public void AreEqual()
        {
            Rectangle first = new Rectangle(1, 1, 100, 100);
            Rectangle second = new Rectangle(1, 1, 100, 100);

            Assert.Equal(first, second);
        }

        /// <summary>
        /// Tests the equality operators for inequality.
        /// </summary>
        [Fact]
        public void AreNotEqual()
        {
            Rectangle first = new Rectangle(1, 1, 0, 100);
            Rectangle second = new Rectangle(1, 1, 100, 100);

            Assert.NotEqual(first, second);
        }

        /// <summary>
        /// Tests whether the rectangle constructors correctly assign properties.
        /// </summary>
        [Fact]
        public void ConstructorAssignsProperties()
        {
            Rectangle first = new Rectangle(1, 1, 50, 100);
            Assert.Equal(1, first.X);
            Assert.Equal(1, first.Y);
            Assert.Equal(50, first.Width);
            Assert.Equal(100, first.Height);
            Assert.Equal(1, first.Top);
            Assert.Equal(51, first.Right);
            Assert.Equal(101, first.Bottom);
            Assert.Equal(1, first.Left);

            Rectangle second = new Rectangle(new Point(1, 1), new Size(50, 100));
            Assert.Equal(1, second.X);
            Assert.Equal(1, second.Y);
            Assert.Equal(50, second.Width);
            Assert.Equal(100, second.Height);
            Assert.Equal(1, second.Top);
            Assert.Equal(51, second.Right);
            Assert.Equal(101, second.Bottom);
            Assert.Equal(1, second.Left);
        }
    }
}
