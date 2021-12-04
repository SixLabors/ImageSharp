// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Microsoft.DotNet.RemoteExecutor;
using SixLabors.ImageSharp.Memory.Internals;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Memory.Allocators
{
    public partial class UniformUnmanagedMemoryPoolTests
    {
        [Collection(nameof(NonParallelTests))]
        public class Trim
        {
            [CollectionDefinition(nameof(NonParallelTests), DisableParallelization = true)]
            public class NonParallelTests
            {
            }

            [Fact]
            public void TrimPeriodElapsed_TrimsHalfOfUnusedArrays()
            {
                RemoteExecutor.Invoke(RunTest).Dispose();
                static void RunTest()
                {
                    var trimSettings = new UniformUnmanagedMemoryPool.TrimSettings { TrimPeriodMilliseconds = 5_000 };
                    var pool = new UniformUnmanagedMemoryPool(128, 256, trimSettings);

                    UnmanagedMemoryHandle[] a = pool.Rent(64);
                    UnmanagedMemoryHandle[] b = pool.Rent(64);
                    pool.Return(a);
                    Assert.Equal(128, UnmanagedMemoryHandle.TotalOutstandingHandles);
                    Thread.Sleep(15_000);

                    // We expect at least 2 Trim actions, first trim 32, then 16 arrays.
                    // 128 - 32 - 16 = 80
                    Assert.True(
                        UnmanagedMemoryHandle.TotalOutstandingHandles <= 80,
                        $"UnmanagedMemoryHandle.TotalOutstandingHandles={UnmanagedMemoryHandle.TotalOutstandingHandles} > 80");
                    pool.Return(b);
                }
            }

            // Complicated Xunit ceremony to disable parallel execution of an individual test,
            // MultiplePoolInstances_TrimPeriodElapsed_AllAreTrimmed,
            // which is strongly timing-sensitive, and might be flaky under high load.
            [CollectionDefinition(nameof(NonParallelCollection), DisableParallelization = true)]
            public class NonParallelCollection
            {
            }

            [Collection(nameof(NonParallelCollection))]
            public class NonParallel
            {
                [Fact]
                public void MultiplePoolInstances_TrimPeriodElapsed_AllAreTrimmed()
                {
                    RemoteExecutor.Invoke(RunTest).Dispose();

                    static void RunTest()
                    {
                        var trimSettings1 = new UniformUnmanagedMemoryPool.TrimSettings { TrimPeriodMilliseconds = 6_000 };
                        var pool1 = new UniformUnmanagedMemoryPool(128, 256, trimSettings1);
                        Thread.Sleep(8_000); // Let some callbacks fire already
                        var trimSettings2 = new UniformUnmanagedMemoryPool.TrimSettings { TrimPeriodMilliseconds = 3_000 };
                        var pool2 = new UniformUnmanagedMemoryPool(128, 256, trimSettings2);

                        pool1.Return(pool1.Rent(64));
                        pool2.Return(pool2.Rent(64));
                        Assert.Equal(128, UnmanagedMemoryHandle.TotalOutstandingHandles);

                        // This exercises pool weak reference list trimming:
                        LeakPoolInstance();
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        Assert.Equal(128, UnmanagedMemoryHandle.TotalOutstandingHandles);

                        Thread.Sleep(15_000);
                        Assert.True(
                            UnmanagedMemoryHandle.TotalOutstandingHandles <= 64,
                            $"UnmanagedMemoryHandle.TotalOutstandingHandles={UnmanagedMemoryHandle.TotalOutstandingHandles} > 64");
                        GC.KeepAlive(pool1);
                        GC.KeepAlive(pool2);
                    }

                    [MethodImpl(MethodImplOptions.NoInlining)]
                    static void LeakPoolInstance()
                    {
                        var trimSettings = new UniformUnmanagedMemoryPool.TrimSettings { TrimPeriodMilliseconds = 4_000 };
                        _ = new UniformUnmanagedMemoryPool(128, 256, trimSettings);
                    }
                }
            }

#if NETCOREAPP3_1_OR_GREATER
            public static readonly bool Is32BitProcess = !Environment.Is64BitProcess;
            private static readonly List<byte[]> PressureArrays = new();

            [ConditionalFact(nameof(Is32BitProcess))]
            public static void GC_Collect_OnHighLoad_TrimsEntirePool()
            {
                RemoteExecutor.Invoke(RunTest).Dispose();

                static void RunTest()
                {
                    Assert.False(Environment.Is64BitProcess);
                    const int OneMb = 1 << 20;

                    var trimSettings = new UniformUnmanagedMemoryPool.TrimSettings { HighPressureThresholdRate = 0.2f };

                    GCMemoryInfo memInfo = GC.GetGCMemoryInfo();
                    int highLoadThreshold = (int)(memInfo.HighMemoryLoadThresholdBytes / OneMb);
                    highLoadThreshold = (int)(trimSettings.HighPressureThresholdRate * highLoadThreshold);

                    var pool = new UniformUnmanagedMemoryPool(OneMb, 16, trimSettings);
                    pool.Return(pool.Rent(16));
                    Assert.Equal(16, UnmanagedMemoryHandle.TotalOutstandingHandles);

                    for (int i = 0; i < highLoadThreshold; i++)
                    {
                        byte[] array = new byte[OneMb];
                        TouchPage(array);
                        PressureArrays.Add(array);
                    }

                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                    GC.WaitForPendingFinalizers(); // The pool should be fully trimmed after this point

                    Assert.Equal(0, UnmanagedMemoryHandle.TotalOutstandingHandles);

                    // Prevent eager collection of the pool:
                    GC.KeepAlive(pool);

                    static void TouchPage(byte[] b)
                    {
                        uint size = (uint)b.Length;
                        const uint pageSize = 4096;
                        uint numPages = size / pageSize;

                        for (uint i = 0; i < numPages; i++)
                        {
                            b[i * pageSize] = (byte)(i % 256);
                        }
                    }
                }
            }
#endif
        }
    }
}
