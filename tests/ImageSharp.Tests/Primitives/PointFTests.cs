// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Tests;

public class PointFTests
{
    private static readonly ApproximateFloatComparer ApproximateFloatComparer =
        new ApproximateFloatComparer(1e-6f);

    [Fact]
    public void CanReinterpretCastFromVector2()
    {
        Vector2 vector = new Vector2(1, 2);

        PointF point = Unsafe.As<Vector2, PointF>(ref vector);

        Assert.Equal(vector.X, point.X);
        Assert.Equal(vector.Y, point.Y);
    }

    [Fact]
    public void DefaultConstructorTest()
    {
        Assert.Equal(default, PointF.Empty);
    }

    [Theory]
    [InlineData(float.MaxValue, float.MinValue)]
    [InlineData(float.MinValue, float.MinValue)]
    [InlineData(float.MaxValue, float.MaxValue)]
    [InlineData(float.MinValue, float.MaxValue)]
    [InlineData(0.0, 0.0)]
    public void NonDefaultConstructorTest(float x, float y)
    {
        PointF p1 = new PointF(x, y);

        Assert.Equal(x, p1.X);
        Assert.Equal(y, p1.Y);
    }

    [Fact]
    public void IsEmptyDefaultsTest()
    {
        Assert.True(PointF.Empty.IsEmpty);
        Assert.True(default(PointF).IsEmpty);
        Assert.True(new PointF(0, 0).IsEmpty);
    }

    [Theory]
    [InlineData(float.MaxValue, float.MinValue)]
    [InlineData(float.MinValue, float.MinValue)]
    [InlineData(float.MaxValue, float.MaxValue)]
    public void IsEmptyRandomTest(float x, float y)
    {
        Assert.False(new PointF(x, y).IsEmpty);
    }

    [Theory]
    [InlineData(float.MaxValue, float.MinValue)]
    [InlineData(float.MinValue, float.MinValue)]
    [InlineData(float.MaxValue, float.MaxValue)]
    [InlineData(0, 0)]
    public void CoordinatesTest(float x, float y)
    {
        PointF p = new PointF(x, y);
        Assert.Equal(x, p.X);
        Assert.Equal(y, p.Y);

        p.X = 10;
        Assert.Equal(10, p.X);

        p.Y = -10.123f;
        Assert.Equal(-10.123, p.Y, 3);
    }

    [Theory]
    [InlineData(float.MaxValue, float.MinValue, int.MaxValue, int.MinValue)]
    [InlineData(float.MinValue, float.MaxValue, int.MinValue, int.MaxValue)]
    [InlineData(0, 0, 0, 0)]
    public void ArithmeticTestWithSize(float x, float y, int x1, int y1)
    {
        PointF p = new PointF(x, y);
        Size s = new Size(x1, y1);

        PointF addExpected = new PointF(x + x1, y + y1);
        PointF subExpected = new PointF(x - x1, y - y1);
        Assert.Equal(addExpected, p + s);
        Assert.Equal(subExpected, p - s);
        Assert.Equal(addExpected, PointF.Add(p, s));
        Assert.Equal(subExpected, PointF.Subtract(p, s));
    }

    [Theory]
    [InlineData(float.MaxValue, float.MinValue)]
    [InlineData(float.MinValue, float.MaxValue)]
    [InlineData(0, 0)]
    public void ArithmeticTestWithSizeF(float x, float y)
    {
        PointF p = new PointF(x, y);
        SizeF s = new SizeF(y, x);

        PointF addExpected = new PointF(x + y, y + x);
        PointF subExpected = new PointF(x - y, y - x);
        Assert.Equal(addExpected, p + s);
        Assert.Equal(subExpected, p - s);
        Assert.Equal(addExpected, PointF.Add(p, s));
        Assert.Equal(subExpected, PointF.Subtract(p, s));
    }

    [Fact]
    public void RotateTest()
    {
        PointF p = new PointF(13, 17);
        Matrix3x2 matrix = Matrix3x2Extensions.CreateRotationDegrees(45, PointF.Empty);

        PointF pout = PointF.Transform(p, matrix);

        Assert.Equal(-2.82842732F, pout.X, ApproximateFloatComparer);
        Assert.Equal(21.2132034F, pout.Y, ApproximateFloatComparer);
    }

    [Fact]
    public void SkewTest()
    {
        PointF p = new PointF(13, 17);
        Matrix3x2 matrix = Matrix3x2Extensions.CreateSkewDegrees(45, 45, PointF.Empty);

        PointF pout = PointF.Transform(p, matrix);
        Assert.Equal(new PointF(30, 30), pout);
    }

    [Theory]
    [InlineData(float.MaxValue, float.MinValue)]
    [InlineData(float.MinValue, float.MaxValue)]
    [InlineData(float.MinValue, float.MinValue)]
    [InlineData(float.MaxValue, float.MaxValue)]
    [InlineData(0, 0)]
    public void EqualityTest(float x, float y)
    {
        PointF pLeft = new PointF(x, y);
        PointF pRight = new PointF(y, x);

        if (x == y)
        {
            Assert.True(pLeft == pRight);
            Assert.False(pLeft != pRight);
            Assert.True(pLeft.Equals(pRight));
            Assert.True(pLeft.Equals((object)pRight));
            Assert.Equal(pLeft.GetHashCode(), pRight.GetHashCode());
            return;
        }

        Assert.True(pLeft != pRight);
        Assert.False(pLeft == pRight);
        Assert.False(pLeft.Equals(pRight));
        Assert.False(pLeft.Equals((object)pRight));
    }

    [Fact]
    public void EqualityTest_NotPointF()
    {
        PointF point = new PointF(0, 0);
        Assert.False(point.Equals(null));
        Assert.False(point.Equals(0));

        // If PointF implements IEquatable<PointF> (e.g. in .NET Core), then structs that are implicitly
        // convertible to var can potentially be equal.
        // See https://github.com/dotnet/corefx/issues/5255.
        bool expectsImplicitCastToPointF = typeof(IEquatable<PointF>).IsAssignableFrom(point.GetType());
        Assert.Equal(expectsImplicitCastToPointF, point.Equals(new Point(0, 0)));

        Assert.False(point.Equals((object)new Point(0, 0))); // No implicit cast
    }

    [Fact]
    public void GetHashCodeTest()
    {
        PointF point = new PointF(10, 10);
        Assert.Equal(point.GetHashCode(), new PointF(10, 10).GetHashCode());
        Assert.NotEqual(point.GetHashCode(), new PointF(20, 10).GetHashCode());
        Assert.NotEqual(point.GetHashCode(), new PointF(10, 20).GetHashCode());
    }

    [Fact]
    public void ToStringTest()
    {
        PointF p = new PointF(5.1F, -5.123F);
        Assert.Equal(string.Format(CultureInfo.CurrentCulture, "PointF [ X={0}, Y={1} ]", p.X, p.Y), p.ToString());
    }

    [Theory]
    [InlineData(float.MaxValue, float.MinValue)]
    [InlineData(float.MinValue, float.MinValue)]
    [InlineData(float.MaxValue, float.MaxValue)]
    [InlineData(0, 0)]
    public void DeconstructTest(float x, float y)
    {
        PointF p = new PointF(x, y);

        (float deconstructedX, float deconstructedY) = p;

        Assert.Equal(x, deconstructedX);
        Assert.Equal(y, deconstructedY);
    }
}
