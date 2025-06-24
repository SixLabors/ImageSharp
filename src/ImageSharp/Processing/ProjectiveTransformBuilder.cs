// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Processing.Processors.Transforms;

namespace SixLabors.ImageSharp.Processing;

/// <summary>
/// A helper class for constructing <see cref="Matrix4x4"/> instances for use in projective transforms.
/// </summary>
public class ProjectiveTransformBuilder
{
    private readonly List<Func<Size, Matrix4x4>> transformMatrixFactories = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectiveTransformBuilder"/> class.
    /// </summary>
    public ProjectiveTransformBuilder()
        : this(TransformSpace.Pixel)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectiveTransformBuilder"/> class.
    /// </summary>
    /// <param name="transformSpace">
    /// The <see cref="TransformSpace"/> to use when applying the projective transform.
    /// </param>
    public ProjectiveTransformBuilder(TransformSpace transformSpace)
        => this.TransformSpace = transformSpace;

    /// <summary>
    /// Gets the <see cref="TransformSpace"/> to use when applying the projective transform.
    /// </summary>
    public TransformSpace TransformSpace { get; }

    /// <summary>
    /// Prepends a matrix that performs a tapering projective transform.
    /// </summary>
    /// <param name="side">An enumeration that indicates the side of the rectangle that tapers.</param>
    /// <param name="corner">An enumeration that indicates on which corners to taper the rectangle.</param>
    /// <param name="fraction">The amount to taper.</param>
    /// <returns>The <see cref="ProjectiveTransformBuilder"/>.</returns>
    public ProjectiveTransformBuilder PrependTaper(TaperSide side, TaperCorner corner, float fraction)
        => this.Prepend(size => TransformUtils.CreateTaperMatrix(size, side, corner, fraction));

    /// <summary>
    /// Appends a matrix that performs a tapering projective transform.
    /// </summary>
    /// <param name="side">An enumeration that indicates the side of the rectangle that tapers.</param>
    /// <param name="corner">An enumeration that indicates on which corners to taper the rectangle.</param>
    /// <param name="fraction">The amount to taper.</param>
    /// <returns>The <see cref="ProjectiveTransformBuilder"/>.</returns>
    public ProjectiveTransformBuilder AppendTaper(TaperSide side, TaperCorner corner, float fraction)
        => this.Append(size => TransformUtils.CreateTaperMatrix(size, side, corner, fraction));

    /// <summary>
    /// Prepends a centered rotation matrix using the given rotation in degrees.
    /// </summary>
    /// <param name="degrees">The amount of rotation, in degrees.</param>
    /// <returns>The <see cref="ProjectiveTransformBuilder"/>.</returns>
    public ProjectiveTransformBuilder PrependRotationDegrees(float degrees)
        => this.PrependRotationRadians(GeometryUtilities.DegreeToRadian(degrees));

    /// <summary>
    /// Prepends a centered rotation matrix using the given rotation in radians.
    /// </summary>
    /// <param name="radians">The amount of rotation, in radians.</param>
    /// <returns>The <see cref="ProjectiveTransformBuilder"/>.</returns>
    public ProjectiveTransformBuilder PrependRotationRadians(float radians)
        => this.Prepend(size => new(TransformUtils.CreateRotationTransformMatrixRadians(radians, size, this.TransformSpace)));

    /// <summary>
    /// Prepends a centered rotation matrix using the given rotation in degrees at the given origin.
    /// </summary>
    /// <param name="degrees">The amount of rotation, in radians.</param>
    /// <param name="origin">The rotation origin point.</param>
    /// <returns>The <see cref="ProjectiveTransformBuilder"/>.</returns>
    internal ProjectiveTransformBuilder PrependRotationDegrees(float degrees, Vector2 origin)
        => this.PrependRotationRadians(GeometryUtilities.DegreeToRadian(degrees), origin);

    /// <summary>
    /// Prepends a centered rotation matrix using the given rotation in radians at the given origin.
    /// </summary>
    /// <param name="radians">The amount of rotation, in radians.</param>
    /// <param name="origin">The rotation origin point.</param>
    /// <returns>The <see cref="ProjectiveTransformBuilder"/>.</returns>
    internal ProjectiveTransformBuilder PrependRotationRadians(float radians, Vector2 origin)
        => this.PrependMatrix(Matrix4x4.CreateRotationZ(radians, new(origin, 0)));

