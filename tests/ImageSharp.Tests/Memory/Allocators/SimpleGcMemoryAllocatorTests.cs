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

    protected SimpleGcMemoryAllocator TestMemoryAllocator { get; } = new SimpleGcMemoryAllocator();

    public static TheoryData<int> InvalidLengths { get; set; } = new()
    {
        { -1 },
        { MemoryAllocator.DefaultMaxAllocatableSize1DInBytes + 1 }
    };

    [Theory]
    [MemberData(nameof(InvalidLengths))]
    public void Allocate_IncorrectAmount_ThrowsCorrect_ArgumentOutOfRangeException(int length)
        => Assert.Throws<InvalidMemoryOperationException>(() => this.TestMemoryAllocator.Allocate<BigStruct>(length));

    [Fact]
    public unsafe void Allocate_MemoryIsPinnableMultipleTimes()
    {
        SimpleGcMemoryAllocator allocator = this.TestMemoryAllocator;
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

    [StructLayout(LayoutKind.Explicit, Size = 512)]
    private struct BigStruct
    {
    }
}
