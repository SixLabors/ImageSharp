// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Memory;

/// <summary>
/// Represents a rectangular region inside a 2D memory buffer (<see cref="Buffer2D{T}"/>).
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
public readonly struct Buffer2DRegion<T>
    where T : unmanaged
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Buffer2DRegion{T}"/> struct.
    /// </summary>
    /// <param name="buffer">The <see cref="Buffer2D{T}"/>.</param>
    /// <param name="bounds">The <see cref="Bounds"/> defining a rectangular area within the buffer.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Buffer2DRegion(Buffer2D<T> buffer, Rectangle bounds)
    {
        DebugGuard.MustBeGreaterThanOrEqualTo(bounds.X, 0, nameof(bounds));
        DebugGuard.MustBeGreaterThanOrEqualTo(bounds.Y, 0, nameof(bounds));
        DebugGuard.MustBeLessThanOrEqualTo(bounds.Width, buffer.Width, nameof(bounds));
        DebugGuard.MustBeLessThanOrEqualTo(bounds.Height, buffer.Height, nameof(bounds));

        this.Buffer = buffer;
        this.Bounds = bounds;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Buffer2DRegion{T}"/> struct.
    /// </summary>
    /// <param name="buffer">The <see cref="Buffer2D{T}"/>.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Buffer2DRegion(Buffer2D<T> buffer)
        : this(buffer, buffer.Bounds)
    {
    }

    /// <summary>
    /// Gets the <see cref="Buffer2D{T}"/> being pointed by this instance.
    /// </summary>
    public Buffer2D<T> Buffer { get; }

    /// <summary>
    /// Gets the width
    /// </summary>
    public int Width => this.Bounds.Width;

    /// <summary>
    /// Gets the height
    /// </summary>
    public int Height => this.Bounds.Height;

    /// <summary>
    /// Gets the number of elements between row starts in <see cref="Buffer"/>.
    /// </summary>
    public int Stride => this.Buffer.RowStride;

    /// <summary>
    /// Gets the size of the area.
    /// </summary>
    public Size Size => this.Bounds.Size;

    /// <summary>
    /// Gets the rectangle specifying the boundaries of the area in <see cref="Buffer"/>.
    /// </summary>
    public Rectangle Bounds { get; }

    /// <summary>
    /// Gets a value indicating whether the area refers to the entire <see cref="Buffer"/>
    /// </summary>
    internal bool IsFullBufferArea => this.Size == this.Buffer.Size;

    /// <summary>
    /// Gets or sets a value at the given index.
    /// </summary>
    /// <param name="x">The position inside a row</param>
    /// <param name="y">The row index</param>
    /// <returns>The reference to the value</returns>
    internal ref T this[int x, int y] => ref this.Buffer[x + this.Bounds.X, y + this.Bounds.Y];

    /// <summary>
    /// Gets a span to row 'y' inside this area.
    /// </summary>
    /// <param name="y">The row index</param>
    /// <returns>The span</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<T> DangerousGetRowSpan(int y)
    {
        int yy = this.Bounds.Y + y;
        int xx = this.Bounds.X;
        int width = this.Bounds.Width;

        return this.Buffer.DangerousGetRowSpan(yy).Slice(xx, width);
    }

    /// <summary>
    /// Returns a subregion as <see cref="Buffer2DRegion{T}"/>. (Similar to <see cref="Span{T}.Slice(int, int)"/>.)
    /// </summary>
    /// <param name="x">The x index at the subregion origin.</param>
    /// <param name="y">The y index at the subregion origin.</param>
    /// <param name="width">The desired width of the subregion.</param>
    /// <param name="height">The desired height of the subregion.</param>
    /// <returns>The subregion</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Buffer2DRegion<T> GetSubRegion(int x, int y, int width, int height)
    {
        Rectangle rectangle = new(x, y, width, height);
        return this.GetSubRegion(rectangle);
    }

    /// <summary>
    /// Returns a subregion as <see cref="Buffer2DRegion{T}"/>. (Similar to <see cref="Span{T}.Slice(int, int)"/>.)
    /// </summary>
    /// <param name="rectangle">The <see cref="Bounds"/> specifying the boundaries of the subregion</param>
    /// <returns>The subregion</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Buffer2DRegion<T> GetSubRegion(Rectangle rectangle)
    {
        DebugGuard.MustBeLessThanOrEqualTo(rectangle.Width, this.Bounds.Width, nameof(rectangle));
        DebugGuard.MustBeLessThanOrEqualTo(rectangle.Height, this.Bounds.Height, nameof(rectangle));

        int x = this.Bounds.X + rectangle.X;
        int y = this.Bounds.Y + rectangle.Y;
        rectangle = new Rectangle(x, y, rectangle.Width, rectangle.Height);
        return new Buffer2DRegion<T>(this.Buffer, rectangle);
    }

    /// <summary>
    /// Gets a reference to the [0,0] element.
    /// </summary>
    /// <returns>The reference to the [0,0] element</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ref T GetReferenceToOrigin()
    {
        int y = this.Bounds.Y;
        int x = this.Bounds.X;
        return ref this.Buffer.DangerousGetRowSpan(y)[x];
    }

    /// <summary>
    /// Clears the contents of this <see cref="Buffer2DRegion{T}"/>.
    /// </summary>
    internal void Clear()
    {
        // Optimization for when the size of the area is the same as the buffer size.
        if (this.IsFullBufferArea && this.Buffer.RowStride == this.Buffer.Width)
        {
            this.Buffer.Clear(default);
            return;
        }

        for (int y = 0; y < this.Bounds.Height; y++)
        {
            Span<T> row = this.DangerousGetRowSpan(y);
            row.Clear();
        }
    }

    /// <summary>
    /// Fills the elements of this <see cref="Buffer2DRegion{T}"/> with the specified value.
    /// </summary>
    /// <param name="value">The value to assign to each element of the region.</param>
    internal void Fill(T value)
    {
        // Optimization for when the size of the area is the same as the buffer size.
        if (this.IsFullBufferArea && this.Buffer.RowStride == this.Buffer.Width)
        {
            this.Buffer.Clear(value);
            return;
        }

        for (int y = 0; y < this.Bounds.Height; y++)
        {
            Span<T> row = this.DangerousGetRowSpan(y);
            row.Fill(value);
        }
    }
}
