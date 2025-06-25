// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using Microsoft.DotNet.RemoteExecutor;
using SixLabors.ImageSharp.Memory.Internals;

namespace SixLabors.ImageSharp.Tests.Memory.Allocators;

public class UnmanagedMemoryHandleTests
{
    [Fact]
    public unsafe void Allocate_AllocatesReadWriteMemory()
    {
        UnmanagedMemoryHandle h = UnmanagedMemoryHandle.Allocate(128);
        Assert.False(h.IsInvalid);
        Assert.True(h.IsValid);
        byte* ptr = (byte*)h.Handle;
        for (int i = 0; i < 128; i++)
        {
            ptr[i] = (byte)i;
        }

        for (int i = 0; i < 128; i++)
        {
            Assert.Equal((byte)i, ptr[i]);
        }

        h.Free();
    }

    [Fact]
    public void Free_ClosesHandle()
    {
        UnmanagedMemoryHandle h = UnmanagedMemoryHandle.Allocate(128);
        h.Free();
        Assert.True(h.IsInvalid);
        Assert.Equal(IntPtr.Zero, h.Handle);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(13)]
    public void Create_Free_AllocationsAreTracked(int count)
    {
        RemoteExecutor.Invoke(RunTest, count.ToString()).Dispose();

        static void RunTest(string countStr)
        {
            int countInner = int.Parse(countStr);
            List<UnmanagedMemoryHandle> l = new();
            for (int i = 0; i < countInner; i++)
            {
                Assert.Equal(i, UnmanagedMemoryHandle.TotalOutstandingHandles);
                UnmanagedMemoryHandle h = UnmanagedMemoryHandle.Allocate(42);
                Assert.Equal(i + 1, UnmanagedMemoryHandle.TotalOutstandingHandles);
                l.Add(h);
            }

            for (int i = 0; i < countInner; i++)
            {
                Assert.Equal(countInner - i, UnmanagedMemoryHandle.TotalOutstandingHandles);
                l[i].Free();
                Assert.Equal(countInner - i - 1, UnmanagedMemoryHandle.TotalOutstandingHandles);
            }
        }
    }

    [Fact]
    public void Equality_WhenTrue()
    {
        UnmanagedMemoryHandle h1 = UnmanagedMemoryHandle.Allocate(10);
        UnmanagedMemoryHandle h2 = h1;

        Assert.True(h1.Equals(h2));
        Assert.True(h2.Equals(h1));
        Assert.True(h1 == h2);
        Assert.False(h1 != h2);
        Assert.True(h1.GetHashCode() == h2.GetHashCode());
        h1.Free();
    }

    [Fact]
    public void Equality_WhenFalse()
    {
        UnmanagedMemoryHandle h1 = UnmanagedMemoryHandle.Allocate(10);
        UnmanagedMemoryHandle h2 = UnmanagedMemoryHandle.Allocate(10);

        Assert.False(h1.Equals(h2));
        Assert.False(h2.Equals(h1));
        Assert.False(h1 == h2);
        Assert.True(h1 != h2);

        h1.Free();
        h2.Free();
    }
}
