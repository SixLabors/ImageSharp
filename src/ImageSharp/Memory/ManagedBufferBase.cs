// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Buffers;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Memory
{
    /// <summary>
    /// Provides a base class for <see cref="IBuffer{T}"/> implementations by implementing pinning logic for <see cref="MemoryManager{T}"/> adaption.
    /// </summary>
    internal abstract class ManagedBufferBase<T> : System.Buffers.MemoryManager<T>
    {
        private GCHandle pinHandle;

        /// <summary>
        /// Gets the object that should be pinned.
        /// </summary>
        protected abstract object GetPinnableObject();

        public override unsafe MemoryHandle Pin(int elementIndex = 0)
        {
            if (!this.pinHandle.IsAllocated)
            {
                this.pinHandle = GCHandle.Alloc(this.GetPinnableObject(), GCHandleType.Pinned);
            }

            void* ptr = (void*)this.pinHandle.AddrOfPinnedObject();
            return new MemoryHandle(ptr, this.pinHandle);
        }

        public override void Unpin()
        {
            if (this.pinHandle.IsAllocated)
            {
                this.pinHandle.Free();
            }
        }
    }
}