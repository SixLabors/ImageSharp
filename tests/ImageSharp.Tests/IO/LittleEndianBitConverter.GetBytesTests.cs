// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.IO;
using Xunit;

namespace SixLabors.ImageSharp.Tests.IO
{
    /// <summary>
    /// The <see cref="LittleEndianBitConverter"/> tests.
    /// </summary>
    public class LittleEndianBitConverterGetBytesTests
    {
        /// <summary>
        /// Tests that passing a <see cref="bool"/> returns the correct bytes.
        /// </summary>
        [Fact]
        public void GetBytesBoolean()
        {
            this.CheckBytes(new byte[] { 0 }, EndianBitConverter.LittleEndianConverter.GetBytes(false));
            this.CheckBytes(new byte[] { 1 }, EndianBitConverter.LittleEndianConverter.GetBytes(true));
        }

        /// <summary>
        /// Tests that passing a <see cref="short"/> returns the correct bytes.
        /// </summary>
        [Fact]
        public void GetBytesShort()
        {
            this.CheckBytes(new byte[] { 0, 0 }, EndianBitConverter.LittleEndianConverter.GetBytes((short)0));
            this.CheckBytes(new byte[] { 1, 0 }, EndianBitConverter.LittleEndianConverter.GetBytes((short)1));
            this.CheckBytes(new byte[] { 0, 1 }, EndianBitConverter.LittleEndianConverter.GetBytes((short)256));
            this.CheckBytes(new byte[] { 255, 255 }, EndianBitConverter.LittleEndianConverter.GetBytes((short)-1));
            this.CheckBytes(new byte[] { 1, 1 }, EndianBitConverter.LittleEndianConverter.GetBytes((short)257));
        }

        /// <summary>
        /// Tests that passing a <see cref="ushort"/> returns the correct bytes.
        /// </summary>
        [Fact]
        public void GetBytesUShort()
        {
            this.CheckBytes(new byte[] { 0, 0 }, EndianBitConverter.LittleEndianConverter.GetBytes((ushort)0));
            this.CheckBytes(new byte[] { 1, 0 }, EndianBitConverter.LittleEndianConverter.GetBytes((ushort)1));
            this.CheckBytes(new byte[] { 0, 1 }, EndianBitConverter.LittleEndianConverter.GetBytes((ushort)256));
            this.CheckBytes(new byte[] { 255, 255 }, EndianBitConverter.LittleEndianConverter.GetBytes(ushort.MaxValue));
            this.CheckBytes(new byte[] { 1, 1 }, EndianBitConverter.LittleEndianConverter.GetBytes((ushort)257));
        }

        /// <summary>
        /// Tests that passing a <see cref="int"/> returns the correct bytes.
        /// </summary>
        [Fact]
        public void GetBytesInt()
        {
            this.CheckBytes(new byte[] { 0, 0, 0, 0 }, EndianBitConverter.LittleEndianConverter.GetBytes(0));
            this.CheckBytes(new byte[] { 1, 0, 0, 0 }, EndianBitConverter.LittleEndianConverter.GetBytes(1));
            this.CheckBytes(new byte[] { 0, 1, 0, 0 }, EndianBitConverter.LittleEndianConverter.GetBytes(256));
            this.CheckBytes(new byte[] { 0, 0, 1, 0 }, EndianBitConverter.LittleEndianConverter.GetBytes(65536));
            this.CheckBytes(new byte[] { 0, 0, 0, 1 }, EndianBitConverter.LittleEndianConverter.GetBytes(16777216));
            this.CheckBytes(new byte[] { 255, 255, 255, 255 }, EndianBitConverter.LittleEndianConverter.GetBytes(-1));
            this.CheckBytes(new byte[] { 1, 1, 0, 0 }, EndianBitConverter.LittleEndianConverter.GetBytes(257));
        }

        /// <summary>
        /// Tests that passing a <see cref="uint"/> returns the correct bytes.
        /// </summary>
        [Fact]
        public void GetBytesUInt()
        {
            this.CheckBytes(new byte[] { 0, 0, 0, 0 }, EndianBitConverter.LittleEndianConverter.GetBytes((uint)0));
            this.CheckBytes(new byte[] { 1, 0, 0, 0 }, EndianBitConverter.LittleEndianConverter.GetBytes((uint)1));
            this.CheckBytes(new byte[] { 0, 1, 0, 0 }, EndianBitConverter.LittleEndianConverter.GetBytes((uint)256));
            this.CheckBytes(new byte[] { 0, 0, 1, 0 }, EndianBitConverter.LittleEndianConverter.GetBytes((uint)65536));
            this.CheckBytes(new byte[] { 0, 0, 0, 1 }, EndianBitConverter.LittleEndianConverter.GetBytes((uint)16777216));
            this.CheckBytes(new byte[] { 255, 255, 255, 255 }, EndianBitConverter.LittleEndianConverter.GetBytes(uint.MaxValue));
            this.CheckBytes(new byte[] { 1, 1, 0, 0 }, EndianBitConverter.LittleEndianConverter.GetBytes((uint)257));
        }

