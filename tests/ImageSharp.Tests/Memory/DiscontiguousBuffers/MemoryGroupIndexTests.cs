// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using Xunit;

namespace SixLabors.ImageSharp.Tests.Memory.DiscontiguousBuffers
{
    public class MemoryGroupIndexTests
    {
        [Fact]
        public void Equal()
        {
            var a = new MemoryGroupIndex(10, 1, 3);
            var b = new MemoryGroupIndex(10, 1, 3);

            Assert.True(a.Equals(b));
            Assert.True(a == b);
            Assert.False(a != b);
            Assert.False(a < b);
            Assert.False(a > b);
        }

        [Fact]
        public void SmallerBufferIndex()
        {
            var a = new MemoryGroupIndex(10, 3, 3);
            var b = new MemoryGroupIndex(10, 5, 3);

            Assert.False(a == b);
            Assert.True(a != b);
            Assert.True(a < b);
            Assert.False(a > b);
        }

        [Fact]
        public void SmallerElementIndex()
        {
            var a = new MemoryGroupIndex(10, 3, 3);
            var b = new MemoryGroupIndex(10, 3, 9);

            Assert.False(a == b);
            Assert.True(a != b);
            Assert.True(a < b);
            Assert.False(a > b);
        }

        [Fact]
        public void Increment()
        {
            var a = new MemoryGroupIndex(10, 3, 3);
            a += 1;
            Assert.Equal(new MemoryGroupIndex(10, 3, 4), a);
        }

        [Fact]
        public void Increment_OverflowBuffer()
        {
            var a = new MemoryGroupIndex(10, 5, 3);
            var b = new MemoryGroupIndex(10, 5, 9);
            a += 8;
            b += 1;

            Assert.Equal(new MemoryGroupIndex(10, 6, 1), a);
            Assert.Equal(new MemoryGroupIndex(10, 6, 0), b);
        }
    }
}
