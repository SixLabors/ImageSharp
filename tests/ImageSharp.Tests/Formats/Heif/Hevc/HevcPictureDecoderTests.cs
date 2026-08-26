// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Security.Cryptography;
using SixLabors.ImageSharp.Formats.Heif.Hevc;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Hevc;

/// <summary>
/// Validates complete HEVC still-picture reconstruction against independently decoded samples.
/// </summary>
[Trait("Format", "Heif")]
public class HevcPictureDecoderTests
{
    /// <summary>
    /// Verifies the first independently coded picture from official ITU RExt conformance streams against its
    /// published decoded-picture hashes.
    /// </summary>
    /// <param name="path">The official Annex B conformance stream.</param>
    /// <param name="bitDepth">The signaled component precision.</param>
    /// <param name="chromaFormat">The signaled HEVC chroma-format identifier.</param>
    /// <param name="lumaDigest">The normative luma-plane MD5 digest.</param>
    /// <param name="chromaBlueDigest">The normative blue-difference-plane MD5 digest, when present.</param>
    /// <param name="chromaRedDigest">The normative red-difference-plane MD5 digest, when present.</param>
    [Theory]
    [InlineData(TestImages.Heif.General8BitMonochrome, 8, 0, "e5223be3da805fb96440dbf2bd170db0", null, null)]
    [InlineData(TestImages.Heif.General8Bit420, 8, 1, "7d66d87736d627193acef745b3b7d014", "cecabae4dd685151d8de966ad01f01a8", "d069d15c457867a0a4fb9c0201eb0585")]
    [InlineData(TestImages.Heif.General8Bit444, 8, 3, "2be0bad2e95b42a9f53138cd874db1c9", "f7d50f66757b468f438df47b7e6b4f36", "72d935e42f5e76aa04e0326c64f026dc")]
    [InlineData(TestImages.Heif.General10Bit420, 10, 1, "9262fdf6a69587b8f1eed23c9026cb24", "cd81cc4b427565dc8c17761f2bd07c09", "0409bf573e03e2a6dd00b760b997a824")]
    [InlineData(TestImages.Heif.General10Bit422, 10, 2, "4c0a0a1bf001ebf1dc440ccd9e0ae3ea", "bd35abc3f86ead4bd59e19403248ee5e", "8ce96a8885e10cda55e67eba25d9ec03")]
    [InlineData(TestImages.Heif.General10Bit444, 10, 3, "d6293dfd466b7ed570beb56dee7823e3", "a82bf54ac3b2e996f40db77beff69b03", "d4f38dae50bbaa4087c7c1cf020d30a2")]
    [InlineData(TestImages.Heif.General12BitMonochrome, 12, 0, "549ff2b94ede8d83bfdc64a34440817d", null, null)]
    [InlineData(TestImages.Heif.General12Bit420, 12, 1, "346f709b5dfe5dd41f2ba1c70d072eb6", "6eee29326b96bb032a4a0ed822e4ba17", "59ef3982a4e0e9597d498a0d035a645a")]
    [InlineData(TestImages.Heif.General12Bit422, 12, 2, "be9c8562410e42b2db985444bf8a448e", "c5d616f1ccf8b2e9f56e1bb1d3e23134", "676a9e1ad75cff3193fce58bdb2721cb")]
    [InlineData(TestImages.Heif.General12Bit444, 12, 3, "057c9c3dd78c63b2689a159e21da1071", "4d0529c8e5755bb49d0ba0ec9a8e7e89", "9f5e9d559b0cf62440c2e05f141aa5d0")]
    public void DecodeOfficialRangeExtensionsPictureMatchesPublishedDigest(
        string path,
        int bitDepth,
        byte chromaFormat,
        string lumaDigest,
        string chromaBlueDigest,
        string chromaRedDigest)
    {
        byte[] annexB = TestFile.Create(path).Bytes;
        byte[] expectedYuv = TestFile.Create($"{path[..^4]}_frame0.yuv").Bytes;
        ConvertAnnexBStillPicture(annexB, bitDepth, chromaFormat, out byte[] configurationData, out byte[] itemData);
        HevcCodecConfiguration configuration = new(configurationData);
        HevcImageItemBitstream bitstream = new(itemData, configuration);
        using HevcPictureDecoder decoder = new(Configuration.Default, bitstream.SliceSegments[0].PictureParameterSet);

        decoder.Decode(bitstream);

        Assert.Equal(bitDepth, decoder.Picture.BitDepthLuma);
        Assert.Equal(chromaFormat, decoder.Picture.ChromaFormat);
        int expectedLength = decoder.Picture.GetWidth(HevcPlane.Y) * decoder.Picture.GetHeight(HevcPlane.Y) * (bitDepth > 8 ? 2 : 1);
        if (chromaFormat != 0)
        {
            int chromaLength = decoder.Picture.GetWidth(HevcPlane.Cb) * decoder.Picture.GetHeight(HevcPlane.Cb) * (bitDepth > 8 ? 2 : 1);
            expectedLength += chromaLength * 2;
        }

        Assert.Equal(expectedLength, expectedYuv.Length);
        int referenceOffset = 0;
        AssertCodedPlaneEqual(decoder.Picture, HevcPlane.Y, expectedYuv, ref referenceOffset);
        if (chromaFormat != 0)
        {
            AssertCodedPlaneEqual(decoder.Picture, HevcPlane.Cb, expectedYuv, ref referenceOffset);
            AssertCodedPlaneEqual(decoder.Picture, HevcPlane.Cr, expectedYuv, ref referenceOffset);
        }

        Assert.Equal(expectedYuv.Length, referenceOffset);
        Assert.Equal(lumaDigest, GetPlaneDigest(decoder.Picture, HevcPlane.Y));
        if (chromaFormat != 0)
        {
            Assert.Equal(chromaBlueDigest, GetPlaneDigest(decoder.Picture, HevcPlane.Cb));
            Assert.Equal(chromaRedDigest, GetPlaneDigest(decoder.Picture, HevcPlane.Cr));
        }
    }

