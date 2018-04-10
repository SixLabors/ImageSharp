// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jpeg.PdfJsPort.Components
{
    /// <summary>
    /// Represents a section of the jpeg component data laid out in pixel order.
    /// </summary>
    internal struct PdfJsJpegPixelArea : IDisposable
    {
        private readonly MemoryManager memoryManager;

        private IBuffer<byte> componentData;

        private int rowStride;

        /// <summary>
        /// Gets the number of components
        /// </summary>
        public int NumberOfComponents;

        /// <summary>
        /// Gets the width
        /// </summary>
        public int Width;

        /// <summary>
        /// Gets the height
        /// </summary>
        public int Height;

        /// <summary>
        /// Initializes a new instance of the <see cref="PdfJsJpegPixelArea"/> struct.
        /// </summary>
        /// <param name="memoryManager">The <see cref="MemoryManager"/> to use for buffer allocations.</param>
        /// <param name="imageWidth">The image width</param>
        /// <param name="imageHeight">The image height</param>
        /// <param name="numberOfComponents">The number of components</param>
        public PdfJsJpegPixelArea(MemoryManager memoryManager, int imageWidth, int imageHeight, int numberOfComponents)
        {
            this.memoryManager = memoryManager;
            this.Width = imageWidth;
            this.Height = imageHeight;
            this.NumberOfComponents = numberOfComponents;
            this.componentData = null;
            this.rowStride = this.Width * this.NumberOfComponents;
            this.componentData = this.memoryManager.Allocate<byte>(this.Width * this.Height * this.NumberOfComponents);
        }

        /// <summary>
        /// Organsizes the decoded jpeg components into a linear array ordered by component.
        /// This must be called before attempting to retrieve the data.
        /// </summary>
        /// <param name="components">The jpeg component blocks</param>
        public void LinearizeBlockData(PdfJsComponentBlocks components)
        {
            ref byte componentDataRef = ref MemoryMarshal.GetReference(this.componentData.Span);
            const uint Mask3Lsb = 0xFFFFFFF8; // Used to clear the 3 LSBs

            using (IBuffer<int> xScaleBlockOffset = this.memoryManager.Allocate<int>(this.Width))
            {
                ref int xScaleBlockOffsetRef = ref MemoryMarshal.GetReference(xScaleBlockOffset.Span);
                for (int i = 0; i < this.NumberOfComponents; i++)
                {
                    ref PdfJsComponent component = ref components.Components[i];
                    ref short outputRef = ref MemoryMarshal.GetReference(component.Output.Span);
                    Vector2 componentScale = component.Scale;
                    int blocksPerScanline = (component.BlocksPerLine + 1) << 3;

                    // Precalculate the xScaleBlockOffset
                    int j;
                    for (int x = 0; x < this.Width; x++)
                    {
                        j = (int)(x * componentScale.X);
                        Unsafe.Add(ref xScaleBlockOffsetRef, x) = (int)((j & Mask3Lsb) << 3) | (j & 7);
                    }

                    // Linearize the blocks of the component
                    int offset = i;
                    for (int y = 0; y < this.Height; y++)
                    {
                        j = (int)(y * componentScale.Y);
                        int index = blocksPerScanline * (int)(j & Mask3Lsb) | ((j & 7) << 3);
                        for (int x = 0; x < this.Width; x++)
                        {
                            Unsafe.Add(ref componentDataRef, offset) = (byte)Unsafe.Add(ref outputRef, index + Unsafe.Add(ref xScaleBlockOffsetRef, x));
                            offset += this.NumberOfComponents;
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