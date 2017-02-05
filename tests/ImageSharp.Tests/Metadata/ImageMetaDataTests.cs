// <copyright file="ImageMetaDataTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using Xunit;

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
            metaData.Quality = 24;
            metaData.RepeatCount = 1;

            ImageMetaData clone = new ImageMetaData(metaData);

            Assert.Equal(exifProfile.ToByteArray(), clone.ExifProfile.ToByteArray());
            Assert.Equal(42, clone.FrameDelay);
            Assert.Equal(4, clone.HorizontalResolution);
            Assert.Equal(2, clone.VerticalResolution);
            Assert.Equal(imageProperty, clone.Properties[0]);
            Assert.Equal(24, clone.Quality);
            Assert.Equal(1, clone.RepeatCount);
        }
    }
}
