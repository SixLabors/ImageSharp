// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Memory.Internals
{
    internal partial class UniformByteArrayPool
    {
        internal class Buffer<T> : ManagedBufferBase<T>
            where T : struct
        {
            /// <summary>
            /// The length of the buffer.
            /// </summary>
            private readonly int length;

            private UniformByteArrayPool sourcePool;

            public Buffer(byte[] data, int length, UniformByteArrayPool sourcePool)
            {
                this.Data = data;
                this.length = length;
                this.sourcePool = sourcePool;
            }

            /// <summary>
            /// Gets the buffer as a byte array.
            /// </summary>
            protected byte[] Data { get; private set; }

            /// <inheritdoc />
            public override Span<T> GetSpan()
            {
                if (this.Data is null)
                {
                    ThrowObjectDisposedException();
                }
#if SUPPORTS_CREATESPAN
                ref byte r0 = ref MemoryMarshal.GetReference<byte>(this.Data);
                return MemoryMarshal.CreateSpan(ref Unsafe.As<byte, T>(ref r0), this.length);
#else
                return MemoryMarshal.Cast<byte, T>(this.Data.AsSpan()).Slice(0, this.length);
#endif

            }

            /// <inheritdoc />
            protected override void Dispose(bool disposing)
            {
                if (this.Data is null || this.sourcePool is null)
                {
                    return;
                }

                this.sourcePool.Return(this.Data);
                this.sourcePool = null;
                this.Data = null;
            }

            internal void MarkDisposed()
            {
                this.sourcePool = null;
                this.Data = null;
            }

            protected override object GetPinnableObject() => this.Data;

            [MethodImpl(InliningOptions.ColdPath)]
            private static void ThrowObjectDisposedException()
            {
                throw new ObjectDisposedException("UniformByteArrayPool.Buffer<T>");
            }
        }

        /// <summary>
        /// When we do byte[][] multi-buffer renting for a MemoryGroup, we handle finlaization in <see cref="MemoryGroup{T}.Owned"/>,
        /// therefore it's beneficial to not have a finalizer in <see cref="Buffer{T}"/>.
        /// However, when we need to wrap a single rented array, we need a separate finalizable type to avoid
        /// pool exhaustion caused by incorrect user code.
        /// </summary>
        public class FinalizableBuffer<T> : Buffer<T>
            where T : struct
        {
            public FinalizableBuffer(byte[] data, int length, UniformByteArrayPool sourcePool)
                : base(data, length, sourcePool)
            {
            }

            ~FinalizableBuffer() => this.Dispose(false);
        }
    }
}
