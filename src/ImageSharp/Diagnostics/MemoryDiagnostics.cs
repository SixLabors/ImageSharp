// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

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
        internal static readonly InteralMemoryDiagnostics Default = new();
        private static AsyncLocal<InteralMemoryDiagnostics> localInstance = null;

        // the async local end up out of scope during finalizers so putting into thte internal class is useless
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

        internal static InteralMemoryDiagnostics Current
        {
            get
            {
                if (localInstance != null && localInstance.Value != null)
                {
                    return localInstance.Value;
                }

                return Default;
            }

            set
            {
                if (localInstance == null)
                {
                    lock (SyncRoot)
                    {
                        localInstance ??= new AsyncLocal<InteralMemoryDiagnostics>();
                    }
                }

                localInstance.Value = value;
            }
        }

        /// <summary>
        /// Gets a value indicating the total number of memory resource objects leaked to the finalizer.
        /// </summary>
        public static int TotalUndisposedAllocationCount => Current.TotalUndisposedAllocationCount;

        internal static bool UndisposedAllocationSubscribed => Volatile.Read(ref undisposedAllocationSubscriptionCounter) > 0;

        internal static void IncrementTotalUndisposedAllocationCount() => Current.IncrementTotalUndisposedAllocationCount();

        internal static void DecrementTotalUndisposedAllocationCount() => Current.DecrementTotalUndisposedAllocationCount();

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

        internal class InteralMemoryDiagnostics
        {
            private int totalUndisposedAllocationCount;

            /// <summary>
            /// Gets a value indicating the total number of memory resource objects leaked to the finalizer.
            /// </summary>
            public int TotalUndisposedAllocationCount => this.totalUndisposedAllocationCount;

            internal void IncrementTotalUndisposedAllocationCount() =>
                Interlocked.Increment(ref this.totalUndisposedAllocationCount);

            internal void DecrementTotalUndisposedAllocationCount() =>
                Interlocked.Decrement(ref this.totalUndisposedAllocationCount);
        }
    }
}
