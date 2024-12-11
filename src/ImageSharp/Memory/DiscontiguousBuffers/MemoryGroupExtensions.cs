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

    internal static void CopyTo<T>(this IMemoryGroup<T> source, Span<T> target)
        where T : struct
    {
        Guard.NotNull(source, nameof(source));
        Guard.MustBeGreaterThanOrEqualTo(target.Length, source.TotalLength, nameof(target));

        var cur = new MemoryGroupCursor<T>(source);
        long position = 0;
        while (position < source.TotalLength)
        {
            int fwd = Math.Min(cur.LookAhead(), target.Length);
            cur.GetSpan(fwd).CopyTo(target);

            cur.Forward(fwd);
            target = target[fwd..];
            position += fwd;
        }
    }

    internal static void CopyTo<T>(this Span<T> source, IMemoryGroup<T> target)
        where T : struct
        => CopyTo((ReadOnlySpan<T>)source, target);

    internal static void CopyTo<T>(this ReadOnlySpan<T> source, IMemoryGroup<T> target)
        where T : struct
    {
        Guard.NotNull(target, nameof(target));
        Guard.MustBeGreaterThanOrEqualTo(target.TotalLength, source.Length, nameof(target));

        var cur = new MemoryGroupCursor<T>(target);

        while (!source.IsEmpty)
        {
            int fwd = Math.Min(cur.LookAhead(), source.Length);
            source[..fwd].CopyTo(cur.GetSpan(fwd));
            cur.Forward(fwd);
            source = source[fwd..];
        }
    }

    internal static void CopyTo<T>(this IMemoryGroup<T>? source, IMemoryGroup<T>? target)
        where T : struct
    {
        Guard.NotNull(source, nameof(source));
        Guard.NotNull(target, nameof(target));
        Guard.IsTrue(source.IsValid, nameof(source), "Source group must be valid.");
        Guard.IsTrue(target.IsValid, nameof(target), "Target group must be valid.");
        Guard.MustBeLessThanOrEqualTo(source.TotalLength, target.TotalLength, "Destination buffer too short!");

        if (source.IsEmpty())
        {
            return;
        }

        long position = 0;
        var srcCur = new MemoryGroupCursor<T>(source);
        var trgCur = new MemoryGroupCursor<T>(target);

        while (position < source.TotalLength)
        {
            int fwd = Math.Min(srcCur.LookAhead(), trgCur.LookAhead());
            Span<T> srcSpan = srcCur.GetSpan(fwd);
            Span<T> trgSpan = trgCur.GetSpan(fwd);
            srcSpan.CopyTo(trgSpan);

            srcCur.Forward(fwd);
            trgCur.Forward(fwd);
            position += fwd;
        }
    }

    internal static void TransformTo<TSource, TTarget>(
        this IMemoryGroup<TSource> source,
        IMemoryGroup<TTarget> target,
        TransformItemsDelegate<TSource, TTarget> transform)
        where TSource : struct
        where TTarget : struct
    {
        Guard.NotNull(source, nameof(source));
        Guard.NotNull(target, nameof(target));
        Guard.NotNull(transform, nameof(transform));
        Guard.IsTrue(source.IsValid, nameof(source), "Source group must be valid.");
        Guard.IsTrue(target.IsValid, nameof(target), "Target group must be valid.");
        Guard.MustBeLessThanOrEqualTo(source.TotalLength, target.TotalLength, "Destination buffer too short!");

        if (source.IsEmpty())
        {
            return;
        }

        long position = 0;
        var srcCur = new MemoryGroupCursor<TSource>(source);
        var trgCur = new MemoryGroupCursor<TTarget>(target);

        while (position < source.TotalLength)
        {
            int fwd = Math.Min(srcCur.LookAhead(), trgCur.LookAhead());
            Span<TSource> srcSpan = srcCur.GetSpan(fwd);
            Span<TTarget> trgSpan = trgCur.GetSpan(fwd);
            transform(srcSpan, trgSpan);

            srcCur.Forward(fwd);
            trgCur.Forward(fwd);
            position += fwd;
        }
    }

    internal static void TransformInplace<T>(
        this IMemoryGroup<T> memoryGroup,
        TransformItemsInplaceDelegate<T> transform)
        where T : struct
    {
        foreach (Memory<T> memory in memoryGroup)
        {
            transform(memory.Span);
        }
    }

    internal static bool IsEmpty<T>(this IMemoryGroup<T> group)
        where T : struct
        => group.Count == 0;

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
