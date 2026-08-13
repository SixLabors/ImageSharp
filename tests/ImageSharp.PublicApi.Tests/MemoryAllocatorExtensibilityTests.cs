// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.PublicApi.Tests;

/// <summary>
/// Verifies that a fully functional <see cref="MemoryAllocator"/> can be implemented outside the ImageSharp assembly.
/// </summary>
public class MemoryAllocatorExtensibilityTests
{
    private const int OneMegabyte = 1 << 20;

    /// <summary>
    /// Verifies that an external allocator can apply <see cref="MemoryAllocatorOptions"/> from its constructor
    /// and that the applied limits are readable through the public properties.
    /// </summary>
    [Fact]
    public void ExternalAllocatorCanApplyOptionsFromConstructor()
    {
        ExternalArrayMemoryAllocator allocator = new(new MemoryAllocatorOptions
        {
            AllocationLimitMegabytes = 8,
            SingleBufferAllocationLimitMegabytes = 2,
            AccumulativeAllocationLimitMegabytes = 8
        });

        Assert.Equal(8L * OneMegabyte, allocator.MemoryGroupAllocationLimitBytes);
        Assert.Equal(2 * OneMegabyte, allocator.SingleBufferAllocationLimitBytes);
        Assert.Equal(8L * OneMegabyte, allocator.AccumulativeAllocationLimitBytes);
    }

    /// <summary>
    /// Verifies that the applied single buffer limit is capped to the group allocation limit.
    /// </summary>
    [Fact]
    public void ExternalAllocatorAppliedSingleBufferLimitIsCappedToGroupLimit()
    {
        ExternalArrayMemoryAllocator allocator = new(new MemoryAllocatorOptions
        {
            AllocationLimitMegabytes = 4,
            SingleBufferAllocationLimitMegabytes = 8
        });

        Assert.Equal(4 * OneMegabyte, allocator.SingleBufferAllocationLimitBytes);
    }

    /// <summary>
    /// Verifies that an external allocator can set the limit properties directly
    /// and that the base class validates allocations against the configured values.
    /// </summary>
    [Fact]
    public void ExternalAllocatorCanSetSingleBufferLimit()
    {
        ExternalArrayMemoryAllocator allocator = new();
        allocator.SetLimits(
            memoryGroupAllocationLimitBytes: 4096,
            singleBufferAllocationLimitBytes: 1024,
            accumulativeAllocationLimitBytes: 4096);

        allocator.Allocate<byte>(1024).Dispose();
        Assert.Throws<InvalidMemoryOperationException>(() => allocator.Allocate<byte>(1025));
    }

    /// <summary>
    /// Verifies that owners produced by an external allocator participate in accumulative allocation tracking.
    /// </summary>
    [Fact]
    public void ExternalAllocatorTracksAccumulativeAllocations()
    {
        ExternalArrayMemoryAllocator allocator = new();
        allocator.SetLimits(
            memoryGroupAllocationLimitBytes: 4096,
            singleBufferAllocationLimitBytes: 4096,
            accumulativeAllocationLimitBytes: 4096);

        IMemoryOwner<byte> owner = allocator.Allocate<byte>(4096);

        // The full accumulative budget is reserved while the owner is live.
        Assert.Throws<InvalidMemoryOperationException>(() => allocator.Allocate<byte>(1));

        // Disposing the owner releases the reservation.
        owner.Dispose();
        allocator.Allocate<byte>(4096).Dispose();
    }

    /// <summary>
    /// Verifies that an external allocator can back image creation through <see cref="Configuration"/>,
    /// for both discontiguous and contiguous buffer preferences, and that disposal reaches the external owners.
    /// </summary>
    /// <param name="preferContiguousImageBuffers">The contiguous buffer preference to apply.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExternalAllocatorBacksImageCreation(bool preferContiguousImageBuffers)
    {
        ExternalArrayMemoryAllocator allocator = new();
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = allocator;
        configuration.PreferContiguousImageBuffers = preferContiguousImageBuffers;

        using (Image<Rgba32> image = new(configuration, 16, 16, Color.Red.ToPixel<Rgba32>()))
        {
            Assert.True(allocator.CreatedOwners > 0);
            Assert.Equal(Color.Red.ToPixel<Rgba32>(), image[8, 8]);
        }

        Assert.Equal(0, allocator.LiveOwners);
    }

