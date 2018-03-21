// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;

namespace SixLabors.ImageSharp.IO
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
        public override short ToInt16(byte[] value, int startIndex)
        {
            return BinaryPrimitives.ReadInt16LittleEndian(value.AsReadOnlySpan().Slice(startIndex));
        }

        /// <inheritdoc/>
        public override int ToInt32(byte[] value, int startIndex)
        {
            return BinaryPrimitives.ReadInt32LittleEndian(value.AsReadOnlySpan().Slice(startIndex));
        }

        /// <inheritdoc/>
        public override long ToInt64(byte[] value, int startIndex)
        {
            return BinaryPrimitives.ReadInt64LittleEndian(value.AsReadOnlySpan().Slice(startIndex));
        }
    }
}