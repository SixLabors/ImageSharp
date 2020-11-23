// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms
{
    /// <summary>
    /// Contains utility methods for working with transforms.
    /// </summary>
    internal static class TransformUtils
    {
        /// <summary>
        /// Returns a value that indicates whether the specified matrix is degenerate
        /// containing one or more values equivalent to <see cref="float.NaN"/> or a
        /// zero determinant and therefore cannot be used for linear transforms.
        /// </summary>
        /// <param name="matrix">The transform matrix.</param>
        public static bool IsDegenerate(Matrix3x2 matrix)
            => IsNaN(matrix) || IsZero(matrix.GetDeterminant());

        /// <summary>
        /// Returns a value that indicates whether the specified matrix is degenerate
        /// containing one or more values equivalent to <see cref="float.NaN"/> or a
        /// zero determinant and therefore cannot be used for linear transforms.
        /// </summary>
        /// <param name="matrix">The transform matrix.</param>
        public static bool IsDegenerate(Matrix4x4 matrix)
            => IsNaN(matrix) || IsZero(matrix.GetDeterminant());

        [MethodImpl(InliningOptions.ShortMethod)]
        private static bool IsZero(float a)
            => a > -Constants.EpsilonSquared && a < Constants.EpsilonSquared;

        /// <summary>
        /// Returns a value that indicates whether the specified matrix contains any values
        /// that are not a number <see cref="float.NaN"/>.
        /// </summary>
        /// <param name="matrix">The transform matrix.</param>
        /// <returns>The <see cref="bool"/>.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static bool IsNaN(Matrix3x2 matrix)
        {
            return float.IsNaN(matrix.M11) || float.IsNaN(matrix.M12)
                || float.IsNaN(matrix.M21) || float.IsNaN(matrix.M22)
                || float.IsNaN(matrix.M31) || float.IsNaN(matrix.M32);
        }

        /// <summary>
        /// Returns a value that indicates whether the specified matrix contains any values
        /// that are not a number <see cref="float.NaN"/>.
        /// </summary>
        /// <param name="matrix">The transform matrix.</param>
        /// <returns>The <see cref="bool"/>.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static bool IsNaN(Matrix4x4 matrix)
        {
            return float.IsNaN(matrix.M11) || float.IsNaN(matrix.M12) || float.IsNaN(matrix.M13) || float.IsNaN(matrix.M14)
                || float.IsNaN(matrix.M21) || float.IsNaN(matrix.M22) || float.IsNaN(matrix.M23) || float.IsNaN(matrix.M24)
                || float.IsNaN(matrix.M31) || float.IsNaN(matrix.M32) || float.IsNaN(matrix.M33) || float.IsNaN(matrix.M34)
                || float.IsNaN(matrix.M41) || float.IsNaN(matrix.M42) || float.IsNaN(matrix.M43) || float.IsNaN(matrix.M44);
        }

        /// <summary>
        /// Applies the projective transform against the given coordinates flattened into the 2D space.
        /// </summary>
        /// <param name="x">The "x" vector coordinate.</param>
        /// <param name="y">The "y" vector coordinate.</param>
        /// <param name="matrix">The transform matrix.</param>
        /// <returns>The <see cref="Vector2"/>.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static Vector2 ProjectiveTransform2D(float x, float y, Matrix4x4 matrix)
        {
            const float Epsilon = 0.0000001F;
            var v4 = Vector4.Transform(new Vector4(x, y, 0, 1F), matrix);
            return new Vector2(v4.X, v4.Y) / MathF.Max(v4.W, Epsilon);
        }

        /// <summary>
        /// Creates a centered rotation matrix using the given rotation in degrees and the source size.
        /// </summary>
        /// <param name="degrees">The amount of rotation, in degrees.</param>
        /// <param name="size">The source image size.</param>
        /// <returns>The <see cref="Matrix3x2"/>.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static Matrix3x2 CreateRotationMatrixDegrees(float degrees, Size size)
            => CreateCenteredTransformMatrix(
                new Rectangle(Point.Empty, size),
                Matrix3x2Extensions.CreateRotationDegrees(degrees, PointF.Empty));

        /// <summary>
        /// Creates a centered rotation matrix using the given rotation in radians and the source size.
        /// </summary>
        /// <param name="radians">The amount of rotation, in radians.</param>
        /// <param name="size">The source image size.</param>
        /// <returns>The <see cref="Matrix3x2"/>.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static Matrix3x2 CreateRotationMatrixRadians(float radians, Size size)
            => CreateCenteredTransformMatrix(
                new Rectangle(Point.Empty, size),
                Matrix3x2Extensions.CreateRotation(radians, PointF.Empty));

        /// <summary>
        /// Creates a centered skew matrix from the give angles in degrees and the source size.
        /// </summary>
        /// <param name="degreesX">The X angle, in degrees.</param>
        /// <param name="degreesY">The Y angle, in degrees.</param>
        /// <param name="size">The source image size.</param>
        /// <returns>The <see cref="Matrix3x2"/>.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static Matrix3x2 CreateSkewMatrixDegrees(float degreesX, float degreesY, Size size)
            => CreateCenteredTransformMatrix(
                new Rectangle(Point.Empty, size),
                Matrix3x2Extensions.CreateSkewDegrees(degreesX, degreesY, PointF.Empty));

        /// <summary>
        /// Creates a centered skew matrix from the give angles in radians and the source size.
        /// </summary>
        /// <param name="radiansX">The X angle, in radians.</param>
        /// <param name="radiansY">The Y angle, in radians.</param>
        /// <param name="size">The source image size.</param>
        /// <returns>The <see cref="Matrix3x2"/>.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static Matrix3x2 CreateSkewMatrixRadians(float radiansX, float radiansY, Size size)
            => CreateCenteredTransformMatrix(
                new Rectangle(Point.Empty, size),
                Matrix3x2Extensions.CreateSkew(radiansX, radiansY, PointF.Empty));

        /// <summary>
        /// Gets the centered transform matrix based upon the source and destination rectangles.
        /// </summary>
        /// <param name="sourceRectangle">The source image bounds.</param>
        /// <param name="matrix">The transformation matrix.</param>
        /// <returns>The <see cref="Matrix3x2"/></returns>
        [MethodImpl(InliningOptions.ShortMethod)]
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
        [MethodImpl(InliningOptions.ShortMethod)]
        public static Matrix4x4 CreateTaperMatrix(Size size, TaperSide side, TaperCorner corner, float fraction)
        {
            Matrix4x4 matrix = Matrix4x4.Identity;

            /*
             * SkMatrix is laid out in the following manner:
             *
             * [ ScaleX  SkewY   Persp0 ]
             * [ SkewX   ScaleY  Persp1 ]
             * [ TransX  TransY  Persp2 ]
             *
             * When converting from Matrix4x4 to SkMatrix, the third row and
             * column is dropped.  When converting from SkMatrix to Matrix4x4
             * the third row and column remain as identity:
             *
             * [ a b c ]      [ a b 0 c ]
             * [ d e f ]  ->  [ d e 0 f ]
             * [ g h i ]      [ 0 0 1 0 ]
             *                [ g h 0 i ]
             */
            switch (side)
            {
                case TaperSide.Left:
                    matrix.M11 = fraction;
                    matrix.M22 = fraction;
                    matrix.M14 = (fraction - 1) / size.Width;

                    switch (corner)
                    {
                        case TaperCorner.RightOrBottom:
                            break;

                        case TaperCorner.LeftOrTop:
                            matrix.M12 = size.Height * matrix.M14;
                            matrix.M42 = size.Height * (1 - fraction);
                            break;

                        case TaperCorner.Both:
                            matrix.M12 = size.Height * .5F * matrix.M14;
                            matrix.M42 = size.Height * (1 - fraction) / 2;
                            break;
                    }

                    break;

                case TaperSide.Top:
                    matrix.M11 = fraction;
                    matrix.M22 = fraction;
                    matrix.M24 = (fraction - 1) / size.Height;

                    switch (corner)
                    {
                        case TaperCorner.RightOrBottom:
                            break;

                        case TaperCorner.LeftOrTop:
                            matrix.M21 = size.Width * matrix.M24;
                            matrix.M41 = size.Width * (1 - fraction);
                            break;

                        case TaperCorner.Both:
                            matrix.M21 = size.Width * .5F * matrix.M24;
                            matrix.M41 = size.Width * (1 - fraction) * .5F;
                            break;
                    }

                    break;

                case TaperSide.Right:
                    matrix.M11 = 1 / fraction;
                    matrix.M14 = (1 - fraction) / (size.Width * fraction);

                    switch (corner)
                    {
                        case TaperCorner.RightOrBottom:
                            break;

                        case TaperCorner.LeftOrTop:
                            matrix.M12 = size.Height * matrix.M14;
                            break;

                        case TaperCorner.Both:
                            matrix.M12 = size.Height * .5F * matrix.M14;
                            break;
                    }

                    break;

                case TaperSide.Bottom:
                    matrix.M22 = 1 / fraction;
                    matrix.M24 = (1 - fraction) / (size.Height * fraction);

                    switch (corner)
                    {
                        case TaperCorner.RightOrBottom:
                            break;

                        case TaperCorner.LeftOrTop:
                            matrix.M21 = size.Width * matrix.M24;
                            break;

                        case TaperCorner.Both:
                            matrix.M21 = size.Width * .5F * matrix.M24;
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
        [MethodImpl(InliningOptions.ShortMethod)]
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
        [MethodImpl(InliningOptions.ShortMethod)]
        public static Rectangle GetTransformedRectangle(Rectangle rectangle, Matrix4x4 matrix)
        {
            if (rectangle.Equals(default) || Matrix4x4.Identity.Equals(matrix))
            {
                return rectangle;
            }

            Vector2 tl = ProjectiveTransform2D(rectangle.Left, rectangle.Top, matrix);
            Vector2 tr = ProjectiveTransform2D(rectangle.Right, rectangle.Top, matrix);
            Vector2 bl = ProjectiveTransform2D(rectangle.Left, rectangle.Bottom, matrix);
            Vector2 br = ProjectiveTransform2D(rectangle.Right, rectangle.Bottom, matrix);

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
        [MethodImpl(InliningOptions.ShortMethod)]
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

        [MethodImpl(InliningOptions.ShortMethod)]
        private static Size ConstrainSize(Rectangle rectangle)
        {
            // We want to resize the canvas here taking into account any translations.
            int height = rectangle.Top < 0 ? rectangle.Bottom : Math.Max(rectangle.Height, rectangle.Bottom);
            int width = rectangle.Left < 0 ? rectangle.Right : Math.Max(rectangle.Width, rectangle.Right);

            // If location in either direction is translated to a negative value equal to or exceeding the
            // dimensions in either direction we need to reassign the dimension.
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

        [MethodImpl(InliningOptions.ShortMethod)]
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
