// Copyright (c) Six Labors and contributors.
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
        private readonly struct WrappingRowIntervalInfo
        {
            public readonly int MinY;
            public readonly int MaxY;
            public readonly int StepY;
            public readonly int MaxX;

            public WrappingRowIntervalInfo(int minY, int maxY, int stepY)
                : this(minY, maxY, stepY, 0)
            {
            }

            public WrappingRowIntervalInfo(int minY, int maxY, int stepY, int maxX)
            {
                this.MinY = minY;
                this.MaxY = maxY;
                this.StepY = stepY;
                this.MaxX = maxX;
            }
        }

        private readonly struct WrappingRowIntervalOperation<T>
            where T : struct, IRowIntervalOperation
        {
            private readonly WrappingRowIntervalInfo info;
            private readonly T operation;

            [MethodImpl(InliningOptions.ShortMethod)]
            public WrappingRowIntervalOperation(in WrappingRowIntervalInfo info, in T operation)
            {
                this.info = info;
                this.operation = operation;
            }

            [MethodImpl(InliningOptions.ShortMethod)]
            public void Invoke(int i)
            {
                int yMin = this.info.MinY + (i * this.info.StepY);

                if (yMin >= this.info.MaxY)
                {
                    return;
                }

                int yMax = Math.Min(yMin + this.info.StepY, this.info.MaxY);
                var rows = new RowInterval(yMin, yMax);

                // Skip the safety copy when invoking a potentially impure method on a readonly field
                Unsafe.AsRef(this.operation).Invoke(in rows);
            }
        }

        private readonly struct WrappingRowIntervalBufferOperation<T, TBuffer>
            where T : struct, IRowIntervalOperation<TBuffer>
            where TBuffer : unmanaged
        {
            private readonly WrappingRowIntervalInfo info;
            private readonly MemoryAllocator allocator;
            private readonly T operation;

            [MethodImpl(InliningOptions.ShortMethod)]
            public WrappingRowIntervalBufferOperation(
                in WrappingRowIntervalInfo info,
                MemoryAllocator allocator,
                in T operation)
            {
                this.info = info;
                this.allocator = allocator;
                this.operation = operation;
            }

            [MethodImpl(InliningOptions.ShortMethod)]
            public void Invoke(int i)
            {
                int yMin = this.info.MinY + (i * this.info.StepY);

                if (yMin >= this.info.MaxY)
                {
                    return;
                }

                int yMax = Math.Min(yMin + this.info.StepY, this.info.MaxY);
                var rows = new RowInterval(yMin, yMax);

                using IMemoryOwner<TBuffer> buffer = this.allocator.Allocate<TBuffer>(this.info.MaxX);

                Unsafe.AsRef(this.operation).Invoke(in rows, buffer.Memory.Span);
            }
        }
    }
}
