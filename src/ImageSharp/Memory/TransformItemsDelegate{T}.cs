// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

using System;

namespace SixLabors.ImageSharp.Memory
{
    internal delegate void TransformItemsDelegate<TSource, TTarget>(ReadOnlySpan<TSource> source, Span<TTarget> target);
}
