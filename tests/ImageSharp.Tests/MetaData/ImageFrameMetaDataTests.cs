// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.MetaData;
using Xunit;

namespace SixLabors.ImageSharp.Tests
{
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
            metaData.DisposalMethod = DisposalMethod.RestoreToBackground;

            ImageFrameMetaData clone = new ImageFrameMetaData(metaData);

            Assert.Equal(42, clone.FrameDelay);
            Assert.Equal(DisposalMethod.RestoreToBackground, clone.DisposalMethod);
        }
    }
}
