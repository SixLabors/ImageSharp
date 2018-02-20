// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Memory
{
    using System;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    using SixLabors.ImageSharp.Memory;

    using Xunit;

    public class ArrayPoolMemoryManagerTests
    {
        private const int MaxPooledBufferSizeInBytes = 2048;

        private const int LargeBufferThresholdInBytes = MaxPooledBufferSizeInBytes / 2;

        private MemoryManager MemoryManager { get; } = new ArrayPoolMemoryManager(MaxPooledBufferSizeInBytes, LargeBufferThresholdInBytes);
        
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
                : base(new ArrayPoolMemoryManager(MaxPooledBufferSizeInBytes, LargeBufferThresholdInBytes))
            {
            }
        }

        public class Constructor
        {
            [Fact]
            public void WhenBothParametersPassedByUser()
            {
                var mgr = new ArrayPoolMemoryManager(1111, 666);
                Assert.Equal(1111, mgr.MaxPoolSizeInBytes);
                Assert.Equal(666, mgr.LargeBufferThresholdInBytes);
            }

            [Fact]
            public void WhenPassedOnly_MaxPooledBufferSizeInBytes_SmallerThresholdIsAutoCalculated()
            {
                var mgr = new ArrayPoolMemoryManager(5000);
                Assert.Equal(5000, mgr.MaxPoolSizeInBytes);
                Assert.True(mgr.LargeBufferThresholdInBytes < mgr.MaxPoolSizeInBytes);
            }

            [Fact]
            public void When_LargeBufferThresholdInBytes_IsGreaterThan_MaxPooledBufferSizeInBytes_Throws()
            {
                Assert.ThrowsAny<Exception>(() => { new ArrayPoolMemoryManager(100, 200); });
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

        [Fact]
        public void ReleaseRetainedResources_ReplacesInnerArrayPool()
        {
            IBuffer<int> buffer = this.MemoryManager.Allocate<int>(32);
            ref int ptrToPrev0 = ref buffer.Span.DangerousGetPinnableReference();
            buffer.Dispose();

            this.MemoryManager.ReleaseRetainedResources();
            buffer = this.MemoryManager.Allocate<int>(32);

            Assert.False(Unsafe.AreSame(ref ptrToPrev0, ref buffer.DangerousGetPinnableReference()));
        }

        [Fact]
        public void ReleaseRetainedResources_DisposingPreviouslyAllocatedBuffer_IsAllowed()
        {
            IBuffer<int> buffer = this.MemoryManager.Allocate<int>(32);
            this.MemoryManager.ReleaseRetainedResources();
            buffer.Dispose();
        }

        [Fact]
        public void AllocationOverLargeArrayThreshold_UsesDifferentPool()
        {
            int arrayLengthThreshold = LargeBufferThresholdInBytes / sizeof(int);

            IBuffer<int> small = this.MemoryManager.Allocate<int>(arrayLengthThreshold - 1);
            ref int ptr2Small = ref small.DangerousGetPinnableReference();
            small.Dispose();

            IBuffer<int> large = this.MemoryManager.Allocate<int>(arrayLengthThreshold + 1);
            
            Assert.False(Unsafe.AreSame(ref ptr2Small, ref large.DangerousGetPinnableReference()));
        }
    }
}