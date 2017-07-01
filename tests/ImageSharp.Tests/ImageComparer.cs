// <copyright file="ImageComparer.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System;
    using ImageSharp;
    using ImageSharp.Memory;
    using ImageSharp.PixelFormats;
    using SixLabors.Primitives;
    using Xunit;

    /// <summary>
    /// Class to perform simple image comparisons.
    /// </summary>
    public static class ImageComparer
    {
        const int DefaultScalingFactor = 32; // This is means the images get scaled into a 32x32 image to sample pixels
        const int DefaultSegmentThreshold = 3; // The greyscale difference between 2 segements my be > 3 before it influences the overall difference
        const float DefaultImageThreshold = 0.000F; // After segment thresholds the images must have no differences

        /// <summary>
        /// Fills the bounded area with a solid color and does a visual comparison between 2 images asserting the difference outwith
        /// that area is less then a configurable threshold.
        /// </summary>
        /// <typeparam name="TPixelA">The color of the expected image</typeparam>
        /// <typeparam name="TPixelB">The color type fo the the actual image</typeparam>
        /// <param name="expected">The expected image</param>
        /// <param name="actual">The actual image</param>
        /// <param name="bounds">The bounds within the image has been altered</param>
        /// <param name="imageTheshold">
        /// The threshold for the percentage difference where the images are asumed to be the same.
        /// The default/undefined value is <see cref="DefaultImageThreshold"/>
        /// </param>
        /// <param name="segmentThreshold">
        /// The threshold of the individual segments before it acumulates towards the overall difference.
        /// The default undefined value is <see cref="DefaultSegmentThreshold"/>
        /// </param>
        /// <param name="scalingFactor">
        /// This is a sampling factor we sample a grid of average pixels <paramref name="scalingFactor"/> width by <paramref name="scalingFactor"/> high
        /// The default undefined value is <see cref="DefaultScalingFactor"/>
        /// </param>
        public static void EnsureProcessorChangesAreConstrained<TPixelA, TPixelB>(Image<TPixelA> expected, Image<TPixelB> actual, Rectangle bounds, float imageTheshold = DefaultImageThreshold, byte segmentThreshold = DefaultSegmentThreshold, int scalingFactor = DefaultScalingFactor)
            where TPixelA : struct, IPixel<TPixelA>
            where TPixelB : struct, IPixel<TPixelB>
        {
            // Draw identical shapes over the bounded and compare to ensure changes are constrained.
            expected.Mutate(x => x.Fill(NamedColors<TPixelA>.HotPink, bounds));
            actual.Mutate(x => x.Fill(NamedColors<TPixelB>.HotPink, bounds));

            CheckSimilarity(expected, actual, imageTheshold, segmentThreshold, scalingFactor);
        }

        /// <summary>
        /// Does a visual comparison between 2 images and then asserts the difference is less then a configurable threshold
        /// </summary>
        /// <typeparam name="TPixelA">The color of the expected image</typeparam>
        /// <typeparam name="TPixelB">The color type fo the the actual image</typeparam>
        /// <param name="expected">The expected image</param>
        /// <param name="actual">The actual image</param>
        /// <param name="imageTheshold">
        /// The threshold for the percentage difference where the images are asumed to be the same.
        /// The default/undefined value is <see cref="DefaultImageThreshold"/>
        /// </param>
        /// <param name="segmentThreshold">
        /// The threshold of the individual segments before it acumulates towards the overall difference.
        /// The default undefined value is <see cref="DefaultSegmentThreshold"/>
        /// </param>
        /// <param name="scalingFactor">
        /// This is a sampling factor we sample a grid of average pixels <paramref name="scalingFactor"/> width by <paramref name="scalingFactor"/> high
        /// The default undefined value is <see cref="DefaultScalingFactor"/>
        /// </param>
        public static void CheckSimilarity<TPixelA, TPixelB>(Image<TPixelA> expected, Image<TPixelB> actual, float imageTheshold = DefaultImageThreshold, byte segmentThreshold = DefaultSegmentThreshold, int scalingFactor = DefaultScalingFactor)
           where TPixelA : struct, IPixel<TPixelA>
           where TPixelB : struct, IPixel<TPixelB>
        {
            float percentage = expected.PercentageDifference(actual, segmentThreshold, scalingFactor);

            Assert.InRange(percentage, 0, imageTheshold);
        }

        /// <summary>
        /// Does a visual comparison between 2 images and then and returns the percentage diffence between the 2
        /// </summary>
        /// <typeparam name="TPixelA">The color of the source image</typeparam>
        /// <typeparam name="TPixelB">The color type for the target image</typeparam>
        /// <param name="source">The source image</param>
        /// <param name="target">The target image</param>
        /// <param name="segmentThreshold">
        /// The threshold of the individual segments before it acumulates towards the overall difference.
        /// The default undefined value is <see cref="DefaultSegmentThreshold"/>
        /// </param>
        /// <param name="scalingFactor">
        /// This is a sampling factor we sample a grid of average pixels <paramref name="scalingFactor"/> width by <paramref name="scalingFactor"/> high
        /// The default undefined value is <see cref="ImageComparer.DefaultScalingFactor"/>
        /// </param>
        /// <returns>Returns a number from 0 - 1 which represents the difference focter between the images.</returns>
        public static float PercentageDifference<TPixelA, TPixelB>(this Image<TPixelA> source, Image<TPixelB> target, byte segmentThreshold = DefaultSegmentThreshold, int scalingFactor = DefaultScalingFactor)
            where TPixelA : struct, IPixel<TPixelA>
            where TPixelB : struct, IPixel<TPixelB>
        {
            // code adapted from https://www.codeproject.com/Articles/374386/Simple-image-comparison-in-NET
            Fast2DArray<byte> differences = GetDifferences(source, target, scalingFactor);

            int diffPixels = 0;

            foreach (byte b in differences.Data)
            {
                if (b > segmentThreshold) { diffPixels++; }
            }

            return diffPixels / (float)(scalingFactor * scalingFactor);
        }

        private static Fast2DArray<byte> GetDifferences<TPixelA, TPixelB>(Image<TPixelA> source, Image<TPixelB> target, int scalingFactor)
            where TPixelA : struct, IPixel<TPixelA>
            where TPixelB : struct, IPixel<TPixelB>
        {
            var differences = new Fast2DArray<byte>(scalingFactor, scalingFactor);
            Fast2DArray<byte> firstGray = source.GetGrayScaleValues(scalingFactor);
            Fast2DArray<byte> secondGray = target.GetGrayScaleValues(scalingFactor);

            for (int y = 0; y < scalingFactor; y++)
            {
                for (int x = 0; x < scalingFactor; x++)
                {
                    int diff = firstGray[x, y] - secondGray[x, y];
                    differences[x, y] = (byte)Math.Abs(diff);
                }
            }

            return differences;
        }

        private static Fast2DArray<byte> GetGrayScaleValues<TPixelA>(this Image<TPixelA> source, int scalingFactor)
            where TPixelA : struct, IPixel<TPixelA>
        {
            byte[] buffer = new byte[3];
            using (Image<TPixelA> img = source.Generate(x => x.Resize(scalingFactor, scalingFactor).Grayscale()))
            {
                using (PixelAccessor<TPixelA> pixels = img.Lock())
                {
                    var grayScale = new Fast2DArray<byte>(scalingFactor, scalingFactor);
                    for (int y = 0; y < scalingFactor; y++)
                    {
                        for (int x = 0; x < scalingFactor; x++)
                        {
                            pixels[x, y].ToXyzBytes(buffer, 0);
                            grayScale[x, y] = buffer[0];
                        }
                    }

                    return grayScale;
                }
            }
        }
    }
}