// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory.Internals;

namespace SixLabors.ImageSharp.Memory
{
    internal sealed class UniformUnmanagedMemoryPoolMemoryAllocator : MemoryAllocator
    {
        private const int OneMegabyte = 1 << 20;
        private const int DefaultContiguousPoolBlockSizeBytes = 4 * OneMegabyte;
        private const int DefaultUnmanagedBlockSizeBytes = 32 * OneMegabyte;
        private readonly int sharedArrayPoolThresholdInBytes;
        private readonly int poolBufferSizeInBytes;
        private readonly int poolCapacity;
        private readonly float trimRate;

        private UniformUnmanagedMemoryPool pool;
        private readonly UnmanagedMemoryAllocator unmnagedAllocator;

        public UniformUnmanagedMemoryPoolMemoryAllocator(int? maxPoolSizeMegabytes, int? minimumContiguousBlockBytes)
            : this(
                minimumContiguousBlockBytes.HasValue ? minimumContiguousBlockBytes.Value : DefaultContiguousPoolBlockSizeBytes,
                maxPoolSizeMegabytes.HasValue ? (long)maxPoolSizeMegabytes.Value * OneMegabyte : GetDefaultMaxPoolSizeBytes(),
                minimumContiguousBlockBytes.HasValue ? Math.Max(minimumContiguousBlockBytes.Value, DefaultUnmanagedBlockSizeBytes) : DefaultUnmanagedBlockSizeBytes)
        {
        }

        public UniformUnmanagedMemoryPoolMemoryAllocator(
            int poolBufferSizeInBytes,
            long maxPoolSizeInBytes,
            int unmanagedBufferSizeInBytes)
            : this(
                OneMegabyte,
                poolBufferSizeInBytes,
                maxPoolSizeInBytes,
                unmanagedBufferSizeInBytes)
        {
        }

        // Internal constructor allowing to change the shared array pool threshold for testing purposes.
        internal UniformUnmanagedMemoryPoolMemoryAllocator(
            int sharedArrayPoolThresholdInBytes,
            int poolBufferSizeInBytes,
            long maxPoolSizeInBytes,
            int unmanagedBufferSizeInBytes)
        {
            this.sharedArrayPoolThresholdInBytes = sharedArrayPoolThresholdInBytes;
            this.poolBufferSizeInBytes = poolBufferSizeInBytes;
            this.poolCapacity = (int)(maxPoolSizeInBytes / poolBufferSizeInBytes);
            this.pool = new UniformUnmanagedMemoryPool(poolBufferSizeInBytes, this.poolCapacity);
            this.unmnagedAllocator = new UnmanagedMemoryAllocator(unmanagedBufferSizeInBytes);
        }

        /// <inheritdoc />
        protected internal override int GetBufferCapacityInBytes() => this.poolBufferSizeInBytes;

        /// <inheritdoc />
        public override IMemoryOwner<T> Allocate<T>(
            int length,
            AllocationOptions options = AllocationOptions.None)
        {
            Guard.MustBeGreaterThanOrEqualTo(length, 0, nameof(length));
            int lengthInBytes = length * Unsafe.SizeOf<T>();

            if (lengthInBytes <= this.sharedArrayPoolThresholdInBytes)
            {
                var buffer = new SharedArrayPoolBuffer<T>(length);
                if (options.Has(AllocationOptions.Clean))
                {
                    buffer.GetSpan().Clear();
                }

                return buffer;
            }

            if (lengthInBytes <= this.poolBufferSizeInBytes)
            {
                UnmanagedMemoryHandle array = this.pool.Rent(options);
                if (array != null)
                {
                    return new UniformUnmanagedMemoryPool.FinalizableBuffer<T>(this.pool, array, length);
                }
            }

            return this.unmnagedAllocator.Allocate<T>(length, options);
        }

        /// <inheritdoc />
        internal override MemoryGroup<T> AllocateGroup<T>(
            long totalLength,
            int bufferAlignment,
            AllocationOptions options = AllocationOptions.None)
        {
            long totalLengthInBytes = totalLength * Unsafe.SizeOf<T>();
            if (totalLengthInBytes <= this.sharedArrayPoolThresholdInBytes)
            {
                var buffer = new SharedArrayPoolBuffer<T>((int)totalLength);
                return MemoryGroup<T>.CreateContiguous(buffer, options.Has(AllocationOptions.Clean));
            }

            if (totalLengthInBytes <= this.poolBufferSizeInBytes)
            {
                // Optimized path renting single array from the pool
                UnmanagedMemoryHandle array = this.pool.Rent(options);
                if (array != null)
                {
                    var buffer = new UniformUnmanagedMemoryPool.FinalizableBuffer<T>(this.pool, array, (int)totalLength);
                    return MemoryGroup<T>.CreateContiguous(buffer, options.Has(AllocationOptions.Clean));
                }
            }

            // Attempt to rent the whole group from the pool, allocate a group of unmanaged buffers if the attempt fails:
            MemoryGroup<T> poolGroup = options.Has(AllocationOptions.Contiguous) ?
                null :
                MemoryGroup<T>.Allocate(this.pool, totalLength, bufferAlignment, options);
            return poolGroup ?? MemoryGroup<T>.Allocate(this.unmnagedAllocator, totalLength, bufferAlignment, options);
        }

        private static long GetDefaultMaxPoolSizeBytes()
        {
#if NETCORE31COMPATIBLE
            // On .NET Core 3.1+, determine the pool as portion of the total available memory.
            // There is a bug in GC.GetGCMemoryInfo() on .NET 5 + 32 bit, making TotalAvailableMemoryBytes unreliable:
            // https://github.com/dotnet/runtime/issues/55126#issuecomment-876779327
            if (Environment.Is64BitProcess || !RuntimeInformation.FrameworkDescription.StartsWith(".NET 5.0"))
            {
                GCMemoryInfo info = GC.GetGCMemoryInfo();
                return info.TotalAvailableMemoryBytes / 8;
            }
#endif

            // Stick to a conservative value of 128 Megabytes on other platforms and 32 bit .NET 5.0:
            return 128 * OneMegabyte;
        }
    }
}
