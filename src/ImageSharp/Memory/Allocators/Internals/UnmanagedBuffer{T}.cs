// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Memory.Allocators.Internals
{
    /// <summary>
    /// Allocates and provides an <see cref="IMemoryOwner{T}"/> implementation giving
    /// access to unmanaged buffers.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    internal sealed class UnmanagedBuffer<T> : MemoryManager<T>
        where T : struct
    {
        private bool isDisposed;
        private readonly SafeHandle safeHandle;
        private readonly int length;

        /// <summary>
        /// Initializes a new instance of the <see cref="UnmanagedBuffer{T}"/> class.
        /// </summary>
        /// <param name="length">The number of elements to allocate.</param>
        public UnmanagedBuffer(int length)
        {
            this.length = length;
            this.safeHandle = new SafeHGlobalHandle(length * Unsafe.SizeOf<T>());
        }

        public override unsafe Span<T> GetSpan()
            => new Span<T>((void*)this.safeHandle.DangerousGetHandle(), this.length);

        /// <inheritdoc />
        public override unsafe MemoryHandle Pin(int elementIndex = 0)
        {
            // Will be released in Unpin
            bool unused = false;
            this.safeHandle.DangerousAddRef(ref unused);

            void* pbData = Unsafe.Add<T>((void*)this.safeHandle.DangerousGetHandle(), elementIndex);
            return new MemoryHandle(pbData);
        }

        /// <inheritdoc />
        public override void Unpin() => this.safeHandle.DangerousRelease();

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (this.isDisposed || this.safeHandle.IsInvalid)
            {
                return;
            }

            if (disposing)
            {
                this.safeHandle.Dispose();
            }

            this.isDisposed = true;
        }

        private sealed class SafeHGlobalHandle : SafeHandle
        {
            private readonly int byteCount;

            public SafeHGlobalHandle(int size)
                : base(IntPtr.Zero, true)
            {
                this.SetHandle(Marshal.AllocHGlobal(size));
                GC.AddMemoryPressure(this.byteCount = size);
            }

            public override bool IsInvalid => this.handle == IntPtr.Zero;

            protected override bool ReleaseHandle()
            {
                if (this.IsInvalid)
                {
                    return false;
                }

                Marshal.FreeHGlobal(this.handle);
                GC.RemoveMemoryPressure(this.byteCount);
                this.handle = IntPtr.Zero;

                return true;
            }
        }
    }
}
