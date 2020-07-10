// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Memory
{
    internal static class MemoryGroupExtensions
    {
        internal static void Fill<T>(this IMemoryGroup<T> group, T value)
            where T : struct
        {
            foreach (Memory<T> memory in group)
            {
                memory.Span.Fill(value);
            }
        }

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
        /// Otherwise <see cref="ArgumentOutOfRangeException"/> is thrown.
        /// </summary>
        internal static Memory<T> GetBoundedSlice<T>(this IMemoryGroup<T> group, long start, int length)
            where T : struct
        {
            Guard.NotNull(group, nameof(group));
            Guard.IsTrue(group.IsValid, nameof(group), "Group must be valid!");
            Guard.MustBeGreaterThanOrEqualTo(length, 0, nameof(length));
            Guard.MustBeLessThan(start, group.TotalLength, nameof(start));

            int bufferIdx = (int)(start / group.BufferLength);

            if (bufferIdx < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(start));
            }

            if (bufferIdx >= group.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(start));
            }

            int bufferStart = (int)(start % group.BufferLength);
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
                target = target.Slice(fwd);
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
                source.Slice(0, fwd).CopyTo(cur.GetSpan(fwd));
                cur.Forward(fwd);
                source = source.Slice(fwd);
            }
        }

        internal static void CopyTo<T>(this IMemoryGroup<T> source, IMemoryGroup<T> target)
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
}
