// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Encoder
{
    internal class JpegComponentConfig
    {
        public JpegComponentConfig(byte id, int hsf, int vsf, int quantIndex, int dcIndex, int acIndex)
        {
            this.Id = id;
            this.HorizontalSampleFactor = hsf;
            this.VerticalSampleFactor = vsf;
            this.QuantizatioTableIndex = quantIndex;
            this.DcTableSelector = dcIndex;
            this.AcTableSelector = acIndex;
        }

        public byte Id { get; }

        public int HorizontalSampleFactor { get; }

        public int VerticalSampleFactor { get; }

        public int QuantizatioTableIndex { get; }

        public int DcTableSelector { get; }

        public int AcTableSelector { get; }
    }
}
