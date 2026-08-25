// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Hevc;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Hevc;

/// <summary>
/// Verifies HEVC arithmetic-decoder suspension and restart around pulse-code-modulated coding units.
/// </summary>
[Trait("Format", "Heic")]
public class HevcCabacDecoderTests
{
    /// <summary>
    /// Verifies that PCM samples begin after the terminating arithmetic bytes and that arithmetic decoding resumes after the raw payload.
    /// </summary>
    [Fact]
    public void PcmPayloadSuspendsAndRestartsArithmeticDecoding()
    {
        ReadOnlySpan<byte> data = [0xFF, 0xFF, 0xAB, 0xFF, 0xFF];
        HevcCabacDecoder decoder = new(data);

        Assert.True(decoder.ReadPcmFlag());
        Assert.Equal((ushort)0xA, decoder.ReadPcmSample(4));
        Assert.Equal((ushort)0xB, decoder.ReadPcmSample(4));

        decoder.RestartAfterPcm();

        Assert.True(decoder.ReadTerminate());
    }

    /// <summary>
    /// Verifies that a PCM sample cannot read beyond its bounded entropy substream.
    /// </summary>
    [Fact]
    public void PcmPayloadRejectsTruncatedSample()
    {
        Assert.Throws<InvalidImageContentException>(ReadTruncatedPcmSample);
    }

    /// <summary>
    /// Attempts to read a sample wider than the remaining raw PCM payload.
    /// </summary>
    private static void ReadTruncatedPcmSample()
    {
        ReadOnlySpan<byte> data = [0xFF, 0xFF, 0x80];
        HevcCabacDecoder decoder = new(data);

        Assert.True(decoder.ReadPcmFlag());
        decoder.ReadPcmSample(16);
    }
}
