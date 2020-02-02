using System.Linq;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Memory.DiscontiguousBuffers
{
    public class MemoryGroupTests
    {
        public class Allocate
        {
            private readonly TestMemoryAllocator memoryAllocator = new TestMemoryAllocator();

#pragma warning disable SA1509
            public static TheoryData<object, int, int, long, int, int, int> AllocateData =
                new TheoryData<object, int, int, long, int, int, int>()
                {
                    { default(S5), 22, 4, 4, 1, 4, 4 },
                    { default(S5), 22, 4, 7, 2, 4, 3 },
                    { default(S5), 22, 4, 8, 2, 4, 4 },
                    { default(S5), 22, 4, 21, 5, 4, 1 },
                    { default(S5), 22, 4, 0, 0, -1, -1 },

                    { default(S4), 50, 12, 12, 1, 12, 12 },
                    { default(S4), 50, 7, 12, 2, 7, 5 },
                    { default(S4), 50, 6, 12, 2, 6, 6 },
                    { default(S4), 50, 5, 12, 2, 10, 2 },
                    { default(S4), 50, 4, 12, 1, 12, 12 },
                    { default(S4), 50, 3, 12, 1, 12, 12 },
                    { default(S4), 50, 2, 12, 1, 12, 12 },
                    { default(S4), 50, 1, 12, 1, 12, 12 },

                    { default(S4), 50, 12, 13, 2, 12, 1 },
                    { default(S4), 50, 7, 21, 3, 7, 7 },
                    { default(S4), 50, 7, 23, 3, 7, 2 },

                    { default(byte), 1000, 512, 2047, 4, 512, 511 }
                };

            [Theory]
            [MemberData(nameof(AllocateData))]
            public void CreatesBlocksOfCorrectSizes<T>(
                T dummy,
                int blockCapacity,
                int blockAlignment,
                long totalLength,
                int expectedNumberOfBlocks,
                int expectedBlockSize,
                int expectedSizeOfLastBlock)
                where T : struct
            {
                this.memoryAllocator.BlockCapacity = blockCapacity;

                // Act:
                using var g = MemoryGroup<T>.Allocate(this.memoryAllocator, totalLength, blockAlignment);

                // Assert:
                Assert.Equal(expectedNumberOfBlocks, g.Count);
                Assert.Equal(expectedBlockSize, g.BlockSize);
                if (g.Count == 0)
                {
                    return;
                }

                for (int i = 0; i < g.Count - 1; i++)
                {
                    Assert.Equal(g[i].Length, expectedBlockSize);
                }

                Assert.Equal(g.Last().Length, expectedSizeOfLastBlock);
            }

            [Fact]
            public void WhenBlockAlignmentIsOverCapacity_Throws_InvalidMemoryOperationException()
            {
                this.memoryAllocator.BlockCapacity = 42;

                Assert.Throws<InvalidMemoryOperationException>(() =>
                {
                    MemoryGroup<byte>.Allocate(this.memoryAllocator, 50, 43);
                });
            }
        }


        [StructLayout(LayoutKind.Sequential, Size = 5)]
        private struct S5
        {
        }

        [StructLayout(LayoutKind.Sequential, Size = 4)]
        private struct S4
        {
        }
    }
}
