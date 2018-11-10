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
    internal static class TransformUtils
    {
        /// <summary>
        /// Creates a centered rotation matrix using the given rotation in degrees and the source size.
        /// </summary>
        /// <param name="degrees">The amount of rotation, in degrees.</param>
        /// <param name="size">The source image size.</param>
        /// <returns>The <see cref="Matrix3x2"/>.</returns>
        public static Matrix3x2 CreateRotationMatrixDegrees(float degrees, Size size)
            => CreateCenteredTransformMatrix(
                new Rectangle(Point.Empty, size),
                Matrix3x2Extensions.CreateRotationDegrees(degrees, PointF.Empty));

        /// <summary>
        /// Creates a centered skew matrix from the give angles in degrees and the source size.
        /// </summary>
        /// <param name="degreesX">The X angle, in degrees.</param>
        /// <param name="degreesY">The Y angle, in degrees.</param>
        /// <param name="size">The source image size.</param>
        /// <returns>The <see cref="Matrix3x2"/>.</returns>
        public static Matrix3x2 CreateSkewMatrixDegrees(float degreesX, float degreesY, Size size)
            => CreateCenteredTransformMatrix(
                new Rectangle(Point.Empty, size),
                Matrix3x2Extensions.CreateSkewDegrees(degreesX, degreesY, PointF.Empty));

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
        /// Creates a matrix that performs a tapering projective transform.
        /// <see href="https://docs.microsoft.com/en-us/xamarin/xamarin-forms/user-interface/graphics/skiasharp/transforms/non-affine"/>
        /// </summary>
        /// <param name="size">The rectangular size of the image being transformed.</param>
        /// <param name="side">An enumeration that indicates the side of the rectangle that tapers.</param>
        /// <param name="corner">An enumeration that indicates on which corners to taper the rectangle.</param>
        /// <param name="fraction">The amount to taper.</param>
        /// <returns>The <see cref="Matrix4x4"/></returns>
        public static Matrix4x4 CreateTaperMatrix(Size size, TaperSide side, TaperCorner corner, float fraction)
        {
            Matrix4x4 matrix = Matrix4x4.Identity;

            switch (side)
            {
                case TaperSide.Left:
                    matrix.M11 = fraction;
                    matrix.M22 = fraction;
                    matrix.M13 = (fraction - 1) / size.Width;

                    switch (corner)
                    {
                        case TaperCorner.RightOrBottom:
                            break;

                        case TaperCorner.LeftOrTop:
                            matrix.M12 = size.Height * matrix.M13;
                            matrix.M32 = size.Height * (1 - fraction);
                            break;

                        case TaperCorner.Both:
                            matrix.M12 = size.Height * .5F * matrix.M13;
                            matrix.M32 = size.Height * (1 - fraction) / 2;
                            break;
                    }

                    break;

                case TaperSide.Top:
                    matrix.M11 = fraction;
                    matrix.M22 = fraction;
                    matrix.M23 = (fraction - 1) / size.Height;

                    switch (corner)
                    {
                        case TaperCorner.RightOrBottom:
                            break;

                        case TaperCorner.LeftOrTop:
                            matrix.M21 = size.Width * matrix.M23;
                            matrix.M31 = size.Width * (1 - fraction);
                            break;

                        case TaperCorner.Both:
                            matrix.M21 = size.Width * .5F * matrix.M23;
                            matrix.M31 = size.Width * (1 - fraction) / 2;
                            break;
                    }

                    break;

                case TaperSide.Right:
                    matrix.M11 = 1 / fraction;
                    matrix.M13 = (1 - fraction) / (size.Width * fraction);

                    switch (corner)
                    {
                        case TaperCorner.RightOrBottom:
                            break;

                        case TaperCorner.LeftOrTop:
                            matrix.M12 = size.Height * matrix.M13;
                            break;

                        case TaperCorner.Both:
                            matrix.M12 = size.Height * .5F * matrix.M13;
                            break;
                    }

                    break;

                case TaperSide.Bottom:
                    matrix.M22 = 1 / fraction;
                    matrix.M23 = (1 - fraction) / (size.Height * fraction);

                    switch (corner)
                    {
                        case TaperCorner.RightOrBottom:
                            break;

                        case TaperCorner.LeftOrTop:
                            matrix.M21 = size.Width * matrix.M23;
                            break;

                        case TaperCorner.Both:
                            matrix.M21 = size.Width * .5F * matrix.M23;
                            break;
                    }

                    break;
            }

            return matrix;
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

        /// <summary>
        /// Returns the size relative to the source for the given transformation matrix.
        /// </summary>
        /// <param name="size">The source size.</param>
        /// <param name="matrix">The transformation matrix.</param>
        /// <returns>
        /// The <see cref="Size"/>.
        /// </returns>
        public static Size GetTransformedSize(Size size, Matrix3x2 matrix)
        {
            Guard.IsTrue(size.Width > 0 && size.Height > 0, nameof(size), "Source size dimensions cannot be 0!");

            if (matrix.Equals(default) || matrix.Equals(Matrix3x2.Identity))
            {
                return size;
            }

            Rectangle rectangle = GetTransformedRectangle(new Rectangle(Point.Empty, size), matrix);

            return ConstrainSize(rectangle);
        }

        /// <summary>
        /// Returns the rectangle relative to the source for the given transformation matrix.
        /// </summary>
        /// <param name="rectangle">The source rectangle.</param>
        /// <param name="matrix">The transformation matrix.</param>
        /// <returns>
        /// The <see cref="Rectangle"/>.
        /// </returns>
        public static Rectangle GetTransformedRectangle(Rectangle rectangle, Matrix4x4 matrix)
        {
            if (rectangle.Equals(default) || Matrix4x4.Identity.Equals(matrix))
            {
                return rectangle;
            }

            Vector2 GetVector(float x, float y)
            {
                const float Epsilon = 0.0000001F;
                var v3 = Vector3.Transform(new Vector3(x, y, 1F), matrix);
                return new Vector2(v3.X, v3.Y) / MathF.Max(v3.Z, Epsilon);
            }

            Vector2 tl = GetVector(rectangle.Left, rectangle.Top);
            Vector2 tr = GetVector(rectangle.Right, rectangle.Top);
            Vector2 bl = GetVector(rectangle.Left, rectangle.Bottom);
            Vector2 br = GetVector(rectangle.Right, rectangle.Bottom);

            return GetBoundingRectangle(tl, tr, bl, br);
        }

        /// <summary>
        /// Returns the size relative to the source for the given transformation matrix.
        /// </summary>
        /// <param name="size">The source size.</param>
        /// <param name="matrix">The transformation matrix.</param>
        /// <returns>
        /// The <see cref="Size"/>.
        /// </returns>
        public static Size GetTransformedSize(Size size, Matrix4x4 matrix)
        {
            Guard.IsTrue(size.Width > 0 && size.Height > 0, nameof(size), "Source size dimensions cannot be 0!");

            if (matrix.Equals(default) || matrix.Equals(Matrix4x4.Identity))
            {
                return size;
            }

            Rectangle rectangle = GetTransformedRectangle(new Rectangle(Point.Empty, size), matrix);

            return ConstrainSize(rectangle);
        }

        private static Size ConstrainSize(Rectangle rectangle)
        {
            // We want to resize the canvas here taking into account any translations.
            int height = rectangle.Top < 0 ? rectangle.Bottom : Math.Max(rectangle.Height, rectangle.Bottom);
            int width = rectangle.Left < 0 ? rectangle.Right : Math.Max(rectangle.Width, rectangle.Right);

            // If location in either direction is translated to a negative value equal to or exceeding the
            // dimensions in eith direction we need to reassign the dimension.
            if (height <= 0)
            {
                height = rectangle.Height;
            }

            if (width <= 0)
            {
                width = rectangle.Width;
            }

            return new Size(width, height);
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
