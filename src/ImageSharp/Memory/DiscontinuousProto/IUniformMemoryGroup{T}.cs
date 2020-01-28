using System;
using System.Collections.Generic;

namespace SixLabors.ImageSharp.Memory.DiscontinuousProto
{
    /// <summary>
    /// Represents discontinuous group of multiple uniformly-sized memory segments.
    /// The last segment can be smaller than the preceding ones.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    public interface IUniformMemoryGroup<T> : IReadOnlyList<Memory<T>> where T : struct
    {
        bool IsValid { get; }
    }
}
