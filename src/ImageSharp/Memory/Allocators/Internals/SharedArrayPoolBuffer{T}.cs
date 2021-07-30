// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Memory.Internals
{
    internal class SharedArrayPoolBuffer<T> : ManagedBufferBase<T>
        where T : struct
    {
        private readonly int lengthInBytes;

        public SharedArrayPoolBuffer(int lengthInElements)
        {
            this.lengthInBytes = lengthInElements * Unsafe.SizeOf<T>();
            this.Array = ArrayPool<byte>.Shared.Rent(this.lengthInBytes);
        }

        ~SharedArrayPoolBuffer()
        {
            this.Dispose(false);
        }

        protected byte[] Array { get; set; }

        protected override void Dispose(bool disposing)
        {
            if (this.Array == null)
            {
                return;
            }

            ArrayPool<byte>.Shared.Return(this.Array);
            this.Array = null;
        }

        public override Span<T> GetSpan() => MemoryMarshal.Cast<byte, T>(this.Array.AsSpan(0, this.lengthInBytes));

        protected override object GetPinnableObject() => this.Array;
    }
}
