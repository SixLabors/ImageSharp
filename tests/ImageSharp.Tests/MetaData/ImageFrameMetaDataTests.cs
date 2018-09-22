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

            var metaData = new ImageFrameMetaData();
            GifFrameMetaData gifFrameMetaData = metaData.GetFormatMetaData(GifFormat.Instance);
            gifFrameMetaData.FrameDelay = frameDelay;
            gifFrameMetaData.ColorTableLength = colorTableLength;
            gifFrameMetaData.DisposalMethod = disposalMethod;

            var clone = new ImageFrameMetaData(metaData);
            GifFrameMetaData cloneGifFrameMetaData = clone.GetFormatMetaData(GifFormat.Instance);

            Assert.Equal(frameDelay, cloneGifFrameMetaData.FrameDelay);
            Assert.Equal(colorTableLength, cloneGifFrameMetaData.ColorTableLength);
            Assert.Equal(disposalMethod, cloneGifFrameMetaData.DisposalMethod);
        }

        [Fact]
        public void CloneIsDeep()
        {
            var metaData = new ImageFrameMetaData();
            ImageFrameMetaData clone = metaData.DeepClone();
            Assert.False(metaData.GetFormatMetaData(GifFormat.Instance).Equals(clone.GetFormatMetaData(GifFormat.Instance)));
        }
    }
}
