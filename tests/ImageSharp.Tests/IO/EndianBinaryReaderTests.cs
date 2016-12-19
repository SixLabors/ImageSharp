// <copyright file="EndianBinaryReaderTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.IO
{
    using System;
    using System.IO;
    using System.Text;

    using ImageSharp.IO;

    using Xunit;

    /// <summary>
    /// The endian binary reader tests.
    /// </summary>
    public class EndianBinaryReaderTests
    {
        /// <summary>
        /// The test string.
        /// </summary>
        private const string TestString = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmopqrstuvwxyz";

        /// <summary>
        /// The test bytes.
        /// </summary>
        private static readonly byte[] TestBytes = Encoding.ASCII.GetBytes(TestString);

        /// <summary>
        /// Tests to ensure that the reader can read beyond internal buffer size.
        /// </summary>
        [Fact]
        public void ReadCharsBeyondInternalBufferSize()
        {
            MemoryStream stream = new MemoryStream(TestBytes);
            using (EndianBinaryReader subject = new EndianBinaryReader(EndianBitConverter.Little, stream))
            {
                char[] chars = new char[TestString.Length];
                subject.Read(chars, 0, chars.Length);
                Assert.Equal(TestString, new string(chars));
            }
        }

        /// <summary>
        /// Tests to ensure that the reader cannot read beyond the provided buffer size.
        /// </summary>
        [Fact]
        public void ReadCharsBeyondProvidedBufferSize()
        {
            Assert.Throws(
                typeof(ArgumentException),
                () =>
                    {
                        MemoryStream stream = new MemoryStream(TestBytes);
                        using (EndianBinaryReader subject = new EndianBinaryReader(EndianBitConverter.Little, stream))
                        {
                            char[] chars = new char[TestString.Length - 1];

                            subject.Read(chars, 0, TestString.Length);
                        }
                    });
        }
    }
}
