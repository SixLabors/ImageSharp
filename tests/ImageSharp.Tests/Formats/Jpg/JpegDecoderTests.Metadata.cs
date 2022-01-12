// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;

using Xunit;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Jpg
{
    [Trait("Format", "Jpg")]
    public partial class JpegDecoderTests
    {
        // TODO: A JPEGsnoop & metadata expert should review if the Exif/Icc expectations are correct.
        // I'm seeing several entries with Exif-related names in images where we do not decode an exif profile. (- Anton)
        public static readonly TheoryData<bool, string, int, bool, bool> MetadataTestData =
        new()
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
            { true, TestImages.Jpeg.Issues.IdentifyMultiFrame1211, 24, true, true },
        };

        public static readonly TheoryData<string, int, int, PixelResolutionUnit> RatioFiles =
        new()
        {
            { TestImages.Jpeg.Baseline.Ratio1x1, 1, 1, PixelResolutionUnit.AspectRatio },
            { TestImages.Jpeg.Baseline.Snake, 300, 300, PixelResolutionUnit.PixelsPerInch },
            { TestImages.Jpeg.Baseline.GammaDalaiLamaGray, 72, 72, PixelResolutionUnit.PixelsPerInch },
            { TestImages.Jpeg.Issues.MultipleApp01932, 400, 400, PixelResolutionUnit.PixelsPerInch }
        };

        public static readonly TheoryData<string, int> QualityFiles =
        new()
        {
            { TestImages.Jpeg.Baseline.Calliphora, 80 },
            { TestImages.Jpeg.Progressive.Fb, 75 },
            { TestImages.Jpeg.Issues.IncorrectQuality845, 98 },
            { TestImages.Jpeg.Baseline.ForestBridgeDifferentComponentsQuality, 89 },
            { TestImages.Jpeg.Progressive.Winter420_NonInterleaved, 80 }
        };

        [Theory]
        [MemberData(nameof(MetadataTestData))]
        public void MetadataIsParsedCorrectly(
            bool useIdentify,
            string imagePath,
            int expectedPixelSize,
            bool exifProfilePresent,
            bool iccProfilePresent) => TestMetadataImpl(
                useIdentify,
                JpegDecoder,
                imagePath,
                expectedPixelSize,
                exifProfilePresent,
                iccProfilePresent);

        [Theory]
        [MemberData(nameof(RatioFiles))]
        public void Decode_VerifyRatio(string imagePath, int xResolution, int yResolution, PixelResolutionUnit resolutionUnit)
        {
            var testFile = TestFile.Create(imagePath);
            using (var stream = new MemoryStream(testFile.Bytes, false))
            {
                var decoder = new JpegDecoder();
                using (Image image = decoder.Decode(Configuration.Default, stream))
                {
                    ImageMetadata meta = image.Metadata;
                    Assert.Equal(xResolution, meta.HorizontalResolution);
                    Assert.Equal(yResolution, meta.VerticalResolution);
                    Assert.Equal(resolutionUnit, meta.ResolutionUnits);
                }
            }
        }

        [Theory]
        [MemberData(nameof(RatioFiles))]
        public void Identify_VerifyRatio(string imagePath, int xResolution, int yResolution, PixelResolutionUnit resolutionUnit)
        {
            var testFile = TestFile.Create(imagePath);
            using (var stream = new MemoryStream(testFile.Bytes, false))
            {
                var decoder = new JpegDecoder();
                IImageInfo image = decoder.Identify(Configuration.Default, stream);
                ImageMetadata meta = image.Metadata;
                Assert.Equal(xResolution, meta.HorizontalResolution);
                Assert.Equal(yResolution, meta.VerticalResolution);
                Assert.Equal(resolutionUnit, meta.ResolutionUnits);
            }
        }

        [Theory]
        [MemberData(nameof(QualityFiles))]
        public void Identify_VerifyQuality(string imagePath, int quality)
        {
            var testFile = TestFile.Create(imagePath);
            using (var stream = new MemoryStream(testFile.Bytes, false))
            {
                var decoder = new JpegDecoder();
                IImageInfo image = decoder.Identify(Configuration.Default, stream);
                JpegMetadata meta = image.Metadata.GetJpegMetadata();
                Assert.Equal(quality, meta.Quality);
            }
        }

        [Theory]
        [MemberData(nameof(QualityFiles))]
        public void Decode_VerifyQuality(string imagePath, int quality)
        {
            var testFile = TestFile.Create(imagePath);
            using (var stream = new MemoryStream(testFile.Bytes, false))
            {
                using (Image image = JpegDecoder.Decode(Configuration.Default, stream))
                {
                    JpegMetadata meta = image.Metadata.GetJpegMetadata();
                    Assert.Equal(quality, meta.Quality);
                }
            }
        }

        [Theory]
        [InlineData(TestImages.Jpeg.Baseline.Floorplan, JpegColorType.Luminance)]
        [InlineData(TestImages.Jpeg.Baseline.Jpeg420Small, JpegColorType.YCbCrRatio420)]
        [InlineData(TestImages.Jpeg.Baseline.Jpeg444, JpegColorType.YCbCrRatio444)]
        [InlineData(TestImages.Jpeg.Baseline.JpegRgb, JpegColorType.Rgb)]
        [InlineData(TestImages.Jpeg.Baseline.Cmyk, JpegColorType.Cmyk)]
        [InlineData(TestImages.Jpeg.Baseline.Jpeg410, JpegColorType.YCbCrRatio410)]
        [InlineData(TestImages.Jpeg.Baseline.Jpeg422, JpegColorType.YCbCrRatio422)]
        [InlineData(TestImages.Jpeg.Baseline.Jpeg411, JpegColorType.YCbCrRatio411)]
        public void Identify_DetectsCorrectColorType(string imagePath, JpegColorType expectedColorType)
        {
            var testFile = TestFile.Create(imagePath);
            using (var stream = new MemoryStream(testFile.Bytes, false))
            {
                IImageInfo image = JpegDecoder.Identify(Configuration.Default, stream);
                JpegMetadata meta = image.Metadata.GetJpegMetadata();
                Assert.Equal(expectedColorType, meta.ColorType);
            }
        }

        [Theory]
        [WithFile(TestImages.Jpeg.Baseline.Floorplan, PixelTypes.Rgb24, JpegColorType.Luminance)]
        [WithFile(TestImages.Jpeg.Baseline.Jpeg420Small, PixelTypes.Rgb24, JpegColorType.YCbCrRatio420)]
        [WithFile(TestImages.Jpeg.Baseline.Jpeg444, PixelTypes.Rgb24, JpegColorType.YCbCrRatio444)]
        [WithFile(TestImages.Jpeg.Baseline.JpegRgb, PixelTypes.Rgb24, JpegColorType.Rgb)]
        [WithFile(TestImages.Jpeg.Baseline.Cmyk, PixelTypes.Rgb24, JpegColorType.Cmyk)]
        public void Decode_DetectsCorrectColorType<TPixel>(TestImageProvider<TPixel> provider, JpegColorType expectedColorType)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(JpegDecoder))
            {
                JpegMetadata meta = image.Metadata.GetJpegMetadata();
                Assert.Equal(expectedColorType, meta.ColorType);
            }
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

        private static void TestMetadataImpl(
            bool useIdentify,
            IImageDecoder decoder,
            string imagePath,
            int expectedPixelSize,
            bool exifProfilePresent,
            bool iccProfilePresent) => TestImageInfo(
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

                        ExifProfile exifProfile = imageInfo.Metadata.ExifProfile;

                        if (exifProfilePresent)
                        {
                            Assert.NotNull(exifProfile);
                            Assert.NotEmpty(exifProfile.Values);
                        }
                        else
                        {
                            Assert.Null(exifProfile);
                        }

                        IccProfile iccProfile = imageInfo.Metadata.IccProfile;

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

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void IgnoreMetadata_ControlsWhetherMetadataIsParsed(bool ignoreMetadata)
        {
            var decoder = new JpegDecoder { IgnoreMetadata = ignoreMetadata };

            // Snake.jpg has both Exif and ICC profiles defined:
            var testFile = TestFile.Create(TestImages.Jpeg.Baseline.Snake);

            using (Image<Rgba32> image = testFile.CreateRgba32Image(decoder))
            {
                if (ignoreMetadata)
                {
                    Assert.Null(image.Metadata.ExifProfile);
                    Assert.Null(image.Metadata.IccProfile);
                }
                else
                {
                    Assert.NotNull(image.Metadata.ExifProfile);
                    Assert.NotNull(image.Metadata.IccProfile);
                }
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Decoder_Reads_Correct_Resolution_From_Jfif(bool useIdentify) => TestImageInfo(
                TestImages.Jpeg.Baseline.Floorplan,
                JpegDecoder,
                useIdentify,
                imageInfo =>
                {
                    Assert.Equal(300, imageInfo.Metadata.HorizontalResolution);
                    Assert.Equal(300, imageInfo.Metadata.VerticalResolution);
                });

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Decoder_Reads_Correct_Resolution_From_Exif(bool useIdentify) => TestImageInfo(
                TestImages.Jpeg.Baseline.Jpeg420Exif,
                JpegDecoder,
                useIdentify,
                imageInfo =>
                    {
                        Assert.Equal(72, imageInfo.Metadata.HorizontalResolution);
                        Assert.Equal(72, imageInfo.Metadata.VerticalResolution);
                    });

        [Fact]
        public void ExifIfdStructure()
        {
            byte[] exifBytes;
            using var memoryStream = new MemoryStream();
            using (var image = Image.Load(TestFile.GetInputFileFullPath(TestImages.Jpeg.Baseline.Calliphora)))
            {
                var exif = new ExifProfile();
                exif.SetValue(ExifTag.XPAuthor, Encoding.GetEncoding("UCS-2").GetBytes("Dan Petitt"));

                exif.SetValue(ExifTag.XPTitle, Encoding.GetEncoding("UCS-2").GetBytes("A bit of test metadata for image title"));
                exif.SetValue(ExifTag.UserComment, new EncodedString("A bit of normal comment text", EncodedStringCode.ASCII));

                exif.SetValue(ExifTag.GPSDateStamp, "2022-01-06");
                exif.SetValue(ExifTag.XPKeywords, new byte[] { 0x41, 0x53, 0x43, 0x49, 0x49, 00, 00, 00, 0x41, 0x41, 0x41 });

                image.Metadata.ExifProfile = exif;

                exifBytes = exif.ToByteArray();

                image.Save(memoryStream, new JpegEncoder());
            }

            memoryStream.Seek(0, SeekOrigin.Begin);
            using (var image = Image.Load(memoryStream))
            {
                Assert.NotNull(image.Metadata.ExifProfile);
            }
        }
    }
}
