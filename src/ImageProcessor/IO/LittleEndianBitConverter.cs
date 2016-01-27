// <copyright file="LittleEndianBitConverter.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.IO
{
    /// <summary>
    /// Implementation of EndianBitConverter which converts to/from little-endian
    /// byte arrays.
    /// <remarks>
    /// Adapted from Miscellaneous Utility Library <see href="http://jonskeet.uk/csharp/miscutil/"/>
    /// This product includes software developed by Jon Skeet and Marc Gravell. Contact <see href="mailto:skeet@pobox.com"/>, or see 
    /// <see href="http://www.pobox.com/~skeet/"/>.
    /// </remarks>
    /// </summary>
    internal sealed class LittleEndianBitConverter : EndianBitConverter
    {
        /// <inheritdoc/>
        public override Endianness Endianness => Endianness.LittleEndian;

        /// <inheritdoc/>
        public override bool IsLittleEndian() => true;

        /// <inheritdoc/>
        protected internal override void CopyBytesImpl(long value, int bytes, byte[] buffer, int index)
        {
            for (int i = 0; i < bytes; i++)
            {
                buffer[i + index] = unchecked((byte)(value & 0xff));
                value = value >> 8;
            }
        }

        /// <inheritdoc/>
        protected internal override long FromBytes(byte[] buffer, int startIndex, int bytesToConvert)
        {
            long ret = 0;
            for (int i = 0; i < bytesToConvert; i++)
            {
                ret = unchecked((ret << 8) | buffer[startIndex + bytesToConvert - 1 - i]);
            }

            return ret;
        }
    }
}