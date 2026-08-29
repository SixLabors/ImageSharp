// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Security.Cryptography;
using SixLabors.ImageSharp.Formats.Heif.Hevc;
using SixLabors.ImageSharp.Memory;
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
