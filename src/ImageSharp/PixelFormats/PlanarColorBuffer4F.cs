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
        private IMemoryOwner<float> data;

        public PlanarColorBuffer4F(int length, MemoryAllocator allocator, AllocationOptions allocationOptions)
        {
            DebugGuard.MustBeGreaterThanOrEqualTo(length, 0, nameof(length));
            DebugGuard.NotNull(allocator, nameof(allocator));

            this.Length = length;
            this.data = allocator.Allocate<float>(4 * length, allocationOptions);
            this.Memory = this.data.Memory;

            this.X = this.Memory.Slice(0, length);
            this.Y = this.Memory.Slice(length, length);
            this.Z = this.Memory.Slice(2 * length, length);
            this.W = this.Memory.Slice(3 * length, length);
        }

        public int Length { get; }

        public Memory<float> Memory { get; }

        public Memory<float> X { get; }

        public Memory<float> Y { get; }

        public Memory<float> Z { get; }

        public Memory<float> W { get; }

        public void Dispose()
        {
            this.data?.Dispose();
            this.data = null;
        }
    }
}