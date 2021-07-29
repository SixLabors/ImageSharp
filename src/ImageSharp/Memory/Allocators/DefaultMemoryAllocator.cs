using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Memory.Internals;

namespace SixLabors.ImageSharp.Memory
{
    internal sealed class DefaultMemoryAllocator : MemoryAllocator
    {
        private const int OneMegabyte = 1024 * 1024;
        private readonly int sharedArrayPoolThresholdInBytes;
        private readonly int maxContiguousPoolBufferInBytes;
        private readonly int poolCapacity;
        private readonly float trimRate;

        private UniformByteArrayPool pool;
        private readonly UnmanagedMemoryAllocator unmnagedAllocator;

        public DefaultMemoryAllocator(int? maxPoolSizeMegabytes, int? minimumContiguousBlockBytes)
            : this(
                minimumContiguousBlockBytes.HasValue ? minimumContiguousBlockBytes.Value : 4 * OneMegabyte,
                maxPoolSizeMegabytes.HasValue ? (long)maxPoolSizeMegabytes.Value * OneMegabyte : GetDefaultMaxPoolSizeBytes(),
                minimumContiguousBlockBytes.HasValue ? Math.Max(minimumContiguousBlockBytes.Value, 32 * OneMegabyte) : 32 * OneMegabyte)
        {
        }

        public DefaultMemoryAllocator(
            int maxContiguousPoolBufferInBytes,
            long maxPoolSizeInBytes,
            int maxContiguousUnmanagedBufferInBytes)
            : this(
                OneMegabyte,
                maxContiguousPoolBufferInBytes,
                maxPoolSizeInBytes,
                maxContiguousUnmanagedBufferInBytes)
        {
        }

        // Internal constructor allowing to change the shared array pool threshold for testing purposes.
        internal DefaultMemoryAllocator(
            int sharedArrayPoolThresholdInBytes,
            int maxContiguousPoolBufferInBytes,
            long maxPoolSizeInBytes,
            int maxCapacityOfUnmanagedBuffers,
            float trimRate = UniformByteArrayPool.DefaultTrimRate)
        {
            this.sharedArrayPoolThresholdInBytes = sharedArrayPoolThresholdInBytes;
            this.maxContiguousPoolBufferInBytes = maxContiguousPoolBufferInBytes;
            this.poolCapacity = (int)(maxPoolSizeInBytes / maxContiguousPoolBufferInBytes);
            this.trimRate = trimRate;
            this.pool = new UniformByteArrayPool(maxContiguousPoolBufferInBytes, this.poolCapacity, trimRate);
            this.unmnagedAllocator = new UnmanagedMemoryAllocator(maxCapacityOfUnmanagedBuffers);
        }

        /// <inheritdoc />
        protected internal override int GetBufferCapacityInBytes() => this.maxContiguousPoolBufferInBytes;

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

            if (lengthInBytes <= this.maxContiguousPoolBufferInBytes)
            {
                byte[] array = this.pool.Rent(options);
                if (array != null)
                {
                    return new UniformByteArrayPool.FinalizableBuffer<T>(array, length, this.pool);
                }
            }

            return this.unmnagedAllocator.Allocate<T>(length, options);
        }

        /// <inheritdoc />
        public override IManagedByteBuffer AllocateManagedByteBuffer(
            int length,
            AllocationOptions options = AllocationOptions.None)
        {
            var buffer = new SharedArrayPoolByteBuffer(length);
            if (options.Has(AllocationOptions.Clean))
            {
                buffer.GetSpan().Clear();
            }

            return buffer;
        }

        /// <inheritdoc />
        public override void ReleaseRetainedResources() =>
            this.pool = new UniformByteArrayPool(this.maxContiguousPoolBufferInBytes, this.poolCapacity, this.trimRate);

        internal override MemoryGroup<T> AllocateGroup<T>(
            long totalLength,
            int bufferAlignment,
            AllocationOptions options = AllocationOptions.None)
        {
            long totalLengthInBytes = totalLength * Unsafe.SizeOf<T>();
            if (totalLengthInBytes <= this.sharedArrayPoolThresholdInBytes)
            {
                var buffer = new SharedArrayPoolBuffer<T>((int)totalLength);
                if (options.Has(AllocationOptions.Clean))
                {
                    buffer.Memory.Span.Clear();
                }

                return MemoryGroup<T>.CreateContiguous(buffer);
            }

            if (totalLengthInBytes <= this.maxContiguousPoolBufferInBytes)
            {
                // Optimized path renting single array from the pool
                byte[] array = this.pool.Rent(options);
                if (array != null)
                {
                    var buffer = new UniformByteArrayPool.FinalizableBuffer<T>(array, (int)totalLength, this.pool);
                    return MemoryGroup<T>.CreateContiguous(buffer);
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
            // On .NET Core 3.1+, determine the pool size by the total available memory:
            GCMemoryInfo info = GC.GetGCMemoryInfo();
            return info.TotalAvailableMemoryBytes / 8;
#else
            // Stick to a conservative value of 128 Megabytes on other platforms:
            return 128 * OneMegabyte;
#endif
        }
    }
}
