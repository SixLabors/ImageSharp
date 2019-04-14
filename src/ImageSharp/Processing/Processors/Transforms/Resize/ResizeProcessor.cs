// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.ParallelUtils;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Memory;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms
{
    /// <summary>
    /// Provides methods that allow the resizing of images using various algorithms.
    /// Adapted from <see href="http://www.realtimerendering.com/resources/GraphicsGems/gemsiii/filter_rcg.c"/>
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class ResizeProcessor<TPixel> : TransformProcessorBase<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        // The following fields are not immutable but are optionally created on demand.
        private ResizeKernelMap horizontalKernelMap;
        private ResizeKernelMap verticalKernelMap;

        /// <summary>
        /// Initializes a new instance of the <see cref="ResizeProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="options">The resize options</param>
        /// <param name="sourceSize">The source image size</param>
        public ResizeProcessor(ResizeOptions options, Size sourceSize)
        {
            Guard.NotNull(options, nameof(options));
            Guard.NotNull(options.Sampler, nameof(options.Sampler));

            int targetWidth = options.Size.Width;
            int targetHeight = options.Size.Height;

            // Ensure size is populated across both dimensions.
            // These dimensions are used to calculate the final dimensions determined by the mode algorithm.
            // If only one of the incoming dimensions is 0, it will be modified here to maintain aspect ratio.
            // If it is not possible to keep aspect ratio, make sure at least the minimum is is kept.
            const int min = 1;
            if (targetWidth == 0 && targetHeight > 0)
            {
                targetWidth = (int)MathF.Max(min, MathF.Round(sourceSize.Width * targetHeight / (float)sourceSize.Height));
            }

            if (targetHeight == 0 && targetWidth > 0)
            {
                targetHeight = (int)MathF.Max(min, MathF.Round(sourceSize.Height * targetWidth / (float)sourceSize.Width));
            }

            Guard.MustBeGreaterThan(targetWidth, 0, nameof(targetWidth));
            Guard.MustBeGreaterThan(targetHeight, 0, nameof(targetHeight));

            (Size size, Rectangle rectangle) = ResizeHelper.CalculateTargetLocationAndBounds(sourceSize, options, targetWidth, targetHeight);

            this.Sampler = options.Sampler;
            this.Width = size.Width;
            this.Height = size.Height;
            this.TargetRectangle = rectangle;
            this.Compand = options.Compand;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ResizeProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="sampler">The sampler to perform the resize operation.</param>
        /// <param name="width">The target width.</param>
        /// <param name="height">The target height.</param>
        /// <param name="sourceSize">The source image size</param>
        public ResizeProcessor(IResampler sampler, int width, int height, Size sourceSize)
            : this(sampler, width, height, sourceSize, new Rectangle(0, 0, width, height), false)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ResizeProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="sampler">The sampler to perform the resize operation.</param>
        /// <param name="width">The target width.</param>
        /// <param name="height">The target height.</param>
        /// <param name="sourceSize">The source image size</param>
        /// <param name="targetRectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the target image object to draw to.
        /// </param>
        /// <param name="compand">Whether to compress or expand individual pixel color values on processing.</param>
        public ResizeProcessor(IResampler sampler, int width, int height, Size sourceSize, Rectangle targetRectangle, bool compand)
        {
            Guard.NotNull(sampler, nameof(sampler));

            // Ensure size is populated across both dimensions.
            // If only one of the incoming dimensions is 0, it will be modified here to maintain aspect ratio.
            // If it is not possible to keep aspect ratio, make sure at least the minimum is is kept.
            const int min = 1;
            if (width == 0 && height > 0)
            {
                width = (int)MathF.Max(min, MathF.Round(sourceSize.Width * height / (float)sourceSize.Height));
                targetRectangle.Width = width;
            }

            if (height == 0 && width > 0)
            {
                height = (int)MathF.Max(min, MathF.Round(sourceSize.Height * width / (float)sourceSize.Width));
                targetRectangle.Height = height;
            }

            Guard.MustBeGreaterThan(width, 0, nameof(width));
            Guard.MustBeGreaterThan(height, 0, nameof(height));

            this.Sampler = sampler;
            this.Width = width;
            this.Height = height;
            this.TargetRectangle = targetRectangle;
            this.Compand = compand;
        }

        /// <summary>
        /// Gets the sampler to perform the resize operation.
        /// </summary>
        public IResampler Sampler { get; }

        /// <summary>
        /// Gets the target width.
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// Gets the target height.
        /// </summary>
        public int Height { get; }

        /// <summary>
        /// Gets the resize rectangle.
        /// </summary>
        public Rectangle TargetRectangle { get; }

        /// <summary>
        /// Gets a value indicating whether to compress or expand individual pixel color values on processing.
        /// </summary>
        public bool Compand { get; }

        /// <inheritdoc/>
        protected override Image<TPixel> CreateDestination(Image<TPixel> source, Rectangle sourceRectangle)
        {
            // We will always be creating the clone even for mutate because we may need to resize the canvas
            IEnumerable<ImageFrame<TPixel>> frames = source.Frames.Select(x => new ImageFrame<TPixel>(source.GetConfiguration(), this.Width, this.Height, x.Metadata.DeepClone()));

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

            var destWorkingRect = Rectangle.Intersect(
                this.TargetRectangle,
                new Rectangle(0, 0, width, height));

            if (this.Sampler is NearestNeighborResampler)
            {
                // Scaling factors
                float widthFactor = sourceRectangle.Width / (float)this.TargetRectangle.Width;
                float heightFactor = sourceRectangle.Height / (float)this.TargetRectangle.Height;

                ParallelHelper.IterateRows(
                    destWorkingRect,
                    configuration,
                    rows =>
                    {
                        for (int y = rows.Min; y < rows.Max; y++)
                        {
                            // Y coordinates of source points
                            Span<TPixel> sourceRow =
                                source.GetPixelRowSpan((int)(((y - startY) * heightFactor) + sourceY));
                            Span<TPixel> targetRow = destination.GetPixelRowSpan(y);

                            for (int x = destWorkingRect.Left; x < destWorkingRect.Right; x++)
                            {
                                // X coordinates of source points
                                targetRow[x] = sourceRow[(int)(((x - startX) * widthFactor) + sourceX)];
                            }
                        }
                    });

                return;
            }

            int sourceHeight = source.Height;

            PixelConversionModifiers conversionModifiers =
                PixelConversionModifiers.Premultiply.ApplyCompanding(this.Compand);

            BufferArea<TPixel> sourceArea = source.PixelBuffer.GetArea(sourceRectangle);

            // Interpolate the image using the calculated weights.
            // A 2-pass 1D algorithm appears to be faster than splitting a 1-pass 2D algorithm
            // First process the columns. Since we are not using multiple threads startY and endY
            // are the upper and lower bounds of the source rectangle.
            using (var resizeWindow = new ResizeWindow<TPixel>(
                configuration,
                sourceArea,
                conversionModifiers,
                this.horizontalKernelMap,
                this.verticalKernelMap,
                width,
                destWorkingRect,
                startX))
            using (IMemoryOwner<Vector4> tempBuffer = source.MemoryAllocator.Allocate<Vector4>(width))
            {
                resizeWindow.Initialize();

                // Now process the rows.
                Span<Vector4> tempColSpan = tempBuffer.GetSpan();

                for (int y = destWorkingRect.Top; y < destWorkingRect.Bottom; y++)
                {
                    // Ensure offsets are normalized for cropping and padding.
                    ResizeKernel kernel = this.verticalKernelMap.GetKernel(y - startY);

                    if (kernel.StartIndex + kernel.Length > resizeWindow.Bottom)
                    {
                        resizeWindow.Slide();
                    }

                    ref Vector4 tempRowBase = ref MemoryMarshal.GetReference(tempColSpan);

                    for (int x = 0; x < width; x++)
                    {
                        Span<Vector4> firstPassColumn = resizeWindow.GetColumnSpan(x, kernel.StartIndex);
                        ref Vector4 rowStartReference = ref MemoryMarshal.GetReference(firstPassColumn);

                        // Destination color components
                        Unsafe.Add(ref tempRowBase, x) = kernel.ConvolveCore(ref rowStartReference);
                    }

                    Span<TPixel> targetRowSpan = destination.GetPixelRowSpan(y);

                    PixelOperations<TPixel>.Instance.FromVector4Destructive(configuration, tempColSpan, targetRowSpan, conversionModifiers);
                }
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