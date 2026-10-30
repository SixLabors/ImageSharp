// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Memory;

/// <summary>
/// Owns three equally sized JPEG XL sample planes.
/// </summary>
/// <typeparam name="T">The sample type.</typeparam>
/// <remarks>
/// Each plane has one owner. Use the image-copy operations to copy samples; copying ownership is not supported.
/// Row views borrow storage from the selected plane and must not outlive it.
/// </remarks>
internal sealed class JxlImage3<T> : IDisposable
    where T : unmanaged
{
    /// <summary>
    /// The three owned planes.
    /// </summary>
    private readonly JxlPlane<T>[] planes;

    /// <summary>
    /// Initializes three empty planes without allocating sample storage.
    /// </summary>
    public JxlImage3() => this.planes = [new(), new(), new()];

    /// <summary>
    /// Initializes three allocated sample planes.
    /// </summary>
    /// <param name="configuration">The configuration that owns the allocator.</param>
    /// <param name="xSize">The logical width in samples.</param>
    /// <param name="ySize">The logical height in rows.</param>
    public JxlImage3(Configuration configuration, int xSize, int ySize)
    {
        this.planes = new JxlPlane<T>[3];
        int allocated = 0;

        try
        {
            for (; allocated < this.planes.Length; allocated++)
            {
                this.planes[allocated] = new(configuration, xSize, ySize);
            }
        }
        catch
        {
            // Construction does not transfer ownership until all planes exist. Release earlier
            // planes if a later allocation fails, then preserve the allocator's original exception.
            for (int i = 0; i < allocated; i++)
            {
                this.planes[i].Dispose();
            }

            throw;
        }
    }

    /// <summary>
    /// Gets the logical width in samples.
    /// </summary>
    public int XSize => this.planes[0].XSize;

    /// <summary>
    /// Gets the logical height in rows.
    /// </summary>
    public int YSize => this.planes[0].YSize;

    /// <summary>
    /// Gets the physical row stride in samples.
    /// </summary>
    public int PixelsPerRow => this.planes[0].PixelsPerRow;

    /// <summary>
    /// Borrows storage from a plane row through the end of that plane's allocation.
    /// </summary>
    /// <param name="plane">The plane index: zero, one, or two.</param>
    /// <param name="row">The row index.</param>
    /// <returns>The row, its padding, and following rows.</returns>
    public Span<T> PlaneRow(int plane, int row) => this.planes[plane].GetRow(row);

    /// <summary>
    /// Borrows memory from a plane row through the end of that plane's allocation.
    /// </summary>
    /// <param name="plane">The plane index: zero, one, or two.</param>
    /// <param name="row">The row index.</param>
    /// <returns>The row, its padding, and following rows.</returns>
    public Memory<T> PlaneRowMemory(int plane, int row) => this.planes[plane].GetRowMemory(row);

    /// <summary>
    /// Borrows storage from a row within a rectangle.
    /// </summary>
    /// <param name="rectangle">The rectangle within the logical plane.</param>
    /// <param name="c">The plane index: zero, one, or two.</param>
    /// <param name="y">The row index relative to the rectangle.</param>
    /// <returns>The remaining allocated samples from the requested position.</returns>
    public Span<T> PlaneRow(Rectangle rectangle, int c, int y) => this.planes[c].GetRow(rectangle, y);

    /// <summary>
    /// Gets an owned plane without transferring its ownership.
    /// </summary>
    /// <param name="index">The plane index: zero, one, or two.</param>
    /// <returns>The selected plane.</returns>
    public JxlPlane<T> Plane(int index) => this.planes[index];

    /// <summary>
    /// Exchanges the storage and dimensions of corresponding planes.
    /// </summary>
    /// <param name="other">The image that receives this image's previous state.</param>
    public void Swap(JxlImage3<T> other)
    {
        for (int i = 0; i < this.planes.Length; i++)
        {
            this.planes[i].Swap(other.planes[i]);
        }
    }

    /// <summary>
    /// Creates three allocated sample planes.
    /// </summary>
    /// <param name="configuration">The configuration that owns the allocator.</param>
    /// <param name="xSize">The logical width in samples.</param>
    /// <param name="ySize">The logical height in rows.</param>
    /// <returns>The image that owns the new planes.</returns>
    public static JxlImage3<T> Create(Configuration configuration, int xSize, int ySize) => new(configuration, xSize, ySize);

    /// <summary>
    /// Changes the logical dimensions of all three planes without changing their strides.
    /// </summary>
    /// <param name="x">The requested width.</param>
    /// <param name="y">The requested height.</param>
    /// <returns>Whether the dimensions fit within the original allocation.</returns>
    public bool ShrinkTo(int x, int y)
    {
        // The planes are allocated and exchanged as a set, so they have the same capacity.
        if (!this.planes[0].ShrinkTo(x, y))
        {
            return false;
        }

        this.planes[1].ShrinkTo(x, y);
        this.planes[2].ShrinkTo(x, y);
        return true;
    }

    /// <summary>
    /// Gets the logical bounds of the image.
    /// </summary>
    /// <returns>The rectangle at the origin with the logical width and height.</returns>
    public Rectangle GetRectangle() => new(0, 0, this.XSize, this.YSize);

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (JxlPlane<T> plane in this.planes)
        {
            plane.Dispose();
        }
    }
}
