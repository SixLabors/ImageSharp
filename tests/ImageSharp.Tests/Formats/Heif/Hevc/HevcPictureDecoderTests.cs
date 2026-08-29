// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Security.Cryptography;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Heif;
using SixLabors.ImageSharp.Formats.Heif.Hevc;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.Memory;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Hevc;

/// <summary>
/// Validates complete HEVC still-picture reconstruction and decoder ownership against independent results.
/// </summary>
[Trait("Format", "Heif")]
public class HevcPictureDecoderTests
{
    /// <summary>
    /// The hardware configurations required to exercise every SAO vector tier and the scalar fallback.
    /// </summary>
    private const HwIntrinsics LoopFilterConfigurations =
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic;

    /// <summary>
    /// Gets a complete prefix SEI RBSP carrying one unknown message followed by every supported metadata message.
    /// The values use the exact field widths and G, B, R ordering read by pinned HM.
    /// </summary>
    private static ReadOnlySpan<byte> SupplementalMetadataRbsp =>
    [
        0xFF, 0x2D, 0x01, 0x7A,
        0x89, 0x18,
        0x3A, 0x98, 0x75, 0x30,
        0x1D, 0x4C, 0x13, 0x88,
        0x7D, 0x00, 0x3E, 0x80,
        0x3D, 0x13, 0x40, 0x42,
        0x02, 0x03, 0x04, 0x05,
        0x01, 0x02, 0x03, 0x04,
        0x90, 0x04, 0x03, 0xE8, 0x01, 0x90,
        0x93, 0x01, 0x10,
        0x94, 0x08, 0x00, 0x0F, 0x42, 0x40, 0x3D, 0x13, 0x40, 0x42,
        0x95, 0x0D, 0x1C, 0x00, 0x3D, 0x09, 0x00, 0x01, 0x31, 0x2D, 0x00, 0x00, 0xB7, 0x1B, 0x02,
        0x80
    ];

    /// <summary>
    /// Gets a display-orientation prefix SEI RBSP that flips horizontally and then rotates a quarter turn
    /// anticlockwise.
    /// </summary>
    private static ReadOnlySpan<byte> DisplayOrientationRbsp =>
    [
        0x2F, 0x03, 0x48, 0x00, 0x08,
        0x80
    ];

    /// <summary>
    /// Identifies residual-tool signaling that an official independently decoded picture must exercise.
    /// </summary>
    [Flags]
    public enum ResidualTools
    {
        /// <summary>
        /// No optional residual tool is signaled.
        /// </summary>
        None = 0,

        /// <summary>
        /// Coding-unit luma quantization deltas are signaled.
        /// </summary>
        DeltaQuantization = 1,

        /// <summary>
        /// Non-flat quantization scaling matrices are enabled.
        /// </summary>
        ScalingLists = 2,

        /// <summary>
        /// Transform skip is enabled.
        /// </summary>
        TransformSkip = 4,

        /// <summary>
        /// Range-extension transform precision is enabled.
        /// </summary>
        ExtendedPrecision = 8,

        /// <summary>
        /// Transform and quantization bypass is enabled.
        /// </summary>
        TransquantizationBypass = 16,

        /// <summary>
        /// A coding-unit chroma quantization-offset list is enabled.
        /// </summary>
        ChromaQuantizationAdjustment = 32,
    }

    /// <summary>
    /// Identifies parallelization syntax that an official independently decoded picture must exercise.
    /// </summary>
    [Flags]
    public enum ParallelizationTools
    {
        /// <summary>
        /// No parallelization syntax is signaled.
        /// </summary>
        None = 0,

        /// <summary>
        /// Dependent slice segments are signaled.
        /// </summary>
        DependentSliceSegments = 1,

        /// <summary>
        /// Tile boundaries are signaled.
        /// </summary>
        Tiles = 2,

        /// <summary>
        /// Wavefront row entry points are signaled.
        /// </summary>
        Wavefront = 4,

        /// <summary>
        /// Slice-header extension bytes are signaled.
        /// </summary>
        SliceHeaderExtensions = 8,

        /// <summary>
        /// One or more entropy entry points are signaled.
        /// </summary>
        EntryPoints = 16,
    }

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
        HevcSliceSegmentHeader sliceHeader = bitstream.SliceSegments[0];
        HevcSequenceParameterSet sequenceParameterSet = sliceHeader.PictureParameterSet.SequenceParameterSet;
        using HevcPictureDecoder decoder = new(Configuration.Default, sliceHeader.PictureParameterSet);

        decoder.Decode(bitstream);

        Assert.True(sequenceParameterSet.SampleAdaptiveOffsetEnabled);
        Assert.False(sliceHeader.DeblockingFilterDisabled);
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
    /// Verifies the official Main Still Picture stream containing every luma and chroma intra mode at every
    /// conformance block size against its published native planar output.
    /// </summary>
    [Fact]
    public void DecodeOfficialIntraPredictionPictureMatchesPublishedReference()
    {
        byte[] annexB = TestFile.Create(TestImages.Heif.IntraPredictionB).Bytes;
        byte[] expectedYuv = TestFile.Create(TestImages.Heif.IntraPredictionBReference).Bytes;
        ConvertAnnexBStillPicture(annexB, 8, 1, out byte[] configurationData, out byte[] itemData);
        HevcCodecConfiguration configuration = new(configurationData);
        HevcImageItemBitstream bitstream = new(itemData, configuration);
        HevcPictureParameterSet pictureParameterSet = bitstream.SliceSegments[0].PictureParameterSet;
        HevcSequenceParameterSet sequenceParameterSet = pictureParameterSet.SequenceParameterSet;
        using HevcPictureDecoder decoder = new(Configuration.Default, pictureParameterSet);

        decoder.Decode(bitstream);

        Assert.Equal(3, configuration.GeneralProfileIdc);
        Assert.Equal(1920, sequenceParameterSet.DisplayWidth);
        Assert.Equal(1080, sequenceParameterSet.DisplayHeight);
        Assert.True(sequenceParameterSet.StrongIntraSmoothingEnabled);
        Assert.False(sequenceParameterSet.IntraSmoothingDisabled);
        Assert.False(pictureParameterSet.ConstrainedIntraPredictionEnabled);

        int expectedLength = sequenceParameterSet.DisplayWidth * sequenceParameterSet.DisplayHeight;
        int chromaWidth = GetDisplaySize(sequenceParameterSet.DisplayWidth, decoder.Picture.GetSubsamplingX(HevcPlane.Cb));
        int chromaHeight = GetDisplaySize(sequenceParameterSet.DisplayHeight, decoder.Picture.GetSubsamplingY(HevcPlane.Cb));
        expectedLength += 2 * chromaWidth * chromaHeight;
        Assert.Equal(expectedLength, expectedYuv.Length);

        int offset = 0;
        AssertPlaneEqual(decoder.Picture, sequenceParameterSet, HevcPlane.Y, expectedYuv, ref offset);
        AssertPlaneEqual(decoder.Picture, sequenceParameterSet, HevcPlane.Cb, expectedYuv, ref offset);
        AssertPlaneEqual(decoder.Picture, sequenceParameterSet, HevcPlane.Cr, expectedYuv, ref offset);
        Assert.Equal(expectedYuv.Length, offset);
    }

