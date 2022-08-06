// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Encoder
{
    internal class JpegFrameConfig
    {
        public JpegFrameConfig(JpegColorSpace colorType, JpegEncodingColor encodingColor, JpegComponentConfig[] components, JpegHuffmanTableConfig[] huffmanTables, JpegQuantizationTableConfig[] quantTables)
        {
            this.ColorType = colorType;
            this.EncodingColor = encodingColor;
            this.Components = components;
            this.HuffmanTables = huffmanTables;
            this.QuantizationTables = quantTables;

            this.MaxHorizontalSamplingFactor = components[0].HorizontalSampleFactor;
            this.MaxVerticalSamplingFactor = components[0].VerticalSampleFactor;
            for (int i = 1; i < components.Length; i++)
            {
                JpegComponentConfig component = components[i];
                this.MaxHorizontalSamplingFactor = Math.Max(this.MaxHorizontalSamplingFactor, component.HorizontalSampleFactor);
                this.MaxVerticalSamplingFactor = Math.Max(this.MaxVerticalSamplingFactor, component.VerticalSampleFactor);
            }
        }

        public JpegColorSpace ColorType { get; }

        public JpegEncodingColor EncodingColor { get; }

        public JpegComponentConfig[] Components { get; }

        public JpegHuffmanTableConfig[] HuffmanTables { get; }

        public JpegQuantizationTableConfig[] QuantizationTables { get; }

        public int MaxHorizontalSamplingFactor { get; }

        public int MaxVerticalSamplingFactor { get; }

        public byte? AdobeColorTransformMarkerFlag { get; set; } = null;
    }
}
