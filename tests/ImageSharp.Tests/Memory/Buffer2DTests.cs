// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Memory;

public partial class Buffer2DTests
{
    // ReSharper disable once ClassNeverInstantiated.Local
    private class Assert : Xunit.Assert
    {
        public static void SpanPointsTo<T>(Span<T> span, Memory<T> buffer, int bufferOffset = 0)
            where T : struct
        {
            ref T actual = ref MemoryMarshal.GetReference(span);
            ref T expected = ref buffer.Span[bufferOffset];

            True(Unsafe.AreSame(ref expected, ref actual), "span does not point to the expected position");
        }
    }

    private TestMemoryAllocator MemoryAllocator { get; } = new();

    private const int Big = 99999;

    [Theory]
    [InlineData(Big, 7, 42)]
    [InlineData(Big, 1025, 17)]
    [InlineData(300, 42, 777)]
    public unsafe void Construct(int bufferCapacity, int width, int height)
    {
        this.MemoryAllocator.BufferCapacityInBytes = sizeof(TestStructs.Foo) * bufferCapacity;

        using (Buffer2D<TestStructs.Foo> buffer = this.MemoryAllocator.Allocate2D<TestStructs.Foo>(width, height))
        {
            Assert.Equal(width, buffer.Width);
            Assert.Equal(height, buffer.Height);
            Assert.Equal(width * height, buffer.FastMemoryGroup.TotalLength);
            Assert.True(buffer.FastMemoryGroup.BufferLength % width == 0);
        }
    }

