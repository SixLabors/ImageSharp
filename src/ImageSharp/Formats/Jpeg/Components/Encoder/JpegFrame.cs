// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Encoder
{
    /// <summary>
    /// Represent a single jpeg frame.
    /// </summary>
    internal sealed class JpegFrame : IDisposable
    {
        public JpegFrame(MemoryAllocator allocator, Image image, Decoder.JpegColorSpace colorSpace)
        {
            this.ColorSpace = colorSpace;

            this.PixelWidth = image.Width;
            this.PixelHeight = image.Height;

            // int componentCount = 3;
            this.Components = new JpegComponent[]
            {
                // RGB
                new JpegComponent(allocator, 1, 1, 0) { DcTableId = 0, AcTableId = 1 },
                new JpegComponent(allocator, 1, 1, 0) { DcTableId = 0, AcTableId = 1 },
                new JpegComponent(allocator, 1, 1, 0) { DcTableId = 0, AcTableId = 1 },

                // YCbCr
                //new JpegComponent(allocator, 1, 1, 0) { DcTableId = 0, AcTableId = 1 },
                //new JpegComponent(allocator, 1, 1, 1) { DcTableId = 2, AcTableId = 3 },
                //new JpegComponent(allocator, 1, 1, 1) { DcTableId = 2, AcTableId = 3 },

                // Luminance
                //new JpegComponent(allocator, 1, 1, 0) { DcTableId = 0, AcTableId = 1 }
            };
        }

        public Decoder.JpegColorSpace ColorSpace { get; }

        public int PixelHeight { get; private set; }

        public int PixelWidth { get; private set; }

        public int ComponentCount => this.Components.Length;

        public JpegComponent[] Components { get; }

        public int McusPerLine { get; set; }

        public int McusPerColumn { get; set; }

        public void Dispose()
        {
            for (int i = 0; i < this.Components.Length; i++)
            {
                this.Components[i]?.Dispose();
            }
        }

        public void Init(int maxSubFactorH, int maxSubFactorV)
        {
            this.McusPerLine = (int)Numerics.DivideCeil((uint)this.PixelWidth, (uint)maxSubFactorH * 8);
            this.McusPerColumn = (int)Numerics.DivideCeil((uint)this.PixelHeight, (uint)maxSubFactorV * 8);

            for (int i = 0; i < this.ComponentCount; i++)
            {
                JpegComponent component = this.Components[i];
                component.Init(this, maxSubFactorH, maxSubFactorV);
            }
        }

        public void AllocateComponents(bool fullScan)
        {
            for (int i = 0; i < this.ComponentCount; i++)
            {
                JpegComponent component = this.Components[i];
                component.AllocateSpectral(fullScan);
            }
        }
    }
}
