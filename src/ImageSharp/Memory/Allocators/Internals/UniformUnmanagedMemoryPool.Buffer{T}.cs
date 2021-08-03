// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Memory.Internals
{
    internal partial class UniformUnmanagedMemoryPool
    {
        public unsafe class Buffer<T> : MemoryManager<T>
            where T : struct
        {
            private UniformUnmanagedMemoryPool pool;
            private UnmanagedMemoryHandle bufferHandle;
            private readonly int length;

            public Buffer(UniformUnmanagedMemoryPool pool, UnmanagedMemoryHandle bufferHandle, int length)
            {
                this.pool = pool;
                this.bufferHandle = bufferHandle;
                this.length = length;
            }

            private void* Pointer => (void*)this.bufferHandle.DangerousGetHandle();

            protected override void Dispose(bool disposing)
            {
                if (this.pool == null)
                {
                    return;
                }

                this.pool.Return(this.bufferHandle);
                this.pool = null;
                this.bufferHandle = null;
            }

            public override Span<T> GetSpan() => new Span<T>(this.Pointer, this.length);

            /// <inheritdoc />
            public override MemoryHandle Pin(int elementIndex = 0)
            {
                // Will be released in Unpin
                bool unused = false;
                this.bufferHandle.DangerousAddRef(ref unused);

                void* pbData = Unsafe.Add<T>(this.Pointer, elementIndex);
                return new MemoryHandle(pbData);
            }

            /// <inheritdoc />
            public override void Unpin() => this.bufferHandle.DangerousRelease();


        }
    }
}
