// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

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

        // 4 MB seemed to perform slightly better in benchmarks than 2MB or higher values:
        private const int DefaultContiguousPoolBlockSizeBytes = 4 * OneMegabyte;
        private const int DefaultNonPoolBlockSizeBytes = 32 * OneMegabyte;
        private readonly int sharedArrayPoolThresholdInBytes;
        private readonly int poolBufferSizeInBytes;
        private readonly int poolCapacity;
        private readonly UniformUnmanagedMemoryPool.TrimSettings trimSettings;

        private readonly UniformUnmanagedMemoryPool pool;
        private readonly UnmanagedMemoryAllocator nonPoolAllocator;

        public UniformUnmanagedMemoryPoolMemoryAllocator(int? maxPoolSizeMegabytes)
            : this(
                DefaultContiguousPoolBlockSizeBytes,
                maxPoolSizeMegabytes.HasValue ? (long)maxPoolSizeMegabytes.Value * OneMegabyte : GetDefaultMaxPoolSizeBytes(),
                DefaultNonPoolBlockSizeBytes)
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

        internal UniformUnmanagedMemoryPoolMemoryAllocator(
            int sharedArrayPoolThresholdInBytes,
            int poolBufferSizeInBytes,
            long maxPoolSizeInBytes,
            int unmanagedBufferSizeInBytes)
            : this(
                sharedArrayPoolThresholdInBytes,
                poolBufferSizeInBytes,
                maxPoolSizeInBytes,
                unmanagedBufferSizeInBytes,
                UniformUnmanagedMemoryPool.TrimSettings.Default)
        {
        }

        internal UniformUnmanagedMemoryPoolMemoryAllocator(
            int sharedArrayPoolThresholdInBytes,
            int poolBufferSizeInBytes,
            long maxPoolSizeInBytes,
            int unmanagedBufferSizeInBytes,
            UniformUnmanagedMemoryPool.TrimSettings trimSettings)
        {
            this.sharedArrayPoolThresholdInBytes = sharedArrayPoolThresholdInBytes;
            this.poolBufferSizeInBytes = poolBufferSizeInBytes;
            this.poolCapacity = (int)(maxPoolSizeInBytes / poolBufferSizeInBytes);
            this.trimSettings = trimSettings;
            this.pool = new UniformUnmanagedMemoryPool(this.poolBufferSizeInBytes, this.poolCapacity, this.trimSettings);
            this.nonPoolAllocator = new UnmanagedMemoryAllocator(unmanagedBufferSizeInBytes);
        }

#if NETCOREAPP3_1_OR_GREATER
        // This delegate allows overriding the method returning the available system memory,
        // so we can test our workaround for https://github.com/dotnet/runtime/issues/65466
        internal static Func<long> GetTotalAvailableMemoryBytes { get; set; } = () => GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
#endif

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
                UnmanagedMemoryHandle mem = this.pool.Rent();
                if (mem.IsValid)
                {
                    UnmanagedBuffer<T> buffer = this.pool.CreateGuardedBuffer<T>(mem, length, options.Has(AllocationOptions.Clean));
                    return buffer;
                }
            }

            return this.nonPoolAllocator.Allocate<T>(length, options);
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
                UnmanagedMemoryHandle mem = this.pool.Rent();
                if (mem.IsValid)
                {
                    UnmanagedBuffer<T> buffer = this.pool.CreateGuardedBuffer<T>(mem, (int)totalLength, options.Has(AllocationOptions.Clean));
                    return MemoryGroup<T>.CreateContiguous(buffer, options.Has(AllocationOptions.Clean));
                }
            }

            // Attempt to rent the whole group from the pool, allocate a group of unmanaged buffers if the attempt fails:
            if (MemoryGroup<T>.TryAllocate(this.pool, totalLength, bufferAlignment, options, out MemoryGroup<T> poolGroup))
            {
                return poolGroup;
            }

            return MemoryGroup<T>.Allocate(this.nonPoolAllocator, totalLength, bufferAlignment, options);
        }

        public override void ReleaseRetainedResources() => this.pool.Release();

        private static long GetDefaultMaxPoolSizeBytes()
        {
#if NETCOREAPP3_1_OR_GREATER
            // On 64 bit .NET Core 3.1+, set the pool size to a portion of the total available memory.
            // There is a bug in GC.GetGCMemoryInfo() on .NET 5 + 32 bit, making TotalAvailableMemoryBytes unreliable:
            // https://github.com/dotnet/runtime/issues/55126#issuecomment-876779327
            if (Environment.Is64BitProcess || !RuntimeInformation.FrameworkDescription.StartsWith(".NET 5.0"))
            {
                long total = GetTotalAvailableMemoryBytes();

                // Workaround for https://github.com/dotnet/runtime/issues/65466
                if (total > 0)
                {
                    return total / 8;
                }
            }
#endif

            // Stick to a conservative value of 128 Megabytes on other platforms and 32 bit .NET 5.0:
            return 128 * OneMegabyte;
        }
    }
}
