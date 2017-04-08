// <copyright file="PngSmokeTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Formats.Png
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.IO;
    using Xunit;
    using ImageSharp.Formats;
    using System.Linq;
    using ImageSharp.IO;

    public class PngSmokeTests
    {
        [Theory]
        [WithTestPatternImages(300, 300, PixelTypes.All)]
        public void GeneralTest<TColor>(TestImageProvider<TColor> provider)
            where TColor : struct, IPixel<TColor>
        {
            // does saving a file then repoening mean both files are identical???
            using (Image<TColor> image = provider.GetImage())
            using (MemoryStream ms = new MemoryStream())
            {
                // image.Save(provider.Utility.GetTestOutputFileName("bmp"));

                image.Save(ms, new PngEncoder());
                ms.Position = 0;
                using (Image img2 = Image.Load(ms, new PngDecoder()))
                {
                    // img2.Save(provider.Utility.GetTestOutputFileName("bmp", "_loaded"), new BmpEncoder());
                    ImageComparer.CheckSimilarity(image, img2);
                }
            }
        }

        [Theory]
        [WithTestPatternImages(100, 100, PixelTypes.All)]
        public void CanSaveIndexedPng<TColor>(TestImageProvider<TColor> provider)
            where TColor : struct, IPixel<TColor>
        {
            // does saving a file then repoening mean both files are identical???
            using (Image<TColor> image = provider.GetImage())
            using (MemoryStream ms = new MemoryStream())
            {
                // image.Save(provider.Utility.GetTestOutputFileName("bmp"));
                image.MetaData.Quality = 256;
                image.Save(ms, new PngEncoder());
                ms.Position = 0;
                using (Image img2 = Image.Load(ms, new PngDecoder()))
                {
                    // img2.Save(provider.Utility.GetTestOutputFileName("bmp", "_loaded"), new BmpEncoder());
                    ImageComparer.CheckSimilarity(image, img2);
                }
            }
        }

        [Theory]
        [WithTestPatternImages(300, 300, PixelTypes.All)]
        public void Resize<TColor>(TestImageProvider<TColor> provider)
            where TColor : struct, IPixel<TColor>
        {
            // does saving a file then repoening mean both files are identical???
            using (Image<TColor> image = provider.GetImage())
            using (MemoryStream ms = new MemoryStream())
            {
                // image.Save(provider.Utility.GetTestOutputFileName("png"));
                image.Resize(100, 100);
                // image.Save(provider.Utility.GetTestOutputFileName("png", "resize"));

                image.Save(ms, new PngEncoder());
                ms.Position = 0;
                using (Image img2 = Image.Load(ms, new PngDecoder()))
                {
                    ImageComparer.CheckSimilarity(image, img2);
                }
            }
        }
    }
}
