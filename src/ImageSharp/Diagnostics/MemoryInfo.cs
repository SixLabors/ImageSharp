// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Diagnostics
{
    public readonly struct MemoryInfo
    {
        internal MemoryInfo(long totalUndisposedAllocationCount)
            => this.TotalUndisposedAllocationCount = totalUndisposedAllocationCount;

        public long TotalUndisposedAllocationCount { get; }
    }
}
