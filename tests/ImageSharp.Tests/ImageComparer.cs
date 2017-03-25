namespace ImageSharp.Tests
{
    using System;
    using ImageSharp;
    using Xunit;

    /// <summary>
    /// Class to perform simple image comparisons.
    /// </summary>
    public static class ImageComparer
    {
        const int DefaultScalingFactor = 32; // this is means the images get scaled into a 32x32 image to sample pixels
        const int DefaultSegmentThreshold = 3; // the greyscale difference between 2 segements my be > 3 before it influances the overall difference
        const float DefaultImageThreshold = 0.000f; // after segment threasholds the images must have no differences

        /// <summary>
        /// Does a visual comparison between 2 images and then asserts the difference is less then a configurable threshold
        /// </summary>
        /// <typeparam name="TColorA">The color of the expected image</typeparam>
        /// <typeparam name="TColorB">The color type fo the the actual image</typeparam>
        /// <param name="expected">The expected image</param>
        /// <param name="actual">The actual image</param>
        /// <param name="imageTheshold">
        /// The threshold for the percentage difference where the images are asumed to be the same.
        /// The default/undefined value is <see cref="ImageComparer.DefaultImageThreshold"/>
        /// </param>
        /// <param name="segmentThreshold">
        /// The threashold of the individual segments before it acumulates towards the overall difference.
        /// The default undefined value is <see cref="ImageComparer.DefaultSegmentThreshold"/>
        /// </param>
        /// <param name="scalingFactor">
        /// This is a sampling factor we sample a grid of average pixels <paramref name="scalingFactor"/> width by <paramref name="scalingFactor"/> high
        /// The default undefined value is <see cref="ImageComparer.DefaultScalingFactor"/>
        /// </param>
        public static void CheckSimilarity<TColorA, TColorB>(Image<TColorA> expected, Image<TColorB> actual, float imageTheshold = DefaultImageThreshold, byte segmentThreshold = DefaultSegmentThreshold, int scalingFactor = DefaultScalingFactor)
           where TColorA : struct, IPixel<TColorA>
           where TColorB : struct, IPixel<TColorB>
        {
            float percentage = expected.PercentageDifference(actual, segmentThreshold, scalingFactor);

            Assert.InRange(percentage, 0, imageTheshold);
        }

        /// <summary>
        /// Does a visual comparison between 2 images and then and returns the percentage diffence between the 2
        /// </summary>
        /// <typeparam name="TColorA">The color of the source image</typeparam>
        /// <typeparam name="TColorB">The color type for the target image</typeparam>
        /// <param name="source">The source image</param>
        /// <param name="target">The target image</param>
        /// <param name="segmentThreshold">
        /// The threashold of the individual segments before it acumulates towards the overall difference.
        /// The default undefined value is <see cref="ImageComparer.DefaultSegmentThreshold"/>
        /// </param>
        /// <param name="scalingFactor">
        /// This is a sampling factor we sample a grid of average pixels <paramref name="scalingFactor"/> width by <paramref name="scalingFactor"/> high
        /// The default undefined value is <see cref="ImageComparer.DefaultScalingFactor"/>
        /// </param>
        /// <returns>Returns a number from 0 - 1 which represents the diference focter between the images.</returns>
        public static float PercentageDifference<TColorA, TColorB>(this Image<TColorA> source, Image<TColorB> target, byte segmentThreshold = DefaultSegmentThreshold, int scalingFactor = DefaultScalingFactor)
            where TColorA : struct, IPixel<TColorA>
            where TColorB : struct, IPixel<TColorB>
        {
            // code adapted from https://www.codeproject.com/Articles/374386/Simple-image-comparison-in-NET
            Fast2DArray<byte> differences = GetDifferences(source, target, scalingFactor);

            int diffPixels = 0;

            foreach (byte b in differences.Data)
            {
                if (b > segmentThreshold) { diffPixels++; }
            }

            return diffPixels / (scalingFactor * scalingFactor);
        }

        private static Fast2DArray<byte> GetDifferences<TColorA, TColorB>(Image<TColorA> source, Image<TColorB> target, int scalingFactor)
            where TColorA : struct, IPixel<TColorA>
            where TColorB : struct, IPixel<TColorB>
        {
            Fast2DArray<byte> differences = new Fast2DArray<byte>(scalingFactor, scalingFactor);
            Fast2DArray<byte> firstGray = source.GetGrayScaleValues(scalingFactor);
            Fast2DArray<byte> secondGray = target.GetGrayScaleValues(scalingFactor);

            for (int y = 0; y < scalingFactor; y++)
            {
                for (int x = 0; x < scalingFactor; x++)
                {
                    differences[x, y] = (byte)Math.Abs(firstGray[x, y] - secondGray[x, y]);
                }
            }

            return differences;
        }

        private static Fast2DArray<byte> GetGrayScaleValues<TColorA>(this Image<TColorA> source, int scalingFactor)
            where TColorA : struct, IPixel<TColorA>
        {
            byte[] buffer = new byte[4];
            using (Image<TColorA> img = new Image<TColorA>(source).Resize(scalingFactor, scalingFactor).Grayscale())
            {
                using (PixelAccessor<TColorA> pixels = img.Lock())
                {
                    Fast2DArray<byte> grayScale = new Fast2DArray<byte>(scalingFactor, scalingFactor);
                    for (int y = 0; y < scalingFactor; y++)
                    {
                        for (int x = 0; x < scalingFactor; x++)
                        {
                            pixels[x, y].ToXyzBytes(buffer, 0);
                            grayScale[x, y] = buffer[1];
                        }
                    }

                    return grayScale;
                }
            }
        }
    }
}