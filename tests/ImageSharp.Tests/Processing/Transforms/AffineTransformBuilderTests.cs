// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.Processing;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Tests.Processing.Transforms
{
    public class AffineTransformBuilderTests : TransformBuilderTestBase<AffineTransformBuilder>
    {
        protected override void AppendTranslation(AffineTransformBuilder builder, PointF translate) => builder.AppendTranslation(translate);

        protected override void AppendScale(AffineTransformBuilder builder, SizeF scale) => builder.AppendScale(scale);

        protected override void AppendRotationRadians(AffineTransformBuilder builder, float radians) => builder.AppendRotationRadians(radians);

        protected override void AppendRotationRadians(AffineTransformBuilder builder, float radians, Vector2 origin) => builder.AppendRotationRadians(radians, origin);

        protected override void AppendRotationDegrees(AffineTransformBuilder builder, float degrees) => builder.AppendRotationDegrees(degrees);

        protected override void AppendRotationDegrees(AffineTransformBuilder builder, float degrees, Vector2 origin) => builder.AppendRotationDegrees(degrees, origin);

        protected override void PrependTranslation(AffineTransformBuilder builder, PointF translate) => builder.PrependTranslation(translate);

        protected override void PrependScale(AffineTransformBuilder builder, SizeF scale) => builder.PrependScale(scale);

        protected override void PrependRotationRadians(AffineTransformBuilder builder, float radians) => builder.PrependRotationRadians(radians);

        protected override void PrependRotationRadians(AffineTransformBuilder builder, float radians, Vector2 origin) =>
            builder.PrependRotationRadians(radians, origin);

        protected override AffineTransformBuilder CreateBuilder(Rectangle rectangle) => new AffineTransformBuilder();

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