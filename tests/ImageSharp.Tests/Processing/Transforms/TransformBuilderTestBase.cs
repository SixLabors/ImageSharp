// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;

namespace SixLabors.ImageSharp.Tests.Processing.Transforms;

[Trait("Category", "Processors")]
public abstract class TransformBuilderTestBase<TBuilder>
{
    private static readonly ApproximateFloatComparer Comparer = new(1e-6f);

    public static readonly TheoryData<Vector2, Vector2, Vector2, Vector2> ScaleTranslate_Data =
        new()
        {
                // scale, translate, source, expectedDest
                { Vector2.One, Vector2.Zero, Vector2.Zero, Vector2.Zero },
                { Vector2.One, Vector2.Zero, new Vector2(10, 20), new Vector2(10, 20) },
                { Vector2.One, new Vector2(3, 1), new Vector2(10, 20), new Vector2(13, 21) },
                { new Vector2(2, 0.5f), new Vector2(3, 1), new Vector2(10, 20), new Vector2(23, 11) },
            };

    [Theory]
    [MemberData(nameof(ScaleTranslate_Data))]
#pragma warning disable SA1300 // Element should begin with upper-case letter
    public void _1Scale_2Translate(Vector2 scale, Vector2 translate, Vector2 source, Vector2 expectedDest)
#pragma warning restore SA1300 // Element should begin with upper-case letter
    {
        // These operations should be size-agnostic:
        Size size = new(123, 321);
        TBuilder builder = this.CreateBuilder();

        this.AppendScale(builder, new SizeF(scale));
        this.AppendTranslation(builder, translate);

        Vector2 actualDest = this.Execute(builder, new Rectangle(Point.Empty, size), source);
        Assert.True(Comparer.Equals(expectedDest, actualDest));
    }

    public static readonly TheoryData<Vector2, Vector2, Vector2, Vector2> TranslateScale_Data =
        new()
        {
                // translate, scale, source, expectedDest
                { Vector2.Zero, Vector2.One, Vector2.Zero, Vector2.Zero },
                { Vector2.Zero, Vector2.One, new Vector2(10, 20), new Vector2(10, 20) },
                { new Vector2(3, 1), new Vector2(2, 0.5f), new Vector2(10, 20), new Vector2(26, 10.5f) },
            };

    [Theory]
    [MemberData(nameof(TranslateScale_Data))]
#pragma warning disable SA1300 // Element should begin with upper-case letter
    public void _1Translate_2Scale(Vector2 translate, Vector2 scale, Vector2 source, Vector2 expectedDest)
#pragma warning restore SA1300 // Element should begin with upper-case letter
    {
        // Translate ans scale are size-agnostic:
        Size size = new(456, 432);
        TBuilder builder = this.CreateBuilder();

        this.AppendTranslation(builder, translate);
        this.AppendScale(builder, new SizeF(scale));

        Vector2 actualDest = this.Execute(builder, new Rectangle(Point.Empty, size), source);
        Assert.Equal(expectedDest, actualDest, Comparer);
    }

    [Theory]
    [InlineData(10, 20)]
    [InlineData(-20, 10)]
    public void LocationOffsetIsPrepended(int locationX, int locationY)
    {
        Rectangle rectangle = new(locationX, locationY, 10, 10);
        TBuilder builder = this.CreateBuilder();

        this.AppendScale(builder, new SizeF(2, 2));

        Vector2 actual = this.Execute(builder, rectangle, Vector2.One);
        Vector2 expected = new Vector2(-locationX + 1, -locationY + 1) * 2;

        Assert.Equal(actual, expected, Comparer);
    }

    [Theory]
    [InlineData(200, 100, 10, 42, 84)]
    [InlineData(200, 100, 100, 42, 84)]
    [InlineData(100, 200, -10, 42, 84)]
    public void AppendRotationDegrees_WithoutSpecificRotationCenter_RotationIsCenteredAroundImageCenter(
        int width,
        int height,
        float degrees,
        float x,
        float y)
    {
        Size size = new(width, height);
        TBuilder builder = this.CreateBuilder();

        this.AppendRotationDegrees(builder, degrees);

        // TODO: We should also test CreateRotationMatrixDegrees() (and all TransformUtils stuff!) for correctness
        Matrix3x2 matrix = TransformUtils.CreateRotationTransformMatrixDegrees(degrees, size);

        Vector2 position = new(x, y);
        Vector2 expected = Vector2.Transform(position, matrix);
        Vector2 actual = this.Execute(builder, new Rectangle(Point.Empty, size), position);

        Assert.Equal(actual, expected, Comparer);
    }

    [Theory]
    [InlineData(200, 100, 10, 30, 61, 42, 84)]
    [InlineData(200, 100, 100, 30, 10, 20, 84)]
    [InlineData(100, 200, -10, 30, 20, 11, 84)]
    public void AppendRotationDegrees_WithRotationCenter(
        int width,
        int height,
        float degrees,
        float cx,
        float cy,
        float x,
        float y)
    {
        Size size = new(width, height);
        TBuilder builder = this.CreateBuilder();

        Vector2 centerPoint = new(cx, cy);
        this.AppendRotationDegrees(builder, degrees, centerPoint);

        Matrix3x2 matrix = Matrix3x2.CreateRotation(GeometryUtilities.DegreeToRadian(degrees), centerPoint);

        Vector2 position = new(x, y);
        Vector2 expected = Vector2.Transform(position, matrix);
        Vector2 actual = this.Execute(builder, new Rectangle(Point.Empty, size), position);

        Assert.Equal(actual, expected, Comparer);
    }

