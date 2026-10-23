// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Jxl.Memory.ImageTypes;

namespace SixLabors.ImageSharp.Formats.Jxl.Memory;

/// <summary>
/// A wrapper over 3 planes of type <typeparamref name="T"/>. See
/// also <see cref="JxlPlane{T}"/>.
/// </summary>
/// <typeparam name="T">
/// The kind of each item in each of the 3 planes.
/// </typeparam>
/// <remarks>
/// <b>This method should not be marked as sealed.</b> Types such as
/// <see cref="JxlImageF"/> are commonly used across the JPEG-XL implementation,
/// which derive from this class.
/// </remarks>
internal class JxlImage3<T> : IDisposable
    where T : unmanaged
{
    /// <summary>
    /// A <see cref="JxlImage3{T}"/> contains 3 planes.
    /// </summary>
    private const int PlaneCount = 3;

    /// <summary>
    /// All 3 underlying planes are stored here.
    /// </summary>
    private JxlPlane<T>[] planes = new JxlPlane<T>[3];

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlImage3{T}"/> class
    /// with 3 planes that are empty and have a width and height of 0 pixels.
    /// </summary>
    public JxlImage3()
    {
        // We want to initialize planes, so they're at least empty
        // planes rather than null.
        this.planes[0] = new();
        this.planes[1] = new();
        this.planes[2] = new();
    }

    public JxlImage3(Configuration configuration, int xSize, int ySize)
        => this.Allocate(configuration, xSize, ySize);

    public JxlImage3(JxlImage3<T> other)
    {
        for (int i = 0; i < PlaneCount; i++)
        {
            this.planes[i] = other.planes[i];
        }
    }

    /// <summary>
    /// Gets the width of this image.
    /// </summary>
    public int XSize => this.planes[0].XSize;

    /// <summary>
    /// Gets the height of this image.
    /// </summary>
    public int YSize => this.planes[0].YSize;

    /// <summary>
    /// Gets the number of bytes per row in this image.
    /// </summary>
    public int BytesPerRow => this.planes[0].BytesPerRow;

    /// <summary>
    /// Gets the number of pixels per row in this image.
    /// </summary>
    public int PixelsPerRow => this.planes[0].PixelsPerRow;

    /// <summary>
    /// Shorthand for <c>image3.Plane(<paramref name="plane"/>).GetRow(<paramref name="row"/>)</c>.
    /// </summary>
    /// <param name="plane">
    /// Index of the plane. This can be 0, 1, or 2.
    /// </param>
    /// <param name="row">
    /// Index of the row within the plane whose index is specified by the
    /// <paramref name="plane"/> parameter.
    /// </param>
    /// <returns>
    /// A Span view for the row of the plane. Modifying values of this span
    /// directly modifies the data for that row in that plane.
    /// </returns>
    public Span<T> PlaneRow(int plane, int row)
    {
        this.PlaneRowBoundsCheck(plane, row);

        int rowOffset = row * this.planes[0].BytesPerRow;
        Span<T> rowSpan = MemoryMarshal.Cast<byte, T>(this.planes[plane].BytesSpan[rowOffset..]);

        return rowSpan;
    }

    // This method performs minor allocations!
    public Memory<T> PlaneRowMemory(int plane, int row)
    {
        this.PlaneRowBoundsCheck(plane, row);

        int rowOffset = row * this.planes[0].BytesPerRow;
        Memory<byte> rowMemoryBytes = this.planes[plane].Bytes[rowOffset..];

        // we have to allocate a utility class so we can reinterpret
        // a Memory<T>.
        // Unsafe.As is truly unsafe because, f.e. what if there are
        // 400 bytes but T is 4 bytes? the length will remain as 400.
        TypeChangingMemoryManager<T> reinterpreter = new(rowMemoryBytes);
        return reinterpreter.Memory;
    }

    public Span<T> PlaneRow(Rectangle rectangle, int c, int y)
    {
        DebugGuard.MustBeGreaterThanOrEqualTo(y + rectangle.Top, 0, nameof(y));

        return this.PlaneRow(c, y + rectangle.Top)[rectangle.Left..];
    }

    /// <summary>
    /// Returns a plane specified by index <paramref name="index"/>. There are
    /// 3 planes in a <see cref="JxlImage3{T}"/> instance.
    /// </summary>
    /// <param name="index">
    /// The index of the plane. This can only be 0, 1 or 2.
    /// </param>
    /// <returns>
    /// A plane whose index is specified by the <paramref name="index"/> parameter.
    /// </returns>
    public JxlPlane<T> Plane(int index) => this.planes[index];

    public void Swap(JxlImage3<T> other)
    {
        for (int i = 0; i < PlaneCount; i++)
        {
            other.planes[i].Swap(this.planes[i]);
        }
    }

    public static JxlImage3<T> Create(Configuration configuration, int xSize, int ySize)
        => new(configuration, xSize, ySize);

    public void Allocate(Configuration configuration, int xSize, int ySize)
    {
        JxlPlane<T> plane0 = JxlPlane<T>.Create(configuration, xSize, ySize);
        JxlPlane<T> plane1 = JxlPlane<T>.Create(configuration, xSize, ySize);
        JxlPlane<T> plane2 = JxlPlane<T>.Create(configuration, xSize, ySize);

        this.planes = [plane0, plane1, plane2];
    }

    public bool ShrinkTo(int x, int y)
    {
        for (int i = 0; i < PlaneCount; i++)
        {
            if (!this.planes[i].ShrinkTo(x, y))
            {
                return false;
            }
        }

        return true;
    }

    private void PlaneRowBoundsCheck(int c, int y)
    {
        DebugGuard.MustBeLessThan(c, PlaneCount, nameof(c));
        DebugGuard.MustBeLessThan(y, this.YSize, nameof(y));
    }

    /// <summary>
    /// Returns the rectangle for this image bounds.
    /// </summary>
    /// <returns>A rectangle with x,y=0,0 width,height=XSize,YSize.</returns>
    public Rectangle GetRectangle() => new(0, 0, this.XSize, this.YSize);

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (JxlPlane<T> plane in this.planes)
        {
            plane.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// <para>
    /// This is used by <see cref="PlaneRowMemory(int, int)"/>. The underlying data
    /// of a plane is stored as <c>Memory&lt;byte&gt;</c>. Unlike a <see cref="Span{T}"/>
    /// where <c>Span&lt;byte&gt;</c> can be directly changed to <c>Span&lt;T&gt;</c>
    /// using <see cref="MemoryMarshal.Cast{TFrom, TTo}(Span{TFrom})"/>, changing the
    /// type of a <see cref="Memory{T}"/> cannot be done as simply. Instead, we have
    /// to create a new instance of a <see cref="MemoryManager{T}"/> that implements
    /// the <see cref="GetSpan"/> method. Therefore the following code snippet can be
    /// used with this class to reinterpret the type of a Memory.
    /// <code>
    /// Memory&lt;T&gt; ConvertMemory&lt;T&gt;(Memory&lt;byte&gt; src)
    /// {
    ///     TypeChangingMemoryManager&lt;T&gt; mm = new(src);
    ///     return mm.Memory;
    /// }
    /// </code>
    /// </para>
    /// <para>
    /// This is the only option so far, but it allocates on the heap.
    /// </para>
    /// </summary>
    /// <typeparam name="TTarget">
    /// The target type that a <c>Memory&lt;byte&gt;</c> will be reinterpreted into.
    /// </typeparam>
    /// <param name="memory">
    /// Input <c>Memory&lt;byte&gt;</c> to reinterpret into <c>Memory&lt;T&gt;</c>.
    /// </param>
    private sealed class TypeChangingMemoryManager<TTarget>(Memory<byte> memory) : MemoryManager<TTarget>
        where TTarget : unmanaged
    {
        /// <inheritdoc />
        public override Span<TTarget> GetSpan() => MemoryMarshal.Cast<byte, TTarget>(memory.Span);

        /// <inheritdoc />
        // NOTE: We only need the .GetSpan() method. We do not need
        // any of the methods below.
        public override MemoryHandle Pin(int elementIndex = 0) => throw new NotImplementedException();

        /// <inheritdoc />
        public override void Unpin() => throw new NotImplementedException();

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
        }
    }
}