    /// <summary>
    /// Appends a centered rotation matrix using the given rotation in degrees.
    /// </summary>
    /// <param name="degrees">The amount of rotation, in degrees.</param>
    /// <returns>The <see cref="ProjectiveTransformBuilder"/>.</returns>
    public ProjectiveTransformBuilder AppendRotationDegrees(float degrees)
        => this.AppendRotationRadians(GeometryUtilities.DegreeToRadian(degrees));

    /// <summary>
    /// Appends a centered rotation matrix using the given rotation in radians.
    /// </summary>
    /// <param name="radians">The amount of rotation, in radians.</param>
    /// <returns>The <see cref="ProjectiveTransformBuilder"/>.</returns>
    public ProjectiveTransformBuilder AppendRotationRadians(float radians)
        => this.Append(size => new(TransformUtils.CreateRotationTransformMatrixRadians(radians, size, this.TransformSpace)));

    /// <summary>
    /// Appends a centered rotation matrix using the given rotation in degrees at the given origin.
    /// </summary>
    /// <param name="degrees">The amount of rotation, in radians.</param>
    /// <param name="origin">The rotation origin point.</param>
    /// <returns>The <see cref="ProjectiveTransformBuilder"/>.</returns>
    internal ProjectiveTransformBuilder AppendRotationDegrees(float degrees, Vector2 origin)
        => this.AppendRotationRadians(GeometryUtilities.DegreeToRadian(degrees), origin);

    /// <summary>
    /// Appends a centered rotation matrix using the given rotation in radians at the given origin.
    /// </summary>
    /// <param name="radians">The amount of rotation, in radians.</param>
    /// <param name="origin">The rotation origin point.</param>
    /// <returns>The <see cref="ProjectiveTransformBuilder"/>.</returns>
    internal ProjectiveTransformBuilder AppendRotationRadians(float radians, Vector2 origin)
        => this.AppendMatrix(Matrix4x4.CreateRotationZ(radians, new(origin, 0)));

    /// <summary>
    /// Prepends a scale matrix from the given uniform scale.
    /// </summary>
    /// <param name="scale">The uniform scale.</param>
    /// <returns>The <see cref="ProjectiveTransformBuilder"/>.</returns>
    public ProjectiveTransformBuilder PrependScale(float scale)
        => this.PrependMatrix(Matrix4x4.CreateScale(scale));

    /// <summary>
    /// Prepends a scale matrix from the given vector scale.
    /// </summary>
    /// <param name="scale">The horizontal and vertical scale.</param>
    /// <returns>The <see cref="ProjectiveTransformBuilder"/>.</returns>
    public ProjectiveTransformBuilder PrependScale(SizeF scale)
        => this.PrependScale((Vector2)scale);

    /// <summary>
    /// Prepends a scale matrix from the given vector scale.
    /// </summary>
    /// <param name="scales">The horizontal and vertical scale.</param>
    /// <returns>The <see cref="ProjectiveTransformBuilder"/>.</returns>
    public ProjectiveTransformBuilder PrependScale(Vector2 scales)
        => this.PrependMatrix(Matrix4x4.CreateScale(new Vector3(scales, 1F)));

    /// <summary>
    /// Appends a scale matrix from the given uniform scale.
    /// </summary>
    /// <param name="scale">The uniform scale.</param>
    /// <returns>The <see cref="ProjectiveTransformBuilder"/>.</returns>
    public ProjectiveTransformBuilder AppendScale(float scale)
        => this.AppendMatrix(Matrix4x4.CreateScale(scale));

    /// <summary>
    /// Appends a scale matrix from the given vector scale.
    /// </summary>
    /// <param name="scales">The horizontal and vertical scale.</param>
    /// <returns>The <see cref="ProjectiveTransformBuilder"/>.</returns>
    public ProjectiveTransformBuilder AppendScale(SizeF scales)
        => this.AppendScale((Vector2)scales);

