// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Numerics;
using System.Threading;
using SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder.ColorConverters;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder
{
    internal class SpectralConverter<TPixel> : SpectralConverter, IDisposable
        where TPixel : unmanaged, IPixel<TPixel>
    {
        private readonly Configuration configuration;

        private readonly CancellationToken cancellationToken;

        private JpegComponentPostProcessor[] componentProcessors;

        private JpegColorConverter colorConverter;

        private IMemoryOwner<Vector4> rgbaBuffer;

        private Buffer2D<TPixel> pixelBuffer;

        private int blockRowsPerStep;

        private int pixelRowsPerStep;

        private int pixelRowCounter;

        public SpectralConverter(Configuration configuration, CancellationToken cancellationToken)
        {
            this.configuration = configuration;
            this.cancellationToken = cancellationToken;
        }

        private bool Converted => this.pixelRowCounter >= this.pixelBuffer.Height;

        public Buffer2D<TPixel> PixelBuffer
        {
            get
            {
                if (!this.Converted)
                {
                    int steps = (int)Math.Ceiling(this.pixelBuffer.Height / (float)this.pixelRowsPerStep);

                    for (int step = 0; step < steps; step++)
                    {
                        this.cancellationToken.ThrowIfCancellationRequested();
                        this.ConvertNextStride(step);
                    }
                }

                return this.pixelBuffer;
            }
        }

        /// <inheritdoc/>
        public override void InjectFrameData(JpegFrame frame, IRawJpegData jpegData)
        {
            MemoryAllocator allocator = this.configuration.MemoryAllocator;

            // iteration data
            IJpegComponent c0 = frame.Components[0];

            const int blockPixelHeight = 8;
            this.blockRowsPerStep = c0.SamplingFactors.Height;
            this.pixelRowsPerStep = this.blockRowsPerStep * blockPixelHeight;

            // pixel buffer for resulting image
            this.pixelBuffer = allocator.Allocate2D<TPixel>(frame.PixelWidth, frame.PixelHeight, AllocationOptions.Clean);

            // component processors from spectral to Rgba32
            var postProcessorBufferSize = new Size(c0.SizeInBlocks.Width * 8, this.pixelRowsPerStep);
            this.componentProcessors = new JpegComponentPostProcessor[frame.Components.Length];
            for (int i = 0; i < this.componentProcessors.Length; i++)
            {
                this.componentProcessors[i] = new JpegComponentPostProcessor(allocator, frame, jpegData, postProcessorBufferSize, frame.Components[i]);
            }

            // single 'stride' rgba32 buffer for conversion between spectral and TPixel
            this.rgbaBuffer = allocator.Allocate<Vector4>(frame.PixelWidth);

            // color converter from Rgba32 to TPixel
            this.colorConverter = this.GetColorConverter(frame, jpegData);
        }

        /// <inheritdoc/>
        public override void ConvertStrideBaseline()
        {
            // Convert next pixel stride using single spectral `stride'
            // Note that zero passing eliminates the need of virtual call from JpegComponentPostProcessor
            this.ConvertNextStride(spectralStep: 0);

            // Clear spectral stride - this is VERY important as jpeg possibly won't fill entire buffer each stride
            // Which leads to decoding artifacts
            // Note that this code clears all buffers of the post processors, it's their responsibility to allocate only single stride
            foreach (JpegComponentPostProcessor cpp in this.componentProcessors)
            {
                cpp.ClearSpectralBuffers();
            }
        }

        public void Dispose()
        {
            if (this.componentProcessors != null)
            {
                foreach (JpegComponentPostProcessor cpp in this.componentProcessors)
                {
                    cpp.Dispose();
                }
            }

            this.rgbaBuffer?.Dispose();
        }

        private void ConvertNextStride(int spectralStep)
        {
            int maxY = Math.Min(this.pixelBuffer.Height, this.pixelRowCounter + this.pixelRowsPerStep);

            var buffers = new Buffer2D<float>[this.componentProcessors.Length];
            for (int i = 0; i < this.componentProcessors.Length; i++)
            {
                this.componentProcessors[i].CopyBlocksToColorBuffer(spectralStep);
                buffers[i] = this.componentProcessors[i].ColorBuffer;
            }

            for (int yy = this.pixelRowCounter; yy < maxY; yy++)
            {
                int y = yy - this.pixelRowCounter;

                var values = new JpegColorConverter.ComponentValues(buffers, y);
                this.colorConverter.ConvertToRgba(values, this.rgbaBuffer.GetSpan());

                Span<TPixel> destRow = this.pixelBuffer.GetRowSpan(yy);

                // TODO: Investigate if slicing is actually necessary
                PixelOperations<TPixel>.Instance.FromVector4Destructive(this.configuration, this.rgbaBuffer.GetSpan().Slice(0, destRow.Length), destRow);
            }

            this.pixelRowCounter += this.pixelRowsPerStep;
        }
    }
}
