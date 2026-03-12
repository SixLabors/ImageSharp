// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.DotNet.RemoteExecutor;
using SixLabors.ImageSharp.Memory.Internals;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests.Memory.Allocators;

public partial class UniformUnmanagedMemoryPoolTests
{
    private readonly ITestOutputHelper output;

    public UniformUnmanagedMemoryPoolTests(ITestOutputHelper output) => this.output = output;

    private class CleanupUtil : IDisposable
    {
        private readonly UniformUnmanagedMemoryPool pool;
        private readonly List<UnmanagedMemoryHandle> handlesToDestroy = new();
        private readonly List<IntPtr> ptrsToDestroy = new();

        public CleanupUtil(UniformUnmanagedMemoryPool pool)
        {
            this.pool = pool;
        }

        public void Register(UnmanagedMemoryHandle handle) => this.handlesToDestroy.Add(handle);

        public void Register(IEnumerable<UnmanagedMemoryHandle> handles) => this.handlesToDestroy.AddRange(handles);

        public void Register(IntPtr memoryPtr) => this.ptrsToDestroy.Add(memoryPtr);

        public void Register(IEnumerable<IntPtr> memoryPtrs) => this.ptrsToDestroy.AddRange(memoryPtrs);

        public void Dispose()
        {
            foreach (UnmanagedMemoryHandle handle in this.handlesToDestroy)
            {
                handle.Free();
            }

            this.pool.Release();

            foreach (IntPtr ptr in this.ptrsToDestroy)
            {
                Marshal.FreeHGlobal(ptr);
            }
        }
    }

    [Theory]
    [InlineData(3, 11)]
    [InlineData(7, 4)]
    public void Constructor_InitializesProperties(int arrayLength, int capacity)
    {
        UniformUnmanagedMemoryPool pool = new(arrayLength, capacity);
        Assert.Equal(arrayLength, pool.BufferLength);
        Assert.Equal(capacity, pool.Capacity);
    }

    [Theory]
    [InlineData(1, 3)]
    [InlineData(8, 10)]
    public void Rent_SingleBuffer_ReturnsCorrectBuffer(int length, int capacity)
    {
        UniformUnmanagedMemoryPool pool = new(length, capacity);
        using CleanupUtil cleanup = new(pool);

        for (int i = 0; i < capacity; i++)
        {
            UnmanagedMemoryHandle h = pool.Rent();
            CheckBuffer(length, pool, h);
            cleanup.Register(h);
        }
    }

    [Fact]
    public void Return_DoesNotDeallocateMemory()
    {
        RemoteExecutor.Invoke(RunTest).Dispose();

        static void RunTest()
        {
            UniformUnmanagedMemoryPool pool = new(16, 16);
            UnmanagedMemoryHandle a = pool.Rent();
            UnmanagedMemoryHandle[] b = pool.Rent(2);

            Assert.Equal(3, UnmanagedMemoryHandle.TotalOutstandingHandles);
            pool.Return(a);
            pool.Return(b);
            Assert.Equal(3, UnmanagedMemoryHandle.TotalOutstandingHandles);
        }
    }

    private static void CheckBuffer(int length, UniformUnmanagedMemoryPool pool, UnmanagedMemoryHandle h)
    {
        Assert.False(h.IsInvalid);
        Span<byte> span = GetSpan(h, pool.BufferLength);
        span.Fill(123);

        byte[] expected = new byte[length];
        expected.AsSpan().Fill(123);
        Assert.True(span.SequenceEqual(expected));
    }

