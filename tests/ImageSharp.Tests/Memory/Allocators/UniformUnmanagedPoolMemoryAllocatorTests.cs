// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.DotNet.RemoteExecutor;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Memory.Internals;
using SixLabors.ImageSharp.Tests.Memory.DiscontiguousBuffers;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Memory.Allocators
{
    public class UniformUnmanagedPoolMemoryAllocatorTests
    {
        public class BufferTests1 : BufferTestSuite
        {
            private static MemoryAllocator CreateMemoryAllocator() =>
                new UniformUnmanagedMemoryPoolMemoryAllocator(
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
                new UniformUnmanagedMemoryPoolMemoryAllocator(
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
            var allocator = new UniformUnmanagedMemoryPoolMemoryAllocator(
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
            var allocator = new UniformUnmanagedMemoryPoolMemoryAllocator(
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
            var allocator = new UniformUnmanagedMemoryPoolMemoryAllocator(
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
        public unsafe void Allocate_MemoryIsPinnableMultipleTimes()
        {
            var allocator = new UniformUnmanagedMemoryPoolMemoryAllocator(null);
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

        [Fact]
        public void MemoryAllocator_Create_WithoutSettings_AllocatesDiscontiguousMemory()
        {
            RemoteExecutor.Invoke(RunTest).Dispose();

            static void RunTest()
            {
                var allocator = MemoryAllocator.Create();
                long sixteenMegabytes = 16 * (1 << 20);

                // Should allocate 4 times 4MB discontiguos blocks:
                MemoryGroup<byte> g = allocator.AllocateGroup<byte>(sixteenMegabytes, 1024);
                Assert.Equal(4, g.Count);
            }
        }

        [Fact]
        public void MemoryAllocator_Create_LimitPoolSize()
        {
            RemoteExecutor.Invoke(RunTest).Dispose();

            static void RunTest()
            {
                var allocator = MemoryAllocator.Create(new MemoryAllocatorSettings()
                {
                    MaximumPoolSizeMegabytes = 8
                });

                MemoryGroup<byte> g0 = allocator.AllocateGroup<byte>(B(8), 1024);
                MemoryGroup<byte> g1 = allocator.AllocateGroup<byte>(B(8), 1024);
                ref byte r0 = ref MemoryMarshal.GetReference(g0[0].Span);
                ref byte r1 = ref MemoryMarshal.GetReference(g1[0].Span);
                g0.Dispose();
                g1.Dispose();

                // Do some unmanaged allocations to make sure new non-pooled unmanaged allocations will grab different memory:
                IntPtr dummy1 = Marshal.AllocHGlobal((IntPtr)B(8));
                IntPtr dummy2 = Marshal.AllocHGlobal((IntPtr)B(8));

                using MemoryGroup<byte> g2 = allocator.AllocateGroup<byte>(B(8), 1024);
                using MemoryGroup<byte> g3 = allocator.AllocateGroup<byte>(B(8), 1024);
                ref byte r2 = ref MemoryMarshal.GetReference(g2[0].Span);
                ref byte r3 = ref MemoryMarshal.GetReference(g3[0].Span);

                Assert.True(Unsafe.AreSame(ref r0, ref r2));
                Assert.False(Unsafe.AreSame(ref r1, ref r3));

                Marshal.FreeHGlobal(dummy1);
                Marshal.FreeHGlobal(dummy2);
            }

            static long B(int value) => value << 20;
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void BufferDisposal_ReturnsToPool(bool shared)
        {
            RemoteExecutor.Invoke(RunTest, shared.ToString()).Dispose();

            static void RunTest(string sharedStr)
            {
                var allocator = new UniformUnmanagedMemoryPoolMemoryAllocator(512, 1024, 16 * 1024, 1024);
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
        public void MemoryGroupDisposal_ReturnsToPool(bool shared)
        {
            RemoteExecutor.Invoke(RunTest, shared.ToString()).Dispose();

            static void RunTest(string sharedStr)
            {
                var allocator = new UniformUnmanagedMemoryPoolMemoryAllocator(512, 1024, 16 * 1024, 1024);
                MemoryGroup<byte> g0 = allocator.AllocateGroup<byte>(bool.Parse(sharedStr) ? 300 : 600, 100);
                g0.Single().Span[0] = 42;
                g0.Dispose();
                using MemoryGroup<byte> g1 = allocator.AllocateGroup<byte>(bool.Parse(sharedStr) ? 300 : 600, 100);
                Assert.Equal(42, g1.Single().Span[0]);
            }
        }

        [Fact]
        public void ReleaseRetainedResources_ShouldFreePooledMemory()
        {
            RemoteExecutor.Invoke(RunTest).Dispose();
            static void RunTest()
            {
                var allocator = new UniformUnmanagedMemoryPoolMemoryAllocator(128, 512, 16 * 512, 1024);
                MemoryGroup<byte> g = allocator.AllocateGroup<byte>(2048, 128);
                g.Dispose();
                Assert.Equal(4, UnmanagedMemoryHandle.TotalOutstandingHandles);
                allocator.ReleaseRetainedResources();
                Assert.Equal(0, UnmanagedMemoryHandle.TotalOutstandingHandles);
            }
        }

        [Fact]
        public void ReleaseRetainedResources_DoesNotFreeOutstandingBuffers()
        {
            RemoteExecutor.Invoke(RunTest).Dispose();
            static void RunTest()
            {
                var allocator = new UniformUnmanagedMemoryPoolMemoryAllocator(128, 512, 16 * 512, 1024);
                IMemoryOwner<byte> b = allocator.Allocate<byte>(256);
                MemoryGroup<byte> g = allocator.AllocateGroup<byte>(2048, 128);
                Assert.Equal(5, UnmanagedMemoryHandle.TotalOutstandingHandles);
                allocator.ReleaseRetainedResources();
                Assert.Equal(5, UnmanagedMemoryHandle.TotalOutstandingHandles);
                b.Dispose();
                g.Dispose();
                Assert.Equal(0, UnmanagedMemoryHandle.TotalOutstandingHandles);
            }
        }

        public static bool IsWindows => TestEnvironment.IsWindows;

        // TODO: This doesn't seem to work on Unix. Open an issue & investigate.
        [ConditionalTheory(nameof(IsWindows))]
        [InlineData(300)]
        [InlineData(600)]
        [InlineData(1200)]
        public void MemoryGroupFinalizer_ReturnsToPool(int length)
        {
            // RunTest(length.ToString());
            RemoteExecutor.Invoke(RunTest, length.ToString()).Dispose();

            static void RunTest(string lengthStr)
            {
                var allocator = new UniformUnmanagedMemoryPoolMemoryAllocator(512, 1024, 16 * 1024, 1024);
                int lengthInner = int.Parse(lengthStr);

                AllocateGroupAndForget(allocator, lengthInner);
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                GC.WaitForPendingFinalizers();

                AllocateGroupAndForget(allocator, lengthInner, true);
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                GC.WaitForPendingFinalizers();

                using MemoryGroup<byte> g = allocator.AllocateGroup<byte>(lengthInner, 100);
                Assert.Equal(42, g.First().Span[0]);
            }
        }

        private static void AllocateGroupAndForget(UniformUnmanagedMemoryPoolMemoryAllocator allocator, int length, bool check = false)
        {
            MemoryGroup<byte> g = allocator.AllocateGroup<byte>(length, 100);
            if (check)
            {
                Assert.Equal(42, g.First().Span[0]);
            }

            g.First().Span[0] = 42;

            if (length < 512)
            {
                // For ArrayPool.Shared, first array will be returned to the TLS storage of the finalizer thread,
                // repeat rental to make sure per-core buckets are also utilized.
                MemoryGroup<byte> g1 = allocator.AllocateGroup<byte>(length, 100);
                g1.First().Span[0] = 42;
            }
        }
    }
}
