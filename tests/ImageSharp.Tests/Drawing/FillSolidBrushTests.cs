// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Numerics;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.Drawing;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Drawing
{
    public class FillSolidBrushTests : FileTestBase
    {
        [Fact]
        public void ImageShouldBeFloodFilledWithColorOnDefaultBackground()
        {
            string path = TestEnvironment.CreateOutputDirectory("Fill", "SolidBrush");
            using (Image<Rgba32> image = new Image<Rgba32>(500, 500))
            {
                image.Mutate(x => x
                    .Fill(Rgba32.HotPink));
                image
                    .Save($"{path}/DefaultBack.png");

                using (PixelAccessor<Rgba32> sourcePixels = image.Lock())
                {
                    Assert.Equal(Rgba32.HotPink, sourcePixels[9, 9]);

                    Assert.Equal(Rgba32.HotPink, sourcePixels[199, 149]);
                }
            }
        }

        [Fact]
        public void ImageShouldBeFloodFilledWithColor()
        {
            string path = TestEnvironment.CreateOutputDirectory("Fill", "SolidBrush");
            using (Image<Rgba32> image = new Image<Rgba32>(500, 500))
            {
                image.Mutate(x => x
                    .BackgroundColor(Rgba32.Blue)
                    .Fill(Rgba32.HotPink));
                image.Save($"{path}/Simple.png");

                using (PixelAccessor<Rgba32> sourcePixels = image.Lock())
                {
                    Assert.Equal(Rgba32.HotPink, sourcePixels[9, 9]);

                    Assert.Equal(Rgba32.HotPink, sourcePixels[199, 149]);
                }
            }
        }

        [Fact]
        public void ImageShouldBeFloodFilledWithColorOpacity()
        {
            string path = TestEnvironment.CreateOutputDirectory("Fill", "SolidBrush");
            using (Image<Rgba32> image = new Image<Rgba32>(500, 500))
            {
                Rgba32 color = new Rgba32(Rgba32.HotPink.R, Rgba32.HotPink.G, Rgba32.HotPink.B, 150);

                image.Mutate(x => x
                    .BackgroundColor(Rgba32.Blue)
                    .Fill(color));
                image.Save($"{path}/Opacity.png");

                //shift background color towards forground color by the opacity amount
                Rgba32 mergedColor = new Rgba32(Vector4.Lerp(Rgba32.Blue.ToVector4(), Rgba32.HotPink.ToVector4(), 150f / 255f));


                using (PixelAccessor<Rgba32> sourcePixels = image.Lock())
                {
                    Assert.Equal(mergedColor, sourcePixels[9, 9]);
                    Assert.Equal(mergedColor, sourcePixels[199, 149]);
                }
            }
        }

    }
}
