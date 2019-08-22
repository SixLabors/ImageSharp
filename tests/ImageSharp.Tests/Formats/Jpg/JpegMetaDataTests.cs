// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Formats.Jpeg;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Jpg
{
    public class JpegMetaDataTests
    {
        [Fact]
        public void CloneIsDeep()
        {
            var meta = new JpegMetadata { Quality = 50 };
            var clone = (JpegMetadata)meta.DeepClone();

            clone.Quality = 99;

            Assert.False(meta.Quality.Equals(clone.Quality));
        }
    }
}
