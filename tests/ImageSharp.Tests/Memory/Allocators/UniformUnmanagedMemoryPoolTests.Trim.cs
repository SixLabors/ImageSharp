// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.DotNet.RemoteExecutor;
using SixLabors.ImageSharp.Memory.Internals;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Memory.Allocators
{
    public partial class UniformUnmanagedMemoryPoolTests
    {
        [CollectionDefinition(nameof(NonParallelTests), DisableParallelization = true)]
        public class NonParallelTests
        {
        }

        [Collection(nameof(NonParallelTests))]
        public class Trim
        {
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

#if NETCORE31COMPATIBLE
            public static readonly bool Is32BitProcess = !Environment.Is64BitProcess;
            private static readonly List<byte[]> PressureArrays = new List<byte[]>();

            [ConditionalFact(nameof(Is32BitProcess))]
            public static void GC_Collect_OnHighLoad_TrimsEntirePool()
            {
                RemoteExecutor.Invoke(RunTest).Dispose();
                static void RunTest()
                {
                    Assert.False(Environment.Is64BitProcess);
                    const int OneMb = 1024 * 1024;

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