    /// <summary>
    /// Appends a scale matrix from the given vector scale.
    /// </summary>
    /// <param name="scales">The horizontal and vertical scale.</param>
    /// <returns>The <see cref="ProjectiveTransformBuilder"/>.</returns>
    public ProjectiveTransformBuilder AppendScale(Vector2 scales)
        => this.AppendMatrix(Matrix4x4.CreateScale(new Vector3(scales, 1F)));

    /// <summary>
    /// Prepends a centered skew matrix from the give angles in degrees.
    /// </summary>
    /// <param name="degreesX">The X angle, in degrees.</param>
    /// <param name="degreesY">The Y angle, in degrees.</param>
    /// <returns>The <see cref="ProjectiveTransformBuilder"/>.</returns>
    internal ProjectiveTransformBuilder PrependSkewDegrees(float degreesX, float degreesY)
        => this.PrependSkewRadians(GeometryUtilities.DegreeToRadian(degreesX), GeometryUtilities.DegreeToRadian(degreesY));

    /// <summary>
    /// Prepends a centered skew matrix from the give angles in radians.
    /// </summary>
    /// <param name="radiansX">The X angle, in radians.</param>
    /// <param name="radiansY">The Y angle, in radians.</param>
    /// <returns>The <see cref="ProjectiveTransformBuilder"/>.</returns>
    public ProjectiveTransformBuilder PrependSkewRadians(float radiansX, float radiansY)
        => this.Prepend(size => new(TransformUtils.CreateSkewTransformMatrixRadians(radiansX, radiansY, size, this.TransformSpace)));

    /// <summary>
    /// Prepends a skew matrix using the given angles in degrees at the given origin.
    /// </summary>
    /// <param name="degreesX">The X angle, in degrees.</param>
    /// <param name="degreesY">The Y angle, in degrees.</param>
    /// <param name="origin">The skew origin point.</param>
    /// <returns>The <see cref="ProjectiveTransformBuilder"/>.</returns>
    public ProjectiveTransformBuilder PrependSkewDegrees(float degreesX, float degreesY, Vector2 origin)
        => this.PrependSkewRadians(GeometryUtilities.DegreeToRadian(degreesX), GeometryUtilities.DegreeToRadian(degreesY), origin);

    /// <summary>
    /// Prepends a skew matrix using the given angles in radians at the given origin.
    /// </summary>
    /// <param name="radiansX">The X angle, in radians.</param>
    /// <param name="radiansY">The Y angle, in radians.</param>
    /// <param name="origin">The skew origin point.</param>
    /// <returns>The <see cref="ProjectiveTransformBuilder"/>.</returns>
    public ProjectiveTransformBuilder PrependSkewRadians(float radiansX, float radiansY, Vector2 origin)
        => this.PrependMatrix(new(Matrix3x2.CreateSkew(radiansX, radiansY, origin)));

    /// <summary>
    /// Appends a centered skew matrix from the give angles in degrees.
    /// </summary>
    /// <param name="degreesX">The X angle, in degrees.</param>
    /// <param name="degreesY">The Y angle, in degrees.</param>
    /// <returns>The <see cref="ProjectiveTransformBuilder"/>.</returns>
    internal ProjectiveTransformBuilder AppendSkewDegrees(float degreesX, float degreesY)
        => this.AppendSkewRadians(GeometryUtilities.DegreeToRadian(degreesX), GeometryUtilities.DegreeToRadian(degreesY));

    /// <summary>
    /// Appends a centered skew matrix from the give angles in radians.
    /// </summary>
    /// <param name="radiansX">The X angle, in radians.</param>
    /// <param name="radiansY">The Y angle, in radians.</param>
    /// <returns>The <see cref="ProjectiveTransformBuilder"/>.</returns>
    public ProjectiveTransformBuilder AppendSkewRadians(float radiansX, float radiansY)
        => this.Append(size => new(TransformUtils.CreateSkewTransformMatrixRadians(radiansX, radiansY, size, this.TransformSpace)));

    /// <summary>
    /// Appends a skew matrix using the given angles in degrees at the given origin.
    /// </summary>
    /// <param name="degreesX">The X angle, in degrees.</param>
    /// <param name="degreesY">The Y angle, in degrees.</param>
    /// <param name="origin">The skew origin point.</param>
    /// <returns>The <see cref="ProjectiveTransformBuilder"/>.</returns>
    public ProjectiveTransformBuilder AppendSkewDegrees(float degreesX, float degreesY, Vector2 origin)
        => this.AppendSkewRadians(GeometryUtilities.DegreeToRadian(degreesX), GeometryUtilities.DegreeToRadian(degreesY), origin);

