// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Tests.Memory
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using SixLabors.Memory;
    using SixLabors.ImageSharp.Tests.Common;
    using SixLabors.Primitives;

    using Xunit;

    public class Buffer2DTests
    {
        // ReSharper disable once ClassNeverInstantiated.Local
        private class Assert : Xunit.Assert
        {
            public static void SpanPointsTo<T>(Span<T> span, IBuffer<T> buffer, int bufferOffset = 0)
                where T : struct
            {
                ref T actual = ref MemoryMarshal.GetReference(span);
                ref T expected = ref Unsafe.Add(ref buffer.GetReference(), bufferOffset);

                Assert.True(Unsafe.AreSame(ref expected, ref actual), "span does not point to the expected position");
            }
        }

        private MemoryAllocator MemoryAllocator { get; } = new MockMemoryAllocator();

        private class MockMemoryAllocator : MemoryAllocator
        {
            internal override IBuffer<T> Allocate<T>(int length, bool clear)
            {
                var array = new T[length + 42];

                if (!clear)
                {
                    Span<byte> data = MemoryMarshal.Cast<T, byte>(array.AsSpan());
                    for (int i = 0; i < data.Length; i++)
                    {
                        data[i] = 42;
                    }
                }

                return new BasicArrayBuffer<T>(array, length);
            }

            internal override IManagedByteBuffer AllocateManagedByteBuffer(int length, bool clear)
            {
                throw new NotImplementedException();
            }
        }

        [Theory]
        [InlineData(7, 42)]
        [InlineData(1025, 17)]
        public void Construct(int width, int height)
        {
            using (Buffer2D<TestStructs.Foo> buffer = this.MemoryAllocator.Allocate2D<TestStructs.Foo>(width, height))
            {
                Assert.Equal(width, buffer.Width);
                Assert.Equal(height, buffer.Height);
                Assert.Equal(width * height, buffer.Buffer.Length());
            }
        }

        [Fact]
        public void CreateClean()
        {
            using (Buffer2D<int> buffer = this.MemoryAllocator.Allocate2D<int>(42, 42, true))
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
                Assert.SpanPointsTo(span, buffer.Buffer, width * y);
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
                Assert.SpanPointsTo(span, buffer.Buffer, width * y + x);
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
                Span<TestStructs.Foo> span = buffer.Buffer.GetSpan();

                ref TestStructs.Foo actual = ref buffer[x, y];

                ref TestStructs.Foo expected = ref span[y * width + x];

                Assert.True(Unsafe.AreSame(ref expected, ref actual));
            }
        }

        [Fact]
        public void SwapContents()
        {
            using (Buffer2D<int> a = this.MemoryAllocator.Allocate2D<int>(10, 5))
            using (Buffer2D<int> b = this.MemoryAllocator.Allocate2D<int>(3, 7))
            {
                IBuffer<int> aa = a.Buffer;
                IBuffer<int> bb = b.Buffer;

                Buffer2D<int>.SwapContents(a, b);

                Assert.Equal(bb, a.Buffer);
                Assert.Equal(new Size(3, 7), a.Size());
                Assert.Equal(new Size(10, 5), b.Size());
            }
        }
    }
}