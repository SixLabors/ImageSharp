// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

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
            const int frameDelay = 42;
            const int colorTableLength = 128;
            const GifDisposalMethod disposalMethod = GifDisposalMethod.RestoreToBackground;

            var gifFrameMetaData = new GifFrameMetaData
            {
                FrameDelay = frameDelay,
                ColorTableLength = colorTableLength,
                DisposalMethod = disposalMethod
            };

            var metaData = new ImageFrameMetaData();
            metaData.AddOrUpdateGifFrameMetaData(gifFrameMetaData);

            var clone = new ImageFrameMetaData(metaData);
            GifFrameMetaData cloneGifFrameMetaData = clone.GetGifFrameMetaData();

            Assert.Equal(frameDelay, cloneGifFrameMetaData.FrameDelay);
            Assert.Equal(colorTableLength, cloneGifFrameMetaData.ColorTableLength);
            Assert.Equal(disposalMethod, cloneGifFrameMetaData.DisposalMethod);
        }
    }
}
