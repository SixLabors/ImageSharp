// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Jpg;

/// <summary>
/// Tests for streams written by imperfect producers which libjpeg based decoders still decode.
/// </summary>
[Trait("Format", "Jpg")]
public partial class JpegDecoderTests
{
    /// <summary>
    /// Gets the encoder layouts paired with the junk inserted before every restart marker.
    /// The junk forms a TEM or reserved marker (FF 01 to FF BF) which cannot end a restart interval.
    /// </summary>
    public static TheoryData<bool, bool, byte[]> ReservedMarkerBeforeRestartData { get; } = new()
    {
        // Baseline interleaved.
        { false, true, [0xFF, 0x12] },
        { false, true, [0xFF, 0x01] },
        { false, true, [0xFF, 0xBF] },
        { false, true, [0xFF, 0x12, 0xFF, 0x34] },

        // Baseline non-interleaved.
        { false, false, [0xFF, 0x12] },

        // Progressive.
        { true, false, [0xFF, 0x12] },
    };

    [Theory]
    [MemberData(nameof(ReservedMarkerBeforeRestartData))]
    public void Decode_ReservedMarkerBeforeRestartMarkers_IsSkipped(bool progressive, bool interleaved, byte[] junk)
    {
        byte[] original = EncodeGradientJpeg(new JpegEncoder
        {
            ColorType = JpegColorType.YCbCrRatio420,
            Progressive = progressive,
            Interleaved = interleaved,
            RestartInterval = 1
        });

        byte[] corrupt = InsertBeforeRestartMarkers(original, junk);

        using Image<Rgba32> expected = Image.Load<Rgba32>(original);
        using Image<Rgba32> actual = Image.Load<Rgba32>(corrupt);
        ImageComparer.Exact.VerifySimilarity(expected, actual);
    }

    [Fact]
    public void Decode_TemMarkerBetweenHeaderSegments_IsSkipped()
    {
        byte[] original = EncodeGradientJpeg(new JpegEncoder { ColorType = JpegColorType.YCbCrRatio420 });

        // TEM has no length field. Insert one straight after the APP0 segment which directly follows SOI.
        byte[] corrupt = Insert(original, SegmentEnd(original, 2), JpegConstants.Markers.XFF, JpegConstants.Markers.TEM);

        ImageInfo info = Image.Identify(corrupt);
        Assert.Equal(new Size(32, 24), info.Size);

        using Image<Rgba32> expected = Image.Load<Rgba32>(original);
        using Image<Rgba32> actual = Image.Load<Rgba32>(corrupt);
        ImageComparer.Exact.VerifySimilarity(expected, actual);
    }

    /// <summary>
    /// Encodes a 32x24 gradient, which spans several MCUs in every layout, with the given encoder.
    /// </summary>
    /// <param name="encoder">The encoder.</param>
    /// <returns>The encoded jpeg.</returns>
    private static byte[] EncodeGradientJpeg(JpegEncoder encoder)
    {
        using Image<Rgba32> image = new(32, 24);
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                image[x, y] = new Rgba32((byte)(x * 8), (byte)(y * 10), (byte)((x + y) * 4));
            }
        }

        using MemoryStream ms = new();
        image.SaveAsJpeg(ms, encoder);
        return ms.ToArray();
    }

    /// <summary>
    /// Inserts the junk bytes before every RSTn marker in the entropy-coded data following the first SOS.
    /// </summary>
    /// <param name="data">The jpeg.</param>
    /// <param name="junk">The bytes to insert.</param>
    /// <returns>The corrupted jpeg.</returns>
    private static byte[] InsertBeforeRestartMarkers(byte[] data, byte[] junk)
    {
        int start = SegmentEnd(data, FindSegment(data, JpegConstants.Markers.SOS));
        using MemoryStream ms = new();
        ms.Write(data, 0, start);

        int count = 0;
        for (int i = start; i < data.Length; i++)
        {
            if (data[i] == JpegConstants.Markers.XFF && i + 1 < data.Length && data[i + 1] is >= JpegConstants.Markers.RST0 and <= JpegConstants.Markers.RST7)
            {
                ms.Write(junk, 0, junk.Length);
                count++;
            }

            ms.WriteByte(data[i]);
        }

        Assert.True(count > 0, "The test image has no restart markers.");
        return ms.ToArray();
    }

    /// <summary>
    /// Walks the marker segments following SOI up to the first SOS.
    /// </summary>
    /// <param name="data">The jpeg.</param>
    /// <param name="marker">The marker code to find.</param>
    /// <returns>The offset of the first segment with the given marker code.</returns>
    private static int FindSegment(byte[] data, byte marker)
    {
        int offset = 2;
        while (data[offset + 1] != marker)
        {
            Assert.NotEqual(JpegConstants.Markers.SOS, data[offset + 1]);
            offset = SegmentEnd(data, offset);
        }

        return offset;
    }

    /// <summary>
    /// Gets the offset just past the marker segment (FF xx and its length field) starting at the given offset.
    /// </summary>
    /// <param name="data">The jpeg.</param>
    /// <param name="offset">The offset of the segment.</param>
    /// <returns>The offset of the following segment.</returns>
    private static int SegmentEnd(byte[] data, int offset)
    {
        Assert.Equal(JpegConstants.Markers.XFF, data[offset]);
        return offset + 2 + ((data[offset + 2] << 8) | data[offset + 3]);
    }

    /// <summary>
    /// Inserts bytes at the given offset.
    /// </summary>
    /// <param name="data">The source data.</param>
    /// <param name="offset">The offset to insert at.</param>
    /// <param name="bytes">The bytes to insert.</param>
    /// <returns>The new data.</returns>
    private static byte[] Insert(byte[] data, int offset, params byte[] bytes)
    {
        byte[] result = new byte[data.Length + bytes.Length];
        Buffer.BlockCopy(data, 0, result, 0, offset);
        Buffer.BlockCopy(bytes, 0, result, offset, bytes.Length);
        Buffer.BlockCopy(data, offset, result, offset + bytes.Length, data.Length - offset);
        return result;
    }
}
