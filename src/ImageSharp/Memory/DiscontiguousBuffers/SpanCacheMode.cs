// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Memory
{
    internal enum SpanCacheMode
    {
        Default = default,
        SingleArray,
        SinglePointer,
        MultiPointer
    }
}
