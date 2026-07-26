// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Tests.Memory.Allocators;

public class SimpleGcMemoryAllocatorTests
{
    public class BufferTests : BufferTestSuite
    {
        public BufferTests()
            : base(new SimpleGcMemoryAllocator())
        {
        }
    }

    protected SimpleGcMemoryAllocator MemoryAllocator { get; } = new();

    public static TheoryData<int> InvalidLengths { get; set; } = new()
    {
        { -1 },
        { (1 << 30) + 1 }
    };

    [Theory]
    [MemberData(nameof(InvalidLengths))]
    public void Allocate_IncorrectAmount_ThrowsCorrect_InvalidMemoryOperationException(int length)
        => Assert.Throws<InvalidMemoryOperationException>(
            () => this.MemoryAllocator.Allocate<BigStruct>(length));

    [Fact]
    public unsafe void Allocate_MemoryIsPinnableMultipleTimes()
    {
        SimpleGcMemoryAllocator allocator = this.MemoryAllocator;
        using IMemoryOwner<byte> memoryOwner = allocator.Allocate<byte>(100);

        using (MemoryHandle pin = memoryOwner.Memory.Pin())
        {
            Assert.NotEqual(IntPtr.Zero, (IntPtr)pin.Pointer);
        }

        using (MemoryHandle pin = memoryOwner.Memory.Pin())
        {
            Assert.NotEqual(IntPtr.Zero, (IntPtr)pin.Pointer);
        }
    }

    [Fact]
    public void Allocate_AccumulativeLimit_ReleasesOnOwnerDispose()
    {
        SimpleGcMemoryAllocator allocator = new(new MemoryAllocatorOptions
        {
            AccumulativeAllocationLimitMegabytes = 1
        });
        const int oneMb = 1 << 20;

        // Reserve the full limit with a single owner.
        IMemoryOwner<byte> b0 = allocator.Allocate<byte>(oneMb);

        // Additional allocation should exceed the limit while the owner is live.
        Assert.Throws<InvalidMemoryOperationException>(() => allocator.Allocate<byte>(1));

        // Disposing the owner releases the reservation.
        b0.Dispose();

        // Allocation should succeed after the reservation is released.
        allocator.Allocate<byte>(oneMb).Dispose();
    }

    [Fact]
    public void AllocateGroup_AccumulativeLimit_ReleasesOnGroupDispose()
    {
        SimpleGcMemoryAllocator allocator = new(new MemoryAllocatorOptions
        {
            AccumulativeAllocationLimitMegabytes = 1
        });
        const int oneMb = 1 << 20;

        // Reserve the full limit with a single group.
        MemoryGroup<byte> g0 = allocator.AllocateGroup<byte>(oneMb, 1024);

        // Additional allocation should exceed the limit while the group is live.
        Assert.Throws<InvalidMemoryOperationException>(() => allocator.AllocateGroup<byte>(1, 1024));

        // Disposing the group releases the reservation.
        g0.Dispose();

        // Allocation should succeed after the reservation is released.
        allocator.AllocateGroup<byte>(oneMb, 1024).Dispose();
    }

    [StructLayout(LayoutKind.Explicit, Size = 512)]
    private struct BigStruct
    {
    }
}