    /// <summary>
    /// Verifies all reconstructed samples from a real HEIC grid tile against the HM reference decoder.
    /// </summary>
    /// <param name="configurationPath">The exact HEVC decoder-configuration record associated with the item.</param>
    /// <param name="itemPath">The exact HEVC item payload to decode.</param>
    /// <param name="referencePath">The corresponding planar samples produced by HM.</param>
    [Theory]
    [InlineData(TestImages.Heif.Image1TileHvcConfiguration, TestImages.Heif.Image1Tile1Payload, TestImages.Heif.Image1Tile1ReferenceYuv)]
    [InlineData(TestImages.Heif.Image1TileHvcConfiguration, TestImages.Heif.Image1Tile2Payload, TestImages.Heif.Image1Tile2ReferenceYuv)]
    [InlineData(TestImages.Heif.Image2TileHvcConfiguration, TestImages.Heif.Image2Tile1Payload, TestImages.Heif.Image2Tile1ReferenceYuv)]
    [InlineData(TestImages.Heif.Image2TileHvcConfiguration, TestImages.Heif.Image2Tile7Payload, TestImages.Heif.Image2Tile7ReferenceYuv)]
    [InlineData(TestImages.Heif.DwsampleTileHvcConfiguration, TestImages.Heif.DwsampleTilePayload, TestImages.Heif.DwsampleTileReferenceYuv)]
    public void DecodeRealHeicTileMatchesHmReference(string configurationPath, string itemPath, string referencePath)
    {
        byte[] configurationData = TestFile.Create(configurationPath).Bytes;
        byte[] itemData = TestFile.Create(itemPath).Bytes;
        byte[] expectedYuv = TestFile.Create(referencePath).Bytes;
        HevcCodecConfiguration configuration = new(configurationData);
        HevcImageItemBitstream bitstream = new(itemData, configuration);
        HevcSequenceParameterSet sequenceParameterSet = bitstream.SliceSegments[0].PictureParameterSet.SequenceParameterSet;
        using HevcPictureDecoder decoder = new(Configuration.Default, bitstream.SliceSegments[0].PictureParameterSet);

        decoder.Decode(bitstream);

        int expectedLength = sequenceParameterSet.DisplayWidth * sequenceParameterSet.DisplayHeight;
        if (decoder.Picture.ChromaFormat != 0)
        {
            int chromaWidth = GetDisplaySize(sequenceParameterSet.DisplayWidth, decoder.Picture.GetSubsamplingX(HevcPlane.Cb));
            int chromaHeight = GetDisplaySize(sequenceParameterSet.DisplayHeight, decoder.Picture.GetSubsamplingY(HevcPlane.Cb));
            expectedLength += 2 * chromaWidth * chromaHeight;
        }

        Assert.Equal(expectedLength, expectedYuv.Length);
        int offset = 0;
        AssertPlaneEqual(decoder.Picture, sequenceParameterSet, HevcPlane.Y, expectedYuv, ref offset);
        if (decoder.Picture.ChromaFormat != 0)
        {
            AssertPlaneEqual(decoder.Picture, sequenceParameterSet, HevcPlane.Cb, expectedYuv, ref offset);
            AssertPlaneEqual(decoder.Picture, sequenceParameterSet, HevcPlane.Cr, expectedYuv, ref offset);
        }

        Assert.Equal(expectedYuv.Length, offset);
    }

