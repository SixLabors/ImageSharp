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
    // TODO: docs
    internal class ResizingSpectralConverter<TPixel> : SpectralConverter<TPixel>, IDisposable
        where TPixel : unmanaged, IPixel<TPixel>
    {
        /// <summary>
        /// <see cref="Configuration"/> instance associated with current
        /// decoding routine.
        /// </summary>
        private readonly Configuration configuration;

        /// <summary>
        /// Target image size after scaled decoding.
        /// </summary>
        private readonly Size targetSize;

        /// <summary>
        /// Jpeg component converters from decompressed spectral to color data.
        /// </summary>
        private JpegComponentPostProcessor8[] componentProcessors;

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
        /// Intermediate buffer of RGB components used in color conversion.
        /// </summary>
        private IMemoryOwner<byte> rgbBuffer;

        /// <summary>
        /// Proxy buffer used in packing from RGB to target TPixel pixels.
        /// </summary>
        private IMemoryOwner<TPixel> paddedProxyPixelRow;

        /// <summary>
        /// Color converter from jpeg color space to target pixel color space.
        /// </summary>
        private JpegColorConverterBase colorConverter;

        public ResizingSpectralConverter(Configuration configuration, Size targetSize)
        {
            this.configuration = configuration;
            this.targetSize = targetSize;
        }

        public override void InjectFrameData(JpegFrame frame, IRawJpegData jpegData)
        {
            MemoryAllocator allocator = this.configuration.MemoryAllocator;

            (int width, int height, int scaleDenominator) = GetScaledImageDimensions(frame.PixelWidth, frame.PixelHeight, this.targetSize.Width, this.targetSize.Height);

            // iteration data
            int majorBlockWidth = frame.Components.Max((component) => component.SizeInBlocks.Width);
            int majorVerticalSamplingFactor = frame.Components.Max((component) => component.SamplingFactors.Height);

            this.pixelBuffer = allocator.Allocate2D<TPixel>(
                width,
                height,
                this.configuration.PreferContiguousImageBuffers);

            this.paddedProxyPixelRow = allocator.Allocate<TPixel>(width + 3);

            // single 'stride' rgba32 buffer for conversion between spectral and TPixel
            this.rgbBuffer = allocator.Allocate<byte>(width * 3);

            // component processors from spectral to Rgba32
            int blockPixelSize = 8 / scaleDenominator;
            this.pixelRowsPerStep = majorVerticalSamplingFactor * blockPixelSize;

            // color converter
            JpegColorConverterBase converter = this.GetColorConverter(frame, jpegData);
            this.colorConverter = converter;

            int bufferWidth = majorBlockWidth * blockPixelSize;
            int batchSize = converter.ElementsPerBatch;
            int correctedBufferWidth = bufferWidth + (batchSize - (bufferWidth % batchSize));
            var postProcessorBufferSize = new Size(correctedBufferWidth, this.pixelRowsPerStep);

            this.componentProcessors = new JpegComponentPostProcessor8[frame.Components.Length];
            for (int i = 0; i < this.componentProcessors.Length; i++)
            {
                this.componentProcessors[i] = new JpegComponentPostProcessor8(allocator, frame, jpegData, postProcessorBufferSize, frame.Components[i]);
            }
        }

        public override void ConvertStrideBaseline()
        {
            // Convert next pixel stride using single spectral `stride'
            // Note that zero passing eliminates the need of virtual call
            // from JpegComponentPostProcessor
            this.ConvertStride(spectralStep: 0);

            foreach (JpegComponentPostProcessor8 cpp in this.componentProcessors)
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

                SimdUtils.NormalizedFloatToByteSaturate(values.Component0.Slice(0, width), r);
                SimdUtils.NormalizedFloatToByteSaturate(values.Component1.Slice(0, width), g);
                SimdUtils.NormalizedFloatToByteSaturate(values.Component2.Slice(0, width), b);

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

        public override Buffer2D<TPixel> GetPixelBuffer(CancellationToken cancellationToken)
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

            return this.pixelBuffer;
        }

        public void Dispose()
        {
            if (this.componentProcessors != null)
            {
                foreach (JpegComponentPostProcessor8 cpp in this.componentProcessors)
                {
                    cpp.Dispose();
                }
            }

            this.rgbBuffer?.Dispose();
            this.paddedProxyPixelRow?.Dispose();
        }

        // TODO: docs, code formatting
        private static readonly (int Num, int Denom)[] ScalingFactors = new (int, int)[]
        {
            /* upscaling factors */
            // (16, 8),
            // (15, 8),
            // (14, 8),
            // (13, 8),
            // (12, 8),
            // (11, 8),
            // (10, 8),
            // (9,  8),

            /* no scaling */
            (8,  8),

            /* downscaling factors */
            // (7,  8),     // 8 => 7
            // (6,  8),     // 8 => 6
            // (5,  8),     // 8 => 5
            // (4,  8),     // 1/2 dct scaling - currently not supported
            // (3,  8),     // 8 => 3
            // (2,  8),     // 1/4 dct scaling - currently not supported
            (1,  8),        // 1/8 dct scaling
        };

        /// <summary>
        /// TODO: docs, code formatting
        /// </summary>
        /// <param name="iWidth">Initial image width.</param>
        /// <param name="iHeight">Initial image height.</param>
        /// <param name="tWidth">Target image width.</param>
        /// <param name="tHeight">Target image height.</param>
        private static (int Width, int Height, int ScaleDenominator) GetScaledImageDimensions(int iWidth, int iHeight, int tWidth, int tHeight)
        {
            int output_width = iWidth;
            int output_height = iHeight;
            int dct_scale = 8;

            for (int i = 1; i < ScalingFactors.Length; i++)
            {
                (int num, int denom) = ScalingFactors[i];
                int scaledw = (int)Numerics.DivideCeil((uint)(iWidth * num), (uint)denom);
                int scaledh = (int)Numerics.DivideCeil((uint)(iHeight * num), (uint)denom);

                if (scaledw < tWidth || scaledh < tHeight)
                {
                    dct_scale = 8 / ScalingFactors[i - 1].Num;
                    break;
                }

                output_width = scaledw;
                output_height = scaledh;
            }

            return (output_width, output_height, dct_scale);
        }
    }
}
