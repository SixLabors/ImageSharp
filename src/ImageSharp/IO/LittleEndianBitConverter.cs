// <copyright file="LittleEndianBitConverter.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.IO
{
    /// <summary>
    /// Implementation of EndianBitConverter which converts to/from little-endian byte arrays.
    /// </summary>
    internal sealed class LittleEndianBitConverter : EndianBitConverter
    {
        /// <inheritdoc/>
        public override Endianness Endianness
        {
            get { return Endianness.LittleEndian; }
        }

        /// <inheritdoc/>
        public override bool IsLittleEndian
        {
            get { return true; }
        }

        /// <inheritdoc/>
        public override void CopyBytes(short value, byte[] buffer, int index)
        {
            CheckByteArgument(buffer, index, 2);

            buffer[index + 1] = (byte)(value >> 8);
            buffer[index] = (byte)value;
        }

        /// <inheritdoc/>
        public override void CopyBytes(int value, byte[] buffer, int index)
        {
            CheckByteArgument(buffer, index, 4);

            buffer[index + 3] = (byte)(value >> 24);
            buffer[index + 2] = (byte)(value >> 16);
            buffer[index + 1] = (byte)(value >> 8);
            buffer[index] = (byte)value;
        }

        /// <inheritdoc/>
        public override void CopyBytes(long value, byte[] buffer, int index)
        {
            CheckByteArgument(buffer, index, 8);

            buffer[index + 7] = (byte)(value >> 56);
            buffer[index + 6] = (byte)(value >> 48);
            buffer[index + 5] = (byte)(value >> 40);
            buffer[index + 4] = (byte)(value >> 32);
            buffer[index + 3] = (byte)(value >> 24);
            buffer[index + 2] = (byte)(value >> 16);
            buffer[index + 1] = (byte)(value >> 8);
            buffer[index] = (byte)value;
        }

        /// <inheritdoc/>
        public unsafe override short ToInt16(byte[] value, int startIndex)
        {
            CheckByteArgument(value, startIndex, 2);
            return (short)((value[startIndex + 1] << 8) | value[startIndex]);
        }

        /// <inheritdoc/>
        public unsafe override int ToInt32(byte[] value, int startIndex)
        {
            CheckByteArgument(value, startIndex, 4);
            return (value[startIndex + 3] << 24) | (value[startIndex + 2] << 16) | (value[startIndex + 1] << 8) | value[startIndex];
        }

        /// <inheritdoc/>
        public unsafe override long ToInt64(byte[] value, int startIndex)
        {
            CheckByteArgument(value, startIndex, 8);
            long p1 = (value[startIndex + 7] << 24) | (value[startIndex + 6] << 16) | (value[startIndex + 5] << 8) | value[startIndex + 4];
            long p2 = (value[startIndex + 3] << 24) | (value[startIndex + 2] << 16) | (value[startIndex + 1] << 8) | value[startIndex];
            return p2 | (p1 << 32);
        }
    }
}