// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Threading;
using SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder.ColorConverters;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder
{
    internal abstract class SpectralConverter : IDisposable
    {
        public abstract void CommitConversion();

        public abstract void InjectFrameData(JpegFrame frame, IRawJpegData jpegData);

        public abstract void ConvertStride(int step, int spectralStep);

        public abstract void ClearStride(int spectralStep);

        public abstract void Dispose();
    }

    internal class SpectralConverter<TPixel> : SpectralConverter
        where TPixel : unmanaged, IPixel<TPixel>
    {
        private Configuration configuration;

        private CancellationToken cancellationToken;


        private JpegComponentPostProcessor[] componentProcessors;

        private JpegColorConverter colorConverter;

        private IMemoryOwner<Vector4> rgbaBuffer;

        private Buffer2D<TPixel> pixelBuffer;



        public int BlockRowsPerStep;

        private int PixelRowsPerStep;


        private bool converted;

        public Buffer2D<TPixel> PixelBuffer
        {
            get
            {
                if (!this.converted)
                {
                    int steps = (int)Math.Ceiling(this.pixelBuffer.Height / (float)this.PixelRowsPerStep);

                    for (int i = 0; i < steps; i++)
                    {
                        this.cancellationToken.ThrowIfCancellationRequested();
                        this.ConvertStride(i, i);
                    }

                    this.converted = true;
                }

                return this.pixelBuffer;
            }
        }

        public SpectralConverter(Configuration configuration, CancellationToken ct)
        {
            this.configuration = configuration;
            this.cancellationToken = ct;
        }

        public override void CommitConversion() => this.converted = true;

        public override void InjectFrameData(JpegFrame frame, IRawJpegData jpegData)
        {
            MemoryAllocator allocator = this.configuration.MemoryAllocator;

            // iteration data
            IJpegComponent c0 = frame.Components[0];

            const int blockPixelHeight = 8;
            this.BlockRowsPerStep = c0.SamplingFactors.Height;
            this.PixelRowsPerStep = this.BlockRowsPerStep * blockPixelHeight;

            // pixel buffer for resulting image
            this.pixelBuffer = allocator.Allocate2D<TPixel>(frame.PixelWidth, frame.PixelHeight, AllocationOptions.Clean);

            // component processors from spectral to Rgba32
            var postProcessorBufferSize = new Size(c0.SizeInBlocks.Width * 8, this.PixelRowsPerStep);
            this.componentProcessors = new JpegComponentPostProcessor[frame.Components.Length];
            for (int i = 0; i < this.componentProcessors.Length; i++)
            {
                this.componentProcessors[i] = new JpegComponentPostProcessor(allocator, jpegData, postProcessorBufferSize, frame.Components[i]);
            }

            // single 'stride' rgba32 buffer for conversion between spectral and TPixel
            this.rgbaBuffer = allocator.Allocate<Vector4>(frame.PixelWidth);

            // color converter from Rgba32 to TPixel
            this.colorConverter = JpegColorConverter.GetConverter(jpegData.ColorSpace, frame.Precision);
        }

        public override void ConvertStride(int step, int spectralStep)
        {
            int pixelRowStart = this.PixelRowsPerStep * step;

            int maxY = Math.Min(this.pixelBuffer.Height, pixelRowStart + this.PixelRowsPerStep);

            var buffers = new Buffer2D<float>[this.componentProcessors.Length];
            for (int i = 0; i < this.componentProcessors.Length; i++)
            {
                this.componentProcessors[i].CopyBlocksToColorBuffer(spectralStep);
                buffers[i] = this.componentProcessors[i].ColorBuffer;
            }

            for (int yy = pixelRowStart; yy < maxY; yy++)
            {
                int y = yy - pixelRowStart;

                var values = new JpegColorConverter.ComponentValues(buffers, y);
                this.colorConverter.ConvertToRgba(values, this.rgbaBuffer.GetSpan());

                Span<TPixel> destRow = this.pixelBuffer.GetRowSpan(yy);

                // TODO: Investigate if slicing is actually necessary
                PixelOperations<TPixel>.Instance.FromVector4Destructive(this.configuration, this.rgbaBuffer.GetSpan().Slice(0, destRow.Length), destRow);
            }
        }

        public override void ClearStride(int spectralStep)
        {
            foreach (JpegComponentPostProcessor cpp in this.componentProcessors)
            {
                cpp.ClearSpectralStride(spectralStep);
            }
        }

        public override void Dispose()
        {
            foreach (JpegComponentPostProcessor cpp in this.componentProcessors)
            {
                cpp.Dispose();
            }

            this.rgbaBuffer.Dispose();
        }
    }
}
