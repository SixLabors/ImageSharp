// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading;

namespace SixLabors.ImageSharp.Diagnostics
{
    /// <summary>
    /// Represents the method to handle <see cref="MemoryDiagnostics.UndisposedAllocation"/>.
    /// </summary>
    public delegate void UndisposedAllocationDelegate(string allocationStackTrace);

    /// <summary>
    /// Utilities to track memory usage and detect memory leaks from not disposing ImageSharp objects.
    /// </summary>
    public static class MemoryDiagnostics
    {
        private static int totalUndisposedAllocationCount;

        private static UndisposedAllocationDelegate undisposedAllocation;
        private static int undisposedAllocationSubscriptionCounter;
        private static readonly object SyncRoot = new();

        /// <summary>
        /// Fires when an ImageSharp object's undisposed memory resource leaks to the finalizer.
        /// The event brings significant overhead, and is intended to be used for troubleshooting only.
        /// For production diagnostics, use <see cref="TotalUndisposedAllocationCount"/>.
        /// </summary>
        public static event UndisposedAllocationDelegate UndisposedAllocation
        {
            add
            {
                lock (SyncRoot)
                {
                    undisposedAllocationSubscriptionCounter++;
                    undisposedAllocation += value;
                }
            }

            remove
            {
                lock (SyncRoot)
                {
                    undisposedAllocation -= value;
                    undisposedAllocationSubscriptionCounter--;
                }
            }
        }

        /// <summary>
        /// Gets a value indicating the total number of memory resource objects leaked to the finalizer.
        /// </summary>
        public static int TotalUndisposedAllocationCount => totalUndisposedAllocationCount;

        internal static bool UndisposedAllocationSubscribed => Volatile.Read(ref undisposedAllocationSubscriptionCounter) > 0;

        internal static void IncrementTotalUndisposedAllocationCount() =>
            Interlocked.Increment(ref totalUndisposedAllocationCount);

        internal static void DecrementTotalUndisposedAllocationCount() =>
            Interlocked.Decrement(ref totalUndisposedAllocationCount);

        internal static void RaiseUndisposedMemoryResource(string allocationStackTrace)
        {
            if (undisposedAllocation is null)
            {
                return;
            }

            // Schedule on the ThreadPool, to avoid user callback messing up the finalizer thread.
            ThreadPool.QueueUserWorkItem(
                stackTrace => undisposedAllocation?.Invoke(stackTrace),
                allocationStackTrace,
                preferLocal: true);
        }
    }
}
