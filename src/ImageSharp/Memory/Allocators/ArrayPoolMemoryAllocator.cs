// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Buffers;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Memory.Allocators.Internals;
using SixLabors.ImageSharp.Memory.Internals;

namespace SixLabors.ImageSharp.Memory
{
    /// <summary>
    /// Implements <see cref="MemoryAllocator"/> by allocating memory from <see cref="ArrayPool{T}"/>.
    /// </summary>
    public sealed partial class ArrayPoolMemoryAllocator : MemoryAllocator
    {
        /// <summary>
        /// The upper threshold to pool arrays the shared buffer. 1MB
        /// This matches the upper pooling length of <see cref="ArrayPool{T}.Shared"/>.
        /// </summary>
        private const int SharedPoolThresholdInBytes = 1024 * 1024;

        /// <summary>
        /// The default value for the maximum size of pooled arrays in bytes. 2MB.
        /// </summary>
        internal const int DefaultMaxArrayLengthInBytes = 2 * SharedPoolThresholdInBytes;

        /// <summary>
        /// The default bucket count for <see cref="largeArrayPool"/>.
        /// </summary>
        private const int DefaultMaxArraysPerBucket = 16;

        /// <summary>
        /// The default maximum length of the largest contiguous buffer that can be handled
        /// by the large allocator.
        /// </summary>
        private const int DefaultMaxContiguousArrayLengthInBytes = DefaultMaxArrayLengthInBytes;

        /// <summary>
        /// The <see cref="ArrayPool{T}"/> for larger buffers.
        /// </summary>
        private readonly GCAwareConfigurableArrayPool<byte> largeArrayPool;

        /// <summary>
        /// Initializes a new instance of the <see cref="ArrayPoolMemoryAllocator"/> class.
        /// </summary>
        public ArrayPoolMemoryAllocator()
            : this(DefaultMaxArrayLengthInBytes)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ArrayPoolMemoryAllocator"/> class.
        /// </summary>
        /// <param name="maxArrayLengthInBytes">
        /// The maximum length, in bytes, of an array instance that may be stored in the pool.
        /// Arrays over the threshold will always be allocated.
        /// </param>
        public ArrayPoolMemoryAllocator(int maxArrayLengthInBytes)
            : this(maxArrayLengthInBytes, DefaultMaxArraysPerBucket, DefaultMaxContiguousArrayLengthInBytes)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ArrayPoolMemoryAllocator"/> class.
        /// </summary>
        /// <param name="maxArrayLengthInBytes">
        /// The maximum length, in bytes, of an array instance that may be stored in the pool.
        /// Arrays over the threshold will always be allocated.
        /// </param>
        /// <param name="maxArraysPerBucket">
        /// The maximum number of array instances that may be stored in each bucket in the pool.
        /// The pool groups arrays of similar lengths into buckets for faster access.
        /// </param>
        /// <param name="maxContiguousArrayLengthInBytes">
        /// The maximum length of the largest contiguous buffer that can be handled by this allocator instance.
        /// </param>
        public ArrayPoolMemoryAllocator(
            int maxArrayLengthInBytes,
            int maxArraysPerBucket,
            int maxContiguousArrayLengthInBytes)
        {
            Guard.MustBeGreaterThanOrEqualTo(maxArrayLengthInBytes, SharedPoolThresholdInBytes, nameof(maxArrayLengthInBytes));
            Guard.MustBeBetweenOrEqualTo(maxContiguousArrayLengthInBytes, 1, maxArrayLengthInBytes, nameof(maxContiguousArrayLengthInBytes));

            this.MaxPoolSizeInBytes = maxArrayLengthInBytes;
            this.BufferCapacityInBytes = maxContiguousArrayLengthInBytes;
            this.MaxArraysPerBucket = maxArraysPerBucket;
            this.largeArrayPool = new GCAwareConfigurableArrayPool<byte>(this.MaxPoolSizeInBytes, this.MaxArraysPerBucket);
        }

        /// <summary>
        /// Gets the maximum size of pooled arrays in bytes.
        /// </summary>
        public int MaxPoolSizeInBytes { get; }

        /// <summary>
        /// Gets the maximum number of array instances that may be stored in each bucket in the pool.
        /// </summary>
        public int MaxArraysPerBucket { get; }

        /// <summary>
        /// Gets the length of the largest contiguous buffer that can be handled by this allocator instance.
        /// </summary>
        public int BufferCapacityInBytes { get; internal set; } // Setter is internal for easy configuration in tests

        /// <inheritdoc />
        public override void ReleaseRetainedResources()
            => this.largeArrayPool.Trim();

        /// <inheritdoc />
        protected internal override int GetBufferCapacityInBytes() => this.BufferCapacityInBytes;

        /// <inheritdoc />
        public override IMemoryOwner<T> Allocate<T>(int length, AllocationOptions options = AllocationOptions.None)
        {
            Guard.MustBeGreaterThanOrEqualTo(length, 0, nameof(length));

            int itemSizeBytes = Unsafe.SizeOf<T>();
            int bufferSizeInBytes = length * itemSizeBytes;

            IMemoryOwner<T> memory;
            if (bufferSizeInBytes > this.MaxPoolSizeInBytes)
            {
                // For anything greater than our pool limit defer to unmanaged memory
                // to prevent LOH fragmentation.
                memory = new UnmanagedBuffer<T>(length);
            }
            else
            {
                // Safe to pool.
                ArrayPool<byte> pool = this.GetArrayPool(bufferSizeInBytes);
                memory = new Buffer<T>(pool.Rent(bufferSizeInBytes), length, pool);
            }

            if (options == AllocationOptions.Clean)
            {
                memory.GetSpan().Clear();
            }

            return memory;
        }

        /// <inheritdoc />
        public override IManagedByteBuffer AllocateManagedByteBuffer(int length, AllocationOptions options = AllocationOptions.None)
        {
            Guard.MustBeGreaterThanOrEqualTo(length, 0, nameof(length));

            ArrayPool<byte> pool = this.GetArrayPool(length);
            byte[] byteArray = pool.Rent(length);

            var buffer = new ManagedByteBuffer(byteArray, length, pool);
            if (options == AllocationOptions.Clean)
            {
                buffer.GetSpan().Clear();
            }

            return buffer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ArrayPool<byte> GetArrayPool(int bufferSizeInBytes)
        {
            if (bufferSizeInBytes <= SharedPoolThresholdInBytes)
            {
                return ArrayPool<byte>.Shared;
            }

            return this.largeArrayPool;
        }
    }
}
