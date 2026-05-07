// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Memory;

/// <summary>
/// Represents a buffer of value type objects
/// interpreted as a 2D region of <see cref="Width"/> x <see cref="Height"/> elements.
/// </summary>
/// <typeparam name="T">The value type.</typeparam>
public sealed class Buffer2D<T> : IDisposable
    where T : struct
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Buffer2D{T}"/> class.
    /// </summary>
    /// <param name="memoryGroup">The <see cref="MemoryGroup{T}"/> to wrap.</param>
    /// <param name="width">The number of elements in a row.</param>
    /// <param name="height">The number of rows.</param>
    internal Buffer2D(MemoryGroup<T> memoryGroup, int width, int height)
        : this(memoryGroup, width, height, width)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Buffer2D{T}"/> class.
    /// </summary>
    /// <param name="memoryGroup">The <see cref="MemoryGroup{T}"/> to wrap.</param>
    /// <param name="width">The number of elements in a row.</param>
    /// <param name="height">The number of rows.</param>
    /// <param name="rowStride">The number of elements between row starts.</param>
    internal Buffer2D(MemoryGroup<T> memoryGroup, int width, int height, int rowStride)
    {
        Guard.MustBeGreaterThan(width, 0, nameof(width));
        Guard.MustBeGreaterThan(height, 0, nameof(height));
        Guard.MustBeGreaterThanOrEqualTo(rowStride, width, nameof(rowStride));

        this.FastMemoryGroup = memoryGroup;
        this.Size = new Size(width, height);
        this.RowStride = rowStride;
    }

    /// <summary>
    /// Gets the width.
    /// </summary>
    public int Width => this.Size.Width;

    /// <summary>
    /// Gets the height.
    /// </summary>
    public int Height => this.Size.Height;

    /// <summary>
    /// Gets the size of the buffer.
    /// </summary>
    public Size Size { get; private set; }

    /// <summary>
    /// Gets the bounds of the buffer.
    /// </summary>
    /// <returns>The <see cref="Rectangle"/></returns>
    public Rectangle Bounds => new(0, 0, this.Width, this.Height);

    /// <summary>
    /// Gets the number of elements between row starts in the backing memory.
    /// </summary>
    public int RowStride { get; private set; }

    /// <summary>
    /// Gets the backing <see cref="IMemoryGroup{T}"/>.
    /// </summary>
    /// <returns>The MemoryGroup.</returns>
    public IMemoryGroup<T> MemoryGroup => this.FastMemoryGroup.View;

    /// <summary>
    /// Gets the backing <see cref="MemoryGroup{T}"/> without the view abstraction.
    /// </summary>
    /// <remarks>
    /// This property has been kept internal intentionally.
    /// It's public counterpart is <see cref="MemoryGroup"/>,
    /// which only exposes the view of the MemoryGroup.
    /// </remarks>
    internal MemoryGroup<T> FastMemoryGroup { get; private set; }

    internal bool IsDisposed { get; private set; }

    /// <summary>
    /// Gets a reference to the element at the specified position.
    /// </summary>
    /// <param name="x">The x coordinate (row)</param>
    /// <param name="y">The y coordinate (position at row)</param>
    /// <returns>A reference to the element.</returns>
    /// <exception cref="IndexOutOfRangeException">When index is out of range of the buffer.</exception>
    public ref T this[int x, int y]
    {
        [MethodImpl(InliningOptions.ShortMethod)]
        get
        {
            DebugGuard.MustBeGreaterThanOrEqualTo(x, 0, nameof(x));
            DebugGuard.MustBeGreaterThanOrEqualTo(y, 0, nameof(y));
            DebugGuard.MustBeLessThan(x, this.Width, nameof(x));
            DebugGuard.MustBeLessThan(y, this.Height, nameof(y));

            return ref this.DangerousGetRowSpan(y)[x];
        }
    }

    /// <summary>
    /// Wraps an existing memory area as a <see cref="Buffer2D{T}"/> with tightly packed rows.
    /// </summary>
    /// <remarks>
    /// This method does not transfer ownership of <paramref name="memory"/> to the returned <see cref="Buffer2D{T}"/>.
    /// The caller is responsible for ensuring that the memory remains valid for the entire lifetime of the returned buffer.
    /// If <paramref name="memory"/> originates from an <see cref="IMemoryOwner{T}"/> (for example from <see cref="MemoryPool{T}"/>),
    /// do not dispose that owner while the returned buffer is still in use.
    /// </remarks>
    /// <param name="memory">The source memory.</param>
    /// <param name="width">The number of elements in each row.</param>
    /// <param name="height">The number of rows.</param>
    /// <returns>The wrapped <see cref="Buffer2D{T}"/> instance.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="width"/> or <paramref name="height"/> is not positive.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="memory"/> is shorter than <c>width * height</c>.</exception>
