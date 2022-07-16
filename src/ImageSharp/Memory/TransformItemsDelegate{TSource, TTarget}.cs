// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;

namespace SixLabors.ImageSharp.Memory
{
#pragma warning disable SA1649 // File name should match first type name
    internal delegate void TransformItemsDelegate<TSource, TTarget>(ReadOnlySpan<TSource> source, Span<TTarget> target);
#pragma warning restore SA1649 // File name should match first type name
}
