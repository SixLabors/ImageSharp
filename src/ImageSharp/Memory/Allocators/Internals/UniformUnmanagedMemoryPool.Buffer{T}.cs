// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Memory.Internals
{
    internal partial class UniformUnmanagedMemoryPool
    {
        public class Buffer<T> : UnmanagedBuffer<T>
            where T : struct
        {
            private UniformUnmanagedMemoryPool pool;

            public Buffer(UniformUnmanagedMemoryPool pool, UnmanagedMemoryHandle bufferHandle, int length)
                : base(bufferHandle, length) =>
                this.pool = pool;

            /// <inheritdoc />
            protected override void Dispose(bool disposing)
            {
                if (this.pool == null)
                {
                    return;
                }

                this.pool.Return(this.BufferHandle);
                this.pool = null;
                this.BufferHandle = null;
            }

            internal void MarkDisposed()
            {
                this.pool = null;
                this.BufferHandle = null;
            }
        }

        public sealed class FinalizableBuffer<T> : Buffer<T>
            where T : struct
        {
            public FinalizableBuffer(UniformUnmanagedMemoryPool pool, UnmanagedMemoryHandle bufferHandle, int length)
                : base(pool, bufferHandle, length)
            {
                bufferHandle.AssignedToNewOwner();
            }

            // A VERY poorly written user code holding a Span<TPixel> on the stack,
            // while loosing the reference to Image<TPixel> (or disposing it) may write to (now unrelated) pool buffer,
            // or cause memory corruption if the underlying UmnanagedMemoryHandle has been released.
            // This is an unlikely scenario we mitigate a warning in DangerousGetRowSpan(i) APIs.
#pragma warning disable CA2015 // Adding a finalizer to a type derived from MemoryManager<T> may permit memory to be freed while it is still in use by a Span<T>
            ~FinalizableBuffer() => this.Dispose(false);
#pragma warning  restore

            protected override void Dispose(bool disposing)
            {
                if (!disposing && this.BufferHandle != null)
                {
                    // We need to prevent handle finalization here.
                    // See comments on UnmanagedMemoryHandle.Resurrect()
                    this.BufferHandle.Resurrect();
                }

                base.Dispose(disposing);
                GC.SuppressFinalize(this);
            }
        }
    }
}
