// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Metadata
{
    /// <summary>
    /// Tests the <see cref="ImageMetadata"/> class.
    /// </summary>
    public class ImageMetadataTests
    {
        [Fact]
        public void ConstructorImageMetadata()
        {
            var metaData = new ImageMetadata();

            var exifProfile = new ExifProfile();

            metaData.ExifProfile = exifProfile;
            metaData.HorizontalResolution = 4;
            metaData.VerticalResolution = 2;

            ImageMetadata clone = metaData.DeepClone();

            Assert.Equal(exifProfile.ToByteArray(), clone.ExifProfile.ToByteArray());
            Assert.Equal(4, clone.HorizontalResolution);
            Assert.Equal(2, clone.VerticalResolution);
        }

        [Fact]
        public void CloneIsDeep()
        {
            var metaData = new ImageMetadata
            {
                ExifProfile = new ExifProfile(),
                HorizontalResolution = 4,
                VerticalResolution = 2
            };

            ImageMetadata clone = metaData.DeepClone();
            clone.HorizontalResolution = 2;
            clone.VerticalResolution = 4;

            Assert.False(metaData.ExifProfile.Equals(clone.ExifProfile));
            Assert.False(metaData.HorizontalResolution.Equals(clone.HorizontalResolution));
            Assert.False(metaData.VerticalResolution.Equals(clone.VerticalResolution));
        }

        [Fact]
        public void HorizontalResolution()
        {
            var metaData = new ImageMetadata();
            Assert.Equal(96, metaData.HorizontalResolution);

            metaData.HorizontalResolution = 0;
            Assert.Equal(96, metaData.HorizontalResolution);

            metaData.HorizontalResolution = -1;
            Assert.Equal(96, metaData.HorizontalResolution);

            metaData.HorizontalResolution = 1;
            Assert.Equal(1, metaData.HorizontalResolution);
        }

        [Fact]
        public void VerticalResolution()
        {
            var metaData = new ImageMetadata();
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
            var exifProfile = new ExifProfile();
            exifProfile.SetValue(ExifTag.XResolution, new Rational(200));
            exifProfile.SetValue(ExifTag.YResolution, new Rational(300));

            using (var image = new Image<Rgba32>(1, 1))
            {
                image.Metadata.ExifProfile = exifProfile;
                image.Metadata.HorizontalResolution = 400;
                image.Metadata.VerticalResolution = 500;

                image.Metadata.SyncProfiles();

                Assert.Equal(400, ((Rational)image.Metadata.ExifProfile.GetValue(ExifTag.XResolution).Value).ToDouble());
                Assert.Equal(500, ((Rational)image.Metadata.ExifProfile.GetValue(ExifTag.YResolution).Value).ToDouble());
            }
        }
    }
}
