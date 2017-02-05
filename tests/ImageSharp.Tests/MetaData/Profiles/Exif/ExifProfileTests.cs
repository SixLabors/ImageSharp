// <copyright file="ExifProfileTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System;
    using System.Collections;
    using System.IO;
    using System.Linq;
    using System.Text;
    using Xunit;

    public class ExifProfileTests
    {
        [Fact]
        public void Constructor()
        {
            Image image = TestFile.Create(TestImages.Jpeg.Baseline.Calliphora).CreateImage();

            Assert.Null(image.MetaData.ExifProfile);

            image.MetaData.ExifProfile = new ExifProfile();
            image.MetaData.ExifProfile.SetValue(ExifTag.Copyright, "Dirk Lemstra");

            image = WriteAndRead(image);

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
            Assert.Throws<ArgumentNullException>(() => { new ExifProfile((ExifProfile)null); });

            ExifProfile profile = GetExifProfile();

            ExifProfile clone = new ExifProfile(profile);
            TestProfile(clone);

            profile.SetValue(ExifTag.ColorSpace, (ushort)2);

            clone = new ExifProfile(profile);
            TestProfile(clone);
        }

        [Fact]
        public void WriteFraction()
        {
            using (MemoryStream memStream = new MemoryStream())
            {
                double exposureTime = 1.0 / 1600;

                ExifProfile profile = GetExifProfile();

                profile.SetValue(ExifTag.ExposureTime, new Rational(exposureTime));

                Image image = new Image(1, 1);
                image.MetaData.ExifProfile = profile;

                image.SaveAsJpeg(memStream);

                memStream.Position = 0;
                image = new Image(memStream);

                profile = image.MetaData.ExifProfile;
                Assert.NotNull(profile);

                ExifValue value = profile.GetValue(ExifTag.ExposureTime);
                Assert.NotNull(value);
                Assert.NotEqual(exposureTime, ((Rational)value.Value).ToDouble());

                memStream.Position = 0;
                profile = GetExifProfile();

                profile.SetValue(ExifTag.ExposureTime, new Rational(exposureTime, true));
                image.MetaData.ExifProfile = profile;

                image.SaveAsJpeg(memStream);

                memStream.Position = 0;
                image = new Image(memStream);

                profile = image.MetaData.ExifProfile;
                Assert.NotNull(profile);

                value = profile.GetValue(ExifTag.ExposureTime);
                Assert.Equal(exposureTime, ((Rational)value.Value).ToDouble());
            }
        }

        [Fact]
        public void ReadWriteInfinity()
        {
            Image image = TestFile.Create(TestImages.Jpeg.Baseline.Floorplan).CreateImage();
            image.MetaData.ExifProfile.SetValue(ExifTag.ExposureBiasValue, new SignedRational(double.PositiveInfinity));

            image = WriteAndRead(image);
            ExifValue value = image.MetaData.ExifProfile.GetValue(ExifTag.ExposureBiasValue);
            Assert.NotNull(value);
            Assert.Equal(new SignedRational(double.PositiveInfinity), value.Value);

            image.MetaData.ExifProfile.SetValue(ExifTag.ExposureBiasValue, new SignedRational(double.NegativeInfinity));

            image = WriteAndRead(image);
            value = image.MetaData.ExifProfile.GetValue(ExifTag.ExposureBiasValue);
            Assert.NotNull(value);
            Assert.Equal(new SignedRational(double.NegativeInfinity), value.Value);

            image.MetaData.ExifProfile.SetValue(ExifTag.FlashEnergy, new Rational(double.NegativeInfinity));

            image = WriteAndRead(image);
            value = image.MetaData.ExifProfile.GetValue(ExifTag.FlashEnergy);
            Assert.NotNull(value);
            Assert.Equal(new Rational(double.PositiveInfinity), value.Value);
        }

        [Fact]
        public void SetValue()
        {
            Rational[] latitude = new Rational[] { new Rational(12.3), new Rational(4.56), new Rational(789.0) };

            Image image = TestFile.Create(TestImages.Jpeg.Baseline.Floorplan).CreateImage();
            image.MetaData.ExifProfile.SetValue(ExifTag.Software, "ImageSharp");

            ExifValue value = image.MetaData.ExifProfile.GetValue(ExifTag.Software);
            TestValue(value, "ImageSharp");

            Assert.Throws<ArgumentException>(() => { value.Value = 15; });

            image.MetaData.ExifProfile.SetValue(ExifTag.ShutterSpeedValue, new SignedRational(75.55));

            value = image.MetaData.ExifProfile.GetValue(ExifTag.ShutterSpeedValue);

            TestValue(value, new SignedRational(7555, 100));

            Assert.Throws<ArgumentException>(() => { value.Value = 75; });

            image.MetaData.ExifProfile.SetValue(ExifTag.XResolution, new Rational(150.0));

            // We also need to change this value because this overrides XResolution when the image is written.
            image.MetaData.HorizontalResolution = 150.0;

            value = image.MetaData.ExifProfile.GetValue(ExifTag.XResolution);
            TestValue(value, new Rational(150, 1));

            Assert.Throws<ArgumentException>(() => { value.Value = "ImageSharp"; });

            image.MetaData.ExifProfile.SetValue(ExifTag.ReferenceBlackWhite, null);

            value = image.MetaData.ExifProfile.GetValue(ExifTag.ReferenceBlackWhite);
            TestValue(value, (string)null);

            image.MetaData.ExifProfile.SetValue(ExifTag.GPSLatitude, latitude);

            value = image.MetaData.ExifProfile.GetValue(ExifTag.GPSLatitude);
            TestValue(value, latitude);

            image = WriteAndRead(image);

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

            image = WriteAndRead(image);

            Assert.NotNull(image.MetaData.ExifProfile);
            Assert.Equal(8, image.MetaData.ExifProfile.Values.Count());

            Assert.NotNull(image.MetaData.ExifProfile.GetValue(ExifTag.ColorSpace));
            Assert.True(image.MetaData.ExifProfile.RemoveValue(ExifTag.ColorSpace));
            Assert.False(image.MetaData.ExifProfile.RemoveValue(ExifTag.ColorSpace));
            Assert.Null(image.MetaData.ExifProfile.GetValue(ExifTag.ColorSpace));

            Assert.Equal(7, image.MetaData.ExifProfile.Values.Count());
        }

        [Fact]
        public void Values()
        {
            ExifProfile profile = GetExifProfile();

            TestProfile(profile);

            var thumbnail = profile.CreateThumbnail<Color>();
            Assert.NotNull(thumbnail);
            Assert.Equal(256, thumbnail.Width);
            Assert.Equal(170, thumbnail.Height);
        }

        [Fact]
        public void WriteTooLargeProfile()
        {
            StringBuilder junk = new StringBuilder();
            for (int i = 0; i < 65500; i++)
            {
                junk.Append("I");
            }

            Image image = new Image(100, 100);
            image.MetaData.ExifProfile = new ExifProfile();
            image.MetaData.ExifProfile.SetValue(ExifTag.ImageDescription, junk.ToString());

            using (MemoryStream memStream = new MemoryStream())
            {
                Assert.Throws<ImageFormatException>(() => image.SaveAsJpeg(memStream));
            }
        }

        private static ExifProfile GetExifProfile()
        {
            Image image = TestFile.Create(TestImages.Jpeg.Baseline.Floorplan).CreateImage();

            ExifProfile profile = image.MetaData.ExifProfile;
            Assert.NotNull(profile);

            return profile;
        }

        private static Image WriteAndRead(Image image)
        {
            using (MemoryStream memStream = new MemoryStream())
            {
                image.SaveAsJpeg(memStream);
                image.Dispose();

                memStream.Position = 0;
                return new Image(memStream);
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
                    Assert.Equal("Windows Photo Editor 10.0.10011.16384", value.ToString());

                if (value.Tag == ExifTag.XResolution)
                    Assert.Equal(new Rational(300.0), value.Value);

                if (value.Tag == ExifTag.PixelXDimension)
                    Assert.Equal(2338U, value.Value);
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
