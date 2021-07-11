// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Numerics;
using System.Threading;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using JpegColorConverter = SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder.ColorConverters.JpegColorConverter;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder
{
    /// <summary>
    /// Encapsulates the execution od post-processing algorithms to be applied on a <see cref="IRawJpegData"/> to produce a valid <see cref="Image{TPixel}"/>: <br/>
    /// (1) Dequantization <br/>
    /// (2) IDCT <br/>
    /// (3) Color conversion form one of the <see cref="JpegColorSpace"/>-s into a <see cref="Vector4"/> buffer of RGBA values <br/>
    /// (4) Packing <see cref="Image{TPixel}"/> pixels from the <see cref="Vector4"/> buffer. <br/>
    /// These operations are executed in <see cref="NumberOfPostProcessorSteps"/> steps.
    /// <see cref="pixelRowsPerStep"/> image rows are converted in one step,
    /// which means that size of the allocated memory is limited (does not depend on <see cref="ImageFrame.Height"/>).
    /// </summary>
    internal class JpegImagePostProcessor : IDisposable
    {
        private readonly Configuration configuration;

        /// <summary>
        /// The number of block rows to be processed in one Step.
        /// </summary>
        private readonly int blockRowsPerStep;

        /// <summary>
        /// The number of image pixel rows to be processed in one step.
        /// </summary>
        private readonly int pixelRowsPerStep;

        /// <summary>
        /// Temporal buffer to store a row of colors.
        /// </summary>
        private readonly IMemoryOwner<Vector4> rgbaBuffer;

        /// <summary>
        /// The <see cref="JpegColorConverter"/> corresponding to the current <see cref="JpegColorSpace"/> determined by <see cref="IRawJpegData.ColorSpace"/>.
        /// </summary>
        private readonly JpegColorConverter colorConverter;

        /// <summary>
        /// Initializes a new instance of the <see cref="JpegImagePostProcessor"/> class.
        /// </summary>
        /// <param name="configuration">The <see cref="Configuration"/> to configure internal operations.</param>
        /// <param name="rawJpeg">The <see cref="IRawJpegData"/> representing the uncompressed spectral Jpeg data</param>
        public JpegImagePostProcessor(Configuration configuration, IRawJpegData rawJpeg)
        {
            this.configuration = configuration;
            this.RawJpeg = rawJpeg;
            IJpegComponent c0 = rawJpeg.Components[0];

            this.blockRowsPerStep = c0.SamplingFactors.Height;
            this.pixelRowsPerStep = this.blockRowsPerStep * 8;

            this.NumberOfPostProcessorSteps = c0.SizeInBlocks.Height / this.blockRowsPerStep;

            var postProcessorBufferSize = new Size(c0.SizeInBlocks.Width * 8, this.pixelRowsPerStep);
            MemoryAllocator memoryAllocator = configuration.MemoryAllocator;
            this.ComponentProcessors = new JpegComponentPostProcessor[rawJpeg.Components.Length];
            for (int i = 0; i < rawJpeg.Components.Length; i++)
            {
                this.ComponentProcessors[i] = new JpegComponentPostProcessor(memoryAllocator, this.RawJpeg, postProcessorBufferSize, rawJpeg.Components[i]);
            }

            this.rgbaBuffer = memoryAllocator.Allocate<Vector4>(rawJpeg.ImageSizeInPixels.Width);
            this.colorConverter = JpegColorConverter.GetConverter(rawJpeg.ColorSpace, rawJpeg.Precision);
        }

        /// <summary>
        /// Gets the <see cref="JpegComponentPostProcessor"/> instances.
        /// </summary>
        public JpegComponentPostProcessor[] ComponentProcessors { get; }

        /// <summary>
        /// Gets the <see cref="IRawJpegData"/> to be processed.
        /// </summary>
        public IRawJpegData RawJpeg { get; }

        /// <summary>
        /// Gets the total number of post processor steps deduced from the height of the image and <see cref="pixelRowsPerStep"/>.
        /// </summary>
        public int NumberOfPostProcessorSteps { get; }

        /// <summary>
        /// Gets the value of the counter that grows by each step by <see cref="pixelRowsPerStep"/>.
        /// </summary>
        public int PixelRowCounter { get; private set; }

        /// <inheritdoc />
        public void Dispose()
        {
            foreach (JpegComponentPostProcessor cpp in this.ComponentProcessors)
            {
                cpp.Dispose();
            }

            this.rgbaBuffer.Dispose();
        }

        /// <summary>
        /// Process all pixels into 'destination'. The image dimensions should match <see cref="RawJpeg"/>.
        /// </summary>
        /// <typeparam name="TPixel">The pixel type</typeparam>
        /// <param name="destination">The destination image</param>
        /// <param name="cancellationToken">The token to request cancellation.</param>
        public void PostProcess<TPixel>(ImageFrame<TPixel> destination, CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            this.PixelRowCounter = 0;

            if (this.RawJpeg.ImageSizeInPixels != destination.Size())
            {
                throw new ArgumentException("Input image is not of the size of the processed one!");
            }

            while (this.PixelRowCounter < this.RawJpeg.ImageSizeInPixels.Height)
            {
                cancellationToken.ThrowIfCancellationRequested();
                this.DoPostProcessorStep(destination);
            }
        }

        /// <summary>
        /// Execute one step processing <see cref="pixelRowsPerStep"/> pixel rows into 'destination'.
        /// Convert and copy <see cref="pixelRowsPerStep"/> row of colors into 'destination' starting at row <see cref="PixelRowCounter"/>.
        /// </summary>
        /// <typeparam name="TPixel">The pixel type</typeparam>
        /// <param name="destination">The destination image</param>
        public void DoPostProcessorStep<TPixel>(ImageFrame<TPixel> destination)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            int maxY = Math.Min(destination.Height, this.PixelRowCounter + this.pixelRowsPerStep);

            var buffers = new Buffer2D<float>[this.ComponentProcessors.Length];
            for (int i = 0; i < this.ComponentProcessors.Length; i++)
            {
                this.ComponentProcessors[i].CopyBlocksToColorBuffer();
                buffers[i] = this.ComponentProcessors[i].ColorBuffer;
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

            this.PixelRowCounter += this.pixelRowsPerStep;
        }
    }
}
