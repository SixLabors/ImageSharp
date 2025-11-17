// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Processing.Processors.Transforms;

namespace SixLabors.ImageSharp.Processing;

/// <summary>
/// A helper class for constructing <see cref="Matrix3x2"/> instances for use in affine transforms.
/// </summary>
public class AffineTransformBuilder
{
    private readonly List<Func<Size, Matrix3x2>> transformMatrixFactories = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="AffineTransformBuilder"/> class.
    /// </summary>
    public AffineTransformBuilder()
    {
    }

    /// <summary>
    /// Prepends a rotation matrix using the given rotation angle in degrees
    /// and the image center point as rotation center.
    /// </summary>
    /// <param name="degrees">The amount of rotation, in degrees.</param>
    /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
    public AffineTransformBuilder PrependRotationDegrees(float degrees)
        => this.PrependRotationRadians(GeometryUtilities.DegreeToRadian(degrees));

    /// <summary>
    /// Prepends a rotation matrix using the given rotation angle in radians
    /// and the image center point as rotation center.
    /// </summary>
    /// <param name="radians">The amount of rotation, in radians.</param>
    /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
    public AffineTransformBuilder PrependRotationRadians(float radians)
        => this.Prepend(
            size => TransformUtilities.CreateRotationTransformMatrixRadians(radians, size));

    /// <summary>
    /// Prepends a rotation matrix using the given rotation in degrees at the given origin.
    /// </summary>
    /// <param name="degrees">The amount of rotation, in degrees.</param>
    /// <param name="origin">The rotation origin point.</param>
    /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
    public AffineTransformBuilder PrependRotationDegrees(float degrees, Vector2 origin)
        => this.PrependRotationRadians(GeometryUtilities.DegreeToRadian(degrees), origin);

    /// <summary>
    /// Prepends a rotation matrix using the given rotation in radians at the given origin.
    /// </summary>
    /// <param name="radians">The amount of rotation, in radians.</param>
    /// <param name="origin">The rotation origin point.</param>
    /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
    public AffineTransformBuilder PrependRotationRadians(float radians, Vector2 origin)
        => this.PrependMatrix(Matrix3x2.CreateRotation(radians, origin));

    /// <summary>
    /// Appends a rotation matrix using the given rotation angle in degrees
    /// and the image center point as rotation center.
    /// </summary>
    /// <param name="degrees">The amount of rotation, in degrees.</param>
    /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
    public AffineTransformBuilder AppendRotationDegrees(float degrees)
        => this.AppendRotationRadians(GeometryUtilities.DegreeToRadian(degrees));

    /// <summary>
    /// Appends a rotation matrix using the given rotation angle in radians
    /// and the image center point as rotation center.
    /// </summary>
    /// <param name="radians">The amount of rotation, in radians.</param>
    /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
    public AffineTransformBuilder AppendRotationRadians(float radians)
        => this.Append(size => TransformUtilities.CreateRotationTransformMatrixRadians(radians, size));

    /// <summary>
    /// Appends a rotation matrix using the given rotation in degrees at the given origin.
    /// </summary>
    /// <param name="degrees">The amount of rotation, in degrees.</param>
    /// <param name="origin">The rotation origin point.</param>
    /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
    public AffineTransformBuilder AppendRotationDegrees(float degrees, Vector2 origin)
        => this.AppendRotationRadians(GeometryUtilities.DegreeToRadian(degrees), origin);

    /// <summary>
    /// Appends a rotation matrix using the given rotation in radians at the given origin.
    /// </summary>
    /// <param name="radians">The amount of rotation, in radians.</param>
    /// <param name="origin">The rotation origin point.</param>
    /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
    public AffineTransformBuilder AppendRotationRadians(float radians, Vector2 origin)
        => this.AppendMatrix(Matrix3x2.CreateRotation(radians, origin));

    /// <summary>
    /// Prepends a scale matrix from the given uniform scale.
    /// </summary>
    /// <param name="scale">The uniform scale.</param>
    /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
    public AffineTransformBuilder PrependScale(float scale)
        => this.PrependMatrix(Matrix3x2.CreateScale(scale));

    /// <summary>
    /// Prepends a scale matrix from the given vector scale.
    /// </summary>
    /// <param name="scale">The horizontal and vertical scale.</param>
    /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
    public AffineTransformBuilder PrependScale(SizeF scale)
        => this.PrependScale((Vector2)scale);

    /// <summary>
    /// Prepends a scale matrix from the given vector scale.
    /// </summary>
    /// <param name="scales">The horizontal and vertical scale.</param>
    /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
    public AffineTransformBuilder PrependScale(Vector2 scales)
        => this.PrependMatrix(Matrix3x2.CreateScale(scales));

