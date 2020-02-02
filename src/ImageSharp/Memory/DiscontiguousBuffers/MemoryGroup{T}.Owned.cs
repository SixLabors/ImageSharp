// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;

namespace SixLabors.ImageSharp.Memory
{
    // Analogous to the "owned" variant of MemorySource
    internal abstract partial class MemoryGroup<T>
    {
        private class Owned : MemoryGroup<T>
        {
            private IMemoryOwner<T>[] memoryOwners;

            public Owned(IMemoryOwner<T>[] memoryOwners, int bufferLength, long totalLength)
                : base(bufferLength, totalLength)
            {
                this.memoryOwners = memoryOwners;
            }

            public override IEnumerator<Memory<T>> GetEnumerator()
            {
                this.EnsureNotDisposed();
                return this.memoryOwners.Select(mo => mo.Memory).GetEnumerator();
            }


            public override int Count
            {
                get
                {
                    this.EnsureNotDisposed();
                    return this.memoryOwners.Length;
                }
            }

            public override Memory<T> this[int index]
            {
                get
                {
                    this.EnsureNotDisposed();
                    return this.memoryOwners[index].Memory;
                }
            }

            public override void Dispose()
            {
                if (this.memoryOwners == null)
                {
                    return;
                }

                foreach (IMemoryOwner<T> memoryOwner in this.memoryOwners)
                {
                    memoryOwner.Dispose();
                }

                this.memoryOwners = null;
                this.IsValid = false;
            }

            private void EnsureNotDisposed()
            {
                if (this.memoryOwners == null)
                {
                    throw new ObjectDisposedException(nameof(MemoryGroup<T>));
                }
            }
        }
    }
}
