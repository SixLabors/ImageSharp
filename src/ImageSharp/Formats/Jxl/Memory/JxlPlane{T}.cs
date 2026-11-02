// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Image;

namespace SixLabors.ImageSharp.Formats.Jxl.Memory;

/// <summary>
/// Owns a contiguous JPEG XL sample plane with padded rows.
/// </summary>
/// <typeparam name="T">The sample type.</typeparam>
/// <remarks>
/// Logical dimensions exclude padding. Changing them does not change the physical stride.
/// Borrowed row views must not outlive the plane.
/// </remarks>
internal sealed class JxlPlane<T> : IDisposable
    where T : unmanaged
{
    /// <summary>
    /// The allocator-owned storage, including prefix and row padding.
    /// </summary>
    private IMemoryOwner<T>? owner;

    /// <summary>
    /// The borrowed view used by row access. An empty plane has no allocation.
    /// </summary>
    private Memory<T> memory;

    /// <summary>
    /// The number of samples before the first row.
    /// </summary>
    private int origin;

    /// <summary>
    /// The original allocation width.
    /// </summary>
    private int allocatedWidth;

    /// <summary>
    /// The original allocation height.
    /// </summary>
    private int allocatedHeight;

    /// <summary>
    /// Initializes an empty plane without allocating sample storage.
    /// </summary>
    public JxlPlane()
    {
    }

    /// <summary>
    /// Initializes an allocated sample plane.
    /// </summary>
    /// <param name="configuration">The configuration that owns the allocator.</param>
    /// <param name="width">The logical width in samples.</param>
    /// <param name="height">The logical height in rows.</param>
    public JxlPlane(Configuration configuration, int width, int height)
        : this(configuration, width, height, 0)
    {
    }

    /// <summary>
    /// Initializes an allocated sample plane with a prefix before its first row.
    /// </summary>
    /// <param name="configuration">The configuration that owns the allocator.</param>
    /// <param name="width">The logical width in samples.</param>
    /// <param name="height">The logical height in rows.</param>
    /// <param name="prePadding">The prefix length in samples.</param>
    public JxlPlane(Configuration configuration, int width, int height, int prePadding)
    {
        Guard.MustBeGreaterThanOrEqualTo(width, 0, nameof(width));
        Guard.MustBeGreaterThanOrEqualTo(height, 0, nameof(height));
        Guard.MustBeGreaterThanOrEqualTo(prePadding, 0, nameof(prePadding));

        this.XSize = this.allocatedWidth = width;
        this.YSize = this.allocatedHeight = height;
        if (width == 0 || height == 0)
        {
            return;
        }

        this.origin = prePadding;

        // Round the physical width up to a complete vector. A final vector can include padding,
        // but cannot read past the allocation. This does not promise an aligned base address.
        int samplesPerVector = Vector512<byte>.Count / Unsafe.SizeOf<T>();
        this.PixelsPerRow = checked((int)((((long)width + samplesPerVector - 1) / samplesPerVector) * samplesPerVector));
        int length = checked((this.PixelsPerRow * height) + prePadding);

        // Existing predictors and transforms address neighboring rows by stride. They require
        // contiguous storage. Typed memory avoids a casting memory-manager allocation per row.
        this.owner = configuration.MemoryAllocator.Allocate<T>(length);
        this.memory = this.owner.Memory;

        // Only padding is initialized here. Callers must write the logical samples before use.
        // Zero padding keeps unused vector lanes finite without clearing the entire image.
        Span<T> storage = this.memory.Span;
        storage[..prePadding].Clear();

        for (int y = 0; y < height; y++)
        {
            storage.Slice(prePadding + (y * this.PixelsPerRow) + width, this.PixelsPerRow - width).Clear();
        }
    }

    /// <summary>
    /// Gets the logical width in samples.
    /// </summary>
    public int XSize { get; private set; }

    /// <summary>
    /// Gets the logical height in rows.
    /// </summary>
    public int YSize { get; private set; }

    /// <summary>
    /// Gets the physical row stride in samples, including padding.
    /// </summary>
    public int PixelsPerRow { get; private set; }

    /// <summary>
    /// Creates an allocated plane.
    /// </summary>
    /// <param name="configuration">The configuration that owns the allocator.</param>
    /// <param name="xSize">The logical width in samples.</param>
    /// <param name="ySize">The logical height in rows.</param>
    /// <param name="prePadding">The prefix length in samples.</param>
    /// <returns>The plane that owns the new storage.</returns>
    public static JxlPlane<T> Create(Configuration configuration, int xSize, int ySize, int prePadding = 0)
        => new(configuration, xSize, ySize, prePadding);

    /// <summary>
    /// Borrows storage from a row's start through the end of the allocation.
    /// </summary>
    /// <param name="y">The row index.</param>
    /// <returns>The row, its padding, and following rows.</returns>
    /// <remarks>
    /// Stride-based kernels access following rows. Copies and fills must select their logical width explicitly.
    /// </remarks>
    public Span<T> GetRow(int y) => this.memory.Span[(this.origin + (y * this.PixelsPerRow))..];

    /// <summary>
    /// Borrows storage starting before a row.
    /// </summary>
    /// <param name="y">The row index.</param>
    /// <param name="minus">The offset to subtract, in samples.</param>
    /// <returns>The remaining allocated samples from the requested position.</returns>
    public Span<T> GetRowMinus(int y, int minus) => this.memory.Span[(this.origin + (y * this.PixelsPerRow) - minus)..];

    /// <summary>
    /// Borrows storage starting at a signed offset from a row.
    /// </summary>
    /// <param name="y">The row index.</param>
    /// <param name="plus">The offset to add, in samples.</param>
    /// <returns>The remaining allocated samples from the requested position.</returns>
    public Span<T> GetRowPlus(int y, int plus) => this.memory.Span[(this.origin + (y * this.PixelsPerRow) + plus)..];

    /// <summary>
    /// Borrows memory from a row's start through the end of the allocation.
    /// </summary>
    /// <param name="y">The row index.</param>
    /// <returns>The row, its padding, and following rows.</returns>
    public Memory<T> GetRowMemory(int y) => this.memory[(this.origin + (y * this.PixelsPerRow))..];

    /// <summary>
    /// Borrows storage from the start of a row within a rectangle.
    /// </summary>
    /// <param name="rectangle">The rectangle within the logical plane.</param>
    /// <param name="y">The row index relative to the rectangle.</param>
    /// <returns>The remaining allocated samples from the requested position.</returns>
    public Span<T> GetRow(Rectangle rectangle, int y) => this.GetRow(y + rectangle.Top)[rectangle.Left..];

    /// <summary>
    /// Borrows memory from the start of a row within a rectangle.
    /// </summary>
    /// <param name="rectangle">The rectangle within the logical plane.</param>
    /// <param name="y">The row index relative to the rectangle.</param>
    /// <returns>The remaining allocated samples from the requested position.</returns>
    public Memory<T> GetRowMemory(Rectangle rectangle, int y) => this.GetRowMemory(y + rectangle.Top)[rectangle.Left..];

    /// <summary>
    /// Changes logical dimensions without reallocating or changing the stride.
    /// </summary>
    /// <param name="x">The requested width.</param>
    /// <param name="y">The requested height.</param>
    /// <returns>Whether both dimensions fit within the original allocation.</returns>
    /// <remarks>
    /// A previously reduced plane can grow back within its original dimensions.
    /// </remarks>
    public bool ShrinkTo(int x, int y)
    {
        if ((uint)x > (uint)this.allocatedWidth || (uint)y > (uint)this.allocatedHeight)
        {
            return false;
        }

        this.XSize = x;
        this.YSize = y;
        return true;
    }

    /// <summary>
    /// Checks whether a rectangle fits within the logical plane.
    /// </summary>
    /// <param name="rectangle">The rectangle to check.</param>
    /// <returns>Whether the plane contains the rectangle.</returns>
    public bool IsRectangleInside(Rectangle rectangle) => this.GetRectangle().Contains(rectangle);

    /// <summary>
    /// Gets the logical bounds of the plane.
    /// </summary>
    /// <returns>The rectangle at the origin with the logical width and height.</returns>
    public Rectangle GetRectangle() => new(0, 0, this.XSize, this.YSize);

    /// <summary>
    /// Clears the logical samples.
    /// </summary>
    public void Clear() => JxlImageOperations.ZeroFillImage(this);

    /// <summary>
    /// Exchanges storage ownership, logical dimensions, and allocation layout.
    /// </summary>
    /// <param name="other">The plane that receives this plane's previous state.</param>
    public void Swap(JxlPlane<T> other)
    {
        (this.owner, other.owner) = (other.owner, this.owner);
        (this.memory, other.memory) = (other.memory, this.memory);
        (this.origin, other.origin) = (other.origin, this.origin);
        (this.allocatedWidth, other.allocatedWidth) = (other.allocatedWidth, this.allocatedWidth);
        (this.allocatedHeight, other.allocatedHeight) = (other.allocatedHeight, this.allocatedHeight);
        (this.XSize, other.XSize) = (other.XSize, this.XSize);
        (this.YSize, other.YSize) = (other.YSize, this.YSize);
        (this.PixelsPerRow, other.PixelsPerRow) = (other.PixelsPerRow, this.PixelsPerRow);
    }

    /// <summary>
    /// Releases the allocation and resets the plane to an empty state.
    /// </summary>
    public void Dispose()
    {
        this.owner?.Dispose();
        this.owner = null;
        this.memory = default;

        // Capacity belongs to the allocation. A released plane cannot satisfy a later ShrinkTo request for sample storage.
        this.origin = 0;
        this.allocatedWidth = 0;
        this.allocatedHeight = 0;
        this.XSize = 0;
        this.YSize = 0;
        this.PixelsPerRow = 0;
    }
}
