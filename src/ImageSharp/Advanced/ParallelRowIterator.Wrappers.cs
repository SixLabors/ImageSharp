// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Advanced
{
    /// <content>
    /// Utility methods for batched processing of pixel row intervals.
    /// Parallel execution is optimized for image processing based on values defined
    /// <see cref="ParallelExecutionSettings"/> or <see cref="Configuration"/>.
    /// Using this class is preferred over direct usage of <see cref="Parallel"/> utility methods.
    /// </content>
    public static partial class ParallelRowIterator
    {
        private readonly struct RowOperationWrapper<T>
            where T : struct, IRowOperation
        {
            private readonly int minY;
            private readonly int maxY;
            private readonly int stepY;
            private readonly T action;

            [MethodImpl(InliningOptions.ShortMethod)]
            public RowOperationWrapper(
                int minY,
                int maxY,
                int stepY,
                in T action)
            {
                this.minY = minY;
                this.maxY = maxY;
                this.stepY = stepY;
                this.action = action;
            }

            [MethodImpl(InliningOptions.ShortMethod)]
            public void Invoke(int i)
            {
                int yMin = this.minY + (i * this.stepY);

                if (yMin >= this.maxY)
                {
                    return;
                }

                int yMax = Math.Min(yMin + this.stepY, this.maxY);

                for (int y = yMin; y < yMax; y++)
                {
                    // Skip the safety copy when invoking a potentially impure method on a readonly field
                    Unsafe.AsRef(this.action).Invoke(y);
                }
            }
        }

        private readonly struct RowOperationWrapper<T, TBuffer>
            where T : struct, IRowOperation<TBuffer>
            where TBuffer : unmanaged
        {
            private readonly int minY;
            private readonly int maxY;
            private readonly int stepY;
            private readonly int width;
            private readonly MemoryAllocator allocator;
            private readonly T action;

            [MethodImpl(InliningOptions.ShortMethod)]
            public RowOperationWrapper(
                int minY,
                int maxY,
                int stepY,
                int width,
                MemoryAllocator allocator,
                in T action)
            {
                this.minY = minY;
                this.maxY = maxY;
                this.stepY = stepY;
                this.width = width;
                this.allocator = allocator;
                this.action = action;
            }

            [MethodImpl(InliningOptions.ShortMethod)]
            public void Invoke(int i)
            {
                int yMin = this.minY + (i * this.stepY);

                if (yMin >= this.maxY)
                {
                    return;
                }

                int yMax = Math.Min(yMin + this.stepY, this.maxY);

                using IMemoryOwner<TBuffer> buffer = this.allocator.Allocate<TBuffer>(this.width);

                Span<TBuffer> span = buffer.Memory.Span;

                for (int y = yMin; y < yMax; y++)
                {
                    Unsafe.AsRef(this.action).Invoke(y, span);
                }
            }
        }

        private readonly struct RowIntervalOperationWrapper<T>
            where T : struct, IRowIntervalOperation
        {
            private readonly int minY;
            private readonly int maxY;
            private readonly int stepY;
            private readonly T operation;

            [MethodImpl(InliningOptions.ShortMethod)]
            public RowIntervalOperationWrapper(
                int minY,
                int maxY,
                int stepY,
                in T operation)
            {
                this.minY = minY;
                this.maxY = maxY;
                this.stepY = stepY;
                this.operation = operation;
            }

            [MethodImpl(InliningOptions.ShortMethod)]
            public void Invoke(int i)
            {
                int yMin = this.minY + (i * this.stepY);

                if (yMin >= this.maxY)
                {
                    return;
                }

                int yMax = Math.Min(yMin + this.stepY, this.maxY);
                var rows = new RowInterval(yMin, yMax);

                // Skip the safety copy when invoking a potentially impure method on a readonly field
                Unsafe.AsRef(in this.operation).Invoke(in rows);
            }
        }

        private readonly struct RowIntervalOperationWrapper<T, TBuffer>
            where T : struct, IRowIntervalOperation<TBuffer>
            where TBuffer : unmanaged
        {
            private readonly int minY;
            private readonly int maxY;
            private readonly int stepY;
            private readonly int width;
            private readonly MemoryAllocator allocator;
            private readonly T operation;

            [MethodImpl(InliningOptions.ShortMethod)]
            public RowIntervalOperationWrapper(
                int minY,
                int maxY,
                int stepY,
                int width,
                MemoryAllocator allocator,
                in T operation)
            {
                this.minY = minY;
                this.maxY = maxY;
                this.stepY = stepY;
                this.width = width;
                this.allocator = allocator;
                this.operation = operation;
            }

            [MethodImpl(InliningOptions.ShortMethod)]
            public void Invoke(int i)
            {
                int yMin = this.minY + (i * this.stepY);

                if (yMin >= this.maxY)
                {
                    return;
                }

                int yMax = Math.Min(yMin + this.stepY, this.maxY);
                var rows = new RowInterval(yMin, yMax);

                using IMemoryOwner<TBuffer> buffer = this.allocator.Allocate<TBuffer>(this.width);

                Unsafe.AsRef(in this.operation).Invoke(in rows, buffer.Memory.Span);
            }
        }
    }
}
