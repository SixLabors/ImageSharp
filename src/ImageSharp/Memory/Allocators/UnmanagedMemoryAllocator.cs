// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using SixLabors.ImageSharp.Memory.Internals;

namespace SixLabors.ImageSharp.Memory
{
    internal class UnmanagedMemoryAllocator : MemoryAllocator
    {
        private readonly int bufferCapacityInBytes;

        public UnmanagedMemoryAllocator(int bufferCapacityInBytes)
        {
            this.bufferCapacityInBytes = bufferCapacityInBytes;
        }

        protected internal override int GetBufferCapacityInBytes() => this.bufferCapacityInBytes;

        public override IMemoryOwner<T> Allocate<T>(int length, AllocationOptions options = AllocationOptions.None)
        {
            var buffer = new UnmanagedBuffer<T>(length);
            if (options.Has(AllocationOptions.Clean))
            {
                buffer.GetSpan().Clear();
            }

            return buffer;
        }

        public override IManagedByteBuffer AllocateManagedByteBuffer(int length, AllocationOptions options = AllocationOptions.None)
        {
            throw new NotSupportedException();
        }
    }
}
