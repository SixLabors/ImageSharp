// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Binarization
{
    /// <summary>
    /// Performs simple binary threshold filtering against an image.
    /// </summary>
    public class BinaryThresholdProcessor : IImageProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BinaryThresholdProcessor"/> class.
        /// </summary>
        /// <param name="threshold">The threshold to split the image. Must be between 0 and 1.</param>
        /// <param name="mode">The color component to be compared to threshold.</param>
        public BinaryThresholdProcessor(float threshold, BinaryThresholdMode mode)
            : this(threshold, Color.White, Color.Black, mode)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BinaryThresholdProcessor"/> class with
        /// Luminance as color component to be compared to threshold.
        /// </summary>
        /// <param name="threshold">The threshold to split the image. Must be between 0 and 1.</param>
        public BinaryThresholdProcessor(float threshold)
            : this(threshold, Color.White, Color.Black, BinaryThresholdMode.Luminance)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BinaryThresholdProcessor"/> class.
        /// </summary>
        /// <param name="threshold">The threshold to split the image. Must be between 0 and 1.</param>
        /// <param name="upperColor">The color to use for pixels that are above the threshold.</param>
        /// <param name="lowerColor">The color to use for pixels that are below the threshold.</param>
        /// <param name="mode">The color component to be compared to threshold.</param>
        public BinaryThresholdProcessor(float threshold, Color upperColor, Color lowerColor, BinaryThresholdMode mode)
        {
            Guard.MustBeBetweenOrEqualTo(threshold, 0, 1, nameof(threshold));
            this.Threshold = threshold;
            this.UpperColor = upperColor;
            this.LowerColor = lowerColor;
            this.Mode = mode;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BinaryThresholdProcessor"/> class with
        /// Luminance as color component to be compared to threshold.
        /// </summary>
        /// <param name="threshold">The threshold to split the image. Must be between 0 and 1.</param>
        /// <param name="upperColor">The color to use for pixels that are above the threshold.</param>
        /// <param name="lowerColor">The color to use for pixels that are below the threshold.</param>
        public BinaryThresholdProcessor(float threshold, Color upperColor, Color lowerColor)
            : this(threshold, upperColor, lowerColor, BinaryThresholdMode.Luminance)
        {
        }

        /// <summary>
        /// Gets the threshold value.
        /// </summary>
        public float Threshold { get; }

        /// <summary>
        /// Gets the color to use for pixels that are above the threshold.
        /// </summary>
        public Color UpperColor { get; }

        /// <summary>
        /// Gets the color to use for pixels that fall below the threshold.
        /// </summary>
        public Color LowerColor { get; }

        /// <summary>
        /// Gets the <see cref="BinaryThresholdMode"/> defining the value to be compared to threshold.
        /// </summary>
        public BinaryThresholdMode Mode { get; }

       /// <inheritdoc />
        public IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>(Configuration configuration, Image<TPixel> source, Rectangle sourceRectangle)
            where TPixel : unmanaged, IPixel<TPixel>
            => new BinaryThresholdProcessor<TPixel>(configuration, this, source, sourceRectangle);
    }
}
