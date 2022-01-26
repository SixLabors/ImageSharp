// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading;

namespace SixLabors.ImageSharp.Diagnostics
{
    public static class MemoryDiagnostics
    {
        private static int totalUndisposedAllocationCount;

        public static MemoryInfo GetMemoryInfo() => new MemoryInfo(totalUndisposedAllocationCount);

        public static bool EnableStrictDisposeWatcher { get; set; }

        internal static void IncrementTotalUndisposedAllocationCount() =>
            Interlocked.Increment(ref totalUndisposedAllocationCount);

        internal static void DecrementTotalUndisposedAllocationCount() =>
            Interlocked.Decrement(ref totalUndisposedAllocationCount);
    }
}
