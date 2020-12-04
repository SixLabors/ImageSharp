// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Formats.Png.Zlib;
using Xunit;
using SharpCrc32 = ICSharpCode.SharpZipLib.Checksum.Crc32;

namespace SixLabors.ImageSharp.Tests.Formats.Png
{
    [Trait("Format", "Png")]
    public class Crc32Tests
    {
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public void ReturnsCorrectWhenEmpty(uint input)
        {
            Assert.Equal(input, Crc32.Calculate(input, default));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(8)]
        [InlineData(215)]
        [InlineData(1024)]
        [InlineData(1024 + 15)]
        [InlineData(2034)]
        [InlineData(4096)]
        public void MatchesReference(int length)
        {
            var data = GetBuffer(length);
            var crc = new SharpCrc32();
            crc.Update(data);

            long expected = crc.Value;
            long actual = Crc32.Calculate(data);

            Assert.Equal(expected, actual);
        }

        private static byte[] GetBuffer(int length)
        {
            var data = new byte[length];
            new Random(1).NextBytes(data);

            return data;
        }
    }
}