    private static unsafe Span<byte> GetSpan(UnmanagedMemoryHandle h, int length) => new(h.Pointer, length);

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 5)]
    [InlineData(42, 7)]
    [InlineData(5, 10)]
    public void Rent_MultiBuffer_ReturnsCorrectBuffers(int length, int bufferCount)
    {
        UniformUnmanagedMemoryPool pool = new(length, 10);
        using CleanupUtil cleanup = new(pool);
        UnmanagedMemoryHandle[] handles = pool.Rent(bufferCount);
        cleanup.Register(handles);

        Assert.NotNull(handles);
        Assert.Equal(bufferCount, handles.Length);

        foreach (UnmanagedMemoryHandle h in handles)
        {
            CheckBuffer(length, pool, h);
        }
    }

    [Fact]
    public void Rent_MultipleTimesWithoutReturn_ReturnsDifferentHandles()
    {
        UniformUnmanagedMemoryPool pool = new(128, 10);
        using CleanupUtil cleanup = new(pool);
        UnmanagedMemoryHandle[] a = pool.Rent(2);
        cleanup.Register(a);
        UnmanagedMemoryHandle b = pool.Rent();
        cleanup.Register(b);

        Assert.NotEqual(a[0].Handle, a[1].Handle);
        Assert.NotEqual(a[0].Handle, b.Handle);
        Assert.NotEqual(a[1].Handle, b.Handle);
    }

    [Theory]
    [InlineData(4, 2, 10)]
    [InlineData(5, 1, 6)]
    [InlineData(12, 4, 12)]
    public void RentReturnRent_SameBuffers(int totalCount, int rentUnit, int capacity)
    {
        UniformUnmanagedMemoryPool pool = new(128, capacity);
        using CleanupUtil cleanup = new(pool);
        HashSet<UnmanagedMemoryHandle> allHandles = new();
        List<UnmanagedMemoryHandle[]> handleUnits = new();

        UnmanagedMemoryHandle[] handles;
        for (int i = 0; i < totalCount; i += rentUnit)
        {
            handles = pool.Rent(rentUnit);
            Assert.NotNull(handles);
            handleUnits.Add(handles);
            foreach (UnmanagedMemoryHandle array in handles)
            {
                allHandles.Add(array);
            }

            // Allocate some memory, so potential new pool allocation wouldn't allocated the same memory:
            cleanup.Register(Marshal.AllocHGlobal(128));
        }

        foreach (UnmanagedMemoryHandle[] arrayUnit in handleUnits)
        {
            if (arrayUnit.Length == 1)
            {
                // Test single-array return:
                pool.Return(arrayUnit.Single());
            }
            else
            {
                pool.Return(arrayUnit);
            }
        }

        handles = pool.Rent(totalCount);

        Assert.NotNull(handles);

        foreach (UnmanagedMemoryHandle array in handles)
        {
            Assert.Contains(array, allHandles);
        }

        cleanup.Register(allHandles);
    }

    [Fact]
    public void Rent_SingleBuffer_OverCapacity_ReturnsInvalidBuffer()
    {
        UniformUnmanagedMemoryPool pool = new(7, 1000);
        using CleanupUtil cleanup = new(pool);
        UnmanagedMemoryHandle[] initial = pool.Rent(1000);
        Assert.NotNull(initial);
        cleanup.Register(initial);
        UnmanagedMemoryHandle b1 = pool.Rent();
        Assert.True(b1.IsInvalid);
    }

    [Theory]
    [InlineData(0, 6, 5)]
    [InlineData(5, 1, 5)]
    [InlineData(4, 7, 10)]
    public void Rent_MultiBuffer_OverCapacity_ReturnsNull(int initialRent, int attempt, int capacity)
    {
        UniformUnmanagedMemoryPool pool = new(128, capacity);
        using CleanupUtil cleanup = new(pool);
        UnmanagedMemoryHandle[] initial = pool.Rent(initialRent);
        Assert.NotNull(initial);
        cleanup.Register(initial);
        UnmanagedMemoryHandle[] b1 = pool.Rent(attempt);
        Assert.Null(b1);
    }

    [Theory]
    [InlineData(0, 5, 5)]
    [InlineData(5, 1, 6)]
    [InlineData(4, 7, 11)]
    [InlineData(3, 3, 7)]
    public void Rent_MultiBuff_BelowCapacity_Succeeds(int initialRent, int attempt, int capacity)
    {
        UniformUnmanagedMemoryPool pool = new(128, capacity);
        using CleanupUtil cleanup = new(pool);
        UnmanagedMemoryHandle[] b0 = pool.Rent(initialRent);
        Assert.NotNull(b0);
        cleanup.Register(b0);
        UnmanagedMemoryHandle[] b1 = pool.Rent(attempt);
        Assert.NotNull(b1);
        cleanup.Register(b1);
    }

    public static readonly bool IsNotMacOS = !TestEnvironment.IsMacOS;

    // TODO: Investigate macOS failures
    [ConditionalTheory(nameof(IsNotMacOS))]
    [InlineData(false)]
    [InlineData(true)]
    public void RentReturnRelease_SubsequentRentReturnsDifferentHandles(bool multiple)
    {
        RemoteExecutor.Invoke(RunTest, multiple.ToString()).Dispose();

        static void RunTest(string multipleInner)
        {
            UniformUnmanagedMemoryPool pool = new(16, 16);
            using CleanupUtil cleanup = new(pool);
            UnmanagedMemoryHandle b0 = pool.Rent();
            IntPtr h0 = b0.Handle;
            UnmanagedMemoryHandle b1 = pool.Rent();
            IntPtr h1 = b1.Handle;
            pool.Return(b0);
            pool.Return(b1);
            pool.Release();

            // Do some unmanaged allocations to make sure new pool buffers are different:
            IntPtr[] dummy = Enumerable.Range(0, 100).Select(_ => Marshal.AllocHGlobal(16)).ToArray();
            cleanup.Register(dummy);

            if (bool.Parse(multipleInner))
            {
                UnmanagedMemoryHandle b = pool.Rent();
                cleanup.Register(b);
                Assert.NotEqual(h0, b.Handle);
                Assert.NotEqual(h1, b.Handle);
            }
            else
            {
                UnmanagedMemoryHandle[] b = pool.Rent(2);
                cleanup.Register(b);
                Assert.NotEqual(h0, b[0].Handle);
                Assert.NotEqual(h1, b[0].Handle);
                Assert.NotEqual(h0, b[1].Handle);
                Assert.NotEqual(h1, b[1].Handle);
            }
        }
    }

    [Fact]
    public void Release_ShouldFreeRetainedMemory()
    {
        RemoteExecutor.Invoke(RunTest).Dispose();

        static void RunTest()
        {
            UniformUnmanagedMemoryPool pool = new(16, 16);
            UnmanagedMemoryHandle a = pool.Rent();
            UnmanagedMemoryHandle[] b = pool.Rent(2);
            pool.Return(a);
            pool.Return(b);

            Assert.Equal(3, UnmanagedMemoryHandle.TotalOutstandingHandles);
            pool.Release();
            Assert.Equal(0, UnmanagedMemoryHandle.TotalOutstandingHandles);
        }
    }

    [Fact]
    public void RentReturn_IsThreadSafe()
    {
        int count = Environment.ProcessorCount * 200;
        UniformUnmanagedMemoryPool pool = new(8, count);
        using CleanupUtil cleanup = new(pool);
        Random rnd = new(0);

        Parallel.For(0, Environment.ProcessorCount, (int i) =>
        {
            List<UnmanagedMemoryHandle> allHandles = new();
            int pauseAt = rnd.Next(100);
            for (int j = 0; j < 100; j++)
            {
                UnmanagedMemoryHandle[] data = pool.Rent(2);

                GetSpan(data[0], pool.BufferLength).Fill((byte)i);
                GetSpan(data[1], pool.BufferLength).Fill((byte)i);
                allHandles.Add(data[0]);
                allHandles.Add(data[1]);

                if (j == pauseAt)
                {
                    Thread.Sleep(15);
                }
            }

            Span<byte> expected = new byte[8];
            expected.Fill((byte)i);

            foreach (UnmanagedMemoryHandle h in allHandles)
            {
                Assert.True(expected.SequenceEqual(GetSpan(h, pool.BufferLength)));
                pool.Return(new[] { h });
            }
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LeakPool_FinalizerShouldFreeRetainedHandles(bool withGuardedBuffers)
    {
        RemoteExecutor.Invoke(RunTest, withGuardedBuffers.ToString()).Dispose();

        static void RunTest(string withGuardedBuffersInner)
        {
            LeakPoolInstance(bool.Parse(withGuardedBuffersInner));
            Assert.Equal(20, UnmanagedMemoryHandle.TotalOutstandingHandles);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Assert.Equal(0, UnmanagedMemoryHandle.TotalOutstandingHandles);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void LeakPoolInstance(bool withGuardedBuffers)
        {
            UniformUnmanagedMemoryPool pool = new(16, 128);
            if (withGuardedBuffers)
            {
                UnmanagedMemoryHandle h = pool.Rent();
                _ = pool.CreateGuardedBuffer<byte>(h, 10, false);
                UnmanagedMemoryHandle[] g = pool.Rent(19);
                _ = pool.CreateGroupLifetimeGuard(g);
            }
            else
            {
                pool.Return(pool.Rent(20));
            }
        }
    }
}
