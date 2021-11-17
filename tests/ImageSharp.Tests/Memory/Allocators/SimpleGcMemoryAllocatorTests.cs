// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Memory.Allocators
{
    public class SimpleGcMemoryAllocatorTests
    {
        public class BufferTests : BufferTestSuite
        {
            public BufferTests()
                : base(new SimpleGcMemoryAllocator())
            {
            }
        }

        protected SimpleGcMemoryAllocator MemoryAllocator { get; } = new SimpleGcMemoryAllocator();

        [Theory]
        [InlineData(-1)]
        public void Allocate_IncorrectAmount_ThrowsCorrect_ArgumentOutOfRangeException(int length)
        {
            ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => this.MemoryAllocator.Allocate<BigStruct>(length));
            Assert.Equal("length", ex.ParamName);
        }

        [Theory]
        [InlineData(-1)]
        public void AllocateManagedByteBuffer_IncorrectAmount_ThrowsCorrect_ArgumentOutOfRangeException(int length)
        {
            ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => this.MemoryAllocator.AllocateManagedByteBuffer(length));
            Assert.Equal("length", ex.ParamName);
        }

        [Fact]
        public unsafe void Allocate_MemoryIsPinnableMultipleTimes()
        {
            SimpleGcMemoryAllocator allocator = this.MemoryAllocator;
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
}
