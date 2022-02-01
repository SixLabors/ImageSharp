// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading;

namespace SixLabors.ImageSharp.Diagnostics
{
    /// <summary>
    /// Represents the method to handle <see cref="MemoryDiagnostics.UndisposedAllocation"/>.
    /// </summary>
    public delegate void UndisposedMemoryResourceDelegate(string allocationStackTrace);

    /// <summary>
    /// Utilities to track memory usage and detect memory leaks from not disposing ImageSharp objects.
    /// </summary>
    public static class MemoryDiagnostics
    {
        private static int totalUndisposedAllocationCount;

        private static UndisposedMemoryResourceDelegate undisposedMemoryResource;
        private static int undisposedMemoryResourceSubscriptionCounter;

        /// <summary>
        /// Fires when an ImageSharp object's undisposed memory resource leaks to the finalizer.
        /// The event brings significant overhead, and is intended to be used for troubleshooting only.
        /// For production diagnostics, use <see cref="TotalUndisposedAllocationCount"/>.
        /// </summary>
        public static event UndisposedMemoryResourceDelegate UndisposedAllocation
        {
            add
            {
                Interlocked.Increment(ref undisposedMemoryResourceSubscriptionCounter);
                undisposedMemoryResource += value;
            }

            remove
            {
                undisposedMemoryResource -= value;
                Interlocked.Decrement(ref undisposedMemoryResourceSubscriptionCounter);
            }
        }

        /// <summary>
        /// Gets a value indicating the total number of memory resource objects leaked to the finalizer.
        /// </summary>
        public static int TotalUndisposedAllocationCount => totalUndisposedAllocationCount;

        internal static bool MemoryResourceLeakedSubscribed => Volatile.Read(ref undisposedMemoryResourceSubscriptionCounter) > 0;

        internal static void IncrementTotalUndisposedAllocationCount() =>
            Interlocked.Increment(ref totalUndisposedAllocationCount);

        internal static void DecrementTotalUndisposedAllocationCount() =>
            Interlocked.Decrement(ref totalUndisposedAllocationCount);

        internal static void RaiseUndisposedMemoryResource(string allocationStackTrace)
        {
            // Schedule on the ThreadPool, to avoid user callback messing up the finalizer thread.
            ThreadPool.QueueUserWorkItem(_ => undisposedMemoryResource?.Invoke(allocationStackTrace));
        }
    }
}
