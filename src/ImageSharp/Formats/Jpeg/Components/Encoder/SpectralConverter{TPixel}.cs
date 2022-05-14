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

        private Buffer2D<TPixel> pixelBuffer;

        private IMemoryOwner<float> redLane;

        private IMemoryOwner<float> greenLane;

        private IMemoryOwner<float> blueLane;

        private int alignedPixelWidth;

        private JpegColorConverterBase colorConverter;

        public SpectralConverter(JpegFrame frame, Image<TPixel> image, Block8x8F[] dequantTables, Configuration configuration)
        {
            this.configuration = configuration;

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

            this.redLane = allocator.Allocate<float>(this.alignedPixelWidth);
            this.greenLane = allocator.Allocate<float>(this.alignedPixelWidth);
            this.blueLane = allocator.Allocate<float>(this.alignedPixelWidth);

            // color converter from Rgb24 to YCbCr
            this.colorConverter = JpegColorConverterBase.GetConverter(colorSpace: frame.ColorSpace, precision: 8);
        }

        public void ConvertStrideBaseline()
        {
            // Convert next pixel stride using single spectral `stride'
            // Note that zero passing eliminates the need of virtual call
            // from JpegComponentPostProcessor
            this.ConvertStride(spectralStep: 0);
        }

        public void ConvertFull()
        {
            int steps = (int)Numerics.DivideCeil((uint)this.pixelBuffer.Height, (uint)this.pixelRowsPerStep);
            for (int i = 0; i < steps; i++)
            {
                this.ConvertStride(i);
            }
        }

        private void ConvertStride(int spectralStep)
        {
            // 1. Unpack from TPixel to r/g/b planes
            // 2. Byte r/g/b planes to normalized float r/g/b planes
            // 3. Convert from r/g/b planes to target pixel type with JpegColorConverter
            // 4. Convert color buffer to spectral blocks with component post processors
            int maxY = Math.Min(this.pixelBuffer.Height, this.pixelRowCounter + this.pixelRowsPerStep);

            Span<float> rLane = this.redLane.GetSpan();
            Span<float> gLane = this.greenLane.GetSpan();
            Span<float> bLane = this.blueLane.GetSpan();
            for (int yy = this.pixelRowCounter; yy < maxY; yy++)
            {
                int y = yy - this.pixelRowCounter;

                // unpack TPixel to r/g/b planes
                Span<TPixel> sourceRow = this.pixelBuffer.DangerousGetRowSpan(yy);

                PixelOperations<TPixel>.Instance.UnpackIntoRgbPlanes(this.configuration, rLane, gLane, bLane, sourceRow);

                var values = new JpegColorConverterBase.ComponentValues(this.componentProcessors, y);
                this.colorConverter.ConvertFromRgbInplace(values, rLane, gLane, bLane);
            }

            for (int i = 0; i < this.componentProcessors.Length; i++)
            {
                this.componentProcessors[i].CopyColorBufferToBlocks(spectralStep);
            }

            this.pixelRowCounter += this.pixelRowsPerStep;
        }
    }
}
