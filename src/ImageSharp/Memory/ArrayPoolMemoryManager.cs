// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Memory
{
    using Guard = SixLabors.Guard;

    /// <summary>
    /// Implements <see cref="MemoryManager"/> by allocating memory from <see cref="ArrayPool{T}"/>.
    /// </summary>
    public partial class ArrayPoolMemoryManager : MemoryManager
    {
        /// <summary>
        /// The default value for: maximum size of pooled arrays in bytes.
        /// Currently set to 32MB, which is equivalent to 8 megapixels of raw <see cref="Rgba32"/> data.
        /// </summary>
        internal const int DefaultMaxPooledBufferSizeInBytes = 32 * 1024 * 1024;

        /// <summary>
        /// The value for: The threshold to pool arrays in <see cref="largeArrayPool"/> which has less buckets for memory safety.
        /// </summary>
        private const int DefaultLargeBufferThresholdInBytes = 8 * 1024 * 1024;

        /// <summary>
        /// The <see cref="ArrayPool{T}"/> for huge buffers, which is not kept clean.
        /// </summary>
        private ArrayPool<byte> largeArrayPool;

        /// <summary>
        /// The <see cref="ArrayPool{T}"/> for small-to-medium buffers which is not kept clean.
        /// </summary>
        private ArrayPool<byte> normalArrayPool;

        /// <summary>
        /// Initializes a new instance of the <see cref="ArrayPoolMemoryManager"/> class.
        /// </summary>
        public ArrayPoolMemoryManager()
            : this(DefaultMaxPooledBufferSizeInBytes, DefaultLargeBufferThresholdInBytes)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ArrayPoolMemoryManager"/> class.
        /// </summary>
        /// <param name="maxPoolSizeInBytes">The maximum size of pooled arrays. Arrays over the thershold are gonna be always allocated.</param>
        public ArrayPoolMemoryManager(int maxPoolSizeInBytes)
            : this(maxPoolSizeInBytes, GetLargeBufferThresholdInBytes(maxPoolSizeInBytes))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ArrayPoolMemoryManager"/> class.
        /// </summary>
        /// <param name="maxPoolSizeInBytes">The maximum size of pooled arrays. Arrays over the thershold are gonna be always allocated.</param>
        /// <param name="largeBufferThresholdInBytes">The threshold to pool arrays in <see cref="largeArrayPool"/> which has less buckets for memory safety.</param>
        public ArrayPoolMemoryManager(int maxPoolSizeInBytes, int largeBufferThresholdInBytes)
        {
            Guard.MustBeGreaterThan(maxPoolSizeInBytes, 0, nameof(maxPoolSizeInBytes));
            Guard.MustBeLessThanOrEqualTo(largeBufferThresholdInBytes, maxPoolSizeInBytes, nameof(largeBufferThresholdInBytes));

            this.MaxPoolSizeInBytes = maxPoolSizeInBytes;
            this.LargeBufferThresholdInBytes = largeBufferThresholdInBytes;

            this.InitArrayPools();
        }

        /// <summary>
        /// Gets the maximum size of pooled arrays in bytes.
        /// </summary>
        public int MaxPoolSizeInBytes { get; }

        /// <summary>
        /// Gets the threshold to pool arrays in <see cref="largeArrayPool"/> which has less buckets for memory safety.
        /// </summary>
        public int LargeBufferThresholdInBytes { get; }

        /// <inheritdoc />
        public override void ReleaseRetainedResources()
        {
            this.InitArrayPools();
        }

        /// <inheritdoc />
        internal override IBuffer<T> Allocate<T>(int length, bool clear)
        {
            int itemSizeBytes = Unsafe.SizeOf<T>();
            int bufferSizeInBytes = length * itemSizeBytes;

            ArrayPool<byte> pool = this.GetArrayPool(bufferSizeInBytes);
            byte[] byteArray = pool.Rent(bufferSizeInBytes);

            var buffer = new Buffer<T>(byteArray, length, pool);
            if (clear)
            {
                buffer.Clear();
            }

            return buffer;
        }

        internal override IManagedByteBuffer AllocateManagedByteBuffer(int length, bool clear)
        {
            ArrayPool<byte> pool = this.GetArrayPool(length);
            byte[] byteArray = pool.Rent(length);

            var buffer = new ManagedByteBuffer(byteArray, length, pool);
            if (clear)
            {
                buffer.Clear();
            }

            return buffer;
        }

        private static int GetLargeBufferThresholdInBytes(int maxPoolSizeInBytes)
        {
            return maxPoolSizeInBytes / 4;
        }

        private ArrayPool<byte> GetArrayPool(int bufferSizeInBytes)
        {
            return bufferSizeInBytes <= this.LargeBufferThresholdInBytes ? this.normalArrayPool : this.largeArrayPool;
        }

        private void InitArrayPools()
        {
            this.largeArrayPool = ArrayPool<byte>.Create(this.MaxPoolSizeInBytes, 8);
            this.normalArrayPool = ArrayPool<byte>.Create(this.LargeBufferThresholdInBytes, 24);
        }
    }
}