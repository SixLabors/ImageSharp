// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Memory
{
    /// <summary>
    /// Options for allocating buffers.
    /// </summary>
    [Flags]
    public enum AllocationOptions
    {
        /// <summary>
        /// Indicates that the buffer should just be allocated.
        /// </summary>
        None = 0,

        /// <summary>
        /// Indicates that the allocated buffer should be cleaned following allocation.
        /// </summary>
        Clean = 1,

        /// <summary>
        /// Affects only group allocations.
        /// Indicates that the requested <see cref="MemoryGroup{T}"/> or <see cref="Buffer2D{T}"/> should be made of contiguous blocks up to <see cref="int.MaxValue"/>.
        /// </summary>
        Contiguous = 2
    }
}