    [Theory]
    [InlineData(200, 100, 10, 10, 42, 84)]
    [InlineData(200, 100, 100, 100, 42, 84)]
    [InlineData(100, 200, -10, -10, 42, 84)]
    public void AppendSkewDegrees_WithoutSpecificSkewCenter_SkewIsCenteredAroundImageCenter(
        int width,
        int height,
        float degreesX,
        float degreesY,
        float x,
        float y)
    {
        Size size = new(width, height);
        TBuilder builder = this.CreateBuilder();

        this.AppendSkewDegrees(builder, degreesX, degreesY);

        Matrix3x2 matrix = TransformUtils.CreateSkewTransformMatrixDegrees(degreesX, degreesY, size);

        Vector2 position = new(x, y);
        Vector2 expected = Vector2.Transform(position, matrix);
        Vector2 actual = this.Execute(builder, new Rectangle(Point.Empty, size), position);
        Assert.Equal(actual, expected, Comparer);
    }

    [Theory]
    [InlineData(200, 100, 10, 10, 30, 61, 42, 84)]
    [InlineData(200, 100, 100, 100, 30, 10, 20, 84)]
    [InlineData(100, 200, -10, -10, 30, 20, 11, 84)]
    public void AppendSkewDegrees_WithSkewCenter(
        int width,
        int height,
        float degreesX,
        float degreesY,
        float cx,
        float cy,
        float x,
        float y)
    {
        Size size = new(width, height);
        TBuilder builder = this.CreateBuilder();

        Vector2 centerPoint = new(cx, cy);
        this.AppendSkewDegrees(builder, degreesX, degreesY, centerPoint);

        Matrix3x2 matrix = Matrix3x2.CreateSkew(GeometryUtilities.DegreeToRadian(degreesX), GeometryUtilities.DegreeToRadian(degreesY), centerPoint);

        Vector2 position = new(x, y);
        Vector2 expected = Vector2.Transform(position, matrix);
        Vector2 actual = this.Execute(builder, new Rectangle(Point.Empty, size), position);

        Assert.Equal(actual, expected, Comparer);
    }

    [Fact]
    public void AppendPrependOpposite()
    {
        Rectangle rectangle = new(-1, -1, 3, 3);
        TBuilder b1 = this.CreateBuilder();
        TBuilder b2 = this.CreateBuilder();

        const float pi = (float)Math.PI;

        // Forwards
        this.AppendRotationRadians(b1, pi);
        this.AppendSkewRadians(b1, pi, pi);
        this.AppendScale(b1, new SizeF(2, 0.5f));
        this.AppendRotationRadians(b1, pi / 2, new Vector2(-0.5f, -0.1f));
        this.AppendSkewRadians(b1, pi, pi / 2, new Vector2(-0.5f, -0.1f));
        this.AppendTranslation(b1, new PointF(123, 321));

        // Backwards
        this.PrependTranslation(b2, new PointF(123, 321));
        this.PrependSkewRadians(b2, pi, pi / 2, new Vector2(-0.5f, -0.1f));
        this.PrependRotationRadians(b2, pi / 2, new Vector2(-0.5f, -0.1f));
        this.PrependScale(b2, new SizeF(2, 0.5f));
        this.PrependSkewRadians(b2, pi, pi);
        this.PrependRotationRadians(b2, pi);

        Vector2 p1 = this.Execute(b1, rectangle, new Vector2(32, 65));
        Vector2 p2 = this.Execute(b2, rectangle, new Vector2(32, 65));

        Assert.Equal(p1, p2, Comparer);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(-1, 0)]
    public void ThrowsForInvalidSizes(int width, int height)
    {
        Size size = new(width, height);

        Assert.ThrowsAny<ArgumentOutOfRangeException>(
            () =>
                {
                    TBuilder builder = this.CreateBuilder();
                    this.Execute(builder, new Rectangle(Point.Empty, size), Vector2.Zero);
                });
    }

    [Fact]
    public void ThrowsForInvalidMatrix()
    {
        Assert.ThrowsAny<DegenerateTransformException>(
            () =>
            {
                TBuilder builder = this.CreateBuilder();
                this.AppendSkewDegrees(builder, 45, 45);
                this.Execute(builder, new Rectangle(0, 0, 150, 150), Vector2.Zero);
            });
    }

    protected abstract TBuilder CreateBuilder();

    protected abstract void AppendRotationDegrees(TBuilder builder, float degrees);

    protected abstract void AppendRotationDegrees(TBuilder builder, float degrees, Vector2 origin);

    protected abstract void AppendRotationRadians(TBuilder builder, float radians);

    protected abstract void AppendRotationRadians(TBuilder builder, float radians, Vector2 origin);

    protected abstract void AppendScale(TBuilder builder, SizeF scale);

    protected abstract void AppendSkewDegrees(TBuilder builder, float degreesX, float degreesY);

    protected abstract void AppendSkewDegrees(TBuilder builder, float degreesX, float degreesY, Vector2 origin);

    protected abstract void AppendSkewRadians(TBuilder builder, float radiansX, float radiansY);

    protected abstract void AppendSkewRadians(TBuilder builder, float radiansX, float radiansY, Vector2 origin);

    protected abstract void AppendTranslation(TBuilder builder, PointF translate);

    protected abstract void PrependRotationRadians(TBuilder builder, float radians);

    protected abstract void PrependRotationRadians(TBuilder builder, float radians, Vector2 origin);

    protected abstract void PrependScale(TBuilder builder, SizeF scale);

    protected abstract void PrependSkewRadians(TBuilder builder, float radiansX, float radiansY);

    protected abstract void PrependSkewRadians(TBuilder builder, float radiansX, float radiansY, Vector2 origin);

    protected abstract void PrependTranslation(TBuilder builder, PointF translate);

    protected abstract Vector2 Execute(TBuilder builder, Rectangle rectangle, Vector2 sourcePoint);
}
