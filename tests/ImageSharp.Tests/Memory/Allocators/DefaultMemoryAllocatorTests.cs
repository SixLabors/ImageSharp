using SixLabors.ImageSharp.Memory;

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
    }
}