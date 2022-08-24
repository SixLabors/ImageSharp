// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.Collections.Generic;
using System.IO;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.Metadata.Profiles.Iptc;
using SixLabors.ImageSharp.PixelFormats;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Jpg
{
    [Trait("Format", "Jpg")]
    public partial class JpegEncoderTests
    {
        public static readonly TheoryData<string, int, int, PixelResolutionUnit> RatioFiles =
            new()
            {
                { TestImages.Jpeg.Baseline.Ratio1x1, 1, 1, PixelResolutionUnit.AspectRatio },
                { TestImages.Jpeg.Baseline.Snake, 300, 300, PixelResolutionUnit.PixelsPerInch },
                { TestImages.Jpeg.Baseline.GammaDalaiLamaGray, 72, 72, PixelResolutionUnit.PixelsPerInch }
            };

        public static readonly TheoryData<string, int> QualityFiles =
            new()
            {
                { TestImages.Jpeg.Baseline.Calliphora, 80 },
                { TestImages.Jpeg.Progressive.Fb, 75 }
            };

        [Fact]
        public void Encode_PreservesIptcProfile()
        {
            // arrange
            using var input = new Image<Rgba32>(1, 1);
            var expectedProfile = new IptcProfile();
            expectedProfile.SetValue(IptcTag.Country, "ESPAÑA");
            expectedProfile.SetValue(IptcTag.City, "unit-test-city");
            input.Metadata.IptcProfile = expectedProfile;

            // act
            using var memStream = new MemoryStream();
            input.Save(memStream, JpegEncoder);

            // assert
            memStream.Position = 0;
            using var output = Image.Load<Rgba32>(memStream);
            IptcProfile actual = output.Metadata.IptcProfile;
            Assert.NotNull(actual);
            IEnumerable<IptcValue> values = expectedProfile.Values;
            Assert.Equal(values, actual.Values);
        }

        [Fact]
        public void Encode_PreservesExifProfile()
        {
            // arrange
            using var input = new Image<Rgba32>(1, 1);
            input.Metadata.ExifProfile = new ExifProfile();
            input.Metadata.ExifProfile.SetValue(ExifTag.Software, "unit_test");

            // act
            using var memStream = new MemoryStream();
            input.Save(memStream, JpegEncoder);

            // assert
            memStream.Position = 0;
            using var output = Image.Load<Rgba32>(memStream);
            ExifProfile actual = output.Metadata.ExifProfile;
            Assert.NotNull(actual);
            IReadOnlyList<IExifValue> values = input.Metadata.ExifProfile.Values;
            Assert.Equal(values, actual.Values);
        }

        [Fact]
        public void Encode_PreservesIccProfile()
        {
            // arrange
            using var input = new Image<Rgba32>(1, 1);
            input.Metadata.IccProfile = new IccProfile(IccTestDataProfiles.Profile_Random_Array);

            // act
            using var memStream = new MemoryStream();
            input.Save(memStream, JpegEncoder);

            // assert
            memStream.Position = 0;
            using var output = Image.Load<Rgba32>(memStream);
            IccProfile actual = output.Metadata.IccProfile;
            Assert.NotNull(actual);
            IccProfile values = input.Metadata.IccProfile;
            Assert.Equal(values.Entries, actual.Entries);
        }

        [Theory]
        [WithFile(TestImages.Jpeg.Issues.ValidExifArgumentNullExceptionOnEncode, PixelTypes.Rgba32)]
        public void Encode_WithValidExifProfile_DoesNotThrowException<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Exception ex = Record.Exception(() =>
            {
                var encoder = new JpegEncoder();
                var stream = new MemoryStream();

                using Image<TPixel> image = provider.GetImage(JpegDecoder);
                image.Save(stream, encoder);
            });

            Assert.Null(ex);
        }

        [Theory]
        [MemberData(nameof(RatioFiles))]
        public void Encode_PreserveRatio(string imagePath, int xResolution, int yResolution, PixelResolutionUnit resolutionUnit)
        {
            var testFile = TestFile.Create(imagePath);
            using (Image<Rgba32> input = testFile.CreateRgba32Image())
            {
                using (var memStream = new MemoryStream())
                {
                    input.Save(memStream, JpegEncoder);

                    memStream.Position = 0;
                    using (var output = Image.Load<Rgba32>(memStream))
                    {
                        ImageMetadata meta = output.Metadata;
                        Assert.Equal(xResolution, meta.HorizontalResolution);
                        Assert.Equal(yResolution, meta.VerticalResolution);
                        Assert.Equal(resolutionUnit, meta.ResolutionUnits);
                    }
                }
            }
        }

        [Theory]
        [MemberData(nameof(QualityFiles))]
        public void Encode_PreservesQuality(string imagePath, int quality)
        {
            var testFile = TestFile.Create(imagePath);
            using (Image<Rgba32> input = testFile.CreateRgba32Image())
            {
                using (var memStream = new MemoryStream())
                {
                    input.Save(memStream, JpegEncoder);

                    memStream.Position = 0;
                    using (var output = Image.Load<Rgba32>(memStream))
                    {
                        JpegMetadata meta = output.Metadata.GetJpegMetadata();
                        Assert.Equal(quality, meta.Quality);
                    }
                }
            }
        }

        [Theory]
        [WithFile(TestImages.Jpeg.Baseline.Floorplan, PixelTypes.Rgb24, JpegEncodingColor.Luminance)]
        [WithFile(TestImages.Jpeg.Baseline.Jpeg444, PixelTypes.Rgb24, JpegEncodingColor.YCbCrRatio444)]
        [WithFile(TestImages.Jpeg.Baseline.Jpeg420Small, PixelTypes.Rgb24, JpegEncodingColor.YCbCrRatio420)]
        [WithFile(TestImages.Jpeg.Baseline.JpegRgb, PixelTypes.Rgb24, JpegEncodingColor.Rgb)]
        public void Encode_PreservesColorType<TPixel>(TestImageProvider<TPixel> provider, JpegEncodingColor expectedColorType)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            // arrange
            using Image<TPixel> input = provider.GetImage(JpegDecoder);
            using var memoryStream = new MemoryStream();

            // act
            input.Save(memoryStream, JpegEncoder);

            // assert
            memoryStream.Position = 0;
            using var output = Image.Load<Rgba32>(memoryStream);
            JpegMetadata meta = output.Metadata.GetJpegMetadata();
            Assert.Equal(expectedColorType, meta.ColorType);
        }
    }
}
