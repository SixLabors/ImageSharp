// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace SixLabors.ImageSharp.Memory.Internals
{
    internal sealed class UnmanagedMemoryHandle : SafeHandle
    {
        private readonly int lengthInBytes;
        private bool resurrected;

        // Track allocations for testing purposes:
        private static int totalOutstandingHandles;

        public UnmanagedMemoryHandle(int lengthInBytes)
            : base(IntPtr.Zero, true)
        {
            this.SetHandle(Marshal.AllocHGlobal(lengthInBytes));
            this.lengthInBytes = lengthInBytes;
            if (lengthInBytes > 0)
            {
                GC.AddMemoryPressure(lengthInBytes);
            }

            Interlocked.Increment(ref totalOutstandingHandles);
        }

        /// <summary>
        /// Gets a value indicating the total outstanding handle allocations for testing purposes.
        /// </summary>
        internal static int TotalOutstandingHandles => totalOutstandingHandles;

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

            this.handle = IntPtr.Zero;
            Interlocked.Decrement(ref totalOutstandingHandles);
            return true;
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
