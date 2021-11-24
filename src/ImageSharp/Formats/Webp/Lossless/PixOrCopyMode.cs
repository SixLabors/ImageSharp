// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Webp.Lossless
{
    internal enum PixOrCopyMode : byte
    {
        Literal,

        CacheIdx,

        Copy,

        None
    }
}
