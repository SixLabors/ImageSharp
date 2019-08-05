// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;

using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Primitives;

using Xunit;

namespace SixLabors.ImageSharp.Tests.MetaData
{
    /// <summary>
    /// Tests the <see cref="ImageMetadata"/> class.
    /// </summary>
    public class ImageMetaDataTests
    {
        [Fact]
        public void ConstructorImageMetaData()
        {
            var metaData = new ImageMetadata();

            var exifProfile = new ExifProfile();
            var pngTextData = new PngTextData("name", "value", "foo", "bar");
            var gifTextData = new List<string>() { "test" }; 

            metaData.ExifProfile = exifProfile;
            metaData.HorizontalResolution = 4;
            metaData.VerticalResolution = 2;
            metaData.PngTextProperties.Add(pngTextData);
            metaData.GifComments.AddRange(gifTextData);

            ImageMetadata clone = metaData.DeepClone();

            Assert.Equal(exifProfile.ToByteArray(), clone.ExifProfile.ToByteArray());
            Assert.Equal(4, clone.HorizontalResolution);
            Assert.Equal(2, clone.VerticalResolution);
            Assert.Equal(pngTextData, clone.PngTextProperties[0]);
            Assert.Equal(gifTextData[0], clone.GifComments[0]);
        }

        [Fact]
        public void CloneIsDeep()
        {
            var metaData = new ImageMetadata();

            var exifProfile = new ExifProfile();
            var pngTextData = new PngTextData("name", "value", "foo", "bar");
            var gifTextData = new List<string>() { "test" };

            metaData.ExifProfile = exifProfile;
            metaData.HorizontalResolution = 4;
            metaData.VerticalResolution = 2;
            metaData.PngTextProperties.Add(pngTextData);
            metaData.GifComments.AddRange(gifTextData);

            ImageMetadata clone = metaData.DeepClone();
            clone.HorizontalResolution = 2;
            clone.VerticalResolution = 4;

            Assert.False(metaData.ExifProfile.Equals(clone.ExifProfile));
            Assert.False(metaData.HorizontalResolution.Equals(clone.HorizontalResolution));
            Assert.False(metaData.VerticalResolution.Equals(clone.VerticalResolution));
            Assert.False(metaData.GifComments.Equals(clone.GifComments));
            Assert.False(metaData.PngTextProperties.Equals(clone.PngTextProperties));
            Assert.False(metaData.GetFormatMetadata(GifFormat.Instance).Equals(clone.GetFormatMetadata(GifFormat.Instance)));
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

            var image = new Image<Rgba32>(1, 1);
            image.Metadata.ExifProfile = exifProfile;
            image.Metadata.HorizontalResolution = 400;
            image.Metadata.VerticalResolution = 500;

            image.Metadata.SyncProfiles();

            Assert.Equal(400, ((Rational)image.Metadata.ExifProfile.GetValue(ExifTag.XResolution).Value).ToDouble());
            Assert.Equal(500, ((Rational)image.Metadata.ExifProfile.GetValue(ExifTag.YResolution).Value).ToDouble());
        }
    }
}
