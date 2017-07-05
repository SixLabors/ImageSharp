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
    using ImageSharp.PixelFormats;

    using SixLabors.Shapes;

    public class SolidPolygonTests : FileTestBase
    {
        [Fact]
        public void ImageShouldBeOverlayedByFilledPolygon()
        {
            string path = this.CreateOutputDirectory("Drawing", "FilledPolygons");
            SixLabors.Primitives.PointF[] simplePath = new SixLabors.Primitives.PointF[] {
                            new Vector2(10, 10),
                            new Vector2(200, 150),
                            new Vector2(50, 300)
            };

            using (Image<Rgba32> image = new Image<Rgba32>(500, 500))
            {
                image
                    .FillPolygon(Rgba32.HotPink, simplePath, new GraphicsOptions(true))
                    .Save($"{path}/Simple.png");

                using (PixelAccessor<Rgba32> sourcePixels = image.Lock())
                {
                    Assert.Equal(Rgba32.HotPink, sourcePixels[81, 145]);
                }
            }
        }

        [Fact]
        public void ImageShouldBeOverlayedByFilledPolygonWithPattern()
        {
            string path = this.CreateOutputDirectory("Drawing", "FilledPolygons");
            SixLabors.Primitives.PointF[] simplePath = new SixLabors.Primitives.PointF[] {
                            new Vector2(10, 10),
                            new Vector2(200, 150),
                            new Vector2(50, 300)
            };

            using (Image<Rgba32> image = new Image<Rgba32>(500, 500))
            {
                image
                    .FillPolygon(Brushes.Horizontal(Rgba32.HotPink), simplePath, new GraphicsOptions(true))
                    .Save($"{path}/Pattern.png");

                using (PixelAccessor<Rgba32> sourcePixels = image.Lock())
                {
                    Assert.Equal(Rgba32.HotPink, sourcePixels[81, 145]);
                }
            }
        }

        [Fact]
        public void ImageShouldBeOverlayedByFilledPolygonNoAntialias()
        {
            string path = this.CreateOutputDirectory("Drawing", "FilledPolygons");
            SixLabors.Primitives.PointF[] simplePath = new SixLabors.Primitives.PointF[] {
                            new Vector2(10, 10),
                            new Vector2(200, 150),
                            new Vector2(50, 300)
            };

            using (Image<Rgba32> image = new Image<Rgba32>(500, 500))
            {
                image
                    .BackgroundColor(Rgba32.Blue)
                    .FillPolygon(Rgba32.HotPink, simplePath, new GraphicsOptions(false))
                    .Save($"{path}/Simple_NoAntialias.png");

                using (PixelAccessor<Rgba32> sourcePixels = image.Lock())
                {
                    Assert.Equal(Rgba32.HotPink, sourcePixels[11, 11]);

                    Assert.Equal(Rgba32.HotPink, sourcePixels[199, 150]);

                    Assert.Equal(Rgba32.HotPink, sourcePixels[50, 50]);

                    Assert.Equal(Rgba32.Blue, sourcePixels[2, 2]);
                }
            }
        }

        [Fact]
        public void ImageShouldBeOverlayedByFilledPolygonImage()
        {
            string path = this.CreateOutputDirectory("Drawing", "FilledPolygons");
            SixLabors.Primitives.PointF[] simplePath = new SixLabors.Primitives.PointF[] {
                            new Vector2(10, 10),
                            new Vector2(200, 150),
                            new Vector2(50, 300)
            };

            using (Image<Rgba32> brushImage = TestFile.Create(TestImages.Bmp.Car).CreateImage())
            using (Image<Rgba32> image = new Image<Rgba32>(500, 500))
            {
                ImageBrush<Rgba32> brush = new ImageBrush<Rgba32>(brushImage);

                image
                .BackgroundColor(Rgba32.Blue)
                .FillPolygon(brush, simplePath)
                .Save($"{path}/Image.png");
            }
        }

        [Fact]
        public void ImageShouldBeOverlayedByFilledPolygonOpacity()
        {
            string path = this.CreateOutputDirectory("Drawing", "FilledPolygons");
            SixLabors.Primitives.PointF[] simplePath = new SixLabors.Primitives.PointF[] {
                            new Vector2(10, 10),
                            new Vector2(200, 150),
                            new Vector2(50, 300)
            };
            Rgba32 color = new Rgba32(Rgba32.HotPink.R, Rgba32.HotPink.G, Rgba32.HotPink.B, 150);

            using (Image<Rgba32> image = new Image<Rgba32>(500, 500))
            {
                image
                    .BackgroundColor(Rgba32.Blue)
                    .FillPolygon(color, simplePath)
                    .Save($"{path}/Opacity.png");

                //shift background color towards forground color by the opacity amount
                Rgba32 mergedColor = new Rgba32(Vector4.Lerp(Rgba32.Blue.ToVector4(), Rgba32.HotPink.ToVector4(), 150f / 255f));

                using (PixelAccessor<Rgba32> sourcePixels = image.Lock())
                {
                    Assert.Equal(Rgba32.Blue, sourcePixels[2, 2]);
                }
            }
        }

        [Fact]
        public void ImageShouldBeOverlayedByFilledRectangle()
        {
            string path = this.CreateOutputDirectory("Drawing", "FilledPolygons");

            using (Image<Rgba32> image = new Image<Rgba32>(500, 500))
            {
                image
                    .BackgroundColor(Rgba32.Blue)
                    .Fill(Rgba32.HotPink, new SixLabors.Shapes.RectangularePolygon(10, 10, 190, 140))
                     .Save($"{path}/Rectangle.png");

                using (PixelAccessor<Rgba32> sourcePixels = image.Lock())
                {
                    Assert.Equal(Rgba32.HotPink, sourcePixels[11, 11]);

                    Assert.Equal(Rgba32.HotPink, sourcePixels[198, 10]);

                    Assert.Equal(Rgba32.HotPink, sourcePixels[10, 50]);

                    Assert.Equal(Rgba32.HotPink, sourcePixels[50, 50]);

                    Assert.Equal(Rgba32.Blue, sourcePixels[2, 2]);
                }
            }
        }

        [Fact]
        public void ImageShouldBeOverlayedByFilledTriangle()
        {
            string path = this.CreateOutputDirectory("Drawing", "FilledPolygons");

            using (Image<Rgba32> image = new Image<Rgba32>(100, 100))
            {
                image
                    .BackgroundColor(Rgba32.Blue)
                    .Fill(Rgba32.HotPink, new RegularPolygon(50, 50, 3, 30))
                     .Save($"{path}/Triangle.png");

                using (PixelAccessor<Rgba32> sourcePixels = image.Lock())
                {
                    Assert.Equal(Rgba32.Blue, sourcePixels[30, 65]);

                    Assert.Equal(Rgba32.HotPink, sourcePixels[50, 50]);
                }
            }
        }

        [Fact]
        public void ImageShouldBeOverlayedByFilledSeptagon()
        {
            string path = this.CreateOutputDirectory("Drawing", "FilledPolygons");

            Configuration config = Configuration.CreateDefaultInstance();
            config.ParallelOptions.MaxDegreeOfParallelism = 1;
            using (Image<Rgba32> image = new Image<Rgba32>(config, 100, 100))
            {
                image
                    .BackgroundColor(Rgba32.Blue)
                    .Fill(Rgba32.HotPink, new RegularPolygon(50, 50, 7, 30, -(float)Math.PI))
                     .Save($"{path}/Septagon.png");
            }
        }

        [Fact]
        public void ImageShouldBeOverlayedByFilledEllipse()
        {
            string path = this.CreateOutputDirectory("Drawing", "FilledPolygons");

            Configuration config = Configuration.CreateDefaultInstance();
            config.ParallelOptions.MaxDegreeOfParallelism = 1;
            using (Image<Rgba32> image = new Image<Rgba32>(config, 100, 100))
            {
                image
                    .BackgroundColor(Rgba32.Blue)
                    .Fill(Rgba32.HotPink, new EllipsePolygon(50, 50, 30, 50)
                    .Rotate((float)(Math.PI / 3)))
                    .Save($"{path}/ellipse.png");
            }
        }

        [Fact]
        public void ImageShouldBeOverlayedBySquareWithCornerClipped()
        {
            string path = this.CreateOutputDirectory("Drawing", "FilledPolygons");

            Configuration config = Configuration.CreateDefaultInstance();
            config.ParallelOptions.MaxDegreeOfParallelism = 1;
            using (Image<Rgba32> image = new Image<Rgba32>(config, 200, 200))
            {
                image
                    .Fill(Rgba32.Blue)
                    .FillPolygon(Rgba32.HotPink, new SixLabors.Primitives.PointF[]
                    {
                            new Vector2( 8, 8 ),
                            new Vector2( 64, 8 ),
                            new Vector2( 64, 64 ),
                            new Vector2( 120, 64 ),
                            new Vector2( 120, 120 ),
                            new Vector2( 8, 120 )
                    })
                     .Save($"{path}/clipped-corner.png");
            }
        }
    }
}
