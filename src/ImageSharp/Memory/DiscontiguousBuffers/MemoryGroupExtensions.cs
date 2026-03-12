// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Memory;

internal static class MemoryGroupExtensions
{
    /// <summary>
    /// Fills the elements of this <see cref="IMemoryGroup{T}"/> with the specified value.
    /// </summary>
    /// <typeparam name="T">The type of element.</typeparam>
    /// <param name="group">The group to fill.</param>
    /// <param name="value">The value to assign to each element of the group.</param>
    internal static void Fill<T>(this IMemoryGroup<T> group, T value)
        where T : struct
    {
        foreach (Memory<T> memory in group)
        {
            memory.Span.Fill(value);
        }
    }

    /// <summary>
    /// Clears the contents of this <see cref="IMemoryGroup{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of element.</typeparam>
    /// <param name="group">The group to clear.</param>
    internal static void Clear<T>(this IMemoryGroup<T> group)
        where T : struct
    {
        foreach (Memory<T> memory in group)
        {
            memory.Span.Clear();
        }
    }

    /// <summary>
    /// Returns a slice that is expected to be within the bounds of a single buffer.
    /// </summary>
    /// <typeparam name="T">The type of element.</typeparam>
    /// <param name="group">The group.</param>
    /// <param name="start">The start index of the slice.</param>
    /// <param name="length">The length of the slice.</param>
    /// <exception cref="ArgumentOutOfRangeException">Slice is out of bounds.</exception>
    /// <returns>The <see cref="MemoryGroup{T}"/> slice.</returns>
    internal static Memory<T> GetBoundedMemorySlice<T>(this IMemoryGroup<T> group, long start, int length)
        where T : struct
    {
        Guard.NotNull(group, nameof(group));
        Guard.IsTrue(group.IsValid, nameof(group), "Group must be valid!");
        Guard.MustBeGreaterThanOrEqualTo(length, 0, nameof(length));
        Guard.MustBeLessThan(start, group.TotalLength, nameof(start));

        int bufferIdx = (int)Math.DivRem(start, group.BufferLength, out long bufferStartLong);
        int bufferStart = (int)bufferStartLong;

        // if (bufferIdx < 0 || bufferIdx >= group.Count)
        if ((uint)bufferIdx >= group.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(start));
        }

        int bufferEnd = bufferStart + length;
        Memory<T> memory = group[bufferIdx];

        if (bufferEnd > memory.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        return memory.Slice(bufferStart, length);
    }

    /// <summary>
    /// Copies a 2D logical region from <paramref name="source"/> into <paramref name="target"/>
    /// using the provided source and target strides.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The source memory group.</param>
    /// <param name="sourceStride">Elements between source row starts.</param>
    /// <param name="target">The destination span.</param>
    /// <param name="targetStride">Elements between destination row starts.</param>
    /// <param name="width">The logical row width to copy.</param>
    /// <param name="height">The number of rows to copy.</param>
    internal static void CopyTo<T>(
        this IMemoryGroup<T> source,
        int sourceStride,
        Span<T> target,
        int targetStride,
        int width,
        int height)
        where T : struct
    {
        Guard.NotNull(source, nameof(source));
        Guard.MustBeGreaterThanOrEqualTo(width, 0, nameof(width));
        Guard.MustBeGreaterThanOrEqualTo(height, 0, nameof(height));
        Guard.MustBeGreaterThanOrEqualTo(sourceStride, width, nameof(sourceStride));
        Guard.MustBeGreaterThanOrEqualTo(targetStride, width, nameof(targetStride));

        long sourceRequired = height == 0 ? 0 : checked(((long)(height - 1) * sourceStride) + width);
        long targetRequired = height == 0 ? 0 : checked(((long)(height - 1) * targetStride) + width);
        Guard.MustBeGreaterThanOrEqualTo(source.TotalLength, sourceRequired, nameof(source));
        Guard.MustBeGreaterThanOrEqualTo(target.Length, targetRequired, nameof(target));

        if (width == 0 || height == 0)
        {
            return;
        }

        MemoryGroupCursor<T> sourceCursor = new(source);
        int sourceSkip = sourceStride - width;

        for (int y = 0; y < height; y++)
        {
            int rowStart = checked(y * targetStride);
            Span<T> destinationRow = target.Slice(rowStart, width);
            CopyFromCursorToSpan(ref sourceCursor, destinationRow);

            // Trailing padding after the last row is optional, so only skip between rows.
            if (y < height - 1)
            {
                ForwardCursor(ref sourceCursor, sourceSkip);
            }
        }
    }

