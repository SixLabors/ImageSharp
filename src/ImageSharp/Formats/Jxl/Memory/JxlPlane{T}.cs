// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Processing.Image;

namespace SixLabors.ImageSharp.Formats.Jxl.Memory;

/// <summary>
/// A generic version of a 2D single-plane JPEG XL image.
/// </summary>
/// <typeparam name="T">The type of each pixel.</typeparam>
internal class JxlPlane<T> : JxlPlaneBase
    where T : unmanaged
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JxlPlane{T}"/> class.
    /// </summary>
    public JxlPlane()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlPlane{T}"/> class with the specified width and height.
    /// </summary>
    /// <param name="width">Plane width.</param>
    /// <param name="height">Plane height</param>
    public unsafe JxlPlane(int width, int height)
        : base(width, height, sizeof(T))
    {
    }

    /// <summary>
    /// Gets the number of pixels per row.
    /// </summary>
    public unsafe int PixelsPerRow => this.BytesPerRow / sizeof(T);

    /// <summary>
    /// Allocates a new plane.
    /// </summary>
    /// <param name="configuration">The configuration which contains a memory allocator.</param>
    /// <param name="xSize">Plane width</param>
    /// <param name="ySize">Plane height</param>
    /// <param name="prePadding">Padding</param>
    /// <returns>A new allocated plane</returns>
    /// <exception cref="InvalidOperationException">Thrown when allocation fails.</exception>
    public static JxlPlane<T> Create(Configuration configuration, int xSize, int ySize, int prePadding = 0)
    {
        JxlPlane<T> plane = new(xSize, ySize);

        bool allocated = plane.Allocate(configuration, prePadding);

        if (!allocated)
        {
            throw new InvalidOperationException("Failed to allocate a JPEG XL plane");
        }

        return plane;
    }

    /// <summary>
    /// Returns a span for the specified row.
    /// </summary>
    /// <param name="y">The row index.</param>
    /// <returns>A span which covers memory for the specified row.</returns>
    public Span<T> GetRow(int y) => this.GetRowBase<T>(y);

    /// <summary>
    /// Returns a span for the specified row, but backwards by specified number of elements.
    /// </summary>
    /// <param name="y">The row index.</param>
    /// <param name="minus">Once the offset of the row was derived, this is how much to subtract the offset.</param>
    /// <returns>A span which covers memory for the specified row.</returns>
    public Span<T> GetRowMinus(int y, int minus) => this.GetRowMinusBase<T>(y, minus);

    /// <summary>
    /// Returns a span for the specified row, but with offset by specified number of elements.
    /// </summary>
    /// <param name="y">The row index.</param>
    /// <param name="plus">Once the offset of the row was derived, this is the additional offset.</param>
    /// <returns>A span which covers memory for the specified row.</returns>
    public Span<T> GetRowPlus(int y, int plus) => this.GetRowMinusBase<T>(y, plus);

    /// <summary>
    /// Returns a span for the specified row within the specified rectangle bounds.
    /// </summary>
    /// <param name="rectangle">The bounds.</param>
    /// <param name="y">The row index.</param>
    /// <returns>A span which covers memory for the specified row with the rectangle offsets.</returns>
    public Span<T> GetRow(Rectangle rectangle, int y)
    {
        DebugGuard.MustBeGreaterThanOrEqualTo(y + rectangle.Top, 0, nameof(y));

        return this.GetRow(y + rectangle.Top)[rectangle.Left..];
    }

    /// <summary>
    /// Checks if the specified rectangle is within the bounds image.
    /// </summary>
    /// <param name="rectangle">The input rectangle.</param>
    /// <returns>Boolean indicating whether the rectangle is inside.</returns>
    public bool IsRectangleInside(Rectangle rectangle) => rectangle.Contains(this.GetRectangle());

    /// <summary>
    /// Returns the rectangle for this image bounds.
    /// </summary>
    /// <returns>A rectangle with x,y=0,0 width,height=XSize,YSize.</returns>
    public Rectangle GetRectangle() => new(0, 0, this.XSize, this.YSize);

    /// <summary>
    /// Fills everything in this image with 0.
    /// </summary>
    public void Clear() => JxlImageOperations.ZeroFillImage(this);
}
