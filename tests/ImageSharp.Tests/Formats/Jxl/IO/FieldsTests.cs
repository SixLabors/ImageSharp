// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using SixLabors.ImageSharp.Formats.Jxl.Cms;
using SixLabors.ImageSharp.Formats.Jxl.Fields;
using SixLabors.ImageSharp.Formats.Jxl.IO.Container;
using SixLabors.ImageSharp.Formats.Jxl.IO.FrameHeader;
using SixLabors.ImageSharp.Formats.Jxl.IO.Metadata;
using SixLabors.ImageSharp.Formats.Jxl.Processing;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.AuxiliaryOutput;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.Tests.ColorProfiles.Icc;

namespace SixLabors.ImageSharp.Tests.Formats.Jxl.IO;

public class FieldsTests
{
    /// <summary>
    /// Checks image dimensions against the size-header fields in libjxl headers.cc, including every built-in ratio.
    /// </summary>
    /// <param name="wire">The fixed, least-significant-bit-first header bytes.</param>
    /// <param name="width">The decoded image width in pixels.</param>
    /// <param name="height">The decoded image height in pixels.</param>
    /// <param name="bitCount">The header length before byte padding.</param>
    [Theory]
    [InlineData(new byte[] { 0x00, 0x10 }, 1, 1, 15)]
    [InlineData(new byte[] { 0x41, 0x00 }, 8, 8, 9)]
    [InlineData(new byte[] { 0x7F, 0x00 }, 256, 256, 9)]
    [InlineData(new byte[] { 0x05, 0x08 }, 40, 24, 14)]
    [InlineData(new byte[] { 0x80, 0x00, 0x18, 0x00 }, 13, 17, 26)]
    [InlineData(new byte[] { 0x40, 0x10 }, 9, 9, 15)]
    [InlineData(new byte[] { 0x40, 0x20 }, 10, 9, 15)]
    [InlineData(new byte[] { 0x40, 0x30 }, 12, 9, 15)]
    [InlineData(new byte[] { 0x40, 0x40 }, 13, 9, 15)]
    [InlineData(new byte[] { 0x40, 0x50 }, 16, 9, 15)]
    [InlineData(new byte[] { 0x40, 0x60 }, 11, 9, 15)]
    [InlineData(new byte[] { 0x40, 0x70 }, 18, 9, 15)]
    [InlineData(new byte[] { 0x02, 0x10, 0x01 }, 513, 513, 19)]
    [InlineData(new byte[] { 0x04, 0x00, 0x21 }, 8193, 8193, 24)]
    [InlineData(new byte[] { 0x06, 0x00, 0x20, 0x00, 0x02 }, 262145, 262145, 36)]
    [InlineData(new byte[] { 0xFE, 0xFF, 0xFF, 0xFF, 0x0B }, 1908874353, 1073741824, 36)]
    [InlineData(new byte[] { 0xF6, 0xFF, 0xFF, 0xFF, 0x0F }, 2147483646, 1073741823, 36)]
    public void SizeHeader_ReadAndWriteMatchesFixedWire(byte[] wire, int width, int height, int bitCount)
    {
        // Read independent wire bytes into a reused header so earlier size representations cannot determine the result.
        using MemoryStream input = new(wire, false);
        JxlBitReader reader = new(input);
        JxlSizeHeader size = new();
        size.Set(40, 24);

        Assert.True(JxlBundle.Read(reader, size));
        Assert.Equal((width, height), (size.XSize, size.YSize));
        Assert.Equal(bitCount, reader.TotalBitsConsumed);

        // Select the native compact form from dimensions, then compare its output with the same fixed wire bytes.
        size.Set(width, height);
        using MemoryStream output = new();
        JxlBitWriter writer = new(output);

        Assert.True(JxlBundle.Write(size, writer, JxlLayerType.Header, null));
        Assert.Equal(bitCount, writer.BitsWritten);

        writer.ZeroPadToByte();

        Assert.Equal(wire, output.ToArray());
    }

    /// <summary>
    /// Checks that initialization and explicit reset restore explicit one-by-one size fields.
    /// </summary>
    [Fact]
    public void SizeHeader_DefaultsRestoreOnePixelDimensions()
    {
        JxlSizeHeader size = new();

        Assert.Equal((1, 1), (size.XSize, size.YSize));
        Assert.True(JxlBundle.AllDefault(size));

        size.Set(40, 24);
        JxlBundle.SetDefault(size);

        Assert.Equal((1, 1), (size.XSize, size.YSize));
        Assert.True(JxlBundle.AllDefault(size));

        // Defaults use explicit dimensions, not the square ratio selected by Set. Both nine-bit values store dimension minus one.
        using MemoryStream output = new();
        JxlBitWriter writer = new(output);

        Assert.True(JxlBundle.Write(size, writer, JxlLayerType.Header, null));
        Assert.Equal(26, writer.BitsWritten);

        writer.ZeroPadToByte();

        Assert.Equal(new byte[] { 0, 0, 0, 0 }, output.ToArray());
    }

    /// <summary>
    /// Checks the derived width before it can overflow the Int32 image-dimension contract.
    /// </summary>
    [Fact]
    public void ReadSizeHeader_RejectsDerivedWidthOutsideImageRange()
    {
        // Height is 2^30 and ratio seven is 2:1. The encoded height fits, but its width is 2^31.
        using MemoryStream input = new([0xFE, 0xFF, 0xFF, 0xFF, 0x0F]);
        JxlBitReader reader = new(input);
        JxlSizeHeader size = new();

        Assert.False(JxlBundle.Read(reader, size));
    }

