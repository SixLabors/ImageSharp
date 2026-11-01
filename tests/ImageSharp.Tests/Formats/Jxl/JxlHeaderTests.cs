// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Security.Cryptography;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jxl;
using SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;
using SixLabors.ImageSharp.Formats.Jxl.Components.Entropy;
using SixLabors.ImageSharp.Formats.Jxl.Components.Modular;
using SixLabors.ImageSharp.Formats.Jxl.Components.VarDct;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.Memory;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Jxl;

[Trait("Format", "Jxl")]
[ValidateDisposedMemoryAllocations]
public class JxlHeaderTests
{
    /// <summary>
    /// Verifies DC-global parsing against both references and the first DC group's samples against libjxl.
    /// </summary>
    /// <param name="file">The unchanged reference input with one combined frame section.</param>
    /// <param name="globalScale">The encoded global scale reported by both references.</param>
    /// <param name="dcQuantizer">The encoded DC quantizer reported by both references.</param>
    /// <param name="globalBits">The number of DC-global bits before the global modular tree.</param>
    /// <param name="modularBits">The number of bits consumed through the global modular data.</param>
    [Theory]
    [InlineData(TestImages.Jxl.Square, 4587, 16, 201, 317)]
    [InlineData(TestImages.Jxl.LossyRgb3x3, 32768, 100, 213, 321)]
    public void ReadVarDctDcMatchesNative(string file, int globalScale, int dcQuantizer, int globalBits, int modularBits)
    {
        using MemoryStream stream = new(TestFile.Create(file).Bytes, false);
        using BufferedReadStream buffered = new(Configuration.Default, stream);
        JxlBitReader reader = new(new JxlCodestreamReader(buffered));
        JxlHeader header = JxlHeader.Read(ref reader, Configuration.Default.MemoryAllocator, SegmentIntegrityHandling.Strict);
        JxlFrameHeader frame = JxlFrameHeader.Read(ref reader, header, false, SegmentIntegrityHandling.Strict);
        using JxlFrameSections sections = JxlFrameSections.Read(ref reader, Configuration.Default.MemoryAllocator, frame.SectionCount, default);
        Assert.False(frame.Modular);
        Assert.Equal(1, sections.Sizes.Length);
        long start = reader.Position;
        reader.BeginSection(sections.Sizes.Span[0]);

        Vector3 factors = JxlDecoderCore.ReadDcQuantization(ref reader);
        using JxlVarDctDecoder varDct = JxlVarDctDecoder.ReadGlobal(ref reader, header, frame, Configuration.Default.MemoryAllocator, factors);
        Assert.Equal(new Vector3(1F / 4096F, 1F / 512F, 1F / 256F), factors);
        Assert.Equal(globalScale, varDct.GlobalScale);
        Assert.Equal(dcQuantizer, varDct.DcQuantizer);
        Assert.Equal(start + globalBits, reader.Position);
        Assert.Equal(1, varDct.DcContextCount);
        Assert.Equal(15, varDct.BlockContextCount);
        Assert.True(varDct.QuantizationThresholds.IsEmpty);

        // Both files explicitly encode this map; neither uses its one-bit default shortcut.
        // Its 183 bits must be consumed before color correlation and the shared modular tree.
        ReadOnlySpan<byte> blockMap =
        [
            0, 1, 2, 2, 3, 3, 4, 5, 6, 6, 6, 6, 6,
            7, 8, 9, 9, 10, 11, 12, 13, 14, 14, 14, 14, 14,
            7, 8, 9, 9, 10, 11, 12, 13, 14, 14, 14, 14, 14
        ];

        Assert.Equal(blockMap, varDct.BlockContextMap);
        Assert.Equal(0F, varDct.DcCorrelationX);
        Assert.Equal(1F, varDct.DcCorrelationB);
        for (int c = 0; c < 3; c++)
        {
            Assert.True(varDct.GetDcThresholds(c).IsEmpty);
        }

        // These VarDCT files have no extra channels. The global modular tree is present, but no
        // global channel header follows it. The same owner then decodes the DC samples in Y, X, B order.
        using JxlModularDecoder modular = JxlModularDecoder.Read(ref reader, header, frame, Configuration.Default.MemoryAllocator, default);
        Assert.Equal(start + modularBits, reader.Position);
        varDct.ReadDcGroup(ref reader, modular, 0, default);
        Assert.Equal(start + (file == TestImages.Jxl.Square ? 326 : 368), reader.Position);

        // Original libjxl DequantDC produces these exact single-precision results. The nonzero fixture
        // uses one extra precision bit and restores B by adding Y after scaling its coded residual.
        Vector3 expected = file == TestImages.Jxl.Square ? Vector3.Zero : new Vector3(0.000244140625F, 0.200312495F, 0.242031246F);
        for (int c = 0; c < 3; c++)
        {
            Buffer2DRegion<float> channel = varDct.GetDcChannel(c);
            Assert.Equal(1, channel.Width);
            Assert.Equal(1, channel.Height);
            Assert.Equal(expected[c], channel.DangerousGetRowSpan(0)[0]);
        }

        Assert.Equal((byte)0, varDct.DcContexts.DangerousGetRowSpan(0)[0]);
    }