    /// <summary>
    /// Appends a scale matrix from the given uniform scale.
    /// </summary>
    /// <param name="scale">The uniform scale.</param>
    /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
    public AffineTransformBuilder AppendScale(float scale)
        => this.AppendMatrix(Matrix3x2.CreateScale(scale));

    /// <summary>
    /// Appends a scale matrix from the given vector scale.
    /// </summary>
    /// <param name="scales">The horizontal and vertical scale.</param>
    /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
    public AffineTransformBuilder AppendScale(SizeF scales)
        => this.AppendScale((Vector2)scales);

    /// <summary>
    /// Appends a scale matrix from the given vector scale.
    /// </summary>
    /// <param name="scales">The horizontal and vertical scale.</param>
    /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
    public AffineTransformBuilder AppendScale(Vector2 scales)
        => this.AppendMatrix(Matrix3x2.CreateScale(scales));

    /// <summary>
    /// Prepends a centered skew matrix from the give angles in degrees.
    /// </summary>
    /// <param name="degreesX">The X angle, in degrees.</param>
    /// <param name="degreesY">The Y angle, in degrees.</param>
    /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
    public AffineTransformBuilder PrependSkewDegrees(float degreesX, float degreesY)
        => this.PrependSkewRadians(GeometryUtilities.DegreeToRadian(degreesX), GeometryUtilities.DegreeToRadian(degreesY));

    /// <summary>
    /// Prepends a centered skew matrix from the give angles in radians.
    /// </summary>
    /// <param name="radiansX">The X angle, in radians.</param>
    /// <param name="radiansY">The Y angle, in radians.</param>
    /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
    public AffineTransformBuilder PrependSkewRadians(float radiansX, float radiansY)
        => this.Prepend(size => TransformUtilities.CreateSkewTransformMatrixRadians(radiansX, radiansY, size));

    /// <summary>
    /// Prepends a skew matrix using the given angles in degrees at the given origin.
    /// </summary>
    /// <param name="degreesX">The X angle, in degrees.</param>
    /// <param name="degreesY">The Y angle, in degrees.</param>
    /// <param name="origin">The skew origin point.</param>
    /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
    public AffineTransformBuilder PrependSkewDegrees(float degreesX, float degreesY, Vector2 origin)
        => this.PrependSkewRadians(GeometryUtilities.DegreeToRadian(degreesX), GeometryUtilities.DegreeToRadian(degreesY), origin);

    /// <summary>
    /// Prepends a skew matrix using the given angles in radians at the given origin.
    /// </summary>
    /// <param name="radiansX">The X angle, in radians.</param>
    /// <param name="radiansY">The Y angle, in radians.</param>
    /// <param name="origin">The skew origin point.</param>
    /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
    public AffineTransformBuilder PrependSkewRadians(float radiansX, float radiansY, Vector2 origin)
        => this.PrependMatrix(Matrix3x2.CreateSkew(radiansX, radiansY, origin));

    /// <summary>
    /// Appends a centered skew matrix from the give angles in degrees.
    /// </summary>
    /// <param name="degreesX">The X angle, in degrees.</param>
    /// <param name="degreesY">The Y angle, in degrees.</param>
    /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
    public AffineTransformBuilder AppendSkewDegrees(float degreesX, float degreesY)
        => this.AppendSkewRadians(GeometryUtilities.DegreeToRadian(degreesX), GeometryUtilities.DegreeToRadian(degreesY));

    /// <summary>
    /// Appends a centered skew matrix from the give angles in radians.
    /// </summary>
    /// <param name="radiansX">The X angle, in radians.</param>
    /// <param name="radiansY">The Y angle, in radians.</param>
    /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
    public AffineTransformBuilder AppendSkewRadians(float radiansX, float radiansY)
        => this.Append(size => TransformUtilities.CreateSkewTransformMatrixRadians(radiansX, radiansY, size));

    /// <summary>
    /// Appends a skew matrix using the given angles in degrees at the given origin.
    /// </summary>
    /// <param name="degreesX">The X angle, in degrees.</param>
    /// <param name="degreesY">The Y angle, in degrees.</param>
    /// <param name="origin">The skew origin point.</param>
    /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
    public AffineTransformBuilder AppendSkewDegrees(float degreesX, float degreesY, Vector2 origin)
        => this.AppendSkewRadians(GeometryUtilities.DegreeToRadian(degreesX), GeometryUtilities.DegreeToRadian(degreesY), origin);

    /// <summary>
    /// Appends a skew matrix using the given angles in radians at the given origin.
    /// </summary>
    /// <param name="radiansX">The X angle, in radians.</param>
    /// <param name="radiansY">The Y angle, in radians.</param>
    /// <param name="origin">The skew origin point.</param>
    /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
    public AffineTransformBuilder AppendSkewRadians(float radiansX, float radiansY, Vector2 origin)
        => this.AppendMatrix(Matrix3x2.CreateSkew(radiansX, radiansY, origin));

