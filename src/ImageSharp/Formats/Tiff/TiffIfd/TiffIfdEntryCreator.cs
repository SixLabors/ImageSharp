// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Text;
using SixLabors.ImageSharp.Primitives;

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// Utility class for generating TIFF IFD entries.
    /// </summary>
    internal static class TiffIfdEntryCreator
    {
        /// <summary>
        /// Adds a new <see cref="TiffIfdEntry"/> of type 'Byte' from a unsigned integer.
        /// </summary>
        /// <param name="entries">The list of <see cref="TiffIfdEntry"/> to add the new entry to.</param>
        /// <param name="tag">The tag for the resulting entry.</param>
        /// <param name="value">The value for the resulting entry.</param>
        public static void AddUnsignedByte(this List<TiffIfdEntry> entries, ushort tag, uint value)
        {
            TiffIfdEntryCreator.AddUnsignedByte(entries, tag, new[] { value });
        }

        /// <summary>
        /// Adds a new <see cref="TiffIfdEntry"/> of type 'Byte' from an array of unsigned integers.
        /// </summary>
        /// <param name="entries">The list of <see cref="TiffIfdEntry"/> to add the new entry to.</param>
        /// <param name="tag">The tag for the resulting entry.</param>
        /// <param name="value">The value for the resulting entry.</param>
        public static void AddUnsignedByte(this List<TiffIfdEntry> entries, ushort tag, uint[] value)
        {
            byte[] bytes = new byte[value.Length];

            for (int i = 0; i < value.Length; i++)
            {
                bytes[i] = (byte)value[i];
            }

            entries.Add(new TiffIfdEntry(tag, TiffType.Byte, (uint)value.Length, bytes));
        }

        /// <summary>
        /// Adds a new <see cref="TiffIfdEntry"/> of type 'Short' from a unsigned integer.
        /// </summary>
        /// <param name="entries">The list of <see cref="TiffIfdEntry"/> to add the new entry to.</param>
        /// <param name="tag">The tag for the resulting entry.</param>
        /// <param name="value">The value for the resulting entry.</param>
        public static void AddUnsignedShort(this List<TiffIfdEntry> entries, ushort tag, uint value)
        {
            TiffIfdEntryCreator.AddUnsignedShort(entries, tag, new[] { value });
        }

        /// <summary>
        /// Adds a new <see cref="TiffIfdEntry"/> of type 'Short' from an array of unsigned integers.
        /// </summary>
        /// <param name="entries">The list of <see cref="TiffIfdEntry"/> to add the new entry to.</param>
        /// <param name="tag">The tag for the resulting entry.</param>
        /// <param name="value">The value for the resulting entry.</param>
        public static void AddUnsignedShort(this List<TiffIfdEntry> entries, ushort tag, uint[] value)
        {
            byte[] bytes = new byte[value.Length * TiffConstants.SizeOfShort];

            for (int i = 0; i < value.Length; i++)
            {
                ToBytes((ushort)value[i], bytes, i * TiffConstants.SizeOfShort);
            }

            entries.Add(new TiffIfdEntry(tag, TiffType.Short, (uint)value.Length, bytes));
        }

        /// <summary>
        /// Adds a new <see cref="TiffIfdEntry"/> of type 'Long' from a unsigned integer.
        /// </summary>
        /// <param name="entries">The list of <see cref="TiffIfdEntry"/> to add the new entry to.</param>
        /// <param name="tag">The tag for the resulting entry.</param>
        /// <param name="value">The value for the resulting entry.</param>
        public static void AddUnsignedLong(this List<TiffIfdEntry> entries, ushort tag, uint value)
        {
            TiffIfdEntryCreator.AddUnsignedLong(entries, tag, new[] { value });
        }

        /// <summary>
        /// Adds a new <see cref="TiffIfdEntry"/> of type 'Long' from an array of unsigned integers.
        /// </summary>
        /// <param name="entries">The list of <see cref="TiffIfdEntry"/> to add the new entry to.</param>
        /// <param name="tag">The tag for the resulting entry.</param>
        /// <param name="value">The value for the resulting entry.</param>
        public static void AddUnsignedLong(this List<TiffIfdEntry> entries, ushort tag, uint[] value)
        {
            byte[] bytes = new byte[value.Length * TiffConstants.SizeOfLong];

            for (int i = 0; i < value.Length; i++)
            {
                ToBytes(value[i], bytes, i * TiffConstants.SizeOfLong);
            }

            entries.Add(new TiffIfdEntry(tag, TiffType.Long, (uint)value.Length, bytes));
        }

        /// <summary>
        /// Adds a new <see cref="TiffIfdEntry"/> of type 'SByte' from a signed integer.
        /// </summary>
        /// <param name="entries">The list of <see cref="TiffIfdEntry"/> to add the new entry to.</param>
        /// <param name="tag">The tag for the resulting entry.</param>
        /// <param name="value">The value for the resulting entry.</param>
        public static void AddSignedByte(this List<TiffIfdEntry> entries, ushort tag, int value)
        {
            TiffIfdEntryCreator.AddSignedByte(entries, tag, new[] { value });
        }

        /// <summary>
        /// Adds a new <see cref="TiffIfdEntry"/> of type 'SByte' from an array of signed integers.
        /// </summary>
        /// <param name="entries">The list of <see cref="TiffIfdEntry"/> to add the new entry to.</param>
        /// <param name="tag">The tag for the resulting entry.</param>
        /// <param name="value">The value for the resulting entry.</param>
        public static void AddSignedByte(this List<TiffIfdEntry> entries, ushort tag, int[] value)
        {
            byte[] bytes = new byte[value.Length];

            for (int i = 0; i < value.Length; i++)
            {
                bytes[i] = (byte)((sbyte)value[i]);
            }

            entries.Add(new TiffIfdEntry(tag, TiffType.SByte, (uint)value.Length, bytes));
        }

        /// <summary>
        /// Adds a new <see cref="TiffIfdEntry"/> of type 'SShort' from a signed integer.
        /// </summary>
        /// <param name="entries">The list of <see cref="TiffIfdEntry"/> to add the new entry to.</param>
        /// <param name="tag">The tag for the resulting entry.</param>
        /// <param name="value">The value for the resulting entry.</param>
        public static void AddSignedShort(this List<TiffIfdEntry> entries, ushort tag, int value)
        {
            TiffIfdEntryCreator.AddSignedShort(entries, tag, new[] { value });
        }

        /// <summary>
        /// Adds a new <see cref="TiffIfdEntry"/> of type 'SShort' from an array of signed integers.
        /// </summary>
        /// <param name="entries">The list of <see cref="TiffIfdEntry"/> to add the new entry to.</param>
        /// <param name="tag">The tag for the resulting entry.</param>
        /// <param name="value">The value for the resulting entry.</param>
        public static void AddSignedShort(this List<TiffIfdEntry> entries, ushort tag, int[] value)
        {
            byte[] bytes = new byte[value.Length * TiffConstants.SizeOfShort];

            for (int i = 0; i < value.Length; i++)
            {
                ToBytes((short)value[i], bytes, i * TiffConstants.SizeOfShort);
            }

            entries.Add(new TiffIfdEntry(tag, TiffType.SShort, (uint)value.Length, bytes));
        }

        /// <summary>
        /// Adds a new <see cref="TiffIfdEntry"/> of type 'SLong' from a signed integer.
        /// </summary>
        /// <param name="entries">The list of <see cref="TiffIfdEntry"/> to add the new entry to.</param>
        /// <param name="tag">The tag for the resulting entry.</param>
        /// <param name="value">The value for the resulting entry.</param>
        public static void AddSignedLong(this List<TiffIfdEntry> entries, ushort tag, int value)
        {
            TiffIfdEntryCreator.AddSignedLong(entries, tag, new[] { value });
        }

        /// <summary>
        /// Adds a new <see cref="TiffIfdEntry"/> of type 'SLong' from an array of signed integers.
        /// </summary>
        /// <param name="entries">The list of <see cref="TiffIfdEntry"/> to add the new entry to.</param>
        /// <param name="tag">The tag for the resulting entry.</param>
        /// <param name="value">The value for the resulting entry.</param>
        public static void AddSignedLong(this List<TiffIfdEntry> entries, ushort tag, int[] value)
        {
            byte[] bytes = new byte[value.Length * TiffConstants.SizeOfLong];

            for (int i = 0; i < value.Length; i++)
            {
                ToBytes(value[i], bytes, i * TiffConstants.SizeOfLong);
            }

            entries.Add(new TiffIfdEntry(tag, TiffType.SLong, (uint)value.Length, bytes));
        }

        /// <summary>
        /// Adds a new <see cref="TiffIfdEntry"/> of type 'Ascii' from a string.
        /// </summary>
        /// <param name="entries">The list of <see cref="TiffIfdEntry"/> to add the new entry to.</param>
        /// <param name="tag">The tag for the resulting entry.</param>
        /// <param name="value">The value for the resulting entry.</param>
        public static void AddAscii(this List<TiffIfdEntry> entries, ushort tag, string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value + "\0");

            entries.Add(new TiffIfdEntry(tag, TiffType.Ascii, (uint)bytes.Length, bytes));
        }

        /// <summary>
        /// Adds a new <see cref="TiffIfdEntry"/> of type 'Rational' from a <see cref="Rational"/>.
        /// </summary>
        /// <param name="entries">The list of <see cref="TiffIfdEntry"/> to add the new entry to.</param>
        /// <param name="tag">The tag for the resulting entry.</param>
        /// <param name="value">The value for the resulting entry.</param>
        public static void AddUnsignedRational(this List<TiffIfdEntry> entries, ushort tag, Rational value)
        {
            TiffIfdEntryCreator.AddUnsignedRational(entries, tag, new[] { value });
        }

        /// <summary>
        /// Adds a new <see cref="TiffIfdEntry"/> of type 'Rational' from an array of <see cref="Rational"/> values.
        /// </summary>
        /// <param name="entries">The list of <see cref="TiffIfdEntry"/> to add the new entry to.</param>
        /// <param name="tag">The tag for the resulting entry.</param>
        /// <param name="value">The value for the resulting entry.</param>
        public static void AddUnsignedRational(this List<TiffIfdEntry> entries, ushort tag, Rational[] value)
        {
            byte[] bytes = new byte[value.Length * TiffConstants.SizeOfRational];

            for (int i = 0; i < value.Length; i++)
            {
                int offset = i * TiffConstants.SizeOfRational;
                ToBytes(value[i].Numerator, bytes, offset);
                ToBytes(value[i].Denominator, bytes, offset + TiffConstants.SizeOfLong);
            }

            entries.Add(new TiffIfdEntry(tag, TiffType.Rational, (uint)value.Length, bytes));
        }

        /// <summary>
        /// Adds a new <see cref="TiffIfdEntry"/> of type 'SRational' from a <see cref="SignedRational"/>.
        /// </summary>
        /// <param name="entries">The list of <see cref="TiffIfdEntry"/> to add the new entry to.</param>
        /// <param name="tag">The tag for the resulting entry.</param>
        /// <param name="value">The value for the resulting entry.</param>
        public static void AddSignedRational(this List<TiffIfdEntry> entries, ushort tag, SignedRational value)
        {
            TiffIfdEntryCreator.AddSignedRational(entries, tag, new[] { value });
        }

        /// <summary>
        /// Adds a new <see cref="TiffIfdEntry"/> of type 'SRational' from an array of <see cref="SignedRational"/> values.
        /// </summary>
        /// <param name="entries">The list of <see cref="TiffIfdEntry"/> to add the new entry to.</param>
        /// <param name="tag">The tag for the resulting entry.</param>
        /// <param name="value">The value for the resulting entry.</param>
        public static void AddSignedRational(this List<TiffIfdEntry> entries, ushort tag, SignedRational[] value)
        {
            byte[] bytes = new byte[value.Length * TiffConstants.SizeOfRational];

            for (int i = 0; i < value.Length; i++)
            {
                int offset = i * TiffConstants.SizeOfRational;
                ToBytes(value[i].Numerator, bytes, offset);
                ToBytes(value[i].Denominator, bytes, offset + TiffConstants.SizeOfLong);
            }

            entries.Add(new TiffIfdEntry(tag, TiffType.SRational, (uint)value.Length, bytes));
        }

        /// <summary>
        /// Adds a new <see cref="TiffIfdEntry"/> of type 'Float' from a floating-point value.
        /// </summary>
        /// <param name="entries">The list of <see cref="TiffIfdEntry"/> to add the new entry to.</param>
        /// <param name="tag">The tag for the resulting entry.</param>
        /// <param name="value">The value for the resulting entry.</param>
        public static void AddFloat(this List<TiffIfdEntry> entries, ushort tag, float value)
        {
            TiffIfdEntryCreator.AddFloat(entries, tag, new[] { value });
        }

        /// <summary>
        /// Adds a new <see cref="TiffIfdEntry"/> of type 'Float' from an array of floating-point values.
        /// </summary>
        /// <param name="entries">The list of <see cref="TiffIfdEntry"/> to add the new entry to.</param>
        /// <param name="tag">The tag for the resulting entry.</param>
        /// <param name="value">The value for the resulting entry.</param>
        public static void AddFloat(this List<TiffIfdEntry> entries, ushort tag, float[] value)
        {
            byte[] bytes = new byte[value.Length * TiffConstants.SizeOfFloat];

            for (int i = 0; i < value.Length; i++)
            {
                byte[] itemBytes = BitConverter.GetBytes(value[i]);
                Array.Copy(itemBytes, 0, bytes, i * TiffConstants.SizeOfFloat, TiffConstants.SizeOfFloat);
            }

            entries.Add(new TiffIfdEntry(tag, TiffType.Float, (uint)value.Length, bytes));
        }

        /// <summary>
        /// Adds a new <see cref="TiffIfdEntry"/> of type 'Double' from a floating-point value.
        /// </summary>
        /// <param name="entries">The list of <see cref="TiffIfdEntry"/> to add the new entry to.</param>
        /// <param name="tag">The tag for the resulting entry.</param>
        /// <param name="value">The value for the resulting entry.</param>
        public static void AddDouble(this List<TiffIfdEntry> entries, ushort tag, double value)
        {
            TiffIfdEntryCreator.AddDouble(entries, tag, new[] { value });
        }

        /// <summary>
        /// Adds a new <see cref="TiffIfdEntry"/> of type 'Double' from an array of floating-point values.
        /// </summary>
        /// <param name="entries">The list of <see cref="TiffIfdEntry"/> to add the new entry to.</param>
        /// <param name="tag">The tag for the resulting entry.</param>
        /// <param name="value">The value for the resulting entry.</param>
        public static void AddDouble(this List<TiffIfdEntry> entries, ushort tag, double[] value)
        {
            byte[] bytes = new byte[value.Length * TiffConstants.SizeOfDouble];

            for (int i = 0; i < value.Length; i++)
            {
                byte[] itemBytes = BitConverter.GetBytes(value[i]);
                Array.Copy(itemBytes, 0, bytes, i * TiffConstants.SizeOfDouble, TiffConstants.SizeOfDouble);
            }

            entries.Add(new TiffIfdEntry(tag, TiffType.Double, (uint)value.Length, bytes));
        }

        private static void ToBytes(ushort value, byte[] bytes, int offset)
        {
            bytes[offset + 0] = (byte)value;
            bytes[offset + 1] = (byte)(value >> 8);
        }

        private static void ToBytes(uint value, byte[] bytes, int offset)
        {
            bytes[offset + 0] = (byte)value;
            bytes[offset + 1] = (byte)(value >> 8);
            bytes[offset + 2] = (byte)(value >> 16);
            bytes[offset + 3] = (byte)(value >> 24);
        }

        private static void ToBytes(short value, byte[] bytes, int offset)
        {
            bytes[offset + 0] = (byte)value;
            bytes[offset + 1] = (byte)(value >> 8);
        }

        private static void ToBytes(int value, byte[] bytes, int offset)
        {
            bytes[offset + 0] = (byte)value;
            bytes[offset + 1] = (byte)(value >> 8);
            bytes[offset + 2] = (byte)(value >> 16);
            bytes[offset + 3] = (byte)(value >> 24);
        }
    }
}