    /// <summary>
    /// Verifies the independently coded first picture of the official constrained-intra stream against its
    /// decoded-picture hashes while the production PPS path retains the enabled constraint.
    /// </summary>
    [Fact]
    public void DecodeOfficialConstrainedIntraPictureMatchesPublishedDigest()
    {
        byte[] annexB = TestFile.Create(TestImages.Heif.ConstrainedIntraPredictionA).Bytes;
        ConvertAnnexBStillPicture(annexB, 8, 1, out byte[] configurationData, out byte[] itemData);
        HevcCodecConfiguration configuration = new(configurationData);
        HevcImageItemBitstream bitstream = new(itemData, configuration);
        HevcPictureParameterSet pictureParameterSet = bitstream.SliceSegments[0].PictureParameterSet;
        HevcSequenceParameterSet sequenceParameterSet = pictureParameterSet.SequenceParameterSet;
        using HevcPictureDecoder decoder = new(Configuration.Default, pictureParameterSet);

        decoder.Decode(bitstream);

        Assert.Equal(1, configuration.GeneralProfileIdc);
        Assert.Equal(416, sequenceParameterSet.DisplayWidth);
        Assert.Equal(240, sequenceParameterSet.DisplayHeight);
        Assert.True(pictureParameterSet.ConstrainedIntraPredictionEnabled);
        Assert.Equal("69a20189e6bbb9c088e3adc967244ca1", GetPlaneDigest(decoder.Picture, HevcPlane.Y));
        Assert.Equal("26502d354bb123f54c20413f14360ddb", GetPlaneDigest(decoder.Picture, HevcPlane.Cb));
        Assert.Equal("baafaef47a55ae2e876862b30b3bc720", GetPlaneDigest(decoder.Picture, HevcPlane.Cr));
    }

    /// <summary>
    /// Verifies official independently coded residual-tool pictures against pinned-HM native-plane hashes.
    /// </summary>
    /// <param name="path">The official or provenance-preserving extracted Annex B picture.</param>
    /// <param name="bitDepth">The signaled component precision.</param>
    /// <param name="chromaFormat">The signaled HEVC chroma-format identifier.</param>
    /// <param name="expectedTools">The residual tools that the retained picture signals.</param>
    /// <param name="lumaDigest">The pinned-HM luma-plane digest.</param>
    /// <param name="chromaBlueDigest">The pinned-HM blue-difference-plane digest.</param>
    /// <param name="chromaRedDigest">The pinned-HM red-difference-plane digest.</param>
    [Theory]
    [InlineData(TestImages.Heif.DeltaQuantizationParameterA, 8, 1, ResidualTools.DeltaQuantization, "2b715c3517e40c00f296260fd0d591c6", "e261d9de5312cba7ac2e355a976ce062", "ca058a402db52ae33aacfcd8c73ae3c6")]
    [InlineData(TestImages.Heif.QuantizationMatrixA, 8, 3, ResidualTools.DeltaQuantization | ResidualTools.ScalingLists | ResidualTools.TransformSkip, "6995cec045398044d9cb9668d01fe295", "970394f8a6df16378a39ef2e8fbad354", "1c76949b0ee61b9c1a7d99681a324419")]
    [InlineData(TestImages.Heif.ExtendedPrecision12Bit444, 12, 3, ResidualTools.TransformSkip | ResidualTools.ExtendedPrecision, "c8664d56d8391b236a8347397eb97a08", "08cd345d8798ac2714b2939462ac45e2", "5424bce4562f298e983302da697db849")]
    [InlineData(TestImages.Heif.ChromaQuantizationAdjustment12Bit444, 12, 3, ResidualTools.TransformSkip | ResidualTools.ChromaQuantizationAdjustment, "0279d9ab84612be260dbd3d4832369b1", "b7eec690a0e5685913ac59d8121ea3e9", "64d7b590e5666f9286204e7e43c6a330")]
    [InlineData(TestImages.Heif.LosslessA, 8, 1, ResidualTools.DeltaQuantization | ResidualTools.TransformSkip | ResidualTools.TransquantizationBypass, "6d063ac9bc53ab53e142e300e668c32e", "b69a3e55ff000c0418b79471247ca73f", "1b7449f2f395578ead369f6abde7c4eb")]
    public void DecodeOfficialResidualToolsPictureMatchesPinnedHmDigest(
        string path,
        int bitDepth,
        byte chromaFormat,
        ResidualTools expectedTools,
        string lumaDigest,
        string chromaBlueDigest,
        string chromaRedDigest)
    {
        byte[] annexB = TestFile.Create(path).Bytes;
        ConvertAnnexBStillPicture(annexB, bitDepth, chromaFormat, out byte[] configurationData, out byte[] itemData);
        HevcCodecConfiguration configuration = new(configurationData);
        HevcImageItemBitstream bitstream = new(itemData, configuration);
        HevcPictureParameterSet pictureParameterSet = bitstream.SliceSegments[0].PictureParameterSet;
        HevcSequenceParameterSet sequenceParameterSet = pictureParameterSet.SequenceParameterSet;
        using HevcPictureDecoder decoder = new(Configuration.Default, pictureParameterSet);

        decoder.Decode(bitstream);

        Assert.Equal(bitDepth, decoder.Picture.BitDepthLuma);
        Assert.Equal(chromaFormat, decoder.Picture.ChromaFormat);
        Assert.Equal((expectedTools & ResidualTools.DeltaQuantization) != 0, pictureParameterSet.CodingUnitQuantizationParameterDeltaEnabled);
        Assert.Equal((expectedTools & ResidualTools.ScalingLists) != 0, sequenceParameterSet.ScalingListEnabled);
        Assert.Equal((expectedTools & ResidualTools.TransformSkip) != 0, pictureParameterSet.TransformSkipEnabled);
        Assert.Equal((expectedTools & ResidualTools.ExtendedPrecision) != 0, sequenceParameterSet.ExtendedPrecisionProcessingEnabled);
        Assert.Equal((expectedTools & ResidualTools.TransquantizationBypass) != 0, pictureParameterSet.TransquantizationBypassEnabled);
        Assert.Equal(
            (expectedTools & ResidualTools.ChromaQuantizationAdjustment) != 0,
            pictureParameterSet.ChromaQuantizationParameterOffsetsCb.Count != 0);

        Assert.Equal(lumaDigest, GetPlaneDigest(decoder.Picture, HevcPlane.Y));
        Assert.Equal(chromaBlueDigest, GetPlaneDigest(decoder.Picture, HevcPlane.Cb));
        Assert.Equal(chromaRedDigest, GetPlaneDigest(decoder.Picture, HevcPlane.Cr));
    }

    /// <summary>
    /// Verifies deblocking and sample-adaptive-offset conformance streams against native-plane digests produced by
    /// the pinned HM decoder from output that matches each archive's published checksum.
    /// </summary>
    /// <param name="path">The complete official Annex B conformance stream.</param>
    /// <param name="bitDepth">The signaled component precision.</param>
    /// <param name="chromaFormat">The signaled HEVC chroma-format identifier.</param>
    /// <param name="sampleAdaptiveOffsetEnabled">Whether the sequence enables sample-adaptive offset filtering.</param>
    /// <param name="expectedWidth">The first independently coded picture's displayed width.</param>
    /// <param name="expectedHeight">The first independently coded picture's displayed height.</param>
    /// <param name="lumaDigest">The pinned-HM luma-plane digest.</param>
    /// <param name="chromaBlueDigest">The pinned-HM blue-difference-plane digest.</param>
    /// <param name="chromaRedDigest">The pinned-HM red-difference-plane digest.</param>
    [Theory]
    [InlineData(TestImages.Heif.DeblockingA, 8, 1, false, 832, 480, "3ea2c2ef1f973345111480e7658908b3", "0390b32143b1a832a385f78229e7e574", "8388f3a8af827da46f1fb52941ec00ad")]
    [InlineData(TestImages.Heif.DeblockingMain10, 10, 1, true, 176, 144, "184a72aab144cb474df3c1a289e8692d", "d30e750dd70cae163d00f441a75896fd", "d253c41f06228df215f4616febbad34f")]
    [InlineData(TestImages.Heif.SampleAdaptiveOffsetA, 8, 1, true, 416, 240, "08723eb3fb41af96c87becc4f6973234", "230778eb7df0ebc009ca92e9697ec4d6", "e8e21ed380d2272dc38384ecd6515e53")]
    [InlineData(TestImages.Heif.SampleAdaptiveOffsetRangeExtensions, 12, 3, true, 2560, 1600, "fb342158a61b6cb3174b99d2e1167d7d", "9ff5400aac0380474882acb903f51f89", "bb320be3c7905a5a220e0066a5edb991")]
    public void DecodeOfficialLoopFilterPictureMatchesPinnedHmDigest(
        string path,
        int bitDepth,
        byte chromaFormat,
        bool sampleAdaptiveOffsetEnabled,
        int expectedWidth,
        int expectedHeight,
        string lumaDigest,
        string chromaBlueDigest,
        string chromaRedDigest)
        => ValidateOfficialLoopFilterPicture(
            path,
            bitDepth,
            chromaFormat,
            sampleAdaptiveOffsetEnabled,
            expectedWidth,
            expectedHeight,
            lumaDigest,
            chromaBlueDigest,
            chromaRedDigest);

