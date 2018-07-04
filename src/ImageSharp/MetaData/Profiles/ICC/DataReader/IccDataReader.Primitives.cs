// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text;

namespace SixLabors.ImageSharp.MetaData.Profiles.Icc
{
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
            return BinaryPrimitives.ReadUInt16BigEndian(this.data.AsSpan(this.AddIndex(2), 2));
        }

        /// <summary>
        /// Reads a short
        /// </summary>
        /// <returns>the value</returns>
        public short ReadInt16()
        {
            return BinaryPrimitives.ReadInt16BigEndian(this.data.AsSpan(this.AddIndex(2), 2));
        }

        /// <summary>
        /// Reads an uint
        /// </summary>
        /// <returns>the value</returns>
        public uint ReadUInt32()
        {
            return BinaryPrimitives.ReadUInt32BigEndian(this.data.AsSpan(this.AddIndex(4), 4));
        }

        /// <summary>
        /// Reads an int
        /// </summary>
        /// <returns>the value</returns>
        public int ReadInt32()
        {
            return BinaryPrimitives.ReadInt32BigEndian(this.data.AsSpan(this.AddIndex(4), 4));
        }

        /// <summary>
        /// Reads an ulong
        /// </summary>
        /// <returns>the value</returns>
        public ulong ReadUInt64()
        {
            return BinaryPrimitives.ReadUInt64BigEndian(this.data.AsSpan(this.AddIndex(8), 8));
        }

        /// <summary>
        /// Reads a long
        /// </summary>
        /// <returns>the value</returns>
        public long ReadInt64()
        {
            return BinaryPrimitives.ReadInt64BigEndian(this.data.AsSpan(this.AddIndex(8), 8));
        }

        /// <summary>
        /// Reads a float.
        /// </summary>
        /// <returns>the value</returns>
        public float ReadSingle()
        {
            int intValue = this.ReadInt32();

            return Unsafe.As<int, float>(ref intValue);
        }

        /// <summary>
        /// Reads a double
        /// </summary>
        /// <returns>the value</returns>
        public double ReadDouble()
        {
            long intValue = this.ReadInt64();

            return Unsafe.As<long, double>(ref intValue);
        }

        /// <summary>
        /// Reads an ASCII encoded string.
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
        /// Reads an UTF-16 big-endian encoded string.
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
        /// Reads a signed 32bit number with 1 sign bit, 15 value bits and 16 fractional bits.
        /// </summary>
        /// <returns>The number as double</returns>
        public float ReadFix16() => this.ReadInt32() / 65536f;

        /// <summary>
        /// Reads an unsigned 32bit number with 16 value bits and 16 fractional bits.
        /// </summary>
        /// <returns>The number as double</returns>
        public float ReadUFix16() => this.ReadUInt32() / 65536f;

        /// <summary>
        /// Reads an unsigned 16bit number with 1 value bit and 15 fractional bits.
        /// </summary>
        /// <returns>The number as double</returns>
        public float ReadU1Fix15() => this.ReadUInt16() / 32768f;

        /// <summary>
        /// Reads an unsigned 16bit number with 8 value bits and 8 fractional bits.
        /// </summary>
        /// <returns>The number as double</returns>
        public float ReadUFix8()
        {
            return this.ReadUInt16() / 256f;
        }

        /// <summary>
        /// Reads a number of bytes and advances the index.
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
