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
        private static readonly object SyncRoot = new();

        /// <summary>
        /// Fires when an ImageSharp object's undisposed memory resource leaks to the finalizer.
        /// The event brings significant overhead, and is intended to be used for troubleshooting only.
        /// For production diagnostics, use <see cref="TotalUndisposedAllocationCount"/>.
        /// </summary>
        public static event UndisposedAllocationDelegate UndisposedAllocation
        {
            add => Current.UndisposedAllocation += value;
            remove => Current.UndisposedAllocation -= value;
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

        internal static bool UndisposedAllocationSubscribed => Current.UndisposedAllocationSubscribed;

        internal static void IncrementTotalUndisposedAllocationCount() => Current.IncrementTotalUndisposedAllocationCount();

        internal static void DecrementTotalUndisposedAllocationCount() => Current.DecrementTotalUndisposedAllocationCount();

        internal static void RaiseUndisposedMemoryResource(string allocationStackTrace)
            => Current.RaiseUndisposedMemoryResource(allocationStackTrace);

        internal class InteralMemoryDiagnostics
        {
            private int totalUndisposedAllocationCount;

            private UndisposedAllocationDelegate undisposedAllocation;
            private int undisposedAllocationSubscriptionCounter;
            private readonly object syncRoot = new();

            /// <summary>
            /// Fires when an ImageSharp object's undisposed memory resource leaks to the finalizer.
            /// The event brings significant overhead, and is intended to be used for troubleshooting only.
            /// For production diagnostics, use <see cref="TotalUndisposedAllocationCount"/>.
            /// </summary>
            public event UndisposedAllocationDelegate UndisposedAllocation
            {
                add
                {
                    lock (this.syncRoot)
                    {
                        this.undisposedAllocationSubscriptionCounter++;
                        this.undisposedAllocation += value;
                    }
                }

                remove
                {
                    lock (this.syncRoot)
                    {
                        this.undisposedAllocation -= value;
                        this.undisposedAllocationSubscriptionCounter--;
                    }
                }
            }

            /// <summary>
            /// Gets a value indicating the total number of memory resource objects leaked to the finalizer.
            /// </summary>
            public int TotalUndisposedAllocationCount => this.totalUndisposedAllocationCount;

            internal bool UndisposedAllocationSubscribed => Volatile.Read(ref this.undisposedAllocationSubscriptionCounter) > 0;

            internal void IncrementTotalUndisposedAllocationCount() =>
                Interlocked.Increment(ref this.totalUndisposedAllocationCount);

            internal void DecrementTotalUndisposedAllocationCount() =>
                Interlocked.Decrement(ref this.totalUndisposedAllocationCount);

            internal void RaiseUndisposedMemoryResource(string allocationStackTrace)
            {
                if (this.undisposedAllocation is null)
                {
                    return;
                }

                // Schedule on the ThreadPool, to avoid user callback messing up the finalizer thread.
#if NETSTANDARD2_1 || NETCOREAPP2_1_OR_GREATER
                ThreadPool.QueueUserWorkItem(
                    stackTrace => this.undisposedAllocation?.Invoke(stackTrace),
                    allocationStackTrace,
                    preferLocal: false);
#else
            ThreadPool.QueueUserWorkItem(
                stackTrace => this.undisposedAllocation?.Invoke((string)stackTrace),
                allocationStackTrace);
#endif
            }
        }
    }
}
