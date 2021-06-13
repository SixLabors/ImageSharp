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
        private enum MemoryPressure
        {
            Low = 0,
            Medium = 1,
            High = 2
        }

        /// <summary>
        /// The buffer implementation of <see cref="ArrayPoolMemoryAllocator"/>.
        /// </summary>
        /// <typeparam name="T">Type of the data stored in the buffer.</typeparam>
        private class Buffer<T> : ManagedBufferBase<T>
            where T : struct
        {
            /// <summary>
            /// The length of the buffer.
            /// </summary>
            private readonly int length;

            /// <summary>
            /// A reference to the source pool.
            /// </summary>
            private readonly ArrayPool<byte> sourcePool;

            public Buffer(byte[] data, int length, ArrayPool<byte> sourcePool)
            {
                this.Data = data;
                this.length = length;
                this.sourcePool = sourcePool;
            }

            /// <summary>
            /// Gets the buffer as a byte array.
            /// </summary>
            protected byte[] Data { get; }

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
                if (!disposing || this.Data is null)
                {
                    return;
                }

                this.sourcePool.Return(this.Data);
            }

            protected override object GetPinnableObject() => this.Data;

            [MethodImpl(InliningOptions.ColdPath)]
            private static void ThrowObjectDisposedException()
                => throw new ObjectDisposedException("ArrayPoolMemoryAllocator.Buffer<T>");
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
