// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Primitives;
using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    public class RecolorImageTest : FileTestBase
    {
        [Fact]
        public void ImageShouldRecolorYellowToHotPink()
        {
            string path = TestEnvironment.CreateOutputDirectory("Drawing", "RecolorImage");

            var brush = new RecolorBrush<Rgba32>(Rgba32.Yellow, Rgba32.HotPink, 0.2f);

            foreach (TestFile file in Files)
            {
                using (Image<Rgba32> image = file.CreateImage())
                {
                    image.Mutate(x => x.Fill(brush));
                    image.Save($"{path}/{file.FileName}");
                }
            }
        }

        [Fact]
        public void ImageShouldRecolorYellowToHotPinkInARectangle()
        {
            string path = TestEnvironment.CreateOutputDirectory("Drawing", "RecolorImage");

            var brush = new RecolorBrush<Rgba32>(Rgba32.Yellow, Rgba32.HotPink, 0.2f);

            foreach (TestFile file in Files)
            {
                using (Image<Rgba32> image = file.CreateImage())
                {
                    int imageHeight = image.Height;
                    image.Mutate(x => x.Fill(brush, new Rectangle(0, imageHeight / 2 - imageHeight / 4, image.Width, imageHeight / 2)));
                    image.Save($"{path}/Shaped_{file.FileName}");
                }
            }
        }
    }
}