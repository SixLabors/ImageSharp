// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Exr;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.Formats.Exr;

/// <summary>
/// Security regression tests for the EXR decoder (Findings EXR-1, EXR-2, EXR-3).
/// The EXR decoder was merged to main but not yet included in a tagged NuGet release.
/// Each test demonstrates a crafted-input crash present in the unfixed code.
/// </summary>
[Trait("Format", "Exr")]
[ValidateDisposedMemoryAllocations]
public class ExrDecoderSecurityTests
{
    /// <summary>
    /// EXR-1 — EXR DataWindow Integer Overflow Produces Negative Image Dimensions (DoS)
    ///
    /// Width and Height are computed from attacker-controlled DataWindow attributes
    /// using unchecked int subtraction:
    ///   this.Width = XMax - XMin + 1  // overflows to -2147483647
    ///
    /// The negative Width is then passed to the Image&lt;TPixel&gt; constructor, which calls
    /// Guard.MustBeGreaterThan(width, 0) → ArgumentOutOfRangeException.
    ///
    /// After a fix this should throw InvalidImageContentException instead.
    ///
    /// Affected file:
    ///   src/ImageSharp/Formats/Exr/ExrDecoderCore.cs lines 600–601
    /// </summary>
    [Fact]
    public void Decode_DataWindowOverflow_NegativeWidth_Throws()
    {
        // XMin = -1073741825, XMax = 1073741823
        // Width = 1073741823 - (-1073741825) + 1 = 2^31 + 1 → wraps to -2147483647
        byte[] data = BuildMinimalExr(xMin: -1073741825, yMin: 0, xMax: 1073741823, yMax: 0);

        using var stream = new MemoryStream(data);
        Assert.Throws<InvalidImageContentException>(
            () => ExrDecoder.Instance.Decode<Rgba32>(DecoderOptions.Default, stream));
    }

    /// <summary>
    /// EXR-2 — EXR Row Offset Table Unvalidated Seek (DoS)
    ///
    /// Row offsets are read from the file and used unconditionally to seek the stream:
    ///   ulong rowOffset = this.ReadUnsignedLong(stream);
    ///   stream.Position = (long)rowOffset;   // no bounds check
    ///
    /// A crafted offset of 0xFFFFFFFFFFFFFFFF casts to −1 as long, causing
    /// an ArgumentOutOfRangeException when setting stream.Position.
    ///
    /// After a fix this should throw InvalidImageContentException instead.
    ///
    /// Affected file:
    ///   src/ImageSharp/Formats/Exr/ExrDecoderCore.cs lines 170–175 (scanline)
    ///                                                   lines 243–248 (tile)
    /// </summary>
    [Fact]
    public void Decode_CraftedRowOffsets_OutOfBounds_Throws()
    {
        // Valid 2×2 image (XMin=0,YMin=0,XMax=1,YMax=1 → Width=2,Height=2).
        // Row offset table immediately follows the header null byte:
        //   2 rows × 8 bytes each, all set to 0xFFFFFFFFFFFFFFFF.
        byte[] invalidOffsets = new byte[16];
        BinaryPrimitives.WriteUInt64LittleEndian(invalidOffsets, 0xFFFFFFFFFFFFFFFF);
        BinaryPrimitives.WriteUInt64LittleEndian(invalidOffsets.AsSpan(8), 0xFFFFFFFFFFFFFFFF);

        byte[] data = BuildMinimalExr(
            xMin: 0, yMin: 0, xMax: 1, yMax: 1,
            rowOffsetTableAppend: invalidOffsets);

        using var stream = new MemoryStream(data);
        Assert.Throws<InvalidImageContentException>(
            () => ExrDecoder.Instance.Decode<Rgba32>(DecoderOptions.Default, stream));
    }

    [Fact]
    public void Decode_CraftedRowOffsets_IntoHeader_Throws()
    {
        // Offset 0 points back into the EXR file header and must be rejected
        // before the decoder seeks to attacker-controlled non-pixel data.
        byte[] headerOffsets = new byte[16];

        byte[] data = BuildMinimalExr(
            xMin: 0, yMin: 0, xMax: 1, yMax: 1,
            rowOffsetTableAppend: headerOffsets);

        using var stream = new MemoryStream(data);
        Assert.Throws<InvalidImageContentException>(
            () => ExrDecoder.Instance.Decode<Rgba32>(DecoderOptions.Default, stream));
    }

