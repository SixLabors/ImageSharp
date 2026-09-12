// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Fields;
using SixLabors.ImageSharp.Formats.Jxl.IO.FrameHeader;
using SixLabors.ImageSharp.Formats.Jxl.IO.Metadata;
using SixLabors.ImageSharp.Formats.Jxl.Processing;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.AuxiliaryOutput;

namespace SixLabors.ImageSharp.Tests.Formats.Jxl.IO;

public class FieldsTests
{
    private sealed class OldBundle : IJxlFields
    {
        private uint oldSmall;
        private float oldF;
        private uint oldLarge;
        private ulong extensions;

        public OldBundle() => JxlBundle.Init(this);

        public uint OldSmall
        {
            get => this.oldSmall;
            set => this.oldSmall = value;
        }

        public float OldF
        {
            get => this.oldF;
            set => this.oldF = value;
        }

        public uint OldLarge
        {
            get => this.oldLarge;
            set => this.oldLarge = value;
        }

        public ulong Extensions
        {
            get => this.extensions;
            set => this.extensions = value;
        }

        public bool Visit(JxlVisitor visitor)
        {
            if (!visitor.U32(
                JxlFieldExpressions.Value(1),
                JxlFieldExpressions.Bits(2),
                JxlFieldExpressions.Bits(3),
                JxlFieldExpressions.Bits(4),
                1,
                ref this.oldSmall))
            {
                return false;
            }

            if (!visitor.F16(1.125f, ref this.oldF))
            {
                return false;
            }

            if (!visitor.U32(
                JxlFieldExpressions.Bits(7),
                JxlFieldExpressions.Bits(12),
                JxlFieldExpressions.Bits(16),
                JxlFieldExpressions.Bits(32),
                0,
                ref this.oldLarge))
            {
                return false;
            }

            if (!visitor.BeginExtensions(ref this.extensions))
            {
                return false;
            }

            return visitor.EndExtensions();
        }
    }

    private sealed class NewBundle : IJxlFields
    {
        private uint oldSmall;
        private float oldF;
        private uint oldLarge;
        private ulong extensions;
        private uint newSmall = 2;
        private float newF = -2.0f;
        private uint newLarge;

        public NewBundle() => JxlBundle.Init(this);

        public uint OldSmall
        {
            get => this.oldSmall;
            set => this.oldSmall = value;
        }

        public float OldF
        {
            get => this.oldF;
            set => this.oldF = value;
        }

        public uint OldLarge
        {
            get => this.oldLarge;
            set => this.oldLarge = value;
        }

        public ulong Extensions
        {
            get => this.extensions;
            set => this.extensions = value;
        }

        public uint NewSmall
        {
            get => this.newSmall;
            set => this.newSmall = value;
        }

        public float NewF
        {
            get => this.newF;
            set => this.newF = value;
        }

        public uint NewLarge
        {
            get => this.newLarge;
            set => this.newLarge = value;
        }

        public bool Visit(JxlVisitor visitor)
        {
            if (!visitor.U32(
                JxlFieldExpressions.Value(1),
                JxlFieldExpressions.Bits(2),
                JxlFieldExpressions.Bits(3),
                JxlFieldExpressions.Bits(4),
                1,
                ref this.oldSmall))
            {
                return false;
            }

            if (!visitor.F16(1.125f, ref this.oldF))
            {
                return false;
            }

            if (!visitor.U32(
                JxlFieldExpressions.Bits(7),
                JxlFieldExpressions.Bits(12),
                JxlFieldExpressions.Bits(16),
                JxlFieldExpressions.Bits(32),
                0,
                ref this.oldLarge))
            {
                return false;
            }

            if (!visitor.BeginExtensions(ref this.extensions))
            {
                return false;
            }

            if (visitor.Conditional((this.Extensions & 1) != 0))
            {
                if (!visitor.U32(
                    JxlFieldExpressions.Value(2),
                    JxlFieldExpressions.Bits(2),
                    JxlFieldExpressions.Bits(3),
                    JxlFieldExpressions.Bits(4),
                    2,
                    ref this.newSmall))
                {
                    return false;
                }

                if (!visitor.F16(-2.0f, ref this.newF))
                {
                    return false;
                }
            }

            if (visitor.Conditional((this.Extensions & 2) != 0))
            {
                if (!visitor.U32(
                    JxlFieldExpressions.Bits(9),
                    JxlFieldExpressions.Bits(12),
                    JxlFieldExpressions.Bits(16),
                    JxlFieldExpressions.Bits(32),
                    0,
                    ref this.newLarge))
                {
                    return false;
                }
            }

            return visitor.EndExtensions();
        }
    }

