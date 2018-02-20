// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Memory
{
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    using SixLabors.ImageSharp.Memory;

    using Xunit;

    public class ArrayPoolMemoryManagerTests
    {
        private const int MaxPooledBufferSizeInBytes = 2048;
        
        private MemoryManager MemoryManager { get; } = new ArrayPoolMemoryManager(MaxPooledBufferSizeInBytes);
        
        /// <summary>
        /// Rent a buffer -> return it -> re-rent -> verify if it's span points to the previous location
        /// </summary>
        private bool CheckIsRentingPooledBuffer<T>(int length)
            where T : struct
        {
            IBuffer<T> buffer = this.MemoryManager.Allocate<T>(length);
            ref T ptrToPrevPosition0 = ref buffer.DangerousGetPinnableReference();
            buffer.Dispose();
            
            buffer = this.MemoryManager.Allocate<T>(length);
            bool sameBuffers = Unsafe.AreSame(ref ptrToPrevPosition0, ref buffer.DangerousGetPinnableReference());
            buffer.Dispose();

            return sameBuffers;
        }

        public class BufferTests : BufferTestSuite
        {
            public BufferTests()
                : base(new ArrayPoolMemoryManager(MaxPooledBufferSizeInBytes))
            {
            }
        }

        [StructLayout(LayoutKind.Explicit, Size = MaxPooledBufferSizeInBytes / 5)]
        struct LargeStruct
        {
        }

        [Theory]
        [InlineData(32)]
        [InlineData(512)]
        [InlineData(MaxPooledBufferSizeInBytes - 1)]
        public void SmallBuffersArePooled_OfByte(int size)
        {
            Assert.True(this.CheckIsRentingPooledBuffer<byte>(size));
        }


        [Theory]
        [InlineData(128 * 1024 * 1024)]
        [InlineData(MaxPooledBufferSizeInBytes + 1)]
        public void LargeBuffersAreNotPooled_OfByte(int size)
        {
            Assert.False(this.CheckIsRentingPooledBuffer<byte>(size));
        }

        [Fact]
        public unsafe void SmallBuffersArePooled_OfBigValueType()
        {
            int count = MaxPooledBufferSizeInBytes / sizeof(LargeStruct) - 1;

            Assert.True(this.CheckIsRentingPooledBuffer<LargeStruct>(count));
        }

        [Fact]
        public unsafe void LaregeBuffersAreNotPooled_OfBigValueType()
        {
            int count = MaxPooledBufferSizeInBytes / sizeof(LargeStruct) + 1;

            Assert.False(this.CheckIsRentingPooledBuffer<LargeStruct>(count));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CleaningRequests_AreControlledByAllocationParameter_Clean(bool clean)
        {
            using (IBuffer<int> firstAlloc = this.MemoryManager.Allocate<int>(42))
            {
                firstAlloc.Span.Fill(666);
            }

            using (IBuffer<int> secondAlloc = this.MemoryManager.Allocate<int>(42, clean))
            {
                int expected = clean ? 0 : 666;
                Assert.Equal(expected, secondAlloc.Span[0]);
            }
        }
    }
}