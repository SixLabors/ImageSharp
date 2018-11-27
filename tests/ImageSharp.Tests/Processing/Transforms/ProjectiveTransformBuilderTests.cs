// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.Processing;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Tests.Processing.Transforms
{
    public class ProjectiveTransformBuilderTests : TransformBuilderTestBase<ProjectiveTransformBuilder>
    {
        protected override ProjectiveTransformBuilder CreateBuilder(Rectangle rectangle) => new ProjectiveTransformBuilder();

        protected override void AppendTranslation(ProjectiveTransformBuilder builder, PointF translate) => builder.AppendTranslation(translate);

        protected override void AppendScale(ProjectiveTransformBuilder builder, SizeF scale) => builder.AppendScale(scale);

        protected override void AppendRotationRadians(ProjectiveTransformBuilder builder, float radians) => builder.AppendRotationRadians(radians);

        protected override void AppendRotationRadians(ProjectiveTransformBuilder builder, float radians, Vector2 center) =>
            builder.AppendRotationRadians(radians, center);

        protected override void PrependTranslation(ProjectiveTransformBuilder builder, PointF translate) => builder.PrependTranslation(translate);

        protected override void PrependScale(ProjectiveTransformBuilder builder, SizeF scale) => builder.PrependScale(scale);

        protected override void PrependRotationRadians(ProjectiveTransformBuilder builder, float radians) => builder.PrependRotationRadians(radians);

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
