// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Formats.Gif;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Gif
{
    public class GifFrameMetaDataTests
    {
        [Fact]
        public void CloneIsDeep()
        {
            var meta = new GifFrameMetaData()
            {
                FrameDelay = 1,
                DisposalMethod = GifDisposalMethod.RestoreToBackground,
                ColorTableLength = 2
            };

            var clone = (GifFrameMetaData)meta.DeepClone();

            clone.FrameDelay = 2;
            clone.DisposalMethod = GifDisposalMethod.RestoreToPrevious;
            clone.ColorTableLength = 1;

            Assert.False(meta.FrameDelay.Equals(clone.FrameDelay));
            Assert.False(meta.DisposalMethod.Equals(clone.DisposalMethod));
            Assert.False(meta.ColorTableLength.Equals(clone.ColorTableLength));
        }
    }
}
