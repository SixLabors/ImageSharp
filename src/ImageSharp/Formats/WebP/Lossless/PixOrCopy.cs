// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

namespace SixLabors.ImageSharp.Formats.WebP.Lossless
{
    internal class PixOrCopy
    {
        public PixOrCopyMode Mode { get; }

        public short Len { get; }

        public uint ArgbOrDistance { get; }

        public bool IsLiteral()
        {
            return this.Mode == PixOrCopyMode.Literal;
        }

        public bool IsCacheIdx()
        {
            return this.Mode == PixOrCopyMode.CacheIdx;
        }

        public bool IsCopy()
        {
            return this.Mode == PixOrCopyMode.Copy;
        }
    }
}
