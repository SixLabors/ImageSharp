// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jpeg.PdfJsPort.Components
{
    /// <summary>
    /// Represents a section of the jpeg component data laid out in pixel order.
    /// </summary>
    internal struct PdfJsJpegPixelArea : IDisposable
    {
        private readonly int imageWidth;

        private readonly int imageHeight;

        private Buffer<byte> componentData;

        private int rowStride;

        /// <summary>
        /// Initializes a new instance of the <see cref="PdfJsJpegPixelArea"/> struct.
        /// </summary>
        /// <param name="imageWidth">The image width</param>
        /// <param name="imageHeight">The image height</param>
        /// <param name="numberOfComponents">The number of components</param>
        public PdfJsJpegPixelArea(int imageWidth, int imageHeight, int numberOfComponents)
        {
            this.imageWidth = imageWidth;
            this.imageHeight = imageHeight;
            this.Width = 0;
            this.Height = 0;
            this.NumberOfComponents = numberOfComponents;
            this.componentData = null;
            this.rowStride = 0;
        }

        /// <summary>
        /// Gets the number of components
        /// </summary>
        public int NumberOfComponents { get; }

        /// <summary>
        /// Gets the width
        /// </summary>
        public int Width { get; private set; }

        /// <summary>
        /// Gets the height
        /// </summary>
        public int Height { get; private set; }

        /// <summary>
        /// Organsizes the decoded jpeg components into a linear array ordered by component.
        /// This must be called before attempting to retrieve the data.
        /// </summary>
        /// <param name="components">The jpeg component blocks</param>
        /// <param name="width">The pixel area width</param>
        /// <param name="height">The pixel area height</param>
        public void LinearizeBlockData(PdfJsComponentBlocks components, int width, int height)
        {
            this.Width = width;
            this.Height = height;
            int numberOfComponents = this.NumberOfComponents;
            this.rowStride = width * numberOfComponents;
            var scale = new Vector2(this.imageWidth / (float)width, this.imageHeight / (float)height);

            this.componentData = new Buffer<byte>(width * height * numberOfComponents);
            Span<byte> componentDataSpan = this.componentData;
            const uint Mask3Lsb = 0xFFFFFFF8; // Used to clear the 3 LSBs

            using (var xScaleBlockOffset = new Buffer<int>(width))
            {
                Span<int> xScaleBlockOffsetSpan = xScaleBlockOffset;
                for (int i = 0; i < numberOfComponents; i++)
                {
                    ref PdfJsComponent component = ref components.Components[i];
                    Vector2 componentScale = component.Scale * scale;
                    int offset = i;
                    Span<short> output = component.Output;
                    int blocksPerScanline = (component.BlocksPerLine + 1) << 3;

                    // Precalculate the xScaleBlockOffset
                    int j;
                    for (int x = 0; x < width; x++)
                    {
                        j = (int)(x * componentScale.X);
                        xScaleBlockOffsetSpan[x] = (int)((j & Mask3Lsb) << 3) | (j & 7);
                    }

                    // Linearize the blocks of the component
                    for (int y = 0; y < height; y++)
                    {
                        j = (int)(y * componentScale.Y);
                        int index = blocksPerScanline * (int)(j & Mask3Lsb) | ((j & 7) << 3);
                        for (int x = 0; x < width; x++)
                        {
                            componentDataSpan[offset] = (byte)output[index + xScaleBlockOffsetSpan[x]];
                            offset += numberOfComponents;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets a <see cref="Span{Byte}"/> representing the row 'y' beginning from the the first byte on that row.
        /// </summary>
        /// <param name="y">The y-coordinate of the pixel row. Must be greater than or equal to zero and less than the height of the pixel area.</param>
        /// <returns>The <see cref="Span{TPixel}"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<byte> GetRowSpan(int y)
        {
            this.CheckCoordinates(y);
            return this.componentData.Slice(y * this.rowStride, this.rowStride);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.componentData?.Dispose();
            this.componentData = null;
        }

        /// <summary>
        /// Checks the coordinates to ensure they are within bounds.
        /// </summary>
        /// <param name="y">The y-coordinate of the row. Must be greater than zero and less than the height of the area.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if the coordinates are not within the bounds of the image.
        /// </exception>
        [Conditional("DEBUG")]
        private void CheckCoordinates(int y)
        {
            if (y < 0 || y >= this.Height)
            {
                throw new ArgumentOutOfRangeException(nameof(y), y, $"{y} is outwith the area bounds.");
            }
        }
    }
}