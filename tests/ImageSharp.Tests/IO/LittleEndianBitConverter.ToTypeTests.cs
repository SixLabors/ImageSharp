// <copyright file="LittleEndianBitConverter.ToTypeTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.IO
{
    using System;
    using ImageSharp.IO;
    using Xunit;

    /// <summary>
    /// The <see cref="NativeBitConverter"/> tests.
    /// </summary>
    public class LittleEndianBitConverterToTypeTests
    {
        [Fact]
        public void CopyToWithNullBufferThrowsException()
        {
            Assert.Throws<ArgumentNullException>(() => EndianBitConverter.GetConverter(Endianness.LittleEndian).ToBoolean(null, 0));
            Assert.Throws<ArgumentNullException>(() => EndianBitConverter.GetConverter(Endianness.LittleEndian).ToInt16(null, 0));
            Assert.Throws<ArgumentNullException>(() => EndianBitConverter.GetConverter(Endianness.LittleEndian).ToUInt16(null, 0));
            Assert.Throws<ArgumentNullException>(() => EndianBitConverter.GetConverter(Endianness.LittleEndian).ToInt32(null, 0));
            Assert.Throws<ArgumentNullException>(() => EndianBitConverter.GetConverter(Endianness.LittleEndian).ToUInt32(null, 0));
            Assert.Throws<ArgumentNullException>(() => EndianBitConverter.GetConverter(Endianness.LittleEndian).ToInt64(null, 0));
            Assert.Throws<ArgumentNullException>(() => EndianBitConverter.GetConverter(Endianness.LittleEndian).ToUInt64(null, 0));
        }

        [Fact]
        public void CopyToWithIndexTooBigThrowsException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => EndianBitConverter.GetConverter(Endianness.LittleEndian).ToBoolean(new byte[1], 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => EndianBitConverter.GetConverter(Endianness.LittleEndian).ToInt16(new byte[2], 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => EndianBitConverter.GetConverter(Endianness.LittleEndian).ToUInt16(new byte[2], 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => EndianBitConverter.GetConverter(Endianness.LittleEndian).ToInt32(new byte[4], 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => EndianBitConverter.GetConverter(Endianness.LittleEndian).ToUInt32(new byte[4], 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => EndianBitConverter.GetConverter(Endianness.LittleEndian).ToInt64(new byte[8], 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => EndianBitConverter.GetConverter(Endianness.LittleEndian).ToUInt64(new byte[8], 1));
        }

        [Fact]
        public void CopyToWithBufferTooSmallThrowsException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => EndianBitConverter.GetConverter(Endianness.LittleEndian).ToBoolean(new byte[0], 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => EndianBitConverter.GetConverter(Endianness.LittleEndian).ToInt16(new byte[1], 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => EndianBitConverter.GetConverter(Endianness.LittleEndian).ToUInt16(new byte[1], 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => EndianBitConverter.GetConverter(Endianness.LittleEndian).ToInt32(new byte[3], 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => EndianBitConverter.GetConverter(Endianness.LittleEndian).ToUInt32(new byte[3], 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => EndianBitConverter.GetConverter(Endianness.LittleEndian).ToInt64(new byte[7], 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => EndianBitConverter.GetConverter(Endianness.LittleEndian).ToUInt64(new byte[7], 0));
        }

        /// <summary>
        /// Tests that passing a <see cref="bool"/> returns the correct bytes.
        /// </summary>
        [Fact]
        public void ToBoolean()
        {
            Assert.Equal(false, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToBoolean(new byte[] { 0 }, 0));
            Assert.Equal(true, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToBoolean(new byte[] { 1 }, 0));

            Assert.Equal(false, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToBoolean(new byte[] { 1, 0 }, 1));
            Assert.Equal(true, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToBoolean(new byte[] { 0, 1 }, 1));
        }

        /// <summary>
        /// Tests that passing a <see cref="short"/> returns the correct bytes.
        /// </summary>
        [Fact]
        public void ToInt16()
        {
            Assert.Equal((short)0, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToInt16(new byte[] { 0, 0 }, 0));
            Assert.Equal((short)1, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToInt16(new byte[] { 1, 0 }, 0));
            Assert.Equal((short)256, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToInt16(new byte[] { 0, 1 }, 0));
            Assert.Equal((short)-1, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToInt16(new byte[] { 255, 255 }, 0));
            Assert.Equal((short)257, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToInt16(new byte[] { 1, 1 }, 0));

            Assert.Equal((short)0, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToInt16(new byte[] { 1, 0, 0 }, 1));
            Assert.Equal((short)1, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToInt16(new byte[] { 0, 1, 0 }, 1));
            Assert.Equal((short)256, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToInt16(new byte[] { 1, 0, 1 }, 1));
            Assert.Equal((short)-1, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToInt16(new byte[] { 0, 255, 255 }, 1));
            Assert.Equal((short)257, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToInt16(new byte[] { 0, 1, 1 }, 1));
        }

        /// <summary>
        /// Tests that passing a <see cref="ushort"/> returns the correct bytes.
        /// </summary>
        [Fact]
        public void ToUInt16()
        {
            Assert.Equal((ushort)0, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToUInt16(new byte[] { 0, 0 }, 0));
            Assert.Equal((ushort)1, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToUInt16(new byte[] { 1, 0 }, 0));
            Assert.Equal((ushort)256, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToUInt16(new byte[] { 0, 1 }, 0));
            Assert.Equal(ushort.MaxValue, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToUInt16(new byte[] { 255, 255 }, 0));
            Assert.Equal((ushort)257, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToUInt16(new byte[] { 1, 1 }, 0));

            Assert.Equal((ushort)0, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToUInt16(new byte[] { 1, 0, 0 }, 1));
            Assert.Equal((ushort)1, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToUInt16(new byte[] { 0, 1, 0 }, 1));
            Assert.Equal((ushort)256, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToUInt16(new byte[] { 1, 0, 1 }, 1));
            Assert.Equal(ushort.MaxValue, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToUInt16(new byte[] { 0, 255, 255 }, 1));
            Assert.Equal((ushort)257, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToUInt16(new byte[] { 0, 1, 1 }, 1));
        }

        /// <summary>
        /// Tests that passing a <see cref="int"/> returns the correct bytes.
        /// </summary>
        [Fact]
        public void ToInt32()
        {
            Assert.Equal(0, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToInt32(new byte[] { 0, 0, 0, 0 }, 0));
            Assert.Equal(1, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToInt32(new byte[] { 1, 0, 0, 0 }, 0));
            Assert.Equal(256, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToInt32(new byte[] { 0, 1, 0, 0 }, 0));
            Assert.Equal(65536, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToInt32(new byte[] { 0, 0, 1, 0 }, 0));
            Assert.Equal(16777216, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToInt32(new byte[] { 0, 0, 0, 1 }, 0));
            Assert.Equal(-1, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToInt32(new byte[] { 255, 255, 255, 255 }, 0));
            Assert.Equal(257, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToInt32(new byte[] { 1, 1, 0, 0 }, 0));

            Assert.Equal(0, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToInt32(new byte[] { 1, 0, 0, 0, 0 }, 1));
            Assert.Equal(1, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToInt32(new byte[] { 0, 1, 0, 0, 0 }, 1));
            Assert.Equal(256, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToInt32(new byte[] { 1, 0, 1, 0, 0 }, 1));
            Assert.Equal(65536, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToInt32(new byte[] { 1, 0, 0, 1, 0 }, 1));
            Assert.Equal(16777216, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToInt32(new byte[] { 1, 0, 0, 0, 1 }, 1));
            Assert.Equal(-1, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToInt32(new byte[] { 0, 255, 255, 255, 255 }, 1));
            Assert.Equal(257, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToInt32(new byte[] { 0, 1, 1, 0, 0 }, 1));
        }

        /// <summary>
        /// Tests that passing a <see cref="uint"/> returns the correct bytes.
        /// </summary>
        [Fact]
        public void ToUInt32()
        {
            Assert.Equal((uint)0, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToUInt32(new byte[] { 0, 0, 0, 0 }, 0));
            Assert.Equal((uint)1, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToUInt32(new byte[] { 1, 0, 0, 0 }, 0));
            Assert.Equal((uint)256, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToUInt32(new byte[] { 0, 1, 0, 0 }, 0));
            Assert.Equal((uint)65536, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToUInt32(new byte[] { 0, 0, 1, 0 }, 0));
            Assert.Equal((uint)16777216, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToUInt32(new byte[] { 0, 0, 0, 1 }, 0));
            Assert.Equal(uint.MaxValue, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToUInt32(new byte[] { 255, 255, 255, 255 }, 0));
            Assert.Equal((uint)257, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToUInt32(new byte[] { 1, 1, 0, 0 }, 0));

            Assert.Equal((uint)0, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToUInt32(new byte[] { 1, 0, 0, 0, 0 }, 1));
            Assert.Equal((uint)1, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToUInt32(new byte[] { 0, 1, 0, 0, 0 }, 1));
            Assert.Equal((uint)256, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToUInt32(new byte[] { 1, 0, 1, 0, 0 }, 1));
            Assert.Equal((uint)65536, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToUInt32(new byte[] { 1, 0, 0, 1, 0 }, 1));
            Assert.Equal((uint)16777216, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToUInt32(new byte[] { 1, 0, 0, 0, 1 }, 1));
            Assert.Equal(uint.MaxValue, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToUInt32(new byte[] { 0, 255, 255, 255, 255 }, 1));
            Assert.Equal((uint)257, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToUInt32(new byte[] { 0, 1, 1, 0, 0 }, 1));
        }

        /// <summary>
        /// Tests that passing a <see cref="long"/> returns the correct bytes.
        /// </summary>
        [Fact]
        public void ToInt64()
        {
            Assert.Equal(0L, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToInt64(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 }, 0));
            Assert.Equal(1L, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToInt64(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0 }, 0));
            Assert.Equal(256L, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToInt64(new byte[] { 0, 1, 0, 0, 0, 0, 0, 0 }, 0));
            Assert.Equal(65536L, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToInt64(new byte[] { 0, 0, 1, 0, 0, 0, 0, 0 }, 0));
            Assert.Equal(16777216L, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToInt64(new byte[] { 0, 0, 0, 1, 0, 0, 0, 0 }, 0));
            Assert.Equal(4294967296L, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToInt64(new byte[] { 0, 0, 0, 0, 1, 0, 0, 0 }, 0));
            Assert.Equal(1099511627776L, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToInt64(new byte[] { 0, 0, 0, 0, 0, 1, 0, 0 }, 0));
            Assert.Equal(1099511627776L * 256, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToInt64(new byte[] { 0, 0, 0, 0, 0, 0, 1, 0 }, 0));
            Assert.Equal(1099511627776L * 256 * 256, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToInt64(new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 }, 0));
            Assert.Equal(-1L, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToInt64(new byte[] { 255, 255, 255, 255, 255, 255, 255, 255 }, 0));
            Assert.Equal(257L, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToInt64(new byte[] { 1, 1, 0, 0, 0, 0, 0, 0 }, 0));

            Assert.Equal(0L, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToInt64(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0, 0 }, 1));
            Assert.Equal(1L, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToInt64(new byte[] { 0, 1, 0, 0, 0, 0, 0, 0, 0 }, 1));
            Assert.Equal(256L, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToInt64(new byte[] { 1, 0, 1, 0, 0, 0, 0, 0, 0 }, 1));
            Assert.Equal(65536L, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToInt64(new byte[] { 1, 0, 0, 1, 0, 0, 0, 0, 0 }, 1));
            Assert.Equal(16777216L, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToInt64(new byte[] { 1, 0, 0, 0, 1, 0, 0, 0, 0 }, 1));
            Assert.Equal(4294967296L, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToInt64(new byte[] { 1, 0, 0, 0, 0, 1, 0, 0, 0 }, 1));
            Assert.Equal(1099511627776L, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToInt64(new byte[] { 1, 0, 0, 0, 0, 0, 1, 0, 0 }, 1));
            Assert.Equal(1099511627776L * 256, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToInt64(new byte[] { 1, 0, 0, 0, 0, 0, 0, 1, 0 }, 1));
            Assert.Equal(1099511627776L * 256 * 256, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToInt64(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0, 1 }, 1));
            Assert.Equal(-1L, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToInt64(new byte[] { 0, 255, 255, 255, 255, 255, 255, 255, 255 }, 1));
            Assert.Equal(257L, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToInt64(new byte[] { 0, 1, 1, 0, 0, 0, 0, 0, 0 }, 1));
        }

        /// <summary>
        /// Tests that passing a <see cref="ulong"/> returns the correct bytes.
        /// </summary>
        [Fact]
        public void ToUInt64()
        {
            Assert.Equal(0UL, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToUInt64(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 }, 0));
            Assert.Equal(1UL, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToUInt64(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0 }, 0));
            Assert.Equal(256UL, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToUInt64(new byte[] { 0, 1, 0, 0, 0, 0, 0, 0 }, 0));
            Assert.Equal(65536UL, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToUInt64(new byte[] { 0, 0, 1, 0, 0, 0, 0, 0 }, 0));
            Assert.Equal(16777216UL, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToUInt64(new byte[] { 0, 0, 0, 1, 0, 0, 0, 0 }, 0));
            Assert.Equal(4294967296UL, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToUInt64(new byte[] { 0, 0, 0, 0, 1, 0, 0, 0 }, 0));
            Assert.Equal(1099511627776UL, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToUInt64(new byte[] { 0, 0, 0, 0, 0, 1, 0, 0 }, 0));
            Assert.Equal(1099511627776UL * 256, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToUInt64(new byte[] { 0, 0, 0, 0, 0, 0, 1, 0 }, 0));
            Assert.Equal(1099511627776UL * 256 * 256, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToUInt64(new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 }, 0));
            Assert.Equal(ulong.MaxValue, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToUInt64(new byte[] { 255, 255, 255, 255, 255, 255, 255, 255 }, 0));
            Assert.Equal(257UL, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToUInt64(new byte[] { 1, 1, 0, 0, 0, 0, 0, 0 }, 0));

            Assert.Equal(0UL, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToUInt64(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0, 0 }, 1));
            Assert.Equal(1UL, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToUInt64(new byte[] { 0, 1, 0, 0, 0, 0, 0, 0, 0 }, 1));
            Assert.Equal(256UL, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToUInt64(new byte[] { 1, 0, 1, 0, 0, 0, 0, 0, 0 }, 1));
            Assert.Equal(65536UL, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToUInt64(new byte[] { 1, 0, 0, 1, 0, 0, 0, 0, 0 }, 1));
            Assert.Equal(16777216UL, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToUInt64(new byte[] { 1, 0, 0, 0, 1, 0, 0, 0, 0 }, 1));
            Assert.Equal(4294967296UL, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToUInt64(new byte[] { 1, 0, 0, 0, 0, 1, 0, 0, 0 }, 1));
            Assert.Equal(1099511627776UL, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToUInt64(new byte[] { 1, 0, 0, 0, 0, 0, 1, 0, 0 }, 1));
            Assert.Equal(1099511627776UL * 256, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToUInt64(new byte[] { 1, 0, 0, 0, 0, 0, 0, 1, 0 }, 1));
            Assert.Equal(1099511627776UL * 256 * 256, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToUInt64(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0, 1 }, 1));
            Assert.Equal(ulong.MaxValue, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToUInt64(new byte[] { 0, 255, 255, 255, 255, 255, 255, 255, 255 }, 1));
            Assert.Equal(257UL, EndianBitConverter.GetConverter(Endianness.LittleEndian).ToUInt64(new byte[] { 0, 1, 1, 0, 0, 0, 0, 0, 0 }, 1));
        }
    }
}