    /// <summary>
    /// Verifies that stopping within a final copy is valid for byte and integer entropy consumers.
    /// </summary>
    /// <param name="file">The stored prefix or ANS entropy section.</param>
    /// <param name="byteValues">Whether to use the byte consumer instead of the circular integer history.</param>
    [Theory]
    [InlineData(TestImages.Jxl.TerminalLzCopy, true)]
    [InlineData(TestImages.Jxl.TerminalLzCopy, false)]
    [InlineData(TestImages.Jxl.TerminalLzCopyAns, true)]
    [InlineData(TestImages.Jxl.TerminalLzCopyAns, false)]
    public void EntropyFinalStateAcceptsIncompleteCopy(string file, bool byteValues)
    {
        using MemoryStream stream = new(TestFile.Create(file).Bytes, false);
        using BufferedReadStream buffered = new(Configuration.Default, stream);
        JxlBitReader reader = new(new JxlCodestreamReader(buffered));
        Assert.Equal(0x0AFFU, reader.ReadBits(16));
        using JxlEntropyDecoder entropy = JxlEntropyDecoder.Read(ref reader, Configuration.Default.MemoryAllocator, 2, true);
        JxlEntropyDecoder.Reader symbols = new(entropy, ref reader);

        // Context zero emits literal 7. Context one emits a length-three copy at distance one.
        // Both references return [7, 7] and accept the final state with two copied values still unused.
        uint first;
        uint second;
        if (byteValues)
        {
            InlineArray4<byte> storage = default;
            Span<byte> history = storage;
            history[0] = symbols.ReadByte(ref reader, 0, []);
            first = history[0];
            second = symbols.ReadByte(ref reader, 1, history[..1]);
        }
        else
        {
            InlineArray4<uint> storage = default;
            Span<uint> history = storage;
            first = symbols.ReadUInt32(ref reader, 0, history, 0);
            second = symbols.ReadUInt32(ref reader, 1, history, 1);
        }

        Assert.Equal(7U, first);
        Assert.Equal(7U, second);
        symbols.CheckFinalState();
    }

    /// <summary>
    /// Verifies that allowing unused copied values does not allow an invalid ANS checksum.
    /// </summary>
    [Fact]
    public void EntropyFinalStateRejectsInvalidAnsWithIncompleteCopy()
    {
        using MemoryStream stream = new(TestFile.Create(TestImages.Jxl.TerminalLzCopyInvalidAns).Bytes, false);
        using BufferedReadStream buffered = new(Configuration.Default, stream);
        JxlBitReader reader = new(new JxlCodestreamReader(buffered));
        Assert.Equal(0x0AFFU, reader.ReadBits(16));
        using JxlEntropyDecoder entropy = JxlEntropyDecoder.Read(ref reader, Configuration.Default.MemoryAllocator, 2, true);
        JxlEntropyDecoder.Reader symbols = new(entropy, ref reader);
        InlineArray4<uint> storage = default;
        Span<uint> history = storage;

        // Only the initial ANS state differs from the valid fixture: 0x130001 instead of 0x130000.
        // Both references still decode [7, 7], then reject the final entropy state.
        Assert.Equal(7U, symbols.ReadUInt32(ref reader, 0, history, 0));
        Assert.Equal(7U, symbols.ReadUInt32(ref reader, 1, history, 1));
        bool rejected = false;
        try
        {
            symbols.CheckFinalState();
        }
        catch (InvalidImageContentException)
        {
            rejected = true;
        }

        Assert.True(rejected);
    }

