// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Linq;
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

        // private IMemoryOwner<Vector4> rgbaBuffer;
        private IMemoryOwner<byte> rgbBuffer;

        private IMemoryOwner<TPixel> paddedProxyPixelRow;

        private Buffer2D<TPixel> pixelBuffer;

        private int pixelRowsPerStep;

        private int pixelRowCounter;

        public SpectralConverter(Configuration configuration, CancellationToken cancellationToken)
        {
            this.configuration = configuration;
            this.cancellationToken = cancellationToken;
        }

        public Buffer2D<TPixel> GetPixelBuffer()
        {
            if (!this.Converted)
            {
                int steps = (int)Math.Ceiling(this.pixelBuffer.Height / (float)this.pixelRowsPerStep);

                for (int step = 0; step < steps; step++)
                {
                    this.cancellationToken.ThrowIfCancellationRequested();
                    this.ConvertStride(step);
                }
            }

            return this.pixelBuffer;
        }

        /// <inheritdoc/>
        public override void InjectFrameData(JpegFrame frame, IRawJpegData jpegData)
        {
            MemoryAllocator allocator = this.configuration.MemoryAllocator;

            // iteration data
            int majorBlockWidth = frame.Components.Max((component) => component.SizeInBlocks.Width);
            int majorVerticalSamplingFactor = frame.Components.Max((component) => component.SamplingFactors.Height);

            const int blockPixelHeight = 8;
            this.pixelRowsPerStep = majorVerticalSamplingFactor * blockPixelHeight;

            // pixel buffer for resulting image
            this.pixelBuffer = allocator.Allocate2D<TPixel>(frame.PixelWidth, frame.PixelHeight);
            this.paddedProxyPixelRow = allocator.Allocate<TPixel>(frame.PixelWidth + 3);

            // component processors from spectral to Rgba32
            const int blockPixelWidth = 8;
            var postProcessorBufferSize = new Size(majorBlockWidth * blockPixelWidth, this.pixelRowsPerStep);
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
            this.ConvertStride(spectralStep: 0);

            foreach (JpegComponentPostProcessor cpp in this.componentProcessors)
            {
                cpp.ClearSpectralBuffers();
            }
        }

        /// <inheritdoc/>
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

        private void ConvertStride(int spectralStep)
        {
            int maxY = Math.Min(this.pixelBuffer.Height, this.pixelRowCounter + this.pixelRowsPerStep);

            var buffers = new Buffer2D<float>[this.componentProcessors.Length];
            for (int i = 0; i < this.componentProcessors.Length; i++)
            {
                this.componentProcessors[i].CopyBlocksToColorBuffer(spectralStep);
                buffers[i] = this.componentProcessors[i].ColorBuffer;
            }

            int width = this.pixelBuffer.Width;

            for (int yy = this.pixelRowCounter; yy < maxY; yy++)
            {
                int y = yy - this.pixelRowCounter;

                var values = new JpegColorConverter.ComponentValues(buffers, y);

                this.colorConverter.ConvertToRgbInplace(values);
                values = values.Slice(0, width); // slice away Jpeg padding

                Span<byte> r = this.rgbBuffer.Slice(0, width);
                Span<byte> g = this.rgbBuffer.Slice(width, width);
                Span<byte> b = this.rgbBuffer.Slice(width * 2, width);

                SimdUtils.NormalizedFloatToByteSaturate(values.Component0, r);
                SimdUtils.NormalizedFloatToByteSaturate(values.Component1, g);
                SimdUtils.NormalizedFloatToByteSaturate(values.Component2, b);

                // PackFromRgbPlanes expects the destination to be padded, so try to get padded span containing extra elements from the next row.
                // If we can't get such a padded row because we are on a MemoryGroup boundary or at the last row,
                // pack pixels to a temporary, padded proxy buffer, then copy the relevant values to the destination row.
                if (this.pixelBuffer.TryGetPaddedRowSpan(yy, 3, out Span<TPixel> destRow))
                {
                    PixelOperations<TPixel>.Instance.PackFromRgbPlanes(this.configuration, r, g, b, destRow);
                }
                else
                {
                    Span<TPixel> proxyRow = this.paddedProxyPixelRow.GetSpan();
                    PixelOperations<TPixel>.Instance.PackFromRgbPlanes(this.configuration, r, g, b, proxyRow);
                    proxyRow.Slice(0, width).CopyTo(this.pixelBuffer.DangerousGetRowSpan(yy));
                }
            }

            this.pixelRowCounter += this.pixelRowsPerStep;
        }
    }
}
