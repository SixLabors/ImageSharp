// <copyright file="SizeTests.cs" company="Six Labors">
// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace SixLabors.Primitives.Tests
{
    using System.Globalization;
    using Xunit;

    /// <summary>
    /// Tests the <see cref="Size"/> struct.
    /// </summary>
    public class SizeTests
    {
        [Fact]
        public void DefaultConstructorTest()
        {
            Assert.Equal(Size.Empty, new Size());
        }

        [Theory]
        [InlineData(int.MaxValue, int.MinValue)]
        [InlineData(int.MinValue, int.MinValue)]
        [InlineData(int.MaxValue, int.MaxValue)]
        [InlineData(0, 0)]
        public void NonDefaultConstructorTest(int width, int height)
        {
            var s1 = new Size(width, height);
            var s2 = new Size(new Point(width, height));

            Assert.Equal(s1, s2);

            s1.Width = 10;
            Assert.Equal(10, s1.Width);

            s1.Height = -10;
            Assert.Equal(-10, s1.Height);
        }

        [Fact]
        public void IsEmptyDefaultsTest()
        {
            Assert.True(Size.Empty.IsEmpty);
            Assert.True(new Size().IsEmpty);
            Assert.True(new Size(0, 0).IsEmpty);
        }

        [Theory]
        [InlineData(int.MaxValue, int.MinValue)]
        [InlineData(int.MinValue, int.MinValue)]
        [InlineData(int.MaxValue, int.MaxValue)]
        public void IsEmptyRandomTest(int width, int height)
        {
            Assert.False(new Size(width, height).IsEmpty);
        }

        [Theory]
        [InlineData(int.MaxValue, int.MinValue)]
        [InlineData(int.MinValue, int.MinValue)]
        [InlineData(int.MaxValue, int.MaxValue)]
        [InlineData(0, 0)]
        public void DimensionsTest(int width, int height)
        {
            var p = new Size(width, height);
            Assert.Equal(width, p.Width);
            Assert.Equal(height, p.Height);
        }

        [Theory]
        [InlineData(int.MaxValue, int.MinValue)]
        [InlineData(int.MinValue, int.MinValue)]
        [InlineData(int.MaxValue, int.MaxValue)]
        [InlineData(0, 0)]
        public void PointFConversionTest(int width, int height)
        {
            SizeF sz = new Size(width, height);
            Assert.Equal(new SizeF(width, height), sz);
        }

        [Theory]
        [InlineData(int.MaxValue, int.MinValue)]
        [InlineData(int.MinValue, int.MinValue)]
        [InlineData(int.MaxValue, int.MaxValue)]
        [InlineData(0, 0)]
        public void SizeConversionTest(int width, int height)
        {
            var sz = (Point)new Size(width, height);
            Assert.Equal(new Point(width, height), sz);
        }

        [Theory]
        [InlineData(int.MaxValue, int.MinValue)]
        [InlineData(int.MinValue, int.MinValue)]
        [InlineData(int.MaxValue, int.MaxValue)]
        [InlineData(0, 0)]
        public void ArithmeticTest(int width, int height)
        {
            var sz1 = new Size(width, height);
            var sz2 = new Size(height, width);
            Size addExpected, subExpected;

            unchecked
            {
                addExpected = new Size(width + height, height + width);
                subExpected = new Size(width - height, height - width);
            }

            Assert.Equal(addExpected, sz1 + sz2);
            Assert.Equal(subExpected, sz1 - sz2);
            Assert.Equal(addExpected, Size.Add(sz1, sz2));
            Assert.Equal(subExpected, Size.Subtract(sz1, sz2));
        }

        [Theory]
        [InlineData(float.MaxValue, float.MinValue)]
        [InlineData(float.MinValue, float.MinValue)]
        [InlineData(float.MaxValue, float.MaxValue)]
        [InlineData(0, 0)]
        public void PointFMathematicalTest(float width, float height)
        {
            var szF = new SizeF(width, height);
            Size pCeiling, pTruncate, pRound;

            unchecked
            {
                pCeiling = new Size((int)MathF.Ceiling(width), (int)MathF.Ceiling(height));
                pTruncate = new Size((int)width, (int)height);
                pRound = new Size((int)MathF.Round(width), (int)MathF.Round(height));
            }

            Assert.Equal(pCeiling, Size.Ceiling(szF));
            Assert.Equal(pRound, Size.Round(szF));
            Assert.Equal(pTruncate, (Size)szF);
        }

        [Theory]
        [InlineData(int.MaxValue, int.MinValue)]
        [InlineData(int.MinValue, int.MinValue)]
        [InlineData(int.MaxValue, int.MaxValue)]
        [InlineData(0, 0)]
        public void EqualityTest(int width, int height)
        {
            var p1 = new Size(width, height);
            var p2 = new Size(unchecked(width - 1), unchecked(height - 1));
            var p3 = new Size(width, height);

            Assert.True(p1 == p3);
            Assert.True(p1 != p2);
            Assert.True(p2 != p3);

            Assert.True(p1.Equals(p3));
            Assert.False(p1.Equals(p2));
            Assert.False(p2.Equals(p3));

            Assert.True(p1.Equals((object)p3));
            Assert.False(p1.Equals((object)p2));
            Assert.False(p2.Equals((object)p3));

            Assert.Equal(p1.GetHashCode(), p3.GetHashCode());
        }

        [Fact]
        public static void EqualityTest_NotSize()
        {
            var size = new Size(0, 0);
            Assert.False(size.Equals(null));
            Assert.False(size.Equals(0));
            Assert.False(size.Equals(new SizeF(0, 0)));
        }

        [Fact]
        public static void GetHashCodeTest()
        {
            var size = new Size(10, 10);
            Assert.Equal(size.GetHashCode(), new Size(10, 10).GetHashCode());
            Assert.NotEqual(size.GetHashCode(), new Size(20, 10).GetHashCode());
            Assert.NotEqual(size.GetHashCode(), new Size(10, 20).GetHashCode());
        }

        [Fact]
        public void ToStringTest()
        {
            var sz = new Size(10, 5);
            Assert.Equal(string.Format(CultureInfo.CurrentCulture, "Size [ Width={0}, Height={1} ]", sz.Width, sz.Height), sz.ToString());
        }

        [Fact]
        public void ToStringTestEmpty()
        {
            var sz = new Size(0, 0);
            Assert.Equal("Size [ Empty ]", sz.ToString());
        }
    }
}