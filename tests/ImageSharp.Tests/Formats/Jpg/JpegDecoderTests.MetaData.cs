// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.MetaData.Profiles.Exif;
using SixLabors.ImageSharp.MetaData.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Jpg
{
    using System;
    using System.Runtime.CompilerServices;

    using SixLabors.ImageSharp.Formats.Jpeg;

    public partial class JpegDecoderTests
    {
        // TODO: A JPEGsnoop & metadata expert should review if the Exif/Icc expectations are correct. 
        // I'm seeing several entries with Exif-related names in images where we do not decode an exif profile. (- Anton)
        public static readonly TheoryData<bool, string, int, bool, bool> MetaDataTestData =
            new TheoryData<bool, string, int, bool, bool>
                {
                    { false, TestImages.Jpeg.Progressive.Progress, 24, false, false },
                    { false, TestImages.Jpeg.Progressive.Fb, 24, false, true },
                    { false, TestImages.Jpeg.Baseline.Cmyk, 32, false, true },
                    { false, TestImages.Jpeg.Baseline.Ycck, 32, true, true },
                    { false, TestImages.Jpeg.Baseline.Jpeg400, 8, false, false },
                    { false, TestImages.Jpeg.Baseline.Snake, 24, true, true },
                    { false, TestImages.Jpeg.Baseline.Jpeg420Exif, 24, true, false },

                    { true, TestImages.Jpeg.Progressive.Progress, 24, false, false },
                    { true, TestImages.Jpeg.Progressive.Fb, 24, false, true },
                    { true, TestImages.Jpeg.Baseline.Cmyk, 32, false, true },
                    { true, TestImages.Jpeg.Baseline.Ycck, 32, true, true },
                    { true, TestImages.Jpeg.Baseline.Jpeg400, 8, false, false },
                    { true, TestImages.Jpeg.Baseline.Snake, 24, true, true },
                    { true, TestImages.Jpeg.Baseline.Jpeg420Exif, 24, true, false },
                };

        [Theory]
        [MemberData(nameof(MetaDataTestData))]
        public void MetaDataIsParsedCorrectly_Orig(
            bool useIdentify,
            string imagePath,
            int expectedPixelSize,
            bool exifProfilePresent,
            bool iccProfilePresent)
        {
            TestMetaDataImpl(
                useIdentify,
                GolangJpegDecoder,
                imagePath,
                expectedPixelSize,
                exifProfilePresent,
                iccProfilePresent);
        }

        [Theory]
        [MemberData(nameof(MetaDataTestData))]
        public void MetaDataIsParsedCorrectly_PdfJs(
            bool useIdentify,
            string imagePath,
            int expectedPixelSize,
            bool exifProfilePresent,
            bool iccProfilePresent)
        {
            TestMetaDataImpl(
                useIdentify,
                PdfJsJpegDecoder,
                imagePath,
                expectedPixelSize,
                exifProfilePresent,
                iccProfilePresent);
        }

        private static void TestImageInfo(string imagePath, IImageDecoder decoder, bool useIdentify, Action<IImageInfo> test)
        {
            var testFile = TestFile.Create(imagePath);
            using (var stream = new MemoryStream(testFile.Bytes, false))
            {
                IImageInfo imageInfo = useIdentify
                                           ? ((IImageInfoDetector)decoder).Identify(Configuration.Default, stream)
                                           : decoder.Decode<Rgba32>(Configuration.Default, stream);
                test(imageInfo);
            }
        }

        private static void TestMetaDataImpl(
            bool useIdentify,
            IImageDecoder decoder,
            string imagePath,
            int expectedPixelSize,
            bool exifProfilePresent,
            bool iccProfilePresent)
        {
            TestImageInfo(
                imagePath,
                decoder,
                useIdentify,
                imageInfo =>
                    {
                        Assert.NotNull(imageInfo);
                        Assert.NotNull(imageInfo.PixelType);

                        if (useIdentify)
                        {
                            Assert.Equal(expectedPixelSize, imageInfo.PixelType.BitsPerPixel);
                        }
                        else
                        {
                            // When full Image<TPixel> decoding is performed, BitsPerPixel will match TPixel
                            int bpp32 = Unsafe.SizeOf<Rgba32>() * 8;
                            Assert.Equal(bpp32, imageInfo.PixelType.BitsPerPixel);
                        }

                        ExifProfile exifProfile = imageInfo.MetaData.ExifProfile;

                        if (exifProfilePresent)
                        {
                            Assert.NotNull(exifProfile);
                            Assert.NotEmpty(exifProfile.Values);
                        }
                        else
                        {
                            Assert.Null(exifProfile);
                        }

                        IccProfile iccProfile = imageInfo.MetaData.IccProfile;

                        if (iccProfilePresent)
                        {
                            Assert.NotNull(iccProfile);
                            Assert.NotEmpty(iccProfile.Entries);
                        }
                        else
                        {
                            Assert.Null(iccProfile);
                        }
                    });
        }
        
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void IgnoreMetaData_ControlsWhetherMetaDataIsParsed(bool ignoreMetaData)
        {
            var decoder = new JpegDecoder() { IgnoreMetadata = ignoreMetaData };

            // Snake.jpg has both Exif and ICC profiles defined:
            var testFile = TestFile.Create(TestImages.Jpeg.Baseline.Snake);

            using (Image<Rgba32> image = testFile.CreateImage(decoder))
            {
                if (ignoreMetaData)
                {
                    Assert.Null(image.MetaData.ExifProfile);
                    Assert.Null(image.MetaData.IccProfile);
                }
                else
                {
                    Assert.NotNull(image.MetaData.ExifProfile);
                    Assert.NotNull(image.MetaData.IccProfile);
                }
            }
        }
        
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Decoder_Reads_Correct_Resolution_From_Jfif(bool useIdentify)
        {
            TestImageInfo(TestImages.Jpeg.Baseline.Floorplan, DefaultJpegDecoder, useIdentify,
                imageInfo =>
                    {
                        Assert.Equal(300, imageInfo.MetaData.HorizontalResolution);
                        Assert.Equal(300, imageInfo.MetaData.VerticalResolution);
                    });
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Decoder_Reads_Correct_Resolution_From_Exif(bool useIdentify)
        {
            TestImageInfo(TestImages.Jpeg.Baseline.Jpeg420Exif, DefaultJpegDecoder, useIdentify,
                imageInfo =>
                    {
                        Assert.Equal(72, imageInfo.MetaData.HorizontalResolution);
                        Assert.Equal(72, imageInfo.MetaData.VerticalResolution);
                    });

            using (Image<Rgba32> image = TestFile.Create(TestImages.Jpeg.Baseline.Jpeg420Exif).CreateImage())
            {
                Assert.Equal(72, image.MetaData.HorizontalResolution);
                Assert.Equal(72, image.MetaData.VerticalResolution);
            }
        }
    }
}