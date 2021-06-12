// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Buffers;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Memory
{
    /// <summary>
    /// Implements <see cref="MemoryAllocator"/> by allocating memory from <see cref="ArrayPool{T}"/>.
    /// </summary>
    public sealed partial class ArrayPoolMemoryAllocator : MemoryAllocator
    {
        /// <summary>
        /// The <see cref="ArrayPool{T}"/> for small-to-medium buffers which is not kept clean.
        /// </summary>
        private ArrayPool<byte> normalArrayPool;

        /// <summary>
        /// The <see cref="ArrayPool{T}"/> for huge buffers, which is not kept clean.
        /// </summary>
        private ArrayPool<byte> largeArrayPool;

        /// <summary>
        /// Initializes a new instance of the <see cref="ArrayPoolMemoryAllocator"/> class.
        /// </summary>
        public ArrayPoolMemoryAllocator()
            : this(DefaultMaxPooledBufferSizeInBytes)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ArrayPoolMemoryAllocator"/> class.
        /// </summary>
        /// <param name="maxPoolSizeInBytes">
        /// The maximum length, in bytes, of an array instance that may be stored in the pool.
        /// Arrays over the threshold will always be allocated.
        /// </param>
        public ArrayPoolMemoryAllocator(int maxPoolSizeInBytes)
            : this(maxPoolSizeInBytes, DefaultLargePoolBucketCount, DefaultBufferCapacityInBytes)
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
            Guard.MustBeGreaterThan(maxContiguousArrayLengthInBytes, 0, nameof(maxContiguousArrayLengthInBytes));

            this.MaxPoolSizeInBytes = maxArrayLengthInBytes;
            this.BufferCapacityInBytes = maxContiguousArrayLengthInBytes;
            this.MaxArraysPerBucket = maxArraysPerBucket;

            this.InitArrayPools();
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
            => this.InitArrayPools();

        /// <inheritdoc />
        protected internal override int GetBufferCapacityInBytes() => this.BufferCapacityInBytes;

        /// <inheritdoc />
        public override IMemoryOwner<T> Allocate<T>(int length, AllocationOptions options = AllocationOptions.None)
        {
            Guard.MustBeGreaterThanOrEqualTo(length, 0, nameof(length));

            int itemSizeBytes = Unsafe.SizeOf<T>();
            int bufferSizeInBytes = length * itemSizeBytes;
            ArrayPool<byte> pool = this.GetArrayPool(bufferSizeInBytes);
            byte[] byteArray = pool.Rent(bufferSizeInBytes);

            var buffer = new Buffer<T>(this, byteArray, length, pool);
            if (options == AllocationOptions.Clean)
            {
                buffer.GetSpan().Clear();
            }

            return buffer;
        }

        /// <inheritdoc />
        public override IManagedByteBuffer AllocateManagedByteBuffer(int length, AllocationOptions options = AllocationOptions.None)
        {
            Guard.MustBeGreaterThanOrEqualTo(length, 0, nameof(length));

            ArrayPool<byte> pool = this.GetArrayPool(length);
            byte[] byteArray = pool.Rent(length);

            var buffer = new ManagedByteBuffer(this, byteArray, length, pool);
            if (options == AllocationOptions.Clean)
            {
                buffer.GetSpan().Clear();
            }

            return buffer;
        }

        private ArrayPool<byte> GetArrayPool(int bufferSizeInBytes)
            => bufferSizeInBytes <= SharedPoolThresholdInBytes
            ? this.normalArrayPool
            : this.largeArrayPool;

        private void InitArrayPools()
        {
            this.largeArrayPool = ArrayPool<byte>.Create(this.MaxPoolSizeInBytes, this.MaxArraysPerBucket);
            this.normalArrayPool = ArrayPool<byte>.Shared;
        }
    }
}
