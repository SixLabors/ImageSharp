// <copyright file="ColorConversionTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Drawing
{
    using System;
    using System.IO;

    using ImageSharp.Drawing;
    using ImageSharp.Drawing.Brushes;

    using Xunit;

    public class FillPatternBrushTests : FileTestBase
    {
        private void Test(string name, Color32 background, IBrush<Color32> brush, Color32[,] expectedPattern)
        {
            string path = this.CreateOutputDirectory("Fill", "PatternBrush");
            using (Image image = new Image(20, 20))
            {
                image
                    .Fill(background)
                    .Fill(brush);

                using (FileStream output = File.OpenWrite($"{path}/{name}.png"))
                {
                    image.Save(output);
                }

                using (PixelAccessor<Color32> sourcePixels = image.Lock())
                {
                    // lets pick random spots to start checking
                    Random r = new Random();
                    Fast2DArray<Color32> expectedPatternFast = new Fast2DArray<Color32>(expectedPattern);
                    int xStride = expectedPatternFast.Width;
                    int yStride = expectedPatternFast.Height;
                    int offsetX = r.Next(image.Width / xStride) * xStride;
                    int offsetY = r.Next(image.Height / yStride) * yStride;
                    for (int x = 0; x < xStride; x++)
                    {
                        for (int y = 0; y < yStride; y++)
                        {
                            int actualX = x + offsetX;
                            int actualY = y + offsetY;
                            Color32 expected = expectedPatternFast[y, x]; // inverted pattern
                            Color32 actual = sourcePixels[actualX, actualY];
                            if (expected != actual)
                            {
                                Assert.True(false, $"Expected {expected} but found {actual} at ({actualX},{actualY})");
                            }
                        }
                    }
                }
                using (FileStream output = File.OpenWrite($"{path}/{name}x4.png"))
                {
                    image.Resize(80, 80).Save(output);
                }
            }
        }

        [Fact]
        public void ImageShouldBeFloodFilledWithPercent10()
        {
            this.Test("Percent10", Color32.Blue, Brushes.Percent10(Color32.HotPink, Color32.LimeGreen),
                new[,]
                {
                { Color32.HotPink , Color32.LimeGreen, Color32.LimeGreen, Color32.LimeGreen},
                { Color32.LimeGreen, Color32.LimeGreen, Color32.LimeGreen, Color32.LimeGreen},
                { Color32.LimeGreen, Color32.LimeGreen, Color32.HotPink , Color32.LimeGreen},
                { Color32.LimeGreen, Color32.LimeGreen, Color32.LimeGreen, Color32.LimeGreen}
            });
        }

        [Fact]
        public void ImageShouldBeFloodFilledWithPercent10Transparent()
        {
            Test("Percent10_Transparent", Color32.Blue, Brushes.Percent10(Color32.HotPink),
            new Color32[,] {
                { Color32.HotPink , Color32.Blue, Color32.Blue, Color32.Blue},
                { Color32.Blue, Color32.Blue, Color32.Blue, Color32.Blue},
                { Color32.Blue, Color32.Blue, Color32.HotPink , Color32.Blue},
                { Color32.Blue, Color32.Blue, Color32.Blue, Color32.Blue}
            });
        }

        [Fact]
        public void ImageShouldBeFloodFilledWithPercent20()
        {
            Test("Percent20", Color32.Blue, Brushes.Percent20(Color32.HotPink, Color32.LimeGreen),
           new Color32[,] {
                { Color32.HotPink , Color32.LimeGreen, Color32.LimeGreen, Color32.LimeGreen},
                { Color32.LimeGreen, Color32.LimeGreen, Color32.HotPink , Color32.LimeGreen},
                { Color32.HotPink , Color32.LimeGreen, Color32.LimeGreen, Color32.LimeGreen},
                { Color32.LimeGreen, Color32.LimeGreen, Color32.HotPink , Color32.LimeGreen}
           });
        }

        [Fact]
        public void ImageShouldBeFloodFilledWithPercent20_transparent()
        {
            Test("Percent20_Transparent", Color32.Blue, Brushes.Percent20(Color32.HotPink),
           new Color32[,] {
                { Color32.HotPink , Color32.Blue, Color32.Blue, Color32.Blue},
                { Color32.Blue, Color32.Blue, Color32.HotPink , Color32.Blue},
                { Color32.HotPink , Color32.Blue, Color32.Blue, Color32.Blue},
                { Color32.Blue, Color32.Blue, Color32.HotPink , Color32.Blue}
           });
        }

        [Fact]
        public void ImageShouldBeFloodFilledWithHorizontal()
        {
            Test("Horizontal", Color32.Blue, Brushes.Horizontal(Color32.HotPink, Color32.LimeGreen),
           new Color32[,] {
                { Color32.LimeGreen , Color32.LimeGreen, Color32.LimeGreen, Color32.LimeGreen},
                { Color32.HotPink, Color32.HotPink, Color32.HotPink , Color32.HotPink},
                { Color32.LimeGreen , Color32.LimeGreen, Color32.LimeGreen, Color32.LimeGreen},
                { Color32.LimeGreen, Color32.LimeGreen, Color32.LimeGreen , Color32.LimeGreen}
           });
        }

        [Fact]
        public void ImageShouldBeFloodFilledWithHorizontal_transparent()
        {
            Test("Horizontal_Transparent", Color32.Blue, Brushes.Horizontal(Color32.HotPink),
           new Color32[,] {
                { Color32.Blue , Color32.Blue, Color32.Blue, Color32.Blue},
                { Color32.HotPink, Color32.HotPink, Color32.HotPink , Color32.HotPink},
                { Color32.Blue , Color32.Blue, Color32.Blue, Color32.Blue},
                { Color32.Blue, Color32.Blue, Color32.Blue , Color32.Blue}
           });
        }



        [Fact]
        public void ImageShouldBeFloodFilledWithMin()
        {
            Test("Min", Color32.Blue, Brushes.Min(Color32.HotPink, Color32.LimeGreen),
           new Color32[,] {
                { Color32.LimeGreen , Color32.LimeGreen, Color32.LimeGreen, Color32.LimeGreen},
                { Color32.LimeGreen , Color32.LimeGreen, Color32.LimeGreen, Color32.LimeGreen},
                { Color32.LimeGreen, Color32.LimeGreen, Color32.LimeGreen , Color32.LimeGreen},
                { Color32.HotPink, Color32.HotPink, Color32.HotPink , Color32.HotPink}
           });
        }

        [Fact]
        public void ImageShouldBeFloodFilledWithMin_transparent()
        {
            Test("Min_Transparent", Color32.Blue, Brushes.Min(Color32.HotPink),
           new Color32[,] {
                { Color32.Blue , Color32.Blue, Color32.Blue, Color32.Blue},
                { Color32.Blue , Color32.Blue, Color32.Blue, Color32.Blue},
                { Color32.Blue, Color32.Blue, Color32.Blue , Color32.Blue},
                { Color32.HotPink, Color32.HotPink, Color32.HotPink , Color32.HotPink},
           });
        }

        [Fact]
        public void ImageShouldBeFloodFilledWithVertical()
        {
            Test("Vertical", Color32.Blue, Brushes.Vertical(Color32.HotPink, Color32.LimeGreen),
           new Color32[,] {
                { Color32.LimeGreen, Color32.HotPink, Color32.LimeGreen, Color32.LimeGreen},
                { Color32.LimeGreen, Color32.HotPink, Color32.LimeGreen, Color32.LimeGreen},
                { Color32.LimeGreen, Color32.HotPink, Color32.LimeGreen, Color32.LimeGreen},
                { Color32.LimeGreen, Color32.HotPink, Color32.LimeGreen, Color32.LimeGreen}
           });
        }

        [Fact]
        public void ImageShouldBeFloodFilledWithVertical_transparent()
        {
            Test("Vertical_Transparent", Color32.Blue, Brushes.Vertical(Color32.HotPink),
           new Color32[,] {
                { Color32.Blue, Color32.HotPink, Color32.Blue, Color32.Blue},
                { Color32.Blue, Color32.HotPink, Color32.Blue, Color32.Blue},
                { Color32.Blue, Color32.HotPink, Color32.Blue, Color32.Blue},
                { Color32.Blue, Color32.HotPink, Color32.Blue, Color32.Blue}
           });
        }

        [Fact]
        public void ImageShouldBeFloodFilledWithForwardDiagonal()
        {
            Test("ForwardDiagonal", Color32.Blue, Brushes.ForwardDiagonal(Color32.HotPink, Color32.LimeGreen),
           new Color32[,] {
                { Color32.LimeGreen, Color32.LimeGreen, Color32.LimeGreen, Color32.HotPink},
                { Color32.LimeGreen, Color32.LimeGreen, Color32.HotPink, Color32.LimeGreen},
                { Color32.LimeGreen, Color32.HotPink, Color32.LimeGreen, Color32.LimeGreen},
                { Color32.HotPink, Color32.LimeGreen, Color32.LimeGreen, Color32.LimeGreen}
           });
        }

        [Fact]
        public void ImageShouldBeFloodFilledWithForwardDiagonal_transparent()
        {
            Test("ForwardDiagonal_Transparent", Color32.Blue, Brushes.ForwardDiagonal(Color32.HotPink),
           new Color32[,] {
                { Color32.Blue,    Color32.Blue,    Color32.Blue,    Color32.HotPink},
                { Color32.Blue,    Color32.Blue,    Color32.HotPink, Color32.Blue},
                { Color32.Blue,    Color32.HotPink, Color32.Blue,    Color32.Blue},
                { Color32.HotPink, Color32.Blue,    Color32.Blue,    Color32.Blue}
           });
        }

        [Fact]
        public void ImageShouldBeFloodFilledWithBackwardDiagonal()
        {
            Test("BackwardDiagonal", Color32.Blue, Brushes.BackwardDiagonal(Color32.HotPink, Color32.LimeGreen),
           new Color32[,] {
                { Color32.HotPink,   Color32.LimeGreen, Color32.LimeGreen, Color32.LimeGreen},
                { Color32.LimeGreen, Color32.HotPink,   Color32.LimeGreen, Color32.LimeGreen},
                { Color32.LimeGreen, Color32.LimeGreen, Color32.HotPink,   Color32.LimeGreen},
                { Color32.LimeGreen, Color32.LimeGreen, Color32.LimeGreen, Color32.HotPink}
           });
        }

        [Fact]
        public void ImageShouldBeFloodFilledWithBackwardDiagonal_transparent()
        {
            Test("BackwardDiagonal_Transparent", Color32.Blue, Brushes.BackwardDiagonal(Color32.HotPink),
           new Color32[,] {
                { Color32.HotPink, Color32.Blue,    Color32.Blue,    Color32.Blue},
                { Color32.Blue,    Color32.HotPink, Color32.Blue,    Color32.Blue},
                { Color32.Blue,    Color32.Blue,    Color32.HotPink, Color32.Blue},
                { Color32.Blue,    Color32.Blue,    Color32.Blue,    Color32.HotPink}
           });
        }
    }
}