    /// <summary>
    /// Verifies retained section sizes and their logical order against the native decoder's conformance test.
    /// </summary>
    [Fact]
    public void ReadSectionPermutationMatchesReference()
    {
        using MemoryStream stream = new(TestFile.Create(TestImages.Jxl.PermutedSections).Bytes, false);
        using BufferedReadStream buffered = new(Configuration.Default, stream);
        JxlBitReader reader = new(new JxlCodestreamReader(buffered));
        JxlHeader header = JxlHeader.Read(ref reader, Configuration.Default.MemoryAllocator, SegmentIntegrityHandling.Strict);
        JxlFrameHeader frame = JxlFrameHeader.Read(ref reader, header, false, SegmentIntegrityHandling.Strict);
        using JxlFrameSections sections = JxlFrameSections.Read(ref reader, Configuration.Default.MemoryAllocator, frame.SectionCount, default);

        // jxl-rs headers/frame_header.rs records these values in test_has_permutation for this unchanged file.
        // Forty-nine sections exercise a non-power-of-two rank tree, including selections at its final entries.
        ReadOnlySpan<uint> sizes =
        [
            155, 992, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 9, 9, 9, 9, 9, 9, 9,
            9, 9, 9, 9, 9, 9, 9, 9, 9, 5, 5, 5, 5, 5, 5, 5, 5, 697, 5, 5, 5, 5, 5, 60
        ];

        ReadOnlySpan<uint> order =
        [
            0, 1, 42, 48, 2, 3, 4, 5, 6, 7, 8, 9, 43, 10, 11, 12, 13, 14, 15, 16, 17,
            44, 18, 19, 20, 21, 22, 23, 24, 25, 45, 26, 27, 28, 29, 30, 31, 32, 33, 46, 34,
            35, 36, 37, 38, 39, 40, 41, 47
        ];

        Assert.Equal(sizes, sections.Sizes.Span);
        Assert.Equal(order, sections.Order.Span);
        Assert.Equal(reader.BytePosition, sections.PayloadOffset);
        Assert.Equal(2193, sections.TotalLength);
    }

    /// <summary>
    /// Verifies CMYK and alpha metadata together with the complete embedded lookup-table profile at each SIMD width.
    /// </summary>
    [Fact]
    public void ReadCmykProfileMatchesAcrossSimdWidths()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateCmykHeader,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks the CMYK conformance header and original profile against the independent reference decoder.
    /// </summary>
    private static void ValidateCmykHeader()
    {
        using MemoryStream stream = new(TestFile.Create(TestImages.Jxl.CmykLayers).Bytes, false);
        using BufferedReadStream buffered = new(Configuration.Default, stream);
        JxlBitReader reader = new(new JxlCodestreamReader(buffered));
        JxlHeader header = JxlHeader.Read(ref reader, Configuration.Default.MemoryAllocator, SegmentIntegrityHandling.Strict);

        Assert.Equal(new Size(512, 512), header.Size);
        Assert.Equal(2, header.Metadata.ExtraChannels.Count);
        Assert.Contains(header.Metadata.ExtraChannels, channel => channel.Type == JxlExtraChannelType.Black);
        Assert.Contains(header.Metadata.ExtraChannels, channel => channel.Type == JxlExtraChannelType.Alpha);
        Assert.Equal(PixelColorType.CMYK, header.Metadata.GetPixelTypeInfo().ColorType & PixelColorType.CMYK);

        // This file embeds U.S. Web Coated (SWOP) v2. The hash covers all 557168 original bytes,
        // including the CMYK lookup tables extracted by jxl_cli --original-icc-out.
        IccProfile profile = Assert.IsType<IccProfile>(header.IccProfile);
        byte[] bytes = profile.ToByteArray();
        Assert.Equal(557168, bytes.Length);
        Assert.Equal("4855B8FABB96BDC6495D45D089BB8C8EFB1AE18389E0DC9E75A5F701A9C0B662", Convert.ToHexString(SHA256.HashData(bytes)));
        Assert.True(profile.CheckIsValid());
        Assert.Equal(IccColorSpaceType.Cmyk, profile.Header.DataColorSpace);
    }

    /// <summary>
    /// Verifies the sample precision of the floating-point conformance input without packing its values to integers.
    /// </summary>
    [Fact]
    public void ReadRetainsFloatingPointPrecision()
    {
        using MemoryStream stream = new(TestFile.Create(TestImages.Jxl.LosslessFloat).Bytes, false);
        using BufferedReadStream buffered = new(Configuration.Default, stream);
        JxlBitReader reader = new(new JxlCodestreamReader(buffered));
        JxlHeader header = JxlHeader.Read(ref reader, Configuration.Default.MemoryAllocator, SegmentIntegrityHandling.Strict);

        Assert.Equal(new Size(500, 500), header.Size);
        Assert.Equal(new JxlBitDepth(32, 8), header.Metadata.BitDepth);
        Assert.Equal(PixelColorRange.Extended, header.Metadata.GetPixelTypeInfo().ColorRange);
    }

