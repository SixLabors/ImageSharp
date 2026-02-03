// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Globalization;
using System.Numerics;

namespace SixLabors.ImageSharp.Tests;

public class PointTests
{
    [Fact]
    public void DefaultConstructorTest()
    {
        Assert.Equal(default, Point.Empty);
    }

    [Theory]
    [InlineData(int.MaxValue, int.MinValue)]
    [InlineData(int.MinValue, int.MinValue)]
    [InlineData(int.MaxValue, int.MaxValue)]
    [InlineData(0, 0)]
    public void NonDefaultConstructorTest(int x, int y)
    {
        Point p1 = new(x, y);
        Point p2 = new(new Size(x, y));

        Assert.Equal(p1, p2);
    }

    [Theory]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    [InlineData(0)]
    public void SingleIntConstructorTest(int x)
    {
        Point p1 = new(x);
        Point p2 = new(unchecked((short)(x & 0xFFFF)), unchecked((short)((x >> 16) & 0xFFFF)));

        Assert.Equal(p1, p2);
    }

    [Fact]
    public void IsEmptyDefaultsTest()
    {
        Assert.True(Point.Empty.IsEmpty);
        Assert.True(default(Point).IsEmpty);
        Assert.True(new Point(0, 0).IsEmpty);
    }

    [Theory]
    [InlineData(int.MaxValue, int.MinValue)]
    [InlineData(int.MinValue, int.MinValue)]
    [InlineData(int.MaxValue, int.MaxValue)]
    public void IsEmptyRandomTest(int x, int y)
    {
        Assert.False(new Point(x, y).IsEmpty);
    }

    [Theory]
    [InlineData(int.MaxValue, int.MinValue)]
    [InlineData(int.MinValue, int.MinValue)]
    [InlineData(int.MaxValue, int.MaxValue)]
    [InlineData(0, 0)]
    public void CoordinatesTest(int x, int y)
    {
        Point p = new(x, y);
        Assert.Equal(x, p.X);
        Assert.Equal(y, p.Y);
    }

    [Theory]
    [InlineData(int.MaxValue, int.MinValue)]
    [InlineData(int.MinValue, int.MinValue)]
    [InlineData(int.MaxValue, int.MaxValue)]
    [InlineData(0, 0)]
    public void PointFConversionTest(int x, int y)
    {
        PointF p = new Point(x, y);
        Assert.Equal(new PointF(x, y), p);
    }

    [Theory]
    [InlineData(int.MaxValue, int.MinValue)]
    [InlineData(int.MinValue, int.MinValue)]
    [InlineData(int.MaxValue, int.MaxValue)]
    [InlineData(0, 0)]
    public void SizeConversionTest(int x, int y)
    {
        Size sz = (Size)new Point(x, y);
        Assert.Equal(new Size(x, y), sz);
    }

    [Theory]
    [InlineData(int.MaxValue, int.MinValue)]
    [InlineData(int.MinValue, int.MinValue)]
    [InlineData(int.MaxValue, int.MaxValue)]
    [InlineData(0, 0)]
    public void ArithmeticTest(int x, int y)
    {
        Point addExpected, subExpected, p = new(x, y);
        Size s = new(y, x);

        unchecked
        {
            addExpected = new Point(x + y, y + x);
            subExpected = new Point(x - y, y - x);
        }

        Assert.Equal(addExpected, p + s);
        Assert.Equal(subExpected, p - s);
        Assert.Equal(addExpected, Point.Add(p, s));
        Assert.Equal(subExpected, Point.Subtract(p, s));
    }