#pragma warning disable CA1000 // Do not declare static members on generic types
    public static Buffer2D<T> WrapMemory(Memory<T> memory, int width, int height)
#pragma warning restore CA1000 // Do not declare static members on generic types
        => WrapMemory(memory, width, height, width);

    /// <summary>
    /// Wraps an existing memory area as a <see cref="Buffer2D{T}"/> using the specified row stride.
    /// </summary>
    /// <remarks>
    /// This method does not transfer ownership of <paramref name="memory"/> to the returned <see cref="Buffer2D{T}"/>.
    /// The caller is responsible for ensuring that the memory remains valid for the entire lifetime of the returned buffer.
    /// If <paramref name="memory"/> originates from an <see cref="IMemoryOwner{T}"/> (for example from <see cref="MemoryPool{T}"/>),
    /// do not dispose that owner while the returned buffer is still in use.
    /// The minimum required length is <c>((height - 1) * stride) + width</c> elements.
    /// </remarks>
    /// <param name="memory">The source memory.</param>
    /// <param name="width">The number of elements in each row.</param>
    /// <param name="height">The number of rows.</param>
    /// <param name="stride">The number of elements between row starts in the source memory.</param>
    /// <returns>The wrapped <see cref="Buffer2D{T}"/> instance.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="width"/> or <paramref name="height"/> is not positive,
    /// or when <paramref name="stride"/> is less than <paramref name="width"/>.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="memory"/> is shorter than the required buffer size.</exception>
#pragma warning disable CA1000 // Do not declare static members on generic types
    public static Buffer2D<T> WrapMemory(Memory<T> memory, int width, int height, int stride)
