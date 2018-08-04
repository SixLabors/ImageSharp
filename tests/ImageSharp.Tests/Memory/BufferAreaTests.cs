// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.
using System;
using SixLabors.ImageSharp.Memory;
using SixLabors.Primitives;
using Xunit;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Memory
{
    public class BufferAreaTests
    {
        [Fact]
        public void Construct()
        {
            using (var buffer = Configuration.Default.MemoryAllocator.Allocate2D<int>(10, 20))
            {
                var rectangle = new Rectangle(3, 2, 5, 6);
                var area = new BufferArea<int>(buffer, rectangle);

                Assert.Equal(buffer, area.DestinationBuffer);
                Assert.Equal(rectangle, area.Rectangle);
            }
        }

        private static Buffer2D<int> CreateTestBuffer(int w, int h)
        {
            var buffer = Configuration.Default.MemoryAllocator.Allocate2D<int>(w, h);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    buffer[x, y] = y * 100 + x;
                }
            }

            return buffer;
        }

        [Theory]
        [InlineData(2, 3, 2, 2)]
        [InlineData(5, 4, 3, 2)]
        public void Indexer(int rx, int ry, int x, int y)
        {
            using (Buffer2D<int> buffer = CreateTestBuffer(20, 30))
            {
                Rectangle r = new Rectangle(rx, ry, 5, 6);

                BufferArea<int> area = buffer.GetArea(r);

                int value = area[x, y];
                int expected = (ry + y) * 100 + rx + x;
                Assert.Equal(expected, value);
            }
        }

        [Theory]
        [InlineData(2, 3, 2, 5, 6)]
        [InlineData(5, 4, 3, 6, 5)]
        public void GetRowSpan(int rx, int ry, int y, int w, int h)
        {
            using (Buffer2D<int> buffer = CreateTestBuffer(20, 30))
            {
                Rectangle r = new Rectangle(rx, ry, w, h);

                BufferArea<int> area = buffer.GetArea(r);

                Span<int> span = area.GetRowSpan(y);

                Assert.Equal(w, span.Length);

                for (int i = 0; i < w; i++)
                {
                    int expected = (ry + y) * 100 + rx + i;
                    int value = span[i];

                    Assert.Equal(expected, value);
                }
            }
        }

        [Fact]
        public void GetSubArea()
        {
            using (Buffer2D<int> buffer = CreateTestBuffer(20, 30))
            {
                BufferArea<int> area0 = buffer.GetArea(6, 8, 10, 10);

                BufferArea<int> area1 = area0.GetSubArea(4, 4, 5, 5);

                var expectedRect = new Rectangle(10, 12, 5, 5);

                Assert.Equal(buffer, area1.DestinationBuffer);
                Assert.Equal(expectedRect, area1.Rectangle);

                int value00 = 12 * 100 + 10;
                Assert.Equal(value00, area1[0, 0]);
            }
        }

        [Fact]
        public void DangerousGetPinnableReference()
        {
            using (Buffer2D<int> buffer = CreateTestBuffer(20, 30))
            {
                BufferArea<int> area0 = buffer.GetArea(6, 8, 10, 10);

                ref int r = ref area0.GetReferenceToOrigin();

                int expected = buffer[6, 8];
                Assert.Equal(expected, r);
            }
        }

        [Fact]
        public void Clear_FullArea()
        {
            using (Buffer2D<int> buffer = CreateTestBuffer(22, 13))
            {
                buffer.GetArea().Clear();
                Span<int> fullSpan = buffer.GetSpan();
                Assert.True(fullSpan.SequenceEqual(new int[fullSpan.Length]));
            }
        }

        [Fact]
        public void Clear_SubArea()
        {
            using (Buffer2D<int> buffer = CreateTestBuffer(20, 30))
            {
                BufferArea<int> area = buffer.GetArea(5, 5, 10, 10);
                area.Clear();

                Assert.NotEqual(0, buffer[4, 4]);
                Assert.NotEqual(0, buffer[15, 15]);

                Assert.Equal(0, buffer[5, 5]);
                Assert.Equal(0, buffer[14, 14]);

                for (int y = area.Rectangle.Y; y < area.Rectangle.Bottom; y++)
                {
                    Span<int> span = buffer.GetRowSpan(y).Slice(area.Rectangle.X, area.Width);
                    Assert.True(span.SequenceEqual(new int[area.Width]));
                }
            }
        }
    }
}