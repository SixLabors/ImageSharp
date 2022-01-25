// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Diagnostics
{
    public readonly struct MemoryInfo
    {
        internal MemoryInfo(long totalUndisposedAllocationCount)
            => this.TotalUndisposedAllocationCount = totalUndisposedAllocationCount;

        public long TotalUndisposedAllocationCount { get; }
    }

    public static class MemoryDiagnostics
    {
        public static MemoryInfo GetMemoryInfo() => throw new NotImplementedException();

        public static bool EnableStrictDisposeWatcher { get; set; }
    }
}
