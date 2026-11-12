// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Components.Entropy;

/// <summary>
/// Provides the ICC header predictions shared by JPEG XL encoding and decoding.
/// </summary>
internal static class JxlIccPrediction
{
    /// <summary>
    /// The fixed ICC header length, in bytes.
    /// </summary>
    public const int HeaderSize = 128;

    /// <summary>
    /// Predicts one header byte from the profile length and preceding profile bytes.
    /// </summary>
    /// <param name="output">The original or reconstructed profile. Bytes before <paramref name="index"/> must be available.</param>
    /// <param name="outputLength">The complete profile length in bytes.</param>
    /// <param name="index">The byte position within the 128-byte header.</param>
    /// <returns>The predicted byte before the residual is added or subtracted modulo 256.</returns>
    public static byte GetHeaderPrediction(ReadOnlySpan<byte> output, int outputLength, int index)
    {
        // Libjxl's ICCInitialHeaderPrediction and ICCPredictHeader supply these defaults and prior-byte rules.
        // Read only earlier profile bytes so the decoder can fill its final array in place and the encoder can borrow original data.
        return index switch
        {
            <= 3 => (byte)(outputLength >> (24 - (8 * index))),
            8 => 4,
            >= 12 and < 24 => "mntrRGB XYZ "u8[index - 12],
            >= 36 and < 40 => "acsp"u8[index - 36],
            41 or 42 when output[40] == (byte)'A' => (byte)'P',
            43 when output[40] == (byte)'A' => (byte)'L',
            41 when output[40] == (byte)'M' => (byte)'S',
            42 when output[40] == (byte)'M' => (byte)'F',
            43 when output[40] == (byte)'M' => (byte)'T',
            42 when output[40] == (byte)'S' && output[41] == (byte)'G' => (byte)'I',
            43 when output[40] == (byte)'S' && output[41] == (byte)'G' => (byte)' ',
            42 when output[40] == (byte)'S' && output[41] == (byte)'U' => (byte)'N',
            43 when output[40] == (byte)'S' && output[41] == (byte)'U' => (byte)'W',
            70 => 246,
            71 => 214,
            73 => 1,
            78 => 211,
            79 => 45,
            >= 80 and < 84 => output[index - 76],
            _ => 0
        };
    }
}
