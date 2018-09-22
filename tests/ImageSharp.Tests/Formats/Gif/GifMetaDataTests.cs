// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Formats.Gif;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Gif
{
    public class GifMetaDataTests
    {
        [Fact]
        public void CloneIsDeep()
        {
            var meta = new GifMetaData()
            {
                RepeatCount = 1,
                ColorTableMode = GifColorTableMode.Global,
                GlobalColorTableLength = 2
            };

            var clone = (GifMetaData)meta.DeepClone();

            clone.RepeatCount = 2;
            clone.ColorTableMode = GifColorTableMode.Local;
            clone.GlobalColorTableLength = 1;

            Assert.False(meta.RepeatCount.Equals(clone.RepeatCount));
            Assert.False(meta.ColorTableMode.Equals(clone.ColorTableMode));
            Assert.False(meta.GlobalColorTableLength.Equals(clone.GlobalColorTableLength));
        }
    }
}
