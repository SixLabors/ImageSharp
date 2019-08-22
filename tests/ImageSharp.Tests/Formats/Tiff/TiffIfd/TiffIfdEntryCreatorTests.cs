// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Primitives;
using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    [Trait("Category", "Tiff")]
    public class TiffIfdEntryCreatorTests
    {
        [Theory]
        [InlineDataAttribute(new byte[] { 0 }, 0)]
        [InlineDataAttribute(new byte[] { 1 }, 1)]
        [InlineDataAttribute(new byte[] { 255 }, 255)]
        public void AddUnsignedByte_AddsSingleValue(byte[] bytes, uint value)
        {
            var entries = new List<TiffIfdEntry>();

            entries.AddUnsignedByte(TiffTags.ImageWidth, value);

            var entry = entries[0];
            Assert.Equal(TiffTags.ImageWidth, entry.Tag);
            Assert.Equal(TiffType.Byte, entry.Type);
            Assert.Equal(1u, entry.Count);
            Assert.Equal(bytes, entry.Value);
        }

        [Theory]
        [InlineDataAttribute(new byte[] { 0 }, new uint[] { 0 })]
        [InlineDataAttribute(new byte[] { 0, 1, 2 }, new uint[] { 0, 1, 2 })]
        [InlineDataAttribute(new byte[] { 0, 1, 2, 3, 4, 5, 6 }, new uint[] { 0, 1, 2, 3, 4, 5, 6 })]
        public void AddUnsignedByte_AddsArray(byte[] bytes, uint[] value)
        {
            var entries = new List<TiffIfdEntry>();

            entries.AddUnsignedByte(TiffTags.ImageWidth, value);

            var entry = entries[0];
            Assert.Equal(TiffTags.ImageWidth, entry.Tag);
            Assert.Equal(TiffType.Byte, entry.Type);
            Assert.Equal((uint)value.Length, entry.Count);
            Assert.Equal(bytes, entry.Value);
        }

        [Theory]
        [InlineDataAttribute(new byte[] { 0, 0 }, 0)]
        [InlineDataAttribute(new byte[] { 1, 0 }, 1)]
        [InlineDataAttribute(new byte[] { 0, 1 }, 256)]
        [InlineDataAttribute(new byte[] { 2, 1 }, 258)]
        [InlineDataAttribute(new byte[] { 255, 255 }, UInt16.MaxValue)]
        public void AddUnsignedShort_AddsSingleValue(byte[] bytes, uint value)
        {
            var entries = new List<TiffIfdEntry>();

            entries.AddUnsignedShort(TiffTags.ImageWidth, value);

            var entry = entries[0];
            Assert.Equal(TiffTags.ImageWidth, entry.Tag);
            Assert.Equal(TiffType.Short, entry.Type);
            Assert.Equal(1u, entry.Count);
            Assert.Equal(bytes, entry.Value);
        }

        [Theory]
        [InlineDataAttribute(new byte[] { 1, 0 }, new uint[] { 1 })]
        [InlineDataAttribute(new byte[] { 1, 0, 3, 2 }, new uint[] { 1, 515 })]
        [InlineDataAttribute(new byte[] { 1, 0, 3, 2, 5, 4 }, new uint[] { 1, 515, 1029 })]
        public void AddUnsignedShort_AddsArray(byte[] bytes, uint[] value)
        {
            var entries = new List<TiffIfdEntry>();

            entries.AddUnsignedShort(TiffTags.ImageWidth, value);

            var entry = entries[0];
            Assert.Equal(TiffTags.ImageWidth, entry.Tag);
            Assert.Equal(TiffType.Short, entry.Type);
            Assert.Equal((uint)value.Length, entry.Count);
            Assert.Equal(bytes, entry.Value);
        }

        [Theory]
        [InlineDataAttribute(new byte[] { 0, 0, 0, 0 }, 0)]
        [InlineDataAttribute(new byte[] { 1, 0, 0, 0 }, 1)]
        [InlineDataAttribute(new byte[] { 0, 1, 0, 0 }, 256)]
        [InlineDataAttribute(new byte[] { 0, 0, 1, 0 }, 256 * 256)]
        [InlineDataAttribute(new byte[] { 0, 0, 0, 1 }, 256 * 256 * 256)]
        [InlineDataAttribute(new byte[] { 1, 2, 3, 4 }, 67305985)]
        [InlineDataAttribute(new byte[] { 255, 255, 255, 255 }, UInt32.MaxValue)]
        public void AddUnsignedLong_AddsSingleValue(byte[] bytes, uint value)
        {
            var entries = new List<TiffIfdEntry>();

            entries.AddUnsignedLong(TiffTags.ImageWidth, value);

            var entry = entries[0];
            Assert.Equal(TiffTags.ImageWidth, entry.Tag);
            Assert.Equal(TiffType.Long, entry.Type);
            Assert.Equal(1u, entry.Count);
            Assert.Equal(bytes, entry.Value);
        }

        [Theory]
        [InlineDataAttribute(new byte[] { 4, 3, 2, 1 }, new uint[] { 0x01020304 })]
        [InlineDataAttribute(new byte[] { 4, 3, 2, 1, 6, 5, 4, 3 }, new uint[] { 0x01020304, 0x03040506 })]
        public void AddUnsignedLong_AddsArray(byte[] bytes, uint[] value)
        {
            var entries = new List<TiffIfdEntry>();

            entries.AddUnsignedLong(TiffTags.ImageWidth, value);

            var entry = entries[0];
            Assert.Equal(TiffTags.ImageWidth, entry.Tag);
            Assert.Equal(TiffType.Long, entry.Type);
            Assert.Equal((uint)value.Length, entry.Count);
            Assert.Equal(bytes, entry.Value);
        }

        [Theory]
        [InlineDataAttribute(new byte[] { 0 }, 0)]
        [InlineDataAttribute(new byte[] { 1 }, 1)]
        [InlineDataAttribute(new byte[] { 255 }, -1)]
        public void AddSignedByte_AddsSingleValue(byte[] bytes, int value)
        {
            var entries = new List<TiffIfdEntry>();

            entries.AddSignedByte(TiffTags.ImageWidth, value);

            var entry = entries[0];
            Assert.Equal(TiffTags.ImageWidth, entry.Tag);
            Assert.Equal(TiffType.SByte, entry.Type);
            Assert.Equal(1u, entry.Count);
            Assert.Equal(bytes, entry.Value);
        }

        [Theory]
        [InlineDataAttribute(new byte[] { 0 }, new int[] { 0 })]
        [InlineDataAttribute(new byte[] { 0, 255, 2 }, new int[] { 0, -1, 2 })]
        [InlineDataAttribute(new byte[] { 0, 255, 2, 3, 4, 5, 6 }, new int[] { 0, -1, 2, 3, 4, 5, 6 })]
        public void AddSignedByte_AddsArray(byte[] bytes, int[] value)
        {
            var entries = new List<TiffIfdEntry>();

            entries.AddSignedByte(TiffTags.ImageWidth, value);

            var entry = entries[0];
            Assert.Equal(TiffTags.ImageWidth, entry.Tag);
            Assert.Equal(TiffType.SByte, entry.Type);
            Assert.Equal((uint)value.Length, entry.Count);
            Assert.Equal(bytes, entry.Value);
        }

        [Theory]
        [InlineDataAttribute(new byte[] { 0, 0 }, 0)]
        [InlineDataAttribute(new byte[] { 1, 0 }, 1)]
        [InlineDataAttribute(new byte[] { 0, 1 }, 256)]
        [InlineDataAttribute(new byte[] { 2, 1 }, 258)]
        [InlineDataAttribute(new byte[] { 255, 255 }, -1)]
        public void AddSignedShort_AddsSingleValue(byte[] bytes, int value)
        {
            var entries = new List<TiffIfdEntry>();

            entries.AddSignedShort(TiffTags.ImageWidth, value);

            var entry = entries[0];
            Assert.Equal(TiffTags.ImageWidth, entry.Tag);
            Assert.Equal(TiffType.SShort, entry.Type);
            Assert.Equal(1u, entry.Count);
            Assert.Equal(bytes, entry.Value);
        }

        [Theory]
        [InlineDataAttribute(new byte[] { 1, 0 }, new int[] { 1 })]
        [InlineDataAttribute(new byte[] { 1, 0, 255, 255 }, new int[] { 1, -1 })]
        [InlineDataAttribute(new byte[] { 1, 0, 255, 255, 5, 4 }, new int[] { 1, -1, 1029 })]
        public void AddSignedShort_AddsArray(byte[] bytes, int[] value)
        {
            var entries = new List<TiffIfdEntry>();

            entries.AddSignedShort(TiffTags.ImageWidth, value);

            var entry = entries[0];
            Assert.Equal(TiffTags.ImageWidth, entry.Tag);
            Assert.Equal(TiffType.SShort, entry.Type);
            Assert.Equal((uint)value.Length, entry.Count);
            Assert.Equal(bytes, entry.Value);
        }

        [Theory]
        [InlineDataAttribute(new byte[] { 0, 0, 0, 0 }, 0)]
        [InlineDataAttribute(new byte[] { 1, 0, 0, 0 }, 1)]
        [InlineDataAttribute(new byte[] { 0, 1, 0, 0 }, 256)]
        [InlineDataAttribute(new byte[] { 0, 0, 1, 0 }, 256 * 256)]
        [InlineDataAttribute(new byte[] { 0, 0, 0, 1 }, 256 * 256 * 256)]
        [InlineDataAttribute(new byte[] { 1, 2, 3, 4 }, 67305985)]
        [InlineDataAttribute(new byte[] { 255, 255, 255, 255 }, -1)]
        public void AddSignedLong_AddsSingleValue(byte[] bytes, int value)
        {
            var entries = new List<TiffIfdEntry>();

            entries.AddSignedLong(TiffTags.ImageWidth, value);

            var entry = entries[0];
            Assert.Equal(TiffTags.ImageWidth, entry.Tag);
            Assert.Equal(TiffType.SLong, entry.Type);
            Assert.Equal(1u, entry.Count);
            Assert.Equal(bytes, entry.Value);
        }

        [Theory]
        [InlineDataAttribute(new byte[] { 4, 3, 2, 1 }, new int[] { 0x01020304 })]
        [InlineDataAttribute(new byte[] { 4, 3, 2, 1, 255, 255, 255, 255 }, new int[] { 0x01020304, -1 })]
        public void AddSignedLong_AddsArray(byte[] bytes, int[] value)
        {
            var entries = new List<TiffIfdEntry>();

            entries.AddSignedLong(TiffTags.ImageWidth, value);

            var entry = entries[0];
            Assert.Equal(TiffTags.ImageWidth, entry.Tag);
            Assert.Equal(TiffType.SLong, entry.Type);
            Assert.Equal((uint)value.Length, entry.Count);
            Assert.Equal(bytes, entry.Value);
        }

        [Theory]
        [InlineDataAttribute(new byte[] { 0 }, "")]
        [InlineDataAttribute(new byte[] { (byte)'A', (byte)'B', (byte)'C', 0 }, "ABC")]
        [InlineDataAttribute(new byte[] { (byte)'A', (byte)'B', (byte)'C', (byte)'D', (byte)'E', (byte)'F', 0 }, "ABCDEF")]
        [InlineDataAttribute(new byte[] { (byte)'A', (byte)'B', (byte)'C', (byte)'D', 0, (byte)'E', (byte)'F', (byte)'G', (byte)'H', 0 }, "ABCD\0EFGH")]
        public void AddAscii_AddsEntry(byte[] bytes, string value)
        {
            var entries = new List<TiffIfdEntry>();

            entries.AddAscii(TiffTags.ImageWidth, value);

            var entry = entries[0];
            Assert.Equal(TiffTags.ImageWidth, entry.Tag);
            Assert.Equal(TiffType.Ascii, entry.Type);
            Assert.Equal((uint)bytes.Length, entry.Count);
            Assert.Equal(bytes, entry.Value);
        }

        [Theory]
        [InlineDataAttribute(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 }, 0, 0)]
        [InlineDataAttribute(new byte[] { 2, 0, 0, 0, 1, 0, 0, 0 }, 2, 1)]
        [InlineDataAttribute(new byte[] { 1, 0, 0, 0, 2, 0, 0, 0 }, 1, 2)]
        public void AddUnsignedRational_AddsSingleValue(byte[] bytes, uint numerator, uint denominator)
        {
            var entries = new List<TiffIfdEntry>();

            entries.AddUnsignedRational(TiffTags.ImageWidth, new Rational(numerator, denominator));

            var entry = entries[0];
            Assert.Equal(TiffTags.ImageWidth, entry.Tag);
            Assert.Equal(TiffType.Rational, entry.Type);
            Assert.Equal(1u, entry.Count);
            Assert.Equal(bytes, entry.Value);
        }

        [Theory]
        [InlineDataAttribute(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 }, new uint[] { 0 }, new uint[] { 0 })]
        [InlineDataAttribute(new byte[] { 1, 0, 0, 0, 2, 0, 0, 0 }, new uint[] { 1 }, new uint[] { 2 })]
        [InlineDataAttribute(new byte[] { 1, 0, 0, 0, 2, 0, 0, 0, 2, 0, 0, 0, 3, 0, 0, 0 }, new uint[] { 1, 2 }, new uint[] { 2, 3 })]
        public void AddUnsignedRational_AddsArray(byte[] bytes, uint[] numerators, uint[] denominators)
        {
            var entries = new List<TiffIfdEntry>();
            Rational[] value = Enumerable.Range(0, numerators.Length).Select(i => new Rational(numerators[i], denominators[i])).ToArray();

            entries.AddUnsignedRational(TiffTags.ImageWidth, value);

            var entry = entries[0];
            Assert.Equal(TiffTags.ImageWidth, entry.Tag);
            Assert.Equal(TiffType.Rational, entry.Type);
            Assert.Equal((uint)numerators.Length, entry.Count);
            Assert.Equal(bytes, entry.Value);
        }

        [Theory]
        [InlineDataAttribute(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 }, 0, 0)]
        [InlineDataAttribute(new byte[] { 2, 0, 0, 0, 1, 0, 0, 0 }, 2, 1)]
        [InlineDataAttribute(new byte[] { 1, 0, 0, 0, 2, 0, 0, 0 }, 1, 2)]
        [InlineDataAttribute(new byte[] { 255, 255, 255, 255, 2, 0, 0, 0 }, -1, 2)]
        public void AddSignedRational_AddsSingleValue(byte[] bytes, int numerator, int denominator)
        {
            var entries = new List<TiffIfdEntry>();

            entries.AddSignedRational(TiffTags.ImageWidth, new SignedRational(numerator, denominator));

            var entry = entries[0];
            Assert.Equal(TiffTags.ImageWidth, entry.Tag);
            Assert.Equal(TiffType.SRational, entry.Type);
            Assert.Equal(1u, entry.Count);
            Assert.Equal(bytes, entry.Value);
        }

        [Theory]
        [InlineDataAttribute(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 }, new int[] { 0 }, new int[] { 0 })]
        [InlineDataAttribute(new byte[] { 2, 0, 0, 0, 1, 0, 0, 0 }, new int[] { 2 }, new int[] { 1 })]
        [InlineDataAttribute(new byte[] { 1, 0, 0, 0, 2, 0, 0, 0 }, new int[] { 1 }, new int[] { 2 })]
        [InlineDataAttribute(new byte[] { 255, 255, 255, 255, 2, 0, 0, 0 }, new int[] { -1 }, new int[] { 2 })]
        [InlineDataAttribute(new byte[] { 255, 255, 255, 255, 2, 0, 0, 0, 2, 0, 0, 0, 3, 0, 0, 0 }, new int[] { -1, 2 }, new int[] { 2, 3 })]
        public void AddSignedRational_AddsArray(byte[] bytes, int[] numerators, int[] denominators)
        {
            var entries = new List<TiffIfdEntry>();
            SignedRational[] value = Enumerable.Range(0, numerators.Length).Select(i => new SignedRational(numerators[i], denominators[i])).ToArray();

            entries.AddSignedRational(TiffTags.ImageWidth, value);

            var entry = entries[0];
            Assert.Equal(TiffTags.ImageWidth, entry.Tag);
            Assert.Equal(TiffType.SRational, entry.Type);
            Assert.Equal((uint)numerators.Length, entry.Count);
            Assert.Equal(bytes, entry.Value);
        }

        [Theory]
        [InlineDataAttribute(new byte[] { 0x00, 0x00, 0x00, 0x00 }, 0.0F)]
        [InlineDataAttribute(new byte[] { 0x00, 0x00, 0x80, 0x3F }, 1.0F)]
        [InlineDataAttribute(new byte[] { 0x00, 0x00, 0x00, 0xC0 }, -2.0F)]
        [InlineDataAttribute(new byte[] { 0xFF, 0xFF, 0x7F, 0x7F }, float.MaxValue)]
        [InlineDataAttribute(new byte[] { 0x00, 0x00, 0x80, 0x7F }, float.PositiveInfinity)]
        [InlineDataAttribute(new byte[] { 0x00, 0x00, 0x80, 0xFF }, float.NegativeInfinity)]
        public void AddFloat_AddsSingleValue(byte[] bytes, float value)
        {
            var entries = new List<TiffIfdEntry>();

            entries.AddFloat(TiffTags.ImageWidth, value);

            var entry = entries[0];
            Assert.Equal(TiffTags.ImageWidth, entry.Tag);
            Assert.Equal(TiffType.Float, entry.Type);
            Assert.Equal(1u, entry.Count);
            Assert.Equal(bytes, entry.Value);
        }

        [Theory]
        [InlineDataAttribute(new byte[] { 0x00, 0x00, 0x00, 0x00 }, new float[] { 0.0F })]
        [InlineDataAttribute(new byte[] { 0x00, 0x00, 0x80, 0x3F }, new float[] { 1.0F })]
        [InlineDataAttribute(new byte[] { 0x00, 0x00, 0x00, 0xC0 }, new float[] { -2.0F })]
        [InlineDataAttribute(new byte[] { 0xFF, 0xFF, 0x7F, 0x7F }, new float[] { float.MaxValue })]
        [InlineDataAttribute(new byte[] { 0x00, 0x00, 0x80, 0x7F }, new float[] { float.PositiveInfinity })]
        [InlineDataAttribute(new byte[] { 0x00, 0x00, 0x80, 0xFF }, new float[] { float.NegativeInfinity })]
        [InlineDataAttribute(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x80, 0x3F, 0x00, 0x00, 0x00, 0xC0 }, new float[] { 0.0F, 1.0F, -2.0F })]
        public void AddFloat_AddsArray(byte[] bytes, float[] value)
        {
            var entries = new List<TiffIfdEntry>();

            entries.AddFloat(TiffTags.ImageWidth, value);

            var entry = entries[0];
            Assert.Equal(TiffTags.ImageWidth, entry.Tag);
            Assert.Equal(TiffType.Float, entry.Type);
            Assert.Equal((uint)value.Length, entry.Count);
            Assert.Equal(bytes, entry.Value);
        }

        [Theory]
        [InlineDataAttribute(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, 0.0)]
        [InlineDataAttribute(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xF0, 0x3F }, 1.0)]
        [InlineDataAttribute(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x40 }, 2.0)]
        [InlineDataAttribute(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xC0 }, -2.0)]
        [InlineDataAttribute(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xEF, 0x7F }, double.MaxValue)]
        [InlineDataAttribute(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xF0, 0x7F }, double.PositiveInfinity)]
        [InlineDataAttribute(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xF0, 0xFF }, double.NegativeInfinity)]
        [InlineDataAttribute(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xF8, 0xFF }, double.NaN)]
        public void AddDouble_AddsSingleValue(byte[] bytes, double value)
        {
            var entries = new List<TiffIfdEntry>();

            entries.AddDouble(TiffTags.ImageWidth, value);

            var entry = entries[0];
            Assert.Equal(TiffTags.ImageWidth, entry.Tag);
            Assert.Equal(TiffType.Double, entry.Type);
            Assert.Equal(1u, entry.Count);
            Assert.Equal(bytes, entry.Value);
        }

        [Theory]
        [InlineDataAttribute(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, new double[] { 0.0 })]
        [InlineDataAttribute(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xF0, 0x3F }, new double[] { 1.0 })]
        [InlineDataAttribute(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x40 }, new double[] { 2.0 })]
        [InlineDataAttribute(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xC0 }, new double[] { -2.0 })]
        [InlineDataAttribute(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xEF, 0x7F }, new double[] { double.MaxValue })]
        [InlineDataAttribute(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xF0, 0x7F }, new double[] { double.PositiveInfinity })]
        [InlineDataAttribute(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xF0, 0xFF }, new double[] { double.NegativeInfinity })]
        [InlineDataAttribute(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xF8, 0xFF }, new double[] { double.NaN })]
        [InlineDataAttribute(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xF0, 0x3F, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xC0 }, new double[] { 0.0, 1.0, -2.0 })]
        public void AddDouble_AddsArray(byte[] bytes, double[] value)
        {
            var entries = new List<TiffIfdEntry>();

            entries.AddDouble(TiffTags.ImageWidth, value);

            var entry = entries[0];
            Assert.Equal(TiffTags.ImageWidth, entry.Tag);
            Assert.Equal(TiffType.Double, entry.Type);
            Assert.Equal((uint)value.Length, entry.Count);
            Assert.Equal(bytes, entry.Value);
        }
    }
}
