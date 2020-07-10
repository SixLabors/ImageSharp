// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Text;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Common
{
    public class EncoderExtensionsTests
    {
        [Fact]
        public void GetString_EmptyBuffer_ReturnsEmptyString()
        {
            var buffer = default(ReadOnlySpan<byte>);

            string result = Encoding.UTF8.GetString(buffer);

            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void GetString_Buffer_ReturnsString()
        {
            var buffer = new ReadOnlySpan<byte>(new byte[] { 73, 109, 97, 103, 101, 83, 104, 97, 114, 112 });

            string result = Encoding.UTF8.GetString(buffer);

            Assert.Equal("ImageSharp", result);
        }
    }
}
