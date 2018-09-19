// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Formats.Png;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Png
{
    public class PngMetaDataTests
    {
        [Fact]
        public void CloneIsDeep()
        {
            var meta = new PngMetaData()
            {
                BitDepth = PngBitDepth.Bit16,
                ColorType = PngColorType.GrayscaleWithAlpha,
                Gamma = 2
            };
            var clone = (PngMetaData)meta.DeepClone();

            clone.BitDepth = PngBitDepth.Bit2;
            clone.ColorType = PngColorType.Palette;
            clone.Gamma = 1;

            Assert.False(meta.BitDepth.Equals(clone.BitDepth));
            Assert.False(meta.ColorType.Equals(clone.ColorType));
            Assert.False(meta.Gamma.Equals(clone.Gamma));
        }
    }
}