// <copyright file="BlankProvider.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;

    public abstract partial class TestImageProvider<TColor>
        where TColor : struct, IPixel<TColor>
    {
        private class TestPatternProvider : TestImageProvider<TColor>
        {
            static Dictionary<string, Image<TColor>> testImages = new Dictionary<string, Image<TColor>>();

            public TestPatternProvider(int width, int height)
            {
                this.Width = width;
                this.Height = height;
            }

            public override string SourceFileOrDescription => $"TestPattern{this.Width}x{this.Height}";

            protected int Height { get; }

            protected int Width { get; }

            public override Image<TColor> GetImage()
            {
                lock (testImages)
                {
                    if (!testImages.ContainsKey(this.SourceFileOrDescription))
                    {
                        Image<TColor> image = new Image<TColor>(this.Width, this.Height);
                        DrawTestPattern(image);
                        testImages.Add(this.SourceFileOrDescription, image);
                    }

                    return new Image<TColor>(testImages[this.SourceFileOrDescription]);
                }
            }

            private static void DrawTestPattern(Image<TColor> image)
            {
                // first lets split the image into 4 quadrants
                using (PixelAccessor<TColor> pixels = image.Lock())
                {
                    BlackWhiteChecker(pixels); // top left
                    HorizontalLines(pixels); // top right
                    TransparentGradients(pixels); // bottom left
                    Raninbow(pixels); // bottom right 
                }
            }

            private static void HorizontalLines(PixelAccessor<TColor> pixels)
            {
                // topLeft 
                int left = pixels.Width / 2;
                int right = pixels.Width;
                int top = 0;
                int bottom = pixels.Height / 2;
                int stride = pixels.Width / 12;
                TColor[] c = {
                        NamedColors<TColor>.HotPink,
                        NamedColors<TColor>.Blue
                    };
                int p = 0;
                for (int y = top; y < bottom; y++)
                {
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

            private static void BlackWhiteChecker(PixelAccessor<TColor> pixels)
            {
                // topLeft 
                int left = 0;
                int right = pixels.Width / 2;
                int top = 0;
                int bottom = pixels.Height / 2;
                int stride = pixels.Width / 6;
                TColor[] c = {
                        NamedColors<TColor>.Black,
                        NamedColors<TColor>.White
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

            private static void TransparentGradients(PixelAccessor<TColor> pixels)
            {
                // topLeft 
                int left = 0;
                int right = pixels.Width / 2;
                int top = pixels.Height / 2;
                int bottom = pixels.Height;
                int height = (int)Math.Ceiling(pixels.Height / 6f);

                Vector4 red = Color.Red.ToVector4(); // use real color so we can see har it translates in the test pattern
                Vector4 green = Color.Green.ToVector4(); // use real color so we can see har it translates in the test pattern
                Vector4 blue = Color.Blue.ToVector4(); // use real color so we can see har it translates in the test pattern

                TColor c = default(TColor);

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
            private static void Raninbow(PixelAccessor<TColor> pixels)
            {
                int left = pixels.Width / 2;
                int right = pixels.Width;
                int top = pixels.Height / 2;
                int bottom = pixels.Height;

                int pixelCount = left * top;
                uint stepsPerPixel = (uint)(uint.MaxValue / pixelCount);
                TColor c = default(TColor);
                Color t = new Color(0);

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