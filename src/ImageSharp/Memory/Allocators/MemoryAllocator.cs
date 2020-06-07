// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;

namespace SixLabors.ImageSharp.Memory
{
    /// <summary>
    /// Memory managers are used to allocate memory for image processing operations.
    /// </summary>
    public abstract class MemoryAllocator
    {
        /// <summary>
        /// Gets the length of the largest contiguous buffer that can be handled by this allocator instance in bytes.
        /// </summary>
        /// <returns>The length of the largest contiguous buffer that can be handled by this allocator instance.</returns>
        protected internal abstract int GetBufferCapacityInBytes();

        /// <summary>
        /// Allocates an <see cref="IMemoryOwner{T}" />, holding a <see cref="System.Memory{T}"/> of length <paramref name="length"/>.
        /// </summary>
        /// <typeparam name="T">Type of the data stored in the buffer.</typeparam>
        /// <param name="length">Size of the buffer to allocate.</param>
        /// <param name="options">The allocation options.</param>
        /// <returns>A buffer of values of type <typeparamref name="T"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">When length is zero or negative.</exception>
        /// <exception cref="InvalidMemoryOperationException">When length is over the capacity of the allocator.</exception>
        public abstract IMemoryOwner<T> Allocate<T>(int length, AllocationOptions options = AllocationOptions.None)
            where T : struct;

        /// <summary>
        /// Allocates an <see cref="IManagedByteBuffer"/>.
        /// </summary>
        /// <param name="length">The requested buffer length.</param>
        /// <param name="options">The allocation options.</param>
        /// <returns>The <see cref="IManagedByteBuffer"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">When length is zero or negative.</exception>
        /// <exception cref="InvalidMemoryOperationException">When length is over the capacity of the allocator.</exception>
        public abstract IManagedByteBuffer AllocateManagedByteBuffer(int length, AllocationOptions options = AllocationOptions.None);

        /// <summary>
        /// Releases all retained resources not being in use.
        /// Eg: by resetting array pools and letting GC to free the arrays.
        /// </summary>
        public virtual void ReleaseRetainedResources()
        {
        }
    }
}