    /// <summary>
    /// Checks that the encoder boundary rejects dimensions that cannot describe an image.
    /// </summary>
    /// <param name="width">The supplied width.</param>
    /// <param name="height">The supplied height.</param>
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(-1, 1)]
    [InlineData(1, -1)]
    public void SizeHeader_SetRejectsNonpositiveDimensions(int width, int height)
    {
        JxlSizeHeader size = new();

        Assert.Throws<ArgumentException>(() => size.Set(width, height));
    }

    /// <summary>
    /// Checks box-header byte order, minimum sizes, and final unbounded boxes against fixed wire bytes.
    /// </summary>
    /// <param name="wire">The complete fixed header, with its type before any extended size.</param>
    /// <param name="expectedSize">The total size stored by the wire header.</param>
    /// <param name="extended">Whether the header has an explicit 64-bit size.</param>
    [Theory]
    [InlineData(new byte[] { 0, 0, 0, 8, 0x6A, 0x78, 0x6C, 0x63 }, 8UL, false)]
    [InlineData(new byte[] { 0, 0, 0, 0, 0x6A, 0x78, 0x6C, 0x63 }, 0UL, false)]
    [InlineData(new byte[] { 0, 0, 0, 1, 0x6A, 0x78, 0x6C, 0x63, 0, 0, 0, 0, 0, 0, 0, 16 }, 16UL, true)]
    [InlineData(new byte[] { 0, 0, 0, 1, 0x6A, 0x78, 0x6C, 0x63, 0, 0, 0, 1, 0, 0, 0, 16 }, 0x1_0000_0010UL, true)]
    [InlineData(new byte[] { 0, 0, 0, 1, 0x6A, 0x78, 0x6C, 0x63, 0, 0, 0, 0, 0, 0, 0, 0 }, 0UL, true)]
    [InlineData(new byte[] { 0, 0, 0, 1, 0x6A, 0x78, 0x6C, 0x63, 255, 255, 255, 255, 255, 255, 255, 255 }, ulong.MaxValue, true)]
    public void BoxHeader_ReadAndWriteMatchesFixedWire(byte[] wire, ulong expectedSize, bool extended)
    {
        // The container stores size32, the four bytes "jxlc", then size64 when size32 is one.
        using MemoryStream input = new(wire, false);
        JxlBoxHeader header = JxlBoxHeader.ReadHeader(input);

        Assert.Equal(expectedSize, header.Size);
        Assert.Equal(0x6A786C63U, header.Type);
        Assert.Equal(extended, header.ContainsLargeSize);
        Assert.Equal(expectedSize == 0, header.SizeExtendsTillEnd);

        // Compare with the independent wire bytes so paired reader/writer ordering errors cannot hide each other.
        using MemoryStream output = new();
        header.WriteHeader(output);

        Assert.Equal(wire, output.ToArray());
    }

    /// <summary>
    /// Checks that a bounded box cannot declare fewer bytes than its complete header.
    /// </summary>
    /// <param name="wire">A fixed 8-byte or 16-byte header with an undersized nonzero length.</param>
    [Theory]
    [InlineData(new byte[] { 0, 0, 0, 2, 0x6A, 0x78, 0x6C, 0x63 })]
    [InlineData(new byte[] { 0, 0, 0, 7, 0x6A, 0x78, 0x6C, 0x63 })]
    [InlineData(new byte[] { 0, 0, 0, 1, 0x6A, 0x78, 0x6C, 0x63, 0, 0, 0, 0, 0, 0, 0, 1 })]
    [InlineData(new byte[] { 0, 0, 0, 1, 0x6A, 0x78, 0x6C, 0x63, 0, 0, 0, 0, 0, 0, 0, 15 })]
    public void BoxHeader_RejectsSizeBelowHeader(byte[] wire)
    {
        using MemoryStream input = new(wire, false);

        Assert.Throws<InvalidOperationException>(() => JxlBoxHeader.ReadHeader(input));
    }

    /// <summary>
    /// Checks that the complete extended size is required after the box type.
    /// </summary>
    [Fact]
    public void BoxHeader_RejectsTruncatedExtendedSize()
    {
        using MemoryStream input = new([0, 0, 0, 1, 0x6A, 0x78, 0x6C, 0x63, 0, 0, 0, 0, 0, 0, 0]);

        Assert.Throws<EndOfStreamException>(() => JxlBoxHeader.ReadHeader(input));
    }

    /// <summary>
    /// Checks that the one-bit default shortcut replaces earlier serialized color fields.
    /// </summary>
    [Fact]
    public void ReadColorHeader_DefaultShortcutRestoresFields()
    {
        JxlColorEncoding encoding = new()
        {
            NeedsIcc = true,
            HaveFields = false,
            ColorSpace = JxlColorSpace.Unknown,
            WhitePoint = JxlWhitePoint.Custom,
            White = new() { X = 123, Y = -456 },
            Primaries = JxlPrimaries.Custom,
            Red = new() { X = 654, Y = -321 },
            RenderingIntent = IccRenderingIntent.AbsoluteColorimetric,
            TransferFunction = new() { HaveGamma = true, Gamma = 3333334 },
            IccProfile = TestIccProfiles.GetProfile(TestIccProfiles.Fogra39)
        };

        Assert.True(encoding.Cmyk);

        // A set first bit replaces the complete serialized color header. No ICC flag or color-space bits follow.
        using MemoryStream input = new([0x01]);
        JxlBitReader reader = new(input);

        Assert.True(JxlBundle.Read(reader, encoding));
        Assert.Equal(1, reader.TotalBitsConsumed);
        Assert.False(encoding.NeedsIcc);
        Assert.True(encoding.HaveFields);
        Assert.Null(encoding.IccProfile);
        Assert.False(encoding.Cmyk);
        Assert.Equal(JxlColorSpace.Rgb, encoding.ColorSpace);
        Assert.Equal(JxlWhitePoint.D65, encoding.WhitePoint);
        Assert.Equal(JxlPrimaries.SRgb, encoding.Primaries);
        Assert.Equal(IccRenderingIntent.MediaRelativeColorimetric, encoding.RenderingIntent);
        Assert.True(encoding.TransferFunction.IsSrgb);
        Assert.Equal(0, encoding.White.X);
        Assert.Equal(0, encoding.White.Y);
        Assert.Equal(0, encoding.Red.X);
        Assert.Equal(0, encoding.Red.Y);
        Assert.True(JxlBundle.AllDefault(encoding));
    }

    /// <summary>
    /// Checks that ICC-only headers retain their color space before the profile payload is available.
    /// </summary>
    [Theory]
    [InlineData(new byte[] { 0x06 }, 1, 4)]
    [InlineData(new byte[] { 0x1A }, 3, 8)]
    public void ReadColorHeader_IccFlagKeepsColorSpace(byte[] wire, int expectedColorSpace, int expectedBits)
    {
        // After all_default=0 and want_icc=1, only the color-space enum is present in this header.
        using MemoryStream input = new(wire);
        JxlBitReader reader = new(input);
        JxlColorEncoding encoding = new();

        Assert.True(JxlBundle.Read(reader, encoding));
        Assert.Equal(expectedBits, reader.TotalBitsConsumed);
        Assert.Equal((JxlColorSpace)expectedColorSpace, encoding.ColorSpace);
        Assert.True(encoding.NeedsIcc);
        Assert.False(encoding.HaveFields);
        Assert.Null(encoding.IccProfile);
        Assert.False(encoding.TryGetPrimaries(out _));
    }

    /// <summary>
    /// Checks custom white and primary coordinate order against fixed signed-integer wire data.
    /// </summary>
    [Fact]
    public void ReadColorHeader_CustomCoordinatesRetainWireOrder()
    {
        // RGB, custom white, custom primaries, sRGB transfer and absolute intent. Coordinates use millionths:
        // white=(312700,329000), red=(640000,330000), green=(300000,600000), blue=(150000,60000).
        using MemoryStream input = new(
            [0x20, 0x84, 0xAF, 0x98, 0xA0, 0x14, 0x24, 0x08, 0x80, 0x38, 0x81, 0x48, 0x28,
             0xE0, 0x93, 0x08, 0xF8, 0x24, 0x80, 0x4F, 0x12, 0x60, 0xEA, 0x70, 0x0D]);

        JxlBitReader reader = new(input);
        JxlColorEncoding encoding = new();

        Assert.True(JxlBundle.Read(reader, encoding));
        Assert.Equal(199, reader.TotalBitsConsumed);
        Assert.Equal(JxlWhitePoint.Custom, encoding.WhitePoint);
        Assert.Equal(JxlPrimaries.Custom, encoding.Primaries);
        Assert.Equal((312700, 329000), (encoding.White.X, encoding.White.Y));
        Assert.Equal((640000, 330000), (encoding.Red.X, encoding.Red.Y));
        Assert.Equal((300000, 600000), (encoding.Green.X, encoding.Green.Y));
        Assert.Equal((150000, 60000), (encoding.Blue.X, encoding.Blue.Y));
        Assert.True(encoding.TransferFunction.IsSrgb);
        Assert.Equal(IccRenderingIntent.AbsoluteColorimetric, encoding.RenderingIntent);
        Assert.False(encoding.NeedsIcc);
        Assert.True(encoding.HaveFields);
    }

    /// <summary>
    /// Checks exact gamma integers and both limits of the encoded OETF exponent.
    /// </summary>
    [Theory]
    [InlineData(new byte[] { 0x50, 0x89, 0x09, 0x00, 0x02 }, 1220U, false)]
    [InlineData(new byte[] { 0x50, 0x8B, 0x09, 0x00, 0x02 }, 1221U, true)]
    [InlineData(new byte[] { 0x50, 0xAD, 0xB9, 0x65, 0x02 }, 3333334U, true)]
    [InlineData(new byte[] { 0x50, 0x01, 0x2D, 0x31, 0x03 }, 10000000U, true)]
    [InlineData(new byte[] { 0x50, 0x03, 0x2D, 0x31, 0x03 }, 10000001U, false)]
    public void ReadColorHeader_CustomGammaRespectsIntegerBounds(byte[] wire, uint gamma, bool valid)
    {
        // Default RGB primaries and white, followed by have_gamma=1, the 24-bit integer and relative intent.
        // 3333334 must remain distinct from the implicit XYB exponent 3333333.
        using MemoryStream input = new(wire);
        JxlBitReader reader = new(input);
        JxlColorEncoding encoding = new();

        Assert.Equal(valid, JxlBundle.Read(reader, encoding));
        Assert.Equal(valid ? 35 : 33, reader.TotalBitsConsumed);

        if (valid)
        {
            Assert.True(encoding.TransferFunction.HaveGamma);
            Assert.Equal(gamma, encoding.TransferFunction.Gamma);
        }
    }

    /// <summary>
    /// Checks that XYB supplies its white point and transfer function without reading their fields.
    /// </summary>
    [Fact]
    public void ReadColorHeader_XybUsesImplicitFields()
    {
        // all_default=0, want_icc=0, XYB enum, relative intent. No white, primary or transfer bits are present.
        using MemoryStream input = new([0x08, 0x01]);
        JxlBitReader reader = new(input);
        JxlColorEncoding encoding = new();

        Assert.True(JxlBundle.Read(reader, encoding));
        Assert.Equal(10, reader.TotalBitsConsumed);
        Assert.Equal(JxlColorSpace.Xyb, encoding.ColorSpace);
        Assert.Equal(JxlWhitePoint.D65, encoding.WhitePoint);
        Assert.True(encoding.TransferFunction.HaveGamma);
        Assert.Equal(3333333U, encoding.TransferFunction.Gamma);
        Assert.False(encoding.HasPrimaries);
        Assert.True(encoding.HaveFields);
    }

    /// <summary>
    /// Checks that grayscale headers read white point and transfer fields without primary fields.
    /// </summary>
    [Fact]
    public void ReadColorHeader_GrayOmitsPrimaries()
    {
        // all_default=0, want_icc=0, Gray, D65, have_gamma=0, BT.709 transfer, relative intent.
        using MemoryStream input = new([0x94, 0x02]);
        JxlBitReader reader = new(input);
        JxlColorEncoding encoding = new();

        Assert.True(JxlBundle.Read(reader, encoding));
        Assert.Equal(11, reader.TotalBitsConsumed);
        Assert.Equal(JxlColorSpace.Gray, encoding.ColorSpace);
        Assert.Equal(JxlWhitePoint.D65, encoding.WhitePoint);
        Assert.False(encoding.HasPrimaries);
        Assert.True(encoding.TransferFunction.Is709);
        Assert.True(encoding.HaveFields);
    }

    /// <summary>
    /// Checks that a defined Unknown transfer code cannot describe colors without an ICC payload.
    /// </summary>
    [Fact]
    public void ReadColorHeader_UnknownTransferRequiresIcc()
    {
        // Explicit RGB/D65/sRGB primaries, Unknown transfer, relative intent, with want_icc=0.
        using MemoryStream input = new([0x50, 0x84, 0x00]);
        JxlBitReader reader = new(input);

        Assert.False(JxlBundle.Read(reader, new JxlColorEncoding()));
        Assert.Equal(17, reader.TotalBitsConsumed);
    }

    /// <summary>
    /// Checks both ends of the signed custom-coordinate encoding without floating-point rounding.
    /// </summary>
    [Fact]
    public void ReadCustomCoordinates_SupportSignedWireExtremes()
    {
        // Each selector is 3. The 21-bit payloads restore packed values 4194303 and 4194302.
        using MemoryStream input = new([0xFF, 0xFF, 0xFF, 0xFD, 0xFF, 0x3F]);
        JxlBitReader reader = new(input);
        JxlCustomXy coordinates = new();

        Assert.True(coordinates.Visit(new JxlReadVisitor(reader)));
        Assert.Equal(46, reader.TotalBitsConsumed);
        Assert.Equal(-2097152, coordinates.X);
        Assert.Equal(2097151, coordinates.Y);
    }

    /// <summary>
    /// Checks byte and UInt32 enum fields against fixed LSB-first wire data.
    /// </summary>
    [Fact]
    public void ReadEnums_PreservesTheirStorageWidths()
    {
        // Six bits encode primaries 9, followed by six bits for intent 3. Both use selector 2.
        using MemoryStream input = new([0x9E, 0x01]);
        JxlBitReader reader = new(input);
        JxlReadVisitor visitor = new(reader);
        JxlPrimaries primaries = JxlPrimaries.SRgb;
        IccRenderingIntent intent = IccRenderingIntent.Perceptual;

        Assert.True(visitor.Enum(JxlPrimaries.SRgb, ref primaries));
        Assert.True(visitor.Enum(IccRenderingIntent.MediaRelativeColorimetric, ref intent));
        Assert.Equal(JxlPrimaries.Bt2020, primaries);
        Assert.Equal(IccRenderingIntent.AbsoluteColorimetric, intent);
        Assert.Equal(12, reader.TotalBitsConsumed);
    }

    /// <summary>
    /// Checks that an undefined enum value cannot replace the previous field value.
    /// </summary>
    [Fact]
    public void ReadEnum_RejectsUndefinedValue()
    {
        // Selector 3 followed by six zero bits encodes value 18, which is not a primary code point.
        using MemoryStream input = new([0x03]);
        JxlReadVisitor visitor = new(new JxlBitReader(input));
        JxlPrimaries primaries = JxlPrimaries.SRgb;

        Assert.False(visitor.Enum(JxlPrimaries.SRgb, ref primaries));
        Assert.Equal(JxlPrimaries.SRgb, primaries);
    }

    /// <summary>
    /// Checks initialization, accumulated default comparisons, and explicit default restoration.
    /// </summary>
    [Fact]
    public void BundleDefaults_RetainEarlierNonDefaultFields()
    {
        OldBundle bundle = new();

        Assert.Equal(1U, bundle.OldSmall);
        Assert.True(JxlBundle.AllDefault(bundle));

        bundle.OldSmall = 2;

        Assert.False(JxlBundle.AllDefault(bundle));
        Assert.Equal(2U, bundle.OldSmall);

        JxlBundle.SetDefault(bundle);

        Assert.Equal(1U, bundle.OldSmall);
        Assert.True(JxlBundle.AllDefault(bundle));

        // Constructed children keep their own values when the parent's fields are initialized.
        JxlImageMetadata metadata = new();
        metadata.ToneMapping.IntensityTarget = 123F;
        JxlBundle.Init(metadata);
        Assert.Equal(123F, metadata.ToneMapping.IntensityTarget);

        // An explicit default reset must also restore nested fields.
        JxlBundle.SetDefault(metadata);
        Assert.Equal(JxlMath.DefaultIntensityTarget, metadata.ToneMapping.IntensityTarget);
    }

    /// <summary>
    /// Checks finite half fields and the wire rejection of nonfinite encodings.
    /// </summary>
    /// <param name="bits">The stored half-precision field.</param>
    /// <param name="expectedBits">The exact single-precision result, or the unchanged initial value after rejection.</param>
    /// <param name="valid">Whether the wire encoding is valid.</param>
    [Theory]
    [InlineData((ushort)0x0000, 0x00000000U, true)]
    [InlineData((ushort)0x8000, 0x80000000U, true)]
    [InlineData((ushort)0x0001, 0x33800000U, true)]
    [InlineData((ushort)0x0400, 0x38800000U, true)]
    [InlineData((ushort)0x7BFF, 0x477FE000U, true)]
    [InlineData((ushort)0x7C00, 0x3F900000U, false)]
    [InlineData((ushort)0xFC00, 0x3F900000U, false)]
    [InlineData((ushort)0x7E00, 0x3F900000U, false)]
    public void ReadF16_PreservesFiniteBitsAndRejectsNonfinite(ushort bits, uint expectedBits, bool valid)
    {
        // JXL fields use least-significant-bit-first bytes. The initial 1.125F must survive an invalid encoding.
        using MemoryStream input = new([(byte)bits, (byte)(bits >> 8)]);
        JxlBitReader reader = new(input);
        float value = 1.125F;

        Assert.Equal(valid, JxlF16Coder.Read(reader, ref value));
        Assert.Equal(expectedBits, BitConverter.SingleToUInt32Bits(value));
    }

    /// <summary>
    /// Checks nearest rounding and ties-to-even against fixed single- and half-precision bit patterns.
    /// </summary>
    /// <param name="sourceBits">The exact single-precision input.</param>
    /// <param name="expectedBits">The nearest half-precision encoding.</param>
    [Theory]
    [InlineData(0x3F801000U, (ushort)0x3C00)]
    [InlineData(0x3F803000U, (ushort)0x3C02)]
    [InlineData(0x3F801001U, (ushort)0x3C01)]
    [InlineData(0xBF801001U, (ushort)0xBC01)]
    [InlineData(0x33000001U, (ushort)0x0001)]
    [InlineData(0x80000000U, (ushort)0x8000)]
    [InlineData(0x477FE000U, (ushort)0x7BFF)]
    public void WriteF16_RoundsToNearestHalf(uint sourceBits, ushort expectedBits)
    {
        // Around 1, halfway mantissas select an even low bit. Just past a midpoint must round toward the nearer value.
        using MemoryStream output = new();
        JxlBitWriter writer = new(output);

        Assert.True(JxlF16Coder.Write(BitConverter.UInt32BitsToSingle(sourceBits), writer));
        Assert.Equal(16, writer.BitsWritten);
        Assert.Equal(expectedBits, BinaryPrimitives.ReadUInt16LittleEndian(output.GetBuffer()));
    }

    /// <summary>
    /// Checks that invalid half fields are rejected before any output is written.
    /// </summary>
    /// <param name="value">A nonfinite value or a finite value outside the half-precision range.</param>
    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    [InlineData(65505F)]
    [InlineData(-65505F)]
    public void WriteF16_RejectsValuesOutsideFiniteRange(float value)
    {
        using MemoryStream output = new();
        JxlBitWriter writer = new(output);

        Assert.False(JxlF16Coder.Write(value, writer));
        Assert.Equal(0, writer.BitsWritten);
    }

    /// <summary>
    /// Checks that probing an incomplete header reports failure without accepting default field status.
    /// </summary>
    [Fact]
    public void CanRead_RejectsIncompleteHeader()
    {
        using MemoryStream input = new();
        JxlBitReader reader = new(input);

        Assert.False(JxlBundle.CanRead(reader, new OldBundle()));
    }

    /// <summary>
    /// Checks that unknown extension data is skipped using its full declared bit length.
    /// </summary>
    [Fact]
    public void ReadExtensions_SkipsRemainingPayload()
    {
        // U64 flag value 1 uses six bits. U64 payload length 5 uses six more bits.
        // Five payload bits, 10101, follow. These bytes are fixed LSB-first field data.
        using MemoryStream input = new([0x41, 0x54, 0x01]);
        JxlBitReader reader = new(input);
        JxlReadVisitor visitor = new(reader);
        ulong extensions = 0;

        Assert.True(visitor.BeginExtensions(ref extensions));
        Assert.Equal(1UL, extensions);
        Assert.Equal(12, reader.TotalBitsConsumed);
        Assert.Equal(1U, reader.ReadBits32(2));
        Assert.True(visitor.EndExtensions());
        Assert.Equal(17, reader.TotalBitsConsumed);
    }

    /// <summary>
    /// Checks that extension lengths cannot wrap their accumulated UInt64 bit budget.
    /// </summary>
    [Fact]
    public void ReadExtensions_RejectsAccumulatedLengthOverflow()
    {
        // Flag value 3 selects two extensions. Their lengths are UInt64.MaxValue and one.
        // A wrapped sum of zero would incorrectly accept the header without skipping its payload.
        using MemoryStream input = new([0xC9, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00]);
        JxlReadVisitor visitor = new(new JxlBitReader(input));
        ulong extensions = 0;

        Assert.False(visitor.BeginExtensions(ref extensions));
        Assert.Equal(3UL, extensions);
    }

    /// <summary>
    /// Checks that extension ends fit the reader's signed bit-position range.
    /// </summary>
    [Fact]
    public void ReadExtensions_RejectsUnrepresentableEnd()
    {
        // One extension declares UInt64.MaxValue payload bits. Its length fits UInt64,
        // but adding the already-consumed header cannot produce an Int64 end position.
        using MemoryStream input = new([0xC1, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x7F]);
        JxlReadVisitor visitor = new(new JxlBitReader(input));
        ulong extensions = 0;

        Assert.True(visitor.BeginExtensions(ref extensions));
        Assert.False(visitor.EndExtensions());
    }

    /// <summary>
    /// Checks full-width raw fields against explicit least-significant-bit-first wire bytes.
    /// </summary>
    [Theory]
    [InlineData(32, 0U, new byte[] { 0x00, 0x00, 0x00, 0x00 })]
    [InlineData(32, uint.MaxValue, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF })]
    [InlineData(31, 0x7FFFFFFFU, new byte[] { 0xFF, 0xFF, 0xFF, 0x7F })]
    public void WriteRawBits_PreservesFullWidthValues(int bits, uint value, byte[] expected)
    {
        using MemoryStream output = new();
        JxlBitWriter writer = new(output);
        int encodedBits = 0;

        Assert.True(JxlBitsCoder.CanEncode(bits, value, ref encodedBits));
        Assert.Equal(bits, encodedBits);
        Assert.True(JxlBitsCoder.Write(bits, value, writer));
        Assert.Equal(bits, writer.BitsWritten);

        // Flush completes the final byte without adding another field bit. A 31-bit value leaves its last high bit clear.
        writer.Flush();
        Assert.Equal(expected, output.ToArray());
    }

    /// <summary>
    /// Checks that invalid raw values fail before the writer emits a partial field.
    /// </summary>
    [Theory]
    [InlineData(0, 1U)]
    [InlineData(31, uint.MaxValue)]
    public void WriteRawBits_RejectsValuesOutsideWidth(int bits, uint value)
    {
        using MemoryStream output = new();
        JxlBitWriter writer = new(output);
        int encodedBits = 0;

        Assert.False(JxlBitsCoder.CanEncode(bits, value, ref encodedBits));
        Assert.False(JxlBitsCoder.Write(bits, value, writer));
        Assert.Equal(0, writer.BitsWritten);
        Assert.Equal(0, output.Length);
    }

    /// <summary>
    /// Checks nested extension lengths, scope restoration, statistics and bit alignment against one fixed wire vector.
    /// </summary>
    [Fact]
    public void WriteNestedExtensions_PreservesEachBundleBitBudget()
    {
        NestedExtensionBundle inner = new(new OldBundle(), 3);
        NestedExtensionBundle outer = new(inner, 1);
        int extensionBits = 0;
        long totalBits = 0;

        // The default OldBundle uses 2 + 16 + 9 + 2 = 29 bits.
        // Inner flags 3 need 6 flag bits and lengths of 10 + 2 bits: its complete length is 47 bits.
        // Outer flags 1 need 6 flag bits and a 10-bit length: its payload is 47 bits and its complete length is 63 bits.
        Assert.True(JxlBundle.CanEncode(outer, ref extensionBits, ref totalBits));
        Assert.Equal(47, extensionBits);
        Assert.Equal(63, totalBits);

        using MemoryStream output = new();
        JxlBitWriter writer = new(output);
        JxlAuxiliaryOutput statistics = new();

        Assert.True(JxlBundle.Write(outer, writer, JxlLayerType.Header, statistics));
        Assert.Equal(63, writer.BitsWritten);
        Assert.Equal(63, statistics.Layers[(int)JxlLayerType.Header].TotalBits);

        // The following one-bit marker completes byte eight. Extension payloads have no intervening byte padding.
        writer.Write(1, 1);
        writer.Flush();
        Assert.Equal(new byte[] { 0x81, 0x1E, 0x89, 0x0C, 0x00, 0xC8, 0x03, 0x80 }, output.ToArray());

        output.Position = 0;
        JxlBitReader reader = new(output);
        OldBundle decodedFields = new() { OldSmall = 7, OldF = -7F, OldLarge = 777 };
        NestedExtensionBundle decodedInner = new(decodedFields, 0);
        NestedExtensionBundle decodedOuter = new(decodedInner, 0);

        Assert.True(JxlBundle.Read(reader, decodedOuter));
        Assert.Equal(63, reader.TotalBitsConsumed);
        Assert.Equal(1UL, decodedOuter.Extensions);
        Assert.Equal(3UL, decodedInner.Extensions);
        Assert.Equal(1U, decodedFields.OldSmall);
        Assert.Equal(1.125F, decodedFields.OldF);
        Assert.Equal(0U, decodedFields.OldLarge);
        Assert.Equal(1U, reader.ReadBits32(1));
    }

    /// <summary>
    /// Carries a nested bundle inside the first extension payload.
    /// </summary>
    private sealed class NestedExtensionBundle : IJxlFields
    {
        private readonly IJxlFields nested;
        private ulong extensions;

        /// <summary>
        /// Initializes a new instance of the <see cref="NestedExtensionBundle"/> class.
        /// </summary>
        /// <param name="nested">The nested fields present when extension bit zero is set.</param>
        /// <param name="extensions">The initial extension flags, overwritten when reading.</param>
        public NestedExtensionBundle(IJxlFields nested, ulong extensions)
        {
            this.nested = nested;
            this.extensions = extensions;
        }

        /// <summary>
        /// Gets the extension flags read or written for this bundle.
        /// </summary>
        public ulong Extensions => this.extensions;

        /// <inheritdoc />
        public bool Visit(JxlVisitor visitor)
        {
            if (!visitor.BeginExtensions(ref this.extensions))
            {
                return false;
            }

            if (visitor.Conditional((this.extensions & 1) != 0) && !visitor.VisitNested(this.nested))
            {
                return false;
            }

            return visitor.EndExtensions();
        }
    }

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
        if (!JxlF16Coder.CanEncode(value, ref maxEncodedBits))
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
        Assert.Throws<InvalidOperationException>(() => TestF16Coder(65504.01f));
        Assert.Throws<InvalidOperationException>(() => TestF16Coder(-65505.0f));
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

    /// <summary>
    /// Checks the sample-type flag, bit-depth alternatives and exponent offset against fixed native header fields.
    /// </summary>
    /// <param name="wire">The fixed bit-depth header bytes.</param>
    /// <param name="floatingPoint">Whether samples use floating-point storage.</param>
    /// <param name="sampleBits">The complete sample width in bits.</param>
    /// <param name="exponentBits">The exponent width, or zero for integer samples.</param>
    /// <param name="bitCount">The header length before byte padding.</param>
    [Theory]
    [InlineData(new byte[] { 0x00 }, false, 8U, 0U, 3)]
    [InlineData(new byte[] { 0x02 }, false, 10U, 0U, 3)]
    [InlineData(new byte[] { 0x04 }, false, 12U, 0U, 3)]
    [InlineData(new byte[] { 0x06, 0x00 }, false, 1U, 0U, 9)]
    [InlineData(new byte[] { 0xF6, 0x00 }, false, 31U, 0U, 9)]
    [InlineData(new byte[] { 0x39 }, true, 32U, 8U, 7)]
    [InlineData(new byte[] { 0x23 }, true, 16U, 5U, 7)]
    [InlineData(new byte[] { 0x35 }, true, 24U, 7U, 7)]
    [InlineData(new byte[] { 0x27, 0x02 }, true, 5U, 2U, 13)]
    public void BitDepthHeader_ReadAndWriteMatchesFixedWire(byte[] wire, bool floatingPoint, uint sampleBits, uint exponentBits, int bitCount)
    {
        // The leading Boolean selects the U32 alternatives. Integer input must also clear a previous floating-point exponent width.
        JxlBitDepthMetadata depth = new() { FloatingPointSample = true, BitsPerSample = 16, ExponentBitsPerSample = 5 };
        using MemoryStream input = new(wire, false);
        JxlBitReader reader = new(input);

        Assert.True(JxlBundle.Read(reader, depth));
        Assert.Equal((floatingPoint, sampleBits, exponentBits), (depth.FloatingPointSample, depth.BitsPerSample, depth.ExponentBitsPerSample));
        Assert.Equal(bitCount, reader.TotalBitsConsumed);

        using MemoryStream output = new();
        JxlBitWriter writer = new(output);

        Assert.True(JxlBundle.Write(depth, writer, JxlLayerType.Header, null));
        Assert.Equal(bitCount, writer.BitsWritten);

        writer.ZeroPadToByte();

        Assert.Equal(wire, output.ToArray());
    }

    /// <summary>
    /// Checks invalid integer width, exponent width and mantissa width at the bit-depth parser boundary.
    /// </summary>
    /// <param name="wire">The fixed header bytes containing an invalid sample representation.</param>
    [Theory]
    [InlineData(new byte[] { 0xFE, 0x00 })]
    [InlineData(new byte[] { 0x41 })]
    [InlineData(new byte[] { 0x1F, 0x02 })]
    [InlineData(new byte[] { 0x07, 0x0F })]
    public void ReadBitDepthHeader_RejectsInvalidSampleRepresentation(byte[] wire)
    {
        // These encode integer32, float32/exponent9, float4/exponent2 and float33/exponent8, respectively.
        // The last two leave one or 24 mantissa bits; native JPEG XL requires two through 23.
        using MemoryStream input = new(wire, false);
        JxlBitReader reader = new(input);

        Assert.False(JxlBundle.Read(reader, new JxlBitDepthMetadata()));
    }

    /// <summary>
    /// Checks the preview-specific dimension distributions against fixed native header fields.
    /// </summary>
    /// <param name="wire">The fixed preview header bytes.</param>
    /// <param name="width">The preview width in pixels.</param>
    /// <param name="height">The preview height in pixels.</param>
    /// <param name="bitCount">The header length before byte padding.</param>
    [Theory]
    [InlineData(new byte[] { 0x00, 0x02 }, 1, 1, 12)]
    [InlineData(new byte[] { 0x09 }, 128, 128, 6)]
    [InlineData(new byte[] { 0x0B }, 256, 256, 6)]
    [InlineData(new byte[] { 0x0D, 0x04 }, 24, 16, 11)]
    [InlineData(new byte[] { 0x02, 0x08 }, 65, 65, 14)]
    [InlineData(new byte[] { 0x04, 0x20 }, 321, 321, 16)]
    [InlineData(new byte[] { 0x06, 0x80, 0x00 }, 1345, 1345, 18)]
    [InlineData(new byte[] { 0xFF, 0x1F }, 4352, 4352, 15)]
    [InlineData(new byte[] { 0xFE, 0x7F, 0xEC, 0xFF }, 5439, 5440, 32)]
    public void PreviewHeader_ReadAndWriteMatchesFixedWire(byte[] wire, int width, int height, int bitCount)
    {
        // Compact previews store units of eight pixels; other previews use their own ranges up to 5440 pixels.
        // The largest case has an odd width so Set retains the non-divided form instead of the smaller compact distribution.
        JxlPreviewHeader preview = new();
        preview.Set(24, 16);
        using MemoryStream input = new(wire, false);
        JxlBitReader reader = new(input);

        Assert.True(JxlBundle.Read(reader, preview));
        Assert.Equal((width, height), (preview.XSize, preview.YSize));
        Assert.Equal(bitCount, reader.TotalBitsConsumed);

        preview.Set(width, height);
        using MemoryStream output = new();
        JxlBitWriter writer = new(output);

        Assert.True(JxlBundle.Write(preview, writer, JxlLayerType.Header, null));
        Assert.Equal(bitCount, writer.BitsWritten);

        writer.ZeroPadToByte();

        Assert.Equal(wire, output.ToArray());
    }

    /// <summary>
    /// Checks animation timing, repetition and timecode fields against fixed native header fields.
    /// </summary>
    /// <param name="wire">The fixed animation header bytes.</param>
    /// <param name="numerator">The ticks-per-second numerator.</param>
    /// <param name="denominator">The ticks-per-second denominator.</param>
    /// <param name="loops">The repetition count, or zero for unlimited repetition.</param>
    /// <param name="timecodes">Whether frames include timecodes.</param>
    /// <param name="bitCount">The header length before byte padding.</param>
    [Theory]
    [InlineData(new byte[] { 0x02, 0x00, 0x00 }, 1U, 1U, 0U, false, 17)]
    [InlineData(new byte[] { 0x50, 0x00 }, 100U, 1U, 1U, false, 10)]
    [InlineData(new byte[] { 0x45 }, 1000U, 1001U, 0U, true, 7)]
    [InlineData(new byte[] { 0x5E, 0x80, 0xFF, 0xFF, 0x00 }, 24U, 1U, 65535U, false, 33)]
    [InlineData(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x7F }, 1073741824U, 1024U, uint.MaxValue, true, 79)]
    public void AnimationHeader_ReadAndWriteMatchesFixedWire(byte[] wire, uint numerator, uint denominator, uint loops, bool timecodes, int bitCount)
    {
        // The final case uses all payload bits of the longest alternatives. In particular, the loop count must remain unsigned.
        using MemoryStream input = new(wire, false);
        JxlBitReader reader = new(input);
        JxlAnimationHeader animation = new();

        Assert.True(JxlBundle.Read(reader, animation));
        Assert.Equal(
            (numerator, denominator, loops, timecodes),
            (animation.TpsNumerator, animation.TpsDenominator, animation.LoopCount, animation.ContainsTimecodes));

        Assert.Equal(bitCount, reader.TotalBitsConsumed);

        using MemoryStream output = new();
        JxlBitWriter writer = new(output);

        Assert.True(JxlBundle.Write(animation, writer, JxlLayerType.Header, null));
        Assert.Equal(bitCount, writer.BitsWritten);

        writer.ZeroPadToByte();

        Assert.Equal(wire, output.ToArray());
    }

    /// <summary>
    /// Checks that each header initializes its defaults and restores them after reuse.
    /// </summary>
    [Fact]
    public void MetadataHeaders_DefaultsRestoreAfterReuse()
    {
        JxlBitDepthMetadata depth = new();
        JxlPreviewHeader preview = new();
        JxlAnimationHeader animation = new();

        Assert.Equal((false, 8U, 0U), (depth.FloatingPointSample, depth.BitsPerSample, depth.ExponentBitsPerSample));
        Assert.Equal((1, 1), (preview.XSize, preview.YSize));
        Assert.Equal((1U, 1U, 0U, false), (animation.TpsNumerator, animation.TpsDenominator, animation.LoopCount, animation.ContainsTimecodes));

        depth.FloatingPointSample = true;
        depth.BitsPerSample = 16;
        depth.ExponentBitsPerSample = 5;
        preview.Set(256, 256);
        animation.TpsNumerator = 1000;
        animation.TpsDenominator = 1001;
        animation.LoopCount = uint.MaxValue;
        animation.ContainsTimecodes = true;

        JxlBundle.SetDefault(depth);
        JxlBundle.SetDefault(preview);
        JxlBundle.SetDefault(animation);

        Assert.Equal((false, 8U, 0U), (depth.FloatingPointSample, depth.BitsPerSample, depth.ExponentBitsPerSample));
        Assert.Equal((1, 1), (preview.XSize, preview.YSize));
        Assert.Equal((1U, 1U, 0U, false), (animation.TpsNumerator, animation.TpsDenominator, animation.LoopCount, animation.ContainsTimecodes));
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
        // Positive Int32 dimensions are valid inputs to Set, but exceed the largest 30-bit wire representation.
        h.Set(int.MaxValue, int.MaxValue);

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
