// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace SixLabors.ImageSharp.Memory.Internals
{
    internal sealed class UnmanagedMemoryHandle : SafeHandle
    {
        // Number of allocation re-attempts when OutOfMemoryException is thrown.
        private const int MaxAllocationAttempts = 1000;

        private readonly int lengthInBytes;
        private bool resurrected;

        // Track allocations for testing purposes:
        private static int totalOutstandingHandles;

        private static long totalOomRetries;

        // A Monitor to wait/signal when we are low on memory.
        private static object lowMemoryMonitor;

#if MEMORY_SENTINEL
        private const int MemorySentinelPadding = 16;
#else
        private const int MemorySentinelPadding = 0;
#endif

        private UnmanagedMemoryHandle(IntPtr handle, int lengthInBytes)
            : base(handle, true)
        {
            this.lengthInBytes = lengthInBytes;
            if (lengthInBytes > 0)
            {
                GC.AddMemoryPressure(lengthInBytes);
            }

            Interlocked.Increment(ref totalOutstandingHandles);
        }

        /// <summary>
        /// Gets the total outstanding handle allocations for testing purposes.
        /// </summary>
        internal static int TotalOutstandingHandles => totalOutstandingHandles;

        /// <summary>
        /// Gets the total number <see cref="OutOfMemoryException"/>-s retried.
        /// </summary>
        internal static long TotalOomRetries => totalOomRetries;

        /// <inheritdoc />
        public override bool IsInvalid => this.handle == IntPtr.Zero;

        protected override bool ReleaseHandle()
        {
            if (this.IsInvalid)
            {
                return false;
            }

            Marshal.FreeHGlobal(this.handle);
            if (this.lengthInBytes > 0)
            {
                GC.RemoveMemoryPressure(this.lengthInBytes);
            }

            if (lowMemoryMonitor != null)
            {
                // We are low on memory. Signal all threads waiting in AllocateHandle().
                Monitor.Enter(lowMemoryMonitor);
                Monitor.PulseAll(lowMemoryMonitor);
                Monitor.Exit(lowMemoryMonitor);
            }

            this.handle = IntPtr.Zero;
            Interlocked.Decrement(ref totalOutstandingHandles);
            return true;
        }

        internal static UnmanagedMemoryHandle Allocate(int lengthInBytes)
        {
            IntPtr handle = AllocateHandle(lengthInBytes + MemorySentinelPadding);
            return new UnmanagedMemoryHandle(handle, lengthInBytes);
        }

        [Conditional("MEMORY_SENTINEL")]
        internal unsafe void InitMemorySentinel() => new Span<byte>((void*)this.handle, this.lengthInBytes + MemorySentinelPadding).Fill(42);

        [Conditional("MEMORY_SENTINEL")]
        internal unsafe void VerifyMemorySentinel(int actualLengthInBytes)
        {
            Span<byte> remainder =
                new Span<byte>((void*)this.handle, this.lengthInBytes + MemorySentinelPadding)
                    .Slice(actualLengthInBytes);
            for (int i = 0; i < remainder.Length; i++)
            {
                if (remainder[i] != 42)
                {
                    throw new InvalidMemoryOperationException("Memory corruption detected!");
                }
            }
        }

        private static IntPtr AllocateHandle(int lengthInBytes)
        {
            int counter = 0;
            IntPtr handle = IntPtr.Zero;
            while (handle == IntPtr.Zero)
            {
                try
                {
                    handle = Marshal.AllocHGlobal(lengthInBytes);
                }
                catch (OutOfMemoryException)
                {
                    // We are low on memory, but expect some memory to be freed soon.
                    // Block the thread & retry to avoid OOM.
                    if (counter < MaxAllocationAttempts)
                    {
                        counter++;
                        Interlocked.Increment(ref totalOomRetries);

                        Interlocked.CompareExchange(ref lowMemoryMonitor, new object(), null);
                        Monitor.Enter(lowMemoryMonitor);
                        Monitor.Wait(lowMemoryMonitor, millisecondsTimeout: 1);
                        Monitor.Exit(lowMemoryMonitor);
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            return handle;
        }

        /// <summary>
        /// UnmanagedMemoryHandle's finalizer would release the underlying handle returning the memory to the OS.
        /// We want to prevent this when a finalizable owner (buffer or MemoryGroup) is returning the handle to
        /// <see cref="UniformUnmanagedMemoryPool"/> in it's finalizer.
        /// Since UnmanagedMemoryHandle is CriticalFinalizable, it is guaranteed that the owner's finalizer is called first.
        /// </summary>
        internal void Resurrect()
        {
            GC.SuppressFinalize(this);
            this.resurrected = true;
        }

        internal void AssignedToNewOwner()
        {
            if (this.resurrected)
            {
                // The handle has been resurrected
                GC.ReRegisterForFinalize(this);
                this.resurrected = false;
            }
        }
    }
}
