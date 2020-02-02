// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Memory
{
    internal static class MemoryGroupExtensions
    {
        public static void CopyTo<T>(this IMemoryGroup<T> source, IMemoryGroup<T> target)
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

        public static void TransformTo<T>(
            this IMemoryGroup<T> source,
            IMemoryGroup<T> target,
            TransformItemsDelegate<T> transform)
            where T : struct
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
            var srcCur = new MemoryGroupCursor<T>(source);
            var trgCur = new MemoryGroupCursor<T>(target);

            while (position < source.TotalLength)
            {
                int fwd = Math.Min(srcCur.LookAhead(), trgCur.LookAhead());
                Span<T> srcSpan = srcCur.GetSpan(fwd);
                Span<T> trgSpan = trgCur.GetSpan(fwd);
                transform(srcSpan, trgSpan);

                srcCur.Forward(fwd);
                trgCur.Forward(fwd);
                position += fwd;
            }
        }

        public static void TransformInplace<T>(
            this IMemoryGroup<T> memoryGroup,
            TransformItemsInplaceDelegate<T> transform)
            where T : struct
        {
            foreach (Memory<T> memory in memoryGroup)
            {
                transform(memory.Span);
            }
        }

        public static bool IsEmpty<T>(this IMemoryGroup<T> group)
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
