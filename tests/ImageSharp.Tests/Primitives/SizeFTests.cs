// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Globalization;

namespace SixLabors.ImageSharp.Tests;

public class SizeFTests
{
    [Fact]
    public void DefaultConstructorTest()
    {
        Assert.Equal(default, SizeF.Empty);
    }

    [Theory]
    [InlineData(float.MaxValue, float.MinValue)]
    [InlineData(float.MinValue, float.MinValue)]
    [InlineData(float.MaxValue, float.MaxValue)]
    [InlineData(0, 0)]
    public void NonDefaultConstructorAndDimensionsTest(float width, float height)
    {
        SizeF s1 = new(width, height);
        PointF p1 = new(width, height);
        SizeF s2 = new(s1);

        Assert.Equal(s1, s2);
        Assert.Equal(s1, new(p1));
        Assert.Equal(s2, new(p1));

        Assert.Equal(width, s1.Width);
        Assert.Equal(height, s1.Height);

        s1.Width = 10;
        Assert.Equal(10, s1.Width);

        s1.Height = -10.123f;
        Assert.Equal(-10.123, s1.Height, 3);
    }

    [Fact]
    public void IsEmptyDefaultsTest()
    {
        Assert.True(SizeF.Empty.IsEmpty);
        Assert.True(default(SizeF).IsEmpty);
        Assert.True(new SizeF(0, 0).IsEmpty);
    }

    [Theory]
    [InlineData(float.MaxValue, float.MinValue)]
    [InlineData(float.MinValue, float.MinValue)]
    [InlineData(float.MaxValue, float.MaxValue)]
    public void IsEmptyRandomTest(float width, float height)
    {
        Assert.False(new SizeF(width, height).IsEmpty);
    }

    [Theory]
    [InlineData(float.MaxValue, float.MinValue)]
    [InlineData(float.MinValue, float.MinValue)]
    [InlineData(float.MaxValue, float.MaxValue)]
    [InlineData(0, 0)]
    public void ArithmeticTest(float width, float height)
    {
        SizeF s1 = new(width, height);
        SizeF s2 = new(height, width);
        SizeF addExpected = new(width + height, width + height);
        SizeF subExpected = new(width - height, height - width);

        Assert.Equal(addExpected, s1 + s2);
        Assert.Equal(addExpected, SizeF.Add(s1, s2));

        Assert.Equal(subExpected, s1 - s2);
        Assert.Equal(subExpected, SizeF.Subtract(s1, s2));
    }

    [Theory]
    [InlineData(float.MaxValue, float.MinValue)]
    [InlineData(float.MinValue, float.MinValue)]
    [InlineData(float.MaxValue, float.MaxValue)]
    [InlineData(0, 0)]
    public void EqualityTest(float width, float height)
    {
        SizeF sLeft = new(width, height);
        SizeF sRight = new(height, width);

        if (width == height)
        {
            Assert.True(sLeft == sRight);
            Assert.False(sLeft != sRight);
            Assert.True(sLeft.Equals(sRight));
            Assert.True(sLeft.Equals((object)sRight));
            Assert.Equal(sLeft.GetHashCode(), sRight.GetHashCode());
            return;
        }

        Assert.True(sLeft != sRight);
        Assert.False(sLeft == sRight);
        Assert.False(sLeft.Equals(sRight));
        Assert.False(sLeft.Equals((object)sRight));
    }

    [Fact]
    public void EqualityTest_NotSizeF()
    {
        SizeF size = new(0, 0);
        Assert.False(size.Equals(null));
        Assert.False(size.Equals(0));

        // If SizeF implements IEquatable<SizeF> (e.g in .NET Core), then classes that are implicitly
        // convertible to SizeF can potentially be equal.
        // See https://github.com/dotnet/corefx/issues/5255.
        bool expectsImplicitCastToSizeF = typeof(IEquatable<SizeF>).IsAssignableFrom(size.GetType());
        Assert.Equal(expectsImplicitCastToSizeF, size.Equals(new Size(0, 0)));

        Assert.False(size.Equals((object)new Size(0, 0))); // No implicit cast
    }

    [Fact]
    public void GetHashCodeTest()
    {
        SizeF size = new(10, 10);
        Assert.Equal(size.GetHashCode(), new SizeF(10, 10).GetHashCode());
        Assert.NotEqual(size.GetHashCode(), new SizeF(20, 10).GetHashCode());
        Assert.NotEqual(size.GetHashCode(), new SizeF(10, 20).GetHashCode());
    }

