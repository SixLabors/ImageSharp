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
        private TiffGenEntry(ushort tag, TiffTagType type, uint count)
        {
            this.Tag = tag;
            this.Type = type;
            this.Count = count;
        }

        public uint Count { get; }
        public ushort Tag { get; }
        public TiffTagType Type { get; }

        public abstract IEnumerable<TiffGenDataBlock> GetData(bool isLittleEndian);

        public static TiffGenEntry Ascii(TiffTagId tag, string value)
        {
            return new TiffGenEntryAscii((ushort)tag, value);
        }

        public static TiffGenEntry Bytes(TiffTagId tag, TiffTagType type, uint count, byte[] value)
        {
            return new TiffGenEntryBytes((ushort)tag, type, count, value);
        }

        public static TiffGenEntry Integer(TiffTagId tag, TiffTagType type, int value)
        {
            return TiffGenEntry.Integer(tag, type, new int[] { value });
        }

        public static TiffGenEntry Integer(TiffTagId tag, TiffTagType type, int[] value)
        {
            if (type != TiffTagType.Byte && type != TiffTagType.Short && type != TiffTagType.Long &&
                type != TiffTagType.SByte && type != TiffTagType.SShort && type != TiffTagType.SLong)
            {
                throw new ArgumentException(nameof(type), "The specified type is not an integer type.");
            }

            return new TiffGenEntryInteger((ushort)tag, type, value);
        }

        public static TiffGenEntry Integer(TiffTagId tag, TiffTagType type, uint value)
        {
            return TiffGenEntry.Integer(tag, type, new uint[] { value });
        }

        public static TiffGenEntry Integer(TiffTagId tag, TiffTagType type, uint[] value)
        {
            if (type != TiffTagType.Byte && type != TiffTagType.Short && type != TiffTagType.Long &&
                type != TiffTagType.SByte && type != TiffTagType.SShort && type != TiffTagType.SLong)
            {
                throw new ArgumentException(nameof(type), "The specified type is not an integer type.");
            }

            return new TiffGenEntryUnsignedInteger((ushort)tag, type, value);
        }

        public static TiffGenEntry Rational(TiffTagId tag, uint numerator, uint denominator)
        {
            return new TiffGenEntryRational((ushort)tag, numerator, denominator);
        }

        private class TiffGenEntryAscii : TiffGenEntry
        {
            public TiffGenEntryAscii(ushort tag, string value) : base(tag, TiffTagType.Ascii, (uint)GetBytes(value).Length)
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
            public TiffGenEntryBytes(ushort tag, TiffTagType type, uint count, byte[] value) : base(tag, type, count)
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
            public TiffGenEntryInteger(ushort tag, TiffTagType type, int[] value) : base(tag, type, (uint)value.Length)
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
                    case TiffTagType.Byte:
                        return Value.Select(i => new byte[] { (byte)i });
                    case TiffTagType.Short:
                        return Value.Select(i => BitConverter.GetBytes((ushort)i));
                    case TiffTagType.Long:
                        return Value.Select(i => BitConverter.GetBytes((uint)i));
                    case TiffTagType.SByte:
                        return Value.Select(i => BitConverter.GetBytes((sbyte)i));
                    case TiffTagType.SShort:
                        return Value.Select(i => BitConverter.GetBytes((short)i));
                    case TiffTagType.SLong:
                        return Value.Select(i => BitConverter.GetBytes((int)i));
                    default:
                        throw new InvalidOperationException();
                }
            }
        }

        private class TiffGenEntryUnsignedInteger : TiffGenEntry
        {
            public TiffGenEntryUnsignedInteger(ushort tag, TiffTagType type, uint[] value) : base(tag, type, (uint)value.Length)
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
                    case TiffTagType.Byte:
                        return Value.Select(i => new byte[] { (byte)i });
                    case TiffTagType.Short:
                        return Value.Select(i => BitConverter.GetBytes((ushort)i));
                    case TiffTagType.Long:
                        return Value.Select(i => BitConverter.GetBytes((uint)i));
                    case TiffTagType.SByte:
                        return Value.Select(i => BitConverter.GetBytes((sbyte)i));
                    case TiffTagType.SShort:
                        return Value.Select(i => BitConverter.GetBytes((short)i));
                    case TiffTagType.SLong:
                        return Value.Select(i => BitConverter.GetBytes((int)i));
                    default:
                        throw new InvalidOperationException();
                }
            }
        }

        private class TiffGenEntryRational : TiffGenEntry
        {
            public TiffGenEntryRational(ushort tag, uint numerator, uint denominator) : base(tag, TiffTagType.Rational, 1u)
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