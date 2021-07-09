// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder.ColorConverters;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder
{
    internal abstract class SpectralConverter
    {
        public abstract void ConvertStride();
    }

    internal class SpectralConverter<TPixel> : SpectralConverter
        where TPixel : unmanaged, IPixel<TPixel>
    {
        private Configuration configuration;

        private JpegComponentPostProcessor[] componentProcessors;

        private JpegColorConverter colorConverter;

        private IMemoryOwner<Vector4> rgbaBuffer;

        private Buffer2D<TPixel> pixelBuffer;

        public JpegFrame Frame
        {
            set => this.InjectFrame(value);
        }

        public SpectralConverter(Configuration configuration)
        {
            this.configuration = configuration;
        }

        private void InjectFrame(JpegFrame frame)
        {
            MemoryAllocator allocator = this.configuration.MemoryAllocator;

            // pixel buffer for resulting image
            this.pixelBuffer = allocator.Allocate2D<TPixel>(frame.PixelWidth, frame.PixelHeight, AllocationOptions.Clean);

            // component processors from spectral to Rgba32
            this.componentProcessors = new JpegComponentPostProcessor[frame.Components.Length];
            for (int i = 0; i < this.componentProcessors.Length; i++)
            {
                this.componentProcessors[i] = new JpegComponentPostProcessor(allocator, this, frame.Components[i]);
            }

            // single 'stride' rgba32 buffer for conversion between spectral and TPixel
            this.rgbaBuffer = allocator.Allocate<Vector4>(frame.PixelWidth);

            // color converter from Rgba32 to TPixel
            this.colorConverter = JpegColorConverter.GetConverter(rawJpeg.ColorSpace, frame.Precision);
        }

        public override void ConvertStride()
        {
            int maxY = Math.Min(this.pixelBuffer.Height, this.PixelRowCounter + PixelRowsPerStep);

            var buffers = new Buffer2D<float>[this.componentProcessors.Length];
            for (int i = 0; i < this.componentProcessors.Length; i++)
            {
                this.componentProcessors[i].CopyBlocksToColorBuffer();
                buffers[i] = this.componentProcessors[i].ColorBuffer;
            }

            for (int yy = this.PixelRowCounter; yy < maxY; yy++)
            {
                int y = yy - this.PixelRowCounter;

                var values = new JpegColorConverter.ComponentValues(buffers, y);
                this.colorConverter.ConvertToRgba(values, this.rgbaBuffer.GetSpan());

                Span<TPixel> destRow = destination.GetPixelRowSpan(yy);

                // TODO: Investigate if slicing is actually necessary
                PixelOperations<TPixel>.Instance.FromVector4Destructive(this.configuration, this.rgbaBuffer.GetSpan().Slice(0, destRow.Length), destRow);
            }

            this.PixelRowCounter += PixelRowsPerStep;
        }
    }
}
