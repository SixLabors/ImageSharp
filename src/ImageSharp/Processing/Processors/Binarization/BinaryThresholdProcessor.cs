// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Binarization
{
    /// <summary>
    /// The color component to be compared to threshold.
    /// </summary>
    public enum BinaryThresholdValueType : int
    {
        /// <summary>
        /// Luminance color component as value to be compared to threshold.
        /// </summary>
        Luminance = 0,

        /// <summary>
        /// Saturation color component as value to be compared to threshold.
        /// </summary>
        Saturation = 1,
    }

    /// <summary>
    /// Performs simple binary threshold filtering against an image.
    /// </summary>
    public class BinaryThresholdProcessor : IImageProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BinaryThresholdProcessor"/> class.
        /// </summary>
        /// <param name="threshold">The threshold to split the image. Must be between 0 and 1.</param>
        /// <param name="valueType">The color component to be compared to threshold.</param>
        public BinaryThresholdProcessor(float threshold, BinaryThresholdValueType valueType = BinaryThresholdValueType.Luminance)
            : this(threshold, Color.White, Color.Black, valueType)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BinaryThresholdProcessor"/> class.
        /// </summary>
        /// <param name="threshold">The threshold to split the image. Must be between 0 and 1.</param>
        /// <param name="upperColor">The color to use for pixels that are above the threshold.</param>
        /// <param name="lowerColor">The color to use for pixels that are below the threshold.</param>
        /// <param name="valueType">The color component to be compared to threshold.</param>
        public BinaryThresholdProcessor(float threshold, Color upperColor, Color lowerColor, BinaryThresholdValueType valueType = BinaryThresholdValueType.Luminance)
        {
            Guard.MustBeBetweenOrEqualTo(threshold, 0, 1, nameof(threshold));
            this.Threshold = threshold;
            this.UpperColor = upperColor;
            this.LowerColor = lowerColor;
            this.ValueType = valueType;
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
        /// Gets a value indicating whether to use saturation value instead of luminance.
        /// </summary>
        public BinaryThresholdValueType ValueType { get; }

       /// <inheritdoc />
        public IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>(Configuration configuration, Image<TPixel> source, Rectangle sourceRectangle)
            where TPixel : unmanaged, IPixel<TPixel>
            => new BinaryThresholdProcessor<TPixel>(configuration, this, source, sourceRectangle);
    }
}