    private static void TestU32Coder(uint value, int expectedBitsWritten)
    {
        JxlU32Enc enc = new(
            JxlFieldExpressions.Value(0),
            JxlFieldExpressions.Bits(4),
            JxlFieldExpressions.Value(0x7FFFFFFF),
            JxlFieldExpressions.Bits(32));

        using MemoryStream memoryStream = new();
        JxlBitWriter bitWriter = new(memoryStream);

        Assert.True(bitWriter.WithMaxBits(
            (ulong)JxlMath.RoundUpBitsToByteMultiple(JxlU32Coder.MaxEncodedBits(enc)),
            () =>
            {
                int encodedBits = 0;
                Assert.True(JxlU32Coder.CanEncode(in enc, value, ref encodedBits));
                Assert.Equal(expectedBitsWritten, encodedBits);

                Assert.True(JxlU32Coder.Write(in enc, value, bitWriter));
                Assert.Equal(expectedBitsWritten, bitWriter.BitsWritten);

                bitWriter.ZeroPadToByte();

                return true;
            }));

        memoryStream.Position = 0;
        JxlBitReader reader = new(memoryStream);
        uint decodedValue = JxlU32Coder.Read(in enc, reader);

        Assert.Equal(value, decodedValue);
    }

    private static void TestU64Coder(ulong value, int expectedBitsWritten)
    {
        using MemoryStream memoryStream = new();
        JxlBitWriter bitWriter = new(memoryStream);

        Assert.True(bitWriter.WithMaxBits(
            (ulong)JxlMath.RoundUpBitsToByteMultiple(JxlU64Coder.MaxEncodedBits()),
            () =>
            {
                int encodedBits = 0;
                Assert.True(JxlU64Coder.CanEncode(value, ref encodedBits));
                Assert.Equal(expectedBitsWritten, encodedBits);

                Assert.True(JxlU64Coder.Write(value, bitWriter));
                Assert.Equal(expectedBitsWritten, bitWriter.BitsWritten);

                bitWriter.ZeroPadToByte();

                return true;
            }));

        memoryStream.Position = 0;
        JxlBitReader reader = new(memoryStream);
        ulong decodedValue = JxlU64Coder.Read(reader);

        Assert.Equal(value, decodedValue);
    }

    private static void TestF16Coder(float value)
    {
        int maxEncodedBits = 0;
        if (JxlF16Coder.CanEncode(value, ref maxEncodedBits))
        {
            throw new InvalidOperationException("This F16 value cannot be encoded: " + value);
        }

        Assert.Equal(JxlF16Coder.MaxEncodedBits(), maxEncodedBits);

        using MemoryStream memoryStream = new();
        JxlBitWriter bitWriter = new(memoryStream);

        Assert.True(bitWriter.WithMaxBits(
            (ulong)JxlMath.RoundUpBitsToByteMultiple(maxEncodedBits),
            () =>
            {
                Assert.True(JxlF16Coder.Write(value, bitWriter));
                Assert.Equal(JxlF16Coder.MaxEncodedBits(), bitWriter.BitsWritten);
                bitWriter.ZeroPadToByte();
                return true;
            }));

        memoryStream.Position = 0;
        JxlBitReader reader = new(memoryStream);

        float decodedValue = 0;
        Assert.True(JxlF16Coder.Read(reader, ref decodedValue));
        Assert.Equal(value, decodedValue);
    }

