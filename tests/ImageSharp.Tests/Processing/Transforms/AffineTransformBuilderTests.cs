// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.Processing;

namespace SixLabors.ImageSharp.Tests.Processing.Transforms
{
    public class AffineTransformBuilderTests : TransformBuilderTestBase<AffineTransformBuilder>
    {
        protected override AffineTransformBuilder CreateBuilder()
            => new AffineTransformBuilder();

        protected override void AppendRotationDegrees(AffineTransformBuilder builder, float degrees)
            => builder.AppendRotationDegrees(degrees);

        protected override void AppendRotationDegrees(AffineTransformBuilder builder, float degrees, Vector2 origin)
            => builder.AppendRotationDegrees(degrees, origin);

        protected override void AppendRotationRadians(AffineTransformBuilder builder, float radians)
            => builder.AppendRotationRadians(radians);

        protected override void AppendRotationRadians(AffineTransformBuilder builder, float radians, Vector2 origin)
            => builder.AppendRotationRadians(radians, origin);

        protected override void AppendScale(AffineTransformBuilder builder, SizeF scale)
            => builder.AppendScale(scale);

        protected override void AppendSkewDegrees(AffineTransformBuilder builder, float degreesX, float degreesY)
            => builder.AppendSkewDegrees(degreesX, degreesY);

        protected override void AppendSkewDegrees(AffineTransformBuilder builder, float degreesX, float degreesY, Vector2 origin)
            => builder.AppendSkewDegrees(degreesX, degreesY, origin);

        protected override void AppendSkewRadians(AffineTransformBuilder builder, float radiansX, float radiansY)
            => builder.AppendSkewRadians(radiansX, radiansY);

        protected override void AppendSkewRadians(AffineTransformBuilder builder, float radiansX, float radiansY, Vector2 origin)
            => builder.AppendSkewRadians(radiansX, radiansY, origin);

        protected override void AppendTranslation(AffineTransformBuilder builder, PointF translate)
            => builder.AppendTranslation(translate);

        protected override void PrependRotationRadians(AffineTransformBuilder builder, float radians)
            => builder.PrependRotationRadians(radians);

        protected override void PrependRotationRadians(AffineTransformBuilder builder, float radians, Vector2 origin)
            => builder.PrependRotationRadians(radians, origin);

        protected override void PrependScale(AffineTransformBuilder builder, SizeF scale)
            => builder.PrependScale(scale);

        protected override void PrependSkewRadians(AffineTransformBuilder builder, float radiansX, float radiansY)
            => builder.PrependSkewRadians(radiansX, radiansY);

        protected override void PrependSkewRadians(AffineTransformBuilder builder, float radiansX, float radiansY, Vector2 origin)
            => builder.PrependSkewRadians(radiansX, radiansY, origin);

        protected override void PrependTranslation(AffineTransformBuilder builder, PointF translate)
            => builder.PrependTranslation(translate);

        protected override Vector2 Execute(
            AffineTransformBuilder builder,
            Rectangle rectangle,
            Vector2 sourcePoint)
        {
            Matrix3x2 matrix = builder.BuildMatrix(rectangle);
            return Vector2.Transform(sourcePoint, matrix);
        }
    }
}
