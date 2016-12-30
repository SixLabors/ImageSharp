// <copyright file="BigEndianBitConverterTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.IO
{
    using ImageSharp.IO;

    using Xunit;

    /// <summary>
    /// The <see cref="BigEndianBitConverter"/> tests.
    /// </summary>
    public class BigEndianBitConverterTests
    {
        /// <summary>
        /// Tests that passing a <see cref="short"/> returns the correct bytes.
        /// </summary>
        [Fact]
        public void GetBytesShort()
        {
            this.CheckBytes(new byte[] { 0, 0 }, EndianBitConverter.GetConverter(Endianness.BigEndian).GetBytes((short)0));
            this.CheckBytes(new byte[] { 0, 1 }, EndianBitConverter.GetConverter(Endianness.BigEndian).GetBytes((short)1));
            this.CheckBytes(new byte[] { 1, 0 }, EndianBitConverter.GetConverter(Endianness.BigEndian).GetBytes((short)256));
            this.CheckBytes(new byte[] { 255, 255 }, EndianBitConverter.GetConverter(Endianness.BigEndian).GetBytes((short)-1));
            this.CheckBytes(new byte[] { 1, 1 }, EndianBitConverter.GetConverter(Endianness.BigEndian).GetBytes((short)257));
        }

        /// <summary>
        /// Tests that passing a <see cref="int"/> returns the correct bytes.
        /// </summary>
        [Fact]
        public void GetBytesInt()
        {
            this.CheckBytes(new byte[] { 0, 0, 0, 0 }, EndianBitConverter.GetConverter(Endianness.BigEndian).GetBytes((int)0));
            this.CheckBytes(new byte[] { 0, 0, 0, 1 }, EndianBitConverter.GetConverter(Endianness.BigEndian).GetBytes((int)1));
            this.CheckBytes(new byte[] { 0, 0, 1, 0 }, EndianBitConverter.GetConverter(Endianness.BigEndian).GetBytes((int)256));
            this.CheckBytes(new byte[] { 0, 1, 0, 0 }, EndianBitConverter.GetConverter(Endianness.BigEndian).GetBytes((int)65536));
            this.CheckBytes(new byte[] { 1, 0, 0, 0 }, EndianBitConverter.GetConverter(Endianness.BigEndian).GetBytes((int)16777216));
            this.CheckBytes(new byte[] { 255, 255, 255, 255 }, EndianBitConverter.GetConverter(Endianness.BigEndian).GetBytes((int)-1));
            this.CheckBytes(new byte[] { 0, 0, 1, 1 }, EndianBitConverter.GetConverter(Endianness.BigEndian).GetBytes((int)257));
        }

        /// <summary>
        /// Tests that passing a <see cref="uint"/> returns the correct bytes.
        /// </summary>
        [Fact]
        public void GetBytesUInt()
        {
            this.CheckBytes(new byte[] { 0, 0, 0, 0 }, EndianBitConverter.GetConverter(Endianness.BigEndian).GetBytes((uint)0));
            this.CheckBytes(new byte[] { 0, 0, 0, 1 }, EndianBitConverter.GetConverter(Endianness.BigEndian).GetBytes((uint)1));
            this.CheckBytes(new byte[] { 0, 0, 1, 0 }, EndianBitConverter.GetConverter(Endianness.BigEndian).GetBytes((uint)256));
            this.CheckBytes(new byte[] { 0, 1, 0, 0 }, EndianBitConverter.GetConverter(Endianness.BigEndian).GetBytes((uint)65536));
            this.CheckBytes(new byte[] { 1, 0, 0, 0 }, EndianBitConverter.GetConverter(Endianness.BigEndian).GetBytes((uint)16777216));
            this.CheckBytes(new byte[] { 255, 255, 255, 255 }, EndianBitConverter.GetConverter(Endianness.BigEndian).GetBytes((uint)uint.MaxValue));
            this.CheckBytes(new byte[] { 0, 0, 1, 1 }, EndianBitConverter.GetConverter(Endianness.BigEndian).GetBytes((uint)257));
        }

        /// <summary>
        /// Tests that passing a <see cref="long"/> returns the correct bytes.
        /// </summary>
        [Fact]
        public void GetBytesLong()
        {
            this.CheckBytes(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 }, EndianBitConverter.GetConverter(Endianness.BigEndian).GetBytes(0L));
            this.CheckBytes(new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 }, EndianBitConverter.GetConverter(Endianness.BigEndian).GetBytes(1L));
            this.CheckBytes(new byte[] { 0, 0, 0, 0, 0, 0, 1, 0 }, EndianBitConverter.GetConverter(Endianness.BigEndian).GetBytes(256L));
            this.CheckBytes(new byte[] { 0, 0, 0, 0, 0, 1, 0, 0 }, EndianBitConverter.GetConverter(Endianness.BigEndian).GetBytes(65536L));
            this.CheckBytes(new byte[] { 0, 0, 0, 0, 1, 0, 0, 0 }, EndianBitConverter.GetConverter(Endianness.BigEndian).GetBytes(16777216L));
            this.CheckBytes(new byte[] { 0, 0, 0, 1, 0, 0, 0, 0 }, EndianBitConverter.GetConverter(Endianness.BigEndian).GetBytes(4294967296L));
            this.CheckBytes(new byte[] { 0, 0, 1, 0, 0, 0, 0, 0 }, EndianBitConverter.GetConverter(Endianness.BigEndian).GetBytes(1099511627776L));
            this.CheckBytes(new byte[] { 0, 1, 0, 0, 0, 0, 0, 0 }, EndianBitConverter.GetConverter(Endianness.BigEndian).GetBytes(1099511627776L * 256));
            this.CheckBytes(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0 }, EndianBitConverter.GetConverter(Endianness.BigEndian).GetBytes(1099511627776L * 256 * 256));
            this.CheckBytes(new byte[] { 255, 255, 255, 255, 255, 255, 255, 255 }, EndianBitConverter.GetConverter(Endianness.BigEndian).GetBytes(-1L));
            this.CheckBytes(new byte[] { 0, 0, 0, 0, 0, 0, 1, 1 }, EndianBitConverter.GetConverter(Endianness.BigEndian).GetBytes(257L));
        }

        /// <summary>
        /// Tests that passing a <see cref="ulong"/> returns the correct bytes.
        /// </summary>
        [Fact]
        public void GetBytesULong()
        {
            this.CheckBytes(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 }, EndianBitConverter.GetConverter(Endianness.BigEndian).GetBytes(0UL));
            this.CheckBytes(new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 }, EndianBitConverter.GetConverter(Endianness.BigEndian).GetBytes(1UL));
            this.CheckBytes(new byte[] { 0, 0, 0, 0, 0, 0, 1, 0 }, EndianBitConverter.GetConverter(Endianness.BigEndian).GetBytes(256UL));
            this.CheckBytes(new byte[] { 0, 0, 0, 0, 0, 1, 0, 0 }, EndianBitConverter.GetConverter(Endianness.BigEndian).GetBytes(65536UL));
            this.CheckBytes(new byte[] { 0, 0, 0, 0, 1, 0, 0, 0 }, EndianBitConverter.GetConverter(Endianness.BigEndian).GetBytes(16777216UL));
            this.CheckBytes(new byte[] { 0, 0, 0, 1, 0, 0, 0, 0 }, EndianBitConverter.GetConverter(Endianness.BigEndian).GetBytes(4294967296UL));
            this.CheckBytes(new byte[] { 0, 0, 1, 0, 0, 0, 0, 0 }, EndianBitConverter.GetConverter(Endianness.BigEndian).GetBytes(1099511627776UL));
            this.CheckBytes(new byte[] { 0, 1, 0, 0, 0, 0, 0, 0 }, EndianBitConverter.GetConverter(Endianness.BigEndian).GetBytes(1099511627776UL * 256));
            this.CheckBytes(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0 }, EndianBitConverter.GetConverter(Endianness.BigEndian).GetBytes(1099511627776UL * 256 * 256));
            this.CheckBytes(new byte[] { 255, 255, 255, 255, 255, 255, 255, 255 }, EndianBitConverter.GetConverter(Endianness.BigEndian).GetBytes(ulong.MaxValue));
            this.CheckBytes(new byte[] { 0, 0, 0, 0, 0, 0, 1, 1 }, EndianBitConverter.GetConverter(Endianness.BigEndian).GetBytes(257UL));
        }

        /// <summary>
        /// Tests the two byte arrays for equality.
        /// </summary>
        /// <param name="expected">The expected bytes.</param>
        /// <param name="actual">The actual bytes.</param>
        private void CheckBytes(byte[] expected, byte[] actual)
        {
            Assert.Equal(expected.Length, actual.Length);
            Assert.Equal(expected, actual);
        }
    }
}