    /// <summary>
    /// Appends a skew matrix using the given angles in radians at the given origin.
    /// </summary>
    /// <param name="radiansX">The X angle, in radians.</param>
    /// <param name="radiansY">The Y angle, in radians.</param>
    /// <param name="origin">The skew origin point.</param>
    /// <returns>The <see cref="ProjectiveTransformBuilder"/>.</returns>
    public ProjectiveTransformBuilder AppendSkewRadians(float radiansX, float radiansY, Vector2 origin)
        => this.AppendMatrix(new(Matrix3x2.CreateSkew(radiansX, radiansY, origin)));

    /// <summary>
    /// Prepends a translation matrix from the given vector.
    /// </summary>
    /// <param name="position">The translation position.</param>
    /// <returns>The <see cref="ProjectiveTransformBuilder"/>.</returns>
    public ProjectiveTransformBuilder PrependTranslation(PointF position)
        => this.PrependTranslation((Vector2)position);

    /// <summary>
    /// Prepends a translation matrix from the given vector.
    /// </summary>
    /// <param name="position">The translation position.</param>
    /// <returns>The <see cref="ProjectiveTransformBuilder"/>.</returns>
    public ProjectiveTransformBuilder PrependTranslation(Vector2 position)
        => this.PrependMatrix(Matrix4x4.CreateTranslation(new(position, 0)));

    /// <summary>
    /// Appends a translation matrix from the given vector.
    /// </summary>
    /// <param name="position">The translation position.</param>
    /// <returns>The <see cref="ProjectiveTransformBuilder"/>.</returns>
    public ProjectiveTransformBuilder AppendTranslation(PointF position)
        => this.AppendTranslation((Vector2)position);

    /// <summary>
    /// Appends a translation matrix from the given vector.
    /// </summary>
    /// <param name="position">The translation position.</param>
    /// <returns>The <see cref="ProjectiveTransformBuilder"/>.</returns>
    public ProjectiveTransformBuilder AppendTranslation(Vector2 position)
        => this.AppendMatrix(Matrix4x4.CreateTranslation(new(position, 0)));

    /// <summary>
    /// Prepends a quad distortion matrix using the specified corner points.
    /// </summary>
    /// <param name="topLeft">The top-left corner point of the distorted quad.</param>
    /// <param name="topRight">The top-right corner point of the distorted quad.</param>
    /// <param name="bottomRight">The bottom-right corner point of the distorted quad.</param>
    /// <param name="bottomLeft">The bottom-left corner point of the distorted quad.</param>
    /// <returns>The <see cref="ProjectiveTransformBuilder"/>.</returns>
    public ProjectiveTransformBuilder PrependQuadDistortion(PointF topLeft, PointF topRight, PointF bottomRight, PointF bottomLeft)
        => this.Prepend(size => TransformUtils.CreateQuadDistortionMatrix(
            new(Point.Empty, size), topLeft, topRight, bottomRight, bottomLeft, this.TransformSpace));

    /// <summary>
    /// Appends a quad distortion matrix using the specified corner points.
    /// </summary>
    /// <param name="topLeft">The top-left corner point of the distorted quad.</param>
    /// <param name="topRight">The top-right corner point of the distorted quad.</param>
    /// <param name="bottomRight">The bottom-right corner point of the distorted quad.</param>
    /// <param name="bottomLeft">The bottom-left corner point of the distorted quad.</param>
    /// <returns>The <see cref="ProjectiveTransformBuilder"/>.</returns>
    public ProjectiveTransformBuilder AppendQuadDistortion(PointF topLeft, PointF topRight, PointF bottomRight, PointF bottomLeft)
        => this.Append(size => TransformUtils.CreateQuadDistortionMatrix(
            new(Point.Empty, size), topLeft, topRight, bottomRight, bottomLeft, this.TransformSpace));

