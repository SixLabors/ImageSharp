// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

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
        Clean = 1
    }
}
