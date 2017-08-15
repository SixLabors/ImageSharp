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
    public class PercentageImageComparer : ImageComparer
    {
        public float ImageThreshold { get; }

        public byte SegmentThreshold { get; }

        public int ScaleIntoSize { get; }


        public PercentageImageComparer(
            float imageThreshold = DefaultImageThreshold,
            byte segmentThreshold = DefaultSegmentThreshold,
            int scaleIntoSize = DefaultScaleIntoSize)
        {
            this.ImageThreshold = imageThreshold;
            this.SegmentThreshold = segmentThreshold;
            this.ScaleIntoSize = scaleIntoSize;
        }

        /// <summary>
        /// This is means the images get scaled into a 32x32 image to sample pixels
        /// </summary>
        public const int DefaultScaleIntoSize = 32;

        /// <summary>
        /// The greyscale difference between 2 segements my be > 3 before it influences the overall difference
        /// </summary>
        public const int DefaultSegmentThreshold = 3;

        /// <summary>
        /// After segment thresholds the images must have no differences
        /// </summary>
        public const float DefaultImageThreshold = 0.000F;

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
        /// <param name="scaleIntoSize">
        /// This is a sampling factor we sample a grid of average pixels <paramref name="scaleIntoSize"/> width by <paramref name="scaleIntoSize"/> high
        /// The default undefined value is <see cref="DefaultScaleIntoSize"/>
        /// </param>
        public static void EnsureProcessorChangesAreConstrained<TPixelA, TPixelB>(
            Image<TPixelA> expected,
            Image<TPixelB> actual,
            Rectangle bounds,
            float imageTheshold = DefaultImageThreshold,
            byte segmentThreshold = DefaultSegmentThreshold,
            int scaleIntoSize = DefaultScaleIntoSize)
            where TPixelA : struct, IPixel<TPixelA>
            where TPixelB : struct, IPixel<TPixelB>
        {
            // Draw identical shapes over the bounded and compare to ensure changes are constrained.
            expected.Mutate(x => x.Fill(NamedColors<TPixelA>.HotPink, bounds));
            actual.Mutate(x => x.Fill(NamedColors<TPixelB>.HotPink, bounds));

            VerifySimilarity(expected, actual, imageTheshold, segmentThreshold, scaleIntoSize);
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
        /// <param name="scaleIntoSize">
        /// This is a sampling factor we sample a grid of average pixels <paramref name="scaleIntoSize"/> width by <paramref name="scaleIntoSize"/> high
        /// The default undefined value is <see cref="DefaultScaleIntoSize"/>
        /// </param>
        public static void VerifySimilarity<TPixelA, TPixelB>(
            Image<TPixelA> expected,
            Image<TPixelB> actual,
            float imageTheshold = DefaultImageThreshold,
            byte segmentThreshold = DefaultSegmentThreshold,
            int scaleIntoSize = DefaultScaleIntoSize)
            where TPixelA : struct, IPixel<TPixelA> where TPixelB : struct, IPixel<TPixelB>
        {
            var comparer = new PercentageImageComparer(imageTheshold, segmentThreshold, scaleIntoSize);
            comparer.Verify(expected, actual);
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
        /// The default undefined value is <see cref="DefaultScaleIntoSize"/>
        /// </param>
        /// <returns>Returns a number from 0 - 1 which represents the difference focter between the images.</returns>
        public static float PercentageDifference<TPixelA, TPixelB>(Image<TPixelA> source, Image<TPixelB> target, byte segmentThreshold = DefaultSegmentThreshold, int scalingFactor = DefaultScaleIntoSize)
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
            Fast2DArray<byte> firstGray = GetGrayScaleValues(source, scalingFactor);
            Fast2DArray<byte> secondGray = GetGrayScaleValues(target, scalingFactor);

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

        private static Fast2DArray<byte> GetGrayScaleValues<TPixelA>(Image<TPixelA> source, int scalingFactor)
            where TPixelA : struct, IPixel<TPixelA>
        {
            byte[] buffer = new byte[3];
            using (Image<TPixelA> img = source.Clone(x => x.Resize(scalingFactor, scalingFactor).Grayscale()))
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

        public override void Verify<TPixelA, TPixelB>(Image<TPixelA> expected, Image<TPixelB> actual)
        {
            if (expected.Size() != actual.Size())
            {
                throw new ImageDimensionsMismatchException(expected.Size(), actual.Size());
            }
            
            float percentage = PercentageDifference(expected, actual, this.SegmentThreshold, this.ScaleIntoSize);

            if (percentage > this.ImageThreshold)
            {
                throw new ImagesSimilarityException(
                    $"The percentage difference of images {percentage} is larger than expected maximum {this.ImageThreshold}!");
            }
        }
    }
}