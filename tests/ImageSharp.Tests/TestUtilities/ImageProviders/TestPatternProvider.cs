// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Numerics;

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests
{
    public abstract partial class TestImageProvider<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// A test image provider that produces test patterns.
        /// </summary>
        /// <typeparam name="TPixel"></typeparam>
        private class TestPatternProvider : BlankProvider
        {
            static Dictionary<string, Image<TPixel>> testImages = new Dictionary<string, Image<TPixel>>();

            public TestPatternProvider(int width, int height)
                : base(width, height)
            {
            }

            public TestPatternProvider()
                : base()
            {
            }

            public override string SourceFileOrDescription => TestUtils.AsInvariantString($"TestPattern{this.Width}x{this.Height}");

            public override Image<TPixel> GetImage()
            {
                lock (testImages)
                {
                    if (!testImages.ContainsKey(this.SourceFileOrDescription))
                    {
                        Image<TPixel> image = new Image<TPixel>(this.Width, this.Height);
                        DrawTestPattern(image);
                        testImages.Add(this.SourceFileOrDescription, image);
                    }
                }

                return testImages[this.SourceFileOrDescription].Clone();
            }

            /// <summary>
            /// Draws the test pattern on an image by drawing 4 other patterns in the for quadrants of the image.
            /// </summary>
            /// <param name="image"></param>
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
            /// <param name="pixels"></param>
            private static void VerticalBars(Buffer2D<TPixel> pixels)
            {
                // topLeft
                int left = pixels.Width / 2;
                int right = pixels.Width;
                int top = 0;
                int bottom = pixels.Height / 2;
                int stride = pixels.Width / 12;
                if (stride < 1)
                {
                    stride = 1;
                }

                TPixel[] c =
                {
                    NamedColors<TPixel>.HotPink,
                    NamedColors<TPixel>.Blue
                };

                for (int y = top; y < bottom; y++)
                {
                    int p = 0;
                    for (int x = left; x < right; x++)
                    {
                        if (x % stride == 0)
                        {
                            p++;
                            p = p % c.Length;
                        }
                        pixels[x, y] = c[p];
                    }
                }
            }

            /// <summary>
            /// fills the top left quadrant with a black and white checker board.
            /// </summary>
            /// <param name="pixels"></param>
            private static void BlackWhiteChecker(Buffer2D<TPixel> pixels)
            {
                // topLeft
                int left = 0;
                int right = pixels.Width / 2;
                int top = 0;
                int bottom = pixels.Height / 2;
                int stride = pixels.Width / 6;
                TPixel[] c = 
                {
                    NamedColors<TPixel>.Black,
                    NamedColors<TPixel>.White
                };

                int p = 0;
                for (int y = top; y < bottom; y++)
                {
                    if (y % stride == 0)
                    {
                        p++;
                        p = p % c.Length;
                    }
                    int pstart = p;
                    for (int x = left; x < right; x++)
                    {
                        if (x % stride == 0)
                        {
                            p++;
                            p = p % c.Length;
                        }
                        pixels[x, y] = c[p];
                    }
                    p = pstart;
                }
            }

            /// <summary>
            /// Fills the bottom left quadrent with 3 horizental bars in Red, Green and Blue with a alpha gradient from left (transparent) to right (solid).
            /// </summary>
            /// <param name="pixels"></param>
            private static void TransparentGradients(Buffer2D<TPixel> pixels)
            {
                // topLeft
                int left = 0;
                int right = pixels.Width / 2;
                int top = pixels.Height / 2;
                int bottom = pixels.Height;
                int height = (int)Math.Ceiling(pixels.Height / 6f);

                Vector4 red = Rgba32.Red.ToVector4(); // use real color so we can see har it translates in the test pattern
                Vector4 green = Rgba32.Green.ToVector4(); // use real color so we can see har it translates in the test pattern
                Vector4 blue = Rgba32.Blue.ToVector4(); // use real color so we can see har it translates in the test pattern

                TPixel c = default(TPixel);

                for (int x = left; x < right; x++)
                {
                    blue.W = red.W = green.W = (float)x / (float)right;

                    c.PackFromVector4(red);
                    int topBand = top;
                    for (int y = topBand; y < top + height; y++)
                    {
                        pixels[x, y] = c;
                    }
                    topBand = topBand + height;
                    c.PackFromVector4(green);
                    for (int y = topBand; y < topBand + height; y++)
                    {
                        pixels[x, y] = c;
                    }
                    topBand = topBand + height;
                    c.PackFromVector4(blue);
                    for (int y = topBand; y < bottom; y++)
                    {
                        pixels[x, y] = c;
                    }
                }
            }

            /// <summary>
            /// Fills the bottom right quadrant with all the colors producable by converting itterating over a uint and unpacking it.
            /// A better algorithm could be used but it works
            /// </summary>
            /// <param name="pixels"></param>
            private static void Rainbow(Buffer2D<TPixel> pixels)
            {
                int left = pixels.Width / 2;
                int right = pixels.Width;
                int top = pixels.Height / 2;
                int bottom = pixels.Height;

                int pixelCount = left * top;
                uint stepsPerPixel = (uint)(uint.MaxValue / pixelCount);
                TPixel c = default;
                Rgba32 t = new Rgba32(0);

                for (int x = left; x < right; x++)
                    for (int y = top; y < bottom; y++)
                    {
                        t.PackedValue += stepsPerPixel;
                        Vector4 v = t.ToVector4();
                        //v.W = (x - left) / (float)left;
                        c.PackFromVector4(v);
                        pixels[x, y] = c;
                    }
            }
        }
    }
}