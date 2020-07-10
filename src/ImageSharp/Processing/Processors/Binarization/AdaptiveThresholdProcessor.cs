// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Binarization
{
    /// <summary>
    /// Performs Bradley Adaptive Threshold filter against an image.
    /// </summary>
    /// <remarks>
    /// Implements "Adaptive Thresholding Using the Integral Image",
    /// see paper: http://citeseerx.ist.psu.edu/viewdoc/download?doi=10.1.1.420.7883&amp;rep=rep1&amp;type=pdf
    /// </remarks>
    public class AdaptiveThresholdProcessor : IImageProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AdaptiveThresholdProcessor"/> class.
        /// </summary>
        public AdaptiveThresholdProcessor()
            : this(Color.White, Color.Black, 0.85f)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AdaptiveThresholdProcessor"/> class.
        /// </summary>
        /// <param name="thresholdLimit">Threshold limit.</param>
        public AdaptiveThresholdProcessor(float thresholdLimit)
            : this(Color.White, Color.Black, thresholdLimit)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AdaptiveThresholdProcessor"/> class.
        /// </summary>
        /// <param name="upper">Color for upper threshold.</param>
        /// <param name="lower">Color for lower threshold.</param>
        public AdaptiveThresholdProcessor(Color upper, Color lower)
            : this(upper, lower, 0.85f)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AdaptiveThresholdProcessor"/> class.
        /// </summary>
        /// <param name="upper">Color for upper threshold.</param>
        /// <param name="lower">Color for lower threshold.</param>
        /// <param name="thresholdLimit">Threshold limit.</param>
        public AdaptiveThresholdProcessor(Color upper, Color lower, float thresholdLimit)
        {
            this.Upper = upper;
            this.Lower = lower;
            this.ThresholdLimit = thresholdLimit;
        }

        /// <summary>
        /// Gets or sets upper color limit for thresholding.
        /// </summary>
        public Color Upper { get; set; }

        /// <summary>
        /// Gets or sets lower color limit for threshold.
        /// </summary>
        public Color Lower { get; set; }

        /// <summary>
        /// Gets or sets the value for threshold limit.
        /// </summary>
        public float ThresholdLimit { get; set; }

        /// <inheritdoc />
        public IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>(Configuration configuration, Image<TPixel> source, Rectangle sourceRectangle)
            where TPixel : unmanaged, IPixel<TPixel>
            => new AdaptiveThresholdProcessor<TPixel>(configuration, this, source, sourceRectangle);
    }
}