#pragma warning restore CA1000 // Do not declare static members on generic types
    {
        Guard.MustBeGreaterThan(width, 0, nameof(width));
        Guard.MustBeGreaterThan(height, 0, nameof(height));
        Guard.MustBeGreaterThanOrEqualTo(stride, width, nameof(stride));

        long requiredLength = checked(((long)(height - 1) * stride) + width);
        Guard.IsTrue(memory.Length >= requiredLength, nameof(memory), "The length of the input memory is less than the specified buffer size");

        MemoryGroup<T> memorySource = MemoryGroup<T>.Wrap(memory);
        return new Buffer2D<T>(memorySource, width, height, stride);
    }

    /// <summary>
    /// Gets the representation of the values as a single contiguous <see cref="Memory{T}"/>
    /// when the backing group is a single tightly packed segment.
    /// </summary>
    /// <param name="memory">The <see cref="Memory{T}"/> referencing the buffer.</param>
    /// <returns>
    /// <see langword="true"/> when the buffer can be copied as one contiguous block
    /// without per-row handling; otherwise <see langword="false"/>.
    /// </returns>
    public bool DangerousTryGetSingleMemory(out Memory<T> memory)
    {
        if (this.MemoryGroup.Count > 1 || this.RowStride != this.Width)
        {
            memory = default;
            return false;
        }

        int logicalLength = checked((int)((long)this.Width * this.Height));
        memory = this.MemoryGroup[0][..logicalLength];
        return true;
    }

    /// <summary>
    /// Copies this buffer into <paramref name="destination"/> using the source logical row layout.
    /// </summary>
    /// <remarks>
    /// When dimensions are equal, destination stride is respected.
    /// When dimensions differ, source stride is used to copy the source logical layout into destination memory.
    /// </remarks>
    /// <param name="destination">The destination buffer.</param>
    internal void CopyTo(Buffer2D<T> destination)
    {
        Guard.NotNull(destination, nameof(destination));

        bool sameDimensions = this.Width == destination.Width && this.Height == destination.Height;
        int destinationStride = sameDimensions ? destination.RowStride : this.RowStride;

        // Different dimensions use source logical layout. This supports SwapOrCopyContent,
        // where metadata is swapped after data copy.
        this.FastMemoryGroup.CopyTo(
            this.RowStride,
            destination.FastMemoryGroup,
            destinationStride,
            this.Width,
            this.Height);
    }

    /// <summary>
    /// Copies this buffer into <paramref name="destination"/> using the source row stride as destination layout.
    /// </summary>
    /// <param name="destination">The destination span.</param>
    internal void CopyTo(Span<T> destination)
    {
        long requiredLength = checked(((long)(this.Height - 1) * this.RowStride) + this.Width);
        Guard.MustBeGreaterThanOrEqualTo(destination.Length, requiredLength, nameof(destination));

        this.FastMemoryGroup.CopyTo(
            this.RowStride,
            destination,
            this.RowStride,
            this.Width,
            this.Height);
    }

    /// <summary>
    /// Copies tightly packed row-major data from <paramref name="source"/> into this buffer.
    /// </summary>
    /// <param name="source">The source data.</param>
    internal void CopyFrom(ReadOnlySpan<T> source) => this.CopyFrom(source, this.Width);

    /// <summary>
    /// Copies row-major data from <paramref name="source"/> into this buffer using
    /// <paramref name="sourceStride"/> elements between source row starts.
    /// </summary>
    /// <param name="source">The source data.</param>
    /// <param name="sourceStride">The number of elements between source row starts.</param>
    internal void CopyFrom(ReadOnlySpan<T> source, int sourceStride)
    {
        Guard.MustBeGreaterThanOrEqualTo(sourceStride, this.Width, nameof(sourceStride));

        long requiredLength = checked(((long)(this.Height - 1) * sourceStride) + this.Width);
        Guard.MustBeGreaterThanOrEqualTo(source.Length, requiredLength, nameof(source));

        // Copy row by row so padded source rows map correctly into the destination logical rows.
        int sourceOffset = 0;
        for (int y = 0; y < this.Height; y++)
        {
            source.Slice(sourceOffset, this.Width).CopyTo(this.DangerousGetRowSpan(y));
            sourceOffset += sourceStride;
        }
    }

    /// <summary>
    /// Clears this buffer when <paramref name="value"/> is default; otherwise fills it with <paramref name="value"/>.
    /// </summary>
    /// <param name="value">The fill value.</param>
    internal void Clear(T value)
    {
        if (value.Equals(default))
        {
            this.FastMemoryGroup.Clear();
            return;
        }

        this.FastMemoryGroup.Fill(value);
    }

    /// <summary>
    /// Disposes the <see cref="Buffer2D{T}"/> instance
    /// </summary>
    public void Dispose()
    {
        this.FastMemoryGroup.Dispose();
        this.IsDisposed = true;
    }

    /// <summary>
    /// Gets a <see cref="Span{T}"/> to the row 'y' beginning from the pixel at the first pixel on that row.
    /// </summary>
    /// <remarks>
    /// This method does not validate the y argument for performance reason,
    /// <see cref="ArgumentOutOfRangeException"/> is being propagated from lower levels.
    /// </remarks>
    /// <param name="y">The row index.</param>
    /// <returns>The <see cref="Span{T}"/> of the pixels in the row.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when row index is out of range.</exception>
    [MethodImpl(InliningOptions.ShortMethod)]
    public Span<T> DangerousGetRowSpan(int y)
    {
        if ((uint)y >= (uint)this.Height)
        {
            this.ThrowYOutOfRangeException(y);
        }

        if (this.RowStride == this.Width)
        {
            return this.FastMemoryGroup.GetRowSpanCoreUnsafe(y, this.Width);
        }

        int rowStart = checked(y * this.RowStride);
        return this.FastMemoryGroup[0].Span.Slice(rowStart, this.Width);
    }

    internal bool DangerousTryGetPaddedRowSpan(int y, int padding, out Span<T> paddedSpan)
    {
        DebugGuard.MustBeGreaterThanOrEqualTo(y, 0, nameof(y));
        DebugGuard.MustBeLessThan(y, this.Height, nameof(y));

        int stride = this.Width + padding;
        long rowStart = y * (long)this.RowStride;
        Span<T> slice = this.RowStride == this.Width
            ? this.FastMemoryGroup.GetRemainingSliceOfBuffer(rowStart)
            : this.FastMemoryGroup[0].Span[checked((int)rowStart)..];

        if (slice.Length < stride)
        {
            paddedSpan = default;
            return false;
        }

        paddedSpan = slice[..stride];
        return true;
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    internal ref T GetElementUnsafe(int x, int y)
    {
        Span<T> span = this.RowStride == this.Width
            ? this.FastMemoryGroup.GetRowSpanCoreUnsafe(y, this.Width)
            : this.FastMemoryGroup[0].Span.Slice(checked(y * this.RowStride), this.Width);

        return ref span[x];
    }

    /// <summary>
    /// Gets a <see cref="Memory{T}"/> to the row 'y' beginning from the pixel at the first pixel on that row.
    /// </summary>
    /// <param name="y">The y (row) coordinate.</param>
    /// <returns>The <see cref="Span{T}"/>.</returns>
    [MethodImpl(InliningOptions.ShortMethod)]
    internal Memory<T> GetSafeRowMemory(int y)
    {
        DebugGuard.MustBeGreaterThanOrEqualTo(y, 0, nameof(y));
        DebugGuard.MustBeLessThan(y, this.Height, nameof(y));

        if (this.RowStride != this.Width)
        {
            int rowStart = checked(y * this.RowStride);
            return this.FastMemoryGroup[0].Slice(rowStart, this.Width);
        }

        return this.FastMemoryGroup.View.GetBoundedMemorySlice(y * (long)this.Width, this.Width);
    }

    /// <summary>
    /// Gets a <see cref="Span{T}"/> to the backing data if the backing group consists of a single contiguous memory buffer.
    /// Throws <see cref="InvalidOperationException"/> otherwise.
    /// </summary>
    /// <returns>The <see cref="Span{T}"/> referencing the memory area.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the backing group is discontiguous.
    /// </exception>
    [MethodImpl(InliningOptions.ShortMethod)]
    internal Span<T> DangerousGetSingleSpan() => this.FastMemoryGroup.Single().Span;

    /// <summary>
    /// Gets a <see cref="Memory{T}"/> to the backing data of if the backing group consists of a single contiguous memory buffer.
    /// Throws <see cref="InvalidOperationException"/> otherwise.
    /// </summary>
    /// <returns>The <see cref="Memory{T}"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the backing group is discontiguous.
    /// </exception>
    [MethodImpl(InliningOptions.ShortMethod)]
    internal Memory<T> DangerousGetSingleMemory() => this.FastMemoryGroup.Single();

    /// <summary>
    /// Swaps the contents of 'destination' with 'source' if the buffers are owned (1),
    /// copies the contents of 'source' to 'destination' otherwise (2). Buffers should be of same size in case 2!
    /// </summary>
    /// <param name="destination">The destination buffer.</param>
    /// <param name="source">The source buffer.</param>
    /// <exception cref="InvalidMemoryOperationException">Attempt to copy/swap incompatible buffers.</exception>
    internal static bool SwapOrCopyContent(Buffer2D<T> destination, Buffer2D<T> source)
    {
        bool swapped = false;
        if (MemoryGroup<T>.CanSwapContent(destination.FastMemoryGroup, source.FastMemoryGroup))
        {
            (destination.FastMemoryGroup, source.FastMemoryGroup) = (source.FastMemoryGroup, destination.FastMemoryGroup);
            destination.FastMemoryGroup.RecreateViewAfterSwap();
            source.FastMemoryGroup.RecreateViewAfterSwap();
            swapped = true;
        }
        else
        {
            long sourceLayoutLength = GetRequiredLength(source.Width, source.Height, source.RowStride);
            long destinationLayoutLength = GetRequiredLength(destination.Width, destination.Height, destination.RowStride);

            bool destinationCanRepresentSource = destination.FastMemoryGroup.TotalLength >= sourceLayoutLength;
            bool sourceCanRepresentDestination = source.FastMemoryGroup.TotalLength >= destinationLayoutLength;
            if (!destinationCanRepresentSource || !sourceCanRepresentDestination)
            {
                throw new InvalidMemoryOperationException(
                    "Trying to copy/swap incompatible buffers. This is most likely caused by applying an unsupported processor to wrapped-memory images.");
            }

            source.CopyTo(destination);
        }

        (destination.Size, source.Size) = (source.Size, destination.Size);
        (destination.RowStride, source.RowStride) = (source.RowStride, destination.RowStride);
        return swapped;
    }

    [MethodImpl(InliningOptions.ColdPath)]
    private void ThrowYOutOfRangeException(int y)
        => throw new ArgumentOutOfRangeException($"DangerousGetRowSpan({y}). Y was out of range. Height={this.Height}");

    [MethodImpl(InliningOptions.ShortMethod)]
    private static long GetRequiredLength(int width, int height, int stride)
        => checked(((long)(height - 1) * stride) + width);
}
