// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using System.Linq;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Tests.Memory;
using Xunit;

namespace SixLabors.ImageSharp.Tests.IO
{
    public class FixedCapacityPooledMemoryStreamTests
    {
        private readonly TestMemoryAllocator memoryAllocator = new TestMemoryAllocator();

        [Theory]
        [InlineData(1)]
        [InlineData(512)]
        public void RentsManagedBuffer(int length)
        {
            MemoryStream ms = this.memoryAllocator.AllocateFixedCapacityMemoryStream(length);
            Assert.Equal(length, this.memoryAllocator.AllocationLog.Single().Length);
            ms.Dispose();
            Assert.Equal(1, this.memoryAllocator.ReturnLog.Count);
        }

        [Theory]
        [InlineData(42)]
        [InlineData(2999)]
        public void UsesRentedBuffer(int length)
        {
            using MemoryStream ms = this.memoryAllocator.AllocateFixedCapacityMemoryStream(length);
            ms.TryGetBuffer(out ArraySegment<byte> buffer);
            byte[] array = buffer.Array;
            Assert.Equal(array.GetHashCode(), this.memoryAllocator.AllocationLog.Single().HashCodeOfBuffer);

            ms.Write(new byte[] { 123 });
            Assert.Equal(123, array[0]);
        }
    }
}
