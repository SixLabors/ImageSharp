// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;

namespace SixLabors.ImageSharp.Formats.Webp.Lossless
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
