// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Hevc;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Hevc;

/// <summary>
/// Validates complete HEVC still-picture reconstruction against independently decoded samples.
/// </summary>
[Trait("Format", "Heif")]
public class HevcPictureDecoderTests
{
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
        int mismatchCount = 0;
        int maximumDifference = 0;
        int firstMismatchX = 0;
        int firstMismatchY = 0;
        ushort firstActual = 0;
        ushort firstExpected = 0;
        for (int y = 0; y < height; y++)
        {
            Span<ushort> actualRow = picture.GetRowSpan(plane, sourceY + y).Slice(sourceX, width);
            for (int x = 0; x < width; x++)
            {
                ushort expectedSample = expected[offset++];
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
            }
        }

        Assert.True(
            mismatchCount == 0,
            $"{plane} contained {mismatchCount} differing samples. The maximum difference was {maximumDifference}; " +
            $"the first mismatch at ({firstMismatchX}, {firstMismatchY}) was {firstActual}, expected {firstExpected}.");
    }

    /// <summary>
    /// Converts a luma display extent to the selected component extent.
    /// </summary>
    /// <param name="lumaSize">The displayed luma extent.</param>
    /// <param name="subsampling">The component subsampling shift.</param>
    /// <returns>The displayed component extent.</returns>
    private static int GetDisplaySize(int lumaSize, int subsampling) => (lumaSize + (1 << subsampling) - 1) >> subsampling;
}
