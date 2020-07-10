// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Metadata;
using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    /// <summary>
    /// Tests the <see cref="ImageFrameMetadataTests"/> class.
    /// </summary>
    public class ImageFrameMetadataTests
    {
        [Fact]
        public void ConstructorImageFrameMetadata()
        {
            const int frameDelay = 42;
            const int colorTableLength = 128;
            const GifDisposalMethod disposalMethod = GifDisposalMethod.RestoreToBackground;

            var metaData = new ImageFrameMetadata();
            GifFrameMetadata gifFrameMetadata = metaData.GetGifMetadata();
            gifFrameMetadata.FrameDelay = frameDelay;
            gifFrameMetadata.ColorTableLength = colorTableLength;
            gifFrameMetadata.DisposalMethod = disposalMethod;

            var clone = new ImageFrameMetadata(metaData);
            GifFrameMetadata cloneGifFrameMetadata = clone.GetGifMetadata();

            Assert.Equal(frameDelay, cloneGifFrameMetadata.FrameDelay);
            Assert.Equal(colorTableLength, cloneGifFrameMetadata.ColorTableLength);
            Assert.Equal(disposalMethod, cloneGifFrameMetadata.DisposalMethod);
        }

        [Fact]
        public void CloneIsDeep()
        {
            var metaData = new ImageFrameMetadata();
            ImageFrameMetadata clone = metaData.DeepClone();
            Assert.False(metaData.GetGifMetadata().Equals(clone.GetGifMetadata()));
        }
    }
}