    /// <summary>
    /// Prepends a translation matrix from the given vector.
    /// </summary>
    /// <param name="position">The translation position.</param>
    /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
    public AffineTransformBuilder PrependTranslation(PointF position)
        => this.PrependTranslation((Vector2)position);

    /// <summary>
    /// Prepends a translation matrix from the given vector.
    /// </summary>
    /// <param name="position">The translation position.</param>
    /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
    public AffineTransformBuilder PrependTranslation(Vector2 position)
        => this.PrependMatrix(Matrix3x2.CreateTranslation(position));

    /// <summary>
    /// Appends a translation matrix from the given vector.
    /// </summary>
    /// <param name="position">The translation position.</param>
    /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
    public AffineTransformBuilder AppendTranslation(PointF position)
        => this.AppendTranslation((Vector2)position);

    /// <summary>
    /// Appends a translation matrix from the given vector.
    /// </summary>
    /// <param name="position">The translation position.</param>
    /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
    public AffineTransformBuilder AppendTranslation(Vector2 position)
        => this.AppendMatrix(Matrix3x2.CreateTranslation(position));

    /// <summary>
    /// Prepends a raw matrix.
    /// </summary>
    /// <param name="matrix">The matrix to prepend.</param>
    /// <exception cref="DegenerateTransformException">
    /// The resultant matrix is degenerate containing one or more values equivalent
    /// to <see cref="float.NaN"/> or a zero determinant and therefore cannot be used
    /// for linear transforms.
    /// </exception>
    /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
    public AffineTransformBuilder PrependMatrix(Matrix3x2 matrix)
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
    /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
    public AffineTransformBuilder AppendMatrix(Matrix3x2 matrix)
    {
        CheckDegenerate(matrix);
        return this.Append(_ => matrix);
    }

    /// <summary>
    /// Returns the combined matrix for a given source size.
    /// </summary>
    /// <param name="sourceSize">The source image size.</param>
    /// <returns>The <see cref="Matrix3x2"/>.</returns>
    public Matrix3x2 BuildMatrix(Size sourceSize)
        => this.BuildMatrix(new Rectangle(Point.Empty, sourceSize));

    /// <summary>
    /// Returns the combined transform matrix for a given source rectangle.
    /// </summary>
    /// <param name="sourceRectangle">The rectangle in the source image.</param>
    /// <exception cref="DegenerateTransformException">
    /// The resultant matrix is degenerate containing one or more values equivalent
    /// to <see cref="float.NaN"/> or a zero determinant and therefore cannot be used
    /// for linear transforms.
    /// </exception>
    /// <returns>The <see cref="Matrix3x2"/>.</returns>
    public Matrix3x2 BuildMatrix(Rectangle sourceRectangle)
    {
        Guard.MustBeGreaterThan(sourceRectangle.Width, 0, nameof(sourceRectangle));
        Guard.MustBeGreaterThan(sourceRectangle.Height, 0, nameof(sourceRectangle));

        // Translate the origin matrix to cater for source rectangle offsets.
        Matrix3x2 matrix = Matrix3x2.CreateTranslation(-sourceRectangle.Location);

        Size size = sourceRectangle.Size;

        foreach (Func<Size, Matrix3x2> factory in this.transformMatrixFactories)
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
    public SizeF GetTransformedSize(Rectangle sourceRectangle)
    {
        Matrix3x2 matrix = this.BuildMatrix(sourceRectangle);
        return GetTransformedSize(sourceRectangle, matrix);
    }

    /// <summary>
    /// Returns the size of a rectangle large enough to contain the transformed source rectangle.
    /// </summary>
    /// <param name="sourceRectangle">The rectangle in the source image.</param>
    /// <param name="matrix">The transformation matrix.</param>
    /// <exception cref="DegenerateTransformException">
    /// The resultant matrix is degenerate containing one or more values equivalent
    /// to <see cref="float.NaN"/> or a zero determinant and therefore cannot be used
    /// for linear transforms.
    /// </exception>
    /// <returns>The <see cref="Size"/>.</returns>
    internal static SizeF GetTransformedSize(Rectangle sourceRectangle, Matrix3x2 matrix)
        => TransformUtilities.GetRawTransformedSize(matrix, sourceRectangle.Size);

    private static void CheckDegenerate(Matrix3x2 matrix)
    {
        if (TransformUtilities.IsDegenerate(matrix))
        {
            throw new DegenerateTransformException("Matrix is degenerate. Check input values.");
        }
    }

    private AffineTransformBuilder Prepend(Func<Size, Matrix3x2> transformFactory)
    {
        this.transformMatrixFactories.Insert(0, transformFactory);
        return this;
    }

    private AffineTransformBuilder Append(Func<Size, Matrix3x2> transformFactory)
    {
        this.transformMatrixFactories.Add(transformFactory);
        return this;
    }
}
