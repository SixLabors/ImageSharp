// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Webp.Lossless
{
    /// <summary>
    /// Enum for the different transform types. Transformations are reversible manipulations of the image data
    /// that can reduce the remaining symbolic entropy by modeling spatial and color correlations.
    /// Transformations can make the final compression more dense.
    /// </summary>
    internal enum Vp8LTransformType : uint
    {
        /// <summary>
        /// The predictor transform can be used to reduce entropy by exploiting the fact that neighboring pixels are often correlated.
        /// </summary>
        PredictorTransform = 0,

        /// <summary>
        /// The goal of the color transform is to de-correlate the R, G and B values of each pixel.
        /// Color transform keeps the green (G) value as it is, transforms red (R) based on green and transforms blue (B) based on green and then based on red.
        /// </summary>
        CrossColorTransform = 1,

        /// <summary>
        /// The subtract green transform subtracts green values from red and blue values of each pixel.
        /// When this transform is present, the decoder needs to add the green value to both red and blue.
        /// There is no data associated with this transform.
        /// </summary>
        SubtractGreen = 2,

        /// <summary>
        /// If there are not many unique pixel values, it may be more efficient to create a color index array and replace the pixel values by the array's indices.
        /// The color indexing transform achieves this.
        /// </summary>
        ColorIndexingTransform = 3,
    }
}
