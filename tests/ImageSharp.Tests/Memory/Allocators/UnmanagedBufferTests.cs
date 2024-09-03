// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using Microsoft.DotNet.RemoteExecutor;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Memory.Internals;

namespace SixLabors.ImageSharp.Tests.Memory.Allocators;

public class UnmanagedBufferTests
{
    public class AllocatorBufferTests : BufferTestSuite
    {
        public AllocatorBufferTests()
            : base(new UnmanagedMemoryAllocator(1024 * 64))
        {
        }
    }

    [Fact]
    public void Allocate_CreatesValidBuffer()
    {
        using UnmanagedBuffer<int> buffer = UnmanagedBuffer<int>.Allocate(10);
        Span<int> span = buffer.GetSpan();
        Assert.Equal(10, span.Length);
        span[9] = 123;
        Assert.Equal(123, span[9]);
    }

    [Fact]
    public unsafe void Dispose_DoesNotReleaseOutstandingReferences()
    {
        RemoteExecutor.Invoke(RunTest).Dispose();

        static void RunTest()
        {
            UnmanagedBuffer<int> buffer = UnmanagedBuffer<int>.Allocate(10);
            Assert.Equal(1, UnmanagedMemoryHandle.TotalOutstandingHandles);
            Span<int> span = buffer.GetSpan();

            // Pin should AddRef
            using (MemoryHandle h = buffer.Pin())
            {
                int* ptr = (int*)h.Pointer;
                ((IDisposable)buffer).Dispose();
                Assert.Equal(1, UnmanagedMemoryHandle.TotalOutstandingHandles);
                ptr[3] = 13;
                Assert.Equal(13, span[3]);
            } // Unpin should ReleaseRef

            Assert.Equal(0, UnmanagedMemoryHandle.TotalOutstandingHandles);
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(12)]
    public void BufferFinalization_TracksAllocations(int count)
    {
        RemoteExecutor.Invoke(RunTest, count.ToString()).Dispose();

        static void RunTest(string countStr)
        {
            int countInner = int.Parse(countStr);
            List<UnmanagedBuffer<byte>> l = FillList(countInner);

            l.RemoveRange(0, l.Count / 2);

            GC.Collect();
            GC.WaitForPendingFinalizers();

            Assert.Equal(countInner / 2, l.Count); // This is here to prevent eager finalization of the list's elements
            Assert.Equal(countInner / 2, UnmanagedMemoryHandle.TotalOutstandingHandles);
        }

        static List<UnmanagedBuffer<byte>> FillList(int countInner)
        {
            List<UnmanagedBuffer<byte>> l = new List<UnmanagedBuffer<byte>>();
            for (int i = 0; i < countInner; i++)
            {
                UnmanagedBuffer<byte> h = UnmanagedBuffer<byte>.Allocate(42);
                l.Add(h);
            }

            return l;
        }
    }
}