    [Theory]
    [InlineData(Big, 0, 42)]
    [InlineData(Big, 1, 0)]
    [InlineData(60, 42, 0)]
    [InlineData(3, 0, 0)]
    public unsafe void Construct_Empty(int bufferCapacity, int width, int height)
    {
        this.MemoryAllocator.BufferCapacityInBytes = sizeof(TestStructs.Foo) * bufferCapacity;

        using (Buffer2D<TestStructs.Foo> buffer = this.MemoryAllocator.Allocate2D<TestStructs.Foo>(width, height))
        {
            Assert.Equal(width, buffer.Width);
            Assert.Equal(height, buffer.Height);
            Assert.Equal(0, buffer.FastMemoryGroup.TotalLength);
            Assert.Equal(0, buffer.DangerousGetSingleSpan().Length);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Construct_PreferContiguousImageBuffers_AllocatesContiguousRegardlessOfCapacity(bool useSizeOverload)
    {
        this.MemoryAllocator.BufferCapacityInBytes = 10_000;

        using Buffer2D<byte> buffer = useSizeOverload ?
            this.MemoryAllocator.Allocate2D<byte>(
                new Size(200, 200),
                preferContiguosImageBuffers: true) :
            this.MemoryAllocator.Allocate2D<byte>(
            200,
            200,
            preferContiguosImageBuffers: true);
        Assert.Equal(1, buffer.FastMemoryGroup.Count);
        Assert.Equal(200 * 200, buffer.FastMemoryGroup.TotalLength);
    }

    [Theory]
    [InlineData(50, 10, 20, 4)]
    public void Allocate2DOveraligned(int bufferCapacity, int width, int height, int alignmentMultiplier)
    {
        this.MemoryAllocator.BufferCapacityInBytes = sizeof(int) * bufferCapacity;

        using Buffer2D<int> buffer = this.MemoryAllocator.Allocate2DOveraligned<int>(width, height, alignmentMultiplier);
        MemoryGroup<int> memoryGroup = buffer.FastMemoryGroup;
        int expectedAlignment = width * alignmentMultiplier;

        Assert.Equal(expectedAlignment, memoryGroup.BufferLength);
    }

    [Fact]
    public void CreateClean()
    {
        using (Buffer2D<int> buffer = this.MemoryAllocator.Allocate2D<int>(42, 42, AllocationOptions.Clean))
        {
            Span<int> span = buffer.DangerousGetSingleSpan();
            for (int j = 0; j < span.Length; j++)
            {
                Assert.Equal(0, span[j]);
            }
        }
    }

    [Theory]
    [InlineData(Big, 7, 42, 0, 0)]
    [InlineData(Big, 7, 42, 10, 0)]
    [InlineData(Big, 17, 42, 41, 0)]
    [InlineData(500, 17, 42, 41, 1)]
    [InlineData(200, 100, 30, 1, 0)]
    [InlineData(200, 100, 30, 2, 1)]
    [InlineData(200, 100, 30, 4, 2)]
    public unsafe void DangerousGetRowSpan_TestAllocator(int bufferCapacity, int width, int height, int y, int expectedBufferIndex)
    {
        this.MemoryAllocator.BufferCapacityInBytes = sizeof(TestStructs.Foo) * bufferCapacity;

        using (Buffer2D<TestStructs.Foo> buffer = this.MemoryAllocator.Allocate2D<TestStructs.Foo>(width, height))
        {
            Span<TestStructs.Foo> span = buffer.DangerousGetRowSpan(y);

            Assert.Equal(width, span.Length);

            int expectedSubBufferOffset = (width * y) - (expectedBufferIndex * buffer.FastMemoryGroup.BufferLength);
            Assert.SpanPointsTo(span, buffer.FastMemoryGroup[expectedBufferIndex], expectedSubBufferOffset);
        }
    }

    [Theory]
    [InlineData(100, 5)] // Within shared pool
    [InlineData(77, 11)] // Within shared pool
    [InlineData(100, 19)] // Single unmanaged pooled buffer
    [InlineData(103, 17)] // Single unmanaged pooled buffer
    [InlineData(100, 22)] // 2 unmanaged pooled buffers
    [InlineData(100, 99)] // 9 unmanaged pooled buffers
    [InlineData(100, 120)] // 2 unpooled buffers
    public unsafe void DangerousGetRowSpan_UnmanagedAllocator(int width, int height)
    {
        const int sharedPoolThreshold = 1_000;
        const int poolBufferSize = 2_000;
        const int maxPoolSize = 10_000;
        const int unpooledBufferSize = 8_000;

        int elementSize = sizeof(TestStructs.Foo);
        UniformUnmanagedMemoryPoolMemoryAllocator allocator = new(
            sharedPoolThreshold * elementSize,
            poolBufferSize * elementSize,
            maxPoolSize * elementSize,
            unpooledBufferSize * elementSize);

        using Buffer2D<TestStructs.Foo> buffer = allocator.Allocate2D<TestStructs.Foo>(width, height);

        Random rnd = new(42);

        for (int y = 0; y < buffer.Height; y++)
        {
            Span<TestStructs.Foo> span = buffer.DangerousGetRowSpan(y);
            for (int x = 0; x < span.Length; x++)
            {
                ref TestStructs.Foo e = ref span[x];
                e.A = rnd.Next();
                e.B = rnd.NextDouble();
            }
        }

        // Re-seed
        rnd = new Random(42);
        for (int y = 0; y < buffer.Height; y++)
        {
            Span<TestStructs.Foo> span = buffer.GetSafeRowMemory(y).Span;
            for (int x = 0; x < span.Length; x++)
            {
                ref TestStructs.Foo e = ref span[x];
                Assert.True(rnd.Next() == e.A, $"Mismatch @ y={y} x={x}");
                Assert.True(rnd.NextDouble() == e.B, $"Mismatch @ y={y} x={x}");
            }
        }
    }

    [Theory]
    [InlineData(10, 0, 0, 0)]
    [InlineData(10, 0, 2, 0)]
    [InlineData(10, 1, 2, 0)]
    [InlineData(10, 1, 3, 0)]
    [InlineData(10, 1, 5, -1)]
    [InlineData(10, 2, 2, -1)]
    [InlineData(10, 3, 2, 1)]
    [InlineData(10, 4, 2, -1)]
    [InlineData(30, 3, 2, 0)]
    [InlineData(30, 4, 1, -1)]
    public void TryGetPaddedRowSpanY(int bufferCapacity, int y, int padding, int expectedBufferIndex)
    {
        this.MemoryAllocator.BufferCapacityInBytes = bufferCapacity;
        using Buffer2D<byte> buffer = this.MemoryAllocator.Allocate2D<byte>(3, 5);

        bool expectSuccess = expectedBufferIndex >= 0;
        bool success = buffer.DangerousTryGetPaddedRowSpan(y, padding, out Span<byte> paddedSpan);
        Xunit.Assert.Equal(expectSuccess, success);
        if (success)
        {
            int expectedSubBufferOffset = (3 * y) - (expectedBufferIndex * buffer.FastMemoryGroup.BufferLength);
            Assert.SpanPointsTo(paddedSpan, buffer.FastMemoryGroup[expectedBufferIndex], expectedSubBufferOffset);
        }
    }

    public static TheoryData<int, int, int, int> GetRowSpanY_OutOfRange_Data = new()
    {
        { Big, 10, 8, -1 },
        { Big, 10, 8, 8 },
        { 20, 10, 8, -1 },
        { 20, 10, 8, 10 },
    };

    [Theory]
    [MemberData(nameof(GetRowSpanY_OutOfRange_Data))]
    public void GetRowSpan_OutOfRange(int bufferCapacity, int width, int height, int y)
    {
        this.MemoryAllocator.BufferCapacityInBytes = bufferCapacity;
        using Buffer2D<byte> buffer = this.MemoryAllocator.Allocate2D<byte>(width, height);

        Exception ex = Assert.ThrowsAny<Exception>(() => buffer.DangerousGetRowSpan(y));
        Assert.True(ex is ArgumentOutOfRangeException || ex is IndexOutOfRangeException);
    }

    public static TheoryData<int, int, int, int, int> Indexer_OutOfRange_Data = new()
    {
        { Big, 10, 8, 1, -1 },
        { Big, 10, 8, 1, 8 },
        { Big, 10, 8, -1, 1 },
        { Big, 10, 8, 10, 1 },
        { 20, 10, 8, 1, -1 },
        { 20, 10, 8, 1, 10 },
        { 20, 10, 8, -1, 1 },
        { 20, 10, 8, 10, 1 },
    };

    [Theory]
    [MemberData(nameof(Indexer_OutOfRange_Data))]
    public void Indexer_OutOfRange(int bufferCapacity, int width, int height, int x, int y)
    {
        this.MemoryAllocator.BufferCapacityInBytes = bufferCapacity;
        using Buffer2D<byte> buffer = this.MemoryAllocator.Allocate2D<byte>(width, height);

        Exception ex = Assert.ThrowsAny<Exception>(() => buffer[x, y]++);
        Assert.True(ex is ArgumentOutOfRangeException || ex is IndexOutOfRangeException);
    }

    [Theory]
    [InlineData(Big, 42, 8, 0, 0)]
    [InlineData(Big, 400, 1000, 20, 10)]
    [InlineData(Big, 99, 88, 98, 87)]
    [InlineData(500, 200, 30, 42, 13)]
    [InlineData(500, 200, 30, 199, 29)]
    public unsafe void Indexer(int bufferCapacity, int width, int height, int x, int y)
    {
        this.MemoryAllocator.BufferCapacityInBytes = sizeof(TestStructs.Foo) * bufferCapacity;

        using (Buffer2D<TestStructs.Foo> buffer = this.MemoryAllocator.Allocate2D<TestStructs.Foo>(width, height))
        {
            int bufferIndex = (width * y) / buffer.FastMemoryGroup.BufferLength;
            int subBufferStart = (width * y) - (bufferIndex * buffer.FastMemoryGroup.BufferLength);

            Span<TestStructs.Foo> span = buffer.FastMemoryGroup[bufferIndex].Span.Slice(subBufferStart);

            ref TestStructs.Foo actual = ref buffer[x, y];

            ref TestStructs.Foo expected = ref span[x];

            Assert.True(Unsafe.AreSame(ref expected, ref actual));
        }
    }

    [Theory]
    [InlineData(100, 20, 0, 90, 10)]
    [InlineData(100, 3, 0, 50, 50)]
    [InlineData(123, 23, 10, 80, 13)]
    [InlineData(10, 1, 3, 6, 3)]
    [InlineData(2, 2, 0, 1, 1)]
    [InlineData(5, 1, 1, 3, 2)]
    public void CopyColumns(int width, int height, int startIndex, int destIndex, int columnCount)
    {
        Random rnd = new(123);
        using (Buffer2D<float> b = this.MemoryAllocator.Allocate2D<float>(width, height))
        {
            rnd.RandomFill(b.DangerousGetSingleSpan(), 0, 1);

            b.DangerousCopyColumns(startIndex, destIndex, columnCount);

            for (int y = 0; y < b.Height; y++)
            {
                Span<float> row = b.DangerousGetRowSpan(y);

                Span<float> s = row.Slice(startIndex, columnCount);
                Span<float> d = row.Slice(destIndex, columnCount);

                Xunit.Assert.True(s.SequenceEqual(d));
            }
        }
    }

    [Fact]
    public void CopyColumns_InvokeMultipleTimes()
    {
        Random rnd = new(123);
        using (Buffer2D<float> b = this.MemoryAllocator.Allocate2D<float>(100, 100))
        {
            rnd.RandomFill(b.DangerousGetSingleSpan(), 0, 1);

            b.DangerousCopyColumns(0, 50, 22);
            b.DangerousCopyColumns(0, 50, 22);

            for (int y = 0; y < b.Height; y++)
            {
                Span<float> row = b.DangerousGetRowSpan(y);

                Span<float> s = row.Slice(0, 22);
                Span<float> d = row.Slice(50, 22);

                Xunit.Assert.True(s.SequenceEqual(d));
            }
        }
    }

    [Fact]
    public void PublicMemoryGroup_IsMemoryGroupView()
    {
        using Buffer2D<int> buffer1 = this.MemoryAllocator.Allocate2D<int>(10, 10);
        using Buffer2D<int> buffer2 = this.MemoryAllocator.Allocate2D<int>(10, 10);
        IMemoryGroup<int> mgBefore = buffer1.MemoryGroup;

        Buffer2D<int>.SwapOrCopyContent(buffer1, buffer2);

        Assert.False(mgBefore.IsValid);
        Assert.NotSame(mgBefore, buffer1.MemoryGroup);
    }

    public static TheoryData<Size> InvalidLengths { get; set; } = new()
    {
        { new Size(-1, -1) },
        { new Size(32768, 32769) },
        { new Size(32769, 32768) }
    };

    [Theory]
    [MemberData(nameof(InvalidLengths))]
    public void Allocate_IncorrectAmount_ThrowsCorrect_InvalidMemoryOperationException(Size size)
        => Assert.Throws<InvalidMemoryOperationException>(() => this.MemoryAllocator.Allocate2D<Rgba32>(size.Width, size.Height));

    [Theory]
    [MemberData(nameof(InvalidLengths))]
    public void Allocate_IncorrectAmount_ThrowsCorrect_InvalidMemoryOperationException_Size(Size size)
        => Assert.Throws<InvalidMemoryOperationException>(() => this.MemoryAllocator.Allocate2D<Rgba32>(new Size(size)));

    [Theory]
    [MemberData(nameof(InvalidLengths))]
    public void Allocate_IncorrectAmount_ThrowsCorrect_InvalidMemoryOperationException_OverAligned(Size size)
        => Assert.Throws<InvalidMemoryOperationException>(() => this.MemoryAllocator.Allocate2DOveraligned<Rgba32>(size.Width, size.Height, 1));
}
