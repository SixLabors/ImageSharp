// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Processing.Processors.Transforms.Linear;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms;

/// <summary>
/// Contains utility methods for working with transforms.
/// </summary>
internal static class TransformUtilities
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
    /// Creates a centered rotation transform matrix using the given rotation in degrees and the original source size.
    /// </summary>
    /// <param name="degrees">The amount of rotation, in degrees.</param>
    /// <param name="size">The source image size.</param>
    /// <returns>The <see cref="Matrix3x2"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix3x2 CreateRotationTransformMatrixDegrees(float degrees, Size size)
        => CreateRotationTransformMatrixRadians(GeometryUtilities.DegreeToRadian(degrees), size);

    /// <summary>
    /// Creates a centered rotation transform matrix using the given rotation in radians and the original source size.
    /// </summary>
    /// <param name="radians">The amount of rotation, in radians.</param>
    /// <param name="size">The source image size.</param>
    /// <returns>The <see cref="Matrix3x2"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix3x2 CreateRotationTransformMatrixRadians(float radians, Size size)
        => CreateCenteredTransformMatrix(Matrix3x2Extensions.CreateRotation(radians, PointF.Empty), size);

    /// <summary>
    /// Creates a centered skew transform matrix from the give angles in degrees and the original source size.
    /// </summary>
    /// <param name="degreesX">The X angle, in degrees.</param>
    /// <param name="degreesY">The Y angle, in degrees.</param>
    /// <param name="size">The source image size.</param>
    /// <returns>The <see cref="Matrix3x2"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix3x2 CreateSkewTransformMatrixDegrees(float degreesX, float degreesY, Size size)
        => CreateSkewTransformMatrixRadians(GeometryUtilities.DegreeToRadian(degreesX), GeometryUtilities.DegreeToRadian(degreesY), size);

    /// <summary>
    /// Creates a centered skew transform matrix from the give angles in radians and the original source size.
    /// </summary>
    /// <param name="radiansX">The X angle, in radians.</param>
    /// <param name="radiansY">The Y angle, in radians.</param>
    /// <param name="size">The source image size.</param>
    /// <returns>The <see cref="Matrix3x2"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix3x2 CreateSkewTransformMatrixRadians(float radiansX, float radiansY, Size size)
        => CreateCenteredTransformMatrix(Matrix3x2Extensions.CreateSkew(radiansX, radiansY, PointF.Empty), size);

    /// <summary>
    /// Gets the centered transform matrix based upon the source rectangle.
    /// </summary>
    /// <param name="matrix">The transformation matrix.</param>
    /// <param name="size">The source image size.</param>
    /// <returns>The <see cref="Matrix3x2"/></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix3x2 CreateCenteredTransformMatrix(Matrix3x2 matrix, Size size)
    {
        // 1) Unbounded size.
        SizeF ts = GetRawTransformedSize(matrix, size);

        // 2) Invert the content transform for screen->world.
        Matrix3x2.Invert(matrix, out Matrix3x2 inv);

        // 3) Translate target (canvas) so its center is at the origin,
        // translate source so its center is at the origin, then undo the content transform.
        Matrix3x2 toTarget = Matrix3x2.CreateTranslation(new Vector2(-ts.Width, -ts.Height) * 0.5f);
        Matrix3x2 toSource = Matrix3x2.CreateTranslation(new Vector2(size.Width, size.Height) * 0.5f);

        // 4) World->screen.
        Matrix3x2.Invert(toTarget * inv * toSource, out Matrix3x2 centered);

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
    /// Computes the projection matrix for a quad distortion transformation.
    /// </summary>
    /// <param name="rectangle">The source rectangle.</param>
    /// <param name="topLeft">The top-left point of the distorted quad.</param>
    /// <param name="topRight">The top-right point of the distorted quad.</param>
    /// <param name="bottomRight">The bottom-right point of the distorted quad.</param>
    /// <param name="bottomLeft">The bottom-left point of the distorted quad.</param>
    /// <returns>The computed projection matrix for the quad distortion.</returns>
    /// <remarks>
    /// This method is based on the algorithm described in the following article:
    /// <see href="https://blog.mbedded.ninja/mathematics/geometry/projective-transformations/"/>
    /// </remarks>
    public static Matrix4x4 CreateQuadDistortionMatrix(
        Rectangle rectangle,
        PointF topLeft,
        PointF topRight,
        PointF bottomRight,
        PointF bottomLeft)
    {
        PointF p1 = new(rectangle.X, rectangle.Y);
        PointF p2 = new(rectangle.X + rectangle.Width, rectangle.Y);
        PointF p3 = new(rectangle.X + rectangle.Width, rectangle.Y + rectangle.Height);
        PointF p4 = new(rectangle.X, rectangle.Y + rectangle.Height);

        PointF q1 = topLeft;
        PointF q2 = topRight;
        PointF q3 = bottomRight;
        PointF q4 = bottomLeft;

        double[][] matrixData =
        [
            [p1.X, p1.Y, 1, 0, 0, 0, -p1.X * q1.X, -p1.Y * q1.X],
            [0, 0, 0, p1.X, p1.Y, 1, -p1.X * q1.Y, -p1.Y * q1.Y],
            [p2.X, p2.Y, 1, 0, 0, 0, -p2.X * q2.X, -p2.Y * q2.X],
            [0, 0, 0, p2.X, p2.Y, 1, -p2.X * q2.Y, -p2.Y * q2.Y],
            [p3.X, p3.Y, 1, 0, 0, 0, -p3.X * q3.X, -p3.Y * q3.X],
            [0, 0, 0, p3.X, p3.Y, 1, -p3.X * q3.Y, -p3.Y * q3.Y],
            [p4.X, p4.Y, 1, 0, 0, 0, -p4.X * q4.X, -p4.Y * q4.X],
            [0, 0, 0, p4.X, p4.Y, 1, -p4.X * q4.Y, -p4.Y * q4.Y],
        ];

        double[] b =
        [
            q1.X,
            q1.Y,
            q2.X,
            q2.Y,
            q3.X,
            q3.Y,
            q4.X,
            q4.Y,
        ];

        GaussianEliminationSolver.Solve(matrixData, b);

#pragma warning disable SA1117
        Matrix4x4 projectionMatrix = new(
            (float)b[0], (float)b[3], 0, (float)b[6],
            (float)b[1], (float)b[4], 0, (float)b[7],
            0, 0, 1, 0,
            (float)b[2], (float)b[5], 0, 1);
#pragma warning restore SA1117

        return projectionMatrix;
    }

    /// <summary>
    /// Calculates the size of a destination canvas large enough to contain
    /// the fully transformed source content, including any translation offsets.
    /// </summary>
    /// <param name="matrix">The transformation matrix.</param>
    /// <param name="size">The original source size.</param>
    /// <returns>
    /// A <see cref="SizeF"/> representing the dimensions of the destination
    /// canvas required to fully contain the transformed source, including
    /// any positive or negative translation offsets.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method ensures that the transformed content remains fully visible
    /// on the destination canvas by expanding its size to include translations
    /// in all directions.
    /// </para>
    /// <para>
    /// It behaves identically to calling
    /// <see cref="GetTransformedSize(Matrix3x2, Size, bool)"/> with
    /// <c>preserveCanvas</c> set to <see langword="true"/>.
    /// </para>
    /// <para>
    /// The resulting canvas size represents the total area required to display
    /// the transformed image without clipping, not merely the geometric bounds
    /// of the transformed source.
    /// </para>
    /// </remarks>
    public static Size GetTransformedCanvasSize(Matrix3x2 matrix, Size size)
        => Size.Ceiling(GetTransformedSize(matrix, size, true));

    /// <summary>
    /// Calculates the size of a destination canvas large enough to contain
    /// the fully transformed source content, including any translation offsets.
    /// </summary>
    /// <param name="matrix">The transformation matrix.</param>
    /// <param name="size">The original source size.</param>
    /// <returns>
    /// A <see cref="SizeF"/> representing the dimensions of the destination
    /// canvas required to fully contain the transformed source, including
    /// any positive or negative translation offsets.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method ensures that the transformed content remains fully visible
    /// on the destination canvas by expanding its size to include translations
    /// in all directions.
    /// </para>
    /// <para>
    /// It behaves identically to calling
    /// <see cref="GetTransformedSize(Matrix3x2, Size, bool)"/> with
    /// <c>preserveCanvas</c> set to <see langword="true"/>.
    /// </para>
    /// <para>
    /// The resulting canvas size represents the total area required to display
    /// the transformed image without clipping, not merely the geometric bounds
    /// of the transformed source.
    /// </para>
    /// </remarks>
    public static Size GetTransformedCanvasSize(Matrix4x4 matrix, Size size)
        => Size.Ceiling(GetTransformedSize(matrix, size, true));

    /// <summary>
    /// Returns the size relative to the source for the given transformation matrix.
    /// </summary>
    /// <param name="matrix">The transformation matrix.</param>
    /// <param name="size">The original source size.</param>
    /// <returns>The <see cref="Size"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SizeF GetRawTransformedSize(Matrix4x4 matrix, Size size)
        => GetTransformedSize(matrix, size, false);

    /// <summary>
    /// Returns the size of the transformed source. When <paramref name="preserveCanvas"/> is true,
    /// the size is expanded to include translation so the full moved content remains visible.
    /// </summary>
    /// <param name="matrix">The transformation matrix.</param>
    /// <param name="size">The original source size.</param>
    /// <param name="preserveCanvas">
    /// If <see langword="true"/>, expand the size to account for translation (left/up as well as right/down).
    /// If <see langword="false"/>, return only the transformed span without translation expansion.
    /// </param>
    /// <returns>The <see cref="SizeF"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SizeF GetTransformedSize(Matrix4x4 matrix, Size size, bool preserveCanvas)
    {
        Guard.IsTrue(size.Width > 0 && size.Height > 0, nameof(size), "Source size dimensions cannot be 0!");

        if (matrix.IsIdentity || matrix.Equals(default))
        {
            return size;
        }

        if (TryGetTransformedRectangle(new RectangleF(Point.Empty, size), matrix, out RectangleF bounds))
        {
            return preserveCanvas ? GetPreserveCanvasSize(bounds) : bounds.Size;
        }

        return size;
    }

    /// <summary>
    /// Attempts to derive a 4x4 projective transform matrix that approximates the behavior of an <typeparamref name="T"/>.
    /// </summary>
    /// <param name="swizzler">
    /// The swizzler to use for the transformation.
    /// </param>
    /// <param name="sourceRectangle">
    /// The source rectangle that defines the area to be transformed.
    /// </param>
    /// <typeparam name="T">
    /// The type of the swizzler, which must implement <see cref="ISwizzler"/>.
    /// </typeparam>
    public static Matrix4x4 GetSwizzlerMatrix<T>(T swizzler, Rectangle sourceRectangle)
        where T : struct, ISwizzler
        => CreateQuadDistortionMatrix(
            sourceRectangle,
            swizzler.Transform(new Point(sourceRectangle.Left, sourceRectangle.Top)),
            swizzler.Transform(new Point(sourceRectangle.Right, sourceRectangle.Top)),
            swizzler.Transform(new Point(sourceRectangle.Right, sourceRectangle.Bottom)),
            swizzler.Transform(new Point(sourceRectangle.Left, sourceRectangle.Bottom)));

    /// <summary>
    /// Returns the size relative to the source for the given transformation matrix.
    /// </summary>
    /// <param name="matrix">The transformation matrix.</param>
    /// <param name="size">The original source size.</param>
    /// <returns>The <see cref="Size"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SizeF GetRawTransformedSize(Matrix3x2 matrix, Size size)
        => GetTransformedSize(matrix, size, false);

    /// <summary>
    /// Returns the size of the transformed source. When <paramref name="preserveCanvas"/> is true,
    /// the size is expanded to include translation so the full moved content remains visible.
    /// </summary>
    /// <param name="matrix">The transformation matrix.</param>
    /// <param name="size">The original source size.</param>
    /// <param name="preserveCanvas">
    /// If <see langword="true"/>, expand the size to account for translation (left/up as well as right/down).
    /// If <see langword="false"/>, return only the transformed span without translation expansion.
    /// </param>
    /// <returns>The <see cref="SizeF"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SizeF GetTransformedSize(Matrix3x2 matrix, Size size, bool preserveCanvas)
    {
        Guard.IsTrue(size.Width > 0 && size.Height > 0, nameof(size), "Source size dimensions cannot be 0!");

        if (matrix.IsIdentity || matrix.Equals(default))
        {
            return size;
        }

        if (TryGetTransformedRectangle(new RectangleF(Point.Empty, size), matrix, out RectangleF bounds))
        {
            return preserveCanvas ? GetPreserveCanvasSize(bounds) : bounds.Size;
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryGetTransformedRectangle(RectangleF rectangle, Matrix3x2 matrix, out RectangleF bounds)
    {
        if (matrix.IsIdentity || rectangle.Equals(default))
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
    internal static bool TryGetTransformedRectangle(RectangleF rectangle, Matrix4x4 matrix, out RectangleF bounds)
    {
        if (matrix.IsIdentity || rectangle.Equals(default))
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

    /// <summary>
    /// Calculates the size of a destination canvas large enough to contain the full
    /// transformed content of a source rectangle while preserving any translation offsets.
    /// </summary>
    /// <param name="rectangle">
    /// The <see cref="RectangleF"/> representing the transformed bounds of the source content
    /// in destination (output) space.
    /// </param>
    /// <returns>
    /// A <see cref="SizeF"/> that describes the canvas dimensions required to fully
    /// contain the transformed content while accounting for any positive or negative translation.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method expands the output canvas to ensure that translated content remains visible.
    /// </para>
    /// <para>
    /// If the transformation produces a positive translation, the method extends the canvas
    /// on the positive side (right or bottom).
    /// If the transformation produces a negative translation (the content moves left or up),
    /// the method extends the canvas on the negative side to include that offset.
    /// </para>
    /// <para>
    /// The result is equivalent to taking the union of:
    /// <list type="bullet">
    /// <item>
    /// <description>The original, untransformed rectangle at the origin [0..Width] × [0..Height].</description>
    /// </item>
    /// <item>
    /// <description>The translated rectangle defined by <paramref name="rectangle"/>.</description>
    /// </item>
    /// </list>
    /// This ensures the entire translated image fits within the resulting canvas,
    /// without trimming any portion caused by translation.
    /// </para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SizeF GetPreserveCanvasSize(RectangleF rectangle)
    {
        // Compute the required height.
        // If the top is negative, expand upward by that amount (rectangle.Bottom already includes height).
        // Otherwise, take the larger of the transformed height or the bottom offset.
        float height = rectangle.Top < 0
            ? rectangle.Bottom
            : MathF.Max(rectangle.Height, rectangle.Bottom);

        // Compute the required width.
        // If the left is negative, expand leftward by that amount (rectangle.Right already includes width).
        // Otherwise, take the larger of the transformed width or the right offset.
        float width = rectangle.Left < 0
            ? rectangle.Right
            : MathF.Max(rectangle.Width, rectangle.Right);

        // Guard: if translation exceeds or cancels dimensions,
        // ensure non-zero positive size using the base rectangle dimensions.
        if (height <= 0)
        {
            height = rectangle.Height;
        }

        if (width <= 0)
        {
            width = rectangle.Width;
        }

        // Return the final size that preserves the full visible region of the transformed content.
        return new SizeF(width, height);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static RectangleF GetBoundingRectangle(Vector2 tl, Vector2 tr, Vector2 bl, Vector2 br)
    {
        float left = MathF.Min(tl.X, MathF.Min(tr.X, MathF.Min(bl.X, br.X)));
        float top = MathF.Min(tl.Y, MathF.Min(tr.Y, MathF.Min(bl.Y, br.Y)));
        float right = MathF.Max(tl.X, MathF.Max(tr.X, MathF.Max(bl.X, br.X)));
        float bottom = MathF.Max(tl.Y, MathF.Max(tr.Y, MathF.Max(bl.Y, br.Y)));

        return RectangleF.FromLTRB(left, top, right, bottom);
    }

    /// <summary>
    /// Normalizes an affine 2D matrix so that it operates in pixel space.
    /// Applies the row-vector conjugation <c>T(+0.5,+0.5) * M * T(-0.5,-0.5)</c>
    /// to align the transform with pixel centers.
    /// </summary>
    /// <param name="matrix">The affine matrix.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix3x2 NormalizeToPixel(Matrix3x2 matrix)
    {
        const float dx = 0.5f, dy = 0.5f;

        matrix.M31 += (-dx) + ((dx * matrix.M11) + (dy * matrix.M21));
        matrix.M32 += (-dy) + ((dx * matrix.M12) + (dy * matrix.M22));
        return matrix;
    }

    /// <summary>
    /// Normalizes a projective 4×4 matrix so that it operates in pixel space.
    /// Applies the row-vector conjugation <c>T(+0.5,+0.5,0) * M * T(-0.5,-0.5,0)</c>
    /// to align the transform with pixel centers.
    /// </summary>
    /// <param name="matrix">The projective matrix.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix4x4 NormalizeToPixel(Matrix4x4 matrix)
    {
        const float dx = 0.5f, dy = 0.5f;

        // Fast path: affine (no perspective)
        if (matrix.M14 == 0f && matrix.M24 == 0f && matrix.M34 == 0f && matrix.M44 == 1f)
        {
            // t' = t + (-d + d·L)
            matrix.M41 += (-dx) + ((dx * matrix.M11) + (dy * matrix.M21));
            matrix.M42 += (-dy) + ((dx * matrix.M12) + (dy * matrix.M22));
            return matrix;
        }

        Matrix4x4 tPos = Matrix4x4.Identity;
        tPos.M41 = dx;
        tPos.M42 = dy;
        Matrix4x4 tNeg = Matrix4x4.Identity;
        tNeg.M41 = -dx;
        tNeg.M42 = -dy;
        return tPos * matrix * tNeg;
    }
}
