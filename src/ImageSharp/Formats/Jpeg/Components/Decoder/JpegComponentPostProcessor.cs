// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder
{
    /// <summary>
    /// Encapsulates spectral data to rgba32 processing for one component.
    /// </summary>
    internal class JpegComponentPostProcessor : IDisposable
    {
        /// <summary>
        /// Points to the current row in <see cref="Component"/>.
        /// </summary>
        private int currentComponentRowInBlocks;

        /// <summary>
        /// The size of the area in <see cref="ColorBuffer"/> corresponding to one 8x8 Jpeg block
        /// </summary>
        private readonly Size blockAreaSize;

        /// <summary>
        /// Initializes a new instance of the <see cref="JpegComponentPostProcessor"/> class.
        /// </summary>
        public JpegComponentPostProcessor(MemoryAllocator memoryAllocator, IRawJpegData rawJpeg, Size postProcessorBufferSize, IJpegComponent component)
        {
            this.Component = component;
            this.RawJpeg = rawJpeg;
            this.blockAreaSize = this.Component.SubSamplingDivisors * 8;
            this.ColorBuffer = memoryAllocator.Allocate2DOveraligned<float>(
                postProcessorBufferSize.Width,
                postProcessorBufferSize.Height,
                this.blockAreaSize.Height);

            this.BlockRowsPerStep = postProcessorBufferSize.Height / 8 / this.Component.SubSamplingDivisors.Height;
        }

        public IRawJpegData RawJpeg { get; }

        /// <summary>
        /// Gets the <see cref="Component"/>
        /// </summary>
        public IJpegComponent Component { get; }

        /// <summary>
        /// Gets the temporary working buffer of color values.
        /// </summary>
        public Buffer2D<float> ColorBuffer { get; }

        /// <summary>
        /// Gets <see cref="IJpegComponent.SizeInBlocks"/>
        /// </summary>
        public Size SizeInBlocks => this.Component.SizeInBlocks;

        /// <summary>
        /// Gets the maximal number of block rows being processed in one step.
        /// </summary>
        public int BlockRowsPerStep { get; }

        /// <inheritdoc />
        public void Dispose() => this.ColorBuffer.Dispose();

        /// <summary>
        /// Invoke <see cref="JpegBlockPostProcessor"/> for <see cref="BlockRowsPerStep"/> block rows, copy the result into <see cref="ColorBuffer"/>.
        /// </summary>
        public void CopyBlocksToColorBuffer(int step)
        {
            Buffer2D<Block8x8> spectralBuffer = this.Component.SpectralBlocks;

            var blockPp = new JpegBlockPostProcessor(this.RawJpeg, this.Component);
            float maximumValue = MathF.Pow(2, this.RawJpeg.Precision) - 1;

            int destAreaStride = this.ColorBuffer.Width;

            int yBlockStart = step * this.BlockRowsPerStep;

            for (int y = 0; y < this.BlockRowsPerStep; y++)
            {
                int yBlock = yBlockStart + y;

                if (yBlock >= spectralBuffer.Height)
                {
                    break;
                }

                int yBuffer = y * this.blockAreaSize.Height;

                Span<float> colorBufferRow = this.ColorBuffer.GetRowSpan(yBuffer);
                Span<Block8x8> blockRow = spectralBuffer.GetRowSpan(yBlock);

                // see: https://github.com/SixLabors/ImageSharp/issues/824
                int widthInBlocks = Math.Min(spectralBuffer.Width, this.SizeInBlocks.Width);

                for (int xBlock = 0; xBlock < widthInBlocks; xBlock++)
                {
                    ref Block8x8 block = ref blockRow[xBlock];
                    int xBuffer = xBlock * this.blockAreaSize.Width;
                    ref float destAreaOrigin = ref colorBufferRow[xBuffer];

                    blockPp.ProcessBlockColorsInto(ref block, ref destAreaOrigin, destAreaStride, maximumValue);
                }
            }
        }

        public void ClearSpectralBuffers()
        {
            Buffer2D<Block8x8> spectralBlocks = this.Component.SpectralBlocks;
            for (int i = 0; i < spectralBlocks.Height; i++)
            {
                spectralBlocks.GetRowSpan(i).Clear();
            }
        }

        public void CopyBlocksToColorBuffer()
        {
            this.CopyBlocksToColorBuffer(this.currentComponentRowInBlocks);
            this.currentComponentRowInBlocks += this.BlockRowsPerStep;
        }
    }
}
