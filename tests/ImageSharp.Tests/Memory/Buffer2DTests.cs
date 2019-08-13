// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using SixLabors.ImageSharp.Memory;
using SixLabors.Memory;
using SixLabors.Primitives;

using Xunit;

// ReSharper disable InconsistentNaming

namespace SixLabors.ImageSharp.Tests.Memory
{
    public class Buffer2DTests
    {
        // ReSharper disable once ClassNeverInstantiated.Local
        private class Assert : Xunit.Assert
        {
            public static void SpanPointsTo<T>(Span<T> span, IMemoryOwner<T> buffer, int bufferOffset = 0)
                where T : struct
            {
                ref T actual = ref MemoryMarshal.GetReference(span);
                ref T expected = ref Unsafe.Add(ref buffer.GetReference(), bufferOffset);

                True(Unsafe.AreSame(ref expected, ref actual), "span does not point to the expected position");
            }
        }

        private MemoryAllocator MemoryAllocator { get; } = new TestMemoryAllocator();

        [Theory]
        [InlineData(7, 42)]
        [InlineData(1025, 17)]
        public void Construct(int width, int height)
        {
            using (Buffer2D<TestStructs.Foo> buffer = this.MemoryAllocator.Allocate2D<TestStructs.Foo>(width, height))
            {
                Assert.Equal(width, buffer.Width);
                Assert.Equal(height, buffer.Height);
                Assert.Equal(width * height, buffer.Memory.Length);
            }
        }

        [Fact]
        public void CreateClean()
        {
            using (Buffer2D<int> buffer = this.MemoryAllocator.Allocate2D<int>(42, 42, AllocationOptions.Clean))
            {
                Span<int> span = buffer.GetSpan();
                for (int j = 0; j < span.Length; j++)
                {
                    Assert.Equal(0, span[j]);
                }
            }
        }

        [Theory]
        [InlineData(7, 42, 0)]
        [InlineData(7, 42, 10)]
        [InlineData(17, 42, 41)]
        public void GetRowSpanY(int width, int height, int y)
        {
            using (Buffer2D<TestStructs.Foo> buffer = this.MemoryAllocator.Allocate2D<TestStructs.Foo>(width, height))
            {
                Span<TestStructs.Foo> span = buffer.GetRowSpan(y);

                // Assert.Equal(width * y, span.Start);
                Assert.Equal(width, span.Length);
                Assert.SpanPointsTo(span, buffer.MemorySource.MemoryOwner, width * y);
            }
        }

        [Theory]
        [InlineData(7, 42, 0, 0)]
        [InlineData(7, 42, 3, 10)]
        [InlineData(17, 42, 0, 41)]
        public void GetRowSpanXY(int width, int height, int x, int y)
        {
            using (Buffer2D<TestStructs.Foo> buffer = this.MemoryAllocator.Allocate2D<TestStructs.Foo>(width, height))
            {
                Span<TestStructs.Foo> span = buffer.GetRowSpan(x, y);

                // Assert.Equal(width * y + x, span.Start);
                Assert.Equal(width - x, span.Length);
                Assert.SpanPointsTo(span, buffer.MemorySource.MemoryOwner, width * y + x);
            }
        }

        [Theory]
        [InlineData(42, 8, 0, 0)]
        [InlineData(400, 1000, 20, 10)]
        [InlineData(99, 88, 98, 87)]
        public void Indexer(int width, int height, int x, int y)
        {
            using (Buffer2D<TestStructs.Foo> buffer = this.MemoryAllocator.Allocate2D<TestStructs.Foo>(width, height))
            {
                Span<TestStructs.Foo> span = buffer.MemorySource.GetSpan();

                ref TestStructs.Foo actual = ref buffer[x, y];

                ref TestStructs.Foo expected = ref span[y * width + x];

                Assert.True(Unsafe.AreSame(ref expected, ref actual));
            }
        }

        [Fact]
        public void SwapOrCopyContent()
        {
            using (Buffer2D<int> a = this.MemoryAllocator.Allocate2D<int>(10, 5))
            using (Buffer2D<int> b = this.MemoryAllocator.Allocate2D<int>(3, 7))
            {
                IMemoryOwner<int> aa = a.MemorySource.MemoryOwner;
                IMemoryOwner<int> bb = b.MemorySource.MemoryOwner;

                Buffer2D<int>.SwapOrCopyContent(a, b);

                Assert.Equal(bb, a.MemorySource.MemoryOwner);
                Assert.Equal(aa, b.MemorySource.MemoryOwner);

                Assert.Equal(new Size(3, 7), a.Size());
                Assert.Equal(new Size(10, 5), b.Size());
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
            var rnd = new Random(123);
            using (Buffer2D<float> b = this.MemoryAllocator.Allocate2D<float>(width, height))
            {
                rnd.RandomFill(b.Span, 0, 1);

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
                rnd.RandomFill(b.Span, 0, 1);

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
    }
}
