// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Advanced
{
    /// <summary>
    /// Defines the contract for an action that operates on a row interval with a temporary buffer.
    /// </summary>
    /// <typeparam name="TBuffer">The type of buffer elements.</typeparam>
    public interface IRowIntervalAction<TBuffer>
        where TBuffer : unmanaged
    {
        /// <summary>
        /// Invokes the method passing the row interval and a buffer.
        /// </summary>
        /// <param name="rows">The row interval.</param>
        /// <param name="memory">The contiguous region of memory.</param>
        void Invoke(in RowInterval rows, Memory<TBuffer> memory);
    }

    internal readonly struct WrappingRowIntervalBufferAction<TBuffer>
        where TBuffer : unmanaged
    {
        private readonly WrappingRowIntervalInfo info;
        private readonly MemoryAllocator allocator;
        private readonly Action<RowInterval, Memory<TBuffer>> action;

        [MethodImpl(InliningOptions.ShortMethod)]
        public WrappingRowIntervalBufferAction(
            in WrappingRowIntervalInfo info,
            MemoryAllocator allocator,
            Action<RowInterval, Memory<TBuffer>> action)
        {
            this.info = info;
            this.allocator = allocator;
            this.action = action;
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
            this.action(rows, buffer.Memory);
        }
    }

    internal readonly struct WrappingRowIntervalBufferAction<T, TBuffer>
        where T : struct, IRowIntervalAction<TBuffer>
        where TBuffer : unmanaged
    {
        private readonly WrappingRowIntervalInfo info;
        private readonly MemoryAllocator allocator;
        private readonly T action;

        [MethodImpl(InliningOptions.ShortMethod)]
        public WrappingRowIntervalBufferAction(
            in WrappingRowIntervalInfo info,
            MemoryAllocator allocator,
            in T action)
        {
            this.info = info;
            this.allocator = allocator;
            this.action = action;
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

            Unsafe.AsRef(this.action).Invoke(in rows, buffer.Memory);
        }
    }
}
