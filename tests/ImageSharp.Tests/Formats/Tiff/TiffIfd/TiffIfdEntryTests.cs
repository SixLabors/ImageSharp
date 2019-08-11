// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Tests
{
    using Xunit;

    using ImageSharp.Formats.Tiff;

    [Trait("Category", "Tiff")]
    public class TiffIfdEntryTests
    {
        [Fact]
        public void Constructor_SetsProperties()
        {
            var entry = new TiffIfdEntry((ushort)10u, TiffType.Short, 20u, new byte[] { 2, 4, 6, 8 });

            Assert.Equal(10u, entry.Tag);
            Assert.Equal(TiffType.Short, entry.Type);
            Assert.Equal(20u, entry.Count);
            Assert.Equal(new byte[] { 2, 4, 6, 8 }, entry.Value);
        }
    }
}