    [Theory]
    [InlineData(float.MaxValue, float.MinValue)]
    [InlineData(float.MinValue, float.MinValue)]
    [InlineData(float.MaxValue, float.MaxValue)]
    [InlineData(0, 0)]
    public void ConversionTest(float width, float height)
    {
        SizeF s1 = new(width, height);
        PointF p1 = (PointF)s1;
        Size s2 = new(unchecked((int)width), unchecked((int)height));

        Assert.Equal(new(width, height), p1);
        Assert.Equal(p1, (PointF)s1);
        Assert.Equal(s2, (Size)s1);
    }

    [Fact]
    public void ToStringTest()
    {
        SizeF sz = new(10, 5);
        Assert.Equal(string.Format(CultureInfo.CurrentCulture, "SizeF [ Width={0}, Height={1} ]", sz.Width, sz.Height), sz.ToString());
    }

    [Theory]
    [InlineData(1000.234f, 0.0f)]
    [InlineData(1000.234f, 1.0f)]
    [InlineData(1000.234f, 2400.933f)]
    [InlineData(1000.234f, float.MaxValue)]
    [InlineData(1000.234f, -1.0f)]
    [InlineData(1000.234f, -2400.933f)]
    [InlineData(1000.234f, float.MinValue)]
    [InlineData(float.MaxValue, 0.0f)]
    [InlineData(float.MaxValue, 1.0f)]
    [InlineData(float.MaxValue, 2400.933f)]
    [InlineData(float.MaxValue, float.MaxValue)]
    [InlineData(float.MaxValue, -1.0f)]
    [InlineData(float.MaxValue, -2400.933f)]
    [InlineData(float.MaxValue, float.MinValue)]
    [InlineData(float.MinValue, 0.0f)]
    [InlineData(float.MinValue, 1.0f)]
    [InlineData(float.MinValue, 2400.933f)]
    [InlineData(float.MinValue, float.MaxValue)]
    [InlineData(float.MinValue, -1.0f)]
    [InlineData(float.MinValue, -2400.933f)]
    [InlineData(float.MinValue, float.MinValue)]
    public void MultiplicationTest(float dimension, float multiplier)
    {
        SizeF sz1 = new(dimension, dimension);
        SizeF mulExpected;

        mulExpected = new(dimension * multiplier, dimension * multiplier);

        Assert.Equal(mulExpected, sz1 * multiplier);
        Assert.Equal(mulExpected, multiplier * sz1);
    }

    [Theory]
    [InlineData(1111.1111f, 2222.2222f, 3333.3333f)]
    public void MultiplicationTestWidthHeightMultiplier(float width, float height, float multiplier)
    {
        SizeF sz1 = new(width, height);
        SizeF mulExpected;

        mulExpected = new(width * multiplier, height * multiplier);

        Assert.Equal(mulExpected, sz1 * multiplier);
        Assert.Equal(mulExpected, multiplier * sz1);
    }

    [Theory]
    [InlineData(0.0f, 1.0f)]
    [InlineData(1.0f, 1.0f)]
    [InlineData(-1.0f, 1.0f)]
    [InlineData(1.0f, -1.0f)]
    [InlineData(-1.0f, -1.0f)]
    [InlineData(float.MaxValue, float.MaxValue)]
    [InlineData(float.MaxValue, float.MinValue)]
    [InlineData(float.MinValue, float.MaxValue)]
    [InlineData(float.MinValue, float.MinValue)]
    [InlineData(float.MaxValue, 1.0f)]
    [InlineData(float.MinValue, 1.0f)]
    [InlineData(float.MaxValue, -1.0f)]
    [InlineData(float.MinValue, -1.0f)]
    [InlineData(float.MinValue, 0.0f)]
    [InlineData(1.0f, float.MinValue)]
    [InlineData(1.0f, float.MaxValue)]
    [InlineData(-1.0f, float.MinValue)]
    [InlineData(-1.0f, float.MaxValue)]
    public void DivideTestSizeFloat(float dimension, float divisor)
    {
        SizeF size = new(dimension, dimension);
        SizeF expected = new(dimension / divisor, dimension / divisor);
        Assert.Equal(expected, size / divisor);
    }

    [Theory]
    [InlineData(-111.111f, 222.222f, 333.333f)]
    public void DivideTestSizeFloatWidthHeightDivisor(float width, float height, float divisor)
    {
        SizeF size = new(width, height);
        SizeF expected = new(width / divisor, height / divisor);
        Assert.Equal(expected, size / divisor);
    }

    [Theory]
    [InlineData(float.MaxValue, float.MinValue)]
    [InlineData(float.MinValue, float.MinValue)]
    [InlineData(float.MaxValue, float.MaxValue)]
    [InlineData(0, 0)]
    public void DeconstructTest(float width, float height)
    {
        SizeF s = new(width, height);

        (float deconstructedWidth, float deconstructedHeight) = s;

        Assert.Equal(width, deconstructedWidth);
        Assert.Equal(height, deconstructedHeight);
    }
}
