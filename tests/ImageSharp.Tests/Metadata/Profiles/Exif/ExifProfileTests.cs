// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;

using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    public class ExifProfileTests
    {
        public enum TestImageWriteFormat
        {
            /// <summary>
            /// Writes a jpg file.
            /// </summary>
            Jpeg,

            /// <summary>
            /// Writes a png file.
            /// </summary>
            Png
        }

        private static readonly Dictionary<ExifTag, object> TestProfileValues = new Dictionary<ExifTag, object>
        {
            { ExifTag.Software, "Software" },
            { ExifTag.Copyright, "Copyright" },
            { ExifTag.Orientation, (ushort)5 },
            { ExifTag.ShutterSpeedValue, new SignedRational(75.55) },
            { ExifTag.ImageDescription, "ImageDescription" },
            { ExifTag.ExposureTime, new Rational(1.0 / 1600.0) },
            { ExifTag.Model, "Model" },
        };

        [Theory]
        [InlineData(TestImageWriteFormat.Jpeg)]
        [InlineData(TestImageWriteFormat.Png)]
        public void Constructor(TestImageWriteFormat imageFormat)
        {
            Image<Rgba32> image = TestFile.Create(TestImages.Jpeg.Baseline.Calliphora).CreateRgba32Image();

            Assert.Null(image.Metadata.ExifProfile);

            const string expected = "Dirk Lemstra";
            image.Metadata.ExifProfile = new ExifProfile();
            image.Metadata.ExifProfile.SetValue(ExifTag.Copyright, expected);

            image = WriteAndRead(image, imageFormat);

            Assert.NotNull(image.Metadata.ExifProfile);
            Assert.Equal(1, image.Metadata.ExifProfile.Values.Count);

            IExifValue<string> value = image.Metadata.ExifProfile.GetValue(ExifTag.Copyright);

            Assert.NotNull(value);
            Assert.Equal(expected, value.Value);
        }

        [Fact]
        public void ConstructorEmpty()
        {
            new ExifProfile((byte[])null);
            new ExifProfile(new byte[] { });
        }

        [Fact]
        public void ConstructorCopy()
        {
            Assert.Throws<NullReferenceException>(() => ((ExifProfile)null).DeepClone());

            ExifProfile profile = GetExifProfile();

            ExifProfile clone = profile.DeepClone();
            TestProfile(clone);

            profile.SetValue(ExifTag.ColorSpace, (ushort)2);

            clone = profile.DeepClone();
            TestProfile(clone);
        }

        [Theory]
        [InlineData(TestImageWriteFormat.Jpeg)]
        [InlineData(TestImageWriteFormat.Png)]
        public void WriteFraction(TestImageWriteFormat imageFormat)
        {
            using (var memStream = new MemoryStream())
            {
                double exposureTime = 1.0 / 1600;

                ExifProfile profile = GetExifProfile();

                profile.SetValue(ExifTag.ExposureTime, new Rational(exposureTime));

                var image = new Image<Rgba32>(1, 1);
                image.Metadata.ExifProfile = profile;

                image = WriteAndRead(image, imageFormat);

                profile = image.Metadata.ExifProfile;
                Assert.NotNull(profile);

                IExifValue<Rational> value = profile.GetValue(ExifTag.ExposureTime);
                Assert.NotNull(value);
                Assert.NotEqual(exposureTime, value.Value.ToDouble());

                memStream.Position = 0;
                profile = GetExifProfile();

                profile.SetValue(ExifTag.ExposureTime, new Rational(exposureTime, true));
                image.Metadata.ExifProfile = profile;

                image = WriteAndRead(image, imageFormat);

                profile = image.Metadata.ExifProfile;
                Assert.NotNull(profile);

                value = profile.GetValue(ExifTag.ExposureTime);
                Assert.Equal(exposureTime, value.Value.ToDouble());

                image.Dispose();
            }
        }

        [Theory]
        [InlineData(TestImageWriteFormat.Jpeg)]
        [InlineData(TestImageWriteFormat.Png)]
        public void ReadWriteInfinity(TestImageWriteFormat imageFormat)
        {
            Image<Rgba32> image = TestFile.Create(TestImages.Jpeg.Baseline.Floorplan).CreateRgba32Image();
            image.Metadata.ExifProfile.SetValue(ExifTag.ExposureBiasValue, new SignedRational(double.PositiveInfinity));

            image = WriteAndReadJpeg(image);
            IExifValue<SignedRational> value = image.Metadata.ExifProfile.GetValue(ExifTag.ExposureBiasValue);
            Assert.NotNull(value);
            Assert.Equal(new SignedRational(double.PositiveInfinity), value.Value);

            image.Metadata.ExifProfile.SetValue(ExifTag.ExposureBiasValue, new SignedRational(double.NegativeInfinity));

            image = WriteAndRead(image, imageFormat);
            value = image.Metadata.ExifProfile.GetValue(ExifTag.ExposureBiasValue);
            Assert.NotNull(value);
            Assert.Equal(new SignedRational(double.NegativeInfinity), value.Value);

            image.Metadata.ExifProfile.SetValue(ExifTag.FlashEnergy, new Rational(double.NegativeInfinity));

            image = WriteAndRead(image, imageFormat);
            IExifValue<Rational> value2 = image.Metadata.ExifProfile.GetValue(ExifTag.FlashEnergy);
            Assert.NotNull(value2);
            Assert.Equal(new Rational(double.PositiveInfinity), value2.Value);
        }

        [Theory]
        [InlineData(TestImageWriteFormat.Jpeg)]
        [InlineData(TestImageWriteFormat.Png)]
        public void SetValue(TestImageWriteFormat imageFormat)
        {
            Image<Rgba32> image = TestFile.Create(TestImages.Jpeg.Baseline.Floorplan).CreateRgba32Image();
            image.Metadata.ExifProfile.SetValue(ExifTag.Software, "ImageSharp");

            IExifValue<string> software = image.Metadata.ExifProfile.GetValue(ExifTag.Software);
            Assert.Equal("ImageSharp", software.Value);

            // ExifString can set integer values.
            Assert.True(software.TrySetValue(15));
            Assert.False(software.TrySetValue(15F));

            image.Metadata.ExifProfile.SetValue(ExifTag.ShutterSpeedValue, new SignedRational(75.55));

            IExifValue<SignedRational> shutterSpeed = image.Metadata.ExifProfile.GetValue(ExifTag.ShutterSpeedValue);

            Assert.Equal(new SignedRational(7555, 100), shutterSpeed.Value);
            Assert.False(shutterSpeed.TrySetValue(75));

            image.Metadata.ExifProfile.SetValue(ExifTag.XResolution, new Rational(150.0));

            // We also need to change this value because this overrides XResolution when the image is written.
            image.Metadata.HorizontalResolution = 150.0;

            IExifValue<Rational> xResolution = image.Metadata.ExifProfile.GetValue(ExifTag.XResolution);
            Assert.Equal(new Rational(150, 1), xResolution.Value);

            Assert.False(xResolution.TrySetValue("ImageSharp"));

            image.Metadata.ExifProfile.SetValue(ExifTag.ReferenceBlackWhite, null);

            IExifValue<Rational[]> referenceBlackWhite = image.Metadata.ExifProfile.GetValue(ExifTag.ReferenceBlackWhite);
            Assert.Null(referenceBlackWhite.Value);

            var expectedLatitude = new Rational[] { new Rational(12.3), new Rational(4.56), new Rational(789.0) };
            image.Metadata.ExifProfile.SetValue(ExifTag.GPSLatitude, expectedLatitude);

            IExifValue<Rational[]> latitude = image.Metadata.ExifProfile.GetValue(ExifTag.GPSLatitude);
            Assert.Equal(expectedLatitude, latitude.Value);

            int profileCount = image.Metadata.ExifProfile.Values.Count;
            image = WriteAndRead(image, imageFormat);

            Assert.NotNull(image.Metadata.ExifProfile);

            // Should be 3 less.
            // 1 x due to setting of null "ReferenceBlackWhite" value.
            // 2 x due to use of non-standard padding tag 0xEA1C listed in EXIF Tool. We can read those values but adhere
            // strictly to the 2.3.1 specification when writing. (TODO: Support 2.3.2)
            // https://exiftool.org/TagNames/EXIF.html
            Assert.Equal(profileCount - 3, image.Metadata.ExifProfile.Values.Count);

            software = image.Metadata.ExifProfile.GetValue(ExifTag.Software);
            Assert.Equal("15", software.Value);

            shutterSpeed = image.Metadata.ExifProfile.GetValue(ExifTag.ShutterSpeedValue);
            Assert.Equal(new SignedRational(75.55), shutterSpeed.Value);

            xResolution = image.Metadata.ExifProfile.GetValue(ExifTag.XResolution);
            Assert.Equal(new Rational(150.0), xResolution.Value);

            referenceBlackWhite = image.Metadata.ExifProfile.GetValue(ExifTag.ReferenceBlackWhite);
            Assert.Null(referenceBlackWhite);

            latitude = image.Metadata.ExifProfile.GetValue(ExifTag.GPSLatitude);
            Assert.Equal(expectedLatitude, latitude.Value);

            image.Metadata.ExifProfile.Parts = ExifParts.ExifTags;

            image = WriteAndRead(image, imageFormat);

            Assert.NotNull(image.Metadata.ExifProfile);
            Assert.Equal(8, image.Metadata.ExifProfile.Values.Count);

            Assert.NotNull(image.Metadata.ExifProfile.GetValue(ExifTag.ColorSpace));
            Assert.True(image.Metadata.ExifProfile.RemoveValue(ExifTag.ColorSpace));
            Assert.False(image.Metadata.ExifProfile.RemoveValue(ExifTag.ColorSpace));
            Assert.Null(image.Metadata.ExifProfile.GetValue(ExifTag.ColorSpace));

            Assert.Equal(7, image.Metadata.ExifProfile.Values.Count);
        }

        [Fact]
        public void Syncs()
        {
            var exifProfile = new ExifProfile();
            exifProfile.SetValue(ExifTag.XResolution, new Rational(200));
            exifProfile.SetValue(ExifTag.YResolution, new Rational(300));

            var metaData = new ImageMetadata
            {
                ExifProfile = exifProfile,
                HorizontalResolution = 200,
                VerticalResolution = 300
            };

            metaData.HorizontalResolution = 100;

            Assert.Equal(200, metaData.ExifProfile.GetValue(ExifTag.XResolution).Value.ToDouble());
            Assert.Equal(300, metaData.ExifProfile.GetValue(ExifTag.YResolution).Value.ToDouble());

            exifProfile.Sync(metaData);

            Assert.Equal(100, metaData.ExifProfile.GetValue(ExifTag.XResolution).Value.ToDouble());
            Assert.Equal(300, metaData.ExifProfile.GetValue(ExifTag.YResolution).Value.ToDouble());

            metaData.VerticalResolution = 150;

            Assert.Equal(100, metaData.ExifProfile.GetValue(ExifTag.XResolution).Value.ToDouble());
            Assert.Equal(300, metaData.ExifProfile.GetValue(ExifTag.YResolution).Value.ToDouble());

            exifProfile.Sync(metaData);

            Assert.Equal(100, metaData.ExifProfile.GetValue(ExifTag.XResolution).Value.ToDouble());
            Assert.Equal(150, metaData.ExifProfile.GetValue(ExifTag.YResolution).Value.ToDouble());
        }

        [Fact]
        public void Values()
        {
            ExifProfile profile = GetExifProfile();

            TestProfile(profile);

            Image<Rgba32> thumbnail = profile.CreateThumbnail<Rgba32>();
            Assert.NotNull(thumbnail);
            Assert.Equal(256, thumbnail.Width);
            Assert.Equal(170, thumbnail.Height);
        }

        [Fact]
        public void ReadWriteLargeProfileJpg()
        {
            ExifTag<string>[] tags = new[] { ExifTag.Software, ExifTag.Copyright, ExifTag.Model, ExifTag.ImageDescription };
            foreach (ExifTag<string> tag in tags)
            {
                // Arrange
                var junk = new StringBuilder();
                for (int i = 0; i < 65600; i++)
                {
                    junk.Append("a");
                }

                var image = new Image<Rgba32>(100, 100);
                ExifProfile expectedProfile = CreateExifProfile();
                var expectedProfileTags = expectedProfile.Values.Select(x => x.Tag).ToList();
                expectedProfile.SetValue(tag, junk.ToString());
                image.Metadata.ExifProfile = expectedProfile;

                // Act
                Image<Rgba32> reloadedImage = WriteAndRead(image, TestImageWriteFormat.Jpeg);

                // Assert
                ExifProfile actualProfile = reloadedImage.Metadata.ExifProfile;
                Assert.NotNull(actualProfile);

                foreach (ExifTag expectedProfileTag in expectedProfileTags)
                {
                    IExifValue actualProfileValue = actualProfile.GetValueInternal(expectedProfileTag);
                    IExifValue expectedProfileValue = expectedProfile.GetValueInternal(expectedProfileTag);
                    Assert.Equal(expectedProfileValue.GetValue(), actualProfileValue.GetValue());
                }

                IExifValue<string> expected = expectedProfile.GetValue(tag);
                IExifValue<string> actual = actualProfile.GetValue(tag);
                Assert.Equal(expected, actual);
            }
        }

        [Fact]
        public void ExifTypeUndefined()
        {
            // This image contains an 802 byte EXIF profile
            // It has a tag with an index offset of 18,481,152 bytes (overrunning the data)
            Image<Rgba32> image = TestFile.Create(TestImages.Jpeg.Progressive.Bad.ExifUndefType).CreateRgba32Image();
            Assert.NotNull(image);

            ExifProfile profile = image.Metadata.ExifProfile;
            Assert.NotNull(profile);

            foreach (ExifValue value in profile.Values)
            {
                if (value.DataType == ExifDataType.Undefined)
                {
                    Assert.True(value.IsArray);
                    Assert.Equal(4U, 4 * ExifDataTypes.GetSize(value.DataType));
                }
            }
        }

        [Fact]
        public void TestArrayValueWithUnspecifiedSize()
        {
            // This images contains array in the exif profile that has zero components.
            Image<Rgba32> image = TestFile.Create(TestImages.Jpeg.Issues.InvalidCast520).CreateRgba32Image();

            ExifProfile profile = image.Metadata.ExifProfile;
            Assert.NotNull(profile);

            // Force parsing of the profile.
            Assert.Equal(25, profile.Values.Count);

            byte[] bytes = profile.ToByteArray();
            Assert.Equal(525, bytes.Length);
        }

        [Theory]
        [InlineData(TestImageWriteFormat.Jpeg)]
        [InlineData(TestImageWriteFormat.Png)]
        public void WritingImagePreservesExifProfile(TestImageWriteFormat imageFormat)
        {
            // Arrange
            var image = new Image<Rgba32>(1, 1);
            image.Metadata.ExifProfile = CreateExifProfile();

            // Act
            Image<Rgba32> reloadedImage = WriteAndRead(image, imageFormat);

            // Assert
            ExifProfile actual = reloadedImage.Metadata.ExifProfile;
            Assert.NotNull(actual);
            foreach (KeyValuePair<ExifTag, object> expectedProfileValue in TestProfileValues)
            {
                IExifValue actualProfileValue = actual.GetValueInternal(expectedProfileValue.Key);
                Assert.NotNull(actualProfileValue);
                Assert.Equal(expectedProfileValue.Value, actualProfileValue.GetValue());
            }
        }

        [Fact]
        public void ProfileToByteArray()
        {
            // Arrange
            byte[] exifBytesWithoutExifCode = ExifConstants.LittleEndianByteOrderMarker.ToArray();
            ExifProfile expectedProfile = CreateExifProfile();
            var expectedProfileTags = expectedProfile.Values.Select(x => x.Tag).ToList();

            // Act
            byte[] actualBytes = expectedProfile.ToByteArray();
            var actualProfile = new ExifProfile(actualBytes);

            // Assert
            Assert.NotNull(actualBytes);
            Assert.NotEmpty(actualBytes);
            Assert.Equal(exifBytesWithoutExifCode, actualBytes.Take(exifBytesWithoutExifCode.Length).ToArray());
            foreach (ExifTag expectedProfileTag in expectedProfileTags)
            {
                IExifValue actualProfileValue = actualProfile.GetValueInternal(expectedProfileTag);
                IExifValue expectedProfileValue = expectedProfile.GetValueInternal(expectedProfileTag);
                Assert.Equal(expectedProfileValue.GetValue(), actualProfileValue.GetValue());
            }
        }

        private static ExifProfile CreateExifProfile()
        {
            var profile = new ExifProfile();

            foreach (KeyValuePair<ExifTag, object> exifProfileValue in TestProfileValues)
            {
                profile.SetValueInternal(exifProfileValue.Key, exifProfileValue.Value);
            }

            return profile;
        }

        internal static ExifProfile GetExifProfile()
        {
            Image<Rgba32> image = TestFile.Create(TestImages.Jpeg.Baseline.Floorplan).CreateRgba32Image();

            ExifProfile profile = image.Metadata.ExifProfile;
            Assert.NotNull(profile);

            return profile;
        }

        private static Image<Rgba32> WriteAndRead(Image<Rgba32> image, TestImageWriteFormat imageFormat)
        {
            switch (imageFormat)
            {
                case TestImageWriteFormat.Jpeg:
                    return WriteAndReadJpeg(image);
                case TestImageWriteFormat.Png:
                    return WriteAndReadPng(image);
                default:
                    throw new ArgumentException("Unexpected test image format, only Jpeg and Png are allowed");
            }
        }

        private static Image<Rgba32> WriteAndReadJpeg(Image<Rgba32> image)
        {
            using (var memStream = new MemoryStream())
            {
                image.SaveAsJpeg(memStream);
                image.Dispose();

                memStream.Position = 0;
                return Image.Load<Rgba32>(memStream);
            }
        }

        private static Image<Rgba32> WriteAndReadPng(Image<Rgba32> image)
        {
            using (var memStream = new MemoryStream())
            {
                image.SaveAsPng(memStream);
                image.Dispose();

                memStream.Position = 0;
                return Image.Load<Rgba32>(memStream);
            }
        }

        private static void TestProfile(ExifProfile profile)
        {
            Assert.NotNull(profile);

            Assert.Equal(16, profile.Values.Count);

            foreach (IExifValue value in profile.Values)
            {
                Assert.NotNull(value.GetValue());
            }

            IExifValue<string> software = profile.GetValue(ExifTag.Software);
            Assert.Equal("Windows Photo Editor 10.0.10011.16384", software.Value);

            IExifValue<Rational> xResolution = profile.GetValue(ExifTag.XResolution);
            Assert.Equal(new Rational(300.0), xResolution.Value);

            IExifValue<Number> xDimension = profile.GetValue(ExifTag.PixelXDimension);
            Assert.Equal(2338U, xDimension.Value);
        }
    }
}
