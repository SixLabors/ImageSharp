// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Advanced;
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
            return new Image<TPixel>(source.GetConfiguration(), source.MetaData.Clone(), frames);
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

            int minY = Math.Max(this.CropRectangle.Y, sourceRectangle.Y);
            int maxY = Math.Min(this.CropRectangle.Bottom, sourceRectangle.Bottom);
            int minX = Math.Max(this.CropRectangle.X, sourceRectangle.X);
            int maxX = Math.Min(this.CropRectangle.Right, sourceRectangle.Right);

            ParallelFor.WithConfiguration(
                minY,
                maxY,
                configuration,
                y =>
                {
                    Span<TPixel> sourceRow = source.GetPixelRowSpan(y).Slice(minX);
                    Span<TPixel> targetRow = destination.GetPixelRowSpan(y - minY);
                    sourceRow.Slice(0, maxX - minX).CopyTo(targetRow);
                });
        }
    }
}