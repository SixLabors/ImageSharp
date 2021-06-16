// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.DotNet.RemoteExecutor;
using SixLabors.ImageSharp.Memory;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Memory.Allocators
{
    public class ArrayPoolMemoryAllocatorTests
    {
        private const int MaxPooledBufferSizeInBytes = 1024 * 1024 * 2;
        private const int PoolSelectorThresholdInBytes = MaxPooledBufferSizeInBytes / 2;

        /// <summary>
        /// Gets the SUT for in-process tests.
        /// </summary>
        private MemoryAllocatorFixture LocalFixture { get; } = new MemoryAllocatorFixture();

        /// <summary>
        /// Gets the SUT for tests executed by <see cref="RemoteExecutor"/>,
        /// recreated in each external process.
        /// </summary>
        private static MemoryAllocatorFixture StaticFixture { get; } = new MemoryAllocatorFixture();

        public class BufferTests : BufferTestSuite
        {
            public BufferTests()
                : base(new ArrayPoolMemoryAllocator(MaxPooledBufferSizeInBytes))
            {
            }
        }

        public class Constructor
        {
            [Fact]
            public void WhenParameterPassedByUser()
            {
                var mgr = new ArrayPoolMemoryAllocator(MaxPooledBufferSizeInBytes);
                Assert.Equal(MaxPooledBufferSizeInBytes, mgr.MaxPooledArrayLengthInBytes);
            }

            [Fact]
            public void WhenAllParametersPassedByUser()
            {
                var mgr = new ArrayPoolMemoryAllocator(MaxPooledBufferSizeInBytes, 1, 2);
                Assert.Equal(MaxPooledBufferSizeInBytes, mgr.MaxPooledArrayLengthInBytes);
                Assert.Equal(1, mgr.MaxArraysPerPoolBucket);
                Assert.Equal(2, mgr.GetMaxContiguousArrayLengthInBytes());
            }

            [Fact]
            public void When_MaxPooledBufferSizeInBytes_SmallerThan_ThresholdValue_ExceptionIsThrown()
                => Assert.ThrowsAny<Exception>(() => new ArrayPoolMemoryAllocator(100));

            [Fact]
            public void When_BucketCount_IsZero_ExceptionIsThrown()
                => Assert.ThrowsAny<Exception>(() => new ArrayPoolMemoryAllocator(MaxPooledBufferSizeInBytes, 0, 1));

            [Fact]
            public void When_BufferCapacityThresholdInBytes_IsZero_ExceptionIsThrown()
                => Assert.ThrowsAny<Exception>(() => new ArrayPoolMemoryAllocator(MaxPooledBufferSizeInBytes, 1, 0));
        }

        [Theory]
        [InlineData(32)]
        [InlineData(512)]
        [InlineData(MaxPooledBufferSizeInBytes - 1)]
        public void SmallBuffersArePooled_OfByte(int size)
            => Assert.True(this.LocalFixture.CheckIsRentingPooledBuffer<byte>(size));

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

        [Fact(Skip = "It looks like the GC is forming an unmanaged pool which makes this test flaky.")]
        public unsafe void LargeBuffersAreNotPooled_OfBigValueType()
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
            IMemoryOwner<int> buffer = memoryAllocator.Allocate<int>(MaxPooledBufferSizeInBytes + 1);
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
                const int arrayLengthThreshold = PoolSelectorThresholdInBytes / sizeof(int);

                IMemoryOwner<int> small = StaticFixture.MemoryAllocator.Allocate<int>(arrayLengthThreshold - 1);
                ref int ptr2Small = ref small.GetReference();
                small.Dispose();

                IMemoryOwner<int> large = StaticFixture.MemoryAllocator.Allocate<int>(arrayLengthThreshold + 1);

                Assert.False(Unsafe.AreSame(ref ptr2Small, ref large.GetReference()));
            }

            RemoteExecutor.Invoke(RunTest).Dispose();
        }

        [Fact]
        public void CreateDefault()
        {
            static void RunTest()
            {
                StaticFixture.MemoryAllocator = ArrayPoolMemoryAllocator.CreateDefault();

                if (TestEnvironment.IsWindows)
                {
                    // TODO: We should have an attribute for this kind of stuff.
                    // This test passes locally but not in the UNIX CI.
                    // This could be due to the GC simply returning the same buffer
                    // from unmanaged memory but this requires confirmation.
                    Assert.False(StaticFixture.CheckIsRentingPooledBuffer<SmallStruct>(2 * 4096 * 4096));
                }

                Assert.True(StaticFixture.CheckIsRentingPooledBuffer<SmallStruct>(1024 * 16));
            }

            RemoteExecutor.Invoke(RunTest).Dispose();
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(-111)]
        public void Allocate_Negative_Throws_ArgumentOutOfRangeException(int length)
        {
            ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
                this.LocalFixture.MemoryAllocator.Allocate<LargeStruct>(length));
            Assert.Equal("length", ex.ParamName);
        }

        [Fact]
        public void AllocateZero()
        {
            using IMemoryOwner<int> buffer = this.LocalFixture.MemoryAllocator.Allocate<int>(0);
            Assert.Equal(0, buffer.Memory.Length);
        }

        private class MemoryAllocatorFixture
        {
            public ArrayPoolMemoryAllocator MemoryAllocator { get; set; } =
                new ArrayPoolMemoryAllocator(MaxPooledBufferSizeInBytes);

            /// <summary>
            /// Rent a buffer -> return it -> re-rent -> verify if it's span points to the previous location.
            /// </summary>
            /// <typeparam name="T">The type of buffer elements.</typeparam>
            /// <param name="length">The length of the requested buffer.</param>
            public bool CheckIsRentingPooledBuffer<T>(int length)
                where T : struct
            {
                IMemoryOwner<T> buffer = this.MemoryAllocator.Allocate<T>(length);
                ref T ptrToPrevPosition0 = ref buffer.GetReference();
                buffer.Dispose();

                buffer = this.MemoryAllocator.Allocate<T>(length);
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

        private const int SizeOfLargeStruct = MaxPooledBufferSizeInBytes / 512 / 5;

        [StructLayout(LayoutKind.Explicit, Size = SizeOfLargeStruct)]
        private struct LargeStruct
        {
        }
    }
}
