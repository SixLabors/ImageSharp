// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Memory.Internals
{
    /// <summary>
    /// Defines an common interface for ref-counted objects.
    /// </summary>
    internal interface IRefCounted
    {
        /// <summary>
        /// Increments the reference counter.
        /// </summary>
        void AddRef();

        /// <summary>
        /// Decrements the reference counter.
        /// </summary>
        void ReleaseRef();
    }
}
