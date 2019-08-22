// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Memory;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Helpers
{
    public class RowIntervalTests
    {
        [Theory]
        [InlineData(10, 20, 5, 10)]
        [InlineData(1, 10, 0, 10)]
        [InlineData(1, 10, 5, 8)]
        [InlineData(1, 1, 0, 1)]
        [InlineData(10, 20, 9, 10)]
        [InlineData(10, 20, 0, 1)]
        public void GetMultiRowSpan(int width, int height, int min, int max)
        {
            using (Buffer2D<int> buffer = Configuration.Default.MemoryAllocator.Allocate2D<int>(width, height))
            {
                var rows = new RowInterval(min, max);

                Span<int> span = buffer.GetMultiRowSpan(rows);

                ref int expected0 = ref buffer.Span[min * width];
                int expectedLength = (max - min) * width;

                ref int actual0 = ref span[0];

                Assert.Equal(span.Length, expectedLength);
                Assert.True(Unsafe.AreSame(ref expected0, ref actual0));
            }
        }

        [Fact]
        public void Slice1()
        {
            var rowInterval = new RowInterval(10, 20);
            RowInterval sliced = rowInterval.Slice(5);

            Assert.Equal(15, sliced.Min);
            Assert.Equal(20, sliced.Max);
        }

        [Fact]
        public void Slice2()
        {
            var rowInterval = new RowInterval(10, 20);
            RowInterval sliced = rowInterval.Slice(3, 5);

            Assert.Equal(13, sliced.Min);
            Assert.Equal(18, sliced.Max);
        }

        [Fact]
        public void Equality_WhenTrue()
        {
            var a = new RowInterval(42, 123);
            var b = new RowInterval(42, 123);

            Assert.True(a.Equals(b));
            Assert.True(a.Equals((object)b));
            Assert.True(a == b);
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
        }

        [Fact]
        public void Equality_WhenFalse()
        {
            var a = new RowInterval(42, 123);
            var b = new RowInterval(42, 125);
            var c = new RowInterval(40, 123);

            Assert.False(a.Equals(b));
            Assert.False(c.Equals(a));
            Assert.False(b.Equals(c));

            Assert.False(a.Equals((object)b));
            Assert.False(a.Equals(null));
            Assert.False(a == b);
            Assert.True(a != c);
        }
    }
}
