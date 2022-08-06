// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Encoder
{
    internal class JpegHuffmanTableConfig
    {
        public JpegHuffmanTableConfig(int @class, int destIndex, HuffmanSpec table)
        {
            this.Class = @class;
            this.DestinationIndex = destIndex;
            this.Table = table;
        }

        public int Class { get; }

        public int DestinationIndex { get; }

        public HuffmanSpec Table { get; }
    }
}
