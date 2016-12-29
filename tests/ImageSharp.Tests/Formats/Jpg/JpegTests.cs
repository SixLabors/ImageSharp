// <copyright file="JpegTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImageSharp.Formats;
using Xunit;
using Xunit.Abstractions;
// ReSharper disable InconsistentNaming

namespace ImageSharp.Tests
{
    using System.Numerics;

    using ImageSharp.Formats.Jpg;

    public class JpegTests : MeasureFixture
    {
        public JpegTests(ITestOutputHelper output)
            : base(output)
        {
        }

        public static IEnumerable<string> AllJpegFiles => TestImages.Jpeg.All;

        [Theory]
        [WithFileCollection(nameof(AllJpegFiles), PixelTypes.Color | PixelTypes.StandardImageClass | PixelTypes.Argb)]
        public void OpenJpeg_SaveBmp<TColor>(TestImageProvider<TColor> provider)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            var image = provider.GetImage();

            provider.Utility.SaveTestOutputFile(image, "bmp");
        }


        public static IEnumerable<string> AllBmpFiles => TestImages.Bmp.All;

        [Theory]
        [WithFileCollection(nameof(AllBmpFiles), PixelTypes.Color | PixelTypes.StandardImageClass | PixelTypes.Argb, JpegSubsample.Ratio420, 75)]
        [WithFileCollection(nameof(AllBmpFiles), PixelTypes.Color | PixelTypes.StandardImageClass | PixelTypes.Argb, JpegSubsample.Ratio444, 75)]
        public void OpenBmp_SaveJpeg<TColor>(TestImageProvider<TColor> provider, JpegSubsample subSample, int quality)
           where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            var image = provider.GetImage();

            var utility = provider.Utility;
            utility.TestName += "_" + subSample + "_Q" + quality;

