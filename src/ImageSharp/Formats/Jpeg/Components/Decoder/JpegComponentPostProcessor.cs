// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder
{
    /// <summary>
    /// Encapsulates postprocessing data for one component for <see cref="JpegImagePostProcessor"/>.
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
        public JpegComponentPostProcessor(MemoryAllocator memoryAllocator, JpegImagePostProcessor imagePostProcessor, IJpegComponent component)
        {
            this.Component = component;
            this.ImagePostProcessor = imagePostProcessor;
            this.blockAreaSize = this.Component.SubSamplingDivisors * 8;
            this.ColorBuffer = memoryAllocator.Allocate2DOveraligned<float>(
                imagePostProcessor.PostProcessorBufferSize.Width,
                imagePostProcessor.PostProcessorBufferSize.Height,
                this.blockAreaSize.Height);

            this.BlockRowsPerStep = JpegImagePostProcessor.BlockRowsPerStep / this.Component.SubSamplingDivisors.Height;
        }

        /// <summary>
        /// Gets the <see cref="JpegImagePostProcessor"/>
        /// </summary>
        public JpegImagePostProcessor ImagePostProcessor { get; }

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
        public void Dispose()
        {
            this.ColorBuffer.Dispose();
        }

        /// <summary>
        /// Invoke <see cref="JpegBlockPostProcessor"/> for <see cref="BlockRowsPerStep"/> block rows, copy the result into <see cref="ColorBuffer"/>.
        /// </summary>
        public void CopyBlocksToColorBuffer()
        {
            var blockPp = new JpegBlockPostProcessor(this.ImagePostProcessor.RawJpeg, this.Component);
            float maximumValue = MathF.Pow(2, this.ImagePostProcessor.RawJpeg.Precision) - 1;

            int destAreaStride = this.ColorBuffer.Width;

            for (int y = 0; y < this.BlockRowsPerStep; y++)
            {
                int yBlock = this.currentComponentRowInBlocks + y;

                if (yBlock >= this.SizeInBlocks.Height)
                {
                    break;
                }

                int yBuffer = y * this.blockAreaSize.Height;

                Span<float> colorBufferRow = this.ColorBuffer.GetRowSpan(yBuffer);
                Span<Block8x8> blockRow = this.Component.SpectralBlocks.GetRowSpan(yBlock);

                // see: https://github.com/SixLabors/ImageSharp/issues/824
                int widthInBlocks = Math.Min(this.Component.SpectralBlocks.Width, this.SizeInBlocks.Width);

                for (int xBlock = 0; xBlock < widthInBlocks; xBlock++)
                {
                    ref Block8x8 block = ref blockRow[xBlock];
                    int xBuffer = xBlock * this.blockAreaSize.Width;
                    ref float destAreaOrigin = ref colorBufferRow[xBuffer];

                    blockPp.ProcessBlockColorsInto(ref block, ref destAreaOrigin, destAreaStride, maximumValue);
                }
            }

            this.currentComponentRowInBlocks += this.BlockRowsPerStep;
        }
    }
}
