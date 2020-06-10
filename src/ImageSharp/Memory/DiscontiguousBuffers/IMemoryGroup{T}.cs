// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;

namespace SixLabors.ImageSharp.Memory
{
    /// <summary>
    /// Represents discontiguous group of multiple uniformly-sized memory segments.
    /// The last segment can be smaller than the preceding ones.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    public interface IMemoryGroup<T> : IReadOnlyList<Memory<T>>
        where T : struct
    {
        /// <summary>
        /// Gets the number of elements per contiguous sub-buffer preceding the last buffer.
        /// The last buffer is allowed to be smaller.
        /// </summary>
        int BufferLength { get; }

        /// <summary>
        /// Gets the aggregate number of elements in the group.
        /// </summary>
        long TotalLength { get; }

        /// <summary>
        /// Gets a value indicating whether the group has been invalidated.
        /// </summary>
        /// <remarks>
        /// Invalidation usually occurs when an image processor capable to alter the image dimensions replaces
        /// the image buffers internally.
        /// </remarks>
        bool IsValid { get; }

        /// <summary>
        /// Returns a value-type implementing an allocation-free enumerator of the memory groups in the current
        /// instance. The return type shouldn't be used directly: just use a <see langword="foreach"/> block on
        /// the <see cref="IMemoryGroup{T}"/> instance in use and the C# compiler will automatically invoke this
        /// method behind the scenes. This method takes precedence over the <see cref="IEnumerable{T}.GetEnumerator"/>
        /// implementation, which is still available when casting to one of the underlying interfaces.
        /// </summary>
        /// <returns>A new <see cref="MemoryGroupEnumerator{T}"/> instance mapping the current <see cref="Memory{T}"/> values in use.</returns>
        new MemoryGroupEnumerator<T> GetEnumerator();
    }
}
