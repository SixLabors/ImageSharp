// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Memory.Internals;

namespace SixLabors.ImageSharp.Tests.Memory.DiscontiguousBuffers;

public partial class MemoryGroupTests
{
    public class Allocate : MemoryGroupTestsBase
    {
#pragma warning disable SA1509
        public static TheoryData<object, int, int, long, int, int, int> AllocateData = new()
            {
                { default(S5), 22, 4, 4, 1, 4, 4 },
                { default(S5), 22, 4, 7, 2, 4, 3 },
                { default(S5), 22, 4, 8, 2, 4, 4 },
                { default(S5), 22, 4, 21, 6, 4, 1 },

                // empty:
                { default(S5), 22, 0, 0, 1, -1, 0 },
                { default(S5), 22, 4, 0, 1, -1, 0 },

                { default(S4), 50, 12, 12, 1, 12, 12 },
                { default(S4), 50, 7, 12, 2, 7, 5 },
                { default(S4), 50, 6, 12, 1, 12, 12 },
                { default(S4), 50, 5, 12, 2, 10, 2 },
                { default(S4), 50, 4, 12, 1, 12, 12 },
                { default(S4), 50, 3, 12, 1, 12, 12 },
                { default(S4), 50, 2, 12, 1, 12, 12 },
                { default(S4), 50, 1, 12, 1, 12, 12 },

                { default(S4), 50, 12, 13, 2, 12, 1 },
                { default(S4), 50, 7, 21, 3, 7, 7 },
                { default(S4), 50, 7, 23, 4, 7, 2 },
                { default(S4), 50, 6, 13, 2, 12, 1 },
                { default(S4), 1024, 20, 800, 4, 240, 80 },

                { default(short), 200, 50, 49, 1, 49, 49 },
                { default(short), 200, 50, 1, 1, 1, 1 },
                { default(byte), 1000, 512, 2047, 4, 512, 511 }
            };

        [Theory]
        [MemberData(nameof(AllocateData))]
        public void Allocate_FromMemoryAllocator_BufferSizesAreCorrect<T>(
            T dummy,
            int bufferCapacity,
            int bufferAlignment,
            long totalLength,
            int expectedNumberOfBuffers,
            int expectedBufferSize,
            int expectedSizeOfLastBuffer)
            where T : struct
        {
            this.MemoryAllocator.BufferCapacityInBytes = bufferCapacity;

            // Act:
            using MemoryGroup<T> g = MemoryGroup<T>.Allocate(this.MemoryAllocator, totalLength, bufferAlignment);

            // Assert:
            ValidateAllocateMemoryGroup(expectedNumberOfBuffers, expectedBufferSize, expectedSizeOfLastBuffer, g);
        }

        [Theory]
        [MemberData(nameof(AllocateData))]
        public void Allocate_FromPool_BufferSizesAreCorrect<T>(
            T dummy,
            int bufferCapacity,
            int bufferAlignment,
            long totalLength,
            int expectedNumberOfBuffers,
            int expectedBufferSize,
            int expectedSizeOfLastBuffer)
            where T : struct
        {
            if (totalLength == 0)
            {
                // Invalid case for UniformByteArrayPool allocations
                return;
            }

            UniformUnmanagedMemoryPool pool = new UniformUnmanagedMemoryPool(bufferCapacity, expectedNumberOfBuffers);

            // Act:
            Assert.True(MemoryGroup<T>.TryAllocate(pool, totalLength, bufferAlignment, AllocationOptions.None, out MemoryGroup<T> g));

            // Assert:
            ValidateAllocateMemoryGroup(expectedNumberOfBuffers, expectedBufferSize, expectedSizeOfLastBuffer, g);
            g.Dispose();
        }

        [Theory]
        [InlineData(AllocationOptions.None)]
        [InlineData(AllocationOptions.Clean)]
        public unsafe void Allocate_FromPool_AllocationOptionsAreApplied(AllocationOptions options)
        {
            UniformUnmanagedMemoryPool pool = new UniformUnmanagedMemoryPool(10, 5);
            UnmanagedMemoryHandle[] buffers = pool.Rent(5);
            foreach (UnmanagedMemoryHandle b in buffers)
            {
                new Span<byte>(b.Pointer, pool.BufferLength).Fill(42);
            }

            pool.Return(buffers);

            Assert.True(MemoryGroup<byte>.TryAllocate(pool, 50, 10, options, out MemoryGroup<byte> g));
            Span<byte> expected = stackalloc byte[10];
            expected.Fill((byte)(options == AllocationOptions.Clean ? 0 : 42));
            foreach (Memory<byte> memory in g)
            {
                Assert.True(expected.SequenceEqual(memory.Span));
            }

            g.Dispose();
        }

