// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Shapes;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Drawing
{
    public class SolidPolygonTests : FileTestBase
    {
        [Fact]
        public void ImageShouldBeOverlayedByFilledPolygon()
        {
            string path = TestEnvironment.CreateOutputDirectory("Drawing", "FilledPolygons");
            SixLabors.Primitives.PointF[] simplePath = {
                            new Vector2(10, 10),
                            new Vector2(200, 150),
                            new Vector2(50, 300)
            };

            using (var image = new Image<Rgba32>(500, 500))
            {
                image.Mutate(x => x.FillPolygon(new GraphicsOptions(true), Rgba32.HotPink, simplePath));
                image.Save($"{path}/Simple.png");

                Buffer2D<Rgba32> sourcePixels = image.GetRootFramePixelBuffer();
                Assert.Equal(Rgba32.HotPink, sourcePixels[81, 145]);
            }
        }

        [Fact]
        public void ImageShouldBeOverlayedByFilledPolygonWithPattern()
        {
            string path = TestEnvironment.CreateOutputDirectory("Drawing", "FilledPolygons");
            var simplePath = new SixLabors.Primitives.PointF[] {
                            new Vector2(10, 10),
                            new Vector2(200, 150),
                            new Vector2(50, 300)
            };

            using (var image = new Image<Rgba32>(500, 500))
            {
                image.Mutate(
                    x => x.FillPolygon(new GraphicsOptions(true), Brushes.Horizontal(Rgba32.HotPink), simplePath));
                image.Save($"{path}/Pattern.png");

                Buffer2D<Rgba32> sourcePixels = image.GetRootFramePixelBuffer();
                Assert.Equal(Rgba32.HotPink, sourcePixels[81, 145]);
            }
        }

        [Fact]
        public void ImageShouldBeOverlayedByFilledPolygonNoAntialias()
        {
            string path = TestEnvironment.CreateOutputDirectory("Drawing", "FilledPolygons");
            var simplePath = new SixLabors.Primitives.PointF[] {
                            new Vector2(10, 10),
                            new Vector2(200, 150),
                            new Vector2(50, 300)
            };

            using (var image = new Image<Rgba32>(500, 500))
            {
                image.Mutate(
                    x => x.BackgroundColor(Rgba32.Blue).FillPolygon(
                        new GraphicsOptions(false),
                        Rgba32.HotPink,
                        simplePath));
                image.Save($"{path}/Simple_NoAntialias.png");

                Buffer2D<Rgba32> sourcePixels = image.GetRootFramePixelBuffer();
                Assert.True(Rgba32.HotPink == sourcePixels[11, 11], "[11, 11] wrong");

                Assert.True(Rgba32.HotPink == sourcePixels[199, 149], "[199, 149] wrong");

                Assert.True(Rgba32.HotPink == sourcePixels[50, 50], "[50, 50] wrong");

                Assert.True(Rgba32.Blue == sourcePixels[2, 2], "[2, 2] wrong");
            }
        }

        [Fact]
        public void ImageShouldBeOverlayedByFilledPolygonImage()
        {
            string path = TestEnvironment.CreateOutputDirectory("Drawing", "FilledPolygons");
            var simplePath = new SixLabors.Primitives.PointF[] {
                            new Vector2(10, 10),
                            new Vector2(200, 150),
                            new Vector2(50, 300)
            };

            using (Image<Rgba32> brushImage = TestFile.Create(TestImages.Bmp.Car).CreateImage())
            using (var image = new Image<Rgba32>(500, 500))
            {
                var brush = new ImageBrush<Rgba32>(brushImage);

                image.Mutate(x => x
                    .BackgroundColor(Rgba32.Blue)
                    .FillPolygon(brush, simplePath));
                image.Save($"{path}/Image.png");
            }
        }

        [Fact]
        public void ImageShouldBeOverlayedByFilledPolygonOpacity()
        {
            string path = TestEnvironment.CreateOutputDirectory("Drawing", "FilledPolygons");
            var simplePath = new SixLabors.Primitives.PointF[] {
                            new Vector2(10, 10),
                            new Vector2(200, 150),
                            new Vector2(50, 300)
            };
            var color = new Rgba32(Rgba32.HotPink.R, Rgba32.HotPink.G, Rgba32.HotPink.B, 150);

            using (var image = new Image<Rgba32>(500, 500))
            {
                image.Mutate(x => x.BackgroundColor(Rgba32.Blue).FillPolygon(color, simplePath));
                image.Save($"{path}/Opacity.png");

                //shift background color towards forground color by the opacity amount
                var mergedColor = new Rgba32(
                    Vector4.Lerp(Rgba32.Blue.ToVector4(), Rgba32.HotPink.ToVector4(), 150f / 255f));

                Buffer2D<Rgba32> sourcePixels = image.GetRootFramePixelBuffer();
                Assert.Equal(Rgba32.Blue, sourcePixels[2, 2]);
            }
        }

        [Fact]
        public void ImageShouldBeOverlayedByFilledRectangle()
        {
            string path = TestEnvironment.CreateOutputDirectory("Drawing", "FilledPolygons");

            using (var image = new Image<Rgba32>(500, 500))
            {
                image.Mutate(
                    x => x.BackgroundColor(Rgba32.Blue).Fill(
                        Rgba32.HotPink,
                        new SixLabors.Shapes.RectangularPolygon(10, 10, 190, 140)));
                image.Save($"{path}/Rectangle.png");

                Buffer2D<Rgba32> sourcePixels = image.GetRootFramePixelBuffer();
                Assert.Equal(Rgba32.HotPink, sourcePixels[11, 11]);

                Assert.Equal(Rgba32.HotPink, sourcePixels[198, 10]);

                Assert.Equal(Rgba32.HotPink, sourcePixels[10, 50]);

                Assert.Equal(Rgba32.HotPink, sourcePixels[50, 50]);

                Assert.Equal(Rgba32.Blue, sourcePixels[2, 2]);
            }
        }

        [Fact]
        public void ImageShouldBeOverlayedByFilledTriangle()
        {
            string path = TestEnvironment.CreateOutputDirectory("Drawing", "FilledPolygons");

            using (var image = new Image<Rgba32>(100, 100))
            {
                image.Mutate(
                    x => x.BackgroundColor(Rgba32.Blue).Fill(Rgba32.HotPink, new RegularPolygon(50, 50, 3, 30)));
                image.Save($"{path}/Triangle.png");

                Buffer2D<Rgba32> sourcePixels = image.GetRootFramePixelBuffer();
                Assert.Equal(Rgba32.Blue, sourcePixels[30, 65]);

                Assert.Equal(Rgba32.HotPink, sourcePixels[50, 50]);
            }
        }

        [Fact]
        public void ImageShouldBeOverlayedByFilledSeptagon()
        {
            string path = TestEnvironment.CreateOutputDirectory("Drawing", "FilledPolygons");

            var config = Configuration.CreateDefaultInstance();
            config.MaxDegreeOfParallelism = 1;
            using (var image = new Image<Rgba32>(config, 100, 100))
            {
                image.Mutate(x => x
                    .BackgroundColor(Rgba32.Blue)
                    .Fill(Rgba32.HotPink, new RegularPolygon(50, 50, 7, 30, -(float)Math.PI)));
                image.Save($"{path}/Septagon.png");
            }
        }

        [Fact]
        public void ImageShouldBeOverlayedByFilledEllipse()
        {
            string path = TestEnvironment.CreateOutputDirectory("Drawing", "FilledPolygons");

            var config = Configuration.CreateDefaultInstance();
            config.MaxDegreeOfParallelism = 1;
            using (var image = new Image<Rgba32>(config, 100, 100))
            {
                image.Mutate(x => x
                    .BackgroundColor(Rgba32.Blue)
                    .Fill(Rgba32.HotPink, new EllipsePolygon(50, 50, 30, 50)
                    .Rotate((float)(Math.PI / 3))));
                image.Save($"{path}/ellipse.png");
            }
        }

        [Fact]
        public void ImageShouldBeOverlayedBySquareWithCornerClipped()
        {
            string path = TestEnvironment.CreateOutputDirectory("Drawing", "FilledPolygons");

            var config = Configuration.CreateDefaultInstance();
            config.MaxDegreeOfParallelism = 1;
            using (var image = new Image<Rgba32>(config, 200, 200))
            {
                image.Mutate(x => x
                    .Fill(Rgba32.Blue)
                    .FillPolygon(Rgba32.HotPink, new SixLabors.Primitives.PointF[]
                    {
                            new Vector2( 8, 8 ),
                            new Vector2( 64, 8 ),
                            new Vector2( 64, 64 ),
                            new Vector2( 120, 64 ),
                            new Vector2( 120, 120 ),
                            new Vector2( 8, 120 )
                    }));
                image.Save($"{path}/clipped-corner.png");
            }
        }
    }
}
