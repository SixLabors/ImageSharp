// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Formats.Bmp;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Bmp
{
    public class BmpMetaDataTests
    {
        [Fact]
        public void CloneIsDeep()
        {
            var meta = new BmpMetaData() { BitsPerPixel = BmpBitsPerPixel.Pixel24 };
            var clone = (BmpMetaData)meta.DeepClone();

            clone.BitsPerPixel = BmpBitsPerPixel.Pixel32;

            Assert.False(meta.BitsPerPixel.Equals(clone.BitsPerPixel));
        }
    }
}