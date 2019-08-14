// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;

namespace SixLabors.ImageSharp.Formats.Tiff
{
    internal class TiffLittleEndianStream : TiffStream
    {
        public TiffLittleEndianStream(Stream stream)
            : base(stream)
        {
        }

        public override TiffByteOrder ByteOrder => TiffByteOrder.LittleEndian;

        /// <summary>
        /// Converts buffer data into an <see cref="short"/> using the correct endianness.
        /// </summary>
        /// <returns>The converted value.</returns>
        public override short ReadInt16()
        {
            byte[] bytes = this.ReadBytes(2);
            return (short)(bytes[0] | (bytes[1] << 8));
        }

        /// <summary>
        /// Converts buffer data into an <see cref="int"/> using the correct endianness.
        /// </summary>
        /// <returns>The converted value.</returns>
        public override int ReadInt32()
        {
            byte[] bytes = this.ReadBytes(4);
            return bytes[0] | (bytes[1] << 8) | (bytes[2] << 16) | (bytes[3] << 24);
        }

        /// <summary>
        /// Converts buffer data into a <see cref="uint"/> using the correct endianness.
        /// </summary>
        /// <returns>The converted value.</returns>
        public override uint ReadUInt32()
        {
            return (uint)this.ReadInt32();
        }

        /// <summary>
        /// Converts buffer data into a <see cref="ushort"/> using the correct endianness.
        /// </summary>
        /// <returns>The converted value.</returns>
        public override ushort ReadUInt16()
        {
            return (ushort)this.ReadInt16();
        }

        /// <summary>
        /// Converts buffer data into a <see cref="float"/> using the correct endianness.
        /// </summary>
        /// <returns>The converted value.</returns>
        public override float ReadSingle()
        {
            byte[] bytes = this.ReadBytes(4);

            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            return BitConverter.ToSingle(bytes, 0);
        }

        /// <summary>
        /// Converts buffer data into a <see cref="double"/> using the correct endianness.
        /// </summary>
        /// <returns>The converted value.</returns>
        public override double ReadDouble()
        {
            byte[] bytes = this.ReadBytes(8);

            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            return BitConverter.ToDouble(bytes, 0);
        }
    }
}