    [Fact]
    public void U32CoderTest()
    {
        TestU32Coder(0, 2);
        TestU32Coder(1, 6);
        TestU32Coder(15, 6);
        TestU32Coder(0x7FFFFFFF, 2);
        TestU32Coder(128, 34);
        TestU32Coder(0x7FFFFFFEu, 34);
        TestU32Coder(0x80000000u, 34);
        TestU32Coder(0xFFFFFFFFu, 34);
    }

    [Fact]
    public void U64CoderTest()
    {
        // Values that should take 2 bits (selector 00): 0
        TestU64Coder(0, 2);

        // Values that should take 6 bits (2 for selector, 4 for value): 1..16
        TestU64Coder(1, 6);
        TestU64Coder(2, 6);
        TestU64Coder(8, 6);
        TestU64Coder(15, 6);
        TestU64Coder(16, 6);

        // Values that should take 10 bits (2 for selector, 8 for value): 17..272
        TestU64Coder(17, 10);
        TestU64Coder(18, 10);
        TestU64Coder(100, 10);
        TestU64Coder(271, 10);
        TestU64Coder(272, 10);

        // Values that should take 15 bits (2 for selector, 12 for value, 1 for varint
        // end): (0)..273..4095
        TestU64Coder(273, 15);
        TestU64Coder(274, 15);
        TestU64Coder(1000, 15);
        TestU64Coder(4094, 15);
        TestU64Coder(4095, 15);

        // Take 24 bits (of which 20 actual value): (0)..4096..1048575
        TestU64Coder(4096, 24);
        TestU64Coder(4097, 24);
        TestU64Coder(10000, 24);
        TestU64Coder(1048574, 24);
        TestU64Coder(1048575, 24);

        // Take 33 bits (of which 28 actual value): (0)..1048576..268435455
        TestU64Coder(1048576, 33);
        TestU64Coder(1048577, 33);
        TestU64Coder(10000000, 33);
        TestU64Coder(268435454, 33);
        TestU64Coder(268435455, 33);

        // Take 42 bits (of which 36 actual value): (0)..268435456..68719476735
        TestU64Coder(268435456uL, 42);
        TestU64Coder(268435457uL, 42);
        TestU64Coder(1000000000uL, 42);
        TestU64Coder(68719476734uL, 42);
        TestU64Coder(68719476735uL, 42);

        // Take 51 bits (of which 44 actual value): (0)..68719476736..17592186044415
        TestU64Coder(68719476736uL, 51);
        TestU64Coder(68719476737uL, 51);
        TestU64Coder(1000000000000uL, 51);
        TestU64Coder(17592186044414uL, 51);
        TestU64Coder(17592186044415uL, 51);

        // Take 60 bits (of which 52 actual value):
        // (0)..17592186044416..4503599627370495
        TestU64Coder(17592186044416uL, 60);
        TestU64Coder(17592186044417uL, 60);
        TestU64Coder(100000000000000uL, 60);
        TestU64Coder(4503599627370494uL, 60);
        TestU64Coder(4503599627370495uL, 60);

        // Take 69 bits (of which 60 actual value):
        // (0)..4503599627370496..1152921504606846975
        TestU64Coder(4503599627370496uL, 69);
        TestU64Coder(4503599627370497uL, 69);
        TestU64Coder(10000000000000000uL, 69);
        TestU64Coder(1152921504606846974uL, 69);
        TestU64Coder(1152921504606846975uL, 69);

        // Take 73 bits (of which 64 actual value):
        // (0)..1152921504606846976..18446744073709551615
        TestU64Coder(1152921504606846976uL, 73);
        TestU64Coder(1152921504606846977uL, 73);
        TestU64Coder(10000000000000000000uL, 73);
        TestU64Coder(18446744073709551614uL, 73);
        TestU64Coder(18446744073709551615uL, 73);
    }

