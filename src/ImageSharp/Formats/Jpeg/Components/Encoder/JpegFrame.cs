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

            JpegComponentConfig[] componentConfigs = frameConfig.Components;
            this.Components = new JpegComponent[componentConfigs.Length];
            for (int i = 0; i < this.Components.Length; i++)
            {
                JpegComponentConfig componentConfig = componentConfigs[i];
                this.Components[i] = new JpegComponent(allocator, componentConfig.HorizontalSampleFactor, componentConfig.VerticalSampleFactor, componentConfig.QuantizatioTableIndex)
                {
                    DcTableId = componentConfig.DcTableSelector,
                    AcTableId = componentConfig.AcTableSelector,
                };

                this.BlocksPerMcu += componentConfig.HorizontalSampleFactor * componentConfig.VerticalSampleFactor;
            }

            int maxSubFactorH = frameConfig.MaxHorizontalSamplingFactor;
            int maxSubFactorV = frameConfig.MaxVerticalSamplingFactor;
            this.McusPerLine = (int)Numerics.DivideCeil((uint)image.Width, (uint)maxSubFactorH * 8);
            this.McusPerColumn = (int)Numerics.DivideCeil((uint)image.Height, (uint)maxSubFactorV * 8);

            for (int i = 0; i < this.ComponentCount; i++)
            {
                JpegComponent component = this.Components[i];
                component.Init(this, maxSubFactorH, maxSubFactorV);
            }
        }

        public JpegColorSpace ColorSpace { get; }

        public int PixelHeight { get; private set; }

        public int PixelWidth { get; private set; }

        public int ComponentCount => this.Components.Length;

        public JpegComponent[] Components { get; }

        public int McusPerLine { get; }

        public int McusPerColumn { get; }

        public int BlocksPerMcu { get; }

        public void Dispose()
        {
            for (int i = 0; i < this.Components.Length; i++)
            {
                this.Components[i]?.Dispose();
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
