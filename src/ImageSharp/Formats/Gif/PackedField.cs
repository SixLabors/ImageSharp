// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Gif
{
    /// <summary>
    /// Represents a byte of data in a GIF data stream which contains a number
    /// of data items.
    /// </summary>
    internal readonly struct PackedField : IEquatable<PackedField>
    {
        /// <summary>
        /// The individual bits representing the packed byte.
        /// </summary>
        private static readonly bool[] Bits = new bool[8];

        /// <summary>
        /// Gets the byte which represents the data items held in this instance.
        /// </summary>
        public byte Byte
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                int returnValue = 0;
                int bitShift = 7;
                foreach (bool bit in Bits)
                {
                    int bitValue;
                    if (bit)
                    {
                        bitValue = 1 << bitShift;
                    }
                    else
                    {
                        bitValue = 0;
                    }

                    returnValue |= bitValue;
                    bitShift--;
                }

                return Convert.ToByte(returnValue & 0xFF);
            }
        }

        /// <summary>
        /// Returns a new <see cref="PackedField"/>  with the bits in the packed fields to
        /// the corresponding bits from the supplied byte.
        /// </summary>
        /// <param name="value">The value to pack.</param>
        /// <returns>The <see cref="PackedField"/></returns>
        public static PackedField FromInt(byte value)
        {
            PackedField packed = default;
            packed.SetBits(0, 8, value);
            return packed;
        }

        /// <summary>
        /// Sets the specified bit within the packed fields to the supplied
        /// value.
        /// </summary>
        /// <param name="index">
        /// The zero-based index within the packed fields of the bit to set.
        /// </param>
        /// <param name="valueToSet">
        /// The value to set the bit to.
        /// </param>
        public void SetBit(int index, bool valueToSet)
        {
            DebugGuard.MustBeBetweenOrEqualTo(index, 0, 7, nameof(index));
            Bits[index] = valueToSet;
        }

        /// <summary>
        /// Sets the specified bits within the packed fields to the supplied
        /// value.
        /// </summary>
        /// <param name="startIndex">The zero-based index within the packed fields of the first bit to  set.</param>
        /// <param name="length">The number of bits to set.</param>
        /// <param name="valueToSet">The value to set the bits to.</param>
        public void SetBits(int startIndex, int length, int valueToSet)
        {
            DebugGuard.MustBeBetweenOrEqualTo(startIndex, 0, 7, nameof(startIndex));
            DebugCheckLength(startIndex, length);

            int bitShift = length - 1;
            for (int i = startIndex; i < startIndex + length; i++)
            {
                int bitValueIfSet = 1 << bitShift;
                int bitValue = valueToSet & bitValueIfSet;
                int bitIsSet = bitValue >> bitShift;
                Bits[i] = bitIsSet == 1;
                bitShift--;
            }
        }

        /// <summary>
        /// Gets the value of the specified bit within the byte.
        /// </summary>
        /// <param name="index">The zero-based index of the bit to get.</param>
        /// <returns>
        /// The value of the specified bit within the byte.
        /// </returns>
        public bool GetBit(int index)
        {
            DebugGuard.MustBeBetweenOrEqualTo(index, 0, 7, nameof(index));
            return Bits[index];
        }

        /// <summary>
        /// Gets the value of the specified bits within the byte.
        /// </summary>
        /// <param name="startIndex">The zero-based index of the first bit to get.</param>
        /// <param name="length">The number of bits to get.</param>
        /// <returns>
        /// The value of the specified bits within the byte.
        /// </returns>
        public int GetBits(int startIndex, int length)
        {
            DebugGuard.MustBeBetweenOrEqualTo(startIndex, 1, 8, nameof(startIndex));
            DebugCheckLength(startIndex, length);

            int returnValue = 0;
            int bitShift = length - 1;
            for (int i = startIndex; i < startIndex + length; i++)
            {
                int bitValue = (Bits[i] ? 1 : 0) << bitShift;
                returnValue += bitValue;
                bitShift--;
            }

            return returnValue;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is PackedField other && this.Equals(other);
        }

        /// <inheritdoc/>
        public bool Equals(PackedField other)
        {
            return this.Byte.Equals(other.Byte);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"PackedField [ Byte={this.Byte} ]";
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return this.Byte.GetHashCode();
        }

        [Conditional("DEBUG")]
        private static void DebugCheckLength(int startIndex, int length)
        {
            if (length < 1 || startIndex + length > 8)
            {
                string message = "Length must be greater than zero and the sum of length and start index must be less than 8. "
                                 + $"Supplied length: {length}. Supplied start index: {startIndex}";

                throw new ArgumentOutOfRangeException(nameof(length), message);
            }
        }
    }
}