// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.
using System;
using SixLabors.ImageSharp.Memory;
using Xunit;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Memory
{
    public class BufferAreaTests
    {
        private readonly TestMemoryAllocator memoryAllocator = new TestMemoryAllocator();

        [Fact]
        public void Construct()
        {
            using Buffer2D<int> buffer = this.memoryAllocator.Allocate2D<int>(10, 20);
            var rectangle = new Rectangle(3, 2, 5, 6);
            var area = new Buffer2DRegion<int>(buffer, rectangle);

            Assert.Equal(buffer, area.Buffer);
            Assert.Equal(rectangle, area.Rectangle);
        }

        private Buffer2D<int> CreateTestBuffer(int w, int h)
        {
            Buffer2D<int> buffer = this.memoryAllocator.Allocate2D<int>(w, h);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    buffer[x, y] = (y * 100) + x;
                }
            }

            return buffer;
        }

        [Theory]
        [InlineData(1000, 2, 3, 2, 2)]
        [InlineData(1000, 5, 4, 3, 2)]
        [InlineData(200, 2, 3, 2, 2)]
        [InlineData(200, 5, 4, 3, 2)]
        public void Indexer(int bufferCapacity, int rx, int ry, int x, int y)
        {
            this.memoryAllocator.BufferCapacityInBytes = sizeof(int) * bufferCapacity;
            using Buffer2D<int> buffer = this.CreateTestBuffer(20, 30);
            var r = new Rectangle(rx, ry, 5, 6);

            Buffer2DRegion<int> region = buffer.GetRegion(r);

            int value = region[x, y];
            int expected = ((ry + y) * 100) + rx + x;
            Assert.Equal(expected, value);
        }

        [Theory]
        [InlineData(1000, 2, 3, 2, 5, 6)]
        [InlineData(1000, 5, 4, 3, 6, 5)]
        [InlineData(200, 2, 3, 2, 5, 6)]
        [InlineData(200, 5, 4, 3, 6, 5)]
        public void GetRowSpan(int bufferCapacity, int rx, int ry, int y, int w, int h)
        {
            this.memoryAllocator.BufferCapacityInBytes = sizeof(int) * bufferCapacity;

            using Buffer2D<int> buffer = this.CreateTestBuffer(20, 30);
            var r = new Rectangle(rx, ry, w, h);

            Buffer2DRegion<int> region = buffer.GetRegion(r);

            Span<int> span = region.GetRowSpan(y);

            Assert.Equal(w, span.Length);

            for (int i = 0; i < w; i++)
            {
                int expected = ((ry + y) * 100) + rx + i;
                int value = span[i];

                Assert.Equal(expected, value);
            }
        }

        [Fact]
        public void GetSubArea()
        {
            using Buffer2D<int> buffer = this.CreateTestBuffer(20, 30);
            Buffer2DRegion<int> area0 = buffer.GetRegion(6, 8, 10, 10);

            Buffer2DRegion<int> area1 = area0.GetSubRegion(4, 4, 5, 5);

            var expectedRect = new Rectangle(10, 12, 5, 5);

            Assert.Equal(buffer, area1.Buffer);
            Assert.Equal(expectedRect, area1.Rectangle);

            int value00 = (12 * 100) + 10;
            Assert.Equal(value00, area1[0, 0]);
        }

        [Theory]
        [InlineData(1000)]
        [InlineData(40)]
        public void GetReferenceToOrigin(int bufferCapacity)
        {
            this.memoryAllocator.BufferCapacityInBytes = sizeof(int) * bufferCapacity;

            using Buffer2D<int> buffer = this.CreateTestBuffer(20, 30);
            Buffer2DRegion<int> area0 = buffer.GetRegion(6, 8, 10, 10);

            ref int r = ref area0.GetReferenceToOrigin();

            int expected = buffer[6, 8];
            Assert.Equal(expected, r);
        }

        [Theory]
        [InlineData(1000)]
        [InlineData(70)]
        public void Clear_FullArea(int bufferCapacity)
        {
            this.memoryAllocator.BufferCapacityInBytes = sizeof(int) * bufferCapacity;

            using Buffer2D<int> buffer = this.CreateTestBuffer(22, 13);
            var emptyRow = new int[22];
            buffer.GetRegion().Clear();

            for (int y = 0; y < 13; y++)
            {
                Span<int> row = buffer.GetRowSpan(y);
                Assert.True(row.SequenceEqual(emptyRow));
            }
        }

        [Theory]
        [InlineData(1000)]
        [InlineData(40)]
        public void Clear_SubArea(int bufferCapacity)
        {
            this.memoryAllocator.BufferCapacityInBytes = sizeof(int) * bufferCapacity;

            using Buffer2D<int> buffer = this.CreateTestBuffer(20, 30);
            Buffer2DRegion<int> region = buffer.GetRegion(5, 5, 10, 10);
            region.Clear();

            Assert.NotEqual(0, buffer[4, 4]);
            Assert.NotEqual(0, buffer[15, 15]);

            Assert.Equal(0, buffer[5, 5]);
            Assert.Equal(0, buffer[14, 14]);

            for (int y = region.Rectangle.Y; y < region.Rectangle.Bottom; y++)
            {
                Span<int> span = buffer.GetRowSpan(y).Slice(region.Rectangle.X, region.Width);
                Assert.True(span.SequenceEqual(new int[region.Width]));
            }
        }
    }
}
