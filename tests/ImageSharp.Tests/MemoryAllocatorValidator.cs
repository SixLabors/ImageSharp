// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Diagnostics;

namespace SixLabors.ImageSharp.Tests;

public static class MemoryAllocatorValidator
{
    private static readonly AsyncLocal<TestMemoryDiagnostics> LocalInstance = new();

    public static bool MonitoringAllocations => LocalInstance.Value != null;

    static MemoryAllocatorValidator()
    {
        MemoryDiagnostics.MemoryAllocated += MemoryDiagnostics_MemoryAllocated;
        MemoryDiagnostics.MemoryReleased += MemoryDiagnostics_MemoryReleased;
    }

    private static void MemoryDiagnostics_MemoryReleased()
    {
        TestMemoryDiagnostics backing = LocalInstance.Value;
        if (backing != null)
        {
            backing.TotalRemainingAllocated--;
        }
    }

    private static void MemoryDiagnostics_MemoryAllocated()
    {
        TestMemoryDiagnostics backing = LocalInstance.Value;
        if (backing != null)
        {
            backing.TotalAllocated++;
            backing.TotalRemainingAllocated++;
        }
    }

    public static TestMemoryDiagnostics MonitorAllocations()
    {
        TestMemoryDiagnostics diag = new();
        LocalInstance.Value = diag;
        return diag;
    }

    public static void StopMonitoringAllocations() => LocalInstance.Value = null;

    public static void ValidateAllocations(int expectedAllocationCount = 0)
        => LocalInstance.Value?.Validate(expectedAllocationCount);

    public class TestMemoryDiagnostics : IDisposable
    {
        public int TotalAllocated { get; set; }

        public int TotalRemainingAllocated { get; set; }

        public void Validate(int expectedAllocationCount)
        {
            int count = this.TotalRemainingAllocated;
            bool pass = expectedAllocationCount == count;
            Assert.True(pass, $"Expected {expectedAllocationCount} undisposed buffers but found {count}");
        }

        public void Dispose()
        {
            this.Validate(0);
            if (LocalInstance.Value == this)
            {
                StopMonitoringAllocations();
            }
        }
    }
}
