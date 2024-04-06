// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Linq;
using System.Threading;
using SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder.ColorConverters;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder
{
    /// <inheritdoc/>
    /// <remarks>
    /// Color decoding scheme:
    /// <list type = "number" >
    /// <listheader>
    ///     <item>Decode spectral data to Jpeg color space</item>
    ///     <item>Convert from Jpeg color space to RGB</item>
    ///     <item>Convert from RGB to target pixel space</item>
    /// </listheader>
    /// </list>
    /// </remarks>
    internal class SpectralConverter<TPixel> : SpectralConverter, IDisposable
        where TPixel : unmanaged, IPixel<TPixel>
    {
        /// <summary>
        /// <see cref="Configuration"/> instance associated with current
        /// decoding routine.
        /// </summary>
        private readonly Configuration configuration;

        /// <summary>
        /// Jpeg component converters from decompressed spectral to color data.
        /// </summary>
        private JpegComponentPostProcessor[] componentProcessors;

        /// <summary>
        /// Color converter from jpeg color space to target pixel color space.
        /// </summary>
        private JpegColorConverterBase colorConverter;

        /// <summary>
        /// Intermediate buffer of RGB components used in color conversion.
        /// </summary>
        private IMemoryOwner<byte> rgbBuffer;

        /// <summary>
        /// Proxy buffer used in packing from RGB to target TPixel pixels.
        /// </summary>
        private IMemoryOwner<TPixel> paddedProxyPixelRow;

        /// <summary>
        /// Resulting 2D pixel buffer.
        /// </summary>
        private Buffer2D<TPixel> pixelBuffer;

        /// <summary>
        /// How many pixel rows are processed in one 'stride'.
        /// </summary>
        private int pixelRowsPerStep;

        /// <summary>
        /// How many pixel rows were processed.
        /// </summary>
        private int pixelRowCounter;

        /// <summary>
        /// Initializes a new instance of the <see cref="SpectralConverter{TPixel}" /> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        public SpectralConverter(Configuration configuration) =>
            this.configuration = configuration;

        /// <summary>
        /// Gets converted pixel buffer.
        /// </summary>
        /// <remarks>
        /// For non-baseline interleaved jpeg this method does a 'lazy' spectral
        /// conversion from spectral to color.
        /// </remarks>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Pixel buffer.</returns>
        public Buffer2D<TPixel> GetPixelBuffer(CancellationToken cancellationToken)
        {
            if (!this.Converted)
            {
                int steps = (int)Math.Ceiling(this.pixelBuffer.Height / (float)this.pixelRowsPerStep);

                for (int step = 0; step < steps; step++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    this.ConvertStride(step);
                }
            }

            var buffer = this.pixelBuffer;
            this.pixelBuffer = null;
            return buffer;
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
            this.pixelBuffer = allocator.Allocate2D<TPixel>(
                frame.PixelWidth,
                frame.PixelHeight,
                this.configuration.PreferContiguousImageBuffers,
                AllocationOptions.Clean);
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

        /// <summary>
        /// Converts single spectral jpeg stride to color stride.
        /// </summary>
        /// <param name="spectralStep">Spectral stride index.</param>
        private void ConvertStride(int spectralStep)
        {
            int maxY = Math.Min(this.pixelBuffer.Height, this.pixelRowCounter + this.pixelRowsPerStep);

            for (int i = 0; i < this.componentProcessors.Length; i++)
            {
                this.componentProcessors[i].CopyBlocksToColorBuffer(spectralStep);
            }

            int width = this.pixelBuffer.Width;

            for (int yy = this.pixelRowCounter; yy < maxY; yy++)
            {
                int y = yy - this.pixelRowCounter;

                var values = new JpegColorConverterBase.ComponentValues(this.componentProcessors, y);

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
                if (this.pixelBuffer.DangerousTryGetPaddedRowSpan(yy, 3, out Span<TPixel> destRow))
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
            this.pixelBuffer?.Dispose();
        }
    }
}