    /// <summary>
    /// A <see cref="MemoryAllocator"/> implemented with only the public API surface, backed by managed arrays.
    /// </summary>
    private sealed class ExternalArrayMemoryAllocator : MemoryAllocator
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExternalArrayMemoryAllocator"/> class with default limits.
        /// </summary>
        public ExternalArrayMemoryAllocator()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExternalArrayMemoryAllocator"/> class with custom limits.
        /// </summary>
        /// <param name="options">The <see cref="MemoryAllocatorOptions"/> to apply.</param>
        public ExternalArrayMemoryAllocator(MemoryAllocatorOptions options) => this.ApplyOptions(options);

        /// <summary>
        /// Gets the total number of owners created by this allocator.
        /// </summary>
        public int CreatedOwners { get; private set; }

        /// <summary>
        /// Gets the number of owners created by this allocator that are not yet disposed.
        /// </summary>
        public int LiveOwners { get; private set; }

        /// <summary>
        /// Sets the protected limit properties directly, as a derived allocator can.
        /// </summary>
        /// <param name="memoryGroupAllocationLimitBytes">The group allocation limit, in bytes.</param>
        /// <param name="singleBufferAllocationLimitBytes">The single buffer allocation limit, in bytes.</param>
        /// <param name="accumulativeAllocationLimitBytes">The accumulative allocation limit, in bytes.</param>
        public void SetLimits(
            long memoryGroupAllocationLimitBytes,
            int singleBufferAllocationLimitBytes,
            long accumulativeAllocationLimitBytes)
        {
            this.MemoryGroupAllocationLimitBytes = memoryGroupAllocationLimitBytes;
            this.SingleBufferAllocationLimitBytes = singleBufferAllocationLimitBytes;
            this.AccumulativeAllocationLimitBytes = accumulativeAllocationLimitBytes;
        }

        /// <inheritdoc />
        protected override int GetBufferCapacityInBytes() => int.MaxValue;

        /// <inheritdoc />
        protected override AllocationTrackedMemoryManager<T> AllocateCore<T>(int length, AllocationOptions options = AllocationOptions.None)
        {
            this.CreatedOwners++;
            this.LiveOwners++;
            return new ExternalArrayMemoryManager<T>(new T[length], this);
        }

        /// <summary>
        /// Records the disposal of an owner created by this allocator.
        /// </summary>
        internal void OnOwnerDisposed() => this.LiveOwners--;
    }

    /// <summary>
    /// An <see cref="AllocationTrackedMemoryManager{T}"/> implemented with only the public API surface.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    private sealed class ExternalArrayMemoryManager<T> : AllocationTrackedMemoryManager<T>
        where T : struct
    {
        private readonly T[] array;
        private readonly ExternalArrayMemoryAllocator allocator;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExternalArrayMemoryManager{T}"/> class.
        /// </summary>
        /// <param name="array">The array that backs this owner.</param>
        /// <param name="allocator">The allocator that created this owner.</param>
        public ExternalArrayMemoryManager(T[] array, ExternalArrayMemoryAllocator allocator)
        {
            this.array = array;
            this.allocator = allocator;
        }

        /// <inheritdoc />
        public override Span<T> GetSpan() => this.array;

        /// <inheritdoc />
        public override MemoryHandle Pin(int elementIndex = 0) => throw new NotSupportedException("Pinning is not required by these tests.");

        /// <inheritdoc />
        public override void Unpin()
        {
        }

        /// <inheritdoc />
        protected override void DisposeCore(bool disposing) => this.allocator.OnOwnerDisposed();
    }
}
