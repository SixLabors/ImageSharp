// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

namespace SixLabors.ImageSharp.Formats.WebP.Lossless
{
    internal enum PixOrCopyMode
    {
        Literal,

        CacheIdx,

        Copy,

        None
    }
}
