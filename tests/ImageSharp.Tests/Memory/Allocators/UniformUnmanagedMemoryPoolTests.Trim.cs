// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.DotNet.RemoteExecutor;
using SixLabors.ImageSharp.Memory.Internals;

namespace SixLabors.ImageSharp.Tests.Memory.Allocators;

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
                UniformUnmanagedMemoryPool.TrimSettings trimSettings = new() { TrimPeriodMilliseconds = 5_000 };
                UniformUnmanagedMemoryPool pool = new(128, 256, trimSettings);

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
            public static readonly bool IsNotMacOS = !TestEnvironment.IsMacOS;

            // TODO: Investigate failures on macOS. All handles are released after GC.
            // (It seems to happen more consistently on .NET 6.)
            [ConditionalFact(nameof(IsNotMacOS))]
            public void MultiplePoolInstances_TrimPeriodElapsed_AllAreTrimmed()
            {
                if (!TestEnvironment.RunsOnCI)
                {
                    // This may fail in local runs resulting in high memory load.
                    // Remove the condition for local debugging!
                    return;
                }

                if (TestEnvironment.OSArchitecture == Architecture.Arm64)
                {
                    // Skip on ARM64: https://github.com/SixLabors/ImageSharp/issues/2342
                    return;
                }

                RemoteExecutor.Invoke(RunTest).Dispose();

                static void RunTest()
                {
                    UniformUnmanagedMemoryPool.TrimSettings trimSettings1 = new() { TrimPeriodMilliseconds = 6_000 };
                    UniformUnmanagedMemoryPool pool1 = new(128, 256, trimSettings1);
                    Thread.Sleep(8_000); // Let some callbacks fire already
                    UniformUnmanagedMemoryPool.TrimSettings trimSettings2 = new() { TrimPeriodMilliseconds = 3_000 };
                    UniformUnmanagedMemoryPool pool2 = new(128, 256, trimSettings2);

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
                    UniformUnmanagedMemoryPool.TrimSettings trimSettings = new() { TrimPeriodMilliseconds = 4_000 };
                    _ = new UniformUnmanagedMemoryPool(128, 256, trimSettings);
                }
            }
        }

        public static readonly bool Is32BitProcess = !Environment.Is64BitProcess;
        private static readonly List<byte[]> PressureArrays = [];

        [Fact]
        public static void GC_Collect_OnHighLoad_TrimsEntirePool()
        {
            if (!Is32BitProcess)
            {
                // This test is only relevant for 32-bit processes.
                return;
            }

            RemoteExecutor.Invoke(RunTest).Dispose();

            static void RunTest()
            {
                Assert.False(Environment.Is64BitProcess);
                const int oneMb = 1 << 20;

                UniformUnmanagedMemoryPool.TrimSettings trimSettings = new() { HighPressureThresholdRate = 0.2f };

                GCMemoryInfo memInfo = GC.GetGCMemoryInfo();
                int highLoadThreshold = (int)(memInfo.HighMemoryLoadThresholdBytes / oneMb);
                highLoadThreshold = (int)(trimSettings.HighPressureThresholdRate * highLoadThreshold);

                UniformUnmanagedMemoryPool pool = new(oneMb, 16, trimSettings);
                pool.Return(pool.Rent(16));
                Assert.Equal(16, UnmanagedMemoryHandle.TotalOutstandingHandles);

                for (int i = 0; i < highLoadThreshold; i++)
                {
                    byte[] array = new byte[oneMb];
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
    }
}
