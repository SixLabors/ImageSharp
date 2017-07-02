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
    using System.Numerics;

    using ImageSharp.PixelFormats;

    public class PngSmokeTests
    {
        [Theory]
        [WithTestPatternImages(300, 300, PixelTypes.Rgba32)]
        public void GeneralTest<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            // does saving a file then repoening mean both files are identical???
            using (Image<TPixel> image = provider.GetImage())
            using (MemoryStream ms = new MemoryStream())
            {
                // image.Save(provider.Utility.GetTestOutputFileName("bmp"));

                image.Save(ms, new PngEncoder());
                ms.Position = 0;
                using (Image<Rgba32> img2 = Image.Load<Rgba32>(ms, new PngDecoder()))
                {
                    // img2.Save(provider.Utility.GetTestOutputFileName("bmp", "_loaded"), new BmpEncoder());
                    ImageComparer.VerifySimilarity(image, img2);
                }
            }
        }

        [Theory]
        [WithTestPatternImages(100, 100, PixelTypes.Rgba32)]
        public void CanSaveIndexedPng<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            // does saving a file then repoening mean both files are identical???
            using (Image<TPixel> image = provider.GetImage())
            using (MemoryStream ms = new MemoryStream())
            {
                // image.Save(provider.Utility.GetTestOutputFileName("bmp"));
                image.MetaData.Quality = 256;
                image.Save(ms, new PngEncoder());
                ms.Position = 0;
                using (Image<Rgba32> img2 = Image.Load<Rgba32>(ms, new PngDecoder()))
                {
                    // img2.Save(provider.Utility.GetTestOutputFileName("bmp", "_loaded"), new BmpEncoder());
                    ImageComparer.VerifySimilarity(image, img2, 0.03f);
                }
            }
        }

        // JJS: Commented out for now since the test does not take into lossy nature of indexing.
        //[Theory]
        //[WithTestPatternImages(100, 100, PixelTypes.Color)]
        //public void CanSaveIndexedPngTwice<TPixel>(TestImageProvider<TPixel> provider)
        //    where TPixel : struct, IPixel<TPixel>
        //{
        //    // does saving a file then repoening mean both files are identical???
        //    using (Image<TPixel> source = provider.GetImage())
        //    using (MemoryStream ms = new MemoryStream())
        //    {
        //        source.MetaData.Quality = 256;
        //        source.Save(ms, new PngEncoder(), new PngEncoderOptions {
        //            Threshold = 200
        //        });
        //        ms.Position = 0;
        //        using (Image img1 = Image.Load(ms, new PngDecoder()))
        //        {
        //            using (MemoryStream ms2 = new MemoryStream())
        //            {
        //                img1.Save(ms2, new PngEncoder(), new PngEncoderOptions
        //                {
        //                    Threshold = 200
        //                });
        //                ms2.Position = 0;
        //                using (Image img2 = Image.Load(ms2, new PngDecoder()))
        //                {
        //                    using (PixelAccessor<Color> pixels1 = img1.Lock())
        //                    using (PixelAccessor<Color> pixels2 = img2.Lock())
        //                    {
        //                        for (int y = 0; y < img1.Height; y++)
        //                        {
        //                            for (int x = 0; x < img1.Width; x++)
        //                            {
        //                                Assert.Equal(pixels1[x, y], pixels2[x, y]);
        //                            }
        //                        }
        //                    }
        //                }
        //            }
        //        }
        //    }
        //}

        [Theory]
        [WithTestPatternImages(300, 300, PixelTypes.Rgba32)]
        public void Resize<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            // does saving a file then repoening mean both files are identical???
            using (Image<TPixel> image = provider.GetImage())
            using (MemoryStream ms = new MemoryStream())
            {
                // image.Save(provider.Utility.GetTestOutputFileName("png"));
                image.Resize(100, 100);
                // image.Save(provider.Utility.GetTestOutputFileName("png", "resize"));

                image.Save(ms, new PngEncoder());
                ms.Position = 0;
                using (Image<Rgba32> img2 = Image.Load<Rgba32>(ms, new PngDecoder()))
                {
                    ImageComparer.VerifySimilarity(image, img2);
                }
            }
        }
    }
}
