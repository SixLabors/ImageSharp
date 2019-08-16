// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;

using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.ParallelUtils;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Memory;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms
{
    /// <summary>
    /// Implements resizing of images using various resamplers.
    /// </summary>
    /// <remarks>
    /// The original code has been adapted from <see href="http://www.realtimerendering.com/resources/GraphicsGems/gemsiii/filter_rcg.c"/>.
    /// </remarks>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class ResizeProcessor<TPixel> : TransformProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        // The following fields are not immutable but are optionally created on demand.
        private ResizeKernelMap horizontalKernelMap;
        private ResizeKernelMap verticalKernelMap;

        private readonly ResizeProcessor parameterSource;

        public ResizeProcessor(ResizeProcessor parameterSource)
        {
            this.parameterSource = parameterSource;
        }

        /// <summary>
        /// Gets the sampler to perform the resize operation.
        /// </summary>
        public IResampler Sampler => this.parameterSource.Sampler;

        /// <summary>
        /// Gets the target width.
        /// </summary>
        public int Width => this.parameterSource.Width;

        /// <summary>
        /// Gets the target height.
        /// </summary>
        public int Height => this.parameterSource.Height;

        /// <summary>
        /// Gets the resize rectangle.
        /// </summary>
        public Rectangle TargetRectangle => this.parameterSource.TargetRectangle;

        /// <summary>
        /// Gets a value indicating whether to compress or expand individual pixel color values on processing.
        /// </summary>
        public bool Compand => this.parameterSource.Compand;

        /// <summary>
        /// This is a shim for tagging the CreateDestination virtual generic method for the AoT iOS compiler.
        /// This method should never be referenced outside of the AotCompiler code.
        /// </summary>
        /// <param name="source">Passed through as source to the CreateDestination method.</param>
        /// <param name="sourceRectangle">Passed through as sourceRectangle to the CreateDestination method.</param>
        /// <returns>The result returned from CreateDestination.</returns>
        internal Image<TPixel> AotCreateDestination(Image<TPixel> source, Rectangle sourceRectangle) => this.CreateDestination(source, sourceRectangle);

        /// <inheritdoc/>
        protected override Image<TPixel> CreateDestination(Image<TPixel> source, Rectangle sourceRectangle)
        {
            // We will always be creating the clone even for mutate because we may need to resize the canvas
            IEnumerable<ImageFrame<TPixel>> frames = source.Frames.Select<ImageFrame<TPixel>, ImageFrame<TPixel>>(
                x => new ImageFrame<TPixel>(
                    source.GetConfiguration(),
                    this.Width,
                    this.Height,
                    x.Metadata.DeepClone()));

            // Use the overload to prevent an extra frame being added
            return new Image<TPixel>(source.GetConfiguration(), source.Metadata.DeepClone(), frames);
        }

        /// <inheritdoc/>
        protected override void BeforeImageApply(Image<TPixel> source, Image<TPixel> destination, Rectangle sourceRectangle)
        {
            if (!(this.Sampler is NearestNeighborResampler))
            {
                // Since all image frame dimensions have to be the same we can calculate this for all frames.
                MemoryAllocator memoryAllocator = source.GetMemoryAllocator();
                this.horizontalKernelMap = ResizeKernelMap.Calculate(
                    this.Sampler,
                    this.TargetRectangle.Width,
                    sourceRectangle.Width,
                    memoryAllocator);

                this.verticalKernelMap = ResizeKernelMap.Calculate(
                    this.Sampler,
                    this.TargetRectangle.Height,
                    sourceRectangle.Height,
                    memoryAllocator);
            }
        }

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source, ImageFrame<TPixel> destination, Rectangle sourceRectangle, Configuration configuration)
        {
            // Handle resize dimensions identical to the original
            if (source.Width == destination.Width && source.Height == destination.Height && sourceRectangle == this.TargetRectangle)
            {
                // The cloned will be blank here copy all the pixel data over
                source.GetPixelSpan().CopyTo(destination.GetPixelSpan());
                return;
            }

            int width = this.Width;
            int height = this.Height;
            int sourceX = sourceRectangle.X;
            int sourceY = sourceRectangle.Y;
            int startY = this.TargetRectangle.Y;
            int startX = this.TargetRectangle.X;

            var targetWorkingRect = Rectangle.Intersect(
                this.TargetRectangle,
                new Rectangle(0, 0, width, height));

            if (this.Sampler is NearestNeighborResampler)
            {
                // Scaling factors
                float widthFactor = sourceRectangle.Width / (float)this.TargetRectangle.Width;
                float heightFactor = sourceRectangle.Height / (float)this.TargetRectangle.Height;

                ParallelHelper.IterateRows(
                    targetWorkingRect,
                    configuration,
                    rows =>
                    {
                        for (int y = rows.Min; y < rows.Max; y++)
                        {
                            // Y coordinates of source points
                            Span<TPixel> sourceRow =
                                source.GetPixelRowSpan((int)(((y - startY) * heightFactor) + sourceY));
                            Span<TPixel> targetRow = destination.GetPixelRowSpan(y);

                            for (int x = targetWorkingRect.Left; x < targetWorkingRect.Right; x++)
                            {
                                // X coordinates of source points
                                targetRow[x] = sourceRow[(int)(((x - startX) * widthFactor) + sourceX)];
                            }
                        }
                    });

                return;
            }

            PixelConversionModifiers conversionModifiers =
                PixelConversionModifiers.Premultiply.ApplyCompanding(this.Compand);

            BufferArea<TPixel> sourceArea = source.PixelBuffer.GetArea(sourceRectangle);

            // To reintroduce parallel processing, we to launch multiple workers
            // for different row intervals of the image.
            using (var worker = new ResizeWorker<TPixel>(
                configuration,
                sourceArea,
                conversionModifiers,
                this.horizontalKernelMap,
                this.verticalKernelMap,
                width,
                targetWorkingRect,
                this.TargetRectangle.Location))
            {
                worker.Initialize();

                var workingInterval = new RowInterval(targetWorkingRect.Top, targetWorkingRect.Bottom);
                worker.FillDestinationPixels(workingInterval, destination.PixelBuffer);
            }
        }

        protected override void AfterImageApply(Image<TPixel> source, Image<TPixel> destination, Rectangle sourceRectangle)
        {
            base.AfterImageApply(source, destination, sourceRectangle);

            // TODO: An exception in the processing chain can leave these buffers undisposed. We should consider making image processors IDisposable!
            this.horizontalKernelMap?.Dispose();
            this.horizontalKernelMap = null;
            this.verticalKernelMap?.Dispose();
            this.verticalKernelMap = null;
        }
    }
}
