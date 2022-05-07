// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Linq;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Encoder
{
    /// <inheritdoc/>
    internal class SpectralConverter<TPixel> : SpectralConverter
        where TPixel : unmanaged, IPixel<TPixel>
    {
        private readonly Configuration configuration;

        private JpegComponentPostProcessor[] componentProcessors;

        private int pixelRowsPerStep;

        private int pixelRowCounter;

        private IMemoryOwner<byte> rgbBuffer;

        private Buffer2D<TPixel> pixelBuffer;

        private int alignedPixelWidth;

        private Decoder.ColorConverters.JpegColorConverterBase colorConverter;

        public SpectralConverter(Configuration configuration) =>
            this.configuration = configuration;

        public void InjectFrameData(JpegFrame frame, Image<TPixel> image, Block8x8F[] dequantTables)
        {
            MemoryAllocator allocator = this.configuration.MemoryAllocator;

            // iteration data
            int majorBlockWidth = frame.Components.Max((component) => component.SizeInBlocks.Width);
            int majorVerticalSamplingFactor = frame.Components.Max((component) => component.SamplingFactors.Height);

            const int blockPixelHeight = 8;
            this.pixelRowsPerStep = majorVerticalSamplingFactor * blockPixelHeight;

            // pixel buffer of the image
            // currently codec only supports encoding single frame jpegs
            this.pixelBuffer = image.GetRootFramePixelBuffer();

            // component processors from spectral to Rgba32
            const int blockPixelWidth = 8;
            this.alignedPixelWidth = majorBlockWidth * blockPixelWidth;
            var postProcessorBufferSize = new Size(this.alignedPixelWidth, this.pixelRowsPerStep);
            this.componentProcessors = new JpegComponentPostProcessor[frame.Components.Length];
            for (int i = 0; i < this.componentProcessors.Length; i++)
            {
                JpegComponent component = frame.Components[i];
                this.componentProcessors[i] = new JpegComponentPostProcessor(allocator, component, postProcessorBufferSize, dequantTables[component.QuantizationTableIndex]);
            }

            // single 'stride' rgba32 buffer for conversion between spectral and TPixel
            this.rgbBuffer = allocator.Allocate<byte>(this.alignedPixelWidth * 3);

            // color converter from Rgb24 to YCbCr
            this.colorConverter = Decoder.ColorConverters.JpegColorConverterBase.GetConverter(colorSpace: frame.ColorSpace, precision: 8);
        }

        public void ConvertStrideBaseline()
        {
            // Convert next pixel stride using single spectral `stride'
            // Note that zero passing eliminates the need of virtual call
            // from JpegComponentPostProcessor
            this.ConvertStride(spectralStep: 0);
        }

        private void ConvertStride(int spectralStep)
        {
            // 1. Unpack from TPixel to r/g/b planes
            // 2. Byte r/g/b planes to normalized float r/g/b planes
            // 3. Convert from r/g/b planes to target pixel type with JpegColorConverter
            // 4. Convert color buffer to spectral blocks with component post processors
            int maxY = Math.Min(this.pixelBuffer.Height, this.pixelRowCounter + this.pixelRowsPerStep);

            int width = this.pixelBuffer.Width;

            Span<byte> r = this.rgbBuffer.Slice(0, this.alignedPixelWidth);
            Span<byte> g = this.rgbBuffer.Slice(this.alignedPixelWidth, this.alignedPixelWidth);
            Span<byte> b = this.rgbBuffer.Slice(this.alignedPixelWidth * 2, this.alignedPixelWidth);

            for (int yy = this.pixelRowCounter; yy < maxY; yy++)
            {
                int y = yy - this.pixelRowCounter;

                // unpack TPixel to r/g/b planes
                Span<TPixel> sourceRow = this.pixelBuffer.DangerousGetRowSpan(yy);
                PixelOperations<TPixel>.Instance.UnpackIntoRgbPlanes(this.configuration, r, g, b, sourceRow);

                var values = new Decoder.ColorConverters.JpegColorConverterBase.ComponentValues(this.componentProcessors, y);

                SimdUtils.ByteToNormalizedFloat(r, values.Component0);
                SimdUtils.ByteToNormalizedFloat(g, values.Component1);
                SimdUtils.ByteToNormalizedFloat(b, values.Component2);

                this.colorConverter.ConvertFromRgbInplace(values);
            }

            for (int i = 0; i < this.componentProcessors.Length; i++)
            {
                this.componentProcessors[i].CopyColorBufferToBlocks(spectralStep);
            }

            this.pixelRowCounter += this.pixelRowsPerStep;
        }
    }
}