    /// <summary>
    /// Compares one decoded component plane with its planar reference samples.
    /// </summary>
    /// <param name="picture">The decoded picture containing the component plane.</param>
    /// <param name="sequenceParameterSet">The coded and displayed picture geometry.</param>
    /// <param name="plane">The component plane to compare.</param>
    /// <param name="expected">The complete planar YUV reference.</param>
    /// <param name="offset">The current reference offset, advanced past the compared plane.</param>
    private static void AssertPlaneEqual(
        HevcPictureBuffer picture,
        HevcSequenceParameterSet sequenceParameterSet,
        HevcPlane plane,
        ReadOnlySpan<byte> expected,
        ref int offset)
    {
        int subsamplingX = picture.GetSubsamplingX(plane);
        int subsamplingY = picture.GetSubsamplingY(plane);
        int sourceX = sequenceParameterSet.ConformanceWindowLeftOffset >> subsamplingX;
        int sourceY = sequenceParameterSet.ConformanceWindowTopOffset >> subsamplingY;
        int width = GetDisplaySize(sequenceParameterSet.DisplayWidth, subsamplingX);
        int height = GetDisplaySize(sequenceParameterSet.DisplayHeight, subsamplingY);
        AssertPlaneEqual(picture, plane, sourceX, sourceY, width, height, expected, ref offset);
    }

    /// <summary>
    /// Compares one complete coded component plane with its planar reference samples.
    /// </summary>
    /// <param name="picture">The decoded picture containing the component plane.</param>
    /// <param name="plane">The component plane to compare.</param>
    /// <param name="expected">The complete planar YUV reference.</param>
    /// <param name="offset">The current reference offset, advanced past the compared plane.</param>
    private static void AssertCodedPlaneEqual(HevcPictureBuffer picture, HevcPlane plane, ReadOnlySpan<byte> expected, ref int offset)
        => AssertPlaneEqual(picture, plane, 0, 0, picture.GetWidth(plane), picture.GetHeight(plane), expected, ref offset);

