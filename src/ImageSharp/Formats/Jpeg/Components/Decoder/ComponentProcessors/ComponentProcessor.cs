// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder
{
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

        public abstract void CopyBlocksToColorBuffer(int spectralStep);

        public void ClearSpectralBuffers()
        {
            Buffer2D<Block8x8> spectralBlocks = this.Component.SpectralBlocks;
            for (int i = 0; i < spectralBlocks.Height; i++)
            {
                spectralBlocks.DangerousGetRowSpan(i).Clear();
            }
        }

        public Span<float> GetColorBufferRowSpan(int row) =>
            this.ColorBuffer.DangerousGetRowSpan(row);

        public void Dispose() => this.ColorBuffer.Dispose();
    }
}
