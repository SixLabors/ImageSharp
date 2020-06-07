// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Memory
{
    internal delegate void TransformItemsDelegate<TSource, TTarget>(ReadOnlySpan<TSource> source, Span<TTarget> target);
}
