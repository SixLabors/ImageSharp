// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Memory
{
    /// <summary>
    /// Implements <see cref="MemoryAllocator"/> by allocating memory from <see cref="ArrayPool{T}"/>.
    /// </summary>
    public sealed partial class ArrayPoolMemoryAllocator : MemoryAllocator
    {
        private readonly int maxArraysPerBucketNormalPool;

        private readonly int maxArraysPerBucketLargePool;

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
            : this(DefaultMaxPooledBufferSizeInBytes, DefaultBufferSelectorThresholdInBytes)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ArrayPoolMemoryAllocator"/> class.
        /// </summary>
        /// <param name="maxPoolSizeInBytes">The maximum size of pooled arrays. Arrays over the thershold are gonna be always allocated.</param>
        public ArrayPoolMemoryAllocator(int maxPoolSizeInBytes)
            : this(maxPoolSizeInBytes, GetLargeBufferThresholdInBytes(maxPoolSizeInBytes))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ArrayPoolMemoryAllocator"/> class.
        /// </summary>
        /// <param name="maxPoolSizeInBytes">The maximum size of pooled arrays. Arrays over the thershold are gonna be always allocated.</param>
        /// <param name="poolSelectorThresholdInBytes">Arrays over this threshold will be pooled in <see cref="largeArrayPool"/> which has less buckets for memory safety.</param>
        public ArrayPoolMemoryAllocator(int maxPoolSizeInBytes, int poolSelectorThresholdInBytes)
            : this(maxPoolSizeInBytes, poolSelectorThresholdInBytes, DefaultLargePoolBucketCount, DefaultNormalPoolBucketCount)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ArrayPoolMemoryAllocator"/> class.
        /// </summary>
        /// <param name="maxPoolSizeInBytes">The maximum size of pooled arrays. Arrays over the thershold are gonna be always allocated.</param>
        /// <param name="poolSelectorThresholdInBytes">The threshold to pool arrays in <see cref="largeArrayPool"/> which has less buckets for memory safety.</param>
        /// <param name="maxArraysPerBucketLargePool">Max arrays per bucket for the large array pool.</param>
        /// <param name="maxArraysPerBucketNormalPool">Max arrays per bucket for the normal array pool.</param>
        public ArrayPoolMemoryAllocator(
            int maxPoolSizeInBytes,
            int poolSelectorThresholdInBytes,
            int maxArraysPerBucketLargePool,
            int maxArraysPerBucketNormalPool)
            : this(
                maxPoolSizeInBytes,
                poolSelectorThresholdInBytes,
                maxArraysPerBucketLargePool,
                maxArraysPerBucketNormalPool,
                DefaultBufferCapacityInBytes)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ArrayPoolMemoryAllocator"/> class.
        /// </summary>
        /// <param name="maxPoolSizeInBytes">The maximum size of pooled arrays. Arrays over the thershold are gonna be always allocated.</param>
        /// <param name="poolSelectorThresholdInBytes">The threshold to pool arrays in <see cref="largeArrayPool"/> which has less buckets for memory safety.</param>
        /// <param name="maxArraysPerBucketLargePool">Max arrays per bucket for the large array pool.</param>
        /// <param name="maxArraysPerBucketNormalPool">Max arrays per bucket for the normal array pool.</param>
        /// <param name="bufferCapacityInBytes">The length of the largest contiguous buffer that can be handled by this allocator instance.</param>
        public ArrayPoolMemoryAllocator(
            int maxPoolSizeInBytes,
            int poolSelectorThresholdInBytes,
            int maxArraysPerBucketLargePool,
            int maxArraysPerBucketNormalPool,
            int bufferCapacityInBytes)
        {
            Guard.MustBeGreaterThan(maxPoolSizeInBytes, 0, nameof(maxPoolSizeInBytes));
            Guard.MustBeLessThanOrEqualTo(poolSelectorThresholdInBytes, maxPoolSizeInBytes, nameof(poolSelectorThresholdInBytes));

            this.MaxPoolSizeInBytes = maxPoolSizeInBytes;
            this.PoolSelectorThresholdInBytes = poolSelectorThresholdInBytes;
            this.BufferCapacityInBytes = bufferCapacityInBytes;
            this.maxArraysPerBucketLargePool = maxArraysPerBucketLargePool;
            this.maxArraysPerBucketNormalPool = maxArraysPerBucketNormalPool;

            this.InitArrayPools();
        }

        /// <summary>
        /// Gets the maximum size of pooled arrays in bytes.
        /// </summary>
        public int MaxPoolSizeInBytes { get; }

        /// <summary>
        /// Gets the threshold to pool arrays in <see cref="largeArrayPool"/> which has less buckets for memory safety.
        /// </summary>
        public int PoolSelectorThresholdInBytes { get; }

        /// <summary>
        /// Gets the length of the largest contiguous buffer that can be handled by this allocator instance.
        /// </summary>
        public int BufferCapacityInBytes { get; internal set; } // Setter is internal for easy configuration in tests

        /// <inheritdoc />
        public override void ReleaseRetainedResources()
        {
            this.InitArrayPools();
        }

        /// <inheritdoc />
        protected internal override int GetBufferCapacityInBytes() => this.BufferCapacityInBytes;

        /// <inheritdoc />
        public override IMemoryOwner<T> Allocate<T>(int length, AllocationOptions options = AllocationOptions.None)
        {
            Guard.MustBeGreaterThanOrEqualTo(length, 0, nameof(length));
            int itemSizeBytes = Unsafe.SizeOf<T>();
            int bufferSizeInBytes = length * itemSizeBytes;
            if (bufferSizeInBytes < 0 || bufferSizeInBytes > this.BufferCapacityInBytes)
            {
                ThrowInvalidAllocationException<T>(length);
            }

            ArrayPool<byte> pool = this.GetArrayPool(bufferSizeInBytes);
            byte[] byteArray = pool.Rent(bufferSizeInBytes);

            var buffer = new Buffer<T>(byteArray, length, pool);
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

            var buffer = new ManagedByteBuffer(byteArray, length, pool);
            if (options == AllocationOptions.Clean)
            {
                buffer.GetSpan().Clear();
            }

            return buffer;
        }

        private static int GetLargeBufferThresholdInBytes(int maxPoolSizeInBytes)
        {
            return maxPoolSizeInBytes / 4;
        }

        [MethodImpl(InliningOptions.ColdPath)]
        private static void ThrowInvalidAllocationException<T>(int length) =>
            throw new InvalidMemoryOperationException(
                $"Requested allocation: {length} elements of {typeof(T).Name} is over the capacity of the MemoryAllocator.");

        private ArrayPool<byte> GetArrayPool(int bufferSizeInBytes)
        {
            return bufferSizeInBytes <= this.PoolSelectorThresholdInBytes ? this.normalArrayPool : this.largeArrayPool;
        }

        private void InitArrayPools()
        {
            this.largeArrayPool = ArrayPool<byte>.Create(this.MaxPoolSizeInBytes, this.maxArraysPerBucketLargePool);
            this.normalArrayPool = ArrayPool<byte>.Create(this.PoolSelectorThresholdInBytes, this.maxArraysPerBucketNormalPool);
        }
    }
}
