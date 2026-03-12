// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests;

public abstract partial class TestImageProvider<TPixel> : IXunitSerializable
    where TPixel : unmanaged, IPixel<TPixel>
{
    /// <summary>
    /// A test image provider that produces test patterns.
    /// </summary>
    private class TestPatternProvider : BlankProvider
    {
        private static readonly Dictionary<string, Image<TPixel>> TestImages = [];

        private static readonly TPixel[] BlackWhitePixels =
        [
            Color.Black.ToPixel<TPixel>(),
            Color.White.ToPixel<TPixel>()
        ];

        private static readonly TPixel[] PinkBluePixels =
        [
            Color.HotPink.ToPixel<TPixel>(),
            Color.Blue.ToPixel<TPixel>()
        ];

        public TestPatternProvider(int width, int height)
            : base(width, height)
        {
        }

        /// <summary>
        /// This parameterless constructor is needed for xUnit deserialization
        /// </summary>
        public TestPatternProvider()
        {
        }

        public override string SourceFileOrDescription => TestUtils.AsInvariantString($"TestPattern{this.Width}x{this.Height}");

        public override Image<TPixel> GetImage()
        {
            lock (TestImages)
            {
                if (!TestImages.TryGetValue(this.SourceFileOrDescription, out Image<TPixel> value))
                {
                    Image<TPixel> image = new(this.Width, this.Height);
                    DrawTestPattern(image);
                    value = image;
                    TestImages.Add(this.SourceFileOrDescription, value);
                }

                return value.Clone(this.Configuration);
            }
        }

        /// <summary>
        /// Draws the test pattern on an image by drawing 4 other patterns in the for quadrants of the image.
        /// </summary>
        /// <param name="image">The image to draw on.</param>
        private static void DrawTestPattern(Image<TPixel> image)
        {
            // first lets split the image into 4 quadrants
            Buffer2D<TPixel> pixels = image.GetRootFramePixelBuffer();
            BlackWhiteChecker(pixels); // top left
            VerticalBars(pixels); // top right
            TransparentGradients(pixels); // bottom left
            Rainbow(pixels); // bottom right
        }

        /// <summary>
        /// Fills the top right quadrant with alternating solid vertical bars.
        /// </summary>
        /// <param name="pixels">The pixel buffer.</param>
        private static void VerticalBars(Buffer2D<TPixel> pixels)
        {
            // topLeft
            int left = pixels.Width / 2;
            int right = pixels.Width;
            const int top = 0;
            int bottom = pixels.Height / 2;
            int stride = pixels.Width / 12;
            if (stride < 1)
            {
                stride = 1;
            }

            for (int y = top; y < bottom; y++)
            {
                int p = 0;
                for (int x = left; x < right; x++)
                {
                    if (x % stride == 0)
                    {
                        p++;
                        p %= PinkBluePixels.Length;
                    }

                    pixels[x, y] = PinkBluePixels[p];
                }
            }
        }

        /// <summary>
        /// fills the top left quadrant with a black and white checker board.
        /// </summary>
        /// <param name="pixels">The pixel buffer.</param>
        private static void BlackWhiteChecker(Buffer2D<TPixel> pixels)
        {
            // topLeft
            const int left = 0;
            int right = pixels.Width / 2;
            const int top = 0;
            int bottom = pixels.Height / 2;
            int stride = pixels.Width / 6;

            int p = 0;
            for (int y = top; y < bottom; y++)
            {
                if (y % stride is 0)
                {
                    p++;
                    p %= BlackWhitePixels.Length;
                }

                int pStart = p;
                for (int x = left; x < right; x++)
                {
                    if (x % stride is 0)
                    {
                        p++;
                        p %= BlackWhitePixels.Length;
                    }

                    pixels[x, y] = BlackWhitePixels[p];
                }

                p = pStart;
            }
        }

        /// <summary>
        /// Fills the bottom left quadrant with 3 horizontal bars in Red, Green and Blue with a alpha gradient from left (transparent) to right (solid).
        /// </summary>
        /// <param name="pixels">The pixel buffer</param>
        private static void TransparentGradients(Buffer2D<TPixel> pixels)
        {
            // topLeft
            const int left = 0;
            int right = pixels.Width / 2;
            int top = pixels.Height / 2;
            int bottom = pixels.Height;
            int height = (int)Math.Ceiling(pixels.Height / 6f);

            Vector4 red = Color.Red.ToPixel<TPixel>().ToVector4(); // use real color so we can see how it translates in the test pattern
            Vector4 green = Color.Green.ToPixel<TPixel>().ToVector4(); // use real color so we can see how it translates in the test pattern
            Vector4 blue = Color.Blue.ToPixel<TPixel>().ToVector4(); // use real color so we can see how it translates in the test pattern

            for (int x = left; x < right; x++)
            {
                blue.W = red.W = green.W = x / (float)right;

                TPixel c = TPixel.FromVector4(red);
                int topBand = top;
                for (int y = topBand; y < top + height; y++)
                {
                    pixels[x, y] = c;
                }

                topBand += height;
                c = TPixel.FromVector4(green);
                for (int y = topBand; y < topBand + height; y++)
                {
                    pixels[x, y] = c;
                }

                topBand += height;
                c = TPixel.FromVector4(blue);
                for (int y = topBand; y < bottom; y++)
                {
                    pixels[x, y] = c;
                }
            }
        }

        /// <summary>
        /// Fills the bottom right quadrant with all the colors producible by converting iterating over a uint and unpacking it.
        /// A better algorithm could be used but it works
        /// </summary>
        /// <param name="pixels">The pixel buffer.</param>
        private static void Rainbow(Buffer2D<TPixel> pixels)
        {
            int left = pixels.Width / 2;
            int right = pixels.Width;
            int top = pixels.Height / 2;
            int bottom = pixels.Height;

            int pixelCount = left * top;
            uint stepsPerPixel = (uint)(uint.MaxValue / pixelCount);
            Rgba32 t = default;

            for (int x = left; x < right; x++)
            {
                for (int y = top; y < bottom; y++)
                {
                    t.PackedValue += stepsPerPixel;
                    pixels[x, y] = TPixel.FromRgba32(t);
                }
            }
        }
    }
}
