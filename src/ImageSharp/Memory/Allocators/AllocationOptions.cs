// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Memory
{
    /// <summary>
    /// Options for allocating buffers.
    /// </summary>
    public enum AllocationOptions
    {
        /// <summary>
        /// Indicates that the buffer should just be allocated.
        /// </summary>
        None,

        /// <summary>
        /// Indicates that the allocated buffer should be cleaned following allocation.
        /// </summary>
        Clean
    }
}
