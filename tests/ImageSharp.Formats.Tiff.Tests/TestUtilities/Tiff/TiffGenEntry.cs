// <copyright file="TiffGenEntry.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using ImageSharp.Formats;

    /// <summary>
    /// A utility data structure to represent Tiff IFD entries in unit tests.
    /// </summary>
    internal abstract class TiffGenEntry : ITiffGenDataSource
    {
        private TiffGenEntry(ushort tag, TiffType type)
        {
            this.Tag = tag;
            this.Type = type;
        }

        public ushort Tag { get; }
        public TiffType Type { get; }

        public abstract IEnumerable<TiffGenDataBlock> GetData(bool isLittleEndian);

        public static TiffGenEntry Ascii(ushort tag, string value)
        {
            return new TiffGenEntryAscii(tag, value);
        }

        public static TiffGenEntry Integer(ushort tag, TiffType type, int value)
        {
            return TiffGenEntry.Integer(tag, type, new int[] {value});
        }

        public static TiffGenEntry Integer(ushort tag, TiffType type, int[] value)
        {
            if (type != TiffType.Byte && type != TiffType.Short && type != TiffType.Long &&
                type != TiffType.SByte && type != TiffType.SShort && type != TiffType.SLong)
                throw new ArgumentException(nameof(type), "The specified type is not an integer type.");

            return new TiffGenEntryInteger(tag, type, value);
        }

        private class TiffGenEntryAscii : TiffGenEntry
        {
            public TiffGenEntryAscii(ushort tag, string value) : base(tag, TiffType.Ascii)
            {
                this.Value = value;
            }

            public string Value { get; }

            public override IEnumerable<TiffGenDataBlock> GetData(bool isLittleEndian)
            {
                byte[] bytes = Encoding.ASCII.GetBytes($"{Value}\0");
                return new[] { new TiffGenDataBlock(bytes) };
            }
        }

        private class TiffGenEntryInteger : TiffGenEntry
        {
            public TiffGenEntryInteger(ushort tag, TiffType type, int[] value) : base(tag, type)
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
    }
}