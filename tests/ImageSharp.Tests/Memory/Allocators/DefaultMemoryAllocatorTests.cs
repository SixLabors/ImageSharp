// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.DotNet.RemoteExecutor;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Tests.Memory.DiscontiguousBuffers;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Memory.Allocators
{
    public class DefaultMemoryAllocatorTests
    {
        public class BufferTests1 : BufferTestSuite
        {
            private static MemoryAllocator CreateMemoryAllocator() =>
                new DefaultMemoryAllocator(
                    sharedArrayPoolThresholdInBytes: 1024,
                    poolBufferSizeInBytes: 2048,
                    maxPoolSizeInBytes: 2048 * 4,
                    unmanagedBufferSizeInBytes: 4096);

            public BufferTests1()
                : base(CreateMemoryAllocator())
            {
            }
        }

        public class BufferTests2 : BufferTestSuite
        {
            private static MemoryAllocator CreateMemoryAllocator() =>
                new DefaultMemoryAllocator(
                    sharedArrayPoolThresholdInBytes: 512,
                    poolBufferSizeInBytes: 1024,
                    maxPoolSizeInBytes: 1024 * 4,
                    unmanagedBufferSizeInBytes: 2048);

            public BufferTests2()
                : base(CreateMemoryAllocator())
            {
            }
        }

        public static TheoryData<object, int, int, int, int, long, int, int, int, int> AllocateData =
            new TheoryData<object, int, int, int, int, long, int, int, int, int>()
            {
                { default(S4), 16, 256, 256, 1024, 64, 64, 1, -1, 64 },
                { default(S4), 16, 256, 256, 1024, 256, 256, 1, -1, 256 },
                { default(S4), 16, 256, 256, 1024, 250, 256, 1, -1, 250 },
                { default(S4), 16, 256, 256, 1024, 248, 250, 1, -1, 248 },
                { default(S4), 16, 1024, 2048, 4096, 512, 256, 2, 256, 256 },
                { default(S4), 16, 1024, 1024, 1024, 511, 256, 2, 256, 255 },
            };

        [Theory]
        [MemberData(nameof(AllocateData))]
        public void AllocateGroup_BufferSizesAreCorrect<T>(
            T dummy,
            int sharedArrayPoolThresholdInBytes,
            int maxContiguousPoolBufferInBytes,
            int maxPoolSizeInBytes,
            int maxCapacityOfUnmanagedBuffers,
            long allocationLengthInElements,
            int bufferAlignmentInElements,
            int expectedNumberOfBuffers,
            int expectedBufferSize,
            int expectedSizeOfLastBuffer)
            where T : struct
        {
            var allocator = new DefaultMemoryAllocator(
                sharedArrayPoolThresholdInBytes,
                maxContiguousPoolBufferInBytes,
                maxPoolSizeInBytes,
                maxCapacityOfUnmanagedBuffers);

            using MemoryGroup<T> g = allocator.AllocateGroup<T>(allocationLengthInElements, bufferAlignmentInElements);
            MemoryGroupTests.Allocate.ValidateAllocateMemoryGroup(
                expectedNumberOfBuffers,
                expectedBufferSize,
                expectedSizeOfLastBuffer,
                g);
        }

        [Fact]
        public void AllocateGroup_MultipleTimes_ExceedPoolLimit()
        {
            var allocator = new DefaultMemoryAllocator(
                64,
                128,
                1024,
                1024);

            var groups = new List<MemoryGroup<S4>>();
            for (int i = 0; i < 16; i++)
            {
                int lengthInElements = 128 / Unsafe.SizeOf<S4>();
                MemoryGroup<S4> g = allocator.AllocateGroup<S4>(lengthInElements, 32);
                MemoryGroupTests.Allocate.ValidateAllocateMemoryGroup(1, -1, lengthInElements, g);
                groups.Add(g);
            }

            foreach (MemoryGroup<S4> g in groups)
            {
                g.Dispose();
            }
        }

        [Theory]
        [InlineData(512)]
        [InlineData(2048)]
        [InlineData(8192)]
        [InlineData(65536)]
        public void AllocateGroup_OptionsContiguous_AllocatesContiguousBuffer(int lengthInBytes)
        {
            var allocator = new DefaultMemoryAllocator(
                128,
                1024,
                2048,
                4096);
            int length = lengthInBytes / Unsafe.SizeOf<S4>();
            using MemoryGroup<S4> g = allocator.AllocateGroup<S4>(length, 32, AllocationOptions.Contiguous);
            Assert.Equal(length, g.BufferLength);
            Assert.Equal(length, g.TotalLength);
            Assert.Equal(1, g.Count);
        }

        [Fact]
        public void MemoryAllocator_CreateDefault_WithoutOptions_AllocatesDiscontiguousMemory()
        {
            RemoteExecutor.Invoke(RunTest).Dispose();

            static void RunTest()
            {
                var allocator = MemoryAllocator.CreateDefault();
                long sixteenMegabytes = 16 * (1 << 20);

                // Should allocate 4 times 4MB discontiguos blocks:
                MemoryGroup<byte> g = allocator.AllocateGroup<byte>(sixteenMegabytes, 1024);
                Assert.Equal(4, g.Count);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void MemoryAllocator_CreateDefault_WithOptions_CanForceContiguousAllocation(bool poolAllocation)
        {
            RemoteExecutor.Invoke(RunTest, poolAllocation.ToString()).Dispose();

            static void RunTest(string poolAllocationStr)
            {
                int fortyEightMegabytes = 48 * (1 << 20);
                var allocator = MemoryAllocator.CreateDefault(new MemoryAllocatorOptions()
                {
                    MaximumPoolSizeMegabytes = bool.Parse(poolAllocationStr) ? 64 : 0,
                    MinimumContiguousBlockBytes = fortyEightMegabytes
                });

                MemoryGroup<byte> g = allocator.AllocateGroup<byte>(fortyEightMegabytes, 1024);
                Assert.Equal(1, g.Count);
                Assert.Equal(fortyEightMegabytes, g.TotalLength);
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void BufferDisposal_ReturnsToPool(bool shared)
        {
            RemoteExecutor.Invoke(RunTest, shared.ToString()).Dispose();

            static void RunTest(string sharedStr)
            {
                var allocator = new DefaultMemoryAllocator(512, 1024, 16 * 1024, 1024);
                IMemoryOwner<byte> buffer0 = allocator.Allocate<byte>(bool.Parse(sharedStr) ? 300 : 600);
                buffer0.GetSpan()[0] = 42;
                buffer0.Dispose();
                using IMemoryOwner<byte> buffer1 = allocator.Allocate<byte>(bool.Parse(sharedStr) ? 300 : 600);
                Assert.Equal(42, buffer1.GetSpan()[0]);
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void BufferFinalizer_ReturnsToPool(bool shared)
        {
            RemoteExecutor.Invoke(RunTest, shared.ToString()).Dispose();

            static void RunTest(string sharedStr)
            {
                var allocator = new DefaultMemoryAllocator(512, 1024, 16 * 1024, 1024, 0.0f);
                bool sharedStrInner = bool.Parse(sharedStr);

                AllocateBufferAndForget(allocator, sharedStrInner);
                Thread.Sleep(200);
                GC.Collect();
                GC.WaitForPendingFinalizers();

                using IMemoryOwner<byte> b = allocator.Allocate<byte>(bool.Parse(sharedStr) ? 300 : 600);
                Assert.Equal(42, b.GetSpan()[0]);
            }
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        private static void AllocateBufferAndForget(DefaultMemoryAllocator allocator, bool sharedInner)
        {
            IMemoryOwner<byte> b0 = allocator.Allocate<byte>(sharedInner ? 300 : 600);
            b0.GetSpan()[0] = 42;

            if (sharedInner)
            {
                // For ArrayPool.Shared, first array will be returned to the TLS storage of the finalizer thread,
                // repeat rental to make sure per-core buckets are also utilized.
                IMemoryOwner<byte> b1 = allocator.Allocate<byte>(sharedInner ? 300 : 600);
                b1.GetSpan()[0] = 42;
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void MemoryGroupDisposal_ReturnsToPool(bool shared)
        {
            RemoteExecutor.Invoke(RunTest, shared.ToString()).Dispose();

            static void RunTest(string sharedStr)
            {
                var allocator = new DefaultMemoryAllocator(512, 1024, 16 * 1024, 1024);
                MemoryGroup<byte> g0 = allocator.AllocateGroup<byte>(bool.Parse(sharedStr) ? 300 : 600, 100);
                g0.Single().Span[0] = 42;
                g0.Dispose();
                using MemoryGroup<byte> g1 = allocator.AllocateGroup<byte>(bool.Parse(sharedStr) ? 300 : 600, 100);
                Assert.Equal(42, g1.Single().Span[0]);
            }

        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void MemoryGroupFinalizer_ReturnsToPool(bool shared)
        {
            RemoteExecutor.Invoke(RunTest, shared.ToString()).Dispose();

            static void RunTest(string sharedStr)
            {
                var allocator = new DefaultMemoryAllocator(512, 1024, 16 * 1024, 1024, 0.0f);
                bool sharedInner = bool.Parse(sharedStr);

                AllocateGroupAndForget(allocator, sharedInner);
                GC.Collect();
                GC.WaitForPendingFinalizers();

                using MemoryGroup<byte> g = allocator.AllocateGroup<byte>(sharedInner ? 300 : 600, 100);
                Assert.Equal(42, g.Single().Span[0]);
            }
        }

        private static void AllocateGroupAndForget(DefaultMemoryAllocator allocator, bool sharedInner)
        {
            MemoryGroup<byte> g0 = allocator.AllocateGroup<byte>(sharedInner ? 300 : 600, 100);
            g0.Single().Span[0] = 42;
            if (sharedInner)
            {
                // For ArrayPool.Shared, first array will be returned to the TLS storage of the finalizer thread,
                // repeat rental to make sure per-core buckets are also utilized.
                MemoryGroup<byte> g1 = allocator.AllocateGroup<byte>(sharedInner ? 300 : 600, 100);
                g1.Single().Span[0] = 42;
            }
        }
    }
}