    /// <summary>
    /// Prepends a raw matrix.
    /// </summary>
    /// <param name="matrix">The matrix to prepend.</param>
    /// <exception cref="DegenerateTransformException">
    /// The resultant matrix is degenerate containing one or more values equivalent
    /// to <see cref="float.NaN"/> or a zero determinant and therefore cannot be used
    /// for linear transforms.
    /// </exception>
    /// <returns>The <see cref="ProjectiveTransformBuilder"/>.</returns>
    public ProjectiveTransformBuilder PrependMatrix(Matrix4x4 matrix)
    {
        CheckDegenerate(matrix);
        return this.Prepend(_ => matrix);
    }

    /// <summary>
    /// Appends a raw matrix.
    /// </summary>
    /// <param name="matrix">The matrix to append.</param>
    /// <exception cref="DegenerateTransformException">
    /// The resultant matrix is degenerate containing one or more values equivalent
    /// to <see cref="float.NaN"/> or a zero determinant and therefore cannot be used
    /// for linear transforms.
    /// </exception>
    /// <returns>The <see cref="ProjectiveTransformBuilder"/>.</returns>
    public ProjectiveTransformBuilder AppendMatrix(Matrix4x4 matrix)
    {
        CheckDegenerate(matrix);
        return this.Append(_ => matrix);
    }

    /// <summary>
    /// Returns the combined matrix for a given source size.
    /// </summary>
    /// <param name="sourceSize">The source image size.</param>
    /// <returns>The <see cref="Matrix4x4"/>.</returns>
    public Matrix4x4 BuildMatrix(Size sourceSize)
        => this.BuildMatrix(new Rectangle(Point.Empty, sourceSize));

    /// <summary>
    /// Returns the combined matrix for a given source rectangle.
    /// </summary>
    /// <param name="sourceRectangle">The rectangle in the source image.</param>
    /// <exception cref="DegenerateTransformException">
    /// The resultant matrix is degenerate containing one or more values equivalent
    /// to <see cref="float.NaN"/> or a zero determinant and therefore cannot be used
    /// for linear transforms.
    /// </exception>
    /// <returns>The <see cref="Matrix4x4"/>.</returns>
    public Matrix4x4 BuildMatrix(Rectangle sourceRectangle)
    {
        Guard.MustBeGreaterThan(sourceRectangle.Width, 0, nameof(sourceRectangle));
        Guard.MustBeGreaterThan(sourceRectangle.Height, 0, nameof(sourceRectangle));

        // Translate the origin matrix to cater for source rectangle offsets.
        Matrix4x4 matrix = Matrix4x4.CreateTranslation(new(-sourceRectangle.Location, 0));

        Size size = sourceRectangle.Size;

        foreach (Func<Size, Matrix4x4> factory in this.transformMatrixFactories)
        {
            matrix *= factory(size);
        }

        CheckDegenerate(matrix);

        return matrix;
    }

    /// <summary>
    /// Returns the size of a rectangle large enough to contain the transformed source rectangle.
    /// </summary>
    /// <param name="sourceRectangle">The rectangle in the source image.</param>
    /// <exception cref="DegenerateTransformException">
    /// The resultant matrix is degenerate containing one or more values equivalent
    /// to <see cref="float.NaN"/> or a zero determinant and therefore cannot be used
    /// for linear transforms.
    /// </exception>
    /// <returns>The <see cref="Size"/>.</returns>
    public Size GetTransformedSize(Rectangle sourceRectangle)
    {
        Matrix4x4 matrix = this.BuildMatrix(sourceRectangle);
        return TransformUtils.GetTransformedSize(matrix, sourceRectangle.Size, this.TransformSpace);
    }

    private static void CheckDegenerate(Matrix4x4 matrix)
    {
        if (TransformUtils.IsDegenerate(matrix))
        {
            throw new DegenerateTransformException("Matrix is degenerate. Check input values.");
        }
    }

    private ProjectiveTransformBuilder Prepend(Func<Size, Matrix4x4> transformFactory)
    {
        this.transformMatrixFactories.Insert(0, transformFactory);
        return this;
    }

    private ProjectiveTransformBuilder Append(Func<Size, Matrix4x4> transformFactory)
    {
        this.transformMatrixFactories.Add(transformFactory);
        return this;
    }
}
