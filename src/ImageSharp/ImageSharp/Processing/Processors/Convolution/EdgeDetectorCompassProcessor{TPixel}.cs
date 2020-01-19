// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using SixLabors.ImageSharp.Advanced.ParallelUtils;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Primitives;
using SixLabors.ImageSharp.Processing.Processors.Filters;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution
{
    /// <summary>
    /// Defines a processor that detects edges within an image using a eight two dimensional matrices.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class EdgeDetectorCompassProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EdgeDetectorCompassProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
        /// <param name="kernels">Gets the kernels to use.</param>
        /// <param name="grayscale">Whether to convert the image to grayscale before performing edge detection.</param>
        /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
        /// <param name="sourceRectangle">The source area to process for the current processor instance.</param>
        internal EdgeDetectorCompassProcessor(Configuration configuration, CompassKernels kernels, bool grayscale, Image<TPixel> source, Rectangle sourceRectangle)
            : base(configuration, source, sourceRectangle)
        {
            this.Grayscale = grayscale;
            this.Kernels = kernels;
        }

        private CompassKernels Kernels { get; }

        private bool Grayscale { get; }

        /// <inheritdoc/>
        protected override void BeforeImageApply()
        {
            if (this.Grayscale)
            {
                new GrayscaleBt709Processor(1F).Execute(this.Configuration, this.Source, this.SourceRectangle);
            }

            base.BeforeImageApply();
        }

        /// <inheritdoc />
        protected override void OnFrameApply(ImageFrame<TPixel> source)
        {
            DenseMatrix<float>[] kernels = this.Kernels.Flatten();

            int startY = this.SourceRectangle.Y;
            int endY = this.SourceRectangle.Bottom;
            int startX = this.SourceRectangle.X;
            int endX = this.SourceRectangle.Right;

            // Align start/end positions.
            int minX = Math.Max(0, startX);
            int maxX = Math.Min(source.Width, endX);
            int minY = Math.Max(0, startY);
            int maxY = Math.Min(source.Height, endY);

            // we need a clean copy for each pass to start from
            using (ImageFrame<TPixel> cleanCopy = source.Clone())
            {
                using (var processor = new ConvolutionProcessor<TPixel>(this.Configuration, kernels[0], true, this.Source, this.SourceRectangle))
                {
                    processor.Apply(source);
                }

                if (kernels.Length == 1)
                {
                    return;
                }

                int shiftY = startY;
                int shiftX = startX;

                // Reset offset if necessary.
                if (minX > 0)
                {
                    shiftX = 0;
                }

                if (minY > 0)
                {
                    shiftY = 0;
                }

                var workingRect = Rectangle.FromLTRB(minX, minY, maxX, maxY);

                // Additional runs.
                // ReSharper disable once ForCanBeConvertedToForeach
                for (int i = 1; i < kernels.Length; i++)
                {
                    using (ImageFrame<TPixel> pass = cleanCopy.Clone())
                    {
                        using (var processor = new ConvolutionProcessor<TPixel>(this.Configuration, kernels[i], true, this.Source, this.SourceRectangle))
                        {
                            processor.Apply(pass);
                        }

                        Buffer2D<TPixel> passPixels = pass.PixelBuffer;
                        Buffer2D<TPixel> targetPixels = source.PixelBuffer;

                        ParallelHelper.IterateRows(
                            workingRect,
                            this.Configuration,
                            rows =>
                                {
                                    for (int y = rows.Min; y < rows.Max; y++)
                                    {
                                        int offsetY = y - shiftY;

                                        ref TPixel passPixelsBase = ref MemoryMarshal.GetReference(passPixels.GetRowSpan(offsetY));
                                        ref TPixel targetPixelsBase = ref MemoryMarshal.GetReference(targetPixels.GetRowSpan(offsetY));

                                        for (int x = minX; x < maxX; x++)
                                        {
                                            int offsetX = x - shiftX;

                                            // Grab the max components of the two pixels
                                            ref TPixel currentPassPixel = ref Unsafe.Add(ref passPixelsBase, offsetX);
                                            ref TPixel currentTargetPixel = ref Unsafe.Add(ref targetPixelsBase, offsetX);

                                            var pixelValue = Vector4.Max(
                                                currentPassPixel.ToVector4(),
                                                currentTargetPixel.ToVector4());

                                            currentTargetPixel.FromVector4(pixelValue);
                                        }
                                    }
                                });
                    }
                }
            }
        }
    }
}
