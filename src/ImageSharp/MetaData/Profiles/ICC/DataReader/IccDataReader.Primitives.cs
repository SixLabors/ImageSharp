// <copyright file="IccDataReader.Primitives.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Text;

    /// <summary>
    /// Provides methods to read ICC data types
    /// </summary>
    internal sealed partial class IccDataReader
    {
        /// <summary>
        /// Reads an ushort
        /// </summary>
        /// <returns>the value</returns>
        public ushort ReadUInt16()
        {
            return this.converter.ToUInt16(this.data, this.AddIndex(2));
        }

        /// <summary>
        /// Reads a short
        /// </summary>
        /// <returns>the value</returns>
        public short ReadInt16()
        {
            return this.converter.ToInt16(this.data, this.AddIndex(2));
        }

        /// <summary>
        /// Reads an uint
        /// </summary>
        /// <returns>the value</returns>
        public uint ReadUInt32()
        {
            return this.converter.ToUInt32(this.data, this.AddIndex(4));
        }

        /// <summary>
        /// Reads an int
        /// </summary>
        /// <returns>the value</returns>
        public int ReadInt32()
        {
            return this.converter.ToInt32(this.data, this.AddIndex(4));
        }

        /// <summary>
        /// Reads an ulong
        /// </summary>
        /// <returns>the value</returns>
        public ulong ReadUInt64()
        {
            return this.converter.ToUInt64(this.data, this.AddIndex(8));
        }

        /// <summary>
        /// Reads a long
        /// </summary>
        /// <returns>the value</returns>
        public long ReadInt64()
        {
            return this.converter.ToInt64(this.data, this.AddIndex(8));
        }

        /// <summary>
        /// Reads a float
        /// </summary>
        /// <returns>the value</returns>
        public float ReadSingle()
        {
            return this.converter.ToSingle(this.data, this.AddIndex(4));
        }

        /// <summary>
        /// Reads a double
        /// </summary>
        /// <returns>the value</returns>
        public double ReadDouble()
        {
            return this.converter.ToDouble(this.data, this.AddIndex(8));
        }

        /// <summary>
        /// Reads an ASCII encoded string
        /// </summary>
        /// <param name="length">number of bytes to read</param>
        /// <returns>The value as a string</returns>
        public string ReadAsciiString(int length)
        {
            if (length == 0)
            {
                return string.Empty;
            }

            Guard.MustBeGreaterThan(length, 0, nameof(length));
            string value = AsciiEncoding.GetString(this.data, this.AddIndex(length), length);

            // remove data after (potential) null terminator
            int pos = value.IndexOf('\0');
            if (pos >= 0)
            {
                value = value.Substring(0, pos);
            }

            return value;
        }

        /// <summary>
        /// Reads an UTF-16 big-endian encoded string
        /// </summary>
        /// <param name="length">number of bytes to read</param>
        /// <returns>The value as a string</returns>
        public string ReadUnicodeString(int length)
        {
            if (length == 0)
            {
                return string.Empty;
            }

            Guard.MustBeGreaterThan(length, 0, nameof(length));

            return Encoding.BigEndianUnicode.GetString(this.data, this.AddIndex(length), length);
        }

        /// <summary>
        /// Reads a signed 32bit number with 1 sign bit, 15 value bits and 16 fractional bits
        /// </summary>
        /// <returns>The number as double</returns>
        public float ReadFix16()
        {
            return this.ReadInt32() / 65536f;
        }

        /// <summary>
        /// Reads an unsigned 32bit number with 16 value bits and 16 fractional bits
        /// </summary>
        /// <returns>The number as double</returns>
        public float ReadUFix16()
        {
            return this.ReadUInt32() / 65536f;
        }

        /// <summary>
        /// Reads an unsigned 16bit number with 1 value bit and 15 fractional bits
        /// </summary>
        /// <returns>The number as double</returns>
        public float ReadU1Fix15()
        {
            return this.ReadUInt16() / 32768f;
        }

        /// <summary>
        /// Reads an unsigned 16bit number with 8 value bits and 8 fractional bits
        /// </summary>
        /// <returns>The number as double</returns>
        public float ReadUFix8()
        {
            return this.ReadUInt16() / 256f;
        }

        /// <summary>
        /// Reads a number of bytes and advances the index
        /// </summary>
        /// <param name="count">The number of bytes to read</param>
        /// <returns>The read bytes</returns>
        public byte[] ReadBytes(int count)
        {
            byte[] bytes = new byte[count];
            Buffer.BlockCopy(this.data, this.AddIndex(count), bytes, 0, count);
            return bytes;
        }
    }
}
