// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.IO
{
    /// <summary>
    /// Equivalent of <see cref="BitConverter"/>, but with either endianness.
    /// </summary>
    internal abstract partial class EndianBitConverter
    {
        /// <summary>
        /// Converts the specified double-precision floating point number to a
        /// 64-bit signed integer. Note: the endianness of this converter does not
        /// affect the returned value.
        /// </summary>
        /// <param name="value">The number to convert. </param>
        /// <returns>A 64-bit signed integer whose value is equivalent to value.</returns>
        public unsafe long DoubleToInt64Bits(double value)
        {
            return *((long*)&value);
        }

        /// <summary>
        /// Converts the specified 64-bit signed integer to a double-precision
        /// floating point number. Note: the endianness of this converter does not
        /// affect the returned value.
        /// </summary>
        /// <param name="value">The number to convert. </param>
        /// <returns>A double-precision floating point number whose value is equivalent to value.</returns>
        public unsafe double Int64BitsToDouble(long value)
        {
            return *((double*)&value);
        }

        /// <summary>
        /// Converts the specified single-precision floating point number to a
        /// 32-bit signed integer. Note: the endianness of this converter does not
        /// affect the returned value.
        /// </summary>
        /// <param name="value">The number to convert. </param>
        /// <returns>A 32-bit signed integer whose value is equivalent to value.</returns>
        public unsafe int SingleToInt32Bits(float value)
        {
            return *((int*)&value);
        }

        /// <summary>
        /// Converts the specified 32-bit signed integer to a single-precision floating point
        /// number. Note: the endianness of this converter does not
        /// affect the returned value.
        /// </summary>
        /// <param name="value">The number to convert. </param>
        /// <returns>A single-precision floating point number whose value is equivalent to value.</returns>
        public unsafe float Int32BitsToSingle(int value)
        {
            return *((float*)&value);
        }
    }
}
