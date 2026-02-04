// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory.Internals;

namespace SixLabors.ImageSharp.Memory;

/// <summary>
/// Represents discontinuous group of multiple uniformly-sized memory segments.
/// The underlying buffers may change with time, therefore it's not safe to expose them directly on
/// <see cref="Image{TPixel}"/> and <see cref="ImageFrame{TPixel}"/>.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
internal abstract partial class MemoryGroup<T> : IMemoryGroup<T>, IDisposable
    where T : struct
{
    private static readonly int ElementSize = Unsafe.SizeOf<T>();

    private MemoryGroupSpanCache memoryGroupSpanCache;

    private MemoryGroup(int bufferLength, long totalLength)
    {
        this.BufferLength = bufferLength;
        this.TotalLength = totalLength;
    }

    /// <inheritdoc />
    public abstract int Count { get; }

    /// <inheritdoc />
    public int BufferLength { get; }

    /// <inheritdoc />
    public long TotalLength { get; }

    /// <inheritdoc />
    public bool IsValid { get; private set; } = true;

    public MemoryGroupView<T> View { get; private set; } = null!;

    /// <inheritdoc />
    public abstract Memory<T> this[int index] { get; }

    /// <inheritdoc />
    public abstract void Dispose();

    /// <inheritdoc />
    public abstract MemoryGroupEnumerator<T> GetEnumerator();

    /// <inheritdoc />
    IEnumerator<Memory<T>> IEnumerable<Memory<T>>.GetEnumerator()
    {
        /* This method is implemented in each derived class.
         * Implementing the method here as non-abstract and throwing,
         * then reimplementing it explicitly in each derived class, is
         * a workaround for the lack of support for abstract explicit
         * interface method implementations in C#. */
        throw new NotImplementedException($"The type {this.GetType()} needs to override IEnumerable<Memory<T>>.GetEnumerator()");
    }

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<Memory<T>>)this).GetEnumerator();

    /// <summary>
    /// Creates a new memory group, allocating it's buffers with the provided allocator.
    /// </summary>
    /// <param name="allocator">The <see cref="MemoryAllocator"/> to use.</param>
    /// <param name="totalLengthInElements">The total length of the buffer.</param>
    /// <param name="bufferAlignmentInElements">The expected alignment (eg. to make sure image rows fit into single buffers).</param>
    /// <param name="options">The <see cref="AllocationOptions"/>.</param>
    /// <returns>A new <see cref="MemoryGroup{T}"/>.</returns>
    /// <exception cref="InvalidMemoryOperationException">Thrown when 'blockAlignment' converted to bytes is greater than the buffer capacity of the allocator.</exception>
    public static MemoryGroup<T> Allocate(
        MemoryAllocator allocator,
        long totalLengthInElements,
        int bufferAlignmentInElements,
        AllocationOptions options = AllocationOptions.None)
    {
        int bufferCapacityInBytes = allocator.GetBufferCapacityInBytes();
        Guard.NotNull(allocator, nameof(allocator));

        if (totalLengthInElements < 0)
        {
            InvalidMemoryOperationException.ThrowNegativeAllocationException(totalLengthInElements);
        }

        int blockCapacityInElements = bufferCapacityInBytes / ElementSize;
        if (bufferAlignmentInElements < 0 || bufferAlignmentInElements > blockCapacityInElements)
        {
            InvalidMemoryOperationException.ThrowInvalidAlignmentException(bufferAlignmentInElements);
        }

        if (totalLengthInElements == 0)
        {
            IMemoryOwner<T>[] buffers0 = [allocator.Allocate<T>(0, options)];
            return new Owned(buffers0, 0, 0, true);
        }

        int numberOfAlignedSegments = blockCapacityInElements / bufferAlignmentInElements;
        int bufferLength = numberOfAlignedSegments * bufferAlignmentInElements;
        if (totalLengthInElements > 0 && totalLengthInElements < bufferLength)
        {
            bufferLength = (int)totalLengthInElements;
        }

        int sizeOfLastBuffer = (int)(totalLengthInElements % bufferLength);
        long bufferCount = totalLengthInElements / bufferLength;

        if (sizeOfLastBuffer == 0)
        {
            sizeOfLastBuffer = bufferLength;
        }
        else
        {
            bufferCount++;
        }

        IMemoryOwner<T>[] buffers = new IMemoryOwner<T>[bufferCount];
        for (int i = 0; i < buffers.Length - 1; i++)
        {
            buffers[i] = allocator.Allocate<T>(bufferLength, options);
        }

        if (bufferCount > 0)
        {
            buffers[^1] = allocator.Allocate<T>(sizeOfLastBuffer, options);
        }

        return new Owned(buffers, bufferLength, totalLengthInElements, true);
    }

    public static MemoryGroup<T> CreateContiguous(IMemoryOwner<T> buffer, bool clear)
    {
        if (clear)
        {
            buffer.GetSpan().Clear();
        }

        int length = buffer.Memory.Length;
        IMemoryOwner<T>[] buffers = [buffer];
        return new Owned(buffers, length, length, true);
    }

    public static bool TryAllocate(
        UniformUnmanagedMemoryPool pool,
        long totalLengthInElements,
        int bufferAlignmentInElements,
        AllocationOptions options,
        [NotNullWhen(true)] out MemoryGroup<T>? memoryGroup)
    {
        Guard.NotNull(pool, nameof(pool));
        Guard.MustBeGreaterThanOrEqualTo(totalLengthInElements, 0, nameof(totalLengthInElements));
        Guard.MustBeGreaterThanOrEqualTo(bufferAlignmentInElements, 0, nameof(bufferAlignmentInElements));

        int blockCapacityInElements = pool.BufferLength / ElementSize;

        if (bufferAlignmentInElements > blockCapacityInElements)
        {
            memoryGroup = null;
            return false;
        }

        if (totalLengthInElements == 0)
        {
            throw new InvalidMemoryOperationException("Allocating 0 length buffer from UniformByteArrayPool is disallowed");
        }

        int numberOfAlignedSegments = blockCapacityInElements / bufferAlignmentInElements;
        int bufferLength = numberOfAlignedSegments * bufferAlignmentInElements;
        if (totalLengthInElements > 0 && totalLengthInElements < bufferLength)
        {
            bufferLength = (int)totalLengthInElements;
        }

        int sizeOfLastBuffer = (int)(totalLengthInElements % bufferLength);
        int bufferCount = (int)(totalLengthInElements / bufferLength);

        if (sizeOfLastBuffer == 0)
        {
            sizeOfLastBuffer = bufferLength;
        }
        else
        {
            bufferCount++;
        }

        UnmanagedMemoryHandle[]? arrays = pool.Rent(bufferCount);

        if (arrays == null)
        {
            // Pool is full
            memoryGroup = null;
            return false;
        }

        memoryGroup = new Owned(pool, arrays, bufferLength, totalLengthInElements, sizeOfLastBuffer, options);
        return true;
    }

    public static MemoryGroup<T> Wrap(params Memory<T>[] source)
    {
        int bufferLength = source.Length > 0 ? source[0].Length : 0;
        for (int i = 1; i < source.Length - 1; i++)
        {
            if (source[i].Length != bufferLength)
            {
                throw new InvalidMemoryOperationException("Wrap: buffers should be uniformly sized!");
            }
        }

        if (source.Length > 0 && source[^1].Length > bufferLength)
        {
            throw new InvalidMemoryOperationException("Wrap: the last buffer is too large!");
        }

        long totalLength = bufferLength > 0 ? ((long)bufferLength * (source.Length - 1)) + source[^1].Length : 0;

        return new Consumed(source, bufferLength, totalLength);
    }

    public static MemoryGroup<T> Wrap(params IMemoryOwner<T>[] source)
    {
        int bufferLength = source.Length > 0 ? source[0].Memory.Length : 0;
        for (int i = 1; i < source.Length - 1; i++)
        {
            if (source[i].Memory.Length != bufferLength)
            {
                throw new InvalidMemoryOperationException("Wrap: buffers should be uniformly sized!");
            }
        }

        if (source.Length > 0 && source[^1].Memory.Length > bufferLength)
        {
            throw new InvalidMemoryOperationException("Wrap: the last buffer is too large!");
        }

        long totalLength = bufferLength > 0 ? ((long)bufferLength * (source.Length - 1)) + source[^1].Memory.Length : 0;

        return new Owned(source, bufferLength, totalLength, false);
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    public unsafe Span<T> GetRowSpanCoreUnsafe(int y, int width)
    {
        switch (this.memoryGroupSpanCache.Mode)
        {
            case SpanCacheMode.SingleArray:
            {
                ref byte b0 = ref MemoryMarshal.GetReference<byte>(this.memoryGroupSpanCache.SingleArray);
                ref T e0 = ref Unsafe.As<byte, T>(ref b0);
                e0 = ref Unsafe.Add(ref e0, (uint)(y * width));
                return MemoryMarshal.CreateSpan(ref e0, width);
            }

            case SpanCacheMode.SinglePointer:
            {
                void* start = Unsafe.Add<T>(this.memoryGroupSpanCache.SinglePointer, y * width);
                return new Span<T>(start, width);
            }

            case SpanCacheMode.MultiPointer:
            {
                this.GetMultiBufferPosition(y, width, out int bufferIdx, out int bufferStart);
                void* start = Unsafe.Add<T>(this.memoryGroupSpanCache.MultiPointer[bufferIdx], bufferStart);
                return new Span<T>(start, width);
            }

            default:
            {
                this.GetMultiBufferPosition(y, width, out int bufferIdx, out int bufferStart);
                return this[bufferIdx].Span.Slice(bufferStart, width);
            }
        }
    }

    /// <summary>
    /// Returns the slice of the buffer starting at global index <paramref name="start"/> that goes until the end of the buffer.
    /// </summary>
    public Span<T> GetRemainingSliceOfBuffer(long start)
    {
        long bufferIdx = Math.DivRem(start, this.BufferLength, out long bufferStart);
        Memory<T> memory = this[(int)bufferIdx];
        return memory.Span[(int)bufferStart..];
    }

    public static bool CanSwapContent(MemoryGroup<T> target, MemoryGroup<T> source) =>
        source is Owned { Swappable: true } && target is Owned { Swappable: true };

    public virtual void RecreateViewAfterSwap()
    {
    }

    public virtual void IncreaseRefCounts()
    {
    }

    public virtual void DecreaseRefCounts()
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void GetMultiBufferPosition(int y, int width, out int bufferIdx, out int bufferStart)
    {
        long start = y * (long)width;
        long bufferIdxLong = Math.DivRem(start, this.BufferLength, out long bufferStartLong);
        bufferIdx = (int)bufferIdxLong;
        bufferStart = (int)bufferStartLong;
    }
}
