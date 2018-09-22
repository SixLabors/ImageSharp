// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.ParallelUtils;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms
{
    /// <summary>
    /// Provides methods to allow the cropping of an image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class CropProcessor<TPixel> : TransformProcessorBase<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CropProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="cropRectangle">The target cropped rectangle.</param>
        public CropProcessor(Rectangle cropRectangle)
        {
            this.CropRectangle = cropRectangle;
        }

        /// <summary>
        /// Gets the width.
        /// </summary>
        public Rectangle CropRectangle { get; }

        /// <inheritdoc/>
        protected override Image<TPixel> CreateDestination(Image<TPixel> source, Rectangle sourceRectangle)
        {
            // We will always be creating the clone even for mutate because we may need to resize the canvas
            IEnumerable<ImageFrame<TPixel>> frames = source.Frames.Select(x => new ImageFrame<TPixel>(source.GetConfiguration(), this.CropRectangle.Width, this.CropRectangle.Height, x.MetaData.DeepClone()));

            // Use the overload to prevent an extra frame being added
            return new Image<TPixel>(source.GetConfiguration(), source.MetaData.DeepClone(), frames);
        }

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source, ImageFrame<TPixel> destination, Rectangle sourceRectangle, Configuration configuration)
        {
            // Handle resize dimensions identical to the original
            if (source.Width == destination.Width && source.Height == destination.Height && sourceRectangle == this.CropRectangle)
            {
                // the cloned will be blank here copy all the pixel data over
                source.GetPixelSpan().CopyTo(destination.GetPixelSpan());
                return;
            }

            var rect = Rectangle.Intersect(this.CropRectangle, sourceRectangle);

            // Copying is cheap, we should process more pixels per task:
            ParallelExecutionSettings parallelSettings = configuration.GetParallelSettings().MultiplyMinimumPixelsPerTask(4);

            ParallelHelper.IterateRows(
                rect,
                parallelSettings,
                rows =>
                    {
                        for (int y = rows.Min; y < rows.Max; y++)
                        {
                            Span<TPixel> sourceRow = source.GetPixelRowSpan(y).Slice(rect.Left);
                            Span<TPixel> targetRow = destination.GetPixelRowSpan(y - rect.Top);
                            sourceRow.Slice(0, rect.Width).CopyTo(targetRow);
                        }
                    });
        }
    }
}