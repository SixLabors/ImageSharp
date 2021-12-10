// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace SixLabors.ImageSharp.Memory.Internals
{
    /// <summary>
    /// Implements reference counting lifetime guard mechanism similar to the one provided by <see cref="SafeHandle"/>,
    /// but without the restriction of the guarded object being a handle.
    /// </summary>
    internal abstract class RefCountedLifetimeGuard : IDisposable
    {
        private int refCount = 1;
        private int disposed;
        private int released;

        ~RefCountedLifetimeGuard()
        {
            Interlocked.Exchange(ref this.disposed, 1);
            this.ReleaseRef();
        }

        public bool IsDisposed => this.disposed == 1;

        public void AddRef() => Interlocked.Increment(ref this.refCount);

        public void ReleaseRef()
        {
            Interlocked.Decrement(ref this.refCount);
            if (this.refCount == 0)
            {
                int wasReleased = Interlocked.Exchange(ref this.released, 1);

                if (wasReleased == 0)
                {
                    this.Release();
                }
            }
        }

        public void Dispose()
        {
            int wasDisposed = Interlocked.Exchange(ref this.disposed, 1);
            if (wasDisposed == 0)
            {
                this.ReleaseRef();
                GC.SuppressFinalize(this);
            }
        }

        protected abstract void Release();
    }
}
