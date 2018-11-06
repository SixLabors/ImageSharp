// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms
{
    /// <summary>
    /// Contains utility methods for working with transforms.
    /// </summary>
    public static class TransformUtils
    {
        /// <summary>
        /// Creates a centered rotation matrix using the given rotation in degrees and the source size.
        /// </summary>
        /// <param name="degrees">The amount of rotation, in degrees.</param>
        /// <param name="size">The source image size.</param>
        /// <returns>The <see cref="Matrix3x2"/>.</returns>
        public static Matrix3x2 CreateCenteredRotationMatrixDegrees(float degrees, Size size)
            => CreateCenteredTransformMatrix(
                new Rectangle(Point.Empty, size),
                Matrix3x2Extensions.CreateRotationDegrees(degrees, PointF.Empty));

        /// <summary>
        /// Gets the centered transform matrix based upon the source and destination rectangles.
        /// </summary>
        /// <param name="sourceRectangle">The source image bounds.</param>
        /// <param name="matrix">The transformation matrix.</param>
        /// <returns>The <see cref="Matrix3x2"/></returns>
        public static Matrix3x2 CreateCenteredTransformMatrix(Rectangle sourceRectangle, Matrix3x2 matrix)
        {
            Rectangle destinationRectangle = GetTransformedBoundingRectangle(sourceRectangle, matrix);

            // We invert the matrix to handle the transformation from screen to world space.
            // This ensures scaling matrices are correct.
            Matrix3x2.Invert(matrix, out Matrix3x2 inverted);

            var translationToTargetCenter = Matrix3x2.CreateTranslation(new Vector2(-destinationRectangle.Width, -destinationRectangle.Height) * .5F);
            var translateToSourceCenter = Matrix3x2.CreateTranslation(new Vector2(sourceRectangle.Width, sourceRectangle.Height) * .5F);

            // Translate back to world space.
            Matrix3x2.Invert(translationToTargetCenter * inverted * translateToSourceCenter, out Matrix3x2 centered);

            return centered;
        }

        /// <summary>
        /// Gets the centered transform matrix based upon the source and destination rectangles.
        /// </summary>
        /// <param name="sourceRectangle">The source image bounds.</param>
        /// <param name="destinationRectangle">The destination image bounds.</param>
        /// <param name="matrix">The transformation matrix.</param>
        /// <returns>The <see cref="Matrix3x2"/></returns>
        public static Matrix3x2 GetCenteredTransformMatrix(Rectangle sourceRectangle, Rectangle destinationRectangle, Matrix3x2 matrix)
        {
            // We invert the matrix to handle the transformation from screen to world space.
            // This ensures scaling matrices are correct.
            Matrix3x2.Invert(matrix, out Matrix3x2 inverted);

            var translationToTargetCenter = Matrix3x2.CreateTranslation(new Vector2(-destinationRectangle.Width, -destinationRectangle.Height) * .5F);
            var translateToSourceCenter = Matrix3x2.CreateTranslation(new Vector2(sourceRectangle.Width, sourceRectangle.Height) * .5F);

            // Translate back to world space.
            Matrix3x2.Invert(translationToTargetCenter * inverted * translateToSourceCenter, out Matrix3x2 centered);

            return centered;
        }

        /// <summary>
        /// Returns the rectangle bounds relative to the source for the given transformation matrix.
        /// </summary>
        /// <param name="rectangle">The source rectangle.</param>
        /// <param name="matrix">The transformation matrix.</param>
        /// <returns>
        /// The <see cref="Rectangle"/>.
        /// </returns>
        public static Rectangle GetTransformedBoundingRectangle(Rectangle rectangle, Matrix3x2 matrix)
        {
            Rectangle transformed = GetTransformedRectangle(rectangle, matrix);

            // TODO: Check this.
            return new Rectangle(0, 0, transformed.Width, transformed.Height);
        }

        /// <summary>
        /// Returns the rectangle relative to the source for the given transformation matrix.
        /// </summary>
        /// <param name="rectangle">The source rectangle.</param>
        /// <param name="matrix">The transformation matrix.</param>
        /// <returns>
        /// The <see cref="Rectangle"/>.
        /// </returns>
        public static Rectangle GetTransformedRectangle(Rectangle rectangle, Matrix3x2 matrix)
        {
            if (rectangle.Equals(default) || Matrix3x2.Identity.Equals(matrix))
            {
                return rectangle;
            }

            var tl = Vector2.Transform(new Vector2(rectangle.Left, rectangle.Top), matrix);
            var tr = Vector2.Transform(new Vector2(rectangle.Right, rectangle.Top), matrix);
            var bl = Vector2.Transform(new Vector2(rectangle.Left, rectangle.Bottom), matrix);
            var br = Vector2.Transform(new Vector2(rectangle.Right, rectangle.Bottom), matrix);

            return GetBoundingRectangle(tl, tr, bl, br);
        }

        private static Rectangle GetBoundingRectangle(Vector2 tl, Vector2 tr, Vector2 bl, Vector2 br)
        {
            // Find the minimum and maximum "corners" based on the given vectors
            float left = MathF.Min(tl.X, MathF.Min(tr.X, MathF.Min(bl.X, br.X)));
            float top = MathF.Min(tl.Y, MathF.Min(tr.Y, MathF.Min(bl.Y, br.Y)));
            float right = MathF.Max(tl.X, MathF.Max(tr.X, MathF.Max(bl.X, br.X)));
            float bottom = MathF.Max(tl.Y, MathF.Max(tr.Y, MathF.Max(bl.Y, br.Y)));

            return Rectangle.Round(RectangleF.FromLTRB(left, top, right, bottom));
        }
    }
}
