// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Memory.Internals
{
    internal partial class UniformByteArrayPool
    {
        public class Buffer<T> : ManagedBufferBase<T>
            where T : struct
        {
            /// <summary>
            /// Gets the buffer as a byte array.
            /// </summary>
            private byte[] data;

            /// <summary>
            /// The length of the buffer.
            /// </summary>
            private readonly int length;

            public Buffer(byte[] data, int length, UniformByteArrayPool sourcePool)
            {
                DebugGuard.NotNull(data, nameof(data));
                this.data = data;
                this.length = length;
                this.SourcePoolReference = new WeakReference<UniformByteArrayPool>(sourcePool);
            }

            protected WeakReference<UniformByteArrayPool> SourcePoolReference { get; private set; }

            /// <inheritdoc />
            public override Span<T> GetSpan()
            {
                if (this.data is null)
                {
                    ThrowObjectDisposedException();
                }

#if SUPPORTS_CREATESPAN && !DEBUG
                ref byte r0 = ref MemoryMarshal.GetReference<byte>(this.data);
                return MemoryMarshal.CreateSpan(ref Unsafe.As<byte, T>(ref r0), this.length);
#else
                return MemoryMarshal.Cast<byte, T>(this.data.AsSpan()).Slice(0, this.length);
#endif
            }

            /// <inheritdoc />
            protected override void Dispose(bool disposing)
            {
                if (this.data is null || this.SourcePoolReference is null)
                {
                    return;
                }

                if (this.SourcePoolReference.TryGetTarget(out UniformByteArrayPool pool))
                {
                    pool.Return(this.data);
                }

                WeakReferenceTracker.Remove(this.SourcePoolReference);
                this.SourcePoolReference = null;
                this.data = null;
            }

            internal void MarkDisposed()
            {
                this.SourcePoolReference = null;
                this.data = null;
            }

            protected override object GetPinnableObject() => this.data;

            [MethodImpl(InliningOptions.ColdPath)]
            private static void ThrowObjectDisposedException()
            {
                throw new ObjectDisposedException("UniformByteArrayPool.Buffer<T>");
            }
        }

        /// <summary>
        /// When we do byte[][] multi-buffer renting for a MemoryGroup, we handle finlaization
        /// in <see cref="MemoryGroup{T}.Owned"/>,
        /// therefore it's beneficial to not have a finalizer in <see cref="Buffer{T}"/>.
        /// However, when we are wrapping a single rented array, it's better to implement finalization
        /// in the wrapping buffer type.
        /// </summary>
        public class FinalizableBuffer<T> : Buffer<T>
            where T : struct
        {
            public FinalizableBuffer(byte[] data, int length, UniformByteArrayPool sourcePool)
                : base(data, length, sourcePool)
            {
                // Track all WeakReference's to make sure they are not finalized before ~FinalizableBuffer<T>()
                WeakReferenceTracker.Add(this.SourcePoolReference);
            }

            protected override void Dispose(bool disposing)
            {
                if (this.SourcePoolReference != null)
                {
                    WeakReferenceTracker.Remove(this.SourcePoolReference);
                }

                base.Dispose(disposing);
            }

            ~FinalizableBuffer()
            {
                this.Dispose(false);
            }
        }
    }
}
