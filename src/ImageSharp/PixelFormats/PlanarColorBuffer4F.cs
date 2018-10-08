// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Numerics;

using SixLabors.Memory;

namespace SixLabors.ImageSharp.PixelFormats
{
    /// <summary>
    /// AOS (Array Of Structures) / Planar Image Layout representation of a 4-channel <see cref="float"/>/<see cref="Vector4"/> color buffer.
    /// <see>https://en.wikipedia.org/wiki/AOS_and_SOA</see>
    /// </summary>
    internal class PlanarColorBuffer4F : IDisposable
    {
        private IMemoryOwner<float> buffer;

        private Memory<float> x;
        private Memory<float> y;
        private Memory<float> z;
        private Memory<float> w;

        public PlanarColorBuffer4F(int length, MemoryAllocator allocator, AllocationOptions allocationOptions)
        {
            DebugGuard.MustBeGreaterThanOrEqualTo(length, 0, nameof(length));
            DebugGuard.NotNull(allocator, nameof(allocator));

            this.Length = length;
            this.buffer = allocator.Allocate<float>(4 * length, allocationOptions);
            Memory<float> memory = this.buffer.Memory;

            this.x = memory.Slice(0, length);
            this.y = memory.Slice(length, length);
            this.z = memory.Slice(2 * length, length);
            this.w = memory.Slice(3 * length, length);
        }

        public int Length { get; }

        public Span<float> Data => this.buffer.Memory.Span;

        public Span<float> X => this.x.Span;

        public Span<float> Y => this.y.Span;

        public Span<float> Z => this.z.Span;

        public Span<float> W => this.w.Span;

        public void Dispose()
        {
            this.buffer?.Dispose();
            this.buffer = null;

            // Trigger invalid behavior by clearing all contents,
            // rather than doing IsDisposed? checks in all property getters:
            this.x = default;
            this.y = default;
            this.z = default;
            this.w = default;
        }
    }
}