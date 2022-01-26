// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.DotNet.RemoteExecutor;
using SixLabors.ImageSharp.Diagnostics;
using SixLabors.ImageSharp.Memory;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Memory.Allocators
{
    public class MemoryDiagnosticsTests
    {
        private const int OneMb = 1 << 20;

        private static MemoryAllocator Allocator => Configuration.Default.MemoryAllocator;

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void AllocateDispose_Maintains_TotalUndisposedLogicalAllocationCount(bool isGroupOuter)
        {
            RemoteExecutor.Invoke(RunTest, isGroupOuter.ToString()).Dispose();

            static void RunTest(string isGroupInner)
            {
                bool isGroup = bool.Parse(isGroupInner);
                List<IDisposable> buffers = new();

                Assert.Equal(0, MemoryDiagnostics.GetMemoryInfo().TotalUndisposedAllocationCount);
                for (int length = 1024; length <= 64 * OneMb; length *= 2)
                {
                    long cntBefore = MemoryDiagnostics.GetMemoryInfo().TotalUndisposedAllocationCount;
                    IDisposable buffer = isGroup ?
                        Allocator.AllocateGroup<byte>(length, 1024) :
                        Allocator.Allocate<byte>(length);
                    buffers.Add(buffer);
                    long cntAfter = MemoryDiagnostics.GetMemoryInfo().TotalUndisposedAllocationCount;
                    Assert.True(cntAfter > cntBefore);
                }

                foreach (IDisposable buffer in buffers)
                {
                    long cntBefore = MemoryDiagnostics.GetMemoryInfo().TotalUndisposedAllocationCount;
                    buffer.Dispose();
                    long cntAfter = MemoryDiagnostics.GetMemoryInfo().TotalUndisposedAllocationCount;
                    Assert.True(cntAfter < cntBefore);
                }

                Assert.Equal(0, MemoryDiagnostics.GetMemoryInfo().TotalUndisposedAllocationCount);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void BufferAndGroupFinalizer_DoesNotReduce_TotalUndisposedLogicalAllocationCount(bool isGroupOuter)
        {
            RemoteExecutor.Invoke(RunTest, isGroupOuter.ToString()).Dispose();

            static void RunTest(string isGroupInner)
            {
                bool isGroup = bool.Parse(isGroupInner);
                Assert.Equal(0, MemoryDiagnostics.GetMemoryInfo().TotalUndisposedAllocationCount);
                for (int length = 1024; length <= 64 * OneMb; length *= 2)
                {
                    long cntBefore = MemoryDiagnostics.GetMemoryInfo().TotalUndisposedAllocationCount;
                    AllocateAndForget(length, isGroup);
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                    long cntAfter = MemoryDiagnostics.GetMemoryInfo().TotalUndisposedAllocationCount;
                    Assert.True(cntAfter > cntBefore);
                }
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
}
