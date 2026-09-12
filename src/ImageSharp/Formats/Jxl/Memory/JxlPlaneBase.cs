// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Jxl.Memory;

/// <summary>
/// Base class for a single-plane image.
/// </summary>
internal class JxlPlaneBase : IDisposable
{
    /// <summary>
    /// Underlying bytes
    /// </summary>
    private IMemoryOwner<byte>? bytes;

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlPlaneBase"/> class.
    /// </summary>
    /// <param name="xSize">Plane width</param>
    /// <param name="ySize">Plane height</param>
    /// <param name="sizeOfT">The size of each pixel in bytes.</param>
    public JxlPlaneBase(int xSize, int ySize, int sizeOfT)
    {
        this.XSize = xSize;
        this.YSize = ySize;
        this.OriginalXSize = xSize;
        this.OriginalYSize = ySize;
        this.BytesPerRow = 0;
        this.Size = sizeOfT;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlPlaneBase"/> class with empty values.
    /// </summary>
    public JxlPlaneBase()
        : this(0, 0, 0)
    {
    }

    /// <summary>
    /// Gets the number of bytes per row.
    /// </summary>
    public int BytesPerRow { get; private set; }

    /// <summary>
    /// Gets the width of the image.
    /// </summary>
    public int XSize { get; private set; }

    /// <summary>
    /// Gets the height of the image.
    /// </summary>
    public int YSize { get; private set; }

    /// <summary>
    /// Gets the underlying bytes of this image as a Memory&lt;T&gt;.
    /// </summary>
    public Memory<byte> Bytes =>
#if DEBUG
        this.bytes?.Memory ?? throw new InvalidOperationException("Bytes are missing");
#else
        return this.bytes!.Memory;
#endif

    /// <summary>
    /// Gets the underlying bytes of this image as a Span&lt;T&gt;.
    /// </summary>
    public Span<byte> BytesSpan => this.Bytes.Span;

    protected int Size { get; set; }

    /// <summary>
    /// Gets or sets the width that was initially assigned. For example, if the image gets shrinked,
    /// the XSize YSize properties get changed while this property will stay same.
    /// </summary>
    protected int OriginalXSize { get; set; }

    /// <summary>
    /// Gets or sets the height that was initially assigned. For example, if the image gets shrinked,
    /// the XSize YSize properties get changed while this property will stay same.
    /// </summary>
    protected int OriginalYSize { get; set; }

    /// <summary>
    /// Allocates the underlying memory for the plane.
    /// </summary>
    /// <param name="configuration">The configuration which has a memory allocator used to allocate memory.</param>
    /// <param name="prePadding">Padding</param>
    /// <returns>Status of allocation.</returns>
    public bool Allocate(Configuration configuration, int prePadding)
    {
        if (this.bytes != null || this.BytesPerRow != 0)
        {
            return false;
        }

        if (this.XSize == 0 || this.YSize == 0)
        {
            return true;
        }

        int totalBytes = unchecked(this.YSize * this.BytesPerRow);

        this.bytes = configuration.MemoryAllocator.Allocate<byte>(totalBytes + (prePadding * this.Size));

        return true;
    }

    /// <summary>
    /// Shrinks the image so its width is equal to <paramref name="x"/> and its height is
    /// equal to <paramref name="y"/>.
    /// </summary>
    /// <param name="x">The output width</param>
    /// <param name="y">The output height</param>
    /// <returns>Status of the shrinking operation.</returns>
    /// <remarks>
    ///   <para>
    ///     This method can only shrink memory. It cannot expand it.
    ///   </para>
    ///   <para>
    ///     When shrinking, the underlying memory does not get resized.
    ///   </para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ShrinkTo(int x, int y)
    {
        if (x <= this.OriginalXSize || y <= this.OriginalYSize)
        {
            return false;
        }

        DebugGuard.MustBeLessThanOrEqualTo(x, this.OriginalXSize, nameof(x));
        DebugGuard.MustBeLessThanOrEqualTo(y, this.OriginalYSize, nameof(y));

        this.XSize = x;
        this.YSize = y;

        return true;
    }

    /// <summary>
    /// Base function to return the span for a specified row as a generic &lt;T&gt;.
    /// </summary>
    /// <typeparam name="T">The type of the row.</typeparam>
    /// <param name="y">The index of the row to get the span for.</param>
    /// <returns>A span which covers the row memory.</returns>
    protected Span<T> GetRowBase<T>(int y)
        where T : unmanaged
    {
        DebugGuard.MustBeLessThan(y, this.YSize, nameof(y));

        Span<byte> row = this.Bytes.Span[(y * this.BytesPerRow)..];
        return MemoryMarshal.Cast<byte, T>(row);
    }

    protected Span<T> GetRowMinusBase<T>(int y, int minus)
        where T : unmanaged
    {
        DebugGuard.MustBeLessThan(y, this.YSize, nameof(y));

        Span<byte> row = this.Bytes.Span[((y * this.BytesPerRow) - minus)..];
        return MemoryMarshal.Cast<byte, T>(row);
    }

    protected Span<T> GetRowPlusBase<T>(int y, int plus)
        where T : unmanaged
    {
        DebugGuard.MustBeLessThan(y, this.YSize, nameof(y));

        Span<byte> row = this.Bytes.Span[((y * this.BytesPerRow) + plus)..];
        return MemoryMarshal.Cast<byte, T>(row);
    }

    /// <summary>
    /// Swaps properties &amp; data of this image with the specified image.
    /// </summary>
    /// <param name="other">The other image to swap with.</param>
    public void Swap(JxlPlaneBase other)
    {
        (this.XSize, other.XSize) = (other.XSize, this.XSize);
        (this.YSize, other.YSize) = (other.YSize, this.YSize);
        (this.OriginalXSize, other.OriginalXSize) = (other.OriginalXSize, this.OriginalXSize);
        (this.OriginalYSize, other.OriginalYSize) = (other.OriginalYSize, this.OriginalYSize);
        (this.BytesPerRow, other.BytesPerRow) = (other.BytesPerRow, this.BytesPerRow);
        (this.bytes, other.bytes) = (other.bytes, this.bytes);
    }

    /// <summary>
    /// Releases all underlying memory used by this plane.
    /// </summary>
    public void Dispose()
    {
        this.bytes?.Dispose();
        GC.SuppressFinalize(this);
    }
}
