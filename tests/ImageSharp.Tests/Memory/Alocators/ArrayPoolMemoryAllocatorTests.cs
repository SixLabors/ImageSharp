// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

// ReSharper disable InconsistentNaming
using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.DotNet.RemoteExecutor;
using SixLabors.ImageSharp.Tests;
using Xunit;

namespace SixLabors.ImageSharp.Memory.Tests
{
    public class ArrayPoolMemoryAllocatorTests
    {
        private const int MaxPooledBufferSizeInBytes = 2048;

        private const int PoolSelectorThresholdInBytes = MaxPooledBufferSizeInBytes / 2;

        /// <summary>
        /// Contains SUT for in-process tests.
        /// </summary>
        private MemoryAllocatorFixture LocalFixture { get; } = new MemoryAllocatorFixture();

        /// <summary>
        /// Contains SUT for tests executed by <see cref="RemoteExecutor"/>,
        /// recreated in each external process.
        /// </summary>
        private static MemoryAllocatorFixture StaticFixture { get; } = new MemoryAllocatorFixture();

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
                Assert.ThrowsAny<Exception>(() => new ArrayPoolMemoryAllocator(100, 200));
            }
        }

        [Theory]
        [InlineData(32)]
        [InlineData(512)]
        [InlineData(MaxPooledBufferSizeInBytes - 1)]
        public void SmallBuffersArePooled_OfByte(int size)
        {
            Assert.True(this.LocalFixture.CheckIsRentingPooledBuffer<byte>(size));
        }

        [Theory]
        [InlineData(128 * 1024 * 1024)]
        [InlineData(MaxPooledBufferSizeInBytes + 1)]
        public void LargeBuffersAreNotPooled_OfByte(int size)
        {
            static void RunTest(string sizeStr)
            {
                int size = int.Parse(sizeStr);
                StaticFixture.CheckIsRentingPooledBuffer<byte>(size);
            }

            RemoteExecutor.Invoke(RunTest, size.ToString()).Dispose();
        }

        [Fact]
        public unsafe void SmallBuffersArePooled_OfBigValueType()
        {
            int count = (MaxPooledBufferSizeInBytes / sizeof(LargeStruct)) - 1;

            Assert.True(this.LocalFixture.CheckIsRentingPooledBuffer<LargeStruct>(count));
        }

        [Fact]
        public unsafe void LaregeBuffersAreNotPooled_OfBigValueType()
        {
            int count = (MaxPooledBufferSizeInBytes / sizeof(LargeStruct)) + 1;

            Assert.False(this.LocalFixture.CheckIsRentingPooledBuffer<LargeStruct>(count));
        }

        [Theory]
        [InlineData(AllocationOptions.None)]
        [InlineData(AllocationOptions.Clean)]
        public void CleaningRequests_AreControlledByAllocationParameter_Clean(AllocationOptions options)
        {
            MemoryAllocator memoryAllocator = this.LocalFixture.MemoryAllocator;
            using (IMemoryOwner<int> firstAlloc = memoryAllocator.Allocate<int>(42))
            {
                firstAlloc.GetSpan().Fill(666);
            }

            using (IMemoryOwner<int> secondAlloc = memoryAllocator.Allocate<int>(42, options))
            {
                int expected = options == AllocationOptions.Clean ? 0 : 666;
                Assert.Equal(expected, secondAlloc.GetSpan()[0]);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ReleaseRetainedResources_ReplacesInnerArrayPool(bool keepBufferAlive)
        {
            MemoryAllocator memoryAllocator = this.LocalFixture.MemoryAllocator;
            IMemoryOwner<int> buffer = memoryAllocator.Allocate<int>(32);
            ref int ptrToPrev0 = ref MemoryMarshal.GetReference(buffer.GetSpan());

            if (!keepBufferAlive)
            {
                buffer.Dispose();
            }

            memoryAllocator.ReleaseRetainedResources();

            buffer = memoryAllocator.Allocate<int>(32);

            Assert.False(Unsafe.AreSame(ref ptrToPrev0, ref buffer.GetReference()));
        }

        [Fact]
        public void ReleaseRetainedResources_DisposingPreviouslyAllocatedBuffer_IsAllowed()
        {
            MemoryAllocator memoryAllocator = this.LocalFixture.MemoryAllocator;
            IMemoryOwner<int> buffer = memoryAllocator.Allocate<int>(32);
            memoryAllocator.ReleaseRetainedResources();
            buffer.Dispose();
        }


        [Fact]
        public void AllocationOverLargeArrayThreshold_UsesDifferentPool()
        {
            static void RunTest()
            {
                const int ArrayLengthThreshold = PoolSelectorThresholdInBytes / sizeof(int);

                IMemoryOwner<int> small = StaticFixture.MemoryAllocator.Allocate<int>(ArrayLengthThreshold - 1);
                ref int ptr2Small = ref small.GetReference();
                small.Dispose();

                IMemoryOwner<int> large = StaticFixture.MemoryAllocator.Allocate<int>(ArrayLengthThreshold + 1);

                Assert.False(Unsafe.AreSame(ref ptr2Small, ref large.GetReference()));
            }

            RemoteExecutor.Invoke(RunTest).Dispose();
        }

        [Fact]
        public void CreateWithAggressivePooling()
        {
            static void RunTest()
            {
                StaticFixture.MemoryAllocator = ArrayPoolMemoryAllocator.CreateWithAggressivePooling();
                Assert.True(StaticFixture.CheckIsRentingPooledBuffer<SmallStruct>(4096 * 4096));
            }

            RemoteExecutor.Invoke(RunTest).Dispose();
        }

        [Fact]
        public void CreateDefault()
        {
            static void RunTest()
            {
                StaticFixture.MemoryAllocator = ArrayPoolMemoryAllocator.CreateDefault();

                Assert.False(StaticFixture.CheckIsRentingPooledBuffer<SmallStruct>(2 * 4096 * 4096));
                Assert.True(StaticFixture.CheckIsRentingPooledBuffer<SmallStruct>(2048 * 2048));
            }

            RemoteExecutor.Invoke(RunTest).Dispose();
        }

        [Fact]
        public void CreateWithModeratePooling()
        {
            static void RunTest()
            {
                StaticFixture.MemoryAllocator = ArrayPoolMemoryAllocator.CreateWithModeratePooling();
                Assert.False(StaticFixture.CheckIsRentingPooledBuffer<SmallStruct>(2048 * 2048));
                Assert.True(StaticFixture.CheckIsRentingPooledBuffer<SmallStruct>(1024 * 16));
            }

            RemoteExecutor.Invoke(RunTest).Dispose();
        }

        [Theory]
        [InlineData(-1)]
        [InlineData((int.MaxValue / SizeOfLargeStruct) + 1)]
        public void AllocateIncorrectAmount_ThrowsCorrect_ArgumentOutOfRangeException(int length)
        {
            ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
                this.LocalFixture.MemoryAllocator.Allocate<LargeStruct>(length));
            Assert.Equal("length", ex.ParamName);
        }

        [Theory]
        [InlineData(-1)]
        public void AllocateManagedByteBuffer_IncorrectAmount_ThrowsCorrect_ArgumentOutOfRangeException(int length)
        {
            ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
                this.LocalFixture.MemoryAllocator.AllocateManagedByteBuffer(length));
            Assert.Equal("length", ex.ParamName);
        }

        private class MemoryAllocatorFixture
        {
            public MemoryAllocator MemoryAllocator { get; set; } =
                new ArrayPoolMemoryAllocator(MaxPooledBufferSizeInBytes, PoolSelectorThresholdInBytes);

            /// <summary>
            /// Rent a buffer -> return it -> re-rent -> verify if it's span points to the previous location.
            /// </summary>
            public bool CheckIsRentingPooledBuffer<T>(int length)
                where T : struct
            {
                IMemoryOwner<T> buffer = MemoryAllocator.Allocate<T>(length);
                ref T ptrToPrevPosition0 = ref buffer.GetReference();
                buffer.Dispose();

                buffer = MemoryAllocator.Allocate<T>(length);
                bool sameBuffers = Unsafe.AreSame(ref ptrToPrevPosition0, ref buffer.GetReference());
                buffer.Dispose();

                return sameBuffers;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SmallStruct
        {
            private readonly uint dummy;
        }

        private const int SizeOfLargeStruct = MaxPooledBufferSizeInBytes / 5;

        [StructLayout(LayoutKind.Explicit, Size = SizeOfLargeStruct)]
        private struct LargeStruct
        {
        }
    }
}