        /// <summary>
        /// Tests that passing a <see cref="long"/> returns the correct bytes.
        /// </summary>
        [Fact]
        public void GetBytesLong()
        {
            this.CheckBytes(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 }, EndianBitConverter.LittleEndianConverter.GetBytes(0L));
            this.CheckBytes(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0 }, EndianBitConverter.LittleEndianConverter.GetBytes(1L));
            this.CheckBytes(new byte[] { 0, 1, 0, 0, 0, 0, 0, 0 }, EndianBitConverter.LittleEndianConverter.GetBytes(256L));
            this.CheckBytes(new byte[] { 0, 0, 1, 0, 0, 0, 0, 0 }, EndianBitConverter.LittleEndianConverter.GetBytes(65536L));
            this.CheckBytes(new byte[] { 0, 0, 0, 1, 0, 0, 0, 0 }, EndianBitConverter.LittleEndianConverter.GetBytes(16777216L));
            this.CheckBytes(new byte[] { 0, 0, 0, 0, 1, 0, 0, 0 }, EndianBitConverter.LittleEndianConverter.GetBytes(4294967296L));
            this.CheckBytes(new byte[] { 0, 0, 0, 0, 0, 1, 0, 0 }, EndianBitConverter.LittleEndianConverter.GetBytes(1099511627776L));
            this.CheckBytes(new byte[] { 0, 0, 0, 0, 0, 0, 1, 0 }, EndianBitConverter.LittleEndianConverter.GetBytes(1099511627776L * 256));
            this.CheckBytes(new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 }, EndianBitConverter.LittleEndianConverter.GetBytes(1099511627776L * 256 * 256));
            this.CheckBytes(new byte[] { 255, 255, 255, 255, 255, 255, 255, 255 }, EndianBitConverter.LittleEndianConverter.GetBytes(-1L));
            this.CheckBytes(new byte[] { 1, 1, 0, 0, 0, 0, 0, 0 }, EndianBitConverter.LittleEndianConverter.GetBytes(257L));
        }

        /// <summary>
        /// Tests that passing a <see cref="ulong"/> returns the correct bytes.
        /// </summary>
        [Fact]
        public void GetBytesULong()
        {
            this.CheckBytes(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 }, EndianBitConverter.LittleEndianConverter.GetBytes(0UL));
            this.CheckBytes(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0 }, EndianBitConverter.LittleEndianConverter.GetBytes(1UL));
            this.CheckBytes(new byte[] { 0, 1, 0, 0, 0, 0, 0, 0 }, EndianBitConverter.LittleEndianConverter.GetBytes(256UL));
            this.CheckBytes(new byte[] { 0, 0, 1, 0, 0, 0, 0, 0 }, EndianBitConverter.LittleEndianConverter.GetBytes(65536UL));
            this.CheckBytes(new byte[] { 0, 0, 0, 1, 0, 0, 0, 0 }, EndianBitConverter.LittleEndianConverter.GetBytes(16777216UL));
            this.CheckBytes(new byte[] { 0, 0, 0, 0, 1, 0, 0, 0 }, EndianBitConverter.LittleEndianConverter.GetBytes(4294967296UL));
            this.CheckBytes(new byte[] { 0, 0, 0, 0, 0, 1, 0, 0 }, EndianBitConverter.LittleEndianConverter.GetBytes(1099511627776UL));
            this.CheckBytes(new byte[] { 0, 0, 0, 0, 0, 0, 1, 0 }, EndianBitConverter.LittleEndianConverter.GetBytes(1099511627776UL * 256));
            this.CheckBytes(new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 }, EndianBitConverter.LittleEndianConverter.GetBytes(1099511627776UL * 256 * 256));
            this.CheckBytes(new byte[] { 255, 255, 255, 255, 255, 255, 255, 255 }, EndianBitConverter.LittleEndianConverter.GetBytes(ulong.MaxValue));
            this.CheckBytes(new byte[] { 1, 1, 0, 0, 0, 0, 0, 0 }, EndianBitConverter.LittleEndianConverter.GetBytes(257UL));
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