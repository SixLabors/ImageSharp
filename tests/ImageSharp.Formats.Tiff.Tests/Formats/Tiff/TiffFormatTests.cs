// <copyright file="TiffFormatTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System.Linq;
    using Xunit;

    using ImageSharp.Formats;

    public class TiffFormatTests
    {
        [Fact]
        public void FormatProperties_AreAsExpected()
        {
            TiffFormat tiffFormat = new TiffFormat();

            Assert.Equal("TIFF", tiffFormat.Name);
            Assert.Equal("image/tiff", tiffFormat.DefaultMimeType);
            Assert.Contains("image/tiff", tiffFormat.MimeTypes);
            Assert.Contains("tif", tiffFormat.FileExtensions);
            Assert.Contains("tiff", tiffFormat.FileExtensions);
        }
    }
}
