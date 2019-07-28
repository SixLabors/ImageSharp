// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Metadata;
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

            var metaData = new ImageFrameMetadata();
            GifFrameMetadata gifFrameMetaData = metaData.GetFormatMetadata(GifFormat.Instance);
            gifFrameMetaData.FrameDelay = frameDelay;
            gifFrameMetaData.ColorTableLength = colorTableLength;
            gifFrameMetaData.DisposalMethod = disposalMethod;

            var clone = new ImageFrameMetadata(metaData);
            GifFrameMetadata cloneGifFrameMetaData = clone.GetFormatMetadata(GifFormat.Instance);

            Assert.Equal(frameDelay, cloneGifFrameMetaData.FrameDelay);
            Assert.Equal(colorTableLength, cloneGifFrameMetaData.ColorTableLength);
            Assert.Equal(disposalMethod, cloneGifFrameMetaData.DisposalMethod);
        }

        [Fact]
        public void CloneIsDeep()
        {
            var metaData = new ImageFrameMetadata();
            ImageFrameMetadata clone = metaData.DeepClone();
            Assert.False(metaData.GetFormatMetadata(GifFormat.Instance).Equals(clone.GetFormatMetadata(GifFormat.Instance)));
        }
    }
}
