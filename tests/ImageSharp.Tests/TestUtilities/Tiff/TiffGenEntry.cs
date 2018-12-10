// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using ImageSharp.Formats.Tiff;

    /// <summary>
    /// A utility data structure to represent Tiff IFD entries in unit tests.
    /// </summary>
    internal abstract class TiffGenEntry : ITiffGenDataSource
    {
        private TiffGenEntry(ushort tag, TiffType type, uint count)
        {
            this.Tag = tag;
            this.Type = type;
            this.Count = count;
        }

        public uint Count { get; }
        public ushort Tag { get; }
        public TiffType Type { get; }

        public abstract IEnumerable<TiffGenDataBlock> GetData(bool isLittleEndian);

        public static TiffGenEntry Ascii(ushort tag, string value)
        {
            return new TiffGenEntryAscii(tag, value);
        }

        public static TiffGenEntry Bytes(ushort tag, TiffType type, uint count, byte[] value)
        {
            return new TiffGenEntryBytes(tag, type, count, value);
        }

        public static TiffGenEntry Integer(ushort tag, TiffType type, int value)
        {
            return TiffGenEntry.Integer(tag, type, new int[] { value });
        }

        public static TiffGenEntry Integer(ushort tag, TiffType type, int[] value)
        {
            if (type != TiffType.Byte && type != TiffType.Short && type != TiffType.Long &&
                type != TiffType.SByte && type != TiffType.SShort && type != TiffType.SLong)
                throw new ArgumentException(nameof(type), "The specified type is not an integer type.");

            return new TiffGenEntryInteger(tag, type, value);
        }

        public static TiffGenEntry Integer(ushort tag, TiffType type, uint value)
        {
            return TiffGenEntry.Integer(tag, type, new uint[] { value });
        }

        public static TiffGenEntry Integer(ushort tag, TiffType type, uint[] value)
        {
            if (type != TiffType.Byte && type != TiffType.Short && type != TiffType.Long &&
                type != TiffType.SByte && type != TiffType.SShort && type != TiffType.SLong)
                throw new ArgumentException(nameof(type), "The specified type is not an integer type.");

            return new TiffGenEntryUnsignedInteger(tag, type, value);
        }

        public static TiffGenEntry Rational(ushort tag, uint numerator, uint denominator)
        {
            return new TiffGenEntryRational(tag, numerator, denominator);
        }

        private class TiffGenEntryAscii : TiffGenEntry
        {
            public TiffGenEntryAscii(ushort tag, string value) : base(tag, TiffType.Ascii, (uint)GetBytes(value).Length)
            {
                this.Value = value;
            }

            public string Value { get; }

            public override IEnumerable<TiffGenDataBlock> GetData(bool isLittleEndian)
            {
                byte[] bytes = GetBytes(Value);
                return new[] { new TiffGenDataBlock(bytes) };
            }

            private static byte[] GetBytes(string value)
            {
                return Encoding.ASCII.GetBytes($"{value}\0");
            }
        }

        private class TiffGenEntryBytes : TiffGenEntry
        {
            public TiffGenEntryBytes(ushort tag, TiffType type, uint count, byte[] value) : base(tag, type, count)
            {
                this.Value = value;
            }

            public byte[] Value { get; }

            public override IEnumerable<TiffGenDataBlock> GetData(bool isLittleEndian)
            {
                return new[] { new TiffGenDataBlock(Value) };
            }
        }

        private class TiffGenEntryInteger : TiffGenEntry
        {
            public TiffGenEntryInteger(ushort tag, TiffType type, int[] value) : base(tag, type, (uint)value.Length)
            {
                this.Value = value;
            }

            public int[] Value { get; }

            public override IEnumerable<TiffGenDataBlock> GetData(bool isLittleEndian)
            {
                byte[] bytes = GetBytes().SelectMany(b => b.WithByteOrder(isLittleEndian)).ToArray();
                return new[] { new TiffGenDataBlock(bytes) };
            }

            private IEnumerable<byte[]> GetBytes()
            {
                switch (Type)
                {
                    case TiffType.Byte:
                        return Value.Select(i => new byte[] { (byte)i });
                    case TiffType.Short:
                        return Value.Select(i => BitConverter.GetBytes((ushort)i));
                    case TiffType.Long:
                        return Value.Select(i => BitConverter.GetBytes((uint)i));
                    case TiffType.SByte:
                        return Value.Select(i => BitConverter.GetBytes((sbyte)i));
                    case TiffType.SShort:
                        return Value.Select(i => BitConverter.GetBytes((short)i));
                    case TiffType.SLong:
                        return Value.Select(i => BitConverter.GetBytes((int)i));
                    default:
                        throw new InvalidOperationException();
                }
            }
        }

        private class TiffGenEntryUnsignedInteger : TiffGenEntry
        {
            public TiffGenEntryUnsignedInteger(ushort tag, TiffType type, uint[] value) : base(tag, type, (uint)value.Length)
            {
                this.Value = value;
            }

            public uint[] Value { get; }

            public override IEnumerable<TiffGenDataBlock> GetData(bool isLittleEndian)
            {
                byte[] bytes = GetBytes().SelectMany(b => b.WithByteOrder(isLittleEndian)).ToArray();
                return new[] { new TiffGenDataBlock(bytes) };
            }

            private IEnumerable<byte[]> GetBytes()
            {
                switch (Type)
                {
                    case TiffType.Byte:
                        return Value.Select(i => new byte[] { (byte)i });
                    case TiffType.Short:
                        return Value.Select(i => BitConverter.GetBytes((ushort)i));
                    case TiffType.Long:
                        return Value.Select(i => BitConverter.GetBytes((uint)i));
                    case TiffType.SByte:
                        return Value.Select(i => BitConverter.GetBytes((sbyte)i));
                    case TiffType.SShort:
                        return Value.Select(i => BitConverter.GetBytes((short)i));
                    case TiffType.SLong:
                        return Value.Select(i => BitConverter.GetBytes((int)i));
                    default:
                        throw new InvalidOperationException();
                }
            }
        }

        private class TiffGenEntryRational : TiffGenEntry
        {
            public TiffGenEntryRational(ushort tag, uint numerator, uint denominator) : base(tag, TiffType.Rational, 1u)
            {
                this.Numerator = numerator;
                this.Denominator = denominator;
            }

            public uint Numerator { get; }

            public uint Denominator { get; }

            public override IEnumerable<TiffGenDataBlock> GetData(bool isLittleEndian)
            {
                byte[] numeratorBytes = BitConverter.GetBytes(Numerator).WithByteOrder(isLittleEndian);
                byte[] denominatorBytes = BitConverter.GetBytes(Denominator).WithByteOrder(isLittleEndian);
                byte[] bytes = Enumerable.Concat(numeratorBytes, denominatorBytes).ToArray();
                return new[] { new TiffGenDataBlock(bytes) };
            }
        }
    }
}