// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.IO;
using Xunit;

namespace SixLabors.ImageSharp.Tests.IO
{
    /// <summary>
    /// The <see cref="LittleEndianBitConverter"/> tests.
    /// </summary>
    public class LittleEndianBitConverterToTypeTests
    {
        [Fact]
        public void CopyToWithNullBufferThrowsException()
        {
            Assert.Throws<ArgumentNullException>(() => EndianBitConverter.LittleEndianConverter.ToBoolean(null, 0));
            Assert.Throws<ArgumentNullException>(() => EndianBitConverter.LittleEndianConverter.ToInt16(null, 0));
            Assert.Throws<ArgumentNullException>(() => EndianBitConverter.LittleEndianConverter.ToUInt16(null, 0));
            Assert.Throws<ArgumentNullException>(() => EndianBitConverter.LittleEndianConverter.ToInt32(null, 0));
            Assert.Throws<ArgumentNullException>(() => EndianBitConverter.LittleEndianConverter.ToUInt32(null, 0));
            Assert.Throws<ArgumentNullException>(() => EndianBitConverter.LittleEndianConverter.ToInt64(null, 0));
            Assert.Throws<ArgumentNullException>(() => EndianBitConverter.LittleEndianConverter.ToUInt64(null, 0));
        }

        [Fact]
        public void CopyToWithIndexTooBigThrowsException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => EndianBitConverter.LittleEndianConverter.ToBoolean(new byte[1], 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => EndianBitConverter.LittleEndianConverter.ToInt16(new byte[2], 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => EndianBitConverter.LittleEndianConverter.ToUInt16(new byte[2], 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => EndianBitConverter.LittleEndianConverter.ToInt32(new byte[4], 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => EndianBitConverter.LittleEndianConverter.ToUInt32(new byte[4], 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => EndianBitConverter.LittleEndianConverter.ToInt64(new byte[8], 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => EndianBitConverter.LittleEndianConverter.ToUInt64(new byte[8], 1));
        }

        [Fact]
        public void CopyToWithBufferTooSmallThrowsException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => EndianBitConverter.LittleEndianConverter.ToBoolean(new byte[0], 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => EndianBitConverter.LittleEndianConverter.ToInt16(new byte[1], 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => EndianBitConverter.LittleEndianConverter.ToUInt16(new byte[1], 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => EndianBitConverter.LittleEndianConverter.ToInt32(new byte[3], 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => EndianBitConverter.LittleEndianConverter.ToUInt32(new byte[3], 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => EndianBitConverter.LittleEndianConverter.ToInt64(new byte[7], 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => EndianBitConverter.LittleEndianConverter.ToUInt64(new byte[7], 0));
        }

        /// <summary>
        /// Tests that passing a <see cref="bool"/> returns the correct bytes.
        /// </summary>
        [Fact]
        public void ToBoolean()
        {
            Assert.False(EndianBitConverter.LittleEndianConverter.ToBoolean(new byte[] { 0 }, 0));
            Assert.True(EndianBitConverter.LittleEndianConverter.ToBoolean(new byte[] { 1 }, 0));

            Assert.False(EndianBitConverter.LittleEndianConverter.ToBoolean(new byte[] { 1, 0 }, 1));
            Assert.True(EndianBitConverter.LittleEndianConverter.ToBoolean(new byte[] { 0, 1 }, 1));
        }

        /// <summary>
        /// Tests that passing a <see cref="short"/> returns the correct bytes.
        /// </summary>
        [Fact]
        public void ToInt16()
        {
            Assert.Equal((short)0, EndianBitConverter.LittleEndianConverter.ToInt16(new byte[] { 0, 0 }, 0));
            Assert.Equal((short)1, EndianBitConverter.LittleEndianConverter.ToInt16(new byte[] { 1, 0 }, 0));
            Assert.Equal((short)256, EndianBitConverter.LittleEndianConverter.ToInt16(new byte[] { 0, 1 }, 0));
            Assert.Equal((short)-1, EndianBitConverter.LittleEndianConverter.ToInt16(new byte[] { 255, 255 }, 0));
            Assert.Equal((short)257, EndianBitConverter.LittleEndianConverter.ToInt16(new byte[] { 1, 1 }, 0));

            Assert.Equal((short)0, EndianBitConverter.LittleEndianConverter.ToInt16(new byte[] { 1, 0, 0 }, 1));
            Assert.Equal((short)1, EndianBitConverter.LittleEndianConverter.ToInt16(new byte[] { 0, 1, 0 }, 1));
            Assert.Equal((short)256, EndianBitConverter.LittleEndianConverter.ToInt16(new byte[] { 1, 0, 1 }, 1));
            Assert.Equal((short)-1, EndianBitConverter.LittleEndianConverter.ToInt16(new byte[] { 0, 255, 255 }, 1));
            Assert.Equal((short)257, EndianBitConverter.LittleEndianConverter.ToInt16(new byte[] { 0, 1, 1 }, 1));
        }

        /// <summary>
        /// Tests that passing a <see cref="ushort"/> returns the correct bytes.
        /// </summary>
        [Fact]
        public void ToUInt16()
        {
            Assert.Equal((ushort)0, EndianBitConverter.LittleEndianConverter.ToUInt16(new byte[] { 0, 0 }, 0));
            Assert.Equal((ushort)1, EndianBitConverter.LittleEndianConverter.ToUInt16(new byte[] { 1, 0 }, 0));
            Assert.Equal((ushort)256, EndianBitConverter.LittleEndianConverter.ToUInt16(new byte[] { 0, 1 }, 0));
            Assert.Equal(ushort.MaxValue, EndianBitConverter.LittleEndianConverter.ToUInt16(new byte[] { 255, 255 }, 0));
            Assert.Equal((ushort)257, EndianBitConverter.LittleEndianConverter.ToUInt16(new byte[] { 1, 1 }, 0));

            Assert.Equal((ushort)0, EndianBitConverter.LittleEndianConverter.ToUInt16(new byte[] { 1, 0, 0 }, 1));
            Assert.Equal((ushort)1, EndianBitConverter.LittleEndianConverter.ToUInt16(new byte[] { 0, 1, 0 }, 1));
            Assert.Equal((ushort)256, EndianBitConverter.LittleEndianConverter.ToUInt16(new byte[] { 1, 0, 1 }, 1));
            Assert.Equal(ushort.MaxValue, EndianBitConverter.LittleEndianConverter.ToUInt16(new byte[] { 0, 255, 255 }, 1));
            Assert.Equal((ushort)257, EndianBitConverter.LittleEndianConverter.ToUInt16(new byte[] { 0, 1, 1 }, 1));
        }

        /// <summary>
        /// Tests that passing a <see cref="int"/> returns the correct bytes.
        /// </summary>
        [Fact]
        public void ToInt32()
        {
            Assert.Equal(0, EndianBitConverter.LittleEndianConverter.ToInt32(new byte[] { 0, 0, 0, 0 }, 0));
            Assert.Equal(1, EndianBitConverter.LittleEndianConverter.ToInt32(new byte[] { 1, 0, 0, 0 }, 0));
            Assert.Equal(256, EndianBitConverter.LittleEndianConverter.ToInt32(new byte[] { 0, 1, 0, 0 }, 0));
            Assert.Equal(65536, EndianBitConverter.LittleEndianConverter.ToInt32(new byte[] { 0, 0, 1, 0 }, 0));
            Assert.Equal(16777216, EndianBitConverter.LittleEndianConverter.ToInt32(new byte[] { 0, 0, 0, 1 }, 0));
            Assert.Equal(-1, EndianBitConverter.LittleEndianConverter.ToInt32(new byte[] { 255, 255, 255, 255 }, 0));
            Assert.Equal(257, EndianBitConverter.LittleEndianConverter.ToInt32(new byte[] { 1, 1, 0, 0 }, 0));

            Assert.Equal(0, EndianBitConverter.LittleEndianConverter.ToInt32(new byte[] { 1, 0, 0, 0, 0 }, 1));
            Assert.Equal(1, EndianBitConverter.LittleEndianConverter.ToInt32(new byte[] { 0, 1, 0, 0, 0 }, 1));
            Assert.Equal(256, EndianBitConverter.LittleEndianConverter.ToInt32(new byte[] { 1, 0, 1, 0, 0 }, 1));
            Assert.Equal(65536, EndianBitConverter.LittleEndianConverter.ToInt32(new byte[] { 1, 0, 0, 1, 0 }, 1));
            Assert.Equal(16777216, EndianBitConverter.LittleEndianConverter.ToInt32(new byte[] { 1, 0, 0, 0, 1 }, 1));
            Assert.Equal(-1, EndianBitConverter.LittleEndianConverter.ToInt32(new byte[] { 0, 255, 255, 255, 255 }, 1));
            Assert.Equal(257, EndianBitConverter.LittleEndianConverter.ToInt32(new byte[] { 0, 1, 1, 0, 0 }, 1));
        }

        /// <summary>
        /// Tests that passing a <see cref="uint"/> returns the correct bytes.
        /// </summary>
        [Fact]
        public void ToUInt32()
        {
            Assert.Equal((uint)0, EndianBitConverter.LittleEndianConverter.ToUInt32(new byte[] { 0, 0, 0, 0 }, 0));
            Assert.Equal((uint)1, EndianBitConverter.LittleEndianConverter.ToUInt32(new byte[] { 1, 0, 0, 0 }, 0));
            Assert.Equal((uint)256, EndianBitConverter.LittleEndianConverter.ToUInt32(new byte[] { 0, 1, 0, 0 }, 0));
            Assert.Equal((uint)65536, EndianBitConverter.LittleEndianConverter.ToUInt32(new byte[] { 0, 0, 1, 0 }, 0));
            Assert.Equal((uint)16777216, EndianBitConverter.LittleEndianConverter.ToUInt32(new byte[] { 0, 0, 0, 1 }, 0));
            Assert.Equal(uint.MaxValue, EndianBitConverter.LittleEndianConverter.ToUInt32(new byte[] { 255, 255, 255, 255 }, 0));
            Assert.Equal((uint)257, EndianBitConverter.LittleEndianConverter.ToUInt32(new byte[] { 1, 1, 0, 0 }, 0));

            Assert.Equal((uint)0, EndianBitConverter.LittleEndianConverter.ToUInt32(new byte[] { 1, 0, 0, 0, 0 }, 1));
            Assert.Equal((uint)1, EndianBitConverter.LittleEndianConverter.ToUInt32(new byte[] { 0, 1, 0, 0, 0 }, 1));
            Assert.Equal((uint)256, EndianBitConverter.LittleEndianConverter.ToUInt32(new byte[] { 1, 0, 1, 0, 0 }, 1));
            Assert.Equal((uint)65536, EndianBitConverter.LittleEndianConverter.ToUInt32(new byte[] { 1, 0, 0, 1, 0 }, 1));
            Assert.Equal((uint)16777216, EndianBitConverter.LittleEndianConverter.ToUInt32(new byte[] { 1, 0, 0, 0, 1 }, 1));
            Assert.Equal(uint.MaxValue, EndianBitConverter.LittleEndianConverter.ToUInt32(new byte[] { 0, 255, 255, 255, 255 }, 1));
            Assert.Equal((uint)257, EndianBitConverter.LittleEndianConverter.ToUInt32(new byte[] { 0, 1, 1, 0, 0 }, 1));
        }

        /// <summary>
        /// Tests that passing a <see cref="long"/> returns the correct bytes.
        /// </summary>
        [Fact]
        public void ToInt64()
        {
            Assert.Equal(0L, EndianBitConverter.LittleEndianConverter.ToInt64(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 }, 0));
            Assert.Equal(1L, EndianBitConverter.LittleEndianConverter.ToInt64(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0 }, 0));
            Assert.Equal(256L, EndianBitConverter.LittleEndianConverter.ToInt64(new byte[] { 0, 1, 0, 0, 0, 0, 0, 0 }, 0));
            Assert.Equal(65536L, EndianBitConverter.LittleEndianConverter.ToInt64(new byte[] { 0, 0, 1, 0, 0, 0, 0, 0 }, 0));
            Assert.Equal(16777216L, EndianBitConverter.LittleEndianConverter.ToInt64(new byte[] { 0, 0, 0, 1, 0, 0, 0, 0 }, 0));
            Assert.Equal(4294967296L, EndianBitConverter.LittleEndianConverter.ToInt64(new byte[] { 0, 0, 0, 0, 1, 0, 0, 0 }, 0));
            Assert.Equal(1099511627776L, EndianBitConverter.LittleEndianConverter.ToInt64(new byte[] { 0, 0, 0, 0, 0, 1, 0, 0 }, 0));
            Assert.Equal(1099511627776L * 256, EndianBitConverter.LittleEndianConverter.ToInt64(new byte[] { 0, 0, 0, 0, 0, 0, 1, 0 }, 0));
            Assert.Equal(1099511627776L * 256 * 256, EndianBitConverter.LittleEndianConverter.ToInt64(new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 }, 0));
            Assert.Equal(-1L, EndianBitConverter.LittleEndianConverter.ToInt64(new byte[] { 255, 255, 255, 255, 255, 255, 255, 255 }, 0));
            Assert.Equal(257L, EndianBitConverter.LittleEndianConverter.ToInt64(new byte[] { 1, 1, 0, 0, 0, 0, 0, 0 }, 0));

            Assert.Equal(0L, EndianBitConverter.LittleEndianConverter.ToInt64(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0, 0 }, 1));
            Assert.Equal(1L, EndianBitConverter.LittleEndianConverter.ToInt64(new byte[] { 0, 1, 0, 0, 0, 0, 0, 0, 0 }, 1));
            Assert.Equal(256L, EndianBitConverter.LittleEndianConverter.ToInt64(new byte[] { 1, 0, 1, 0, 0, 0, 0, 0, 0 }, 1));
            Assert.Equal(65536L, EndianBitConverter.LittleEndianConverter.ToInt64(new byte[] { 1, 0, 0, 1, 0, 0, 0, 0, 0 }, 1));
            Assert.Equal(16777216L, EndianBitConverter.LittleEndianConverter.ToInt64(new byte[] { 1, 0, 0, 0, 1, 0, 0, 0, 0 }, 1));
            Assert.Equal(4294967296L, EndianBitConverter.LittleEndianConverter.ToInt64(new byte[] { 1, 0, 0, 0, 0, 1, 0, 0, 0 }, 1));
            Assert.Equal(1099511627776L, EndianBitConverter.LittleEndianConverter.ToInt64(new byte[] { 1, 0, 0, 0, 0, 0, 1, 0, 0 }, 1));
            Assert.Equal(1099511627776L * 256, EndianBitConverter.LittleEndianConverter.ToInt64(new byte[] { 1, 0, 0, 0, 0, 0, 0, 1, 0 }, 1));
            Assert.Equal(1099511627776L * 256 * 256, EndianBitConverter.LittleEndianConverter.ToInt64(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0, 1 }, 1));
            Assert.Equal(-1L, EndianBitConverter.LittleEndianConverter.ToInt64(new byte[] { 0, 255, 255, 255, 255, 255, 255, 255, 255 }, 1));
            Assert.Equal(257L, EndianBitConverter.LittleEndianConverter.ToInt64(new byte[] { 0, 1, 1, 0, 0, 0, 0, 0, 0 }, 1));
        }

        /// <summary>
        /// Tests that passing a <see cref="ulong"/> returns the correct bytes.
        /// </summary>
        [Fact]
        public void ToUInt64()
        {
            Assert.Equal(0UL, EndianBitConverter.LittleEndianConverter.ToUInt64(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 }, 0));
            Assert.Equal(1UL, EndianBitConverter.LittleEndianConverter.ToUInt64(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0 }, 0));
            Assert.Equal(256UL, EndianBitConverter.LittleEndianConverter.ToUInt64(new byte[] { 0, 1, 0, 0, 0, 0, 0, 0 }, 0));
            Assert.Equal(65536UL, EndianBitConverter.LittleEndianConverter.ToUInt64(new byte[] { 0, 0, 1, 0, 0, 0, 0, 0 }, 0));
            Assert.Equal(16777216UL, EndianBitConverter.LittleEndianConverter.ToUInt64(new byte[] { 0, 0, 0, 1, 0, 0, 0, 0 }, 0));
            Assert.Equal(4294967296UL, EndianBitConverter.LittleEndianConverter.ToUInt64(new byte[] { 0, 0, 0, 0, 1, 0, 0, 0 }, 0));
            Assert.Equal(1099511627776UL, EndianBitConverter.LittleEndianConverter.ToUInt64(new byte[] { 0, 0, 0, 0, 0, 1, 0, 0 }, 0));
            Assert.Equal(1099511627776UL * 256, EndianBitConverter.LittleEndianConverter.ToUInt64(new byte[] { 0, 0, 0, 0, 0, 0, 1, 0 }, 0));
            Assert.Equal(1099511627776UL * 256 * 256, EndianBitConverter.LittleEndianConverter.ToUInt64(new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 }, 0));
            Assert.Equal(ulong.MaxValue, EndianBitConverter.LittleEndianConverter.ToUInt64(new byte[] { 255, 255, 255, 255, 255, 255, 255, 255 }, 0));
            Assert.Equal(257UL, EndianBitConverter.LittleEndianConverter.ToUInt64(new byte[] { 1, 1, 0, 0, 0, 0, 0, 0 }, 0));

            Assert.Equal(0UL, EndianBitConverter.LittleEndianConverter.ToUInt64(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0, 0 }, 1));
            Assert.Equal(1UL, EndianBitConverter.LittleEndianConverter.ToUInt64(new byte[] { 0, 1, 0, 0, 0, 0, 0, 0, 0 }, 1));
            Assert.Equal(256UL, EndianBitConverter.LittleEndianConverter.ToUInt64(new byte[] { 1, 0, 1, 0, 0, 0, 0, 0, 0 }, 1));
            Assert.Equal(65536UL, EndianBitConverter.LittleEndianConverter.ToUInt64(new byte[] { 1, 0, 0, 1, 0, 0, 0, 0, 0 }, 1));
            Assert.Equal(16777216UL, EndianBitConverter.LittleEndianConverter.ToUInt64(new byte[] { 1, 0, 0, 0, 1, 0, 0, 0, 0 }, 1));
            Assert.Equal(4294967296UL, EndianBitConverter.LittleEndianConverter.ToUInt64(new byte[] { 1, 0, 0, 0, 0, 1, 0, 0, 0 }, 1));
            Assert.Equal(1099511627776UL, EndianBitConverter.LittleEndianConverter.ToUInt64(new byte[] { 1, 0, 0, 0, 0, 0, 1, 0, 0 }, 1));
            Assert.Equal(1099511627776UL * 256, EndianBitConverter.LittleEndianConverter.ToUInt64(new byte[] { 1, 0, 0, 0, 0, 0, 0, 1, 0 }, 1));
            Assert.Equal(1099511627776UL * 256 * 256, EndianBitConverter.LittleEndianConverter.ToUInt64(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0, 1 }, 1));
            Assert.Equal(ulong.MaxValue, EndianBitConverter.LittleEndianConverter.ToUInt64(new byte[] { 0, 255, 255, 255, 255, 255, 255, 255, 255 }, 1));
            Assert.Equal(257UL, EndianBitConverter.LittleEndianConverter.ToUInt64(new byte[] { 0, 1, 1, 0, 0, 0, 0, 0, 0 }, 1));
        }
    }
}