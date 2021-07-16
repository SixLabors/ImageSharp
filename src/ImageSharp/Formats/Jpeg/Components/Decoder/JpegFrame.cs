// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder
{
    /// <summary>
    /// Represent a single jpeg frame
    /// </summary>
    internal sealed class JpegFrame : IDisposable
    {
        /// <summary>
        /// Gets or sets a value indicating whether the frame uses the extended specification.
        /// </summary>
        public bool Extended { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the frame uses the progressive specification.
        /// </summary>
        public bool Progressive { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the frame is encoded using multiple scans (SOS markers).
        /// </summary>
        /// <remarks>
        /// This is true for progressive and baseline non-interleaved images.
        /// </remarks>
        public bool MultiScan { get; set; }

        /// <summary>
        /// Gets or sets the precision.
        /// </summary>
        public byte Precision { get; set; }

        /// <summary>
        /// Gets or sets the number of scanlines within the frame.
        /// </summary>
        public int PixelHeight { get; set; }

        /// <summary>
        /// Gets or sets the number of samples per scanline.
        /// </summary>
        public int PixelWidth { get; set; }

        /// <summary>
        /// Gets the pixel size of the image.
        /// </summary>
        public Size PixelSize => new Size(this.PixelWidth, this.PixelHeight);

        /// <summary>
        /// Gets or sets the number of components within a frame. In progressive frames this value can range from only 1 to 4.
        /// </summary>
        public byte ComponentCount { get; set; }

        /// <summary>
        /// Gets or sets the component id collection.
        /// </summary>
        public byte[] ComponentIds { get; set; }

        /// <summary>
        /// Gets or sets the order in which to process the components.
        /// in interleaved mode.
        /// </summary>
        public byte[] ComponentOrder { get; set; }

        /// <summary>
        /// Gets or sets the frame component collection.
        /// </summary>
        public JpegComponent[] Components { get; set; }

        /// <summary>
        /// Gets or sets the number of MCU's per line.
        /// </summary>
        public int McusPerLine { get; set; }

        /// <summary>
        /// Gets or sets the number of MCU's per column.
        /// </summary>
        public int McusPerColumn { get; set; }

        /// <summary>
        /// Gets the mcu size of the image.
        /// </summary>
        public Size McuSize => new Size(this.McusPerLine, this.McusPerColumn);

        /// <summary>
        /// Gets the color depth, in number of bits per pixel.
        /// </summary>
        public int BitsPerPixel => this.ComponentCount * this.Precision;

        /// <inheritdoc/>
        public void Dispose()
        {
            if (this.Components != null)
            {
                for (int i = 0; i < this.Components.Length; i++)
                {
                    this.Components[i]?.Dispose();
                }

                this.Components = null;
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
                component.Init(maxSubFactorH, maxSubFactorV);
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
