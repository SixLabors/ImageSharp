// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Memory;

/// <summary>
/// Memory managers are used to allocate memory for image processing operations.
/// </summary>
public abstract class MemoryAllocator
{
    private const int OneGigabyte = 1 << 30;

    /// <summary>
    /// Gets the default platform-specific global <see cref="MemoryAllocator"/> instance that
    /// serves as the default value for <see cref="Configuration.MemoryAllocator"/>.
    /// <para />
    /// This is a get-only property,
    /// you should set <see cref="Configuration.Default"/>'s <see cref="Configuration.MemoryAllocator"/>
    /// to change the default allocator used by <see cref="Image"/> and it's operations.
    /// </summary>
    public static MemoryAllocator Default { get; } = Create();

    internal long MemoryGroupAllocationLimitBytes { get; private set; } = Environment.Is64BitProcess ? 4L * OneGigabyte : OneGigabyte;

    internal int SingleBufferAllocationLimitBytes { get; private set; } = OneGigabyte;

    /// <summary>
    /// Gets the length of the largest contiguous buffer that can be handled by this allocator instance in bytes.
    /// </summary>
    /// <returns>The length of the largest contiguous buffer that can be handled by this allocator instance.</returns>
    protected internal abstract int GetBufferCapacityInBytes();

    /// <summary>
    /// Creates a default instance of a <see cref="MemoryAllocator"/> optimized for the executing platform.
    /// </summary>
    /// <returns>The <see cref="MemoryAllocator"/>.</returns>
    public static MemoryAllocator Create() => Create(default);

    /// <summary>
    /// Creates the default <see cref="MemoryAllocator"/> using the provided options.
    /// </summary>
    /// <param name="options">The <see cref="MemoryAllocatorOptions"/>.</param>
    /// <returns>The <see cref="MemoryAllocator"/>.</returns>
    public static MemoryAllocator Create(MemoryAllocatorOptions options)
    {
        UniformUnmanagedMemoryPoolMemoryAllocator allocator = new(options.MaximumPoolSizeMegabytes);
        if (options.AllocationLimitMegabytes.HasValue)
        {
            allocator.MemoryGroupAllocationLimitBytes = options.AllocationLimitMegabytes.Value * 1024L * 1024L;
            allocator.SingleBufferAllocationLimitBytes = (int)Math.Min(allocator.SingleBufferAllocationLimitBytes, allocator.MemoryGroupAllocationLimitBytes);
        }

        return allocator;
    }

    /// <summary>
    /// Allocates an <see cref="IMemoryOwner{T}" />, holding a <see cref="Memory{T}"/> of length <paramref name="length"/>.
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
    /// Releases all retained resources not being in use.
    /// Eg: by resetting array pools and letting GC to free the arrays.
    /// </summary>
    public virtual void ReleaseRetainedResources()
    {
    }

    /// <summary>
    /// Allocates a <see cref="MemoryGroup{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of element to allocate.</typeparam>
    /// <param name="totalLength">The total length of the buffer.</param>
    /// <param name="bufferAlignment">The expected alignment (eg. to make sure image rows fit into single buffers).</param>
    /// <param name="options">The <see cref="AllocationOptions"/>.</param>
    /// <returns>A new <see cref="MemoryGroup{T}"/>.</returns>
    /// <exception cref="InvalidMemoryOperationException">Thrown when 'blockAlignment' converted to bytes is greater than the buffer capacity of the allocator.</exception>
    internal MemoryGroup<T> AllocateGroup<T>(
        long totalLength,
        int bufferAlignment,
        AllocationOptions options = AllocationOptions.None)
        where T : struct
    {
        if (totalLength < 0)
        {
            InvalidMemoryOperationException.ThrowNegativeAllocationException(totalLength);
        }

        ulong totalLengthInBytes = (ulong)totalLength * (ulong)Unsafe.SizeOf<T>();
        if (totalLengthInBytes > (ulong)this.MemoryGroupAllocationLimitBytes)
        {
            InvalidMemoryOperationException.ThrowAllocationOverLimitException(totalLengthInBytes, this.MemoryGroupAllocationLimitBytes);
        }

        // Cast to long is safe because we already checked that the total length is within the limit.
        return this.AllocateGroupCore<T>(totalLength, (long)totalLengthInBytes, bufferAlignment, options);
    }

    internal virtual MemoryGroup<T> AllocateGroupCore<T>(long totalLengthInElements, long totalLengthInBytes, int bufferAlignment, AllocationOptions options)
        where T : struct
        => MemoryGroup<T>.Allocate(this, totalLengthInElements, bufferAlignment, options);
}
