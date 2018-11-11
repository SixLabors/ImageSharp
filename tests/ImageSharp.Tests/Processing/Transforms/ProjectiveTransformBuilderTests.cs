// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using SixLabors.ImageSharp.Processing;
using SixLabors.Primitives;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Transforms
{
    public class ProjectiveTransformBuilderTests
    {
        [Fact]
        public void ConstructorAssignsProperties()
        {
            var s = new Size(1, 1);
            var builder = new ProjectiveTransformBuilder(new Rectangle(Point.Empty, s));
            Assert.Equal(s, builder.Size);
        }

        [Fact]
        public void ConstructorThrowsInvalid()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                var s = new Size(0, 1);
                var builder = new ProjectiveTransformBuilder(new Rectangle(Point.Empty, s));
            });

            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                var s = new Size(1, 0);
                var builder = new ProjectiveTransformBuilder(new Rectangle(Point.Empty, s));
            });
        }

        [Fact]
        public void AppendPrependOpposite()
        {
            var rectangle = new Rectangle(-1, -1, 3, 3);
            var b1 = new ProjectiveTransformBuilder(rectangle);
            var b2 = new ProjectiveTransformBuilder(rectangle);

            const float pi = (float)Math.PI;

            Matrix4x4 m4 = Matrix4x4.Identity;
            m4.M31 = pi;

            // Forwards
            b1.AppendMatrix(m4)
              .AppendTaperMatrix(TaperSide.Left, TaperCorner.LeftOrTop, pi);

            // Backwards
            b2.PrependTaperMatrix(TaperSide.Left, TaperCorner.LeftOrTop, pi)
              .PrependMatrix(m4);

            Assert.Equal(b1.BuildMatrix(), b2.BuildMatrix());
        }

        [Fact]
        public void BuilderCanClear()
        {
            var rectangle = new Rectangle(0, 0, 3, 3);
            var builder = new ProjectiveTransformBuilder(rectangle);
            Matrix4x4 matrix = Matrix4x4.Identity;
            matrix.M31 = (float)Math.PI;

            Assert.Equal(Matrix4x4.Identity, builder.BuildMatrix());

            builder.AppendMatrix(matrix);
            Assert.NotEqual(Matrix4x4.Identity, builder.BuildMatrix());

            builder.Clear();
            Assert.Equal(Matrix4x4.Identity, builder.BuildMatrix());
        }
    }
}