    /// <summary>
    /// Compares one rectangular component region with its planar reference samples.
    /// </summary>
    /// <param name="picture">The decoded picture containing the component plane.</param>
    /// <param name="plane">The component plane to compare.</param>
    /// <param name="sourceX">The source-region X coordinate in component samples.</param>
    /// <param name="sourceY">The source-region Y coordinate in component samples.</param>
    /// <param name="width">The compared width in component samples.</param>
    /// <param name="height">The compared height in component samples.</param>
    /// <param name="expected">The complete planar YUV reference.</param>
    /// <param name="offset">The current reference offset, advanced past the compared plane.</param>
    private static void AssertPlaneEqual(
        HevcPictureBuffer picture,
        HevcPlane plane,
        int sourceX,
        int sourceY,
        int width,
        int height,
        ReadOnlySpan<byte> expected,
        ref int offset)
    {
        bool usesHighBitDepthSamples = picture.GetBitDepth(plane) > 8;
        int mismatchCount = 0;
        int maximumDifference = 0;
        int firstMismatchX = 0;
        int firstMismatchY = 0;
        int minimumMismatchX = width;
        int minimumMismatchY = height;
        int maximumMismatchX = 0;
        int maximumMismatchY = 0;
        ushort firstActual = 0;
        ushort firstExpected = 0;
        for (int y = 0; y < height; y++)
        {
            Span<ushort> actualRow = picture.GetRowSpan(plane, sourceY + y).Slice(sourceX, width);
            for (int x = 0; x < width; x++)
            {
                ushort expectedSample;
                if (usesHighBitDepthSamples)
                {
                    expectedSample = BinaryPrimitives.ReadUInt16LittleEndian(expected[offset..]);
                    offset += 2;
                }
                else
                {
                    expectedSample = expected[offset++];
                }

                int difference = Math.Abs(actualRow[x] - expectedSample);
                if (difference == 0)
                {
                    continue;
                }

                if (mismatchCount == 0)
                {
                    firstMismatchX = x;
                    firstMismatchY = y;
                    firstActual = actualRow[x];
                    firstExpected = expectedSample;
                }

                mismatchCount++;
                maximumDifference = Math.Max(maximumDifference, difference);
                minimumMismatchX = Math.Min(minimumMismatchX, x);
                minimumMismatchY = Math.Min(minimumMismatchY, y);
                maximumMismatchX = Math.Max(maximumMismatchX, x);
                maximumMismatchY = Math.Max(maximumMismatchY, y);
            }
        }

        Assert.True(
            mismatchCount == 0,
            $"{plane} contained {mismatchCount} differing samples. The maximum difference was {maximumDifference}; " +
            $"the mismatches span ({minimumMismatchX}, {minimumMismatchY}) through ({maximumMismatchX}, {maximumMismatchY}), " +
            $"and the first mismatch at ({firstMismatchX}, {firstMismatchY}) was {firstActual}, expected {firstExpected}.");
    }

    /// <summary>
    /// Converts a luma display extent to the selected component extent.
    /// </summary>
    /// <param name="lumaSize">The displayed luma extent.</param>
    /// <param name="subsampling">The component subsampling shift.</param>
    /// <returns>The displayed component extent.</returns>
    private static int GetDisplaySize(int lumaSize, int subsampling) => (lumaSize + (1 << subsampling) - 1) >> subsampling;

