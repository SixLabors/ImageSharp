// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Encoder
{
    internal class JpegQuantizationTableConfig
    {
        public JpegQuantizationTableConfig(int destIndex, ReadOnlySpan<byte> quantizationTable)
        {
            this.DestinationIndex = destIndex;
            this.Table = Block8x8.Load(quantizationTable);
        }

        public int DestinationIndex { get; }

        public Block8x8 Table { get; }
    }
}
