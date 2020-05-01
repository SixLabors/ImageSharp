// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

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
