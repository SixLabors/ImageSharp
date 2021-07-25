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
    }
}