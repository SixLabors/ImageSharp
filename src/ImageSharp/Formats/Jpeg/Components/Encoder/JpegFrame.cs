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
        public JpegFrame(JpegFrameConfig frameConfig, MemoryAllocator allocator, Image image, JpegColorSpace colorSpace)
        {
            this.ColorSpace = colorSpace;

            this.PixelWidth = image.Width;
            this.PixelHeight = image.Height;

            // int componentCount = 3;
            var componentConfigs = frameConfig.Components;
            this.Components = new JpegComponent[componentConfigs.Length];
            for (int i = 0; i < this.Components.Length; i++)
            {
                var componentConfig = componentConfigs[i];
                this.Components[i] = new JpegComponent(allocator, componentConfig.HorizontalSampleFactor, componentConfig.VerticalSampleFactor, componentConfig.QuantizatioTableIndex)
                {
                    DcTableId = componentConfig.dcTableSelector,
                    AcTableId = componentConfig.acTableSelector,
                };
            }
        }

        public JpegColorSpace ColorSpace { get; }

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
