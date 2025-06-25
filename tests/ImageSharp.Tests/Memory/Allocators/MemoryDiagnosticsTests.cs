// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using Microsoft.DotNet.RemoteExecutor;
using SixLabors.ImageSharp.Diagnostics;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Tests.Memory.Allocators;

public class MemoryDiagnosticsTests
{
    private const int OneMb = 1 << 20;

    private static MemoryAllocator Allocator => Configuration.Default.MemoryAllocator;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PerfectCleanup_NoLeaksReported(bool isGroupOuter)
    {
        RemoteExecutor.Invoke(RunTest, isGroupOuter.ToString()).Dispose();

        static void RunTest(string isGroupInner)
        {
            bool isGroup = bool.Parse(isGroupInner);
            int leakCounter = 0;
            MemoryDiagnostics.UndisposedAllocation += _ => Interlocked.Increment(ref leakCounter);

            List<IDisposable> buffers = [];

            Assert.Equal(0, MemoryDiagnostics.TotalUndisposedAllocationCount);
            for (int length = 1024; length <= 64 * OneMb; length *= 2)
            {
                long cntBefore = MemoryDiagnostics.TotalUndisposedAllocationCount;
                IDisposable buffer = isGroup ?
                    Allocator.AllocateGroup<byte>(length, 1024) :
                    Allocator.Allocate<byte>(length);
                buffers.Add(buffer);
                long cntAfter = MemoryDiagnostics.TotalUndisposedAllocationCount;
                Assert.True(cntAfter > cntBefore);
            }

            foreach (IDisposable buffer in buffers)
            {
                long cntBefore = MemoryDiagnostics.TotalUndisposedAllocationCount;
                buffer.Dispose();
                long cntAfter = MemoryDiagnostics.TotalUndisposedAllocationCount;
                Assert.True(cntAfter < cntBefore);
            }

            Assert.Equal(0, MemoryDiagnostics.TotalUndisposedAllocationCount);
            Assert.Equal(0, leakCounter);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void MissingCleanup_LeaksAreReported(bool isGroupOuter, bool subscribeLeakHandleOuter)
    {
        RemoteExecutor.Invoke(RunTest, isGroupOuter.ToString(), subscribeLeakHandleOuter.ToString()).Dispose();

        static void RunTest(string isGroupInner, string subscribeLeakHandleInner)
        {
            bool isGroup = bool.Parse(isGroupInner);
            bool subscribeLeakHandle = bool.Parse(subscribeLeakHandleInner);
            int leakCounter = 0;
            bool stackTraceOk = true;
            if (subscribeLeakHandle)
            {
                MemoryDiagnostics.UndisposedAllocation += stackTrace =>
                {
                    Interlocked.Increment(ref leakCounter);
                    stackTraceOk &= stackTrace.Contains(nameof(RunTest)) && stackTrace.Contains(nameof(AllocateAndForget));
                    Assert.Contains(nameof(AllocateAndForget), stackTrace);
                };
            }

            Assert.Equal(0, MemoryDiagnostics.TotalUndisposedAllocationCount);
            for (int length = 1024; length <= 64 * OneMb; length *= 2)
            {
                long cntBefore = MemoryDiagnostics.TotalUndisposedAllocationCount;
                AllocateAndForget(length, isGroup);
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                long cntAfter = MemoryDiagnostics.TotalUndisposedAllocationCount;
                Assert.True(cntAfter > cntBefore);
            }

            if (subscribeLeakHandle)
            {
                // Make sure at least some of the leak callbacks have time to complete on the ThreadPool
                Thread.Sleep(1000);
                Assert.True(leakCounter > 3, $"leakCounter did not count enough leaks ({leakCounter} only)");
            }

            Assert.True(stackTraceOk);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void AllocateAndForget(int length, bool isGroup)
        {
            if (isGroup)
            {
                _ = Allocator.AllocateGroup<byte>(length, 1024);
            }
            else
            {
                _ = Allocator.Allocate<byte>(length);
            }
        }
    }
}
