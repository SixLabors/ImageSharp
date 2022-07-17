// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder
{
    /// <summary>
    /// Base class for processing component spectral data and converting it to raw color data.
    /// </summary>
    internal abstract class ComponentProcessor : IDisposable
    {
        public ComponentProcessor(MemoryAllocator memoryAllocator, JpegFrame frame, Size postProcessorBufferSize, IJpegComponent component, int blockSize)
        {
            this.Frame = frame;
            this.Component = component;

            this.BlockAreaSize = component.SubSamplingDivisors * blockSize;
            this.ColorBuffer = memoryAllocator.Allocate2DOveraligned<float>(
                postProcessorBufferSize.Width,
                postProcessorBufferSize.Height,
                this.BlockAreaSize.Height);
        }

        protected JpegFrame Frame { get; }

        protected IJpegComponent Component { get; }

        protected Buffer2D<float> ColorBuffer { get; }

        protected Size BlockAreaSize { get; }

        /// <summary>
        /// Converts spectral data to color data accessible via <see cref="GetColorBufferRowSpan(int)"/>.
        /// </summary>
        /// <param name="row">Spectral row index to convert.</param>
        public abstract void CopyBlocksToColorBuffer(int row);

        /// <summary>
        /// Clears spectral buffers.
        /// </summary>
        /// <remarks>
        /// Should only be called during baseline interleaved decoding.
        /// </remarks>
        public void ClearSpectralBuffers()
        {
            Buffer2D<Block8x8> spectralBlocks = this.Component.SpectralBlocks;
            for (int i = 0; i < spectralBlocks.Height; i++)
            {
                spectralBlocks.DangerousGetRowSpan(i).Clear();
            }
        }

        /// <summary>
        /// Gets converted color buffer row.
        /// </summary>
        /// <param name="row">Row index.</param>
        /// <returns>Color buffer row.</returns>
        public Span<float> GetColorBufferRowSpan(int row) =>
            this.ColorBuffer.DangerousGetRowSpan(row);

        public void Dispose() => this.ColorBuffer.Dispose();
    }
}
