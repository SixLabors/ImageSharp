// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

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
        /// Fires when ImageSharp allocates memory from a MemoryAllocator
        /// </summary>
        internal static event Action MemoryAllocated;

        /// <summary>
        /// Fires when ImageSharp releases memory allocated from a MemoryAllocator
        /// </summary>
        internal static event Action MemoryReleased;

        /// <summary>
        /// Gets a value indicating the total number of memory resource objects leaked to the finalizer.
        /// </summary>
        public static int TotalUndisposedAllocationCount => totalUndisposedAllocationCount;

        internal static bool UndisposedAllocationSubscribed => Volatile.Read(ref undisposedAllocationSubscriptionCounter) > 0;

        internal static void IncrementTotalUndisposedAllocationCount()
        {
            Interlocked.Increment(ref totalUndisposedAllocationCount);
            MemoryAllocated?.Invoke();
        }

        internal static void DecrementTotalUndisposedAllocationCount()
        {
            Interlocked.Decrement(ref totalUndisposedAllocationCount);
            MemoryReleased?.Invoke();
        }

        internal static void RaiseUndisposedMemoryResource(string allocationStackTrace)
        {
            if (undisposedAllocation is null)
            {
                return;
            }

            // Schedule on the ThreadPool, to avoid user callback messing up the finalizer thread.
#if NETSTANDARD2_1 || NETCOREAPP2_1_OR_GREATER
            ThreadPool.QueueUserWorkItem(
                stackTrace => undisposedAllocation?.Invoke(stackTrace),
                allocationStackTrace,
                preferLocal: false);
#else
            ThreadPool.QueueUserWorkItem(
                stackTrace => undisposedAllocation?.Invoke((string)stackTrace),
                allocationStackTrace);
#endif
        }
    }
}
