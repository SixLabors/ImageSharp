// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using SixLabors.ImageSharp.Memory;

using Xunit;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Memory
{
    public class Buffer2DTests
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

        private TestMemoryAllocator MemoryAllocator { get; } = new TestMemoryAllocator();

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
                Assert.Equal(0, buffer.GetSingleSpan().Length);
            }
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
                Span<int> span = buffer.GetSingleSpan();
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
        public unsafe void GetRowSpanY(int bufferCapacity, int width, int height, int y, int expectedBufferIndex)
        {
            this.MemoryAllocator.BufferCapacityInBytes = sizeof(TestStructs.Foo) * bufferCapacity;

            using (Buffer2D<TestStructs.Foo> buffer = this.MemoryAllocator.Allocate2D<TestStructs.Foo>(width, height))
            {
                Span<TestStructs.Foo> span = buffer.GetRowSpan(y);

                Assert.Equal(width, span.Length);

                int expectedSubBufferOffset = (width * y) - (expectedBufferIndex * buffer.FastMemoryGroup.BufferLength);
                Assert.SpanPointsTo(span, buffer.FastMemoryGroup[expectedBufferIndex], expectedSubBufferOffset);
            }
        }

        public static TheoryData<int, int, int, int> GetRowSpanY_OutOfRange_Data = new TheoryData<int, int, int, int>()
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

            Exception ex = Assert.ThrowsAny<Exception>(() => buffer.GetRowSpan(y));
            Assert.True(ex is ArgumentOutOfRangeException || ex is IndexOutOfRangeException);
        }

        public static TheoryData<int, int, int, int, int> Indexer_OutOfRange_Data = new TheoryData<int, int, int, int, int>()
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

        [Fact]
        public void SwapOrCopyContent_WhenBothAllocated()
        {
            using (Buffer2D<int> a = this.MemoryAllocator.Allocate2D<int>(10, 5, AllocationOptions.Clean))
            using (Buffer2D<int> b = this.MemoryAllocator.Allocate2D<int>(3, 7, AllocationOptions.Clean))
            {
                a[1, 3] = 666;
                b[1, 3] = 444;

                Memory<int> aa = a.FastMemoryGroup.Single();
                Memory<int> bb = b.FastMemoryGroup.Single();

                Buffer2D<int>.SwapOrCopyContent(a, b);

                Assert.Equal(bb, a.FastMemoryGroup.Single());
                Assert.Equal(aa, b.FastMemoryGroup.Single());

                Assert.Equal(new Size(3, 7), a.Size());
                Assert.Equal(new Size(10, 5), b.Size());

                Assert.Equal(666, b[1, 3]);
                Assert.Equal(444, a[1, 3]);
            }
        }

        [Fact]
        public void SwapOrCopyContent_WhenDestinationIsOwned_ShouldNotSwapInDisposedSourceBuffer()
        {
            using var destData = MemoryGroup<int>.Wrap(new int[100]);
            using var dest = new Buffer2D<int>(destData, 10, 10);

            using (Buffer2D<int> source = this.MemoryAllocator.Allocate2D<int>(10, 10, AllocationOptions.Clean))
            {
                source[0, 0] = 1;
                dest[0, 0] = 2;

                Buffer2D<int>.SwapOrCopyContent(dest, source);
            }

            int actual1 = dest.GetRowSpan(0)[0];
            int actual2 = dest.GetRowSpan(0)[0];
            int actual3 = dest.GetSafeRowMemory(0).Span[0];
            int actual4 = dest.GetFastRowMemory(0).Span[0];
            int actual5 = dest[0, 0];

            Assert.Equal(1, actual1);
            Assert.Equal(1, actual2);
            Assert.Equal(1, actual3);
            Assert.Equal(1, actual4);
            Assert.Equal(1, actual5);
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
            var rnd = new Random(123);
            using (Buffer2D<float> b = this.MemoryAllocator.Allocate2D<float>(width, height))
            {
                rnd.RandomFill(b.GetSingleSpan(), 0, 1);

                b.CopyColumns(startIndex, destIndex, columnCount);

                for (int y = 0; y < b.Height; y++)
                {
                    Span<float> row = b.GetRowSpan(y);

                    Span<float> s = row.Slice(startIndex, columnCount);
                    Span<float> d = row.Slice(destIndex, columnCount);

                    Xunit.Assert.True(s.SequenceEqual(d));
                }
            }
        }

        [Fact]
        public void CopyColumns_InvokeMultipleTimes()
        {
            var rnd = new Random(123);
            using (Buffer2D<float> b = this.MemoryAllocator.Allocate2D<float>(100, 100))
            {
                rnd.RandomFill(b.GetSingleSpan(), 0, 1);

                b.CopyColumns(0, 50, 22);
                b.CopyColumns(0, 50, 22);

                for (int y = 0; y < b.Height; y++)
                {
                    Span<float> row = b.GetRowSpan(y);

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
    }
}
