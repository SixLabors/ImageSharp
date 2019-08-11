// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Formats.Tiff;
using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    [Trait("Category", "Tiff")]
    public class TiffFormatTests
    {
        [Fact]
        public void FormatProperties_AreAsExpected()
        {
            TiffFormat tiffFormat = TiffFormat.Instance;

            Assert.Equal("TIFF", tiffFormat.Name);
            Assert.Equal("image/tiff", tiffFormat.DefaultMimeType);
            Assert.Contains("image/tiff", tiffFormat.MimeTypes);
            Assert.Contains("tif", tiffFormat.FileExtensions);
            Assert.Contains("tiff", tiffFormat.FileExtensions);
        }
    }
}
