// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Advanced
{
    /// <summary>
    /// Defines the contract for an action that operates on a row with a temporary buffer.
    /// </summary>
    /// <typeparam name="TBuffer">The type of buffer elements.</typeparam>
    public interface IRowAction<TBuffer>
        where TBuffer : unmanaged
    {
        /// <summary>
        /// Invokes the method passing the row and a buffer.
        /// </summary>
        /// <param name="y">The row y coordinate.</param>
        /// <param name="span">The contiguous region of memory.</param>
        void Invoke(int y, Span<TBuffer> span);
    }

    internal readonly struct WrappingRowAction<T, TBuffer>
        where T : struct, IRowAction<TBuffer>
        where TBuffer : unmanaged
    {
        public readonly int MinY;
        public readonly int MaxY;
        public readonly int StepY;
        public readonly int MaxX;

        private readonly MemoryAllocator allocator;
        private readonly T action;

        [MethodImpl(InliningOptions.ShortMethod)]
        public WrappingRowAction(
            int minY,
            int maxY,
            int stepY,
            MemoryAllocator allocator,
            in T action)
            : this(minY, maxY, stepY, 0, allocator, action)
        {
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public WrappingRowAction(
            int minY,
            int maxY,
            int stepY,
            int maxX,
            MemoryAllocator allocator,
            in T action)
        {
            this.MinY = minY;
            this.MaxY = maxY;
            this.StepY = stepY;
            this.MaxX = maxX;
            this.allocator = allocator;
            this.action = action;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public void Invoke(int i)
        {
            int yMin = this.MinY + (i * this.StepY);

            if (yMin >= this.MaxY)
            {
                return;
            }

            int yMax = Math.Min(yMin + this.StepY, this.MaxY);

            using IMemoryOwner<TBuffer> buffer = this.allocator.Allocate<TBuffer>(this.MaxX);

            Span<TBuffer> span = buffer.Memory.Span;

            for (int y = yMin; y < yMax; y++)
            {
                Unsafe.AsRef(this.action).Invoke(y, span);
            }
        }
    }
}
