// <copyright file="JpegDecoderTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

// ReSharper disable InconsistentNaming
namespace ImageSharp.Tests
{
    using System;
    using System.IO;

    using ImageSharp.Formats;

    using Xunit;

    public class JpegDecoderTests : TestBase
    {
        public static string[] BaselineTestJpegs =
            {
                TestImages.Jpeg.Baseline.Calliphora, TestImages.Jpeg.Baseline.Cmyk,
                TestImages.Jpeg.Baseline.Jpeg400, TestImages.Jpeg.Baseline.Jpeg444,
                TestImages.Jpeg.Baseline.Testimgorig
            };

        public static string[] ProgressiveTestJpegs = TestImages.Jpeg.Progressive.All;

        [Theory]
        [WithFileCollection(nameof(BaselineTestJpegs), PixelTypes.Color | PixelTypes.StandardImageClass | PixelTypes.Argb)]
        public void OpenBaselineJpeg_SaveBmp<TColor>(TestImageProvider<TColor> provider)
            where TColor : struct, IPixel<TColor>
        {
            using (Image<TColor> image = provider.GetImage())
            {
                provider.Utility.SaveTestOutputFile(image, "bmp");
            }
        }

        [Theory]
        [WithFileCollection(nameof(ProgressiveTestJpegs), PixelTypes.Color | PixelTypes.StandardImageClass | PixelTypes.Argb)]
        public void OpenProgressiveJpeg_SaveBmp<TColor>(TestImageProvider<TColor> provider)
            where TColor : struct, IPixel<TColor>
        {
            using (Image<TColor> image = provider.GetImage())
            {
                provider.Utility.SaveTestOutputFile(image, "bmp");
            }
        }

        [Theory]
        [WithSolidFilledImages(16, 16, 255, 0, 0, PixelTypes.StandardImageClass, JpegSubsample.Ratio420, 75)]
        [WithSolidFilledImages(16, 16, 255, 0, 0, PixelTypes.StandardImageClass, JpegSubsample.Ratio420, 100)]
        [WithSolidFilledImages(16, 16, 255, 0, 0, PixelTypes.StandardImageClass, JpegSubsample.Ratio444, 75)]
        [WithSolidFilledImages(16, 16, 255, 0, 0, PixelTypes.StandardImageClass, JpegSubsample.Ratio444, 100)]
        [WithSolidFilledImages(8, 8, 255, 0, 0, PixelTypes.StandardImageClass, JpegSubsample.Ratio444, 100)]
        public void DecodeGenerated_SaveBmp<TColor>(
            TestImageProvider<TColor> provider,
            JpegSubsample subsample,
            int quality)
            where TColor : struct, IPixel<TColor>
        {
            byte[] data;
            using (Image<TColor> image = provider.GetImage())
            {
                JpegEncoder encoder = new JpegEncoder();
                JpegEncoderOptions options = new JpegEncoderOptions { Subsample = subsample, Quality = quality };

                data = new byte[65536];
                using (MemoryStream ms = new MemoryStream(data))
                {
                    image.Save(ms, encoder, options);
                }
            }

            // TODO: Automatic image comparers could help here a lot :P
            Image<TColor> mirror = provider.Factory.CreateImage(data);
            provider.Utility.TestName += $"_{subsample}_Q{quality}";
            provider.Utility.SaveTestOutputFile(mirror, "bmp");
        }

        [Theory]
        [WithSolidFilledImages(42, 88, 255, 0, 0, PixelTypes.StandardImageClass)]
        public void DecodeGenerated_MetadataOnly<TColor>(
            TestImageProvider<TColor> provider)
            where TColor : struct, IPixel<TColor>
        {
            using (Image<TColor> image = provider.GetImage())
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    image.Save(ms, new JpegEncoder());
                    ms.Seek(0, SeekOrigin.Begin);
                    
                    using (JpegDecoderCore decoder = new JpegDecoderCore(null, null))
                    {
                        Image<TColor> mirror = decoder.Decode<TColor>(ms);

                        Assert.Equal(decoder.ImageWidth, image.Width);
                        Assert.Equal(decoder.ImageHeight, image.Height);
                    }
                }
            }
        }

        [Fact]
        public void Decoder_Reads_Correct_Resolution_From_Jfif()
        {
            using (Image image = TestFile.Create(TestImages.Jpeg.Baseline.Floorplan).CreateImage())
            {
                Assert.Equal(300, image.MetaData.HorizontalResolution);
                Assert.Equal(300, image.MetaData.VerticalResolution);
            }
        }

        [Fact]
        public void Decoder_Reads_Correct_Resolution_From_Exif()
        {
            using (Image image = TestFile.Create(TestImages.Jpeg.Baseline.Jpeg420).CreateImage())
            {
                Assert.Equal(72, image.MetaData.HorizontalResolution);
                Assert.Equal(72, image.MetaData.VerticalResolution);
            }
        }

        [Fact]
        public void Decode_IgnoreMetadataIsFalse_ExifProfileIsRead()
        {
            DecoderOptions options = new DecoderOptions()
            {
                IgnoreMetadata = false
            };

            TestFile testFile = TestFile.Create(TestImages.Jpeg.Baseline.Floorplan);

            using (Image image = testFile.CreateImage(options))
            {
                Assert.NotNull(image.MetaData.ExifProfile);
            }
        }

        [Fact]
        public void Decode_IgnoreMetadataIsTrue_ExifProfileIgnored()
        {
            DecoderOptions options = new DecoderOptions()
            {
                IgnoreMetadata = true
            };

            TestFile testFile = TestFile.Create(TestImages.Jpeg.Baseline.Floorplan);

            using (Image image = testFile.CreateImage(options))
            {
                Assert.Null(image.MetaData.ExifProfile);
            }
        }
    }
}