// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
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

        private IMemoryOwner<byte> rgbBuffer;

        private IMemoryOwner<TPixel> paddedProxyPixelRow;

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

        public Buffer2D<TPixel> GetPixelBuffer()
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
            this.pixelBuffer = allocator.Allocate2D<TPixel>(frame.PixelWidth, frame.PixelHeight);
            this.paddedProxyPixelRow = allocator.Allocate<TPixel>(frame.PixelWidth + 3);

            // component processors from spectral to Rgba32
            var postProcessorBufferSize = new Size(c0.SizeInBlocks.Width * 8, this.pixelRowsPerStep);
            this.componentProcessors = new JpegComponentPostProcessor[frame.Components.Length];
            for (int i = 0; i < this.componentProcessors.Length; i++)
            {
                this.componentProcessors[i] = new JpegComponentPostProcessor(allocator, frame, jpegData, postProcessorBufferSize, frame.Components[i]);
            }

            // single 'stride' rgba32 buffer for conversion between spectral and TPixel
            this.rgbBuffer = allocator.Allocate<byte>(frame.PixelWidth * 3);

            // color converter from Rgba32 to TPixel
            this.colorConverter = this.GetColorConverter(frame, jpegData);
        }

        /// <inheritdoc/>
        public override void ConvertStrideBaseline()
        {
            // Convert next pixel stride using single spectral `stride'
            // Note that zero passing eliminates the need of virtual call
            // from JpegComponentPostProcessor
            this.ConvertNextStride(mcuRow: 0);

            foreach (JpegComponentPostProcessor cpp in this.componentProcessors)
            {
                cpp.ClearSpectralBuffers();
            }
        }

        private void ConvertNextStride(int mcuRow)
        {
            // Convert spectral data to color data for each component
            for (int i = 0; i < this.componentProcessors.Length; i++)
            {
                this.componentProcessors[i].CopyBlocksToColorBuffer(mcuRow);
            }

            int width = this.pixelBuffer.Width;

            // This method converts pixel rows in MCU batches which height
            // is divisible by 8
            // Resulting image may not have height divisible by 8 which
            // can lead to index out of bounds exception
            // To prevent it we must take minimum of expected last index
            // and actual pixel buffer height
            int start = this.pixelRowCounter;
            int end = Math.Min(start + this.pixelRowsPerStep, this.pixelBuffer.Height);
            for (int imageRowIndex = start; imageRowIndex < end; imageRowIndex++)
            {
                // Convert from jpeg color space to rgb colorspace
                int colorBufferRowIndex = imageRowIndex - start;
                var values = new JpegColorConverter.ComponentValues(this.componentProcessors, colorBufferRowIndex);
                this.colorConverter.ConvertToRgbInplace(values);

                Span<byte> r = this.rgbBuffer.Slice(0, width);
                Span<byte> g = this.rgbBuffer.Slice(width, width);
                Span<byte> b = this.rgbBuffer.Slice(width * 2, width);

                SimdUtils.NormalizedFloatToByteSaturate(values.Component0, r);
                r = r.Slice(0, width);
                SimdUtils.NormalizedFloatToByteSaturate(values.Component1, g);
                g = g.Slice(0, width);
                SimdUtils.NormalizedFloatToByteSaturate(values.Component2, b);
                b = b.Slice(0, width);

                // PackFromRgbPlanes expects the destination to be padded, so try to get padded span containing extra elements from the next row.
                // If we can't get such a padded row because we are on a MemoryGroup boundary or at the last row,
                // pack pixels to a temporary, padded proxy buffer, then copy the relevant values to the destination row.
                if (this.pixelBuffer.TryGetPaddedRowSpan(imageRowIndex, 3, out Span<TPixel> destRow))
                {
                    PixelOperations<TPixel>.Instance.PackFromRgbPlanes(this.configuration, r, g, b, destRow);
                }
                else
                {
                    Span<TPixel> proxyRow = this.paddedProxyPixelRow.GetSpan();
                    PixelOperations<TPixel>.Instance.PackFromRgbPlanes(this.configuration, r, g, b, proxyRow);
                    proxyRow.Slice(0, width).CopyTo(this.pixelBuffer.GetRowSpan(imageRowIndex));
                }
            }

            this.pixelRowCounter += this.pixelRowsPerStep;
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

            this.rgbBuffer?.Dispose();
            this.paddedProxyPixelRow?.Dispose();
        }
    }
}
