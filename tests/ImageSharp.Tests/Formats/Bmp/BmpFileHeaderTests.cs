// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Formats.Bmp;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Bmp
{
    [Trait("Format", "Bmp")]
    public class BmpFileHeaderTests
    {
        [Fact]
        public void TestWrite()
        {
            var header = new BmpFileHeader(1, 2, 3, 4);

            var buffer = new byte[14];

            header.WriteTo(buffer);

            Assert.Equal("AQACAAAAAwAAAAQAAAA=", Convert.ToBase64String(buffer));
        }
    }
}