            using (var outputStream = File.OpenWrite(utility.GetTestOutputFileName("jpg")))
            {
                var encoder = new JpegEncoder()
                {
                    Subsample = subSample,
                    Quality = quality
                };

                image.Save(outputStream, encoder);
            }
        }

        private const int BenchmarkExecTimes = 30;

        public static readonly string[] EncoderBenchmarkFiles =
            {
                TestImages.Bmp.Car, TestImages.Bmp.NegHeight,
                TestImages.Bmp.F, TestImages.Png.Splash,
                TestImages.Jpeg.Jpeg420, TestImages.Jpeg.Calliphora,
                TestImages.Jpeg.Cmyk
            };

        private const PixelTypes BenchmarkPixels = PixelTypes.StandardImageClass; //PixelTypes.Color | PixelTypes.Argb;

        //[Theory] // Benchmark, enable manually
        [InlineData(TestImages.Jpeg.Cmyk)]
        [InlineData(TestImages.Jpeg.Ycck)]
        [InlineData(TestImages.Jpeg.Calliphora)]
        [InlineData(TestImages.Jpeg.Jpeg400)]
        [InlineData(TestImages.Jpeg.Jpeg420)]
        [InlineData(TestImages.Jpeg.Jpeg444)]
        public void Benchmark_JpegDecoder(string fileName)
        {
            string path = TestFile.GetPath(fileName);
            byte[] bytes = File.ReadAllBytes(path);

            this.Measure(
                100,
                () =>
                    {
                        Image img = new Image(bytes);
                    },
                $"Decode {fileName}");

        }

        //[Theory] // Benchmark, enable manually
        [WithFileCollection(nameof(EncoderBenchmarkFiles), BenchmarkPixels, JpegSubsample.Ratio420, 75)]
        [WithFileCollection(nameof(EncoderBenchmarkFiles), BenchmarkPixels, JpegSubsample.Ratio444, 75)]
        public void Benchmark_JpegEncoder<TColor>(TestImageProvider<TColor> provider, JpegSubsample subSample, int quality)
           where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            var image = provider.GetImage();

            using (var outputStream = new MemoryStream())
            {
                var encoder = new JpegEncoder()
                {
                    Subsample = subSample,
                    Quality = quality
                };

                for (int i = 0; i < BenchmarkExecTimes; i++)
                {
                    image.Save(outputStream, encoder);
                    outputStream.Seek(0, SeekOrigin.Begin);
                }
            }
        }

        public static Image<TColor> CreateTestImage<TColor>(GenericFactory<TColor> factory)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            Image<TColor> image = factory.CreateImage(10, 10);

            using (var pixels = image.Lock())
            {
                for (int i = 0; i < 10; i++)
                {
                    for (int j = 0; j < 10; j++)
                    {
                        Vector4 v = new Vector4(i/10f, j/10f, 0, 1);

                        TColor color = default(TColor);
                        color.PackFromVector4(v);

                        pixels[i, j] = color;
                    }
                }
            }
            return image;
        }

        [Theory]
        [WithMemberFactory(nameof(CreateTestImage), PixelTypes.Color | PixelTypes.StandardImageClass | PixelTypes.Argb)]
        public void CopyStretchedRGBTo_FromOrigo<TColor>(TestImageProvider<TColor> provider)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            var src = provider.GetImage();

            PixelArea<TColor> area = new PixelArea<TColor>(8, 8, ComponentOrder.Xyz);
            var dest = provider.Factory.CreateImage(8, 8);

            using (var s = src.Lock())
            {
                using (var d = dest.Lock())
                {
                    s.CopyRGBBytesStretchedTo(area, 0, 0);
                    d.CopyFrom(area, 0, 0);

                    Assert.Equal(s[0, 0], d[0, 0]);
                    Assert.Equal(s[7, 0], d[7, 0]);
                    Assert.Equal(s[0, 7], d[0, 7]);
                    Assert.Equal(s[7, 7], d[7, 7]);
                }
            }
        }

        [Theory]
        [WithMemberFactory(nameof(CreateTestImage), PixelTypes.Color | PixelTypes.StandardImageClass | PixelTypes.Argb)]
        public void CopyStretchedRGBTo_WithOffset<TColor>(TestImageProvider<TColor> provider)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            var src = provider.GetImage();

            PixelArea<TColor> area = new PixelArea<TColor>(8, 8, ComponentOrder.Xyz);
            var dest = provider.Factory.CreateImage(8, 8);

            using (var s = src.Lock())
            {
                using (var d = dest.Lock())
                {
                    s.CopyRGBBytesStretchedTo(area, 7, 6);
                    d.CopyFrom(area, 0, 0);

                    Assert.Equal(s[6, 7], d[0, 0]);
                    Assert.Equal(s[6, 8], d[0, 1]);
                    Assert.Equal(s[7, 8], d[1, 1]);

                    Assert.Equal(s[6, 9], d[0, 2]);
                    Assert.Equal(s[6, 9], d[0, 3]);
                    Assert.Equal(s[6, 9], d[0, 7]);

                    Assert.Equal(s[7, 9], d[1, 2]);
                    Assert.Equal(s[7, 9], d[1, 3]);
                    Assert.Equal(s[7, 9], d[1, 7]);

                    Assert.Equal(s[9, 9], d[3, 2]);
                    Assert.Equal(s[9, 9], d[3, 3]);
                    Assert.Equal(s[9, 9], d[3, 7]);

                    Assert.Equal(s[9, 7], d[3, 0]);
                    Assert.Equal(s[9, 7], d[4, 0]);
                    Assert.Equal(s[9, 7], d[7, 0]);

                    Assert.Equal(s[9, 9], d[3, 2]);
                    Assert.Equal(s[9, 9], d[4, 2]);
                    Assert.Equal(s[9, 9], d[7, 2]);

                    Assert.Equal(s[9, 9], d[4, 3]);
                    Assert.Equal(s[9, 9], d[7, 7]);
                }
            }
        }
    }
}