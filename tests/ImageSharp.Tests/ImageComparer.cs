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
        const int DefaultScalingFactor = 32;
        const int DefaultSegmentThreshold = 3;
        const float DefaultImageThreshold = 0.000f;

        public static void VisualComparer<TColorA, TColorB>(Image<TColorA> expected, Image<TColorB> actual, float imageTheshold = DefaultImageThreshold, byte segmentThreshold = DefaultSegmentThreshold, int scalingFactor = DefaultScalingFactor)
           where TColorA : struct, IPixel<TColorA>
           where TColorB : struct, IPixel<TColorB>
        {
            float percentage = expected.PercentageDifference(actual, segmentThreshold, scalingFactor);

            Assert.InRange(percentage, 0, imageTheshold);
        }

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