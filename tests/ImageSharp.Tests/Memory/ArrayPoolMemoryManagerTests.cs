// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

// ReSharper disable InconsistentNaming

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.Memory
{
    using System;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    using SixLabors.Memory;

    using Xunit;

    public class ArrayPoolMemoryManagerTests
    {
        private const int MaxPooledBufferSizeInBytes = 2048;

        private const int PoolSelectorThresholdInBytes = MaxPooledBufferSizeInBytes / 2;

        private MemoryAllocator MemoryAllocator { get; set; } = new ArrayPoolMemoryAllocator(MaxPooledBufferSizeInBytes, PoolSelectorThresholdInBytes);
        
        /// <summary>
        /// Rent a buffer -> return it -> re-rent -> verify if it's span points to the previous location
        /// </summary>
        private bool CheckIsRentingPooledBuffer<T>(int length)
            where T : struct
        {
            IBuffer<T> buffer = this.MemoryAllocator.Allocate<T>(length);
            ref T ptrToPrevPosition0 = ref buffer.GetReference();
            buffer.Dispose();
            
            buffer = this.MemoryAllocator.Allocate<T>(length);
            bool sameBuffers = Unsafe.AreSame(ref ptrToPrevPosition0, ref buffer.GetReference());
            buffer.Dispose();

            return sameBuffers;
        }

        public class BufferTests : BufferTestSuite
        {
            public BufferTests()
                : base(new ArrayPoolMemoryAllocator(MaxPooledBufferSizeInBytes, PoolSelectorThresholdInBytes))
            {
            }
        }

        public class Constructor
        {
            [Fact]
            public void WhenBothParametersPassedByUser()
            {
                var mgr = new ArrayPoolMemoryAllocator(1111, 666);
                Assert.Equal(1111, mgr.MaxPoolSizeInBytes);
                Assert.Equal(666, mgr.PoolSelectorThresholdInBytes);
            }

            [Fact]
            public void WhenPassedOnly_MaxPooledBufferSizeInBytes_SmallerThresholdValueIsAutoCalculated()
            {
                var mgr = new ArrayPoolMemoryAllocator(5000);
                Assert.Equal(5000, mgr.MaxPoolSizeInBytes);
                Assert.True(mgr.PoolSelectorThresholdInBytes < mgr.MaxPoolSizeInBytes);
            }

            [Fact]
            public void When_PoolSelectorThresholdInBytes_IsGreaterThan_MaxPooledBufferSizeInBytes_ExceptionIsThrown()
            {
                Assert.ThrowsAny<Exception>(() => { new ArrayPoolMemoryAllocator(100, 200); });
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
            if (!TestEnvironment.Is64BitProcess)
            {
                // can lead to OutOfMemoryException
                return;
            }

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
            if (!TestEnvironment.Is64BitProcess)
            {
                // can lead to OutOfMemoryException
                return;
            }

            int count = MaxPooledBufferSizeInBytes / sizeof(LargeStruct) + 1;

            Assert.False(this.CheckIsRentingPooledBuffer<LargeStruct>(count));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CleaningRequests_AreControlledByAllocationParameter_Clean(bool clean)
        {
            using (IBuffer<int> firstAlloc = this.MemoryAllocator.Allocate<int>(42))
            {
                firstAlloc.GetSpan().Fill(666);
            }

            using (IBuffer<int> secondAlloc = this.MemoryAllocator.Allocate<int>(42, clean))
            {
                int expected = clean ? 0 : 666;
                Assert.Equal(expected, secondAlloc.GetSpan()[0]);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ReleaseRetainedResources_ReplacesInnerArrayPool(bool keepBufferAlive)
        {
            IBuffer<int> buffer = this.MemoryAllocator.Allocate<int>(32);
            ref int ptrToPrev0 = ref MemoryMarshal.GetReference(buffer.GetSpan());

            if (!keepBufferAlive)
            {
                buffer.Dispose();
            }

            this.MemoryAllocator.ReleaseRetainedResources();
            
            buffer = this.MemoryAllocator.Allocate<int>(32);

            Assert.False(Unsafe.AreSame(ref ptrToPrev0, ref buffer.GetReference()));
        }

        [Fact]
        public void ReleaseRetainedResources_DisposingPreviouslyAllocatedBuffer_IsAllowed()
        {
            IBuffer<int> buffer = this.MemoryAllocator.Allocate<int>(32);
            this.MemoryAllocator.ReleaseRetainedResources();
            buffer.Dispose();
        }

        [Fact]
        public void AllocationOverLargeArrayThreshold_UsesDifferentPool()
        {
            if (!TestEnvironment.Is64BitProcess)
            {
                // can lead to OutOfMemoryException
                return;
            }

            int arrayLengthThreshold = PoolSelectorThresholdInBytes / sizeof(int);

            IBuffer<int> small = this.MemoryAllocator.Allocate<int>(arrayLengthThreshold - 1);
            ref int ptr2Small = ref small.GetReference();
            small.Dispose();

            IBuffer<int> large = this.MemoryAllocator.Allocate<int>(arrayLengthThreshold + 1);
            
            Assert.False(Unsafe.AreSame(ref ptr2Small, ref large.GetReference()));
        }

        [Fact]
        public void CreateWithAggressivePooling()
        {
            if (!TestEnvironment.Is64BitProcess)
            {
                // can lead to OutOfMemoryException
                return;
            }

            this.MemoryAllocator = ArrayPoolMemoryAllocator.CreateWithAggressivePooling();

            Assert.True(this.CheckIsRentingPooledBuffer<Rgba32>(4096 * 4096));
        }

        [Fact]
        public void CreateDefault()
        {
            if (!TestEnvironment.Is64BitProcess)
            {
                // can lead to OutOfMemoryException
                return;
            }

            this.MemoryAllocator = ArrayPoolMemoryAllocator.CreateDefault();

            Assert.False(this.CheckIsRentingPooledBuffer<Rgba32>(2 * 4096 * 4096));
            Assert.True(this.CheckIsRentingPooledBuffer<Rgba32>(2048 * 2048));
        }

        [Fact]
        public void CreateWithModeratePooling()
        {
            if (!TestEnvironment.Is64BitProcess)
            {
                // can lead to OutOfMemoryException
                return;
            }

            this.MemoryAllocator = ArrayPoolMemoryAllocator.CreateWithModeratePooling();

            Assert.False(this.CheckIsRentingPooledBuffer<Rgba32>(2048 * 2048));
            Assert.True(this.CheckIsRentingPooledBuffer<Rgba32>(1024 * 16));
        }
    }
}