    /// <summary>
    /// Verifies that an incomplete compressed profile fails at the bitstream boundary and releases its table storage.
    /// </summary>
    [Fact]
    public void ReadRejectsTruncatedIcc()
    {
        // This stored input contains the first 80 bytes of with_icc.jxl. The reference inspector also rejects it as truncated.
        using MemoryStream stream = new(TestFile.Create(TestImages.Jxl.TruncatedIcc).Bytes, false);
        using BufferedReadStream buffered = new(Configuration.Default, stream);
        JxlBitReader reader = new(new JxlCodestreamReader(buffered));

        Assert.Throws<InvalidImageContentException>(() => JxlHeader.Read(ref reader, Configuration.Default.MemoryAllocator, SegmentIntegrityHandling.Strict));
    }

    /// <summary>
    /// Verifies a 16-bit spot-color header and its original profile with each SIMD width and scalar fallback.
    /// </summary>
    [Fact]
    public void ReadSpotColorProfileMatchesAcrossSimdWidths()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateSpotColorHeader,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks the header against the independent reference inspector and extracted profile bytes.
    /// </summary>
    private static void ValidateSpotColorHeader()
    {
        using MemoryStream stream = new(TestFile.Create(TestImages.Jxl.SpotColor).Bytes, false);
        using BufferedReadStream buffered = new(Configuration.Default, stream);
        JxlBitReader reader = new(new JxlCodestreamReader(buffered));
        JxlHeader header = JxlHeader.Read(ref reader, Configuration.Default.MemoryAllocator, SegmentIntegrityHandling.Strict);

        Assert.Equal(new Size(600, 400), header.Size);
        Assert.Equal(new JxlBitDepth(16, 0), header.Metadata.BitDepth);
        Assert.Equal(3, header.Metadata.ExtraChannels.Count);

        // jxl-rs extracts a 940-byte ProPhoto RGB profile from this unmodified conformance file.
        IccProfile profile = Assert.IsType<IccProfile>(header.IccProfile);
        byte[] bytes = profile.ToByteArray();
        Assert.Equal(940, bytes.Length);
        Assert.Equal("CE0CAEE9506116EA94D7367D646F7FD6D0B7E82FEB8D1F3DE4EDB3BA57BAE07E", Convert.ToHexString(SHA256.HashData(bytes)));
        Assert.True(profile.CheckIsValid());
    }

    /// <summary>
    /// Verifies complete headers against the dimensions and coding models reported by the Rust reference decoder.
    /// </summary>
    /// <param name="file">The stored reference input.</param>
    /// <param name="width">The encoded width.</param>
    /// <param name="height">The encoded height.</param>
    /// <param name="colorType">The encoded component model.</param>
    [Theory]
    [InlineData(TestImages.Jxl.Splines, 2048, 2048, JxlColorType.Rgb)]
    [InlineData(TestImages.Jxl.SplinesFragmentedV0, 2048, 2048, JxlColorType.Rgb)]
    [InlineData(TestImages.Jxl.SplinesFragmentedV1, 2048, 2048, JxlColorType.Rgb)]
    [InlineData(TestImages.Jxl.SquareExtendedSizeContainer, 8, 8, JxlColorType.Xyb)]
    [InlineData(TestImages.Jxl.ExifXmp, 1, 1, JxlColorType.Rgb)]
    [InlineData(TestImages.Jxl.CroppedTrafficLight, 50, 80, JxlColorType.Rgb)]
    public void ReadMatchesReferenceHeader(string file, int width, int height, JxlColorType colorType)
    {
        using MemoryStream stream = new(TestFile.Create(file).Bytes, false);
        using BufferedReadStream buffered = new(Configuration.Default, stream);
        JxlBitReader reader = new(new JxlCodestreamReader(buffered));
        JxlHeader header = JxlHeader.Read(ref reader, Configuration.Default.MemoryAllocator, SegmentIntegrityHandling.Strict);

        Assert.Equal(new Size(width, height), header.Size);
        Assert.Equal(colorType, header.Metadata.ColorType);
        Assert.Equal(new JxlBitDepth(8, 0), header.Metadata.BitDepth);
        Assert.False(header.HasIccProfile);
        Assert.Null(header.IccProfile);
    }