    /// <summary>
    /// Verifies all official loop-filter pictures through every available SIMD tier and the scalar fallback.
    /// </summary>
    [Fact]
    public void DecodeOfficialLoopFilterPicturesMatchPinnedHmDigestsAcrossIntrinsicWidths()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateOfficialLoopFilterPictures, LoopFilterConfigurations);

    /// <summary>
    /// Verifies sample-adaptive-offset reconstruction with split allocator groups and balanced final disposal.
    /// </summary>
    [Fact]
    public void DecodeOfficialLoopFilterPictureWithConstrainedAllocatorMatchesPinnedHmDigest()
    {
        byte[] annexB = TestFile.Create(TestImages.Heif.SampleAdaptiveOffsetA).Bytes;
        ConvertAnnexBStillPicture(annexB, 8, 1, out byte[] configurationData, out byte[] itemData);
        HevcCodecConfiguration codecConfiguration = new(configurationData);
        HevcImageItemBitstream bitstream = new(itemData, codecConfiguration);
        TestMemoryAllocator allocator = new() { BufferCapacityInBytes = 2_048 };
        allocator.EnableNonThreadSafeLogging();
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = allocator;
        using (HevcPictureDecoder decoder = new(configuration, bitstream.SliceSegments[0].PictureParameterSet))
        {
            decoder.Decode(bitstream);

            Assert.Equal("08723eb3fb41af96c87becc4f6973234", GetPlaneDigest(decoder.Picture, HevcPlane.Y));
            Assert.Equal("230778eb7df0ebc009ca92e9697ec4d6", GetPlaneDigest(decoder.Picture, HevcPlane.Cb));
            Assert.Equal("e8e21ed380d2272dc38384ecd6515e53", GetPlaneDigest(decoder.Picture, HevcPlane.Cr));
        }

        Assert.NotEmpty(allocator.AllocationLog);
        AssertBalancedAllocations(allocator);
    }

    /// <summary>
    /// Verifies the official deblocking and sample-adaptive-offset pictures in the active intrinsic configuration.
    /// </summary>
    private static void ValidateOfficialLoopFilterPictures()
    {
        ValidateOfficialLoopFilterPicture(
            TestImages.Heif.DeblockingA,
            8,
            1,
            false,
            832,
            480,
            "3ea2c2ef1f973345111480e7658908b3",
            "0390b32143b1a832a385f78229e7e574",
            "8388f3a8af827da46f1fb52941ec00ad");

        ValidateOfficialLoopFilterPicture(
            TestImages.Heif.DeblockingMain10,
            10,
            1,
            true,
            176,
            144,
            "184a72aab144cb474df3c1a289e8692d",
            "d30e750dd70cae163d00f441a75896fd",
            "d253c41f06228df215f4616febbad34f");

        ValidateOfficialLoopFilterPicture(
            TestImages.Heif.SampleAdaptiveOffsetA,
            8,
            1,
            true,
            416,
            240,
            "08723eb3fb41af96c87becc4f6973234",
            "230778eb7df0ebc009ca92e9697ec4d6",
            "e8e21ed380d2272dc38384ecd6515e53");

        ValidateOfficialLoopFilterPicture(
            TestImages.Heif.SampleAdaptiveOffsetRangeExtensions,
            12,
            3,
            true,
            2560,
            1600,
            "fb342158a61b6cb3174b99d2e1167d7d",
            "9ff5400aac0380474882acb903f51f89",
            "bb320be3c7905a5a220e0066a5edb991");
    }

    /// <summary>
    /// Verifies one official loop-filter picture in the active intrinsic configuration.
    /// </summary>
    /// <param name="path">The complete official Annex B conformance stream.</param>
    /// <param name="bitDepth">The signaled component precision.</param>
    /// <param name="chromaFormat">The signaled HEVC chroma-format identifier.</param>
    /// <param name="sampleAdaptiveOffsetEnabled">Whether the sequence enables sample-adaptive offset filtering.</param>
    /// <param name="expectedWidth">The first independently coded picture's displayed width.</param>
    /// <param name="expectedHeight">The first independently coded picture's displayed height.</param>
    /// <param name="lumaDigest">The pinned-HM luma-plane digest.</param>
    /// <param name="chromaBlueDigest">The pinned-HM blue-difference-plane digest.</param>
    /// <param name="chromaRedDigest">The pinned-HM red-difference-plane digest.</param>
    private static void ValidateOfficialLoopFilterPicture(
        string path,
        int bitDepth,
        byte chromaFormat,
        bool sampleAdaptiveOffsetEnabled,
        int expectedWidth,
        int expectedHeight,
        string lumaDigest,
        string chromaBlueDigest,
        string chromaRedDigest)
    {
        byte[] annexB = TestFile.Create(path).Bytes;
        ConvertAnnexBStillPicture(annexB, bitDepth, chromaFormat, out byte[] configurationData, out byte[] itemData);
        HevcCodecConfiguration configuration = new(configurationData);
        HevcImageItemBitstream bitstream = new(itemData, configuration);
        HevcSliceSegmentHeader sliceHeader = bitstream.SliceSegments[0];
        HevcSequenceParameterSet sequenceParameterSet = sliceHeader.PictureParameterSet.SequenceParameterSet;
        using HevcPictureDecoder decoder = new(Configuration.Default, sliceHeader.PictureParameterSet);

        decoder.Decode(bitstream);

        Assert.Equal(expectedWidth, decoder.Picture.Width);
        Assert.Equal(expectedHeight, decoder.Picture.Height);
        Assert.Equal(bitDepth, decoder.Picture.BitDepthLuma);
        Assert.Equal(chromaFormat, decoder.Picture.ChromaFormat);
        Assert.Equal(sampleAdaptiveOffsetEnabled, sequenceParameterSet.SampleAdaptiveOffsetEnabled);
        Assert.False(sliceHeader.DeblockingFilterDisabled);
        Assert.Equal(lumaDigest, GetPlaneDigest(decoder.Picture, HevcPlane.Y));
        Assert.Equal(chromaBlueDigest, GetPlaneDigest(decoder.Picture, HevcPlane.Cb));
        Assert.Equal(chromaRedDigest, GetPlaneDigest(decoder.Picture, HevcPlane.Cr));
    }

    /// <summary>
    /// Verifies coding-tree and transform-tree conformance streams against native-plane digests produced by the
    /// pinned HM decoder from output that matches each archive's published checksum.
    /// </summary>
    /// <param name="path">The official Annex B conformance stream.</param>
    /// <param name="expectedCodingTreeBlockLog2">The expected coding-tree-block size logarithm.</param>
    /// <param name="expectedMinCodingBlockLog2">The expected minimum coding-block size logarithm.</param>
    /// <param name="expectedMinTransformBlockLog2">The expected minimum transform-block size logarithm.</param>
    /// <param name="expectedMaxTransformHierarchyDepthIntra">The expected internal intra transform-depth limit.</param>
    /// <param name="lumaDigest">The reference luma-plane MD5 digest.</param>
    /// <param name="chromaBlueDigest">The reference blue-difference-plane MD5 digest.</param>
    /// <param name="chromaRedDigest">The reference red-difference-plane MD5 digest.</param>
    [Theory]
    [InlineData(TestImages.Heif.RqtA, 6, 3, 2, 1, "adf2bfab6de808840c82f48eee31a0a5", "4b29b1d2699b77e42a4e22fb0d70993e", "8ada6d784647329a4f0da232176914f4")]
    [InlineData(TestImages.Heif.RqtB, 6, 3, 2, 2, "797f41be9a4d53b640332f9d03b1f304", "ef67e5ceff912f7aeb14f2e00ddd5cbf", "e2af5fa6f3c4bcc96dfa95cb8947a7db")]
    [InlineData(TestImages.Heif.RqtC, 6, 3, 2, 3, "5d431346cd0b3f52846fc20fc0afdcc4", "ff9355b8cc72d77edbad6f5bd4938df1", "24f8aae8c00f418af487be29d2a5a126")]
    [InlineData(TestImages.Heif.RqtD, 6, 3, 2, 4, "30138fa13664590d16355be8f7362eb2", "6198b3d1e990baadcaa533b4c44a5be2", "45f5427aec28b4f4655240812387607b")]
    [InlineData(TestImages.Heif.RqtE, 6, 3, 2, 5, "e9e182380f3209ef877b75199b546c00", "f555fd054855dc5a1cb82cb8f3393b5a", "baed53765a5424fbc38aa541917d0625")]
    [InlineData(TestImages.Heif.StructA, 4, 3, 2, 2, "bf47fb8ff96a225c2646c0539744ac93", "c105b60fd0e8740574fb76972bbc3b1a", "f9e2d8327da50734b54771df29b343bb")]
    [InlineData(TestImages.Heif.StructB, 5, 4, 2, 2, "61e13729a4e3d6fd96f5002a501d1d60", "fee8312ceaccb1f254ef21449f104e4e", "dbe684d5fffbbc2f5ca90b205018060c")]
    [InlineData(TestImages.Heif.TuSizeA, 6, 5, 4, 3, "17a84e6f516dbcb8810c7548bf6e216c", "edd3085f7a152d7ffaabae816f4942ac", "e8b47494064c1a730aca3c6ee23572c8")]
    public void DecodeOfficialTraversalPictureMatchesPinnedHmDigest(
        string path,
        int expectedCodingTreeBlockLog2,
        int expectedMinCodingBlockLog2,
        int expectedMinTransformBlockLog2,
        int expectedMaxTransformHierarchyDepthIntra,
        string lumaDigest,
        string chromaBlueDigest,
        string chromaRedDigest)
    {
        byte[] annexB = TestFile.Create(path).Bytes;
        ConvertAnnexBStillPicture(annexB, 8, 1, out byte[] configurationData, out byte[] itemData);
        HevcCodecConfiguration configuration = new(configurationData);
        HevcImageItemBitstream bitstream = new(itemData, configuration);
        HevcSequenceParameterSet sequenceParameterSet = bitstream.SliceSegments[0].PictureParameterSet.SequenceParameterSet;
        using HevcPictureDecoder decoder = new(Configuration.Default, bitstream.SliceSegments[0].PictureParameterSet);

        decoder.Decode(bitstream);

        Assert.Equal(8, decoder.Picture.BitDepthLuma);
        Assert.Equal(1, decoder.Picture.ChromaFormat);
        Assert.Equal(expectedCodingTreeBlockLog2, sequenceParameterSet.CodingTreeBlockLog2);
        Assert.Equal(expectedMinCodingBlockLog2, sequenceParameterSet.MinCodingBlockLog2);
        Assert.Equal(expectedMinTransformBlockLog2, sequenceParameterSet.MinTransformBlockLog2);
        Assert.Equal(expectedMaxTransformHierarchyDepthIntra, sequenceParameterSet.MaxTransformHierarchyDepthIntra);
        Assert.Equal(lumaDigest, GetPlaneDigest(decoder.Picture, HevcPlane.Y));
        Assert.Equal(chromaBlueDigest, GetPlaneDigest(decoder.Picture, HevcPlane.Cb));
        Assert.Equal(chromaRedDigest, GetPlaneDigest(decoder.Picture, HevcPlane.Cr));
    }

    /// <summary>
    /// Verifies the complete sequential decoder path for HEVC tiles, wavefront entry points, dependent slice
    /// segments, and slice-header extensions against native-plane digests from published or pinned-HM output.
    /// </summary>
    /// <param name="path">The official Annex B conformance stream.</param>
    /// <param name="expectedTools">The parallelization syntax that the retained picture signals.</param>
    /// <param name="expectedCodingTreeBlockLog2">The expected coding-tree-block size logarithm.</param>
    /// <param name="expectedCodingTreeBlockWidth">The expected picture width in coding-tree blocks.</param>
    /// <param name="expectedTileColumns">The exact tile-column count, or zero when only a multi-tile assertion applies.</param>
    /// <param name="expectedTileRows">The exact tile-row count, or zero when only a multi-tile assertion applies.</param>
    /// <param name="lumaDigest">The pinned-HM or published luma-plane MD5 digest.</param>
    /// <param name="chromaBlueDigest">The pinned-HM or published blue-difference-plane MD5 digest.</param>
    /// <param name="chromaRedDigest">The pinned-HM or published red-difference-plane MD5 digest.</param>
    [Theory]
    [InlineData(TestImages.Heif.DependentSlicesA, ParallelizationTools.DependentSliceSegments, 6, 30, 1, 1, "00dc01343ab9dc53c078344d7f77dab1", "899538536f2b327d84894c947f87bbc2", "a1bc8421c5a72ce2792b40847e95d438")]
    [InlineData(TestImages.Heif.DependentSlicesB, ParallelizationTools.DependentSliceSegments | ParallelizationTools.Wavefront, 6, 30, 1, 1, "d048cfe1b7f0e6a6e3689733914caa19", "d7400a314011173564516b81407c3f42", "7bddfaa6d440f8490ec88b94fdbac706")]
    [InlineData(TestImages.Heif.DependentSlicesC, ParallelizationTools.DependentSliceSegments | ParallelizationTools.Tiles, 6, 30, 0, 0, "a8c96c581d9de294a4fe798cede17817", "afb4cdebbbb31edfeab504d64c779895", "e593a80b1f17724f930728068993c06c")]
    [InlineData(TestImages.Heif.TilesA, ParallelizationTools.Tiles | ParallelizationTools.EntryPoints, 6, 30, 5, 5, "6828e4b27ab4fda31fe3b8bcdb3bccef", "1f899aff0a453d133de048232d3ee1c0", "87ca938e4a19cd289bdaa85023c704c5")]
    [InlineData(TestImages.Heif.TilesB, ParallelizationTools.Tiles | ParallelizationTools.EntryPoints, 6, 30, 5, 5, "aa44a1bf0f77f5a78e514eab3621aab2", "53a29305bb7b60dcd3a0082ce5aed9f4", "0e7ad4ea85eedf8fa06ac9b366323175")]
    [InlineData(TestImages.Heif.WavefrontA, ParallelizationTools.Wavefront | ParallelizationTools.SliceHeaderExtensions | ParallelizationTools.EntryPoints, 6, 7, 1, 1, "69bd520cd6b017b49144275f1c3b498c", "33bb1c6216561f30fbefb89ce87c0956", "1f88fd804c87d8f5c8c72cee4043d140")]
    [InlineData(TestImages.Heif.WavefrontB, ParallelizationTools.Wavefront | ParallelizationTools.SliceHeaderExtensions | ParallelizationTools.EntryPoints, 5, 13, 1, 1, "ff78fcf56cf449c195708626a975e870", "b230844124f07aad4102aa21e2fc0f15", "e10c05f8c4007b14ddc6a7cf858f374e")]
    [InlineData(TestImages.Heif.WavefrontC, ParallelizationTools.Wavefront | ParallelizationTools.SliceHeaderExtensions | ParallelizationTools.EntryPoints, 4, 26, 1, 1, "d55877b038bbe2af6a4b35eeff27b26f", "4e1145cc891c295543407b6c62c7ad55", "8fa9c17216a9b02a65582c322206216b")]
    [InlineData(TestImages.Heif.WavefrontD, ParallelizationTools.Wavefront | ParallelizationTools.SliceHeaderExtensions | ParallelizationTools.EntryPoints, 6, 1, 1, 1, "ab7c74b80340e5bde0858276f11a37ed", "4fe6b63cfe5656bf88912ccf9caeb85a", "3dbb149d5b90a49cf72d4e2d5fbb0511")]
    [InlineData(TestImages.Heif.WavefrontE, ParallelizationTools.Wavefront | ParallelizationTools.SliceHeaderExtensions | ParallelizationTools.EntryPoints, 6, 2, 1, 1, "d2b8cd7d9e7baf4dd3e383ba8fef3c22", "315f19843d4e637dc41f964c4adff626", "07f619c6aedd51a04ba99d2bfc0ecb2c")]
    [InlineData(TestImages.Heif.WavefrontF, ParallelizationTools.Wavefront | ParallelizationTools.SliceHeaderExtensions | ParallelizationTools.EntryPoints, 6, 3, 1, 1, "797335a8e293c6ce6dd87fde6f797f07", "77f74454394860093d4b18ae9ae793fe", "e99ff04282a3457a44685378008ff86a")]
    [InlineData(TestImages.Heif.EntryPointsA, ParallelizationTools.Tiles | ParallelizationTools.EntryPoints, 6, 30, 2, 2, "ea26d532556e6b71b369b41def35af93", "295a5552a8152035a1cae2405a690251", "1d81ff340ac8a37d75f7782ca140cae3")]
    [InlineData(TestImages.Heif.EntryPointsC, ParallelizationTools.Wavefront | ParallelizationTools.EntryPoints, 6, 30, 1, 1, "81b087fcf7df2626c7592ec5d39ec1fc", "93bd06e1a216a388568c069ead14f98e", "f0446d71e2061a8c3a6281fede767d81")]
    public void DecodeOfficialParallelizationPictureMatchesPublishedReference(
        string path,
        ParallelizationTools expectedTools,
        int expectedCodingTreeBlockLog2,
        int expectedCodingTreeBlockWidth,
        int expectedTileColumns,
        int expectedTileRows,
        string lumaDigest,
        string chromaBlueDigest,
        string chromaRedDigest)
    {
        byte[] annexB = TestFile.Create(path).Bytes;
        ConvertAnnexBStillPicture(annexB, 8, 1, out byte[] configurationData, out byte[] itemData);
        HevcCodecConfiguration configuration = new(configurationData);
        HevcImageItemBitstream bitstream = new(itemData, configuration);
        HevcPictureParameterSet pictureParameterSet = bitstream.SliceSegments[0].PictureParameterSet;
        HevcSequenceParameterSet sequenceParameterSet = pictureParameterSet.SequenceParameterSet;
        using HevcPictureDecoder decoder = new(Configuration.Default, pictureParameterSet);

        decoder.Decode(bitstream);

        bool hasDependentSliceSegments = false;
        bool hasEntryPoints = false;
        foreach (HevcSliceSegmentHeader sliceSegment in bitstream.SliceSegments)
        {
            hasDependentSliceSegments |= sliceSegment.DependentSliceSegment;
            hasEntryPoints |= sliceSegment.EntryPointOffsets.Count != 0;
        }

        bool expectsTiles = (expectedTools & ParallelizationTools.Tiles) != 0;
        bool expectsWavefront = (expectedTools & ParallelizationTools.Wavefront) != 0;
        int codingTreeBlockSize = 1 << sequenceParameterSet.CodingTreeBlockLog2;
        int codingTreeBlockWidth = (sequenceParameterSet.Width + codingTreeBlockSize - 1) / codingTreeBlockSize;

        Assert.Equal((expectedTools & ParallelizationTools.DependentSliceSegments) != 0, hasDependentSliceSegments);
        Assert.Equal(expectsTiles, pictureParameterSet.TilesEnabled);
        Assert.Equal(expectsWavefront, pictureParameterSet.EntropyCodingSynchronizationEnabled);
        Assert.Equal((expectedTools & ParallelizationTools.SliceHeaderExtensions) != 0, pictureParameterSet.SliceSegmentHeaderExtensionPresent);
        Assert.Equal((expectedTools & ParallelizationTools.EntryPoints) != 0, hasEntryPoints);
        Assert.Equal(expectedCodingTreeBlockLog2, sequenceParameterSet.CodingTreeBlockLog2);
        Assert.Equal(expectedCodingTreeBlockWidth, codingTreeBlockWidth);

        if (expectedTileColumns == 0)
        {
            Assert.True(pictureParameterSet.TileColumnWidths.Count > 1);
            Assert.True(pictureParameterSet.TileRowHeights.Count > 1);
        }
        else
        {
            Assert.Equal(expectedTileColumns, pictureParameterSet.TileColumnWidths.Count);
            Assert.Equal(expectedTileRows, pictureParameterSet.TileRowHeights.Count);
        }

        Assert.Equal(lumaDigest, GetPlaneDigest(decoder.Picture, HevcPlane.Y));
        Assert.Equal(chromaBlueDigest, GetPlaneDigest(decoder.Picture, HevcPlane.Cb));
        Assert.Equal(chromaRedDigest, GetPlaneDigest(decoder.Picture, HevcPlane.Cr));
    }

    /// <summary>
    /// Verifies the dependent-slice tile and wavefront combinations through split allocator groups with balanced
    /// final disposal.
    /// </summary>
    /// <param name="path">The official Annex B conformance stream.</param>
    /// <param name="lumaDigest">The pinned-HM luma-plane MD5 digest.</param>
    /// <param name="chromaBlueDigest">The pinned-HM blue-difference-plane MD5 digest.</param>
    /// <param name="chromaRedDigest">The pinned-HM red-difference-plane MD5 digest.</param>
    [Theory]
    [InlineData(TestImages.Heif.DependentSlicesB, "d048cfe1b7f0e6a6e3689733914caa19", "d7400a314011173564516b81407c3f42", "7bddfaa6d440f8490ec88b94fdbac706")]
    [InlineData(TestImages.Heif.DependentSlicesC, "a8c96c581d9de294a4fe798cede17817", "afb4cdebbbb31edfeab504d64c779895", "e593a80b1f17724f930728068993c06c")]
    public void DecodeOfficialParallelizationPicturesWithConstrainedAllocatorMatchPinnedHmDigest(
        string path,
        string lumaDigest,
        string chromaBlueDigest,
        string chromaRedDigest)
    {
        byte[] annexB = TestFile.Create(path).Bytes;
        ConvertAnnexBStillPicture(annexB, 8, 1, out byte[] configurationData, out byte[] itemData);
        HevcCodecConfiguration codecConfiguration = new(configurationData);
        HevcImageItemBitstream bitstream = new(itemData, codecConfiguration);
        TestMemoryAllocator allocator = new() { BufferCapacityInBytes = 4_096 };
        allocator.EnableNonThreadSafeLogging();
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = allocator;
        using (HevcPictureDecoder decoder = new(configuration, bitstream.SliceSegments[0].PictureParameterSet))
        {
            decoder.Decode(bitstream);

            Assert.Equal(lumaDigest, GetPlaneDigest(decoder.Picture, HevcPlane.Y));
            Assert.Equal(chromaBlueDigest, GetPlaneDigest(decoder.Picture, HevcPlane.Cb));
            Assert.Equal(chromaRedDigest, GetPlaneDigest(decoder.Picture, HevcPlane.Cr));
        }

        Assert.NotEmpty(allocator.AllocationLog);
        AssertBalancedAllocations(allocator);
    }

    /// <summary>
    /// Verifies that entry-point byte lengths exclude emulation-prevention bytes from the decoded substream.
    /// </summary>
    [Fact]
    public void EntryPointOffsetsExcludeEmulationPreventionBytes()
    {
        ReadOnlySpan<int> preventionBytePositions = [3, 8, 14];

        const int DecodedHeaderLength = 4;
        int encodedHeaderLength = HevcSliceSegmentHeader.GetEncodedPayloadOffset(
            DecodedHeaderLength,
            preventionBytePositions);

        const int EncodedSubstreamLength = 11;
        int decodedBoundary = HevcSliceSegmentHeader.GetDecodedPayloadOffset(
            encodedHeaderLength + EncodedSubstreamLength,
            preventionBytePositions);

        Assert.Equal(5, encodedHeaderLength);
        Assert.Equal(13, decodedBoundary);
        Assert.Equal(9, decodedBoundary - DecodedHeaderLength);
    }

    /// <summary>
    /// Verifies every supported prefix SEI payload against the field ordering and fixed-point units read by pinned HM.
    /// </summary>
    [Fact]
    public void SupplementalEnhancementInformationReadsPinnedHmSyntax()
    {
        HevcSupplementalEnhancementInformation supplementalEnhancementInformation = new();
        supplementalEnhancementInformation.ReadPrefixNalUnit(SupplementalMetadataRbsp);
        supplementalEnhancementInformation.ReadPrefixNalUnit(DisplayOrientationRbsp);

        Assert.True(supplementalEnhancementInformation.HasDisplayOrientation);
        Assert.True(supplementalEnhancementInformation.HorizontalFlip);
        Assert.False(supplementalEnhancementInformation.VerticalFlip);
        Assert.Equal((ushort)16384, supplementalEnhancementInformation.AnticlockwiseRotation);
        Assert.Equal((byte)CicpTransferCharacteristics.SmpteSt2084, supplementalEnhancementInformation.PreferredTransferCharacteristics);

        HeifContentLightLevel contentLightLevel = supplementalEnhancementInformation.ContentLightLevel.Value;
        Assert.Equal((ushort)1000, contentLightLevel.MaximumContentLightLevel);
        Assert.Equal((ushort)400, contentLightLevel.MaximumPictureAverageLightLevel);

        HeifMasteringDisplayColorVolume masteringDisplayColorVolume
            = supplementalEnhancementInformation.MasteringDisplayColorVolume.Value;

        Assert.Equal(0.64F, masteringDisplayColorVolume.Primaries.R.X, 5);
        Assert.Equal(0.32F, masteringDisplayColorVolume.Primaries.R.Y, 5);
        Assert.Equal(3375.2069D, masteringDisplayColorVolume.MaximumLuminance, 4);
        Assert.Equal(1690.906D, masteringDisplayColorVolume.MinimumLuminance, 4);

        HeifAmbientViewingEnvironment ambientViewingEnvironment
            = supplementalEnhancementInformation.AmbientViewingEnvironment.Value;

        Assert.Equal(100D, ambientViewingEnvironment.Illuminance);
        Assert.Equal(0.3127F, ambientViewingEnvironment.AmbientLight.X, 5);
        Assert.Equal(0.329F, ambientViewingEnvironment.AmbientLight.Y, 5);

        HeifContentColorVolume contentColorVolume = supplementalEnhancementInformation.ContentColorVolume.Value;
        Assert.Null(contentColorVolume.Primaries);
        Assert.Equal(0.1D, contentColorVolume.MinimumLuminance.Value, 5);
        Assert.Equal(0.5D, contentColorVolume.MaximumLuminance.Value, 5);
        Assert.Equal(0.3D, contentColorVolume.AverageLuminance.Value, 5);

        ReadOnlySpan<byte> cancellationRbsp =
        [
            0x2F, 0x01, 0xC0,
            0x95, 0x01, 0xC0,
            0x80
        ];

        supplementalEnhancementInformation.ReadPrefixNalUnit(cancellationRbsp);

        Assert.False(supplementalEnhancementInformation.HasDisplayOrientation);
        Assert.Null(supplementalEnhancementInformation.ContentColorVolume);
        Assert.NotNull(supplementalEnhancementInformation.ContentLightLevel);
    }

    /// <summary>
    /// Verifies malformed SEI framing and payload trailing bits fail at the bounded NAL boundary.
    /// </summary>
    [Fact]
    public void SupplementalEnhancementInformationRejectsMalformedPayloads()
    {
        byte[] emptyRbsp = [];
        byte[] truncatedHeader = [0xFF, 0x80];
        byte[] truncatedPayload = [0x90, 0x04, 0x03, 0xE8, 0x80];
        byte[] missingPayloadMarker = [0x2F, 0x03, 0x48, 0x00, 0x00, 0x80];

        Assert.Throws<InvalidImageContentException>(
            () => new HevcSupplementalEnhancementInformation().ReadPrefixNalUnit(emptyRbsp));

        Assert.Throws<InvalidImageContentException>(
            () => new HevcSupplementalEnhancementInformation().ReadPrefixNalUnit(truncatedHeader));

        Assert.Throws<InvalidImageContentException>(
            () => new HevcSupplementalEnhancementInformation().ReadPrefixNalUnit(truncatedPayload));

        Assert.Throws<InvalidImageContentException>(
            () => new HevcSupplementalEnhancementInformation().ReadPrefixNalUnit(missingPayloadMarker));
    }

    /// <summary>
    /// Verifies HEVC item metadata and complete HEIF presentation under normal and scalar execution while every
    /// constrained allocator group is returned exactly once.
    /// </summary>
    [Fact]
    public void DecodeSupplementalPresentationAndMetadataAcrossIntrinsicWidths()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateSupplementalPresentationAndMetadata,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Verifies no-display and conflicting item-property metadata are rejected by the complete item decoder.
    /// </summary>
    [Fact]
    public void DecodeRejectsNonDisplayAndConflictingSupplementalMetadata()
    {
        byte[] annexB = TestFile.Create(TestImages.Heif.IntraPredictionB).Bytes;
        ConvertAnnexBStillPicture(annexB, 8, 1, out byte[] configurationData, out byte[] itemData);
        HevcCodecConfiguration codecConfiguration = new(configurationData);
        HeifItem item = new(Heif4CharCode.Hvc1, 1) { HevcCodecConfiguration = codecConfiguration };
        HevcHeifItemDecoder<Rgba32> itemDecoder = new();
        DecoderOptions options = new();
        ReadOnlySpan<byte> noDisplayRbsp = [0x87, 0x00, 0x80];

        byte[] noDisplayItemData = PrependPrefixSeiNalUnit(itemData, noDisplayRbsp);
        Assert.Throws<InvalidImageContentException>(() =>
        {
            using Image<Rgba32> image = itemDecoder.DecodeItemData(
                options,
                item,
                noDisplayItemData,
                null,
                TestContext.Current.CancellationToken);
        });

        item.ContentLightLevel = new HeifContentLightLevel(1, 2);
        byte[] supplementalItemData = PrependPrefixSeiNalUnit(itemData, SupplementalMetadataRbsp);
        Assert.Throws<InvalidImageContentException>(() =>
        {
            using Image<Rgba32> image = itemDecoder.DecodeItemData(
                options,
                item,
                supplementalItemData,
                null,
                TestContext.Current.CancellationToken);
        });

        int prefixNalLength = supplementalItemData.Length - itemData.Length;
        byte[] postVclPrefixSeiItemData = new byte[supplementalItemData.Length];
        itemData.CopyTo(postVclPrefixSeiItemData, 0);
        supplementalItemData.AsSpan(0, prefixNalLength).CopyTo(postVclPrefixSeiItemData.AsSpan(itemData.Length));
        Assert.Throws<InvalidImageContentException>(
            () => new HevcImageItemBitstream(postVclPrefixSeiItemData, codecConfiguration));
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
    /// Verifies that every possible allocator failure during decoder construction releases all earlier owners.
    /// </summary>
    [Fact]
    public void ConstructorFailureReleasesEveryEarlierAllocation()
    {
        byte[] configurationData = TestFile.Create(TestImages.Heif.Image1TileHvcConfiguration).Bytes;
        byte[] itemData = TestFile.Create(TestImages.Heif.Image1Tile1Payload).Bytes;
        HevcCodecConfiguration codecConfiguration = new(configurationData);
        HevcImageItemBitstream bitstream = new(itemData, codecConfiguration);
        HevcPictureParameterSet pictureParameterSet = bitstream.SliceSegments[0].PictureParameterSet;

        FailingTestMemoryAllocator successfulAllocator = new(int.MaxValue);
        Configuration successfulConfiguration = Configuration.Default.Clone();
        successfulConfiguration.MemoryAllocator = successfulAllocator;
        using (new HevcPictureDecoder(successfulConfiguration, pictureParameterSet))
        {
        }

        int allocationCount = successfulAllocator.AllocationAttemptCount;
        Assert.True(allocationCount > 0);
        AssertBalancedAllocations(successfulAllocator);

        for (int failureAllocationNumber = 1; failureAllocationNumber <= allocationCount; failureAllocationNumber++)
        {
            FailingTestMemoryAllocator allocator = new(failureAllocationNumber);
            Configuration configuration = Configuration.Default.Clone();
            configuration.MemoryAllocator = allocator;

            Assert.Throws<InvalidMemoryOperationException>(
                () => new HevcPictureDecoder(configuration, pictureParameterSet));

            Assert.Equal(failureAllocationNumber, allocator.AllocationAttemptCount);
            Assert.Equal(failureAllocationNumber - 1, allocator.AllocationLog.Count);
            AssertBalancedAllocations(allocator);
        }
    }

    /// <summary>
    /// Verifies successful production reconstruction with split allocator groups and balanced final disposal.
    /// </summary>
    [Fact]
    public void DecodeWithConstrainedAllocatorReleasesEveryAllocation()
    {
        byte[] configurationData = TestFile.Create(TestImages.Heif.Image1TileHvcConfiguration).Bytes;
        byte[] itemData = TestFile.Create(TestImages.Heif.Image1Tile1Payload).Bytes;
        HevcCodecConfiguration codecConfiguration = new(configurationData);
        HevcImageItemBitstream bitstream = new(itemData, codecConfiguration);
        TestMemoryAllocator allocator = new() { BufferCapacityInBytes = 2_048 };
        allocator.EnableNonThreadSafeLogging();
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = allocator;
        using (HevcPictureDecoder decoder = new(configuration, bitstream.SliceSegments[0].PictureParameterSet))
        {
            decoder.Decode(bitstream);
        }

        Assert.NotEmpty(allocator.AllocationLog);
        AssertBalancedAllocations(allocator);
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

        string message =
            $"{plane} contained {mismatchCount} differing samples. The maximum difference was {maximumDifference}; " +
            $"the mismatches span ({minimumMismatchX}, {minimumMismatchY}) through ({maximumMismatchX}, {maximumMismatchY}), " +
            $"and the first mismatch at ({firstMismatchX}, {firstMismatchY}) was {firstActual}, expected {firstExpected}.";

        Assert.True(mismatchCount == 0, message);
    }

    /// <summary>
    /// Converts a luma display extent to the selected component extent.
    /// </summary>
    /// <param name="lumaSize">The displayed luma extent.</param>
    /// <param name="subsampling">The component subsampling shift.</param>
    /// <returns>The displayed component extent.</returns>
    private static int GetDisplaySize(int lumaSize, int subsampling) => (lumaSize + (1 << subsampling) - 1) >> subsampling;

    /// <summary>
    /// Verifies HEVC item metadata and the complete public HEIF presentation path in the active intrinsic
    /// configuration.
    /// </summary>
    private static void ValidateSupplementalPresentationAndMetadata()
    {
        TestMemoryAllocator allocator = new() { BufferCapacityInBytes = 8_192 };
        allocator.EnableNonThreadSafeLogging();
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = allocator;
        DecoderOptions options = new() { Configuration = configuration };

        // FeatureTestRunner executes this method outside the originating xUnit context when it disables
        // intrinsics, so the remote process cannot obtain the test's cancellation token.
        CancellationToken cancellationToken = CancellationToken.None;
        byte[] annexB = TestFile.Create(TestImages.Heif.IntraPredictionB).Bytes;
        ConvertAnnexBStillPicture(annexB, 8, 1, out byte[] configurationData, out byte[] itemData);
        HevcCodecConfiguration codecConfiguration = new(configurationData);
        HeifItem item = new(Heif4CharCode.Hvc1, 1) { HevcCodecConfiguration = codecConfiguration };
        byte[] supplementalItemData = PrependPrefixSeiNalUnit(itemData, SupplementalMetadataRbsp);
        HevcHeifItemDecoder<Rgba32> itemDecoder = new();
        using (Image<Rgba32> metadataImage = itemDecoder.DecodeItemData(
            options,
            item,
            supplementalItemData,
            null,
            cancellationToken))
        {
            Assert.Equal(CicpTransferCharacteristics.SmpteSt2084, metadataImage.Metadata.CicpProfile.TransferCharacteristics);
            HeifMetadata metadata = metadataImage.Metadata.GetHeifMetadata();
            Assert.Equal((ushort)1000, metadata.ContentLightLevel.Value.MaximumContentLightLevel);
            Assert.NotNull(metadata.MasteringDisplayColorVolume);
            Assert.NotNull(metadata.ContentColorVolume);
            Assert.NotNull(metadata.AmbientViewingEnvironment);
        }

        byte[] source = [.. TestFile.Create(TestImages.Heif.Image4).Bytes];
        byte[] orientedContainer = InsertPrimaryItemPrefixSeiNalUnit(source, DisplayOrientationRbsp);
        string referencePath = Path.Combine(
            TestEnvironment.ReferenceOutputDirectoryFullPath,
            "HeifDecoderTests",
            "DecodeHevcStillImage_Rgba32_image4.png");

        using (Image<Rgba32> baseline = Image.Load<Rgba32>(referencePath))
        using (Image<Rgba32> actual = Image.Load<Rgba32>(options, orientedContainer))
        {
            Assert.Equal(baseline.Height, actual.Width);
            Assert.Equal(baseline.Width, actual.Height);
            ImageFrame<Rgba32> baselineFrame = baseline.Frames.RootFrame;
            ImageFrame<Rgba32> actualFrame = actual.Frames.RootFrame;

            // A horizontal flip followed by the signaled anticlockwise quarter turn is an exact transpose. Compare
            // every RGBA sample directly so both color and auxiliary alpha must share the production transform.
            for (int y = 0; y < actual.Height; y++)
            {
                ReadOnlySpan<Rgba32> actualRow = actualFrame.DangerousGetPixelRowMemory(y).Span;
                for (int x = 0; x < actual.Width; x++)
                {
                    Assert.Equal(baselineFrame.DangerousGetPixelRowMemory(x).Span[y], actualRow[x]);
                }
            }
        }

        Assert.NotEmpty(allocator.AllocationLog);
        AssertBalancedAllocations(allocator);
    }

    /// <summary>
    /// Inserts one prefix SEI NAL unit at the start of the real fixture's primary file-relative extent.
    /// </summary>
    private static byte[] InsertPrimaryItemPrefixSeiNalUnit(byte[] container, ReadOnlySpan<byte> rbsp)
    {
        int metaOffset = FindBoxOffset(container, Heif4CharCode.Meta, 0, container.Length);
        Assert.True(metaOffset >= 0);
        int metaSize = (int)BinaryPrimitives.ReadUInt32BigEndian(container.AsSpan(metaOffset));
        int primaryItemOffset = FindBoxOffset(container, Heif4CharCode.Pitm, metaOffset + 12, metaSize - 12);
        int itemLocationOffset = FindBoxOffset(container, Heif4CharCode.Iloc, metaOffset + 12, metaSize - 12);
        int mediaDataOffset = FindBoxOffset(container, Heif4CharCode.Mdat, 0, container.Length);
        Assert.True(primaryItemOffset >= 0);
        Assert.True(itemLocationOffset >= 0);
        Assert.True(mediaDataOffset >= 0);

        Assert.Equal(0, container[primaryItemOffset + 8]);
        ushort primaryItemId = BinaryPrimitives.ReadUInt16BigEndian(container.AsSpan(primaryItemOffset + 12));

        int position = itemLocationOffset + 8;
        Assert.Equal(0, container[position]);
        position += 4;
        byte fieldSizes = container[position++];
        byte baseOffsetSizes = container[position++];
        Assert.Equal(4, fieldSizes >> 4);
        Assert.Equal(4, fieldSizes & 15);
        Assert.Equal(0, baseOffsetSizes >> 4);
        ushort itemCount = BinaryPrimitives.ReadUInt16BigEndian(container.AsSpan(position));
        position += 2;
        Assert.True(itemCount > 0);

        ushort firstItemId = BinaryPrimitives.ReadUInt16BigEndian(container.AsSpan(position));
        position += 2;
        Assert.Equal(primaryItemId, firstItemId);
        Assert.Equal(0, BinaryPrimitives.ReadUInt16BigEndian(container.AsSpan(position)));
        position += 2;
        ushort primaryExtentCount = BinaryPrimitives.ReadUInt16BigEndian(container.AsSpan(position));
        position += 2;
        Assert.Equal(1, primaryExtentCount);

        int primaryOffsetField = position;
        int insertionOffset = (int)BinaryPrimitives.ReadUInt32BigEndian(container.AsSpan(position));
        position += 4;
        int primaryLengthField = position;
        uint primaryLength = BinaryPrimitives.ReadUInt32BigEndian(container.AsSpan(position));
        position += 4;

        byte[] prefixNalUnit = PrependPrefixSeiNalUnit([], rbsp);
        BinaryPrimitives.WriteUInt32BigEndian(
            container.AsSpan(primaryLengthField),
            checked(primaryLength + (uint)prefixNalUnit.Length));

        // The fixture stores every extent as an absolute file offset. Inserting into the first extent shifts only
        // later extents; its own offset remains the exact start at which the prefix NAL is inserted.
        for (int itemIndex = 1; itemIndex < itemCount; itemIndex++)
        {
            position += 4;
            ushort extentCount = BinaryPrimitives.ReadUInt16BigEndian(container.AsSpan(position));
            position += 2;
            for (int extentIndex = 0; extentIndex < extentCount; extentIndex++)
            {
                int extentOffsetField = position;
                uint extentOffset = BinaryPrimitives.ReadUInt32BigEndian(container.AsSpan(position));
                position += 8;
                if (extentOffset > insertionOffset)
                {
                    BinaryPrimitives.WriteUInt32BigEndian(
                        container.AsSpan(extentOffsetField),
                        checked(extentOffset + (uint)prefixNalUnit.Length));
                }
            }
        }

        Assert.Equal(
            (uint)insertionOffset,
            BinaryPrimitives.ReadUInt32BigEndian(container.AsSpan(primaryOffsetField)));

        uint compactMediaDataSize = BinaryPrimitives.ReadUInt32BigEndian(container.AsSpan(mediaDataOffset));
        ulong mediaDataSize = compactMediaDataSize == 1
            ? BinaryPrimitives.ReadUInt64BigEndian(container.AsSpan(mediaDataOffset + 8))
            : compactMediaDataSize;

        if (compactMediaDataSize == 1)
        {
            BinaryPrimitives.WriteUInt64BigEndian(
                container.AsSpan(mediaDataOffset + 8),
                checked(mediaDataSize + (uint)prefixNalUnit.Length));
        }
        else
        {
            BinaryPrimitives.WriteUInt32BigEndian(
                container.AsSpan(mediaDataOffset),
                checked((uint)mediaDataSize + (uint)prefixNalUnit.Length));
        }

        byte[] result = new byte[container.Length + prefixNalUnit.Length];
        container.AsSpan(0, insertionOffset).CopyTo(result);
        prefixNalUnit.CopyTo(result.AsSpan(insertionOffset));
        container.AsSpan(insertionOffset).CopyTo(result.AsSpan(insertionOffset + prefixNalUnit.Length));
        return result;
    }

    /// <summary>
    /// Finds a bounded ISO BMFF child box, including boxes that use a 64-bit extended size.
    /// </summary>
    private static int FindBoxOffset(ReadOnlySpan<byte> data, Heif4CharCode type, int offset, int length)
    {
        int endOffset = offset + length;
        while (offset < endOffset)
        {
            uint compactSize = BinaryPrimitives.ReadUInt32BigEndian(data[offset..]);
            ulong boxSize = compactSize == 1
                ? BinaryPrimitives.ReadUInt64BigEndian(data[(offset + 8)..])
                : compactSize;

            Assert.InRange(boxSize, 8UL, (ulong)(endOffset - offset));
            Heif4CharCode boxType = (Heif4CharCode)BinaryPrimitives.ReadUInt32BigEndian(data[(offset + 4)..]);
            if (boxType == type)
            {
                return offset;
            }

            offset += (int)boxSize;
        }

        return -1;
    }

    /// <summary>
    /// Prepends one valid prefix SEI NAL unit to a length-delimited HEVC item payload.
    /// </summary>
    private static byte[] PrependPrefixSeiNalUnit(ReadOnlySpan<byte> itemData, ReadOnlySpan<byte> rbsp)
    {
        int preventionByteCount = 0;
        int consecutiveZeroBytes = 0;
        foreach (byte value in rbsp)
        {
            if (consecutiveZeroBytes == 2 && value <= 3)
            {
                preventionByteCount++;
                consecutiveZeroBytes = 0;
            }

            consecutiveZeroBytes = value == 0 ? consecutiveZeroBytes + 1 : 0;
        }

        const int lengthFieldLength = 4;
        const int nalHeaderLength = 2;
        int nalLength = nalHeaderLength + rbsp.Length + preventionByteCount;
        byte[] result = new byte[lengthFieldLength + nalLength + itemData.Length];
        BinaryPrimitives.WriteUInt32BigEndian(result, (uint)nalLength);
        result[lengthFieldLength] = 0x4E;
        result[lengthFieldLength + 1] = 0x01;
        int destinationOffset = lengthFieldLength + nalHeaderLength;
        consecutiveZeroBytes = 0;
        foreach (byte value in rbsp)
        {
            if (consecutiveZeroBytes == 2 && value <= 3)
            {
                result[destinationOffset++] = 3;
                consecutiveZeroBytes = 0;
            }

            result[destinationOffset++] = value;
            consecutiveZeroBytes = value == 0 ? consecutiveZeroBytes + 1 : 0;
        }

        itemData.CopyTo(result.AsSpan(destinationOffset));
        return result;
    }

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

    /// <summary>
    /// Verifies that every tracked allocation was returned exactly once.
    /// </summary>
    /// <param name="allocator">The allocator whose ownership log is complete.</param>
    private static void AssertBalancedAllocations(TestMemoryAllocator allocator)
    {
        Assert.Equal(allocator.AllocationLog.Count, allocator.ReturnLog.Count);
        foreach (TestMemoryAllocator.AllocationRequest allocation in allocator.AllocationLog)
        {
            Assert.Single(
                allocator.ReturnLog,
                returned => returned.AllocationId == allocation.AllocationId);
        }
    }

    /// <summary>
    /// Provides tracked owners until the configured allocation attempt fails.
    /// </summary>
    private sealed class FailingTestMemoryAllocator : TestMemoryAllocator
    {
        private readonly int failureAllocationNumber;
        private int allocationAttemptCount;

        /// <summary>
        /// Initializes a new instance of the <see cref="FailingTestMemoryAllocator"/> class.
        /// </summary>
        /// <param name="failureAllocationNumber">The one-based allocation attempt that must fail.</param>
        public FailingTestMemoryAllocator(int failureAllocationNumber)
        {
            this.failureAllocationNumber = failureAllocationNumber;
            this.EnableNonThreadSafeLogging();
        }

        /// <summary>
        /// Gets the number of backing-owner allocation attempts.
        /// </summary>
        public int AllocationAttemptCount => this.allocationAttemptCount;

        /// <inheritdoc/>
        protected override AllocationTrackedMemoryManager<T> AllocateCore<T>(
            int length,
            AllocationOptions options = AllocationOptions.None)
        {
            this.allocationAttemptCount++;
            if (this.allocationAttemptCount == this.failureAllocationNumber)
            {
                // Fail before delegation so the failed attempt never creates an owner that needs rollback.
                throw new InvalidMemoryOperationException("The configured HEVC allocation failed.");
            }

            return base.AllocateCore<T>(length, options);
        }
    }
}
