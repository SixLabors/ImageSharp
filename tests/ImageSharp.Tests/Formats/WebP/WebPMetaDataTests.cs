// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO;

using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.WebP;
using Xunit;

// ReSharper disable InconsistentNaming

namespace SixLabors.ImageSharp.Tests.Formats.WebP
{
    using static SixLabors.ImageSharp.Tests.TestImages.WebP;
    
    public class WebPMetaDataTests
    {
        [Fact]
        public void CloneIsDeep()
        {
            /*TODO:
            var meta = new WebPMetadata { BitsPerPixel = BmpBitsPerPixel.Pixel24 };
            var clone = (WebPMetadata)meta.DeepClone();

            clone.BitsPerPixel = BmpBitsPerPixel.Pixel32;

            Assert.False(meta.BitsPerPixel.Equals(clone.BitsPerPixel));*/
        }
    }
}
