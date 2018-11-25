// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;
using SixLabors.Primitives;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Transforms
{
    public class AffineTransformBuilderTests : TransformBuilderTestBase<AffineTransformBuilder>
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

        protected override void AppendTranslation(AffineTransformBuilder builder, PointF translate) => builder.AppendTranslation(translate);
        protected override void AppendScale(AffineTransformBuilder builder, SizeF scale) => builder.AppendScale(scale);
        protected override void AppendRotationRadians(AffineTransformBuilder builder, float radians) => builder.AppendRotationRadians(radians);

        protected override void PrependTranslation(AffineTransformBuilder builder, PointF translate) => builder.PrependTranslation(translate);
        protected override void PrependScale(AffineTransformBuilder builder, SizeF scale) => builder.PrependScale(scale);
        protected override void PrependRotationRadians(AffineTransformBuilder builder, float radians) => builder.PrependRotationRadians(radians);

        protected override Vector2 Execute(
            AffineTransformBuilder builder,
            Rectangle rectangle,
            Vector2 sourcePoint)
        {
            Matrix3x2 matrix = builder.BuildMatrix();
            return Vector2.Transform(sourcePoint, matrix);
        }
    }
}
