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
        /// Gets the number of elements per contiguous sub-block.
        /// </summary>
        public int BlockSize { get; }

        bool IsValid { get; }
    }
}
