using System.Collections.Generic;
using System.Runtime.CompilerServices;
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
                    maxContiguousPoolBufferInBytes: 2048,
                    maxPoolSizeInBytes: 2048 * 4,
                    maxCapacityOfUnmanagedBuffers: 4096);

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
                    maxContiguousPoolBufferInBytes: 1024,
                    maxPoolSizeInBytes: 1024 * 4,
                    maxCapacityOfUnmanagedBuffers: 2048);

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
    }
}