    [Theory]
    [InlineData(float.MaxValue, float.MinValue)]
    [InlineData(float.MinValue, float.MinValue)]
    [InlineData(float.MaxValue, float.MaxValue)]
    [InlineData(0, 0)]
    public void PointFMathematicalTest(float x, float y)
    {
        PointF pf = new(x, y);
        Point pCeiling, pTruncate, pRound;

        unchecked
        {
            pCeiling = new Point((int)MathF.Ceiling(x), (int)MathF.Ceiling(y));
            pTruncate = new Point((int)x, (int)y);
            pRound = new Point((int)MathF.Round(x), (int)MathF.Round(y));
        }

        Assert.Equal(pCeiling, Point.Ceiling(pf));
        Assert.Equal(pRound, Point.Round(pf));
        Assert.Equal(pTruncate, (Point)pf);
    }

    [Theory]
    [InlineData(int.MaxValue, int.MinValue)]
    [InlineData(int.MinValue, int.MinValue)]
    [InlineData(int.MaxValue, int.MaxValue)]
    [InlineData(0, 0)]
    public void OffsetTest(int x, int y)
    {
        Point p1 = new(x, y);
        Point p2 = new(y, x);

        p1.Offset(p2);

        Assert.Equal(unchecked(p2.X + p2.Y), p1.X);
        Assert.Equal(p1.X, p1.Y);

        p2.Offset(x, y);
        Assert.Equal(p1, p2);
    }

    [Fact]
    public void RotateTest()
    {
        Point p = new(13, 17);
        Matrix3x2 matrix = Matrix3x2Extensions.CreateRotationDegrees(45, Point.Empty);

        Point pout = Point.Transform(p, matrix);

        Assert.Equal(new Point(-3, 21), pout);
    }

    [Fact]
    public void SkewTest()
    {
        Point p = new(13, 17);
        Matrix3x2 matrix = Matrix3x2Extensions.CreateSkewDegrees(45, 45, Point.Empty);

        Point pout = Point.Transform(p, matrix);
        Assert.Equal(new Point(30, 30), pout);
    }

    [Theory]
    [InlineData(int.MaxValue, int.MinValue)]
    [InlineData(int.MinValue, int.MinValue)]
    [InlineData(int.MaxValue, int.MaxValue)]
    [InlineData(0, 0)]
    public void EqualityTest(int x, int y)
    {
        Point p1 = new(x, y);
        Point p2 = new((x / 2) - 1, (y / 2) - 1);
        Point p3 = new(x, y);

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
    public void EqualityTest_NotPoint()
    {
        Point point = new(0, 0);
        Assert.False(point.Equals(null));
        Assert.False(point.Equals(0));
        Assert.False(point.Equals(new PointF(0, 0)));
    }

    [Fact]
    public void GetHashCodeTest()
    {
        Point point = new(10, 10);
        Assert.Equal(point.GetHashCode(), new Point(10, 10).GetHashCode());
        Assert.NotEqual(point.GetHashCode(), new Point(20, 10).GetHashCode());
        Assert.NotEqual(point.GetHashCode(), new Point(10, 20).GetHashCode());
    }

    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(1, -2, 3, -4)]
    public void ConversionTest(int x, int y, int width, int height)
    {
        Rectangle rect = new(x, y, width, height);
        RectangleF rectF = rect;
        Assert.Equal(x, rectF.X);
        Assert.Equal(y, rectF.Y);
        Assert.Equal(width, rectF.Width);
        Assert.Equal(height, rectF.Height);
    }

    [Fact]
    public void ToStringTest()
    {
        Point p = new(5, -5);
        Assert.Equal(string.Format(CultureInfo.CurrentCulture, "Point [ X={0}, Y={1} ]", p.X, p.Y), p.ToString());
    }

    [Theory]
    [InlineData(int.MaxValue, int.MinValue)]
    [InlineData(int.MinValue, int.MinValue)]
    [InlineData(int.MaxValue, int.MaxValue)]
    [InlineData(0, 0)]
    public void DeconstructTest(int x, int y)
    {
        Point p = new(x, y);

        (int deconstructedX, int deconstructedY) = p;

        Assert.Equal(x, deconstructedX);
        Assert.Equal(y, deconstructedY);
    }
}
