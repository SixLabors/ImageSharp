// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace SixLabors.ImageSharp.Memory.Internals
{
    internal sealed class UnmanagedMemoryHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        private readonly int lengthInBytes;
        private bool resurrected;

        // Track allocations for testing purposes:
        private static int totalOutstandingHandles;

        public UnmanagedMemoryHandle(int lengthInBytes)
            : base(true)
        {
            this.SetHandle(Marshal.AllocHGlobal(lengthInBytes));
            this.lengthInBytes = lengthInBytes;
            if (lengthInBytes > 0)
            {
                GC.AddMemoryPressure(lengthInBytes);
            }

            Interlocked.Increment(ref totalOutstandingHandles);
        }

        internal static int TotalOutstandingHandles => totalOutstandingHandles;

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

        internal void Resurrect()
        {
            GC.SuppressFinalize(this);
            this.resurrected = true;
        }

        internal void UnResurrect()
        {
            if (this.resurrected)
            {
                GC.ReRegisterForFinalize(this);
                this.resurrected = true;
            }
        }
    }
}
