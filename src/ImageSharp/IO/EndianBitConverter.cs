// <copyright file="EndianBitConverter.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.IO
{
    using System;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Equivalent of <see cref="BitConverter"/>, but with either endianness.
    /// </summary>
    internal abstract partial class EndianBitConverter
    {
        /// <summary>
        /// The little-endian bit converter.
        /// </summary>
        public static readonly LittleEndianBitConverter LittleEndianConverter = new LittleEndianBitConverter();

        /// <summary>
        /// The big-endian bit converter.
        /// </summary>
        public static readonly BigEndianBitConverter BigEndianConverter = new BigEndianBitConverter();

        /// <summary>
        /// Gets the byte order ("endianness") in which data is converted using this class.
        /// </summary>
        public abstract Endianness Endianness { get; }

        /// <summary>
        /// Gets a value indicating whether the byte order ("endianness") in which data is converted is little endian.
        /// </summary>
        /// <remarks>
        /// Different computer architectures store data using different byte orders. "Big-endian"
        /// means the most significant byte is on the left end of a word. "Little-endian" means the
        /// most significant byte is on the right end of a word.
        /// </remarks>
        public abstract bool IsLittleEndian { get; }

        /// <summary>
        /// Gets the converter.
        /// </summary>
        /// <param name="endianness">The endianness.</param>
        /// <returns>an <see cref="EndianBitConverter"/></returns>
        /// <exception cref="ArgumentException">Not a valid form of Endianness - endianness</exception>
        public static EndianBitConverter GetConverter(Endianness endianness)
        {
            switch (endianness)
            {
                case Endianness.LittleEndian:
                    return LittleEndianConverter;
                case Endianness.BigEndian:
                    return BigEndianConverter;
                default:
                    throw new ArgumentException("Not a valid form of Endianness", nameof(endianness));
            }
        }

        /// <summary>
        /// Returns a String converted from the elements of a byte array.
        /// </summary>
        /// <param name="value">An array of bytes.</param>
        /// <remarks>All the elements of value are converted.</remarks>
        /// <returns>
        /// A String of hexadecimal pairs separated by hyphens, where each pair
        /// represents the corresponding element in value; for example, "7F-2C-4A".
        /// </returns>
        public static string ToString(byte[] value)
        {
            return BitConverter.ToString(value);
        }

        /// <summary>
        /// Returns a String converted from the elements of a byte array starting at a specified array position.
        /// </summary>
        /// <param name="value">An array of bytes.</param>
        /// <param name="startIndex">The starting position within value.</param>
        /// <remarks>The elements from array position startIndex to the end of the array are converted.</remarks>
        /// <returns>
        /// A String of hexadecimal pairs separated by hyphens, where each pair
        /// represents the corresponding element in value; for example, "7F-2C-4A".
        /// </returns>
        public static string ToString(byte[] value, int startIndex)
        {
            return BitConverter.ToString(value, startIndex);
        }

        /// <summary>
        /// Returns a String converted from a specified number of bytes at a specified position in a byte array.
        /// </summary>
        /// <param name="value">An array of bytes.</param>
        /// <param name="startIndex">The starting position within value.</param>
        /// <param name="length">The number of bytes to convert.</param>
        /// <remarks>The length elements from array position startIndex are converted.</remarks>
        /// <returns>
        /// A String of hexadecimal pairs separated by hyphens, where each pair
        /// represents the corresponding element in value; for example, "7F-2C-4A".
        /// </returns>
        public static string ToString(byte[] value, int startIndex, int length)
        {
            return BitConverter.ToString(value, startIndex, length);
        }

        /// <summary>
        /// Checks the given argument for validity.
        /// </summary>
        /// <param name="value">The byte array passed in</param>
        /// <param name="startIndex">The start index passed in</param>
        /// <param name="bytesRequired">The number of bytes required</param>
        /// <exception cref="ArgumentNullException">value is a null reference</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// startIndex is less than zero or greater than the length of value minus bytesRequired.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static void CheckByteArgument(byte[] value, int startIndex, int bytesRequired)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (startIndex < 0 || startIndex > value.Length - bytesRequired)
            {
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            }
        }
    }
}
