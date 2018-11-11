// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using SixLabors.ImageSharp.Processing;
using SixLabors.Primitives;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Transforms
{
    public class AffineTransformBuilderTests
    {
        [Fact]
        public void ConstructorAssignsProperties()
        {
            var s = new Size(1, 1);
            var builder = new AffineTransformBuilder(new Rectangle(Point.Empty, s));
            Assert.Equal(s, builder.Size);
        }

        [Fact]
        public void ConstructorThrowsInvalid()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                var s = new Size(0, 1);
                var builder = new AffineTransformBuilder(new Rectangle(Point.Empty, s));
            });

            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                var s = new Size(1, 0);
                var builder = new AffineTransformBuilder(new Rectangle(Point.Empty, s));
            });
        }

        [Fact]
        public void AppendPrependOpposite()
        {
            var rectangle = new Rectangle(-1, -1, 3, 3);
            var b1 = new AffineTransformBuilder(rectangle);
            var b2 = new AffineTransformBuilder(rectangle);

            const float pi = (float)Math.PI;

            // Forwards
            b1.AppendRotateMatrixDegrees(pi)
              .AppendSkewMatrixDegrees(pi, pi)
              .AppendScaleMatrix(new SizeF(pi, pi))
              .AppendTranslationMatrix(new PointF(pi, pi));

            // Backwards
            b2.PrependTranslationMatrix(new PointF(pi, pi))
              .PrependScaleMatrix(new SizeF(pi, pi))
              .PrependSkewMatrixDegrees(pi, pi)
              .PrependRotateMatrixDegrees(pi);

            Assert.Equal(b1.BuildMatrix(), b2.BuildMatrix());
        }

        [Fact]
        public void BuilderCanClear()
        {
            var rectangle = new Rectangle(0, 0, 3, 3);
            var builder = new AffineTransformBuilder(rectangle);
            Matrix3x2 matrix = Matrix3x2.Identity;
            matrix.M31 = (float)Math.PI;

            Assert.Equal(Matrix3x2.Identity, builder.BuildMatrix());

            builder.AppendMatrix(matrix);
            Assert.NotEqual(Matrix3x2.Identity, builder.BuildMatrix());

            builder.Clear();
            Assert.Equal(Matrix3x2.Identity, builder.BuildMatrix());
        }
    }
}
