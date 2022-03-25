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
        /// Supported scaling factors for DCT jpeg scaling.
        /// </summary>
        private static readonly int[] ScalingFactors = new int[]
        {
            // 8 => 8, no scaling
            8,

            // 8 => 1, 1/8 of the original size
            1,
        };

        /// <summary>
        /// <see cref="Configuration"/> instance associated with current
        /// decoding routine.
        /// </summary>
        private readonly Configuration configuration;

        /// <summary>
        /// Jpeg component converters from decompressed spectral to color data.
        /// </summary>
        private ComponentProcessor[] componentProcessors;

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
        /// Represent target size after decoding for scaling decoding mode.
        /// </summary>
        /// <remarks>
        /// Null if no scaling is required.
        /// </remarks>
        private Size? targetSize;

        /// <summary>
        /// Initializes a new instance of the <see cref="SpectralConverter{TPixel}" /> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="targetSize">Optional target size for decoded image.</param>
        public SpectralConverter(Configuration configuration, Size? targetSize = null)
        {
            this.configuration = configuration;

            this.targetSize = targetSize;
        }

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

            return this.pixelBuffer;
        }

        /// <summary>
        /// Calculates resulting image size and jpeg block scaling.
        /// </summary>
        /// <param name="nativeSize">Native size of the image.</param>
        /// <param name="blockPixelSize">Resulting jpeg block pixel size.</param>
        /// <returns>Scaled jpeg image size.</returns>
        private Size GetResultingImageSize(Size nativeSize, out int blockPixelSize)
        {
            if (this.targetSize == null)
            {
                blockPixelSize = 8;
                return nativeSize;
            }
            else
            {
                const uint jpegBlockPixelSize = 8;

                Size targetSize = this.targetSize.Value;
                int outputWidth = nativeSize.Width;
                int outputHeight = nativeSize.Height;
                blockPixelSize = 1;

                for (int i = 1; i < ScalingFactors.Length; i++)
                {
                    int scale = ScalingFactors[i];
                    int scaledw = (int)Numerics.DivideCeil((uint)(nativeSize.Width * scale), jpegBlockPixelSize);
                    int scaledh = (int)Numerics.DivideCeil((uint)(nativeSize.Height * scale), jpegBlockPixelSize);

                    if (scaledw < targetSize.Width || scaledh < targetSize.Height)
                    {
                        blockPixelSize = ScalingFactors[i - 1];
                        break;
                    }

                    outputWidth = scaledw;
                    outputHeight = scaledh;
                }

                return new Size(outputWidth, outputHeight);
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
        public override void InjectFrameData(JpegFrame frame, IRawJpegData jpegData)
        {
            MemoryAllocator allocator = this.configuration.MemoryAllocator;

            // color converter from RGB to TPixel
            JpegColorConverterBase converter = this.GetColorConverter(frame, jpegData);
            this.colorConverter = converter;

            // Resulting image size
            Size pixelSize = this.GetResultingImageSize(frame.PixelSize, out int blockPixelSize);

            // iteration data
            int majorBlockWidth = frame.Components.Max((component) => component.SizeInBlocks.Width);
            int majorVerticalSamplingFactor = frame.Components.Max((component) => component.SamplingFactors.Height);

            this.pixelRowsPerStep = majorVerticalSamplingFactor * blockPixelSize;

            // pixel buffer for resulting image
            this.pixelBuffer = allocator.Allocate2D<TPixel>(
                pixelSize.Width,
                pixelSize.Height,
                this.configuration.PreferContiguousImageBuffers);
            this.paddedProxyPixelRow = allocator.Allocate<TPixel>(pixelSize.Width + 3);

            // component processors from spectral to RGB
            int bufferWidth = majorBlockWidth * blockPixelSize;
            int batchSize = converter.ElementsPerBatch;
            int converterAlignedBufferWidth = bufferWidth + (batchSize - (bufferWidth % batchSize));
            var postProcessorBufferSize = new Size(converterAlignedBufferWidth, this.pixelRowsPerStep);
            this.componentProcessors = new ComponentProcessor[frame.Components.Length];
            switch (blockPixelSize)
            {
                case 8:
                    for (int i = 0; i < this.componentProcessors.Length; i++)
                    {
                        this.componentProcessors[i] = new DirectComponentProcessor(allocator, frame, jpegData, postProcessorBufferSize, frame.Components[i]);
                    }

                    break;
                case 1:
                    for (int i = 0; i < this.componentProcessors.Length; i++)
                    {
                        this.componentProcessors[i] = new DownScalingComponentProcessor8(allocator, frame, jpegData, postProcessorBufferSize, frame.Components[i]);
                    }

                    break;

                // TODO: default?
            }

            // single 'stride' rgba32 buffer for conversion between spectral and TPixel
            this.rgbBuffer = allocator.Allocate<byte>(pixelSize.Width * 3);
        }

        /// <inheritdoc/>
        public override void ConvertStrideBaseline()
        {
            // Convert next pixel stride using single spectral `stride'
            // Note that zero passing eliminates extra virtual call
            this.ConvertStride(spectralStep: 0);

            foreach (ComponentProcessor cpp in this.componentProcessors)
            {
                cpp.ClearSpectralBuffers();
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (this.componentProcessors != null)
            {
                foreach (ComponentProcessor cpp in this.componentProcessors)
                {
                    cpp.Dispose();
                }
            }

            this.rgbBuffer?.Dispose();
            this.paddedProxyPixelRow?.Dispose();
        }
    }
}
