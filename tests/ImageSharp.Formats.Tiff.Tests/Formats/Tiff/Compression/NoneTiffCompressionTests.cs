// <copyright file="NoneTiffCompressionTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System.IO;
    using Xunit;

    using ImageSharp.Formats;

    public class NoneTiffCompressionTests
    {
        [Theory]
        [InlineData(new byte[] { 10, 15, 20, 25, 30, 35, 40, 45 }, 8, new byte[] { 10, 15, 20, 25, 30, 35, 40, 45 })]
        [InlineData(new byte[] { 10, 15, 20, 25, 30, 35, 40, 45 }, 5, new byte[] { 10, 15, 20, 25, 30 })]
        public void Decompress_ReadsData(byte[] inputData, int byteCount, byte[] expectedResult)
        {
            Stream stream = new MemoryStream(inputData);
            byte[] buffer = new byte[expectedResult.Length];

            NoneTiffCompression.Decompress(stream, byteCount, buffer);

            Assert.Equal(expectedResult, buffer);
        }
    }
}