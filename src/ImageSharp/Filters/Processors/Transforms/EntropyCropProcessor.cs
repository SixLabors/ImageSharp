// <copyright file="EntropyCropProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processors
{
    /// <summary>
    /// Provides methods to allow the cropping of an image to preserve areas of highest
    /// entropy.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
    public class EntropyCropProcessor<TColor, TPacked> : ImageFilteringProcessor<TColor, TPacked>
        where TColor : struct, IPackedPixel<TPacked>
        where TPacked : struct
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EntropyCropProcessor{TColor, TPacked}"/> class.
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
        protected override void Apply(ImageBase<TColor, TPacked> source, Rectangle sourceRectangle, int startY, int endY)
        {
            ImageBase<TColor, TPacked> temp = new Image<TColor, TPacked>(source.Width, source.Height);

            // Detect the edges.
            new SobelProcessor<TColor, TPacked>().Apply(temp, source, sourceRectangle);

            // Apply threshold binarization filter.
            new BinaryThresholdProcessor<TColor, TPacked>(this.Value).Apply(temp, sourceRectangle);

            // Search for the first white pixels
            Rectangle rectangle = ImageMaths.GetFilteredBoundingRectangle(temp, 0);

            if (rectangle == sourceRectangle)
            {
                return;
            }

            new CropProcessor<TColor, TPacked>(rectangle).Apply(source, sourceRectangle);
        }
    }
}