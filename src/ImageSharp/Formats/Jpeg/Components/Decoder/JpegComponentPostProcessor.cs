// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using SixLabors.ImageSharp.Memory;
using SixLabors.Memory;
using SixLabors.Primitives;

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
            this.ColorBuffer = memoryAllocator.Allocate2D<float>(
                imagePostProcessor.PostProcessorBufferSize.Width,
                imagePostProcessor.PostProcessorBufferSize.Height);

            this.BlockRowsPerStep = JpegImagePostProcessor.BlockRowsPerStep / this.Component.SubSamplingDivisors.Height;
            this.blockAreaSize = this.Component.SubSamplingDivisors * 8;
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
            float maximumValue = (float)Math.Pow(2, this.ImagePostProcessor.RawJpeg.Precision) - 1;

            for (int y = 0; y < this.BlockRowsPerStep; y++)
            {
                int yBlock = this.currentComponentRowInBlocks + y;

                if (yBlock >= this.SizeInBlocks.Height)
                {
                    break;
                }

                int yBuffer = y * this.blockAreaSize.Height;

                Span<Block8x8> blockRow = this.Component.SpectralBlocks.GetRowSpan(yBlock);

                ref Block8x8 blockRowBase = ref MemoryMarshal.GetReference(blockRow);

                for (int xBlock = 0; xBlock < this.SizeInBlocks.Width; xBlock++)
                {
                    ref Block8x8 block = ref Unsafe.Add(ref blockRowBase, xBlock);
                    int xBuffer = xBlock * this.blockAreaSize.Width;

                    BufferArea<float> destArea = this.ColorBuffer.GetArea(
                        xBuffer,
                        yBuffer,
                        this.blockAreaSize.Width,
                        this.blockAreaSize.Height);

                    blockPp.ProcessBlockColorsInto(ref block, destArea, maximumValue);
                }
            }

            this.currentComponentRowInBlocks += this.BlockRowsPerStep;
        }
    }
}