    /// <summary>
    /// Copies a 2D logical region from <paramref name="source"/> into <paramref name="target"/>
    /// using the provided source and target strides.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The source memory group.</param>
    /// <param name="sourceStride">Elements between source row starts.</param>
    /// <param name="target">The destination memory group.</param>
    /// <param name="targetStride">Elements between destination row starts.</param>
    /// <param name="width">The logical row width to copy.</param>
    /// <param name="height">The number of rows to copy.</param>
    internal static void CopyTo<T>(
        this IMemoryGroup<T> source,
        int sourceStride,
        IMemoryGroup<T> target,
        int targetStride,
        int width,
        int height)
        where T : struct
    {
        Guard.NotNull(source, nameof(source));
        Guard.NotNull(target, nameof(target));
        Guard.IsTrue(source.IsValid, nameof(source), "Source group must be valid.");
        Guard.IsTrue(target.IsValid, nameof(target), "Target group must be valid.");
        Guard.MustBeGreaterThanOrEqualTo(width, 0, nameof(width));
        Guard.MustBeGreaterThanOrEqualTo(height, 0, nameof(height));
        Guard.MustBeGreaterThanOrEqualTo(sourceStride, width, nameof(sourceStride));
        Guard.MustBeGreaterThanOrEqualTo(targetStride, width, nameof(targetStride));

        long sourceRequired = height == 0 ? 0 : checked(((long)(height - 1) * sourceStride) + width);
        long targetRequired = height == 0 ? 0 : checked(((long)(height - 1) * targetStride) + width);
        Guard.MustBeGreaterThanOrEqualTo(source.TotalLength, sourceRequired, nameof(source));
        Guard.MustBeGreaterThanOrEqualTo(target.TotalLength, targetRequired, nameof(target));

        if (width == 0 || height == 0)
        {
            return;
        }

        MemoryGroupCursor<T> sourceCursor = new(source);
        MemoryGroupCursor<T> targetCursor = new(target);
        int sourceSkip = sourceStride - width;
        int targetSkip = targetStride - width;

        for (int y = 0; y < height; y++)
        {
            CopyFromCursorToCursor(ref sourceCursor, ref targetCursor, width);

            // Trailing padding after the last row is optional, so only skip between rows.
            if (y < height - 1)
            {
                ForwardCursor(ref sourceCursor, sourceSkip);
                ForwardCursor(ref targetCursor, targetSkip);
            }
        }
    }

    private static void CopyFromCursorToCursor<T>(
        ref MemoryGroupCursor<T> source,
        ref MemoryGroupCursor<T> target,
        int count)
        where T : struct
    {
        int remaining = count;
        while (remaining > 0)
        {
            int fwd = Math.Min(remaining, Math.Min(source.LookAhead(), target.LookAhead()));
            source.GetSpan(fwd).CopyTo(target.GetSpan(fwd));
            source.Forward(fwd);
            target.Forward(fwd);
            remaining -= fwd;
        }
    }

    private static void CopyFromCursorToSpan<T>(ref MemoryGroupCursor<T> source, Span<T> target)
        where T : struct
    {
        int remaining = target.Length;
        while (remaining > 0)
        {
            int copied = target.Length - remaining;
            int fwd = Math.Min(remaining, source.LookAhead());
            source.GetSpan(fwd).CopyTo(target[copied..]);
            source.Forward(fwd);
            remaining -= fwd;
        }
    }

    private static void ForwardCursor<T>(ref MemoryGroupCursor<T> cursor, int steps)
        where T : struct
    {
        int remaining = steps;
        while (remaining > 0)
        {
            int fwd = Math.Min(remaining, cursor.LookAhead());
            cursor.Forward(fwd);
            remaining -= fwd;
        }
    }

    private struct MemoryGroupCursor<T>
        where T : struct
    {
        private readonly IMemoryGroup<T> memoryGroup;

        private int bufferIndex;

        private int elementIndex;

        public MemoryGroupCursor(IMemoryGroup<T> memoryGroup)
        {
            this.memoryGroup = memoryGroup;
            this.bufferIndex = 0;
            this.elementIndex = 0;
        }

        private bool IsAtLastBuffer => this.bufferIndex == this.memoryGroup.Count - 1;

        private int CurrentBufferLength => this.memoryGroup[this.bufferIndex].Length;

        public Span<T> GetSpan(int length)
        {
            return this.memoryGroup[this.bufferIndex].Span.Slice(this.elementIndex, length);
        }

        public int LookAhead()
        {
            return this.CurrentBufferLength - this.elementIndex;
        }

        public void Forward(int steps)
        {
            int nextIdx = this.elementIndex + steps;
            int currentBufferLength = this.CurrentBufferLength;

            if (nextIdx < currentBufferLength)
            {
                this.elementIndex = nextIdx;
            }
            else if (nextIdx == currentBufferLength)
            {
                this.bufferIndex++;
                this.elementIndex = 0;
            }
            else
            {
                // If we get here, it indicates a bug in CopyTo<T>:
                throw new ArgumentException("Can't forward multiple buffers!", nameof(steps));
            }
        }
    }
}
