// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

/// <summary>
/// A rectangle of one AV1 component plane. The plane is a single contiguous allocation whose rows are one stride
/// apart, so a kernel addresses any plane sample, borders included, from <see cref="Samples"/> and <see cref="Stride"/>.
/// Reference: the buffer, stride and crop dimensions of one YV12_BUFFER_CONFIG plane.
/// </summary>
/// <typeparam name="TSample">The sample storage type.</typeparam>
internal readonly struct Av1PlaneRegion<TSample>
    where TSample : unmanaged
{
    /// <summary>
    /// The complete plane, including its borders.
    /// </summary>
    private readonly Memory<TSample> plane;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1PlaneRegion{TSample}"/> struct.
    /// </summary>
    /// <param name="plane">The complete plane, including its borders.</param>
    /// <param name="stride">The number of samples between the starts of adjacent plane rows.</param>
    /// <param name="bounds">The rectangle inside the plane.</param>
    public Av1PlaneRegion(Memory<TSample> plane, int stride, Rectangle bounds)
    {
        DebugGuard.MustBeGreaterThan(stride, 0, nameof(stride));
        DebugGuard.MustBeGreaterThanOrEqualTo(bounds.X, 0, nameof(bounds));
        DebugGuard.MustBeGreaterThanOrEqualTo(bounds.Y, 0, nameof(bounds));
        DebugGuard.MustBeLessThanOrEqualTo(bounds.Right, stride, nameof(bounds));
        DebugGuard.MustBeLessThanOrEqualTo(bounds.Bottom, plane.Length / stride, nameof(bounds));

        this.plane = plane;
        this.Stride = stride;
        this.Bounds = bounds;
    }

    /// <summary>
    /// Gets the complete plane, including its borders.
    /// </summary>
    public Span<TSample> Samples
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.plane.Span;
    }

    /// <summary>
    /// Gets the number of samples between the starts of adjacent plane rows.
    /// </summary>
    public int Stride { get; }

    /// <summary>
    /// Gets the number of rows in the complete plane.
    /// </summary>
    public int PlaneHeight => this.Stride == 0 ? 0 : this.plane.Length / this.Stride;

    /// <summary>
    /// Gets the rectangle inside the plane.
    /// </summary>
    public Rectangle Bounds { get; }

    /// <summary>
    /// Gets the rectangle width.
    /// </summary>
    public int Width => this.Bounds.Width;

    /// <summary>
    /// Gets the rectangle height.
    /// </summary>
    public int Height => this.Bounds.Height;

    /// <summary>
    /// Gets the rectangle size.
    /// </summary>
    public Size Size => this.Bounds.Size;

    /// <summary>
    /// Gets the index in <see cref="Samples"/> of the rectangle's top-left sample.
    /// </summary>
    public int Origin => (this.Bounds.Y * this.Stride) + this.Bounds.X;

    /// <summary>
    /// Gets the index in <see cref="Samples"/> of a sample given relative to the rectangle's top-left sample.
    /// </summary>
    /// <param name="x">The column relative to the rectangle.</param>
    /// <param name="y">The row relative to the rectangle.</param>
    /// <returns>The plane index.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetOffset(int x, int y) => ((this.Bounds.Y + y) * this.Stride) + this.Bounds.X + x;

    /// <summary>
    /// Gets one row of the rectangle.
    /// </summary>
    /// <param name="y">The row relative to the rectangle.</param>
    /// <returns>The <see cref="Width"/> samples of the row.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<TSample> GetRowSpan(int y) => this.plane.Span.Slice(this.GetOffset(0, y), this.Bounds.Width);

    /// <summary>
    /// Gets one complete row of the plane, including its borders.
    /// </summary>
    /// <param name="y">The plane row.</param>
    /// <returns>The <see cref="Stride"/> samples of the plane row.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<TSample> GetPlaneRowSpan(int y) => this.plane.Span.Slice(y * this.Stride, this.Stride);

    /// <summary>
    /// Gets the complete plane, including its borders, as a rectangle.
    /// </summary>
    /// <returns>The rectangle covering every plane sample.</returns>
    public Av1PlaneRegion<TSample> GetFullPlane()
        => new(this.plane, this.Stride, new Rectangle(0, 0, this.Stride, this.PlaneHeight));

    /// <summary>
    /// Gets a rectangle inside this rectangle.
    /// </summary>
    /// <param name="x">The column of the new rectangle relative to this rectangle.</param>
    /// <param name="y">The row of the new rectangle relative to this rectangle.</param>
    /// <param name="width">The width of the new rectangle.</param>
    /// <param name="height">The height of the new rectangle.</param>
    /// <returns>The new rectangle of the same plane.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Av1PlaneRegion<TSample> GetSubRegion(int x, int y, int width, int height)
        => this.GetSubRegion(new Rectangle(x, y, width, height));

    /// <summary>
    /// Gets a rectangle inside this rectangle.
    /// </summary>
    /// <param name="rectangle">The new rectangle relative to this rectangle.</param>
    /// <returns>The new rectangle of the same plane.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Av1PlaneRegion<TSample> GetSubRegion(Rectangle rectangle)
    {
        DebugGuard.MustBeLessThanOrEqualTo(rectangle.Width, this.Bounds.Width, nameof(rectangle));
        DebugGuard.MustBeLessThanOrEqualTo(rectangle.Height, this.Bounds.Height, nameof(rectangle));

        return new Av1PlaneRegion<TSample>(
            this.plane,
            this.Stride,
            new Rectangle(this.Bounds.X + rectangle.X, this.Bounds.Y + rectangle.Y, rectangle.Width, rectangle.Height));
    }

    /// <summary>
    /// Sets every sample of the rectangle to zero.
    /// </summary>
    public void Clear()
    {
        for (int y = 0; y < this.Bounds.Height; y++)
        {
            this.GetRowSpan(y).Clear();
        }
    }

    /// <summary>
    /// Sets every sample of the rectangle to one value.
    /// </summary>
    /// <param name="value">The sample value.</param>
    public void Fill(TSample value)
    {
        for (int y = 0; y < this.Bounds.Height; y++)
        {
            this.GetRowSpan(y).Fill(value);
        }
    }
}
