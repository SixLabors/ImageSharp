// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.MetaData;
using SixLabors.ImageSharp.MetaData.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    /// <summary>
    /// Tests the <see cref="ImageMetaData"/> class.
    /// </summary>
    public class ImageMetaDataTests
    {
        [Fact]
        public void ConstructorImageMetaData()
        {
            ImageMetaData metaData = new ImageMetaData();

            ExifProfile exifProfile = new ExifProfile();
            ImageProperty imageProperty = new ImageProperty("name", "value");

            metaData.ExifProfile = exifProfile;
            metaData.FrameDelay = 42;
            metaData.HorizontalResolution = 4;
            metaData.VerticalResolution = 2;
            metaData.Properties.Add(imageProperty);
            metaData.RepeatCount = 1;
            metaData.DisposalMethod = DisposalMethod.RestoreToBackground;

            ImageMetaData clone = new ImageMetaData(metaData);

            Assert.Equal(exifProfile.ToByteArray(), clone.ExifProfile.ToByteArray());
            Assert.Equal(42, clone.FrameDelay);
            Assert.Equal(4, clone.HorizontalResolution);
            Assert.Equal(2, clone.VerticalResolution);
            Assert.Equal(imageProperty, clone.Properties[0]);
            Assert.Equal(1, clone.RepeatCount);
            Assert.Equal(DisposalMethod.RestoreToBackground, clone.DisposalMethod);
        }

        [Fact]
        public void HorizontalResolution()
        {
            ImageMetaData metaData = new ImageMetaData();
            Assert.Equal(96, metaData.HorizontalResolution);

            metaData.HorizontalResolution=0;
            Assert.Equal(96, metaData.HorizontalResolution);

            metaData.HorizontalResolution=-1;
            Assert.Equal(96, metaData.HorizontalResolution);

            metaData.HorizontalResolution=1;
            Assert.Equal(1, metaData.HorizontalResolution);
        }

        [Fact]
        public void VerticalResolution()
        {
            ImageMetaData metaData = new ImageMetaData();
            Assert.Equal(96, metaData.VerticalResolution);

            metaData.VerticalResolution = 0;
            Assert.Equal(96, metaData.VerticalResolution);

            metaData.VerticalResolution = -1;
            Assert.Equal(96, metaData.VerticalResolution);

            metaData.VerticalResolution = 1;
            Assert.Equal(1, metaData.VerticalResolution);
        }

        [Fact]
        public void SyncProfiles()
        {
            ExifProfile exifProfile = new ExifProfile();
            exifProfile.SetValue(ExifTag.XResolution, new Rational(200));
            exifProfile.SetValue(ExifTag.YResolution, new Rational(300));

            Image<Rgba32> image = new Image<Rgba32>(1, 1);
            image.MetaData.ExifProfile = exifProfile;
            image.MetaData.HorizontalResolution = 400;
            image.MetaData.VerticalResolution = 500;

            image.MetaData.SyncProfiles();

            Assert.Equal(400, ((Rational)image.MetaData.ExifProfile.GetValue(ExifTag.XResolution).Value).ToDouble());
            Assert.Equal(500, ((Rational)image.MetaData.ExifProfile.GetValue(ExifTag.YResolution).Value).ToDouble());
        }
    }
}
