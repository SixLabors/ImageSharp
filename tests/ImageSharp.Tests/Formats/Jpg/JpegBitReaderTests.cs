// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder;
using SixLabors.ImageSharp.IO;

namespace SixLabors.ImageSharp.Tests.Formats.Jpg;

/// <summary>
/// Makes sure that the bit reader puts the stream at the position that the segment parser expects.
/// The bit reader reads bytes before it needs them.
/// </summary>
[Trait("Format", "Jpg")]
public class JpegBitReaderTests
{
    // The data has two bytes of an earlier segment, eight entropy-coded bytes and an EOI marker.
    // The scan must not consume the bytes after the marker.
    private const int ScanStart = 2;
    private const int MarkerStart = ScanStart + 8;

    private static readonly byte[] Data =
    [
        0xAA, 0xAA,
        0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE, 0xF0,
        0xFF, 0xD9,
        0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C
    ];

    public static readonly TheoryData<int> EntropyBufferLengths = new() { 1, 5, JpegBitReader.EntropyBufferSize };

    [Theory]
    [MemberData(nameof(EntropyBufferLengths))]
    public unsafe void EndScan_AfterMarker_MovesStreamToMarkerStart(int entropyBufferLength)
    {
        using MemoryStream memoryStream = new(Data);
        using BufferedReadStream stream = new(Configuration.Default, memoryStream);
        stream.Position = ScanStart;

        byte[] entropyBuffer = new byte[entropyBufferLength];
        fixed (byte* entropy = entropyBuffer)
        {
            JpegBitReader reader = new(stream, entropy, entropyBufferLength);

            // Each fill loads six bytes. The second fill finds the marker after the eighth entropy-coded byte.
            reader.CheckBits();
            _ = reader.GetBits(16);
            _ = reader.GetBits(16);
            _ = reader.GetBits(16);
            reader.CheckBits();

            Assert.Equal(0xD9, reader.Marker);
            Assert.Equal(MarkerStart, reader.MarkerPosition);

            reader.EndScan();
        }

        // The segment parser reads the marker next.
        Assert.Equal(MarkerStart, stream.Position);
        Assert.Equal(0xFF, stream.ReadByte());
        Assert.Equal(0xD9, stream.ReadByte());
    }

    [Theory]
    [MemberData(nameof(EntropyBufferLengths))]
    public unsafe void EndScan_WithoutMarker_MovesStreamToNextUnreadByte(int entropyBufferLength)
    {
        using MemoryStream memoryStream = new(Data);
        using BufferedReadStream stream = new(Configuration.Default, memoryStream);
        stream.Position = ScanStart;

        byte[] entropyBuffer = new byte[entropyBufferLength];
        fixed (byte* entropy = entropyBuffer)
        {
            JpegBitReader reader = new(stream, entropy, entropyBufferLength);

            // One fill loads six bytes into the bit buffer. The bytes after them are not read.
            reader.CheckBits();
            Assert.Equal(0x1234, reader.GetBits(16));

            reader.EndScan();
        }

        Assert.Equal(ScanStart + 6, stream.Position);
        Assert.Equal(0xDE, stream.ReadByte());
    }

    [Theory]
    [MemberData(nameof(EntropyBufferLengths))]
    public unsafe void RewindToMarker_MovesStreamToMarkerStart(int entropyBufferLength)
    {
        using MemoryStream memoryStream = new(Data);
        using BufferedReadStream stream = new(Configuration.Default, memoryStream);
        stream.Position = ScanStart;

        byte[] entropyBuffer = new byte[entropyBufferLength];
        fixed (byte* entropy = entropyBuffer)
        {
            JpegBitReader reader = new(stream, entropy, entropyBufferLength);

            reader.CheckBits();
            _ = reader.GetBits(16);
            _ = reader.GetBits(16);
            _ = reader.GetBits(16);
            reader.CheckBits();

            reader.RewindToMarker();
        }

        Assert.Equal(MarkerStart, stream.Position);
        Assert.Equal(0xFF, stream.ReadByte());
    }
}
