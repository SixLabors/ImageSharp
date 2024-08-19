// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms;

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsZero(float a)
        => a > -Constants.EpsilonSquared && a < Constants.EpsilonSquared;

    /// <summary>
    /// Returns a value that indicates whether the specified matrix contains any values
    /// that are not a number <see cref="float.NaN"/>.
    /// </summary>
    /// <param name="matrix">The transform matrix.</param>
    /// <returns>The <see cref="bool"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNaN(Matrix3x2 matrix)
        => float.IsNaN(matrix.M11) || float.IsNaN(matrix.M12)
        || float.IsNaN(matrix.M21) || float.IsNaN(matrix.M22)
        || float.IsNaN(matrix.M31) || float.IsNaN(matrix.M32);

    /// <summary>
    /// Returns a value that indicates whether the specified matrix contains any values
    /// that are not a number <see cref="float.NaN"/>.
    /// </summary>
    /// <param name="matrix">The transform matrix.</param>
    /// <returns>The <see cref="bool"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNaN(Matrix4x4 matrix)
        => float.IsNaN(matrix.M11) || float.IsNaN(matrix.M12) || float.IsNaN(matrix.M13) || float.IsNaN(matrix.M14)
        || float.IsNaN(matrix.M21) || float.IsNaN(matrix.M22) || float.IsNaN(matrix.M23) || float.IsNaN(matrix.M24)
        || float.IsNaN(matrix.M31) || float.IsNaN(matrix.M32) || float.IsNaN(matrix.M33) || float.IsNaN(matrix.M34)
        || float.IsNaN(matrix.M41) || float.IsNaN(matrix.M42) || float.IsNaN(matrix.M43) || float.IsNaN(matrix.M44);

    /// <summary>
    /// Applies the projective transform against the given coordinates flattened into the 2D space.
    /// </summary>
    /// <param name="x">The "x" vector coordinate.</param>
    /// <param name="y">The "y" vector coordinate.</param>
    /// <param name="matrix">The transform matrix.</param>
    /// <returns>The <see cref="Vector2"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 ProjectiveTransform2D(float x, float y, Matrix4x4 matrix)
    {
        // The w component (v4.W) resulting from the transformation can be less than 0 in certain cases,
        // such as when the point is transformed behind the camera in a perspective projection.
        // However, in many 2D contexts, negative w values are not meaningful and could cause issues
        // like flipped or distorted projections. To avoid this, we take the max of w and epsilon to ensure
        // we don't divide by a very small or negative number, effectively treating any negative w as epsilon.
        const float epsilon = 0.0000001F;
        Vector4 v4 = Vector4.Transform(new Vector4(x, y, 0, 1F), matrix);
        return new Vector2(v4.X, v4.Y) / MathF.Max(v4.W, epsilon);
    }

    /// <summary>
    /// Creates a centered rotation transform matrix using the given rotation in degrees and the source size.
    /// </summary>
    /// <param name="degrees">The amount of rotation, in degrees.</param>
    /// <param name="size">The source image size.</param>
    /// <param name="transformSpace">The <see cref="TransformSpace"/> to use when creating the centered matrix.</param>
    /// <returns>The <see cref="Matrix3x2"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix3x2 CreateRotationTransformMatrixDegrees(float degrees, Size size, TransformSpace transformSpace)
        => CreateRotationTransformMatrixRadians(GeometryUtilities.DegreeToRadian(degrees), size, transformSpace);

    /// <summary>
    /// Creates a centered rotation transform matrix using the given rotation in radians and the source size.
    /// </summary>
    /// <param name="radians">The amount of rotation, in radians.</param>
    /// <param name="size">The source image size.</param>
    /// <param name="transformSpace">The <see cref="TransformSpace"/> to use when creating the centered matrix.</param>
    /// <returns>The <see cref="Matrix3x2"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix3x2 CreateRotationTransformMatrixRadians(float radians, Size size, TransformSpace transformSpace)
        => CreateCenteredTransformMatrix(Matrix3x2Extensions.CreateRotation(radians, PointF.Empty), size, transformSpace);

    /// <summary>
    /// Creates a centered skew transform matrix from the give angles in degrees and the source size.
    /// </summary>
    /// <param name="degreesX">The X angle, in degrees.</param>
    /// <param name="degreesY">The Y angle, in degrees.</param>
    /// <param name="size">The source image size.</param>
    /// <param name="transformSpace">The <see cref="TransformSpace"/> to use when creating the centered matrix.</param>
    /// <returns>The <see cref="Matrix3x2"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix3x2 CreateSkewTransformMatrixDegrees(float degreesX, float degreesY, Size size, TransformSpace transformSpace)
        => CreateSkewTransformMatrixRadians(GeometryUtilities.DegreeToRadian(degreesX), GeometryUtilities.DegreeToRadian(degreesY), size, transformSpace);

    /// <summary>
    /// Creates a centered skew transform matrix from the give angles in radians and the source size.
    /// </summary>
    /// <param name="radiansX">The X angle, in radians.</param>
    /// <param name="radiansY">The Y angle, in radians.</param>
    /// <param name="size">The source image size.</param>
    /// <param name="transformSpace">The <see cref="TransformSpace"/> to use when creating the centered matrix.</param>
    /// <returns>The <see cref="Matrix3x2"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix3x2 CreateSkewTransformMatrixRadians(float radiansX, float radiansY, Size size, TransformSpace transformSpace)
        => CreateCenteredTransformMatrix(Matrix3x2Extensions.CreateSkew(radiansX, radiansY, PointF.Empty), size, transformSpace);

    /// <summary>
    /// Gets the centered transform matrix based upon the source rectangle.
    /// </summary>
    /// <param name="matrix">The transformation matrix.</param>
    /// <param name="size">The source image size.</param>
    /// <param name="transformSpace">
    /// The <see cref="TransformSpace"/> to use when creating the centered matrix.
    /// </param>
    /// <returns>The <see cref="Matrix3x2"/></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix3x2 CreateCenteredTransformMatrix(Matrix3x2 matrix, Size size, TransformSpace transformSpace)
    {
        Size transformSize = GetUnboundedTransformedSize(matrix, size, transformSpace);

        // We invert the matrix to handle the transformation from screen to world space.
        // This ensures scaling matrices are correct.
        Matrix3x2.Invert(matrix, out Matrix3x2 inverted);

        // The source size is provided using the coordinate space of the source image.
        // however the transform should always be applied in the pixel space.
        // To account for this we offset by the size - 1 to translate to the pixel space.
        float offset = transformSpace == TransformSpace.Pixel ? 1F : 0F;

        Matrix3x2 translationToTargetCenter = Matrix3x2.CreateTranslation(new Vector2(-(transformSize.Width - offset), -(transformSize.Height - offset)) * .5F);
        Matrix3x2 translateToSourceCenter = Matrix3x2.CreateTranslation(new Vector2(size.Width - offset, size.Height - offset) * .5F);

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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
    /// Returns the size relative to the source for the given transformation matrix.
    /// </summary>
    /// <param name="matrix">The transformation matrix.</param>
    /// <param name="size">The source size.</param>
    /// <param name="transformSpace">The <see cref="TransformSpace"/> to use when calculating the size.</param>
    /// <returns>The <see cref="Size"/>.</returns>
    public static Size GetTransformedSize(Matrix3x2 matrix, Size size, TransformSpace transformSpace)
        => GetTransformedSize(matrix, size, transformSpace, true);

    /// <summary>
    /// Returns the size relative to the source for the given transformation matrix.
    /// </summary>
    /// <param name="matrix">The transformation matrix.</param>
    /// <param name="size">The source size.</param>
    /// <returns>
    /// The <see cref="Size"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Size GetTransformedSize(Matrix4x4 matrix, Size size)
    {
        Guard.IsTrue(size.Width > 0 && size.Height > 0, nameof(size), "Source size dimensions cannot be 0!");

        if (matrix.Equals(default) || matrix.Equals(Matrix4x4.Identity))
        {
            return size;
        }

        // Check if the matrix involves only affine transformations by inspecting the relevant components.
        // We want to use pixel space for calculations only if the transformation is purely 2D and does not include
        // any perspective effects, non-standard scaling, or unusual translations that could distort the image.
        // The conditions are as follows:
        bool usePixelSpace =

            // 1. Ensure there's no perspective distortion:
            // M34 corresponds to the perspective component. For a purely 2D affine transformation, this should be 0.
            (matrix.M34 == 0) &&

            // 2. Ensure standard affine transformation without any unusual depth or perspective scaling:
            // M44 should be 1 for a standard affine transformation. If M44 is not 1, it indicates non-standard depth
            // scaling or perspective, which suggests a more complex transformation.
            (matrix.M44 == 1) &&

            // 3. Ensure no unusual translation in the x-direction:
            // M14 represents translation in the x-direction that might be part of a more complex transformation.
            // For standard affine transformations, M14 should be 0.
            (matrix.M14 == 0) &&

            // 4. Ensure no unusual translation in the y-direction:
            // M24 represents translation in the y-direction that might be part of a more complex transformation.
            // For standard affine transformations, M24 should be 0.
            (matrix.M24 == 0);

        // Define an offset size to translate between pixel space and coordinate space.
        // When using pixel space, apply a scaling sensitive offset to translate to discrete pixel coordinates.
        // When not using pixel space, use SizeF.Empty as the offset.

        // Compute scaling factors from the matrix
        float scaleX = 1F / new Vector2(matrix.M11, matrix.M21).Length(); // sqrt(M11^2 + M21^2)
        float scaleY = 1F / new Vector2(matrix.M12, matrix.M22).Length(); // sqrt(M12^2 + M22^2)

        // Apply the offset relative to the scale
        SizeF offsetSize = usePixelSpace ? new SizeF(scaleX, scaleY) : SizeF.Empty;

        // Subtract the offset size to translate to the appropriate space (pixel or coordinate).
        if (TryGetTransformedRectangle(new RectangleF(Point.Empty, size - offsetSize), matrix, out Rectangle bounds))
        {
            // Add the offset size back to translate the transformed bounds to the correct space.
            return Size.Ceiling(ConstrainSize(bounds) + offsetSize);
        }

        return size;
    }

    /// <summary>
    /// Returns the size relative to the source for the given transformation matrix.
    /// </summary>
    /// <param name="matrix">The transformation matrix.</param>
    /// <param name="size">The source size.</param>
    /// <param name="transformSpace">The <see cref="TransformSpace"/> to use when calculating the size.</param>
    /// <returns>The <see cref="Size"/>.</returns>
    private static Size GetUnboundedTransformedSize(Matrix3x2 matrix, Size size, TransformSpace transformSpace)
        => GetTransformedSize(matrix, size, transformSpace, false);

    /// <summary>
    /// Returns the size relative to the source for the given transformation matrix.
    /// </summary>
    /// <param name="matrix">The transformation matrix.</param>
    /// <param name="size">The source size.</param>
    /// <param name="transformSpace">The <see cref="TransformSpace"/> to use when calculating the size.</param>
    /// <param name="constrain">Whether to constrain the size to ensure that the dimensions are positive.</param>
    /// <returns>
    /// The <see cref="Size"/>.
    /// </returns>
    private static Size GetTransformedSize(Matrix3x2 matrix, Size size, TransformSpace transformSpace, bool constrain)
    {
        Guard.IsTrue(size.Width > 0 && size.Height > 0, nameof(size), "Source size dimensions cannot be 0!");

        if (matrix.Equals(default) || matrix.Equals(Matrix3x2.Identity))
        {
            return size;
        }

        // Define an offset size to translate between coordinate space and pixel space.
        // Compute scaling factors from the matrix
        SizeF offsetSize = SizeF.Empty;
        if (transformSpace == TransformSpace.Pixel)
        {
            float scaleX = 1F / new Vector2(matrix.M11, matrix.M21).Length(); // sqrt(M11^2 + M21^2)
            float scaleY = 1F / new Vector2(matrix.M12, matrix.M22).Length(); // sqrt(M12^2 + M22^2)
            offsetSize = new(scaleX, scaleY);
        }

        // Subtract the offset size to translate to the pixel space.
        if (TryGetTransformedRectangle(new RectangleF(Point.Empty, size - offsetSize), matrix, out Rectangle bounds))
        {
            // Add the offset size back to translate the transformed bounds to the coordinate space.
            return Size.Ceiling((constrain ? ConstrainSize(bounds) : bounds.Size) + offsetSize);
        }

        return size;
    }

    /// <summary>
    /// Returns the rectangle relative to the source for the given transformation matrix.
    /// </summary>
    /// <param name="rectangle">The source rectangle.</param>
    /// <param name="matrix">The transformation matrix.</param>
    /// <param name="bounds">The resulting bounding rectangle.</param>
    /// <returns>
    /// <see langword="true"/> if the transformation was successful; otherwise, <see langword="false"/>.
    /// </returns>
    private static bool TryGetTransformedRectangle(RectangleF rectangle, Matrix3x2 matrix, out Rectangle bounds)
    {
        if (rectangle.Equals(default) || Matrix3x2.Identity.Equals(matrix))
        {
            bounds = default;
            return false;
        }

        Vector2 tl = Vector2.Transform(new Vector2(rectangle.Left, rectangle.Top), matrix);
        Vector2 tr = Vector2.Transform(new Vector2(rectangle.Right, rectangle.Top), matrix);
        Vector2 bl = Vector2.Transform(new Vector2(rectangle.Left, rectangle.Bottom), matrix);
        Vector2 br = Vector2.Transform(new Vector2(rectangle.Right, rectangle.Bottom), matrix);

        bounds = GetBoundingRectangle(tl, tr, bl, br);
        return true;
    }

    /// <summary>
    /// Returns the rectangle relative to the source for the given transformation matrix.
    /// </summary>
    /// <param name="rectangle">The source rectangle.</param>
    /// <param name="matrix">The transformation matrix.</param>
    /// <param name="bounds">The resulting bounding rectangle.</param>
    /// <returns>
    /// <see langword="true"/> if the transformation was successful; otherwise, <see langword="false"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryGetTransformedRectangle(RectangleF rectangle, Matrix4x4 matrix, out Rectangle bounds)
    {
        if (rectangle.Equals(default) || Matrix4x4.Identity.Equals(matrix))
        {
            bounds = default;
            return false;
        }

        Vector2 tl = ProjectiveTransform2D(rectangle.Left, rectangle.Top, matrix);
        Vector2 tr = ProjectiveTransform2D(rectangle.Right, rectangle.Top, matrix);
        Vector2 bl = ProjectiveTransform2D(rectangle.Left, rectangle.Bottom, matrix);
        Vector2 br = ProjectiveTransform2D(rectangle.Right, rectangle.Bottom, matrix);

        bounds = GetBoundingRectangle(tl, tr, bl, br);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Rectangle GetBoundingRectangle(Vector2 tl, Vector2 tr, Vector2 bl, Vector2 br)
    {
        // Find the minimum and maximum "corners" based on the given vectors
        float left = MathF.Min(tl.X, MathF.Min(tr.X, MathF.Min(bl.X, br.X)));
        float top = MathF.Min(tl.Y, MathF.Min(tr.Y, MathF.Min(bl.Y, br.Y)));
        float right = MathF.Max(tl.X, MathF.Max(tr.X, MathF.Max(bl.X, br.X)));
        float bottom = MathF.Max(tl.Y, MathF.Max(tr.Y, MathF.Max(bl.Y, br.Y)));

        // Clamp the values to the nearest whole pixel.
        return Rectangle.FromLTRB(
            (int)Math.Floor(left),
            (int)Math.Floor(top),
            (int)Math.Ceiling(right),
            (int)Math.Ceiling(bottom));
    }
}