    [Fact]
    public void F16CoderTest()
    {
        ReadOnlySpan<float> signs = [-1.0f, 1.0f];
        ReadOnlySpan<float> magnitudes = [0.0f, 0.5f, 1.0f, 2.0f, 2.5f, 16.015625f, 1.0f / 4096, 1.0f / 16384, 65504.0f];

        foreach (float sign in signs)
        {
            foreach (float magnitude in magnitudes)
            {
                TestF16Coder(sign * magnitude);
            }
        }

        // These values are out of range and cannot be represented
        // by an F16 coder, so they're expected to throw.
        Assert.Throws<Exception>(() => TestF16Coder(65504.01f));
        Assert.Throws<Exception>(() => TestF16Coder(-65505.0f));
    }

    [Fact]
    public void TestRoundtripSize()
    {
        for (int i = 0; i < 8; i++)
        {
            JxlSizeHeader size = new();
            size.Set(123 + (77 * i), 7 + i); // It'll throw if it can't set

            int extensionBits = 999;
            long totalBits = 999;

            Assert.True(JxlBundle.CanEncode(size, ref extensionBits, ref totalBits));
            Assert.Equal(0, extensionBits);

            using MemoryStream ms = new();
            JxlBitWriter writer = new(ms);

            Assert.True(JxlBundle.WriteSizeHeader(size, writer, JxlLayerType.Header, new JxlAuxiliaryOutput()));
            Assert.Equal(totalBits, writer.BitsWritten);

            writer.ZeroPadToByte();

            JxlSizeHeader size2 = new();

            ms.Position = 0;
            JxlBitReader reader = new(ms);

            Assert.True(JxlBundle.Read(reader, size2));
            Assert.Equal(totalBits, reader.TotalBitsConsumed);

            Assert.Equal(size.XSize, size2.XSize);
            Assert.Equal(size.YSize, size2.YSize);
        }
    }

    [Fact]
    public void TestCropRect()
    {
        JxlCodecMetadata metadata = new();

        for (int i = -999; i < 19000; i++)
        {
            JxlFrameHeader f = new()
            {
                Metadata = metadata,
                CustomSizeOrOrigin = true,
                FrameOrigin = new Point(i),
                FrameSize = new(1000 + i)
            };

            int extensionBits = 0;
            long totalBits = 0;

            Assert.True(JxlBundle.CanEncode(f, ref extensionBits, ref totalBits));
            Assert.Equal(0, extensionBits);
            Assert.True(totalBits >= 9);
        }
    }

    [Fact]
    public void TestPreview()
    {
        for (int i = 1; i < 4360; i++)
        {
            JxlPreviewHeader p = new();
            p.Set(i, i); // Throws if it fails

            int extensionBits = 0;
            long totalBits = 0;

            Assert.True(JxlBundle.CanEncode(p, ref extensionBits, ref totalBits));
            Assert.Equal(0, extensionBits);
            Assert.True(totalBits >= 6);
        }
    }

    [Fact]
    public void TestRoundtripFrame()
    {
        JxlCodecMetadata metadata = new();

        JxlFrameHeader h = new()
        {
            Metadata = metadata,
            Extensions = 0x800
        };

        int extensionBits = 999;
        long totalBits = 999;

        Assert.True(JxlBundle.CanEncode(h, ref extensionBits, ref totalBits));
        Assert.Equal(0, extensionBits);

        using MemoryStream ms = new();
        JxlBitWriter writer = new(ms);

        Assert.True(JxlBundle.WriteFrameHeader(h, writer, null));
        Assert.Equal(totalBits, writer.BitsWritten);

        writer.ZeroPadToByte();

        JxlFrameHeader h2 = new()
        {
            Metadata = metadata
        };

        ms.Position = 0;
        JxlBitReader reader = new(ms);

        Assert.True(JxlBundle.Read(reader, h2));
        Assert.Equal(totalBits, reader.TotalBitsConsumed);

        Assert.Equal(h.Extensions, h2.Extensions);
        Assert.Equal(h.Flags, h2.Flags);
    }

    [Fact]
    public void TestOutOfRange()
    {
        JxlSizeHeader h = new();
        h.Set(unchecked((int)0xFFFFFFFF), unchecked((int)0xFFFFFFFF));

        int extensionBits = 999;
        long totalBits = 999;

        Assert.False(JxlBundle.CanEncode(h, ref extensionBits, ref totalBits));
    }

