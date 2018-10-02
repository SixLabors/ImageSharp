// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
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
        private WeightsBuffer horizontalWeights;
        private WeightsBuffer verticalWeights;

        /// <summary>
        /// Initializes a new instance of the <see cref="ResizeProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="options">The resize options</param>
        /// <param name="sourceSize">The source image size</param>
        public ResizeProcessor(ResizeOptions options, Size sourceSize)
        {
            Guard.NotNull(options, nameof(options));
            Guard.NotNull(options.Sampler, nameof(options.Sampler));

            int tempWidth = options.Size.Width;
            int tempHeight = options.Size.Height;

            // Ensure size is populated across both dimensions.
            // These dimensions are used to calculate the final dimensions determined by the mode algorithm.
            if (tempWidth == 0 && tempHeight > 0)
            {
                tempWidth = (int)MathF.Round(sourceSize.Width * tempHeight / (float)sourceSize.Height);
            }

            if (tempHeight == 0 && tempWidth > 0)
            {
                tempHeight = (int)MathF.Round(sourceSize.Height * tempWidth / (float)sourceSize.Width);
            }

            Guard.MustBeGreaterThan(tempWidth, 0, nameof(tempWidth));
            Guard.MustBeGreaterThan(tempHeight, 0, nameof(tempHeight));

            (Size size, Rectangle rectangle) = ResizeHelper.CalculateTargetLocationAndBounds(sourceSize, options, tempWidth, tempHeight);

            this.Sampler = options.Sampler;
            this.Width = size.Width;
            this.Height = size.Height;
            this.ResizeRectangle = rectangle;
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
        /// <param name="resizeRectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the target image object to draw to.
        /// </param>
        /// <param name="compand">Whether to compress or expand individual pixel color values on processing.</param>
        public ResizeProcessor(IResampler sampler, int width, int height, Size sourceSize, Rectangle resizeRectangle, bool compand)
        {
            Guard.NotNull(sampler, nameof(sampler));

            // Ensure size is populated across both dimensions.
            if (width == 0 && height > 0)
            {
                width = (int)MathF.Round(sourceSize.Width * height / (float)sourceSize.Height);
                resizeRectangle.Width = width;
            }

            if (height == 0 && width > 0)
            {
                height = (int)MathF.Round(sourceSize.Height * width / (float)sourceSize.Width);
                resizeRectangle.Height = height;
            }

            Guard.MustBeGreaterThan(width, 0, nameof(width));
            Guard.MustBeGreaterThan(height, 0, nameof(height));

            this.Sampler = sampler;
            this.Width = width;
            this.Height = height;
            this.ResizeRectangle = resizeRectangle;
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
        public Rectangle ResizeRectangle { get; }

        /// <summary>
        /// Gets a value indicating whether to compress or expand individual pixel color values on processing.
        /// </summary>
        public bool Compand { get; }

        /// <summary>
        /// Computes the weights to apply at each pixel when resizing.
        /// </summary>
        /// <param name="memoryAllocator">The <see cref="MemoryAllocator"/> to use for buffer allocations</param>
        /// <param name="destinationSize">The destination size</param>
        /// <param name="sourceSize">The source size</param>
        /// <returns>The <see cref="WeightsBuffer"/></returns>
        // TODO: Made internal to simplify experimenting with weights data. Make it private when finished figuring out how to optimize all the stuff!
        internal WeightsBuffer PrecomputeWeights(MemoryAllocator memoryAllocator, int destinationSize, int sourceSize)
        {
            float ratio = (float)sourceSize / destinationSize;
            float scale = ratio;

            if (scale < 1F)
            {
                scale = 1F;
            }

            IResampler sampler = this.Sampler;
            float radius = MathF.Ceiling(scale * sampler.Radius);
            var result = new WeightsBuffer(memoryAllocator, sourceSize, destinationSize);

            for (int i = 0; i < destinationSize; i++)
            {
                float center = ((i + .5F) * ratio) - .5F;

                // Keep inside bounds.
                int left = (int)MathF.Ceiling(center - radius);
                if (left < 0)
                {
                    left = 0;
                }

                int right = (int)MathF.Floor(center + radius);
                if (right > sourceSize - 1)
                {
                    right = sourceSize - 1;
                }

                float sum = 0;

                WeightsWindow ws = result.GetWeightsWindow(i, left, right);
                result.Weights[i] = ws;

                ref float weightsBaseRef = ref ws.GetStartReference();

                for (int j = left; j <= right; j++)
                {
                    float weight = sampler.GetValue((j - center) / scale);
                    sum += weight;

                    // weights[j - left] = weight:
                    Unsafe.Add(ref weightsBaseRef, j - left) = weight;
                }

                // Normalize, best to do it here rather than in the pixel loop later on.
                if (sum > 0)
                {
                    for (int w = 0; w < ws.Length; w++)
                    {
                        // weights[w] = weights[w] / sum:
                        ref float wRef = ref Unsafe.Add(ref weightsBaseRef, w);
                        wRef /= sum;
                    }
                }
            }

            return result;
        }

        /// <inheritdoc/>
        protected override Image<TPixel> CreateDestination(Image<TPixel> source, Rectangle sourceRectangle)
        {
            // We will always be creating the clone even for mutate because we may need to resize the canvas
            IEnumerable<ImageFrame<TPixel>> frames = source.Frames.Select(x => new ImageFrame<TPixel>(source.GetConfiguration(), this.Width, this.Height, x.MetaData.DeepClone()));

            // Use the overload to prevent an extra frame being added
            return new Image<TPixel>(source.GetConfiguration(), source.MetaData.DeepClone(), frames);
        }

        /// <inheritdoc/>
        protected override void BeforeImageApply(Image<TPixel> source, Image<TPixel> destination, Rectangle sourceRectangle)
        {
            if (!(this.Sampler is NearestNeighborResampler))
            {
                // Since all image frame dimensions have to be the same we can calculate this for all frames.
                MemoryAllocator memoryAllocator = source.GetMemoryAllocator();
                this.horizontalWeights = this.PrecomputeWeights(
                    memoryAllocator,
                    this.ResizeRectangle.Width,
                    sourceRectangle.Width);

                this.verticalWeights = this.PrecomputeWeights(
                    memoryAllocator,
                    this.ResizeRectangle.Height,
                    sourceRectangle.Height);
            }
        }

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source, ImageFrame<TPixel> destination, Rectangle sourceRectangle, Configuration configuration)
        {
            // Handle resize dimensions identical to the original
            if (source.Width == destination.Width && source.Height == destination.Height && sourceRectangle == this.ResizeRectangle)
            {
                // The cloned will be blank here copy all the pixel data over
                source.GetPixelSpan().CopyTo(destination.GetPixelSpan());
                return;
            }

            int width = this.Width;
            int height = this.Height;
            int sourceX = sourceRectangle.X;
            int sourceY = sourceRectangle.Y;
            int startY = this.ResizeRectangle.Y;
            int endY = this.ResizeRectangle.Bottom;
            int startX = this.ResizeRectangle.X;
            int endX = this.ResizeRectangle.Right;

            int minX = Math.Max(0, startX);
            int maxX = Math.Min(width, endX);
            int minY = Math.Max(0, startY);
            int maxY = Math.Min(height, endY);

            if (this.Sampler is NearestNeighborResampler)
            {
                var workingRect = Rectangle.FromLTRB(minX, minY, maxX, maxY);

                // Scaling factors
                float widthFactor = sourceRectangle.Width / (float)this.ResizeRectangle.Width;
                float heightFactor = sourceRectangle.Height / (float)this.ResizeRectangle.Height;

                ParallelHelper.IterateRows(
                    workingRect,
                    configuration,
                    rows =>
                        {
                            for (int y = rows.Min; y < rows.Max; y++)
                            {
                                // Y coordinates of source points
                                Span<TPixel> sourceRow =
                                    source.GetPixelRowSpan((int)(((y - startY) * heightFactor) + sourceY));
                                Span<TPixel> targetRow = destination.GetPixelRowSpan(y);

                                for (int x = minX; x < maxX; x++)
                                {
                                    // X coordinates of source points
                                    targetRow[x] = sourceRow[(int)(((x - startX) * widthFactor) + sourceX)];
                                }
                            }
                        });

                return;
            }

            // Interpolate the image using the calculated weights.
            // A 2-pass 1D algorithm appears to be faster than splitting a 1-pass 2D algorithm
            // First process the columns. Since we are not using multiple threads startY and endY
            // are the upper and lower bounds of the source rectangle.
            // TODO: Using a transposed variant of 'firstPassPixels' could eliminate the need for the WeightsWindow.ComputeWeightedColumnSum() method, and improve speed!
            using (Buffer2D<Vector4> firstPassPixels = source.MemoryAllocator.Allocate2D<Vector4>(width, source.Height))
            {
                firstPassPixels.MemorySource.Clear();

                var processColsRect = new Rectangle(0, 0, source.Width, sourceRectangle.Bottom);

                ParallelHelper.IterateRowsWithTempBuffer<Vector4>(
                    processColsRect,
                    configuration,
                    (rows, tempRowBuffer) =>
                        {
                            for (int y = rows.Min; y < rows.Max; y++)
                            {
                                ref Vector4 firstPassRow =
                                    ref MemoryMarshal.GetReference(firstPassPixels.GetRowSpan(y));
                                Span<TPixel> sourceRow = source.GetPixelRowSpan(y);
                                Span<Vector4> tempRowSpan = tempRowBuffer.Span;

                                PixelOperations<TPixel>.Instance.ToVector4(sourceRow, tempRowSpan, sourceRow.Length);

                                if (this.Compand)
                                {
                                    for (int x = minX; x < maxX; x++)
                                    {
                                        WeightsWindow window = this.horizontalWeights.Weights[x - startX];
                                        Unsafe.Add(ref firstPassRow, x) =
                                            window.ComputeExpandedWeightedRowSum(tempRowSpan, sourceX);
                                    }
                                }
                                else
                                {
                                    for (int x = minX; x < maxX; x++)
                                    {
                                        WeightsWindow window = this.horizontalWeights.Weights[x - startX];
                                        Unsafe.Add(ref firstPassRow, x) =
                                            window.ComputeWeightedRowSum(tempRowSpan, sourceX);
                                    }
                                }
                            }
                        });

                var processRowsRect = Rectangle.FromLTRB(0, minY, width, maxY);

                // Now process the rows.
                ParallelHelper.IterateRows(
                    processRowsRect,
                    configuration,
                    rows =>
                        {
                            for (int y = rows.Min; y < rows.Max; y++)
                            {
                                // Ensure offsets are normalized for cropping and padding.
                                WeightsWindow window = this.verticalWeights.Weights[y - startY];
                                ref TPixel targetRow = ref MemoryMarshal.GetReference(destination.GetPixelRowSpan(y));

                                if (this.Compand)
                                {
                                    for (int x = 0; x < width; x++)
                                    {
                                        // Destination color components
                                        Vector4 destinationVector = window.ComputeWeightedColumnSum(
                                            firstPassPixels,
                                            x,
                                            sourceY);
                                        destinationVector = destinationVector.Compress();

                                        ref TPixel pixel = ref Unsafe.Add(ref targetRow, x);
                                        pixel.PackFromVector4(destinationVector);
                                    }
                                }
                                else
                                {
                                    for (int x = 0; x < width; x++)
                                    {
                                        // Destination color components
                                        Vector4 destinationVector = window.ComputeWeightedColumnSum(
                                            firstPassPixels,
                                            x,
                                            sourceY);

                                        ref TPixel pixel = ref Unsafe.Add(ref targetRow, x);
                                        pixel.PackFromVector4(destinationVector);
                                    }
                                }
                            }
                        });
            }
        }

        protected override void AfterImageApply(Image<TPixel> source, Image<TPixel> destination, Rectangle sourceRectangle)
        {
            base.AfterImageApply(source, destination, sourceRectangle);

            // TODO: An exception in the processing chain can leave these buffers undisposed. We should consider making image processors IDisposable!
            this.horizontalWeights?.Dispose();
            this.horizontalWeights = null;
            this.verticalWeights?.Dispose();
            this.verticalWeights = null;
        }
    }
}