// <copyright file="ColorConversionTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Drawing
{
    using Drawing;
    using ImageSharp.Drawing;
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Numerics;
    using Xunit;
    using ImageSharp.Drawing.Brushes;
    using SixLabors.Shapes;

    public class SolidPolygonTests : FileTestBase
    {
        [Fact]
        public void ImageShouldBeOverlayedByFilledPolygon()
        {
            string path = this.CreateOutputDirectory("Drawing", "FilledPolygons");
            Vector2[] simplePath = new[] {
                            new Vector2(10, 10),
                            new Vector2(200, 150),
                            new Vector2(50, 300)
            };

            using (Image image = new Image(500, 500))
            {
                using (FileStream output = File.OpenWrite($"{path}/Simple.png"))
                {
                    image
                        .FillPolygon(Color32.HotPink, simplePath, new GraphicsOptions(true))
                        .Save(output);
                }

                using (PixelAccessor<Color32> sourcePixels = image.Lock())
                {
                    Assert.Equal(Color32.HotPink, sourcePixels[81, 145]);
                }
            }
        }

        [Fact]
        public void ImageShouldBeOverlayedByFilledPolygonWithPattern()
        {
            string path = this.CreateOutputDirectory("Drawing", "FilledPolygons");
            Vector2[] simplePath = new[] {
                            new Vector2(10, 10),
                            new Vector2(200, 150),
                            new Vector2(50, 300)
            };

            using (Image image = new Image(500, 500))
            {
                using (FileStream output = File.OpenWrite($"{path}/Pattern.png"))
                {
                    image
                        .FillPolygon(Brushes.Horizontal(Color32.HotPink), simplePath, new GraphicsOptions(true))
                        .Save(output);
                }

                using (PixelAccessor<Color32> sourcePixels = image.Lock())
                {
                    Assert.Equal(Color32.HotPink, sourcePixels[81, 145]);
                }
            }
        }

        [Fact]
        public void ImageShouldBeOverlayedByFilledPolygonNoAntialias()
        {
            string path = this.CreateOutputDirectory("Drawing", "FilledPolygons");
            Vector2[] simplePath = new[] {
                            new Vector2(10, 10),
                            new Vector2(200, 150),
                            new Vector2(50, 300)
            };

            using (Image image = new Image(500, 500))
            using (FileStream output = File.OpenWrite($"{path}/Simple_NoAntialias.png"))
            {
                image
                    .BackgroundColor(Color32.Blue)
                    .FillPolygon(Color32.HotPink, simplePath, new GraphicsOptions(false))
                    .Save(output);

                using (PixelAccessor<Color32> sourcePixels = image.Lock())
                {
                    Assert.Equal(Color32.HotPink, sourcePixels[11, 11]);

                    Assert.Equal(Color32.HotPink, sourcePixels[199, 150]);

                    Assert.Equal(Color32.HotPink, sourcePixels[50, 50]);

                    Assert.Equal(Color32.Blue, sourcePixels[2, 2]);
                }
            }
        }

        [Fact]
        public void ImageShouldBeOverlayedByFilledPolygonImage()
        {
            string path = this.CreateOutputDirectory("Drawing", "FilledPolygons");
            Vector2[] simplePath = new[] {
                            new Vector2(10, 10),
                            new Vector2(200, 150),
                            new Vector2(50, 300)
            };

            using (Image brushImage = TestFile.Create(TestImages.Bmp.Car).CreateImage())
            using (Image image = new Image(500, 500))
            using (FileStream output = File.OpenWrite($"{path}/Image.png"))
            {
                ImageBrush brush = new ImageBrush(brushImage);

                image
                .BackgroundColor(Color32.Blue)
                .FillPolygon(brush, simplePath)
                .Save(output);
            }
        }

        [Fact]
        public void ImageShouldBeOverlayedByFilledPolygonOpacity()
        {
            string path = this.CreateOutputDirectory("Drawing", "FilledPolygons");
            Vector2[] simplePath = new[] {
                            new Vector2(10, 10),
                            new Vector2(200, 150),
                            new Vector2(50, 300)
            };
            Color32 color = new Color32(Color32.HotPink.R, Color32.HotPink.G, Color32.HotPink.B, 150);

            using (Image image = new Image(500, 500))
            {
                using (FileStream output = File.OpenWrite($"{path}/Opacity.png"))
                {
                    image
                        .BackgroundColor(Color32.Blue)
                        .FillPolygon(color, simplePath)
                        .Save(output);
                }

                //shift background color towards forground color by the opacity amount
                Color32 mergedColor = new Color32(Vector4.Lerp(Color32.Blue.ToVector4(), Color32.HotPink.ToVector4(), 150f / 255f));

                using (PixelAccessor<Color32> sourcePixels = image.Lock())
                {
                    Assert.Equal(Color32.Blue, sourcePixels[2, 2]);
                }
            }
        }

        [Fact]
        public void ImageShouldBeOverlayedByFilledRectangle()
        {
            string path = this.CreateOutputDirectory("Drawing", "FilledPolygons");

            using (Image image = new Image(500, 500))
            {
                using (FileStream output = File.OpenWrite($"{path}/Rectangle.png"))
                {
                    image
                        .BackgroundColor(Color32.Blue)
                        .Fill(Color32.HotPink, new SixLabors.Shapes.Rectangle(10, 10, 190, 140))
                         .Save(output);
                }

                using (PixelAccessor<Color32> sourcePixels = image.Lock())
                {
                    Assert.Equal(Color32.HotPink, sourcePixels[11, 11]);

                    Assert.Equal(Color32.HotPink, sourcePixels[198, 10]);

                    Assert.Equal(Color32.HotPink, sourcePixels[10, 50]);

                    Assert.Equal(Color32.HotPink, sourcePixels[50, 50]);

                    Assert.Equal(Color32.Blue, sourcePixels[2, 2]);
                }
            }
        }

        [Fact]
        public void ImageShouldBeOverlayedByFilledTriangle()
        {
            string path = this.CreateOutputDirectory("Drawing", "FilledPolygons");

            using (Image image = new Image(100, 100))
            {
                using (FileStream output = File.OpenWrite($"{path}/Triangle.png"))
                {
                    image
                        .BackgroundColor(Color32.Blue)
                        .Fill(Color32.HotPink, new RegularPolygon(50, 50, 3, 30))
                         .Save(output);
                }

                using (PixelAccessor<Color32> sourcePixels = image.Lock())
                {
                    Assert.Equal(Color32.Blue, sourcePixels[30, 65]);

                    Assert.Equal(Color32.HotPink, sourcePixels[50, 50]);
                }
            }
        }

        [Fact]
        public void ImageShouldBeOverlayedByFilledSeptagon()
        {
            string path = this.CreateOutputDirectory("Drawing", "FilledPolygons");

            Configuration config = Configuration.CreateDefaultInstance();
            config.ParallelOptions.MaxDegreeOfParallelism = 1;
            using (Image image = new Image(100, 100, config))
            {
                using (FileStream output = File.OpenWrite($"{path}/Septagon.png"))
                {
                    image
                        .BackgroundColor(Color32.Blue)
                        .Fill(Color32.HotPink, new RegularPolygon(50, 50, 7, 30, -(float)Math.PI))
                         .Save(output);
                }
            }
        }

        [Fact]
        public void ImageShouldBeOverlayedByFilledEllipse()
        {
            string path = this.CreateOutputDirectory("Drawing", "FilledPolygons");

            Configuration config = Configuration.CreateDefaultInstance();
            config.ParallelOptions.MaxDegreeOfParallelism = 1;
            using (Image image = new Image(100, 100, config))
            {
                using (FileStream output = File.OpenWrite($"{path}/ellipse.png"))
                {
                    image
                        .BackgroundColor(Color32.Blue)
                        .Fill(Color32.HotPink, new Ellipse(50, 50, 30, 50)
                                                .Rotate((float)(Math.PI / 3)))
                         .Save(output);
                }
            }
        }

        [Fact]
        public void ImageShouldBeOverlayedBySquareWithCornerClipped()
        {
            string path = this.CreateOutputDirectory("Drawing", "FilledPolygons");

            Configuration config = Configuration.CreateDefaultInstance();
            config.ParallelOptions.MaxDegreeOfParallelism = 1;
            using (Image image = new Image(200, 200, config))
            {
                using (FileStream output = File.OpenWrite($"{path}/clipped-corner.png"))
                {
                    image
                        .Fill(Color32.Blue)
                        .FillPolygon(Color32.HotPink, new[]
                        {
                            new Vector2( 8, 8 ),
                            new Vector2( 64, 8 ),
                            new Vector2( 64, 64 ),
                            new Vector2( 120, 64 ),
                            new Vector2( 120, 120 ),
                            new Vector2( 8, 120 )
                        } )
                         .Save(output);
                }
            }
        }
    }
}
