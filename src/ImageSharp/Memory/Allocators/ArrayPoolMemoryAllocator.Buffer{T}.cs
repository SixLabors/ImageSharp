// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory.Internals;

namespace SixLabors.ImageSharp.Memory
{
    /// <summary>
    /// Contains <see cref="Buffer{T}"/> and <see cref="ManagedByteBuffer"/>.
    /// </summary>
    public partial class ArrayPoolMemoryAllocator
    {
        /// <summary>
        /// The buffer implementation of <see cref="ArrayPoolMemoryAllocator"/>.
        /// </summary>
        private class Buffer<T> : ManagedBufferBase<T>
            where T : struct
        {
            /// <summary>
            /// The length of the buffer.
            /// </summary>
            private readonly int length;

            /// <summary>
            /// A weak reference to the source pool.
            /// </summary>
            /// <remarks>
            /// By using a weak reference here, we are making sure that array pools and their retained arrays are always GC-ed
            /// after a call to <see cref="ArrayPoolMemoryAllocator.ReleaseRetainedResources"/>, regardless of having buffer instances still being in use.
            /// </remarks>
            private WeakReference<ArrayPool<byte>> sourcePoolReference;

            public Buffer(byte[] data, int length, ArrayPool<byte> sourcePool)
            {
                this.Data = data;
                this.length = length;
                this.sourcePoolReference = new WeakReference<ArrayPool<byte>>(sourcePool);
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
                if (!disposing || this.Data is null || this.sourcePoolReference is null)
                {
                    return;
                }

                if (this.sourcePoolReference.TryGetTarget(out ArrayPool<byte> pool))
                {
                    pool.Return(this.Data);
                }

                this.sourcePoolReference = null;
                this.Data = null;
            }

            protected override object GetPinnableObject() => this.Data;

            [MethodImpl(InliningOptions.ColdPath)]
            private static void ThrowObjectDisposedException()
            {
                throw new ObjectDisposedException("ArrayPoolMemoryAllocator.Buffer<T>");
            }
        }

        /// <summary>
        /// The <see cref="IManagedByteBuffer"/> implementation of <see cref="ArrayPoolMemoryAllocator"/>.
        /// </summary>
        private sealed class ManagedByteBuffer : Buffer<byte>, IManagedByteBuffer
        {
            public ManagedByteBuffer(byte[] data, int length, ArrayPool<byte> sourcePool)
                : base(data, length, sourcePool)
            {
            }

            /// <inheritdoc />
            public byte[] Array => this.Data;
        }
    }
}
