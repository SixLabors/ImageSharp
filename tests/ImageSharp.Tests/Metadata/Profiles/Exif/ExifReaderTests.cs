// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    public class ExifReaderTests
    {
        [Fact]
        public void Read_DataIsEmpty_ReturnsEmptyCollection()
        {
            var reader = new ExifReader(Array.Empty<byte>());

            IList<IExifValue> result = reader.ReadValues();

            Assert.Equal(0, result.Count);
        }

        [Fact]
        public void Read_DataIsMinimal_ReturnsEmptyCollection()
        {
            var reader = new ExifReader(new byte[] { 69, 120, 105, 102, 0, 0 });

            IList<IExifValue> result = reader.ReadValues();

            Assert.Equal(0, result.Count);
        }
    }
}