    /// <summary>
    /// Adapts the first independently coded Annex B picture to the bounded <c>hvc1</c> item contract used by the
    /// production decoder.
    /// </summary>
    /// <param name="annexB">The complete official conformance stream.</param>
    /// <param name="bitDepth">The stream's published component precision.</param>
    /// <param name="chromaFormat">The stream's published chroma-format identifier.</param>
    /// <param name="configurationData">The generated item-local HEVC decoder configuration.</param>
    /// <param name="itemData">The generated length-delimited payload containing only the first picture.</param>
    private static void ConvertAnnexBStillPicture(
        ReadOnlySpan<byte> annexB,
        int bitDepth,
        byte chromaFormat,
        out byte[] configurationData,
        out byte[] itemData)
    {
        const int VideoParameterSetNalUnitType = 32;
        const int SequenceParameterSetNalUnitType = 33;
        const int PictureParameterSetNalUnitType = 34;
        const int HighestVideoCodingLayerNalUnitType = 31;
        const int NalUnitHeaderLength = 2;
        const int SpsConfigurationPrefixLength = 13;
        const int ProfileTierLevelLength = 12;
        const int ConfigurationHeaderLength = 23;
        const int ParameterSetArrayHeaderLength = 5;
        const int ParameterSetCount = 3;
        const int ItemNalUnitLengthFieldLength = 4;

        (int Offset, int Length) videoParameterSet = default;
        (int Offset, int Length) sequenceParameterSet = default;
        (int Offset, int Length) pictureParameterSet = default;
        List<(int Offset, int Length)> pictureNalUnits = [];
        int offset = 0;
        bool foundPicture = false;
        while (TryReadAnnexBNalUnit(annexB, ref offset, out int nalOffset, out int nalLength))
        {
            // HEVC stores nal_unit_type in the six bits following forbidden_zero_bit.
            int nalUnitType = (annexB[nalOffset] >> 1) & 0x3F;
            if (!foundPicture)
            {
                switch (nalUnitType)
                {
                    case VideoParameterSetNalUnitType:
                        videoParameterSet = (nalOffset, nalLength);
                        break;
                    case SequenceParameterSetNalUnitType:
                        sequenceParameterSet = (nalOffset, nalLength);
                        break;
                    case PictureParameterSetNalUnitType:
                        pictureParameterSet = (nalOffset, nalLength);
                        break;
                }
            }

            if (nalUnitType > HighestVideoCodingLayerNalUnitType)
            {
                continue;
            }

            // The first RBSP bit after the two-byte NAL header is first_slice_segment_in_pic_flag. No emulation byte
            // can precede that first bit, so it can terminate the extracted picture without parsing later sequences.
            bool firstSliceSegment = (annexB[nalOffset + NalUnitHeaderLength] & 0x80) != 0;
            if (foundPicture && firstSliceSegment)
            {
                break;
            }

            foundPicture = true;
            pictureNalUnits.Add((nalOffset, nalLength));
        }

        Assert.True(videoParameterSet.Length > 0, "The conformance stream does not contain a VPS before its first picture.");
        Assert.True(sequenceParameterSet.Length > 0, "The conformance stream does not contain an SPS before its first picture.");
        Assert.True(pictureParameterSet.Length > 0, "The conformance stream does not contain a PPS before its first picture.");
        Assert.NotEmpty(pictureNalUnits);

        ReadOnlySpan<byte> sps = annexB.Slice(sequenceParameterSet.Offset, sequenceParameterSet.Length);
        Span<byte> spsRbspPrefix = stackalloc byte[SpsConfigurationPrefixLength];
        CopyRbspPrefix(sps[NalUnitHeaderLength..], spsRbspPrefix);

        int configurationLength = ConfigurationHeaderLength
            + (ParameterSetCount * ParameterSetArrayHeaderLength)
            + videoParameterSet.Length
            + sequenceParameterSet.Length
            + pictureParameterSet.Length;

        configurationData = new byte[configurationLength];

        // ISO/IEC 14496-15 defines a fixed 23-byte HEVCDecoderConfigurationRecord header. Copying
        // profile_tier_level directly from the published SPS avoids synthesizing codec capability claims.
        configurationData[0] = 1; // configurationVersion
        spsRbspPrefix.Slice(1, ProfileTierLevelLength).CopyTo(configurationData.AsSpan(1, ProfileTierLevelLength));
        configurationData[13] = 0xF0; // reserved and min_spatial_segmentation_idc = 0
        configurationData[15] = 0xFC; // reserved and parallelismType = 0
        configurationData[16] = (byte)(0xFC | chromaFormat); // reserved and chromaFormat
        configurationData[17] = (byte)(0xF8 | (bitDepth - 8)); // reserved and bitDepthLumaMinus8
        configurationData[18] = (byte)(0xF8 | (chromaFormat == 0 ? 0 : bitDepth - 8)); // reserved and bitDepthChromaMinus8

        int maxSubLayers = ((spsRbspPrefix[0] >> 1) & 7) + 1;
        int temporalIdNesting = spsRbspPrefix[0] & 1;
        configurationData[21] = (byte)((maxSubLayers << 3) | (temporalIdNesting << 2) | 3); // lengthSizeMinusOne = 3
        configurationData[22] = ParameterSetCount;
        int configurationOffset = ConfigurationHeaderLength;
        WriteParameterSetArray(configurationData, ref configurationOffset, VideoParameterSetNalUnitType, annexB.Slice(videoParameterSet.Offset, videoParameterSet.Length));
        WriteParameterSetArray(configurationData, ref configurationOffset, SequenceParameterSetNalUnitType, sps);
        WriteParameterSetArray(configurationData, ref configurationOffset, PictureParameterSetNalUnitType, annexB.Slice(pictureParameterSet.Offset, pictureParameterSet.Length));
        Assert.Equal(configurationData.Length, configurationOffset);

        int itemLength = 0;
        foreach ((int _, int nalLength) in pictureNalUnits)
        {
            itemLength += ItemNalUnitLengthFieldLength + nalLength;
        }

        itemData = new byte[itemLength];
        int itemOffset = 0;
        foreach ((int nalOffset, int nalLength) in pictureNalUnits)
        {
            BinaryPrimitives.WriteUInt32BigEndian(itemData.AsSpan(itemOffset), (uint)nalLength);
            itemOffset += ItemNalUnitLengthFieldLength;
            annexB.Slice(nalOffset, nalLength).CopyTo(itemData.AsSpan(itemOffset));
            itemOffset += nalLength;
        }
    }

