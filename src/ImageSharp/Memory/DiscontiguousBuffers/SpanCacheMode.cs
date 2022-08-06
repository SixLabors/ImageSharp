// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Memory
{
    /// <summary>
    /// Selects active values in <see cref="MemoryGroupSpanCache"/>.
    /// </summary>
    internal enum SpanCacheMode
    {
        Default = default,
        SingleArray,
        SinglePointer,
        MultiPointer
    }
}