    [Fact]
    public void Decode_CraftedRowOffsets_IntoOffsetTable_Throws()
    {
        byte[] data = BuildMinimalExr(
            xMin: 0, yMin: 0, xMax: 1, yMax: 1,
            rowOffsetTableAppend: new byte[16]);

        // Point the first row offset at the second row offset entry.
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(data.Length - 16), (ulong)(data.Length - 8));

        using var stream = new MemoryStream(data);
        Assert.Throws<InvalidImageContentException>(
            () => ExrDecoder.Instance.Decode<Rgba32>(DecoderOptions.Default, stream));
    }

    /// <summary>
    /// EXR-3 — Oversized EXR RGBA row sizing is rejected as invalid image content.
    ///
    /// With 4 RGBA HALF channels and Width = 2^29, the decoded row staging and
    /// bytes-per-row arithmetic both exceed the supported buffer sizing limits.
    /// The decoder must reject this as InvalidImageContentException before any allocation.
    ///
    /// Affected file:
    ///   src/ImageSharp/Formats/Exr/ExrDecoderCore.cs lines 142–150, 215–223
    ///   src/ImageSharp/Formats/Exr/ExrUtils.cs CalculateBytesPerRow
    /// </summary>
    [Fact]
    public void Decode_RgbaRowSizingExceedsBufferLimits_Throws()
    {
        // 4 RGBA HALF channels at this width cannot be represented by the decoder's
        // int-sized row staging or block buffers.
        byte[] data = BuildMinimalRgbaExr(xMin: 0, yMin: 0, xMax: 536870911, yMax: 0);

        using var stream = new MemoryStream(data);
        Assert.Throws<InvalidImageContentException>(
            () => ExrDecoder.Instance.Decode<Rgba32>(DecoderOptions.Default, stream));
    }

    [Fact]
    public void Decode_DataWindowWidthExceedsRowBufferLimit_Throws()
    {
        // A single HALF channel keeps bytesPerBlock below int.MaxValue, but the decoder
        // still stages four color planes and must reject widths that overflow width × 4.
        byte[] data = BuildMinimalExr(xMin: 0, yMin: 0, xMax: int.MaxValue / 4, yMax: 0);

        using var stream = new MemoryStream(data);
        Assert.Throws<InvalidImageContentException>(
            () => ExrDecoder.Instance.Decode<Rgba32>(DecoderOptions.Default, stream));
    }

    [Fact]
    public void Identify_RowOffsetTableExceedsStream_Throws()
    {
        // Identify parses the header only, so this verifies the offset table bound is
        // validated before scanline decoding reads from the table.
        byte[] data = BuildMinimalExr(xMin: 0, yMin: 0, xMax: 1, yMax: 1);

        using var stream = new MemoryStream(data);
        Assert.Throws<InvalidImageContentException>(
            () => ExrDecoder.Instance.Identify(DecoderOptions.Default, stream));
    }

    // -------------------------------------------------------------------------
    // Helpers: construct minimal valid-enough EXR scanline files.
    //
    // Required attributes per ParseHeaderAttributes validation:
    //   channels, compression, dataWindow, displayWindow,
    //   lineOrder, pixelAspectRatio, screenWindowCenter, screenWindowWidth
    // -------------------------------------------------------------------------

    private static byte[] BuildMinimalExr(
        int xMin, int yMin, int xMax, int yMax,
        byte[] rowOffsetTableAppend = null)
    {
        // channels: single "R" HALF channel with xSampling=1, ySampling=1
        // Layout per ReadChannelInfo: name\0 (2) + pixelType (4) + pLinear+reserved (4)
        //                             + xSampling (4) + ySampling (4) = 18 bytes/channel
        //                             + list-null (1) = 19 total
        byte[] channelData =
        [
            0x52, 0x00,              // "R\0"
            0x01, 0x00, 0x00, 0x00, // pixelType = Half (1)
            0x00, 0x00, 0x00, 0x00, // pLinear + 3 reserved bytes
            0x01, 0x00, 0x00, 0x00, // xSampling = 1
            0x01, 0x00, 0x00, 0x00, // ySampling = 1
            0x00,                   // channel-list null terminator
        ];

        return BuildExrWithChannels(xMin, yMin, xMax, yMax, channelData, rowOffsetTableAppend);
    }

    private static byte[] BuildMinimalRgbaExr(int xMin, int yMin, int xMax, int yMax)
    {
        // 4 HALF channels in alphabetical order (A, B, G, R) per EXR spec.
        // 18 bytes per channel × 4 channels + 1 list-null = 73 bytes total.
        byte[] channelData =
        [
            0x41, 0x00,              // "A\0"
            0x01, 0x00, 0x00, 0x00, // pixelType = Half (1)
            0x00, 0x00, 0x00, 0x00, // pLinear + 3 reserved bytes
            0x01, 0x00, 0x00, 0x00, // xSampling = 1
            0x01, 0x00, 0x00, 0x00, // ySampling = 1
            0x42, 0x00,              // "B\0"
            0x01, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x01, 0x00, 0x00, 0x00,
            0x01, 0x00, 0x00, 0x00,
            0x47, 0x00,              // "G\0"
            0x01, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x01, 0x00, 0x00, 0x00,
            0x01, 0x00, 0x00, 0x00,
            0x52, 0x00,              // "R\0"
            0x01, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x01, 0x00, 0x00, 0x00,
            0x01, 0x00, 0x00, 0x00,
            0x00,                   // channel-list null terminator
        ];

        return BuildExrWithChannels(xMin, yMin, xMax, yMax, channelData);
    }

    private static byte[] BuildExrWithChannels(
        int xMin, int yMin, int xMax, int yMax,
        byte[] channelData,
        byte[] rowOffsetTableAppend = null)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms, System.Text.Encoding.ASCII, leaveOpen: true);

        // Magic (0x01312F76 LE) + version 2, scanline (flags = 0x00)
        bw.Write(new byte[] { 0x76, 0x2F, 0x31, 0x01, 0x02, 0x00, 0x00, 0x00 });

        void WriteAttr(string name, string type, byte[] payload)
        {
            foreach (char c in name) bw.Write((byte)c);
            bw.Write((byte)0);
            foreach (char c in type) bw.Write((byte)c);
            bw.Write((byte)0);
            bw.Write(payload.Length);
            bw.Write(payload);
        }

        WriteAttr("channels", "chlist", channelData);

        WriteAttr("compression", "compression", [0x00]); // None

        byte[] dw = new byte[16];
        BinaryPrimitives.WriteInt32LittleEndian(dw, xMin);
        BinaryPrimitives.WriteInt32LittleEndian(dw.AsSpan(4), yMin);
        BinaryPrimitives.WriteInt32LittleEndian(dw.AsSpan(8), xMax);
        BinaryPrimitives.WriteInt32LittleEndian(dw.AsSpan(12), yMax);
        WriteAttr("dataWindow", "box2i", dw);

        WriteAttr("displayWindow", "box2i", new byte[16]); // all zeros (0,0,0,0)

        WriteAttr("lineOrder", "lineOrder", [0x00]); // IncreasingY

        byte[] aspect = new byte[4];
        BinaryPrimitives.WriteSingleLittleEndian(aspect, 1.0f);
        WriteAttr("pixelAspectRatio", "float", aspect);

        WriteAttr("screenWindowCenter", "v2f", new byte[8]); // (0f, 0f)

        byte[] sww = new byte[4];
        BinaryPrimitives.WriteSingleLittleEndian(sww, 1.0f);
        WriteAttr("screenWindowWidth", "float", sww);

        bw.Write((byte)0x00); // end-of-header sentinel

        if (rowOffsetTableAppend is not null)
            bw.Write(rowOffsetTableAppend);

        return ms.ToArray();
    }
}