    /// <summary>
    /// Reads the next NAL-unit payload from an Annex B byte stream.
    /// </summary>
    /// <param name="source">The complete Annex B byte stream.</param>
    /// <param name="offset">The current search offset, advanced to the next start code.</param>
    /// <param name="nalOffset">The returned NAL-unit payload offset.</param>
    /// <param name="nalLength">The returned NAL-unit payload length.</param>
    /// <returns><see langword="true"/> when another complete NAL unit was found.</returns>
    private static bool TryReadAnnexBNalUnit(ReadOnlySpan<byte> source, ref int offset, out int nalOffset, out int nalLength)
    {
        int startCodeOffset = FindAnnexBStartCode(source, offset, out int startCodeLength);
        if (startCodeOffset < 0)
        {
            nalOffset = 0;
            nalLength = 0;
            return false;
        }

        nalOffset = startCodeOffset + startCodeLength;
        int nextStartCodeOffset = FindAnnexBStartCode(source, nalOffset, out _);
        int nalEnd = nextStartCodeOffset < 0 ? source.Length : nextStartCodeOffset;

        // Annex B permits trailing_zero_8bits between a NAL unit and the next start-code prefix. They are byte-stream
        // framing and must not enter the length-delimited item payload.
        while (nalEnd > nalOffset && source[nalEnd - 1] == 0)
        {
            nalEnd--;
        }

        offset = nextStartCodeOffset < 0 ? source.Length : nextStartCodeOffset;
        nalLength = nalEnd - nalOffset;
        return nalLength >= 2;
    }

