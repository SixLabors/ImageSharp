// <copyright file="EntropyCropProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processing.Processors
{
    using System;

    /// <summary>
    /// Provides methods to allow the cropping of an image to preserve areas of highest
    /// entropy.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    public class EntropyCropProcessor<TColor> : ImageProcessor<TColor>
        where TColor : struct, IPackedPixel, IEquatable<TColor>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EntropyCropProcessor{TColor}"/> class.
        /// </summary>
        /// <param name="threshold">The threshold to split the image. Must be between 0 and 1.</param>
        /// <exception cref="System.ArgumentException">
        /// <paramref name="threshold"/> is less than 0 or is greater than 1.
        /// </exception>
        public EntropyCropProcessor(float threshold)
        {
            Guard.MustBeBetweenOrEqualTo(threshold, 0, 1, nameof(threshold));
            this.Value = threshold;
        }

        /// <summary>
        /// Gets the threshold value.
        /// </summary>
        public float Value { get; }

        /// <inheritdoc/>
        protected override void OnApply(ImageBase<TColor> source, Rectangle sourceRectangle)
        {
            ImageBase<TColor> temp = new Image<TColor>(source.Width, source.Height);
            temp.ClonePixels(source.Width, source.Height, source.Pixels);

            // Detect the edges.
            new SobelProcessor<TColor>().Apply(temp, sourceRectangle);

            // Apply threshold binarization filter.
            new BinaryThresholdProcessor<TColor>(this.Value).Apply(temp, sourceRectangle);

            // Search for the first white pixels
            Rectangle rectangle = ImageMaths.GetFilteredBoundingRectangle(temp, 0);

            if (rectangle == sourceRectangle)
            {
                return;
            }

            new CropProcessor<TColor>(rectangle).Apply(source, sourceRectangle);
        }
    }
}