    [Fact]
    public void TestNewDecoderOldData()
    {
        OldBundle oldBundle = new()
        {
            OldLarge = 123,
            OldF = 3.75f,
            Extensions = 0
        };

        const int maxOutBytes = 999;

        using MemoryStream ms = new();
        JxlBitWriter writer = new(ms);

        int extensionBits = 12345;
        long totalBits = 12345;

        Assert.True(JxlBundle.CanEncode(oldBundle, ref extensionBits, ref totalBits));
        Assert.True(totalBits <= maxOutBytes * JxlMath.BitsPerByte);
        Assert.Equal(0, extensionBits);

        JxlAuxiliaryOutput auxOut = new();
        Assert.True(
            JxlBundle.Write(
                oldBundle,
                writer,
                JxlLayerType.Header,
                auxOut));

        Assert.True(writer.WithMaxBits(
            (ulong)((maxOutBytes * JxlMath.BitsPerByte) - totalBits),
            () =>
            {
                writer.Write(20, 0xA55A);
                writer.ZeroPadToByte();
                return true;
            }));

        ms.Position = 0;
        JxlBitReader reader = new(ms);
        NewBundle newBundle = new();

        Assert.True(JxlBundle.Read(reader, newBundle));

        Assert.Equal(
            reader.TotalBitsConsumed,
            auxOut.Layers[(int)JxlLayerType.Header].TotalBits);

        Assert.Equal(0xA55A, (int)reader.ReadBits32(20));

        // Old fields are the same in both.
        Assert.Equal(oldBundle.Extensions, newBundle.Extensions);
        Assert.Equal(oldBundle.OldSmall, newBundle.OldSmall);
        Assert.Equal(oldBundle.OldF, newBundle.OldF);
        Assert.Equal(oldBundle.OldLarge, newBundle.OldLarge);

        // New fields match their defaults.
        Assert.Equal(2u, newBundle.NewSmall);
        Assert.Equal(-2.0f, newBundle.NewF);
        Assert.Equal(0u, newBundle.NewLarge);
    }

    [Fact]
    public void TestOldDecoderNewData()
    {
        NewBundle newBundle = new()
        {
            OldLarge = 123,
            Extensions = 3,
            NewF = 999.0f,
            NewLarge = 456
        };

        const int maxOutBytes = 999;

        using MemoryStream ms = new();
        JxlBitWriter writer = new(ms);

        int extensionBits = 12345;
        long totalBits = 12345;

        Assert.True(JxlBundle.CanEncode(
            newBundle,
            ref extensionBits,
            ref totalBits));

        Assert.NotEqual(0, extensionBits);

        JxlAuxiliaryOutput auxOut = new();
        Assert.True(JxlBundle.Write(
            newBundle,
            writer,
            JxlLayerType.Header,
            auxOut));

        int headerBits = auxOut.Layers[(int)JxlLayerType.Header].TotalBits;

        Assert.True(headerBits <= maxOutBytes * JxlMath.BitsPerByte);

        Assert.True(writer.WithMaxBits(
            (ulong)((maxOutBytes * JxlMath.BitsPerByte) - headerBits),
            () =>
            {
                writer.Write(20, 0xA55A);
                writer.ZeroPadToByte();
                return true;
            }));

        ms.Position = 0;
        JxlBitReader reader = new(ms);
        OldBundle oldBundle = new();

        Assert.True(JxlBundle.Read(reader, oldBundle));

        Assert.Equal(
            reader.TotalBitsConsumed,
            auxOut.Layers[(int)JxlLayerType.Header].TotalBits);

        Assert.Equal(0xA55A, (int)reader.ReadBits32(20));

        // Old fields are the same in both.
        Assert.Equal(newBundle.Extensions, oldBundle.Extensions);
        Assert.Equal(newBundle.OldSmall, oldBundle.OldSmall);
        Assert.Equal(newBundle.OldF, oldBundle.OldF);
        Assert.Equal(newBundle.OldLarge, oldBundle.OldLarge);
    }
}
