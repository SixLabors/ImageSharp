// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder;
using SixLabors.ImageSharp.MetaData;
using SixLabors.ImageSharp.MetaData.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Primitives;

using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    public class ExifProfileTests
    {
        public enum TestImageWriteFormat
        {
            Jpeg,
            Png
        }

        private static readonly Dictionary<ExifTag, object> TestProfileValues = new Dictionary<ExifTag, object>()
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
            Image<Rgba32> image = TestFile.Create(TestImages.Jpeg.Baseline.Calliphora).CreateImage();

            Assert.Null(image.MetaData.ExifProfile);

            image.MetaData.ExifProfile = new ExifProfile();
            image.MetaData.ExifProfile.SetValue(ExifTag.Copyright, "Dirk Lemstra");

            image = WriteAndRead(image, imageFormat);

            Assert.NotNull(image.MetaData.ExifProfile);
            Assert.Equal(1, image.MetaData.ExifProfile.Values.Count());

            ExifValue value = image.MetaData.ExifProfile.Values.FirstOrDefault(val => val.Tag == ExifTag.Copyright);
            TestValue(value, "Dirk Lemstra");
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
                image.MetaData.ExifProfile = profile;

                image = WriteAndRead(image, imageFormat);

                profile = image.MetaData.ExifProfile;
                Assert.NotNull(profile);

                ExifValue value = profile.GetValue(ExifTag.ExposureTime);
                Assert.NotNull(value);
                Assert.NotEqual(exposureTime, ((Rational)value.Value).ToDouble());

                memStream.Position = 0;
                profile = GetExifProfile();

                profile.SetValue(ExifTag.ExposureTime, new Rational(exposureTime, true));
                image.MetaData.ExifProfile = profile;

                image = WriteAndRead(image, imageFormat);

                profile = image.MetaData.ExifProfile;
                Assert.NotNull(profile);

                value = profile.GetValue(ExifTag.ExposureTime);
                Assert.Equal(exposureTime, ((Rational)value.Value).ToDouble());
            }
        }

        [Theory]
        [InlineData(TestImageWriteFormat.Jpeg)]
        [InlineData(TestImageWriteFormat.Png)]
        public void ReadWriteInfinity(TestImageWriteFormat imageFormat)
        {
            Image<Rgba32> image = TestFile.Create(TestImages.Jpeg.Baseline.Floorplan).CreateImage();
            image.MetaData.ExifProfile.SetValue(ExifTag.ExposureBiasValue, new SignedRational(double.PositiveInfinity));

            image = WriteAndReadJpeg(image);
            ExifValue value = image.MetaData.ExifProfile.GetValue(ExifTag.ExposureBiasValue);
            Assert.NotNull(value);
            Assert.Equal(new SignedRational(double.PositiveInfinity), value.Value);

            image.MetaData.ExifProfile.SetValue(ExifTag.ExposureBiasValue, new SignedRational(double.NegativeInfinity));

            image = WriteAndRead(image, imageFormat);
            value = image.MetaData.ExifProfile.GetValue(ExifTag.ExposureBiasValue);
            Assert.NotNull(value);
            Assert.Equal(new SignedRational(double.NegativeInfinity), value.Value);

            image.MetaData.ExifProfile.SetValue(ExifTag.FlashEnergy, new Rational(double.NegativeInfinity));

            image = WriteAndRead(image, imageFormat);
            value = image.MetaData.ExifProfile.GetValue(ExifTag.FlashEnergy);
            Assert.NotNull(value);
            Assert.Equal(new Rational(double.PositiveInfinity), value.Value);
        }

        [Theory]
        [InlineData(TestImageWriteFormat.Jpeg)]
        [InlineData(TestImageWriteFormat.Png)]
        public void SetValue(TestImageWriteFormat imageFormat)
        {
            var latitude = new Rational[] { new Rational(12.3), new Rational(4.56), new Rational(789.0) };

            Image<Rgba32> image = TestFile.Create(TestImages.Jpeg.Baseline.Floorplan).CreateImage();
            image.MetaData.ExifProfile.SetValue(ExifTag.Software, "ImageSharp");

            ExifValue value = image.MetaData.ExifProfile.GetValue(ExifTag.Software);
            TestValue(value, "ImageSharp");

            Assert.Throws<ArgumentException>(() => { value.WithValue(15); });

            image.MetaData.ExifProfile.SetValue(ExifTag.ShutterSpeedValue, new SignedRational(75.55));

            value = image.MetaData.ExifProfile.GetValue(ExifTag.ShutterSpeedValue);

            TestValue(value, new SignedRational(7555, 100));

            Assert.Throws<ArgumentException>(() => { value.WithValue(75); });

            image.MetaData.ExifProfile.SetValue(ExifTag.XResolution, new Rational(150.0));

            // We also need to change this value because this overrides XResolution when the image is written.
            image.MetaData.HorizontalResolution = 150.0;

            value = image.MetaData.ExifProfile.GetValue(ExifTag.XResolution);
            TestValue(value, new Rational(150, 1));

            Assert.Throws<ArgumentException>(() => { value.WithValue("ImageSharp"); });

            image.MetaData.ExifProfile.SetValue(ExifTag.ReferenceBlackWhite, null);

            value = image.MetaData.ExifProfile.GetValue(ExifTag.ReferenceBlackWhite);
            TestValue(value, (string)null);

            image.MetaData.ExifProfile.SetValue(ExifTag.GPSLatitude, latitude);

            value = image.MetaData.ExifProfile.GetValue(ExifTag.GPSLatitude);
            TestValue(value, latitude);

            image = WriteAndRead(image, imageFormat);

            Assert.NotNull(image.MetaData.ExifProfile);
            Assert.Equal(17, image.MetaData.ExifProfile.Values.Count());

            value = image.MetaData.ExifProfile.GetValue(ExifTag.Software);
            TestValue(value, "ImageSharp");

            value = image.MetaData.ExifProfile.GetValue(ExifTag.ShutterSpeedValue);
            TestValue(value, new SignedRational(75.55));

            value = image.MetaData.ExifProfile.GetValue(ExifTag.XResolution);
            TestValue(value, new Rational(150.0));

            value = image.MetaData.ExifProfile.GetValue(ExifTag.ReferenceBlackWhite);
            Assert.Null(value);

            value = image.MetaData.ExifProfile.GetValue(ExifTag.GPSLatitude);
            TestValue(value, latitude);

            image.MetaData.ExifProfile.Parts = ExifParts.ExifTags;

            image = WriteAndRead(image, imageFormat);

            Assert.NotNull(image.MetaData.ExifProfile);
            Assert.Equal(8, image.MetaData.ExifProfile.Values.Count());

            Assert.NotNull(image.MetaData.ExifProfile.GetValue(ExifTag.ColorSpace));
            Assert.True(image.MetaData.ExifProfile.RemoveValue(ExifTag.ColorSpace));
            Assert.False(image.MetaData.ExifProfile.RemoveValue(ExifTag.ColorSpace));
            Assert.Null(image.MetaData.ExifProfile.GetValue(ExifTag.ColorSpace));

            Assert.Equal(7, image.MetaData.ExifProfile.Values.Count());
        }

        [Fact]
        public void Syncs()
        {
            var exifProfile = new ExifProfile();
            exifProfile.SetValue(ExifTag.XResolution, new Rational(200));
            exifProfile.SetValue(ExifTag.YResolution, new Rational(300));

            var metaData = new ImageMetaData
            {
                ExifProfile = exifProfile,
                HorizontalResolution = 200,
                VerticalResolution = 300
            };

            metaData.HorizontalResolution = 100;

            Assert.Equal(200, ((Rational)metaData.ExifProfile.GetValue(ExifTag.XResolution).Value).ToDouble());
            Assert.Equal(300, ((Rational)metaData.ExifProfile.GetValue(ExifTag.YResolution).Value).ToDouble());

            exifProfile.Sync(metaData);

            Assert.Equal(100, ((Rational)metaData.ExifProfile.GetValue(ExifTag.XResolution).Value).ToDouble());
            Assert.Equal(300, ((Rational)metaData.ExifProfile.GetValue(ExifTag.YResolution).Value).ToDouble());

            metaData.VerticalResolution = 150;

            Assert.Equal(100, ((Rational)metaData.ExifProfile.GetValue(ExifTag.XResolution).Value).ToDouble());
            Assert.Equal(300, ((Rational)metaData.ExifProfile.GetValue(ExifTag.YResolution).Value).ToDouble());

            exifProfile.Sync(metaData);

            Assert.Equal(100, ((Rational)metaData.ExifProfile.GetValue(ExifTag.XResolution).Value).ToDouble());
            Assert.Equal(150, ((Rational)metaData.ExifProfile.GetValue(ExifTag.YResolution).Value).ToDouble());
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

        [Theory]
        [InlineData(ExifTag.Software)]
        [InlineData(ExifTag.Copyright)]
        [InlineData(ExifTag.Model)]
        [InlineData(ExifTag.ImageDescription)]
        public void ReadWriteLargeProfileJpg(ExifTag exifValueToChange)
        {
            // arrange
            var junk = new StringBuilder();
            for (int i = 0; i < 65600; i++)
            {
                junk.Append("a");
            }
            var image = new Image<Rgba32>(100, 100);
            ExifProfile expectedProfile = CreateExifProfile();
            var expectedProfileTags = expectedProfile.Values.Select(x => x.Tag).ToList();
            expectedProfile.SetValue(exifValueToChange, junk.ToString());
            image.MetaData.ExifProfile = expectedProfile;

            // act
            Image<Rgba32> reloadedImage = WriteAndRead(image, TestImageWriteFormat.Jpeg);

            // assert
            ExifProfile actualProfile = reloadedImage.MetaData.ExifProfile;
            Assert.NotNull(actualProfile);
            foreach (ExifTag expectedProfileTag in expectedProfileTags)
            {
                ExifValue actualProfileValue = actualProfile.GetValue(expectedProfileTag);
                ExifValue expectedProfileValue = expectedProfile.GetValue(expectedProfileTag);
                Assert.Equal(expectedProfileValue.Value, actualProfileValue.Value);
            }
        }

        [Fact]
        public void ExifTypeUndefined()
        {
            // This image contains an 802 byte EXIF profile
            // It has a tag with an index offset of 18,481,152 bytes (overrunning the data)

            Image<Rgba32> image = TestFile.Create(TestImages.Jpeg.Progressive.Bad.ExifUndefType).CreateImage();
            Assert.NotNull(image);

            ExifProfile profile = image.MetaData.ExifProfile;
            Assert.NotNull(profile);

            foreach (ExifValue value in profile.Values)
            {
                if (value.DataType == ExifDataType.Undefined)
                {
                    Assert.Equal(4, value.NumberOfComponents);
                }
            }
        }

        [Fact]
        public void TestArrayValueWithUnspecifiedSize()
        {
            // This images contains array in the exif profile that has zero components.
            Image<Rgba32> image = TestFile.Create(TestImages.Jpeg.Issues.InvalidCast520).CreateImage();

            ExifProfile profile = image.MetaData.ExifProfile;
            Assert.NotNull(profile);

            // Force parsing of the profile.
            Assert.Equal(24, profile.Values.Count);

            byte[] bytes = profile.ToByteArray();
            Assert.Equal(489, bytes.Length);
        }

        [Theory]
        [InlineData(TestImageWriteFormat.Jpeg)]
        [InlineData(TestImageWriteFormat.Png)]
        public void WritingImagePreservesExifProfile(TestImageWriteFormat imageFormat)
        {
            // arrange
            var image = new Image<Rgba32>(1, 1);
            ExifProfile expected = CreateExifProfile();
            image.MetaData.ExifProfile = expected;

            // act
            Image<Rgba32> reloadedImage = WriteAndRead(image, imageFormat);

            // assert
            ExifProfile actual = reloadedImage.MetaData.ExifProfile;
            Assert.NotNull(actual);
            foreach (KeyValuePair<ExifTag, object> expectedProfileValue in TestProfileValues)
            {
                ExifValue actualProfileValue = actual.GetValue(expectedProfileValue.Key);
                Assert.NotNull(actualProfileValue);
                Assert.Equal(expectedProfileValue.Value, actualProfileValue.Value);
            }
        }

        [Fact]
        public void ProfileToByteArray()
        {
            // arrange
            byte[] exifBytesWithExifCode = ProfileResolver.ExifMarker.Concat(ExifConstants.LittleEndianByteOrderMarker).ToArray();
            byte[] exifBytesWithoutExifCode = ExifConstants.LittleEndianByteOrderMarker;
            ExifProfile expectedProfile = CreateExifProfile();
            var expectedProfileTags = expectedProfile.Values.Select(x => x.Tag).ToList();

            // act
            byte[] actualBytes = expectedProfile.ToByteArray();
            var actualProfile = new ExifProfile(actualBytes);

            // assert
            Assert.NotNull(actualBytes);
            Assert.NotEmpty(actualBytes);
            Assert.Equal(exifBytesWithoutExifCode, actualBytes.Take(exifBytesWithoutExifCode.Length).ToArray());
            foreach (ExifTag expectedProfileTag in expectedProfileTags)
            {
                ExifValue actualProfileValue = actualProfile.GetValue(expectedProfileTag);
                ExifValue expectedProfileValue = expectedProfile.GetValue(expectedProfileTag);
                Assert.Equal(expectedProfileValue.Value, actualProfileValue.Value);
            }
        }

        private static ExifProfile CreateExifProfile()
        {
            var profile = new ExifProfile();

            foreach (KeyValuePair<ExifTag, object> exifProfileValue in TestProfileValues)
            {
                profile.SetValue(exifProfileValue.Key, exifProfileValue.Value);
            }

            return profile;
        }

        internal static ExifProfile GetExifProfile()
        {
            Image<Rgba32> image = TestFile.Create(TestImages.Jpeg.Baseline.Floorplan).CreateImage();

            ExifProfile profile = image.MetaData.ExifProfile;
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
                    throw new ArgumentException("unexpected test image format, only Jpeg and Png are allowed");
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

            Assert.Equal(16, profile.Values.Count());

            foreach (ExifValue value in profile.Values)
            {
                Assert.NotNull(value.Value);

                if (value.Tag == ExifTag.Software)
                {
                    Assert.Equal("Windows Photo Editor 10.0.10011.16384", value.ToString());
                }

                if (value.Tag == ExifTag.XResolution)
                {
                    Assert.Equal(new Rational(300.0), value.Value);
                }

                if (value.Tag == ExifTag.PixelXDimension)
                {
                    Assert.Equal(2338U, value.Value);
                }
            }
        }

        private static void TestValue(ExifValue value, string expected)
        {
            Assert.NotNull(value);
            Assert.Equal(expected, value.Value);
        }

        private static void TestValue(ExifValue value, Rational expected)
        {
            Assert.NotNull(value);
            Assert.Equal(expected, value.Value);
        }

        private static void TestValue(ExifValue value, SignedRational expected)
        {
            Assert.NotNull(value);
            Assert.Equal(expected, value.Value);
        }

        private static void TestValue(ExifValue value, Rational[] expected)
        {
            Assert.NotNull(value);

            Assert.Equal(expected, (ICollection)value.Value);
        }

        private static void TestValue(ExifValue value, double expected)
        {
            Assert.NotNull(value);
            Assert.Equal(expected, value.Value);
        }

        private static void TestValue(ExifValue value, double[] expected)
        {
            Assert.NotNull(value);

            Assert.Equal(expected, (ICollection)value.Value);
        }
    }
}
