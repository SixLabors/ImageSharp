// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Memory.DiscontiguousBuffers
{
    /// <summary>
    /// A value-type enumerator for <see cref="MemoryGroup{T}"/> instances.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    public ref struct MemoryGroupEnumerator<T>
        where T : struct
    {
        private readonly MemoryGroup<T> memoryGroup;
        private readonly int count;
        private int index;

        [MethodImpl(InliningOptions.ShortMethod)]
        internal MemoryGroupEnumerator(MemoryGroup<T>.Owned memoryGroup)
        {
            this.memoryGroup = memoryGroup;
            this.count = memoryGroup.Count;
            this.index = -1;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        internal MemoryGroupEnumerator(MemoryGroup<T>.Consumed memoryGroup)
        {
            this.memoryGroup = memoryGroup;
            this.count = memoryGroup.Count;
            this.index = -1;
        }

        /// <inheritdoc cref="System.Collections.Generic.IEnumerator{T}.Current"/>
        public Memory<T> Current
        {
            [MethodImpl(InliningOptions.ShortMethod)]
            get => this.memoryGroup[this.index];
        }

        /// <inheritdoc cref="System.Collections.IEnumerator.MoveNext"/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public bool MoveNext()
        {
            int index = this.index + 1;

            if (index < this.count)
            {
                this.index = index;

                return true;
            }

            return false;
        }
    }
}