        [Theory]
        [InlineData(64, 4, 60, 240, true)]
        [InlineData(64, 4, 60, 244, false)]
        public void Allocate_FromPool_AroundLimit(
            int bufferCapacityBytes,
            int poolCapacity,
            int alignmentBytes,
            int requestBytes,
            bool shouldSucceed)
        {
            UniformUnmanagedMemoryPool pool = new UniformUnmanagedMemoryPool(bufferCapacityBytes, poolCapacity);
            int alignmentElements = alignmentBytes / Unsafe.SizeOf<S4>();
            int requestElements = requestBytes / Unsafe.SizeOf<S4>();

            Assert.Equal(shouldSucceed, MemoryGroup<S4>.TryAllocate(pool, requestElements, alignmentElements, AllocationOptions.None, out MemoryGroup<S4> g));
            if (shouldSucceed)
            {
                Assert.NotNull(g);
            }
            else
            {
                Assert.Null(g);
            }

            g?.Dispose();
        }

        internal static void ValidateAllocateMemoryGroup<T>(
            int expectedNumberOfBuffers,
            int expectedBufferSize,
            int expectedSizeOfLastBuffer,
            MemoryGroup<T> g)
            where T : struct
        {
            Assert.Equal(expectedNumberOfBuffers, g.Count);

            if (expectedBufferSize >= 0)
            {
                Assert.Equal(expectedBufferSize, g.BufferLength);
            }

            if (g.Count == 0)
            {
                return;
            }

            for (int i = 0; i < g.Count - 1; i++)
            {
                Assert.Equal(g[i].Length, expectedBufferSize);
            }

            Assert.Equal(g.Last().Length, expectedSizeOfLastBuffer);
        }

        [Fact]
        public void WhenBlockAlignmentIsOverCapacity_Throws_InvalidMemoryOperationException()
        {
            this.MemoryAllocator.BufferCapacityInBytes = 84; // 42 * Int16

            Assert.Throws<InvalidMemoryOperationException>(() =>
            {
                MemoryGroup<short>.Allocate(this.MemoryAllocator, 50, 43);
            });
        }

        [Theory]
        [InlineData(AllocationOptions.None)]
        [InlineData(AllocationOptions.Clean)]
        public void MemoryAllocatorIsUtilizedCorrectly(AllocationOptions allocationOptions)
        {
            this.MemoryAllocator.BufferCapacityInBytes = 200;
            this.MemoryAllocator.EnableNonThreadSafeLogging();

            HashSet<int> bufferHashes;

            int expectedBlockCount = 5;
            using (MemoryGroup<short> g = MemoryGroup<short>.Allocate(this.MemoryAllocator, 500, 100, allocationOptions))
            {
                IReadOnlyList<TestMemoryAllocator.AllocationRequest> allocationLog = this.MemoryAllocator.AllocationLog;
                Assert.Equal(expectedBlockCount, allocationLog.Count);
                bufferHashes = allocationLog.Select(l => l.HashCodeOfBuffer).ToHashSet();
                Assert.Equal(expectedBlockCount, bufferHashes.Count);
                Assert.Equal(0, this.MemoryAllocator.ReturnLog.Count);

                for (int i = 0; i < expectedBlockCount; i++)
                {
                    Assert.Equal(allocationOptions, allocationLog[i].AllocationOptions);
                    Assert.Equal(100, allocationLog[i].Length);
                    Assert.Equal(200, allocationLog[i].LengthInBytes);
                }
            }

            Assert.Equal(expectedBlockCount, this.MemoryAllocator.ReturnLog.Count);
            Assert.True(bufferHashes.SetEquals(this.MemoryAllocator.ReturnLog.Select(l => l.HashCodeOfBuffer)));
        }
    }
}

[StructLayout(LayoutKind.Sequential, Size = 5)]
internal struct S5
{
    public override readonly string ToString() => nameof(S5);
}

[StructLayout(LayoutKind.Sequential, Size = 4)]
internal struct S4
{
    public override readonly string ToString() => nameof(S4);
}

[StructLayout(LayoutKind.Explicit, Size = 512)]
internal struct S512
{
    public override readonly string ToString() => nameof(S512);
}
