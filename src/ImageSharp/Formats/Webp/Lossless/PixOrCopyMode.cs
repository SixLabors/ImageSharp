// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

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
