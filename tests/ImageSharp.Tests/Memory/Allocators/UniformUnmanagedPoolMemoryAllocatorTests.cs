// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.DotNet.RemoteExecutor;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Memory.Internals;
using SixLabors.ImageSharp.Tests.Memory.DiscontiguousBuffers;

namespace SixLabors.ImageSharp.Tests.Memory.Allocators;

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
        new()
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
        UniformUnmanagedMemoryPoolMemoryAllocator allocator = new(
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
        UniformUnmanagedMemoryPoolMemoryAllocator allocator = new(
            64,
            128,
            1024,
            1024);

        List<MemoryGroup<S4>> groups = new();
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

    [Fact]
    public void AllocateGroup_SizeInBytesOverLongMaxValue_ThrowsInvalidMemoryOperationException()
    {
        UniformUnmanagedMemoryPoolMemoryAllocator allocator = new(null);
        Assert.Throws<InvalidMemoryOperationException>(() => allocator.AllocateGroup<byte>(int.MaxValue * (long)int.MaxValue, int.MaxValue));
    }

    public static TheoryData<int> InvalidLengths { get; set; } = new()
    {
        { -1 },
        { (1 << 30) + 1 }
    };

    [Theory]
    [MemberData(nameof(InvalidLengths))]
    public void Allocate_IncorrectAmount_ThrowsCorrect_InvalidMemoryOperationException(int length)
        => Assert.Throws<InvalidMemoryOperationException>(() => new UniformUnmanagedMemoryPoolMemoryAllocator(null).Allocate<S512>(length));

    [Fact]
    public unsafe void Allocate_MemoryIsPinnableMultipleTimes()
    {
        UniformUnmanagedMemoryPoolMemoryAllocator allocator = new(null);
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
            MemoryAllocator allocator = MemoryAllocator.Create();
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
            MemoryAllocator allocator = MemoryAllocator.Create(new MemoryAllocatorOptions
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
            UniformUnmanagedMemoryPoolMemoryAllocator allocator = new(512, 1024, 16 * 1024, 1024);
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
            UniformUnmanagedMemoryPoolMemoryAllocator allocator = new(512, 1024, 16 * 1024, 1024);
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
            UniformUnmanagedMemoryPoolMemoryAllocator allocator = new(128, 512, 16 * 512, 1024);
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
            UniformUnmanagedMemoryPoolMemoryAllocator allocator = new(128, 512, 16 * 512, 1024);
            IMemoryOwner<byte> b = allocator.Allocate<byte>(256);
            MemoryGroup<byte> g = allocator.AllocateGroup<byte>(2048, 128);
            Assert.Equal(5, UnmanagedMemoryHandle.TotalOutstandingHandles);
            allocator.ReleaseRetainedResources();
            Assert.Equal(5, UnmanagedMemoryHandle.TotalOutstandingHandles);
            b.Dispose();
            g.Dispose();
            Assert.Equal(5, UnmanagedMemoryHandle.TotalOutstandingHandles);
            allocator.ReleaseRetainedResources();
            Assert.Equal(0, UnmanagedMemoryHandle.TotalOutstandingHandles);
        }
    }

    [Theory]
    [InlineData(300)] // Group of single SharedArrayPoolBuffer<T>
    [InlineData(600)] // Group of single UniformUnmanagedMemoryPool buffer
    [InlineData(1200)] // Group of two UniformUnmanagedMemoryPool buffers
    public void AllocateMemoryGroup_Finalization_ReturnsToPool(int length)
    {
        if (TestEnvironment.IsMacOS)
        {
            // Skip on macOS: https://github.com/SixLabors/ImageSharp/issues/1887
            return;
        }

        if (TestEnvironment.OSArchitecture == Architecture.Arm64)
        {
            // Skip on ARM64: https://github.com/SixLabors/ImageSharp/issues/2342
            return;
        }

        if (!TestEnvironment.RunsOnCI)
        {
            // This may fail in local runs resulting in high memory load.
            // Remove the condition for local debugging!
            return;
        }

        // RunTest(length.ToString());
        RemoteExecutor.Invoke(RunTest, length.ToString()).Dispose();

        static void RunTest(string lengthStr)
        {
            UniformUnmanagedMemoryPoolMemoryAllocator allocator = new(512, 1024, 16 * 1024, 1024);
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

    [Theory]
    [InlineData(300)] // Group of single SharedArrayPoolBuffer<T>
    [InlineData(600)] // Group of single UniformUnmanagedMemoryPool buffer
    public void AllocateSingleMemoryOwner_Finalization_ReturnsToPool(int length)
    {
        if (TestEnvironment.IsMacOS)
        {
            // Skip on macOS: https://github.com/SixLabors/ImageSharp/issues/1887
            return;
        }

        if (TestEnvironment.OSArchitecture == Architecture.Arm64)
        {
            // Skip on ARM64: https://github.com/SixLabors/ImageSharp/issues/2342
            return;
        }

        if (!TestEnvironment.RunsOnCI)
        {
            // This may fail in local runs resulting in high memory load.
            // Remove the condition for local debugging!
            return;
        }

        // RunTest(length.ToString());
        RemoteExecutor.Invoke(RunTest, length.ToString()).Dispose();

        static void RunTest(string lengthStr)
        {
            UniformUnmanagedMemoryPoolMemoryAllocator allocator = new(512, 1024, 16 * 1024, 1024);
            int lengthInner = int.Parse(lengthStr);

            AllocateSingleAndForget(allocator, lengthInner);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            GC.WaitForPendingFinalizers();

            AllocateSingleAndForget(allocator, lengthInner, true);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            GC.WaitForPendingFinalizers();

            using IMemoryOwner<byte> g = allocator.Allocate<byte>(lengthInner);
            Assert.Equal(42, g.GetSpan()[0]);
            GC.KeepAlive(allocator);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AllocateSingleAndForget(UniformUnmanagedMemoryPoolMemoryAllocator allocator, int length, bool check = false)
    {
        IMemoryOwner<byte> g = allocator.Allocate<byte>(length);
        if (check)
        {
            Assert.Equal(42, g.GetSpan()[0]);
        }

        g.GetSpan()[0] = 42;

        if (length < 512)
        {
            // For ArrayPool.Shared, first array will be returned to the TLS storage of the finalizer thread,
            // repeat rental to make sure per-core buckets are also utilized.
            IMemoryOwner<byte> g1 = allocator.Allocate<byte>(length);
            g1.GetSpan()[0] = 42;
        }
    }

    [Fact]
    public void Issue2001_NegativeMemoryReportedByGc()
    {
        RemoteExecutor.Invoke(RunTest).Dispose();

        static void RunTest()
        {
            // Emulate GC.GetGCMemoryInfo() issue https://github.com/dotnet/runtime/issues/65466
            UniformUnmanagedMemoryPoolMemoryAllocator.GetTotalAvailableMemoryBytes = () => -402354176;
            _ = MemoryAllocator.Create();
        }
    }

    [Fact]
    public void Allocate_OverLimit_ThrowsInvalidMemoryOperationException()
    {
        MemoryAllocator allocator = MemoryAllocator.Create(new MemoryAllocatorOptions
        {
            AllocationLimitMegabytes = 4
        });
        const int oneMb = 1 << 20;
        allocator.Allocate<byte>(4 * oneMb).Dispose(); // Should work
        Assert.Throws<InvalidMemoryOperationException>(() => allocator.Allocate<byte>(5 * oneMb));
    }

    [Fact]
    public void AllocateGroup_OverLimit_ThrowsInvalidMemoryOperationException()
    {
        MemoryAllocator allocator = MemoryAllocator.Create(new MemoryAllocatorOptions
        {
            AllocationLimitMegabytes = 4
        });
        const int oneMb = 1 << 20;
        allocator.AllocateGroup<byte>(4 * oneMb, 1024).Dispose(); // Should work
        Assert.Throws<InvalidMemoryOperationException>(() => allocator.AllocateGroup<byte>(5 * oneMb, 1024));
    }

    [ConditionalFact(typeof(Environment), nameof(Environment.Is64BitProcess))]
    public void MemoryAllocator_Create_SetHighLimit()
    {
        RemoteExecutor.Invoke(RunTest).Dispose();
        static void RunTest()
        {
            const long threeGB = 3L * (1 << 30);
            MemoryAllocator allocator = MemoryAllocator.Create(new MemoryAllocatorOptions
            {
                AllocationLimitMegabytes = (int)(threeGB / 1024)
            });
            using MemoryGroup<byte> memoryGroup = allocator.AllocateGroup<byte>(threeGB, 1024);
            Assert.Equal(threeGB, memoryGroup.TotalLength);
        }
    }
}
