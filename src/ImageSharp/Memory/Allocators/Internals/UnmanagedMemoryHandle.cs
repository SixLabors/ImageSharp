// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace SixLabors.ImageSharp.Memory.Internals
{
    internal sealed class UnmanagedMemoryHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        private readonly int lengthInBytes;
        private bool resurrected;

        public UnmanagedMemoryHandle(int lengthInBytes)
            : base(true)
        {
            this.SetHandle(Marshal.AllocHGlobal(lengthInBytes));
            this.lengthInBytes = lengthInBytes;
            if (lengthInBytes > 0)
            {
                GC.AddMemoryPressure(lengthInBytes);
            }
        }

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
