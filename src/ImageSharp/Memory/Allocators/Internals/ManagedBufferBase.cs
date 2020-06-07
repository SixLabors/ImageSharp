// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Buffers;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Memory.Internals
{
    /// <summary>
    /// Provides a base class for <see cref="IMemoryOwner{T}"/> implementations by implementing pinning logic for <see cref="MemoryManager{T}"/> adaption.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    internal abstract class ManagedBufferBase<T> : MemoryManager<T>
        where T : struct
    {
        private GCHandle pinHandle;

        /// <inheritdoc />
        public override unsafe MemoryHandle Pin(int elementIndex = 0)
        {
            if (!this.pinHandle.IsAllocated)
            {
                this.pinHandle = GCHandle.Alloc(this.GetPinnableObject(), GCHandleType.Pinned);
            }

            void* ptr = (void*)this.pinHandle.AddrOfPinnedObject();
            return new MemoryHandle(ptr, this.pinHandle);
        }

        /// <inheritdoc />
        public override void Unpin()
        {
            if (this.pinHandle.IsAllocated)
            {
                this.pinHandle.Free();
            }
        }

        /// <summary>
        /// Gets the object that should be pinned.
        /// </summary>
        /// <returns>The pinnable <see cref="object"/>.</returns>
        protected abstract object GetPinnableObject();
    }
}