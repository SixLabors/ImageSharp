// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats
{
    public class InvalidImageDimensionsTests
    {
        [Theory]
        [InlineData("R0lGODdhAgIAAIEAAAD/AP8AAAAA/wAAACwAAAQAAgACAAAIBwADABAQICAAOw==")]
        [InlineData("AAACAAAAAAAAAAAAAgDCsRgAAgAAAP8A/wD/AAAA//8=")]
        public void Load_WithNonPositiveDimensions_ThrowsInvalidImageContentException(string encodedData)
        {
            byte[] data = Convert.FromBase64String(encodedData);

            Assert.Throws<InvalidImageContentException>(() => Image.Load(data));
        }
    }
}