    /// <summary>
    /// Locates the next three- or four-byte Annex B start code.
    /// </summary>
    /// <param name="source">The complete Annex B byte stream.</param>
    /// <param name="offset">The first byte to inspect.</param>
    /// <param name="length">The returned start-code length.</param>
    /// <returns>The start-code offset, or negative one when no code remains.</returns>
    private static int FindAnnexBStartCode(ReadOnlySpan<byte> source, int offset, out int length)
    {
        for (int index = offset; index <= source.Length - 3; index++)
        {
            if (source[index] != 0 || source[index + 1] != 0)
            {
                continue;
            }

            if (source[index + 2] == 1)
            {
                length = 3;
                return index;
            }

            if (index <= source.Length - 4 && source[index + 2] == 0 && source[index + 3] == 1)
            {
                length = 4;
                return index;
            }
        }

        length = 0;
        return -1;
    }

    /// <summary>
    /// Copies the fixed SPS prefix through general_level_idc while removing emulation-prevention bytes.
    /// </summary>
    /// <param name="escapedRbsp">The SPS bytes following the NAL-unit header.</param>
    /// <param name="destination">The fixed 13-byte SPS prefix destination.</param>
    private static void CopyRbspPrefix(ReadOnlySpan<byte> escapedRbsp, Span<byte> destination)
    {
        const int EscapeZeroCount = 2;
        const byte EmulationPreventionByte = 3;

        int sourceOffset = 0;
        int destinationOffset = 0;
        int consecutiveZeroes = 0;
        while (destinationOffset < destination.Length)
        {
            byte value = escapedRbsp[sourceOffset++];
            if (consecutiveZeroes == EscapeZeroCount && value == EmulationPreventionByte)
            {
                consecutiveZeroes = 0;
                continue;
            }

            destination[destinationOffset++] = value;
            consecutiveZeroes = value == 0 ? consecutiveZeroes + 1 : 0;
        }
    }

    /// <summary>
    /// Writes one complete parameter-set array to an HEVC decoder-configuration record.
    /// </summary>
    /// <param name="configuration">The complete configuration destination.</param>
    /// <param name="offset">The current destination offset, advanced past the array.</param>
    /// <param name="nalUnitType">The parameter-set NAL-unit type.</param>
    /// <param name="nalUnit">The complete NAL unit without Annex B framing.</param>
    private static void WriteParameterSetArray(Span<byte> configuration, ref int offset, byte nalUnitType, ReadOnlySpan<byte> nalUnit)
    {
        // Each complete array contains exactly one parameter set from the source stream. ISO/IEC 14496-15 stores
        // array_completeness in the high bit and the six-bit HEVC NAL-unit type in the low bits.
        configuration[offset++] = (byte)(0x80 | nalUnitType);
        BinaryPrimitives.WriteUInt16BigEndian(configuration[offset..], 1);
        offset += 2;
        BinaryPrimitives.WriteUInt16BigEndian(configuration[offset..], (ushort)nalUnit.Length);
        offset += 2;
        nalUnit.CopyTo(configuration[offset..]);
        offset += nalUnit.Length;
    }

    /// <summary>
    /// Calculates the HEVC decoded-picture MD5 digest for one reconstructed component plane.
    /// </summary>
    /// <param name="picture">The reconstructed picture.</param>
    /// <param name="plane">The component plane to hash.</param>
    /// <returns>The lowercase hexadecimal decoded-picture digest.</returns>
    private static string GetPlaneDigest(HevcPictureBuffer picture, HevcPlane plane)
    {
        int width = picture.GetWidth(plane);
        int height = picture.GetHeight(plane);
        int bytesPerSample = picture.GetBitDepth(plane) > 8 ? 2 : 1;
        byte[] rowBytes = new byte[width * bytesPerSample];
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        for (int y = 0; y < height; y++)
        {
            Span<ushort> samples = picture.GetRowSpan(plane, y)[..width];
            if (bytesPerSample == 1)
            {
                for (int x = 0; x < width; x++)
                {
                    rowBytes[x] = (byte)samples[x];
                }
            }
            else
            {
                for (int x = 0; x < width; x++)
                {
                    BinaryPrimitives.WriteUInt16LittleEndian(rowBytes.AsSpan(x * 2), samples[x]);
                }
            }

            hash.AppendData(rowBytes);
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }
}
