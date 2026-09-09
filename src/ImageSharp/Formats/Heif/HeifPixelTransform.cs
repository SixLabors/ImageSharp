// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Maps cropped source pixels to their rotated and mirrored destination coordinates.
/// </summary>
internal readonly struct HeifPixelTransform
{
    private readonly Matrix3x2 orientation;

    /// <summary>
    /// Initializes a new instance of the <see cref="HeifPixelTransform"/> struct.
    /// </summary>
    /// <param name="rotation">The counter-clockwise quarter-turn count.</param>
    /// <param name="mirrorAxis">The optional destination mirror axis.</param>
    public HeifPixelTransform(byte rotation, byte? mirrorAxis)
    {
        this.orientation = Matrix3x2.CreateRotation(-rotation * MathF.PI / 2);
        this.orientation *= Matrix3x2.CreateScale(mirrorAxis == 1 ? -1 : 1, mirrorAxis == 0 ? -1 : 1);
    }

    /// <summary>
    /// Gets a value indicating whether source rows map directly to destination rows.
    /// </summary>
    public bool IsIdentity => this.orientation == default || this.orientation.IsIdentity;

    /// <summary>
    /// Gets the destination extent for a cropped source extent.
    /// </summary>
    /// <param name="sourceSize">The cropped source extent.</param>
    /// <returns>The destination extent.</returns>
    public Size GetDestinationSize(Size sourceSize)
        => Rectangle.Round(Rectangle.Transform(new Rectangle(Point.Empty, sourceSize), this.GetMatrix(sourceSize))).Size;

    /// <summary>
    /// Finds the source rectangle corresponding to a destination window.
    /// </summary>
    /// <param name="destination">The window in transformed coordinates.</param>
    /// <param name="sourceSize">The complete cropped source extent.</param>
    /// <returns>The source window before rotation and mirroring.</returns>
    public Rectangle GetSourceRectangle(Rectangle destination, Size sourceSize)
    {
        Matrix3x2.Invert(this.GetMatrix(sourceSize), out Matrix3x2 inverse);
        return Rectangle.Round(Rectangle.Transform(destination, inverse));
    }

    /// <summary>
    /// Maps one source pixel to the destination.
    /// </summary>
    /// <param name="x">The source column relative to the crop.</param>
    /// <param name="y">The source row relative to the crop.</param>
    /// <param name="matrix">The matrix resolved once for the source region.</param>
    /// <returns>The destination pixel coordinate.</returns>
    public static Point Transform(int x, int y, Matrix3x2 matrix)
    {
        Vector2 point = Vector2.Transform(new Vector2(x + 0.5F, y + 0.5F), matrix);
        return new Point((int)MathF.Floor(point.X), (int)MathF.Floor(point.Y));
    }

    /// <summary>
    /// Maps a source rectangle to its exact destination rectangle.
    /// </summary>
    /// <param name="source">The rectangle relative to the cropped source.</param>
    /// <param name="sourceSize">The complete cropped source extent.</param>
    /// <returns>The destination rectangle.</returns>
    public Rectangle TransformRectangle(Rectangle source, Size sourceSize)
    {
        return Rectangle.Round(Rectangle.Transform(source, this.GetMatrix(sourceSize)));
    }

    /// <summary>
    /// Writes a converted source row into its destination coordinates.
    /// </summary>
    /// <typeparam name="TPixel">The packed pixel type.</typeparam>
    /// <param name="row">The converted source row.</param>
    /// <param name="y">The source row index relative to the crop.</param>
    /// <param name="matrix">The matrix resolved once for the source region.</param>
    /// <param name="destination">The exact destination region.</param>
    public static void WriteRow<TPixel>(ReadOnlySpan<TPixel> row, int y, Matrix3x2 matrix, Buffer2DRegion<TPixel> destination)
        where TPixel : unmanaged
    {
        // The matrix's first basis vector is the integer destination step for one source column.
        // Quarter turns and mirrors require no matrix operations inside the pixel loop.
        Point first = Transform(0, y, matrix);
        int stepX = (int)matrix.M11;
        int stepY = (int)matrix.M12;

        for (int x = 0; x < row.Length; x++)
        {
            destination.DangerousGetRowSpan(first.Y)[first.X] = row[x];
            first.X += stepX;
            first.Y += stepY;
        }
    }

    /// <summary>
    /// Gets the transform translated into the positive destination bounds.
    /// </summary>
    /// <param name="sourceSize">The source extent.</param>
    /// <returns>The transform of rectangle edges and pixel centers.</returns>
    public Matrix3x2 GetMatrix(Size sourceSize)
    {
        Matrix3x2 matrix = this.IsIdentity ? Matrix3x2.Identity : this.orientation;
        RectangleF bounds = Rectangle.Transform(new Rectangle(Point.Empty, sourceSize), matrix);
        matrix.Translation = new Vector2(-bounds.X, -bounds.Y);
        return matrix;
    }
}
