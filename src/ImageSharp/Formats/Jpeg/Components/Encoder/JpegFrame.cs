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
        public JpegFrame(MemoryAllocator allocator, Image image, byte componentCount)
        {
            this.PixelWidth = image.Width;
            this.PixelHeight = image.Height;

            if (componentCount != 3)
            {
                throw new ArgumentException("This is YCbCr debug path only.");
            }

            this.Components = new JpegComponent[]
            {
                new JpegComponent(allocator, 1, 1, 0),
                new JpegComponent(allocator, 1, 1, 1),
                new JpegComponent(allocator, 1, 1, 1),
            };
        }

        /// <summary>
        /// Gets the number of pixel per row.
        /// </summary>
        public int PixelHeight { get; private set; }

        /// <summary>
        /// Gets the number of pixels per line.
        /// </summary>
        public int PixelWidth { get; private set; }

        /// <summary>
        /// Gets the number of components within a frame.
        /// </summary>
        public int ComponentCount => this.Components.Length;

        /// <summary>
        /// Gets the frame component collection.
        /// </summary>
        public JpegComponent[] Components { get; }

        /// <summary>
        /// Gets or sets the number of MCU's per line.
        /// </summary>
        public int McusPerLine { get; set; }

        /// <summary>
        /// Gets or sets the number of MCU's per column.
        /// </summary>
        public int McusPerColumn { get; set; }

        /// <inheritdoc/>
        public void Dispose()
        {
            for (int i = 0; i < this.Components.Length; i++)
            {
                this.Components[i]?.Dispose();
            }
        }

        /// <summary>
        /// Allocates the frame component blocks.
        /// </summary>
        /// <param name="maxSubFactorH">Maximal horizontal subsampling factor among all the components.</param>
        /// <param name="maxSubFactorV">Maximal vertical subsampling factor among all the components.</param>
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
