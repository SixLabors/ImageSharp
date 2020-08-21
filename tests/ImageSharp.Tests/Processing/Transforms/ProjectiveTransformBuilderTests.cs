// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using SixLabors.ImageSharp.Processing;

namespace SixLabors.ImageSharp.Tests.Processing.Transforms
{
    public class ProjectiveTransformBuilderTests : TransformBuilderTestBase<ProjectiveTransformBuilder>
    {
        protected override ProjectiveTransformBuilder CreateBuilder()
            => new ProjectiveTransformBuilder();

        protected override void AppendRotationDegrees(ProjectiveTransformBuilder builder, float degrees)
            => builder.AppendRotationDegrees(degrees);

        protected override void AppendRotationDegrees(ProjectiveTransformBuilder builder, float degrees, Vector2 origin)
            => builder.AppendRotationDegrees(degrees, origin);

        protected override void AppendRotationRadians(ProjectiveTransformBuilder builder, float radians)
            => builder.AppendRotationRadians(radians);

        protected override void AppendRotationRadians(ProjectiveTransformBuilder builder, float radians, Vector2 origin)
            => builder.AppendRotationRadians(radians, origin);

        protected override void AppendScale(ProjectiveTransformBuilder builder, SizeF scale) => builder.AppendScale(scale);

        protected override void AppendSkewDegrees(ProjectiveTransformBuilder builder, float degreesX, float degreesY)
            => builder.AppendSkewDegrees(degreesX, degreesY);

        protected override void AppendSkewDegrees(ProjectiveTransformBuilder builder, float degreesX, float degreesY, Vector2 origin)
            => builder.AppendSkewDegrees(degreesX, degreesY, origin);

        protected override void AppendSkewRadians(ProjectiveTransformBuilder builder, float radiansX, float radiansY)
            => builder.AppendSkewRadians(radiansX, radiansY);

        protected override void AppendSkewRadians(ProjectiveTransformBuilder builder, float radiansX, float radiansY, Vector2 origin)
            => builder.AppendSkewRadians(radiansX, radiansY, origin);

        protected override void AppendTranslation(ProjectiveTransformBuilder builder, PointF translate) => builder.AppendTranslation(translate);

        protected override void PrependRotationRadians(ProjectiveTransformBuilder builder, float radians) => builder.PrependRotationRadians(radians);

        protected override void PrependScale(ProjectiveTransformBuilder builder, SizeF scale) => builder.PrependScale(scale);

        protected override void PrependSkewRadians(ProjectiveTransformBuilder builder, float radiansX, float radiansY)
            => builder.PrependSkewRadians(radiansX, radiansY);

        protected override void PrependSkewRadians(ProjectiveTransformBuilder builder, float radiansX, float radiansY, Vector2 origin)
            => builder.PrependSkewRadians(radiansX, radiansY, origin);

        protected override void PrependTranslation(ProjectiveTransformBuilder builder, PointF translate)
            => builder.PrependTranslation(translate);

        protected override void PrependRotationRadians(ProjectiveTransformBuilder builder, float radians, Vector2 origin) =>
            builder.PrependRotationRadians(radians, origin);

        protected override Vector2 Execute(
            ProjectiveTransformBuilder builder,
            Rectangle rectangle,
            Vector2 sourcePoint)
        {
            Matrix4x4 matrix = builder.BuildMatrix(rectangle);
            return Vector2.Transform(sourcePoint, matrix);
        }
    }
}
