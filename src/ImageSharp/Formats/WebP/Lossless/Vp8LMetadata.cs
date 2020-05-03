// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

using System.Buffers;

namespace SixLabors.ImageSharp.Formats.WebP.Lossless
{
    internal class Vp8LMetadata
    {
        public int ColorCacheSize { get; set; }

        public ColorCache ColorCache { get; set; }

        public int HuffmanMask { get; set; }

        public int HuffmanSubSampleBits { get; set; }

        public int HuffmanXSize { get; set; }

        public IMemoryOwner<uint> HuffmanImage { get; set; }

        public int NumHTreeGroups { get; set; }

        public HTreeGroup[] HTreeGroups { get; set; }

        public HuffmanCode[] HuffmanTables { get; set; }
    }
}
