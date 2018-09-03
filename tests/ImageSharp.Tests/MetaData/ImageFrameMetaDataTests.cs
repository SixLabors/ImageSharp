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
            const DisposalMethod disposalMethod = DisposalMethod.RestoreToBackground;

            var metaData = new ImageFrameMetaData
            {
                FrameDelay = frameDelay,
                ColorTableLength = colorTableLength,
                DisposalMethod = disposalMethod
            };

            var clone = new ImageFrameMetaData(metaData);

            Assert.Equal(frameDelay, clone.FrameDelay);
            Assert.Equal(colorTableLength, clone.ColorTableLength);
            Assert.Equal(disposalMethod, clone.DisposalMethod);
        }
    }
}
