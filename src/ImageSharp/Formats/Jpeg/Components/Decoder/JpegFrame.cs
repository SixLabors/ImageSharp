// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder
{
    /// <summary>
    /// Represent a single jpeg frame.
    /// </summary>
    internal sealed class JpegFrame : IDisposable
    {
        public JpegFrame(JpegFileMarker sofMarker, byte precision, int width, int height, byte componentCount)
        {
            this.IsExtended = sofMarker.Marker is JpegConstants.Markers.SOF1 or JpegConstants.Markers.SOF9;
            this.Progressive = sofMarker.Marker is JpegConstants.Markers.SOF2 or JpegConstants.Markers.SOF10;

            this.Precision = precision;
            this.MaxColorChannelValue = MathF.Pow(2, precision) - 1;

            this.PixelWidth = width;
            this.PixelHeight = height;

            this.ComponentCount = componentCount;
        }

        /// <summary>
        /// Gets a value indicating whether the frame uses the extended specification.
        /// </summary>
        public bool IsExtended { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the frame uses the progressive specification.
        /// </summary>
        public bool Progressive { get; private set; }

        /// <summary>
        /// Gets or sets a value indicating whether the frame is encoded using multiple scans (SOS markers).
        /// </summary>
        /// <remarks>
        /// This is true for progressive and baseline non-interleaved images.
        /// </remarks>
        public bool Interleaved { get; set; }

        /// <summary>
        /// Gets the precision.
        /// </summary>
        public byte Precision { get; private set; }

        /// <summary>
        /// Gets the maximum color value derived from <see cref="Precision"/>.
        /// </summary>
        public float MaxColorChannelValue { get; private set; }

        /// <summary>
        /// Gets the number of pixel per row.
        /// </summary>
        public int PixelHeight { get; private set; }

        /// <summary>
        /// Gets the number of pixels per line.
        /// </summary>
        public int PixelWidth { get; private set; }

        /// <summary>
        /// Gets the pixel size of the image.
        /// </summary>
        public Size PixelSize => new(this.PixelWidth, this.PixelHeight);

        /// <summary>
        /// Gets the number of components within a frame.
        /// </summary>
        public byte ComponentCount { get; private set; }

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
        public Size McuSize => new(this.McusPerLine, this.McusPerColumn);

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
                IJpegComponent component = this.Components[i];
                component.Init(maxSubFactorH, maxSubFactorV);
            }
        }

        public void AllocateComponents()
        {
            bool fullScan = this.Progressive || !this.Interleaved;
            for (int i = 0; i < this.ComponentCount; i++)
            {
                IJpegComponent component = this.Components[i];
                component.AllocateSpectral(fullScan);
            }
        }
    }
}
