// <copyright file="ImageFrameMetaDataTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using Xunit;

    /// <summary>
    /// Tests the <see cref="ImageFrameMetaDataTests"/> class.
    /// </summary>
    public class ImageFrameMetaDataTests
    {
        [Fact]
        public void ConstructorImageFrameMetaData()
        {
            ImageFrameMetaData metaData = new ImageFrameMetaData();
            metaData.FrameDelay = 42;

            ImageFrameMetaData clone = new ImageFrameMetaData(metaData);

            Assert.Equal(42, clone.FrameDelay);
        }
    }
}
