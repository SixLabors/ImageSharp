// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using SixLabors.ImageSharp.Diagnostics;
using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    public static class MemoryAllocatorValidator
    {
        public static IDisposable MonitorAllocations(int max = 0)
        {
            MemoryDiagnostics.Current = new();
            return new TestMemoryAllocatorDisposable(max);
        }

        public static void ValidateAllocation(int max = 0)
        {
            var count = MemoryDiagnostics.TotalUndisposedAllocationCount;
            var pass = count <= max;
            Assert.True(pass, $"Expected a max of {max} undisposed buffers but found {count}");

            if (count > 0)
            {
                Debug.WriteLine("We should have Zero undisposed memory allocations.");
            }

            MemoryDiagnostics.Current = null;
        }

        public struct TestMemoryAllocatorDisposable : IDisposable
        {
            private readonly int max;

            public TestMemoryAllocatorDisposable(int max) => this.max = max;

            public void Dispose()
                => ValidateAllocation(this.max);
        }
    }
}
