// Copyright (c) Six Labors and contributors.
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
    }
}