    /// <summary>
    /// Verifies every reconstructed profile byte against the original profile extracted by jxl-rs.
    /// </summary>
    /// <param name="file">The stored ICC-bearing input.</param>
    /// <param name="length">The independently decoded profile length.</param>
    /// <param name="hash">The SHA-256 of the reference decoder's original ICC bytes.</param>
    /// <param name="colorSpace">The profile's device color space.</param>
    [Theory]
    [InlineData(TestImages.Jxl.WithIcc, 544, "92AAEE40521D7671DBC901DFC88CC36CB83D41766B2C466FF97268EE5AB0C577", IccColorSpaceType.Gray)]
    [InlineData(TestImages.Jxl.GrayscalePatchesWithIcc, 544, "92AAEE40521D7671DBC901DFC88CC36CB83D41766B2C466FF97268EE5AB0C577", IccColorSpaceType.Gray)]
    [InlineData(TestImages.Jxl.LossyWithIcc, 584, "72860842AB20BDD42AB229C72DB0906CF66FC87EF8CEAD828042A4083A029B2E", IccColorSpaceType.Rgb)]
    public void ReadIccMatchesReferenceBytes(string file, int length, string hash, IccColorSpaceType colorSpace)
    {
        // Entropy tables borrow allocator storage, which can contain nonzero data from an earlier use.
        // A dirty allocator verifies that implicit single-context maps and lookup entries are initialized.
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = new TestMemoryAllocator();
        using MemoryStream stream = new(TestFile.Create(file).Bytes, false);
        using BufferedReadStream buffered = new(configuration, stream);
        JxlBitReader reader = new(new JxlCodestreamReader(buffered));
        JxlHeader header = JxlHeader.Read(ref reader, configuration.MemoryAllocator, SegmentIntegrityHandling.Strict);

        // jxl_cli --original-icc-out extracts the embedded bytes before color conversion.
        // These hashes cover the full profile, including its header, directory, curves and text tags.
        Assert.True(header.HasIccProfile);
        IccProfile profile = Assert.IsType<IccProfile>(header.IccProfile);
        byte[] bytes = profile.ToByteArray();
        Assert.Equal(length, bytes.Length);
        Assert.Equal(hash, Convert.ToHexString(SHA256.HashData(bytes)));
        Assert.True(profile.CheckIsValid());
        Assert.Equal(colorSpace, profile.Header.DataColorSpace);
        Assert.NotEmpty(profile.Entries);
    }

    /// <summary>
    /// Verifies that the image header retains animation timing and alpha independently of pixel decoding.
    /// </summary>
    [Fact]
    public void ReadRetainsAnimationAndAlpha()
    {
        using MemoryStream stream = new(TestFile.Create(TestImages.Jxl.CroppedTrafficLight).Bytes, false);
        using BufferedReadStream buffered = new(Configuration.Default, stream);
        JxlBitReader reader = new(new JxlCodestreamReader(buffered));
        JxlHeader header = JxlHeader.Read(ref reader, Configuration.Default.MemoryAllocator, SegmentIntegrityHandling.Strict);

        Assert.Equal(new Rational(100, 1), header.TicksPerSecond);
        Assert.Equal(0U, header.Metadata.RepeatCount);
        JxlExtraChannelMetadata alpha = Assert.Single(header.Metadata.ExtraChannels);
        Assert.Equal(JxlExtraChannelType.Alpha, alpha.Type);
        Assert.Equal(PixelAlphaRepresentation.Unassociated, alpha.AlphaRepresentation);
    }

    /// <summary>
    /// Verifies that header orientation does not transpose encoded dimensions before frame reconstruction.
    /// </summary>
    [Fact]
    public void ReadRetainsOrientation()
    {
        using MemoryStream stream = new(TestFile.Create(TestImages.Jxl.Orientation6).Bytes, false);
        using BufferedReadStream buffered = new(Configuration.Default, stream);
        JxlBitReader reader = new(new JxlCodestreamReader(buffered));
        JxlHeader header = JxlHeader.Read(ref reader, Configuration.Default.MemoryAllocator, SegmentIntegrityHandling.Strict);

        // The reference inspector reports the displayed 256-by-100 size after applying orientation 6.
        // The encoded raster remains 100 by 256 until orientation is applied to the reconstructed image.
        Assert.Equal(new Size(100, 256), header.Size);
        Assert.Equal(ExifOrientationMode.RightTop, header.Orientation);
    }

    /// <summary>
    /// Verifies the separate preview-size coding against the stored reference image.
    /// </summary>
    [Fact]
    public void ReadRetainsPreview()
    {
        using MemoryStream stream = new(TestFile.Create(TestImages.Jxl.WithPreview).Bytes, false);
        using BufferedReadStream buffered = new(Configuration.Default, stream);
        JxlBitReader reader = new(new JxlCodestreamReader(buffered));
        JxlHeader header = JxlHeader.Read(ref reader, Configuration.Default.MemoryAllocator, SegmentIntegrityHandling.Strict);

        Assert.Equal(new Size(64, 64), header.Size);
        Assert.Equal(new Size(16, 16), header.PreviewSize);
    }
}
