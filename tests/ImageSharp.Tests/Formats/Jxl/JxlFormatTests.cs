// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.Intrinsics;
using System.Security.Cryptography;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jxl;
using SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;
using SixLabors.ImageSharp.Formats.Jxl.Components.Modular;
using SixLabors.ImageSharp.Formats.Jxl.Components.VarDct;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.Memory;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Jxl;

[Trait("Format", "Jxl")]
[ValidateDisposedMemoryAllocations]
public class JxlFormatTests
{
    /// <summary>
    /// Verifies AC metadata packing and field bounds at every SIMD width and the scalar fallback.
    /// </summary>
    [Fact]
    public void AcMetadataPackingPreservesFieldRulesAcrossSimdWidths()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateAcMetadataPacking,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks signed-byte saturation, valid sharpness values, rejected sharpness bounds and exact packed-store lengths.
    /// </summary>
    private static void ValidateAcMetadataPacking()
    {
        ReadOnlySpan<int> correlations =
        [
            int.MinValue, -129, -128, -127, -1, 0, 1, 7, 8, 126, 127, 128, 129, int.MaxValue, -200, 200,
            -2, 2, -130, 130, -128, 127, 0, 4, -9, 9, -255, 255, -129, 128, 6, 5, 3
        ];

        // Correlation fields saturate to signed eight-bit storage. Sharpness is an index into an eight-entry table:
        // values zero through seven retain their value; every other integer is invalid rather than saturated.
        ReadOnlySpan<sbyte> expectedCorrelations =
        [
            -128, -128, -128, -127, -1, 0, 1, 7, 8, 126, 127, 127, 127, 127, -128, 127,
            -2, 2, -128, 127, -128, 127, 0, 4, -9, 9, -128, 127, -128, 127, 6, 5, 3
        ];

        ReadOnlySpan<int> sharpness = [0, 1, 2, 3, 4, 5, 6, 7, 0, 1, 2, 3, 4, 5, 6, 7, 0, 1, 2, 3, 4, 5, 6, 7, 0, 1, 2, 3, 4, 5, 6, 7, 0];
        ReadOnlySpan<byte> expectedSharpness = [0, 1, 2, 3, 4, 5, 6, 7, 0, 1, 2, 3, 4, 5, 6, 7, 0, 1, 2, 3, 4, 5, 6, 7, 0, 1, 2, 3, 4, 5, 6, 7, 0];
        sbyte[] correlationOutput = new sbyte[correlations.Length + 2];
        byte[] sharpnessOutput = new byte[sharpness.Length + 2];
        ReadOnlySpan<int> lengths = [1, 7, 15, 29, 33];
        foreach (int length in lengths)
        {
            // Offset destinations test unaligned stores. Sentinels at both ends detect stores wider than the logical row.
            correlationOutput.AsSpan().Fill(85);
            sharpnessOutput.AsSpan().Fill(byte.MaxValue);
            JxlVarDctDecoder.ConvertCorrelationRow(correlations[..length], correlationOutput.AsSpan(1, length));
            JxlVarDctDecoder.ConvertSharpnessRow(sharpness[..length], sharpnessOutput.AsSpan(1, length));

            Assert.Equal(expectedCorrelations[..length], correlationOutput.AsSpan(1, length));
            Assert.Equal(expectedSharpness[..length], sharpnessOutput.AsSpan(1, length));
            Assert.Equal((sbyte)85, correlationOutput[0]);
            Assert.Equal((sbyte)85, correlationOutput[length + 1]);
            Assert.Equal(byte.MaxValue, sharpnessOutput[0]);
            Assert.Equal(byte.MaxValue, sharpnessOutput[length + 1]);
        }

        int[] invalidSharpness = sharpness.ToArray();
        ReadOnlySpan<int> invalidValues = [-1, 8];
        foreach (int invalid in invalidValues)
        {
            // Put the invalid value in the first vector block, so accelerated paths must enforce the same rule as scalar code.
            invalidSharpness[0] = invalid;
            Assert.Throws<InvalidImageContentException>(
                () => JxlVarDctDecoder.ConvertSharpnessRow(invalidSharpness, sharpnessOutput.AsSpan(0, invalidSharpness.Length)));
        }
    }

    /// <summary>
    /// Verifies that layouts allocate no samples and borrowed channels do not release their source storage.
    /// </summary>
    [Fact]
    public void ModularChannelSeparatesLayoutAndSampleOwnership()
    {
        TestMemoryAllocator allocator = new();
        allocator.EnableNonThreadSafeLogging();
        JxlModularChannelLayout layout = new(new Size(3, 2), 1, 0);
        Assert.Empty(allocator.AllocationLog);

        using (JxlModularChannel owned = JxlModularChannel.Allocate(allocator, layout))
        {
            // Allocate returns usable storage immediately. No later initialization step is required.
            Assert.Equal(layout, owned.Layout);
            owned.Region.DangerousGetRowSpan(1)[1] = 37;
            using (JxlModularChannel borrowed = owned.Borrow(new Rectangle(1, 1, 1, 1)))
            {
                Assert.Equal(new Size(1, 1), borrowed.Layout.Size);
                Assert.Equal(1, borrowed.Layout.HorizontalShift);
                Assert.Equal(0, borrowed.Layout.VerticalShift);
                Assert.Equal(37, borrowed.Region.DangerousGetRowSpan(0)[0]);
                borrowed.Region.DangerousGetRowSpan(0)[0] = 53;
            }

            // Disposing the borrowed channel does not return its owner's buffer. Its sample write remains available.
            Assert.Empty(allocator.ReturnLog);
            Assert.Equal(53, owned.Region.DangerousGetRowSpan(1)[1]);
        }

        Assert.NotEmpty(allocator.AllocationLog);
        Assert.Equal(allocator.AllocationLog.Count, allocator.ReturnLog.Count);
        Assert.Equal(0, allocator.LiveBufferBytes);

        int allocationCount = allocator.AllocationLog.Count;
        ReadOnlySpan<Size> emptySizes = [new(0, 1), new(1, 0)];
        foreach (Size size in emptySizes)
        {
            // Empty transform outputs retain their logical dimensions and shifts without a dummy allocation.
            JxlModularChannelLayout emptyLayout = new(size, -1, -1);
            using JxlModularChannel empty = JxlModularChannel.Allocate(allocator, emptyLayout);
            Assert.Equal(emptyLayout, empty.Layout);
            Assert.True(empty.IsEmpty);
            Assert.Equal(0, empty.Region.Width);
            Assert.Equal(0, empty.Region.Height);
        }

        Assert.Equal(allocationCount, allocator.AllocationLog.Count);
        Assert.Equal(0, allocator.LiveBufferBytes);
    }

    /// <summary>
    /// Verifies DC arithmetic, signed threshold buckets and short tails against native output at each SIMD width.
    /// </summary>
    /// <param name="file">The stored image or partial DC-global metadata used to prepare the operator.</param>
    [Theory]
    [InlineData(TestImages.Jxl.LossyRgb3x3)]
    [InlineData(TestImages.Jxl.VarDctDcThresholds)]
    public void DequantizeDcRowMatchesNativeAcrossSimdWidths(string file)
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateVarDctDcRows,
            file,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks independently decoded native float bits and bucket indices, including integer extremes and reconstruction values above pixel bounds.
    /// </summary>
    /// <param name="file">The stored quantization and threshold metadata.</param>
    private static void ValidateVarDctDcRows(string file)
    {
        using MemoryStream headerInput = new(TestFile.Create(TestImages.Jxl.LossyRgb3x3).Bytes, false);
        using BufferedReadStream headerBuffered = new(Configuration.Default, headerInput);
        JxlBitReader headerReader = new(new JxlCodestreamReader(headerBuffered));
        JxlHeader header = JxlHeader.Read(ref headerReader, Configuration.Default.MemoryAllocator, SegmentIntegrityHandling.Strict, CancellationToken.None);
        JxlFrameHeader frame = JxlFrameHeader.Read(ref headerReader, header, false, SegmentIntegrityHandling.Strict);
        using JxlFrameSections sections = JxlFrameSections.Read(ref headerReader, Configuration.Default.MemoryAllocator, frame.SectionCount, default);

        bool customThresholds = file == TestImages.Jxl.VarDctDcThresholds;
        using MemoryStream input = new(TestFile.Create(file).Bytes, false);
        using BufferedReadStream buffered = new(Configuration.Default, input);
        JxlBitReader reader = new(new JxlCodestreamReader(buffered));

        // The partial input contains the same DC-global syntax directly after its two-byte signature.
        // The complete input uses the section offset obtained from its normal frame-table parser.
        reader.SeekBytePosition(customThresholds ? 2 : sections.PayloadOffset);
        Vector3 factors = JxlDecoderCore.ReadDcQuantization(ref reader);
        using JxlVarDctDecoder varDct = JxlVarDctDecoder.ReadGlobal(
            ref reader, header, frame, Configuration.Default.MemoryAllocator, factors, []);

        ReadOnlySpan<int> x =
        [
            int.MinValue, int.MaxValue, -258, -257, -256, -2, -1, 0, 1, 255, 256, 257,
            -16777217, -16777216, -16777215, 16777215, 16777216, 16777217,
            -1025, -1024, -1023, 1023, 1024, 1025, -33, -32, -31, 31, 32, 33, -3, 3, 0
        ];

        ReadOnlySpan<int> y =
        [
            int.MaxValue, int.MinValue, -18, -17, -16, -1, 0, 1, 16, 17, 18, 0,
            -16777217, -16777216, -16777215, 16777215, 16777216, 16777217,
            1025, 1024, 1023, -1023, -1024, -1025, -33, -32, -31, 31, 32, 33, 1, -1, 0
        ];

        ReadOnlySpan<int> b =
        [
            int.MinValue, int.MaxValue, -101, -100, -99, 99, 100, 101, 0, -1, 1, 0,
            16777217, 16777216, 16777215, -16777215, -16777216, -16777217,
            -1025, -1024, -1023, 1023, 1024, 1025, -33, -32, -31, 31, 32, 33, -2, 2, int.MaxValue
        ];

        // Original libjxl DequantDC produced these exact float bits with extra precision = 1.
        // Correlation uses ratios zero and one, so fused and unfused multiply-add give the same results here.
        ReadOnlySpan<uint> expectedX =
        [
            0xC5A3D70A, 0x45A3D70A, 0xBA251EB8, 0xBA247AE1, 0xBA23D70A, 0xB6A3D70A, 0xB623D70A, 0x00000000,
            0x3623D70A, 0x3A233333, 0x3A23D70A, 0x3A247AE1, 0xC223D70A, 0xC223D70A, 0xC223D709, 0x4223D709,
            0x4223D70A, 0x4223D70A, 0xBB240000, 0xBB23D70A, 0xBB23AE14, 0x3B23AE14, 0x3B23D70A, 0x3B240000,
            0xB8A8F5C2, 0xB8A3D70A, 0xB89EB852, 0x389EB852, 0x38A3D70A, 0x38A8F5C2, 0xB6F5C28F, 0x36F5C28F, 0x00000000
        ];

        ReadOnlySpan<uint> expectedY =
        [
            0x4723D70A, 0xC723D70A, 0xB9B851EB, 0xB9AE147B, 0xB9A3D70A, 0xB7A3D70A, 0x00000000, 0x37A3D70A,
            0x39A3D70A, 0x39AE147B, 0x39B851EB, 0x00000000, 0xC3A3D70A, 0xC3A3D70A, 0xC3A3D709, 0x43A3D709,
            0x43A3D70A, 0x43A3D70A, 0x3CA40000, 0x3CA3D70A, 0x3CA3AE14, 0xBCA3AE14, 0xBCA3D70A, 0xBCA40000,
            0xBA28F5C2, 0xBA23D70A, 0xBA1EB852, 0x3A1EB852, 0x3A23D70A, 0x3A28F5C2, 0x37A3D70A, 0xB7A3D70A, 0x00000000
        ];

        ReadOnlySpan<uint> expectedB =
        [
            0xC723D70A, 0x4723D70A, 0xBB8CCCCD, 0xBB8AE148, 0xBB88F5C2, 0x3B7C28F5, 0x3B800000, 0x3B81EB85,
            0x39A3D70A, 0x3999999A, 0x39CCCCCC, 0x00000000, 0x43A3D70A, 0x43A3D70A, 0x43A3D709, 0xC3A3D709,
            0xC3A3D70A, 0xC3A3D70A, 0xBCA40000, 0xBCA3D70A, 0xBCA3AE14, 0x3CA3AE14, 0x3CA3D70A, 0x3CA40000,
            0xBAFD70A3, 0xBAF5C28F, 0xBAEE147B, 0x3AEE147B, 0x3AF5C28F, 0x3AFD70A3, 0xB875C28F, 0x3875C28F, 0x47A3D70A
        ];

        ReadOnlySpan<byte> expectedContexts = [1, 6, 0, 0, 0, 2, 3, 3, 5, 5, 5, 5, 2, 2, 2, 5, 5, 5, 1, 1, 1, 6, 6, 6, 0, 0, 0, 7, 7, 7, 1, 6, 3];
        float[] outputX = new float[x.Length];
        float[] outputY = new float[x.Length];
        float[] outputB = new float[x.Length];
        byte[] contexts = new byte[x.Length + 2];
        ReadOnlySpan<int> lengths = [1, 7, 15, 29, 33];
        foreach (int length in lengths)
        {
            // Vary the row end to cover vector remainders and scalar tails. Sentinel bytes detect a context
            // packing store that writes a complete register instead of its four, eight or sixteen valid bytes.
            contexts.AsSpan().Fill(byte.MaxValue);
            varDct.DequantizeDcRow(
                x[..length],
                y[..length],
                b[..length],
                outputX.AsSpan(0, length),
                outputY.AsSpan(0, length),
                outputB.AsSpan(0, length),
                contexts.AsSpan(1, length),
                1);

            Assert.Equal(byte.MaxValue, contexts[0]);
            Assert.Equal(byte.MaxValue, contexts[length + 1]);
            for (int i = 0; i < length; i++)
            {
                Assert.Equal(expectedX[i], BitConverter.SingleToUInt32Bits(outputX[i]));
                Assert.Equal(expectedY[i], BitConverter.SingleToUInt32Bits(outputY[i]));
                Assert.Equal(expectedB[i], BitConverter.SingleToUInt32Bits(outputB[i]));
                Assert.Equal(customThresholds ? expectedContexts[i] : (byte)0, contexts[i + 1]);
            }
        }
    }

    /// <summary>
    /// Verifies complete modular pixel output against independently decoded native pixels.
    /// </summary>
    /// <param name="file">The unchanged reference fixture.</param>
    /// <param name="width">The native output width.</param>
    /// <param name="height">The native output height.</param>
    /// <param name="pixelHash">The SHA-256 of row-major unassociated RGBA bytes from the reference decoder.</param>
    [Theory]
    [InlineData(TestImages.Jxl.SqueezeEdge, 513, 513, "153CEA5DAAE29DE298D4D1E9DCEF6B1854622D4A32049882E1A966CA2F8E5031")]
    [InlineData(TestImages.Jxl.GrayAlphaLossless, 64, 64, "B53CECBA04562FB5728FDBE175AF374BB9EB44B47A0F936B3C35E3D4A547E000")]
    [InlineData(TestImages.Jxl.DeltaPalette, 555, 751, "CD2DAB268DFAF19A5285277BCE16A71E73DB337BDA82DD47A1354F4FEC80EABF")]
    [InlineData(TestImages.Jxl.EmptyPalette, 1623, 1080, "50E5FC336905C9E32842A94F52DB68815446A0B12C45C03988C3133870181283")]
    [InlineData(TestImages.Jxl.GrayscalePatches, 169, 118, "2A6DD4E00A9AB88BFA96F09C05C15E662C508AD72B2B9496694EB109428FFBC4")]
    [InlineData(TestImages.Jxl.GrayscalePatchesWithIcc, 169, 118, "2A6DD4E00A9AB88BFA96F09C05C15E662C508AD72B2B9496694EB109428FFBC4")]
    public void DecodeModularMatchesNativePixels(string file, int width, int height, string pixelHash)
    {
        Configuration configuration = new(new JxlConfigurationModule());
        DecoderOptions options = new(configuration);
        using MemoryStream input = new(TestFile.Create(file).Bytes, false);
        ImageInfo info = Image.Identify(options, input);
        Assert.Equal(new Size(width, height), info.Size);

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateModularPixels,
            file,
            pixelHash,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
    }

    /// <summary>
    /// Verifies that sixteen-bit samples reach both integer and floating-point destinations without byte rounding.
    /// </summary>
    [Fact]
    public void Decode16BitPreservesSamplePrecision()
    {
        Configuration configuration = new(new JxlConfigurationModule());
        DecoderOptions options = new(configuration);
        byte[] data = TestFile.Create(TestImages.Jxl.Rgb16).Bytes;
        using MemoryStream integerInput = new(data, false);
        using MemoryStream vectorInput = new(data, false);
        using MemoryStream automaticInput = new(data, false);
        using Image<Rgb48> integer = Image.Load<Rgb48>(options, integerInput);
        using Image<RgbaVector> vectors = Image.Load<RgbaVector>(options, vectorInput);
        using Image automatic = Image.Load(options, automaticInput);

        // The reference's image-integration test specifies these two RGB16 pixels literally.
        // Red = 32768 must remain halfway through the sixteen-bit range; a byte intermediate would change that value.
        Assert.Equal(new Rgb48(ushort.MaxValue, 0, 0), integer[0, 0]);
        Assert.Equal(new Rgb48(32768, ushort.MaxValue, 0), integer[1, 0]);
        Assert.Equal(new RgbaVector(1F, 0F, 0F, 1F), vectors[0, 0]);
        Assert.Equal(new RgbaVector(32768F / ushort.MaxValue, 1F, 0F, 1F), vectors[1, 0]);
        Assert.IsType<Image<Rgb48>>(automatic);
    }

    /// <summary>
    /// Verifies every reversible color operation, including signed rounding and overflow, at each SIMD width and scalar fallback.
    /// </summary>
    [Fact]
    public void InverseModularColorTransformMatchesNativeArithmetic()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateInverseModularColorTransform,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Verifies native cross-channel properties at row edges and interior blocks for every SIMD width and scalar fallback.
    /// </summary>
    [Fact]
    public void ModularCrossChannelPropertiesMatchNativeArithmetic()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateModularCrossChannelProperties,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Verifies squeeze reconstruction, signed division and odd edges at each SIMD width and scalar fallback.
    /// </summary>
    [Fact]
    public void InverseModularSqueezeMatchesNativeArithmetic()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateInverseModularSqueeze,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Verifies the first complete modular path through format discovery, explicit decoding and automatic pixel selection.
    /// </summary>
    /// <param name="file">The unchanged native lossless fixture.</param>
    /// <param name="hasAlpha">Whether its independently decoded alpha samples are 128 rather than implicit opacity.</param>
    [Theory]
    [InlineData(TestImages.Jxl.LosslessRgb3x3, false)]
    [InlineData(TestImages.Jxl.LosslessRgba3x3, true)]
    public void DecodeLosslessMatchesNativePixels(string file, bool hasAlpha)
    {
        Configuration configuration = new(new JxlConfigurationModule());
        DecoderOptions options = new(configuration);
        byte[] data = TestFile.Create(file).Bytes;
        using MemoryStream discoveredInput = new(data, false);
        using MemoryStream explicitInput = new(data, false);
        using MemoryStream automaticInput = new(data, false);
        using Image<Rgba32> discovered = Image.Load<Rgba32>(options, discoveredInput);
        using Image<Rgba32> explicitImage = JxlDecoder.Instance.Decode<Rgba32>(options, explicitInput);
        using Image automatic = Image.Load(options, automaticInput);

        // The native decoder returns the original gamma-encoded samples: 0, 64, 128 and 255.
        // The file declares compression gamma 0.45455. Convert x to linear with x^(1/0.45455), then apply the sRGB curve.
        // In eight-bit sRGB, those samples become 0, 62, 129 and 255. Alpha retains its independent value of 128.
        ReadOnlySpan<Rgb24> expected =
        [
            new(255, 0, 0), new(0, 255, 0), new(0, 0, 255),
            new(129, 62, 62), new(62, 129, 62), new(62, 62, 129),
            new(255, 255, 255), new(129, 129, 129), new(0, 0, 0)
        ];

        Assert.Equal(new Size(3, 3), discovered.Size);
        Assert.Equal(1, discovered.Frames.Count);
        Assert.Equal(JxlColorType.Rgb, discovered.Metadata.GetFormatMetadata(JxlFormat.Instance).ColorType);
        for (int i = 0; i < expected.Length; i++)
        {
            Rgb24 color = expected[i];
            Rgba32 pixel = new(color.R, color.G, color.B, hasAlpha ? (byte)128 : byte.MaxValue);

            Assert.Equal(pixel, discovered[i % 3, i / 3]);
            Assert.Equal(pixel, explicitImage[i % 3, i / 3]);
        }

        if (hasAlpha)
        {
            Assert.IsType<Image<Rgba32>>(automatic);
        }
        else
        {
            Assert.IsType<Image<Rgb24>>(automatic);
        }

        Assert.True(discoveredInput.CanRead);
        Assert.True(explicitInput.CanRead);
        Assert.True(automaticInput.CanRead);
    }

    /// <summary>
    /// Verifies that a short modular payload returns all temporary allocations and leaves the caller's stream open.
    /// </summary>
    [Fact]
    public void DecodeRejectsTruncatedLosslessPayload()
    {
        TestMemoryAllocator allocator = new();
        allocator.EnableNonThreadSafeLogging();
        Configuration configuration = new(new JxlConfigurationModule()) { MemoryAllocator = allocator };
        DecoderOptions options = new(configuration);
        using MemoryStream stream = new(TestFile.Create(TestImages.Jxl.LosslessRgb3x3Truncated).Bytes, false);

        // This stored fixture omits the original file's final payload byte. The native decoder also rejects it as truncated.
        // Allocation logging checks cleanup immediately, without relying on finalizers to release failed reconstruction buffers.
        Assert.Throws<InvalidImageContentException>(() =>
        {
            using Image<Rgba32> image = Image.Load<Rgba32>(options, stream);
        });

        Assert.Equal(0, allocator.LiveBufferBytes);
        Assert.True(stream.CanRead);
    }

    /// <summary>
    /// Verifies that structural patch-grid mismatches remain invalid under every segment recovery policy.
    /// </summary>
    /// <param name="handling">The selected segment error policy.</param>
    [Theory]
    [InlineData(SegmentIntegrityHandling.Strict)]
    [InlineData(SegmentIntegrityHandling.IgnoreAncillary)]
    [InlineData(SegmentIntegrityHandling.IgnoreImageData)]
    public void DecodeRejectsPatchesWithMixedUpsampling(SegmentIntegrityHandling handling)
    {
        TestMemoryAllocator allocator = new();
        allocator.EnableNonThreadSafeLogging();
        Configuration configuration = new(new JxlConfigurationModule()) { MemoryAllocator = allocator };
        DecoderOptions options = new(configuration) { SegmentIntegrityHandling = handling };
        using MemoryStream stream = new(TestFile.Create(TestImages.Jxl.PatchesMixedUpsampling).Bytes, false);

        // This stored jxl-rs regression requires 4x enlargement of each color-plane dimension.
        // The extra channel has an additional DimensionShift of one, so it requires 8x enlargement.
        // Both references prohibit patching these different sample grids.
        Assert.Throws<InvalidImageContentException>(() =>
        {
            using Image<Rgba32> image = Image.Load<Rgba32>(options, stream);
        });

        Assert.Equal(0, allocator.LiveBufferBytes);
        Assert.True(stream.CanRead);
    }

    /// <summary>
    /// Verifies cancellation through the public asynchronous decoder without transferring stream ownership.
    /// </summary>
    [Fact]
    public async Task DecodeLosslessHonorsCancellation()
    {
        Configuration configuration = new(new JxlConfigurationModule());
        DecoderOptions options = new(configuration);
        using MemoryStream stream = new(TestFile.Create(TestImages.Jxl.LosslessRgb3x3).Bytes, false);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            using Image<Rgba32> image = await JxlDecoder.Instance.DecodeAsync<Rgba32>(options, stream, cancellation.Token);
        });

        Assert.True(stream.CanRead);
    }

    /// <summary>
    /// Verifies displayed animation timing and MaxFrames against the native reference inspector.
    /// </summary>
    /// <param name="file">The stored animation input.</param>
    /// <param name="maxFrames">The displayed frame limit.</param>
    /// <param name="firstSeconds">The first frame's duration reported by the native inspector, in seconds.</param>
    /// <param name="shortSeconds">The shorter later frame's duration, in seconds.</param>
    [Theory]
    [InlineData(TestImages.Jxl.CroppedTrafficLight, 1, 3L, 1L)]
    [InlineData(TestImages.Jxl.CroppedTrafficLight, 4, 3L, 1L)]
    [InlineData(TestImages.Jxl.CroppedTrafficLightLongDuration, 4, 4398046510080L, 102400L)]
    public void IdentifyReportsDisplayedAnimationFrames(string file, int maxFrames, long firstSeconds, long shortSeconds)
    {
        Configuration configuration = new(new JxlConfigurationModule());
        DecoderOptions options = new(configuration) { MaxFrames = (uint)maxFrames };
        using MemoryStream stream = new(TestFile.Create(file).Bytes, false);

        ImageInfo info = Image.Identify(options, stream);

        // jxlinspect reports the exact durations for both files. The long variant uses the maximum native
        // tick count and a 1/1024 tick rate, which exceeds both uint seconds and TimeSpan's duration range.
        // Cropped intermediate content must not change the canvas size or add displayed frames.
        ReadOnlySpan<long> seconds = [firstSeconds, shortSeconds, 3 * shortSeconds, shortSeconds];

        Assert.Equal(new Size(50, 80), info.Size);
        Assert.Equal(maxFrames, info.FrameCount);
        Assert.Equal(0U, info.Metadata.GetFormatMetadata(JxlFormat.Instance).RepeatCount);

        for (int i = 0; i < maxFrames; i++)
        {
            JxlFrameMetadata frame = info.FrameMetadataCollection[i].GetFormatMetadata(JxlFormat.Instance);

            Assert.Equal(new LongRational(seconds[i], 1), frame.FrameDelay);
        }
    }

    /// <summary>
    /// Verifies that the optional preview is traversed without being counted as a displayed image.
    /// </summary>
    [Fact]
    public void IdentifyDoesNotCountPreviewAsDisplayedFrame()
    {
        Configuration configuration = new(new JxlConfigurationModule());
        using MemoryStream stream = new(TestFile.Create(TestImages.Jxl.WithPreview).Bytes, false);

        ImageInfo info = Image.Identify(new DecoderOptions(configuration), stream);

        Assert.Equal(new Size(64, 64), info.Size);
        Assert.Equal(1, info.FrameCount);
    }

    /// <summary>
    /// Verifies that invalid bounded Brotli wrappers and payloads follow the shared ancillary error policy.
    /// </summary>
    /// <param name="file">The stored input with invalid optional metadata.</param>
    /// <param name="handling">The selected segment error policy.</param>
    [Theory]
    [InlineData(TestImages.Jxl.ExifBrotliTruncated, SegmentIntegrityHandling.Strict)]
    [InlineData(TestImages.Jxl.ExifBrotliTruncated, SegmentIntegrityHandling.IgnoreAncillary)]
    [InlineData(TestImages.Jxl.ExifBrotliTruncated, SegmentIntegrityHandling.IgnoreImageData)]
    [InlineData(TestImages.Jxl.ExifBrotliShortType, SegmentIntegrityHandling.Strict)]
    [InlineData(TestImages.Jxl.ExifBrotliShortType, SegmentIntegrityHandling.IgnoreAncillary)]
    [InlineData(TestImages.Jxl.ExifBrotliShortType, SegmentIntegrityHandling.IgnoreImageData)]
    [InlineData(TestImages.Jxl.ExifBrotliForbiddenType, SegmentIntegrityHandling.Strict)]
    [InlineData(TestImages.Jxl.ExifBrotliForbiddenType, SegmentIntegrityHandling.IgnoreAncillary)]
    [InlineData(TestImages.Jxl.ExifBrotliForbiddenType, SegmentIntegrityHandling.IgnoreImageData)]
    public void IdentifyAppliesAncillaryPolicyToCompressedExif(string file, SegmentIntegrityHandling handling)
    {
        Configuration configuration = new(new JxlConfigurationModule());
        DecoderOptions options = new(configuration) { SegmentIntegrityHandling = handling };
        using MemoryStream stream = new(TestFile.Create(file).Bytes, false);

        // Each damaged payload has a valid outer box range. Ignoring it must retain the following image,
        // without consuming a type byte from the next box or accepting the malformed EXIF profile.
        if (handling == SegmentIntegrityHandling.Strict)
        {
            Assert.Throws<InvalidImageContentException>(() => Image.Identify(options, stream));
        }
        else
        {
            ImageInfo info = Image.Identify(options, stream);

            Assert.Equal(new Size(256, 128), info.Size);
            Assert.Null(info.Metadata.ExifProfile);
            Assert.Same(JxlFormat.Instance, info.Metadata.DecodedImageFormat);
        }
    }

    /// <summary>
    /// Verifies that skipped optional boxes do not validate their payloads, even with strict integrity handling.
    /// </summary>
    /// <param name="file">The stored input with an invalid bounded wrapper.</param>
    [Theory]
    [InlineData(TestImages.Jxl.ExifBrotliShortType)]
    [InlineData(TestImages.Jxl.ExifBrotliForbiddenType)]
    public void IdentifySkipsInvalidOptionalBoxes(string file)
    {
        Configuration configuration = new(new JxlConfigurationModule());
        DecoderOptions options = new(configuration) { SkipMetadata = true, SegmentIntegrityHandling = SegmentIntegrityHandling.Strict };
        using MemoryStream stream = new(TestFile.Create(file).Bytes, false);

        ImageInfo info = Image.Identify(options, stream);

        Assert.Equal(new Size(256, 128), info.Size);
        Assert.Null(info.Metadata.ExifProfile);
    }

    /// <summary>
    /// Verifies that invalid optional names do not prevent parsing the fields and image sections that follow them.
    /// </summary>
    /// <param name="file">The stored input with one invalid UTF-8 label.</param>
    /// <param name="handling">The selected segment error policy.</param>
    [Theory]
    [InlineData(TestImages.Jxl.InvalidFrameName, SegmentIntegrityHandling.Strict)]
    [InlineData(TestImages.Jxl.InvalidFrameName, SegmentIntegrityHandling.IgnoreAncillary)]
    [InlineData(TestImages.Jxl.InvalidFrameName, SegmentIntegrityHandling.IgnoreImageData)]
    [InlineData(TestImages.Jxl.InvalidExtraChannelName, SegmentIntegrityHandling.Strict)]
    [InlineData(TestImages.Jxl.InvalidExtraChannelName, SegmentIntegrityHandling.IgnoreAncillary)]
    [InlineData(TestImages.Jxl.InvalidExtraChannelName, SegmentIntegrityHandling.IgnoreImageData)]
    public void IdentifyAppliesAncillaryPolicyToNames(string file, SegmentIntegrityHandling handling)
    {
        Configuration configuration = new(new JxlConfigurationModule());
        DecoderOptions options = new(configuration) { SegmentIntegrityHandling = handling };
        using MemoryStream stream = new(TestFile.Create(file).Bytes, false);

        if (handling == SegmentIntegrityHandling.Strict)
        {
            Assert.Throws<InvalidImageContentException>(() => Image.Identify(options, stream));
        }
        else
        {
            ImageInfo info = Image.Identify(options, stream);

            Assert.Equal(1, info.FrameCount);
            if (file == TestImages.Jxl.InvalidExtraChannelName)
            {
                // The next valid label and the spot mixture must survive recovery of the preceding invalid label.
                JxlMetadata metadata = info.Metadata.GetFormatMetadata(JxlFormat.Instance);

                Assert.Equal(new Size(600, 400), info.Size);
                Assert.Equal(3, metadata.ExtraChannels.Count);
                Assert.Empty(metadata.ExtraChannels[1].Name);
                Assert.Equal("A", metadata.ExtraChannels[2].Name);
                Assert.Equal(JxlExtraChannelType.SpotColor, metadata.ExtraChannels[2].Type);
                Assert.NotNull(info.Metadata.IccProfile);
            }
            else
            {
                Assert.Equal(new Size(8, 8), info.Size);
            }
        }
    }

    /// <summary>
    /// Verifies that a complete but invalid ICC prediction can be omitted only when XYB supplies the native color transform.
    /// </summary>
    /// <param name="file">The stored input with a complete entropy stream and invalid prediction preamble.</param>
    /// <param name="handling">The selected segment error policy.</param>
    [Theory]
    [InlineData(TestImages.Jxl.XybInvalidIccPrediction, SegmentIntegrityHandling.Strict)]
    [InlineData(TestImages.Jxl.XybInvalidIccPrediction, SegmentIntegrityHandling.IgnoreAncillary)]
    [InlineData(TestImages.Jxl.XybInvalidIccPrediction, SegmentIntegrityHandling.IgnoreImageData)]
    [InlineData(TestImages.Jxl.RgbInvalidIccPrediction, SegmentIntegrityHandling.Strict)]
    [InlineData(TestImages.Jxl.RgbInvalidIccPrediction, SegmentIntegrityHandling.IgnoreAncillary)]
    [InlineData(TestImages.Jxl.RgbInvalidIccPrediction, SegmentIntegrityHandling.IgnoreImageData)]
    public void IdentifyRequiresNativeComponentIcc(string file, SegmentIntegrityHandling handling)
    {
        Configuration configuration = new(new JxlConfigurationModule());
        DecoderOptions options = new(configuration) { SegmentIntegrityHandling = handling };
        using MemoryStream stream = new(TestFile.Create(file).Bytes, false);

        if (handling == SegmentIntegrityHandling.Strict || file == TestImages.Jxl.RgbInvalidIccPrediction)
        {
            Assert.Throws<InvalidImageContentException>(() => Image.Identify(options, stream));
        }
        else
        {
            ImageInfo info = Image.Identify(options, stream);

            Assert.Equal(new Size(8, 8), info.Size);
            Assert.Equal(1, info.FrameCount);
            Assert.Equal(JxlColorType.Xyb, info.Metadata.GetFormatMetadata(JxlFormat.Instance).ColorType);
            Assert.Null(info.Metadata.IccProfile);
        }
    }

    /// <summary>
    /// Verifies that missing name bytes and incomplete ICC entropy remain fatal in every integrity mode.
    /// </summary>
    /// <param name="file">The stored truncated field input.</param>
    /// <param name="handling">The selected segment error policy.</param>
    [Theory]
    [InlineData(TestImages.Jxl.TruncatedFrameName, SegmentIntegrityHandling.Strict)]
    [InlineData(TestImages.Jxl.TruncatedFrameName, SegmentIntegrityHandling.IgnoreAncillary)]
    [InlineData(TestImages.Jxl.TruncatedFrameName, SegmentIntegrityHandling.IgnoreImageData)]
    [InlineData(TestImages.Jxl.XybTruncatedIcc, SegmentIntegrityHandling.Strict)]
    [InlineData(TestImages.Jxl.XybTruncatedIcc, SegmentIntegrityHandling.IgnoreAncillary)]
    [InlineData(TestImages.Jxl.XybTruncatedIcc, SegmentIntegrityHandling.IgnoreImageData)]
    public void IdentifyRejectsTruncatedHeaderFields(string file, SegmentIntegrityHandling handling)
    {
        Configuration configuration = new(new JxlConfigurationModule());
        DecoderOptions options = new(configuration) { SegmentIntegrityHandling = handling };
        using MemoryStream stream = new(TestFile.Create(file).Bytes, false);

        // Select the decoder explicitly so even an input shorter than the format-detection header reaches its field parser.
        Assert.Throws<InvalidImageContentException>(() => JxlDecoder.Instance.Identify(options, stream));
    }

    /// <summary>
    /// Verifies that codestream structure remains mandatory when optional metadata is skipped.
    /// </summary>
    /// <param name="file">The stored input with invalid level or fragment control.</param>
    [Theory]
    [InlineData(TestImages.Jxl.SquareInvalidLevel)]
    [InlineData(TestImages.Jxl.SquareDuplicateLevel)]
    [InlineData(TestImages.Jxl.SquareLateLevel)]
    [InlineData(TestImages.Jxl.SplinesDuplicateFragment)]
    [InlineData(TestImages.Jxl.SplinesMissingFinalFlag)]
    public void IdentifyRejectsMalformedStructure(string file)
    {
        Configuration configuration = new(new JxlConfigurationModule());
        DecoderOptions options = new(configuration) { SkipMetadata = true, SegmentIntegrityHandling = SegmentIntegrityHandling.IgnoreImageData };
        using MemoryStream stream = new(TestFile.Create(file).Bytes, false);

        Assert.Throws<InvalidImageContentException>(() => Image.Identify(options, stream));
    }

    /// <summary>
    /// Verifies that a complete table and frame header survive a short pixel payload only under the image-data recovery policy.
    /// </summary>
    /// <param name="handling">The selected segment error policy.</param>
    [Theory]
    [InlineData(SegmentIntegrityHandling.Strict)]
    [InlineData(SegmentIntegrityHandling.IgnoreAncillary)]
    [InlineData(SegmentIntegrityHandling.IgnoreImageData)]
    public void IdentifyRetainsFrameMetadataForTruncatedPayload(SegmentIntegrityHandling handling)
    {
        Configuration configuration = new(new JxlConfigurationModule());
        DecoderOptions options = new(configuration) { SegmentIntegrityHandling = handling };
        using MemoryStream stream = new(TestFile.Create(TestImages.Jxl.TruncatedSections).Bytes, false);

        if (handling != SegmentIntegrityHandling.IgnoreImageData)
        {
            Assert.Throws<InvalidImageContentException>(() => Image.Identify(options, stream));
        }
        else
        {
            // The original conformance header reports 2143 by 1050 pixels with 16-bit samples.
            // This stored variant removes only the final payload byte; its frame header and all 49 sizes remain intact.
            ImageInfo info = Image.Identify(options, stream);

            Assert.Equal(new Size(2143, 1050), info.Size);
            Assert.Equal(1, info.FrameCount);
            Assert.Equal(new JxlBitDepth(16, 0), info.Metadata.GetFormatMetadata(JxlFormat.Instance).BitDepth);
        }
    }

    /// <summary>
    /// Verifies plain and Brotli-compressed EXIF from native fixtures, including trailing and open-ended boxes.
    /// </summary>
    /// <param name="file">The stored auxiliary-box fixture.</param>
    [Theory]
    [InlineData(TestImages.Jxl.Exif)]
    [InlineData(TestImages.Jxl.ExifBrotli)]
    [InlineData(TestImages.Jxl.ExifBrotliTrailingFinite)]
    [InlineData(TestImages.Jxl.ExifBrotliTrailingInfinite)]
    public void IdentifyReadsReferenceExif(string file)
    {
        // One-byte I/O buffers force partial compressed input and output. The expected software name
        // comes from the original uncompressed EXIF fixture, not from our Brotli or EXIF writer.
        Configuration configuration = new(new JxlConfigurationModule()) { StreamProcessingBufferSize = 1 };
        DecoderOptions options = new(configuration) { SegmentIntegrityHandling = SegmentIntegrityHandling.Strict };
        using MemoryStream stream = new(TestFile.Create(file).Bytes, false);

        ImageInfo info = Image.Identify(options, stream);

        Assert.NotNull(info.Metadata.ExifProfile);
        Assert.True(info.Metadata.ExifProfile.TryGetValue(ExifTag.Software, out IExifValue<string> software));
        Assert.Equal("Pixelmator Pro 3.6.16", software.Value);
    }

    /// <summary>
    /// Verifies format registration and public identification using stored raw and container inputs.
    /// </summary>
    /// <param name="file">The stored reference input.</param>
    /// <param name="width">The width reported by the reference inspector.</param>
    /// <param name="height">The height reported by the reference inspector.</param>
    /// <param name="precision">The encoded color sample precision.</param>
    [Theory]
    [InlineData(TestImages.Jxl.Square, 8, 8, 8)]
    [InlineData(TestImages.Jxl.SquareExtendedSizeContainer, 8, 8, 8)]
    [InlineData(TestImages.Jxl.SquareLevel5, 8, 8, 8)]
    [InlineData(TestImages.Jxl.SplinesFragmentedV1, 2048, 2048, 8)]
    [InlineData(TestImages.Jxl.LosslessFloat, 500, 500, 32)]
    [InlineData(TestImages.Jxl.PermutedSections, 2143, 1050, 16)]
    public void IdentifyUsesRegisteredDecoder(string file, int width, int height, int precision)
    {
        Configuration configuration = new(new JxlConfigurationModule());
        DecoderOptions options = new(configuration);
        using MemoryStream stream = new(TestFile.Create(file).Bytes, false);

        ImageInfo info = Image.Identify(options, stream);

        Assert.Equal(new Size(width, height), info.Size);
        Assert.Same(JxlFormat.Instance, info.Metadata.DecodedImageFormat);
        Assert.Equal((byte)precision, info.Metadata.GetFormatMetadata(JxlFormat.Instance).BitDepth.BitsPerSample);
        Assert.True(stream.CanRead);
    }

    /// <summary>
    /// Verifies optional profiles before and after the codestream through the shared nonseekable-input wrapper.
    /// </summary>
    /// <param name="file">The stored container input.</param>
    /// <param name="skipMetadata">Whether optional EXIF and XMP profiles are omitted.</param>
    [Theory]
    [InlineData(TestImages.Jxl.ExifXmp, false)]
    [InlineData(TestImages.Jxl.ExifXmp, true)]
    [InlineData(TestImages.Jxl.ExifXmpAfterCodestream, false)]
    [InlineData(TestImages.Jxl.ExifXmpAfterCodestream, true)]
    public void IdentifyReadsOptionalMetadata(string file, bool skipMetadata)
    {
        Configuration configuration = new(new JxlConfigurationModule());
        DecoderOptions options = new(configuration) { SkipMetadata = skipMetadata };
        using MemoryStream data = new(TestFile.Create(file).Bytes, false);
        using NonSeekableStream stream = new(data);

        ImageInfo info = Image.Identify(options, stream);

        Assert.Equal(new Size(1, 1), info.Size);
        Assert.Equal(!skipMetadata, info.Metadata.ExifProfile is not null);
        Assert.Equal(!skipMetadata, info.Metadata.XmpProfile is not null);
    }

    /// <summary>
    /// Verifies pixel decoding and optional metadata in JPEG-reconstruction containers with either box order.
    /// </summary>
    /// <param name="file">The stored container with a jbrd recipe and separate EXIF and XMP boxes.</param>
    /// <param name="skipMetadata">Whether optional EXIF and XMP profiles are omitted.</param>
    [Theory]
    [InlineData(TestImages.Jxl.ExifXmp, false)]
    [InlineData(TestImages.Jxl.ExifXmp, true)]
    [InlineData(TestImages.Jxl.ExifXmpAfterCodestream, false)]
    [InlineData(TestImages.Jxl.ExifXmpAfterCodestream, true)]
    public void DecodeReadsOptionalMetadata(string file, bool skipMetadata)
    {
        Configuration configuration = new(new JxlConfigurationModule());
        DecoderOptions options = new(configuration) { SkipMetadata = skipMetadata };
        using MemoryStream data = new(TestFile.Create(file).Bytes, false);
        using NonSeekableStream stream = new(data);
        using Image<RgbaVector> image = Image.Load<RgbaVector>(options, stream);

        Assert.Equal(new Size(1, 1), image.Size);
        Assert.Equal(!skipMetadata, image.Metadata.ExifProfile is not null);
        Assert.Equal(!skipMetadata, image.Metadata.XmpProfile is not null);

        // Libjxl's public decoder produces this float sRGB value in each RGB component. The jbrd
        // recipe is not requested: pixels come from the codestream, regardless of optional box order.
        float expected = BitConverter.UInt32BitsToSingle(0x3F800FD0U);
        Vector4 actual = image[0, 0].ToVector4();
        Assert.InRange(MathF.Abs(actual.X - expected), 0F, .0005F);
        Assert.InRange(MathF.Abs(actual.Y - expected), 0F, .0005F);
        Assert.InRange(MathF.Abs(actual.Z - expected), 0F, .0005F);
        Assert.Equal(1F, actual.W);
    }

    /// <summary>
    /// Verifies that SkipMetadata keeps required ICC color data when the caller selects Preserve.
    /// </summary>
    [Fact]
    public void IdentifyPreservesIccWhenOptionalMetadataIsSkipped()
    {
        Configuration configuration = new(new JxlConfigurationModule());
        DecoderOptions options = new(configuration) { SkipMetadata = true, ColorProfileHandling = ColorProfileHandling.Preserve };
        using MemoryStream stream = new(TestFile.Create(TestImages.Jxl.WithIcc).Bytes, false);

        ImageInfo info = JxlDecoder.Instance.Identify(options, stream);

        Assert.Equal(new Size(169, 118), info.Size);
        Assert.NotNull(info.Metadata.IccProfile);
        Assert.Equal(544, info.Metadata.IccProfile.ToByteArray().Length);
        Assert.Equal(PixelAlphaRepresentation.Unassociated, info.PixelType.AlphaRepresentation);
    }

    /// <summary>
    /// Verifies native color conversion, profile retention and straight alpha through the public decoder.
    /// </summary>
    /// <param name="file">The stored profiled or compact-color input.</param>
    [Theory]
    [InlineData(TestImages.Jxl.WithIcc)]
    [InlineData(TestImages.Jxl.LinearRgbaWithIcc)]
    [InlineData(TestImages.Jxl.CmykFirstFrame)]
    [InlineData(TestImages.Jxl.CmykFourthFrame)]
    [InlineData(TestImages.Jxl.HdrPq)]
    [InlineData(TestImages.Jxl.HdrHlg)]
    [InlineData(TestImages.Jxl.Splines)]
    [InlineData(TestImages.Jxl.Noise8x8)]
    [InlineData(TestImages.Jxl.Noise)]
    [InlineData(TestImages.Jxl.SpotColor)]
    public void DecodeColorMatchesNativeSrgb(string file)
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateDecodedColors,
            file,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks independently converted float samples with each profile-retention and optional-metadata setting.
    /// </summary>
    /// <param name="file">The stored profiled or compact-color input.</param>
    /// <remarks>
    /// Original libjxl revision 91837342b24034078780b687f57f670a7def8153 decoded the source samples and profiles.
    /// Its default CMS generated the RGB and grayscale ICC results with relative-colorimetric sRGB output.
    /// The spline and noise results use the same native public decoder with float sRGB output.
    /// The spot-color input uses native composed ProPhoto RGB with both spots applied, then LittleCMS 2.17
    /// with perceptual intent, no black-point compensation, and the exact CompactSrgbV4 target.
    /// Libjxl's generated 504-byte sRGB profile has different coefficients from this 480-byte target.
    /// Noise retains native's 23 random mantissa bits. Selected coordinates cover mirrored edges, group boundaries and SIMD tails.
    /// Compact PQ/HLG results use native linear-sRGB output, then the original GetFromLinearStage sRGB operation.
    /// That operation uses the signed curve required by the shared working space. A direct CMS sRGB target instead
    /// applies its ICC curve's linear branch to all negative components when the source primaries differ.
    /// The CMYK results use LittleCMS 2.17, relative-colorimetric intent and the exact CompactSrgbV4 target profile.
    /// Black-point compensation is disabled to match the shared converter. Libjxl enables it by default.
    /// LittleCMS flags 0 and NOOPTIMIZE produce identical CMYK results across both complete images.
    /// All inputs retain their original orientation. Native and jxl-rs independently agree on the ICC inputs' source samples and profiles.
    /// </remarks>
    private static void ValidateDecodedColors(string file)
    {
        Size size = file switch
        {
            TestImages.Jxl.LinearRgbaWithIcc => new Size(4, 1),
            TestImages.Jxl.HdrPq or TestImages.Jxl.HdrHlg => new Size(1024, 1024),
            TestImages.Jxl.Splines => new Size(2048, 2048),
            TestImages.Jxl.Noise8x8 => new Size(8, 8),
            TestImages.Jxl.Noise => new Size(500, 606),
            TestImages.Jxl.SpotColor => new Size(600, 400),
            TestImages.Jxl.CmykFirstFrame => new Size(512, 512),
            TestImages.Jxl.CmykFourthFrame => new Size(110, 68),
            _ => new Size(169, 118)
        };

        string? profileHash = file switch
        {
            TestImages.Jxl.SpotColor => "CE0CAEE9506116EA94D7367D646F7FD6D0B7E82FEB8D1F3DE4EDB3BA57BAE07E",
            TestImages.Jxl.LinearRgbaWithIcc => "5FB4D2577884135C20D1E81FE1A0912907037B31486E5BCDA0029270C924E264",
            TestImages.Jxl.HdrPq or TestImages.Jxl.HdrHlg or TestImages.Jxl.Splines
                or TestImages.Jxl.Noise8x8 or TestImages.Jxl.Noise => null,
            TestImages.Jxl.CmykFirstFrame or TestImages.Jxl.CmykFourthFrame => "4855B8FABB96BDC6495D45D089BB8C8EFB1AE18389E0DC9E75A5F701A9C0B662",
            _ => "92AAEE40521D7671DBC901DFC88CC36CB83D41766B2C466FF97268EE5AB0C577"
        };

        ReadOnlySpan<(int X, int Y, uint R, uint G, uint B, uint A)> points = file switch
        {
            TestImages.Jxl.SpotColor => [
                (0, 0, 0x3F94F761U, 0xC01F27FAU, 0x3F81E5FEU, 0x3F800000U),
                (599, 399, 0x3F80142CU, 0xBFD76F68U, 0x3F745807U, 0x3F800000U),
                (255, 255, 0x3F94F761U, 0xC01F27FAU, 0x3F81E5FEU, 0x3F800000U),
                (256, 255, 0x3F94F761U, 0xC01F27FAU, 0x3F81E5FEU, 0x3F800000U),
                (255, 256, 0x3F94F761U, 0xC01F27FAU, 0x3F81E5FEU, 0x3F800000U),
                (256, 256, 0x3F94F761U, 0xC01F27FAU, 0x3F81E5FEU, 0x3F800000U),
                (300, 200, 0x3F5EFA51U, 0xBF8080CAU, 0x3F68B918U, 0x3F800000U),
                (209, 62, 0x3FAE7D64U, 0xC03D102DU, 0xBDE1667CU, 0x3F800000U),
                (326, 113, 0x3F8CE895U, 0x3F808E6EU, 0xC0059B0FU, 0x3F800000U),
                (91, 117, 0xC136BCB2U, 0x3F8C28D7U, 0x3F32DA27U, 0x3F800000U)
            ],
            TestImages.Jxl.Noise8x8 => [
                (0, 0, 0x3F82FF75U, 0x3F82EB9DU, 0x3F82EC83U, 0x3F800000U),
                (7, 0, 0x3F823A09U, 0x3F824D13U, 0x3F824C36U, 0x3F800000U),
                (4, 4, 0x3F82DB47U, 0x3F82D8C4U, 0x3F82D8E1U, 0x3F800000U),
                (0, 7, 0x3F82891AU, 0x3F828FDAU, 0x3F828F8AU, 0x3F800000U),
                (7, 7, 0x3F82B2CAU, 0x3F82B42EU, 0x3F82B41FU, 0x3F800000U)
            ],
            TestImages.Jxl.Noise => [
                (0, 0, 0x3F15824BU, 0x3F16D985U, 0x3F030E73U, 0x3F800000U),
                (255, 255, 0x3F189654U, 0x3F04BEF6U, 0x3F063E41U, 0x3F800000U),
                (256, 255, 0x3F2C1315U, 0x3F1A6AF7U, 0x3F1D01A6U, 0x3F800000U),
                (255, 256, 0x3F7E525EU, 0x3F47E5BDU, 0x3F4BDA6FU, 0x3F800000U),
                (256, 256, 0x3F3CC344U, 0x3F1BF198U, 0x3F1D9D0EU, 0x3F800000U),
                (499, 256, 0x3EF084D6U, 0x3EFA0EC0U, 0x3EF802ADU, 0x3F800000U),
                (255, 511, 0x3EE863C8U, 0x3EEF003EU, 0x3EF7475AU, 0x3F800000U),
                (256, 512, 0x3EE59B8DU, 0x3EE6FB6DU, 0x3EF2CFDEU, 0x3F800000U),
                (499, 605, 0x3EA0B155U, 0x3E99CF14U, 0x3E9BD20AU, 0x3F800000U)
            ],
            TestImages.Jxl.Splines => [
                (0, 0, 0x00000000U, 0x00000000U, 0x00000000U, 0x3F800000U),
                (64, 378, 0x3E85E841U, 0x3E424F2CU, 0x3E8C2359U, 0x3F800000U),
                (737, 129, 0x3F4678EAU, 0x3F66DB15U, 0xBE41769FU, 0x3F800000U),
                (112, 426, 0x3F139634U, 0x3EB1CD19U, 0x3F18D3BEU, 0x3F800000U),
                (1540, 1920, 0x3D22EB93U, 0x3EA17B17U, 0xBDC38580U, 0x3F800000U),
                (420, 1540, 0xB841074AU, 0x3E752E6CU, 0x3CB2CDA8U, 0x3F800000U)
            ],
            TestImages.Jxl.HdrPq => [
                (0, 0, 0x3A449D9CU, 0x3A4497BCU, 0x3A449A30U, 0x3F800000U),
                (512, 128, 0x3E450C0BU, 0x3E4508E2U, 0x3E450A34U, 0x3F800000U),
                (1023, 1023, 0x3F800107U, 0x3F7FFEB0U, 0x3F80000DU, 0x3F800000U),
                (1023, 704, 0xBF55223CU, 0x3F86C4BAU, 0x3F8103D9U, 0x3F800000U),
                (1023, 768, 0x3F9FC5D0U, 0xBEC6A300U, 0xBE12DDD4U, 0x3F800000U),
                (1023, 896, 0x3F8404B9U, 0x3F807768U, 0xBEC21DA3U, 0x3F800000U)
            ],
            TestImages.Jxl.HdrHlg => [
                (0, 0, 0x3CBD7B26U, 0x3CBD757DU, 0x3CBD77DBU, 0x3F800000U),
                (512, 128, 0x3EB06458U, 0x3EB061CDU, 0x3EB062DDU, 0x3F800000U),
                (1023, 1023, 0x3F80010AU, 0x3F7FFEB6U, 0x3F80000FU, 0x3F800000U),
                (1023, 704, 0xBF4F6FA6U, 0x3F833664U, 0x3F7B34DEU, 0x3F800000U),
                (1023, 768, 0x3F8E3095U, 0xBEAEBAC4U, 0xBDFAE69BU, 0x3F800000U),
                (1023, 896, 0x3F834FDAU, 0x3F7F8E4AU, 0xBEC0FCA8U, 0x3F800000U)
            ],
            TestImages.Jxl.LinearRgbaWithIcc => [
                (0, 0, 0x3EC757B7U, 0x3F09BE2EU, 0x3F3C8B7DU, 0x3F800000U),
                (1, 0, 0x3F3C983AU, 0x3F505365U, 0x3F61D67BU, 0x3F800000U),
                (2, 0, 0x3F71CF19U, 0x3F25AF24U, 0x3EF121B2U, 0x3F008081U),
                (3, 0, 0x3E8E478BU, 0x3EF12F54U, 0x3F1881C6U, 0x00000000U)
            ],
            TestImages.Jxl.CmykFirstFrame => [
                (0, 0, 0x3F7FFFD0U, 0x3F80000AU, 0x3F7FFFC7U, 0x3F800000U),
                (196, 134, 0xBE714484U, 0x3F38FA64U, 0x3F3502D5U, 0x3F800000U),
                (104, 138, 0xBE6BB4AEU, 0x3F38F04DU, 0x3F3461A7U, 0x3F800000U),
                (124, 150, 0x3EF20B0CU, 0x3F4D10C4U, 0x3F4C6D60U, 0x3F800000U),
                (123, 146, 0x3F06D734U, 0x3F513A30U, 0x3F50CF97U, 0x3F800000U),
                (237, 160, 0x3F3D9024U, 0x3F64E016U, 0x3F657F4EU, 0x3F800000U)
            ],
            TestImages.Jxl.CmykFourthFrame => [
                (6, 0, 0x3F7FFFD0U, 0x3F80000AU, 0x3F7FFFC7U, 0x00000000U),
                (0, 0, 0x3E5D33BCU, 0x3E546DF1U, 0x3E5655B9U, 0x3E40C0C2U),
                (1, 1, 0x3E5D33BCU, 0x3E546DF1U, 0x3E5655B9U, 0x3F800000U),
                (1, 0, 0x3E5D33BCU, 0x3E546DF1U, 0x3E5655B9U, 0x3E808081U),
                (105, 21, 0x3E5D33BCU, 0x3E546DF1U, 0x3E5655B9U, 0x3F008081U),
                (0, 1, 0x3E5D33BCU, 0x3E546DF1U, 0x3E5655B9U, 0x3F40C0C2U)
            ],
            _ => [
                (0, 0, 0x00000000U, 0x00000000U, 0x00000000U, 0x3F800000U),
                (167, 9, 0x3D70F0F2U, 0x3D70F0F2U, 0x3D70F0F2U, 0x3F018182U),
                (168, 9, 0x3E24A4A5U, 0x3E24A4A5U, 0x3E24A4A5U, 0x3F018182U),
                (168, 8, 0x3EEAEAECU, 0x3EEAEAECU, 0x3EEAEAECU, 0x3DC0C0C2U),
                (165, 6, 0x00000000U, 0x00000000U, 0x00000000U, 0x3DB8B8B9U),
                (163, 2, 0x00000000U, 0x00000000U, 0x00000000U, 0x3F018182U)
            ]
        };

        Configuration configuration = new(new JxlConfigurationModule());
        byte[] data = TestFile.Create(file).Bytes;
        // Embedded ICC inputs need every retention/metadata combination. Compact inputs have no
        // optional profile to retain; Convert with metadata skipped proves that mandatory color and feature data remain active.
        ReadOnlySpan<ColorProfileHandling> policies = profileHash is null
            ? [ColorProfileHandling.Convert]
            : [ColorProfileHandling.Preserve, ColorProfileHandling.Compact, ColorProfileHandling.Convert];

        ReadOnlySpan<bool> metadataModes = profileHash is null ? [true] : [false, true];
        foreach (ColorProfileHandling handling in policies)
        {
            foreach (bool skipMetadata in metadataModes)
            {
                DecoderOptions options = new(configuration) { ColorProfileHandling = handling, SkipMetadata = skipMetadata };
                using MemoryStream input = new(data, false);
                using Image<RgbaVector> image = Image.Load<RgbaVector>(options, input);
                Assert.Equal(size, image.Size);

                // All policies convert pixels. Compact color descriptions have no embedded ICC profile.
                // Convert removes noncanonical embedded profiles; SkipMetadata must retain required color information.
                if (handling == ColorProfileHandling.Convert || profileHash is null)
                {
                    Assert.Null(image.Metadata.IccProfile);
                }
                else
                {
                    Assert.NotNull(image.Metadata.IccProfile);
                    Assert.Equal(profileHash, Convert.ToHexString(SHA256.HashData(image.Metadata.IccProfile.ToByteArray())));
                }

                foreach ((int x, int y, uint r, uint g, uint b, uint a) in points)
                {
                    Vector4 actual = image[x, y].ToVector4();
                    Vector4 expected = new(
                        BitConverter.UInt32BitsToSingle(r), BitConverter.UInt32BitsToSingle(g),
                        BitConverter.UInt32BitsToSingle(b), BitConverter.UInt32BitsToSingle(a));

                    // CompandingTests establishes the shared power bound. The linear RGB input changes
                    // by up to 0.287 during native ICC conversion, so omitting that conversion fails this check.
                    for (int component = 0; component < 3; component++)
                    {
                        float bound = .0005F * MathF.Max(1F, MathF.Abs(expected[component]));
                        Assert.True(MathF.Abs(actual[component] - expected[component]) <= bound,
                            $"{file}, ({x},{y}), component {component}: {actual[component]:R}, native {expected[component]:R}.");
                    }

                    // Alpha bypasses ICC. Permit only its existing integer-to-float normalization rounding,
                    // including retention of straight RGB at zero alpha in the final linear-RGB sample.
                    Assert.InRange(MathF.Abs(actual.W - expected.W), 0F, .0000001F);
                }
            }
        }
    }

    /// <summary>
    /// Verifies the public format identifiers.
    /// </summary>
    [Fact]
    public void FormatProperties_AreAsExpected()
    {
        JxlFormat format = JxlFormat.Instance;

        Assert.Equal("JPEG XL", format.Name);
        Assert.Equal("image/jxl", format.DefaultMimeType);
        Assert.Contains("image/jxl", format.MimeTypes);
        Assert.Contains("jxl", format.FileExtensions);
    }

    /// <summary>
    /// Verifies both signatures, partial headers and headers with trailing bytes.
    /// </summary>
    /// <param name="hex">The header bytes in hexadecimal notation.</param>
    /// <param name="expected">Whether the header identifies JPEG XL.</param>
    [Theory]
    [InlineData("", false)]
    [InlineData("FF", false)]
    [InlineData("FFD8FF", false)]
    [InlineData("FF0A", true)]
    [InlineData("FF0A123456", true)]
    [InlineData("0000000C4A584C200D0A87", false)]
    [InlineData("0000000C4A584C200D0A870A", true)]
    [InlineData("0000000C4A584C200D0A870A0000001466747970", true)]
    [InlineData("0000000C4A584C200D0A870B", false)]
    public void DetectorRecognizesOnlyCompleteSignatures(string hex, bool expected)
    {
        // These are format signatures, not generated image fixtures. No image header or pixel data is constructed.
        byte[] header = Convert.FromHexString(hex);
        JxlImageFormatDetector detector = new();

        bool detected = detector.TryDetectFormat(header, out IImageFormat format);

        Assert.Equal(expected, detected);
        Assert.Same(expected ? JxlFormat.Instance : null, format);
    }

    /// <summary>
    /// Verifies that specialized options retain the caller's configuration and policies.
    /// </summary>
    [Fact]
    public void DecoderOptionsRetainGeneralOptions()
    {
        DecoderOptions generalOptions = new(new Configuration())
        {
            MaxFrames = 2,
            SkipMetadata = true,
            TargetSize = new Size(64, 32)
        };

        JxlDecoderOptions options = new(generalOptions);

        Assert.Same(generalOptions, options.GeneralOptions);
    }

    /// <summary>
    /// Verifies the specialized options' public constructor boundary.
    /// </summary>
    [Fact]
    public void DecoderOptionsRejectNullGeneralOptions()
        => Assert.Throws<ArgumentNullException>(() => new JxlDecoderOptions(null));

    /// <summary>
    /// Checks every decoded pixel in one hardware configuration against independently decoded native pixels.
    /// </summary>
    /// <param name="file">The stored reference fixture.</param>
    /// <param name="pixelHash">The expected SHA-256 of complete unassociated RGBA output.</param>
    private static void ValidateModularPixels(string file, string pixelHash)
    {
        Configuration configuration = new(new JxlConfigurationModule());
        DecoderOptions options = new(configuration);
        using MemoryStream input = new(TestFile.Create(file).Bytes, false);
        using Image<Rgba32> image = Image.Load<Rgba32>(options, input);
        byte[] pixels = new byte[image.Width * image.Height * 4];
        image.CopyPixelDataTo(pixels);

        // Compact inputs declare the sRGB transfer curve. The profiled gray patch input has the same curve;
        // its native sRGB conversion produces the same complete byte image as the compact variant.
        // The hash covers every channel of every pixel, including RGB beneath transparent alpha.
        Assert.Equal(pixelHash, Convert.ToHexString(SHA256.HashData(pixels)));
    }

    /// <summary>
    /// Checks native reversible color equations against fixed integer results with complete vector blocks and a scalar tail.
    /// </summary>
    private static void ValidateInverseModularColorTransform()
    {
        ReadOnlySpan<int> firstValues = [-7, int.MaxValue, int.MinValue, 9, 0, -1, 42];
        ReadOnlySpan<int> secondValues = [10, 2, -3, -5, int.MinValue, -1, 17];
        ReadOnlySpan<int> thirdValues = [2, 2, -1, -7, int.MaxValue, -1, -103];

        // libjxl InvRCTRow defines wrapping 32-bit sums and signed right shifts, including before the average in operations four and five.
        // Operation five uses the restored third channel in that average. Operation six reverses YCoCg in two successive half-shift steps.
        // Each operation below lists seven first-channel results, seven second-channel results, then seven third-channel results.
        ReadOnlySpan<int> expected =
        [
            // No arithmetic.
            -7, int.MaxValue, int.MinValue, 9, 0, -1, 42,
            10, 2, -3, -5, int.MinValue, -1, 17,
            2, 2, -1, -7, int.MaxValue, -1, -103,

            // Add the first channel to the third.
            -7, int.MaxValue, int.MinValue, 9, 0, -1, 42,
            10, 2, -3, -5, int.MinValue, -1, 17,
            -5, -2147483647, int.MaxValue, 2, int.MaxValue, -2, -61,

            // Add the first channel to the second.
            -7, int.MaxValue, int.MinValue, 9, 0, -1, 42,
            3, -2147483647, 2147483645, 4, int.MinValue, -2, 59,
            2, 2, -1, -7, int.MaxValue, -1, -103,

            // Add the first channel to both other channels.
            -7, int.MaxValue, int.MinValue, 9, 0, -1, 42,
            3, -2147483647, 2147483645, 4, int.MinValue, -2, 59,
            -5, -2147483647, int.MaxValue, 2, int.MaxValue, -2, -61,

            // Add the signed average of the first and third channels to the second.
            -7, int.MaxValue, int.MinValue, 9, 0, -1, 42,
            7, -1073741822, 1073741820, -4, -1073741825, -2, -14,
            2, 2, -1, -7, int.MaxValue, -1, -103,

            // Restore the third channel, then use it in the signed average added to the second.
            -7, int.MaxValue, int.MinValue, 9, 0, -1, 42,
            4, 2, -4, 0, -1073741825, -3, 7,
            -5, -2147483647, int.MaxValue, 2, int.MaxValue, -2, -61,

            // Reverse YCoCg, with first = Y, second = Co and third = Cg.
            -3, int.MaxValue, int.MinValue, 11, -2147483647, 0, 103,
            -6, int.MinValue, int.MinValue, 6, 1073741824, -1, -9,
            -13, 2147483645, -2147483645, 16, 1, 1, 86
        ];

        // Thirty-one columns contain complete 512-, 256- and 128-bit blocks, followed by three scalar columns.
        // A seven-value period puts different samples at each block boundary instead of repeating vector-sized groups.
        int length = (2 * Vector512<int>.Count) - 1;
        int[] first = new int[length];
        int[] second = new int[length];
        int[] third = new int[length];
        int sampleCount = firstValues.Length;

        for (int operation = 0; operation <= 6; operation++)
        {
            for (int i = 0; i < length; i++)
            {
                int sample = i % sampleCount;
                first[i] = firstValues[sample];
                second[i] = secondValues[sample];
                third[i] = thirdValues[sample];
            }

            JxlModularColorTransform.Apply(operation, first, second, third);

            int offset = operation * 3 * sampleCount;
            for (int i = 0; i < length; i++)
            {
                int sample = i % sampleCount;

                Assert.Equal(expected[offset + sample], first[i]);
                Assert.Equal(expected[offset + sampleCount + sample], second[i]);
                Assert.Equal(expected[offset + (2 * sampleCount) + sample], third[i]);
            }
        }

        // jxl-rs rct_impl_i16 stores wrapping signed 16-bit results. Operation five wraps the
        // restored third value before averaging it with the first. Its second sample distinguishes
        // that rule from retaining the wide third value until the final store.
        ReadOnlySpan<short> shortFirst = [-7, short.MaxValue, short.MinValue, 9, 0, -1, 42];
        ReadOnlySpan<short> shortSecond = [10, 2, -3, -5, short.MinValue, -1, 17];
        ReadOnlySpan<short> shortThird = [2, 2, -1, -7, short.MaxValue, -1, -103];
        ReadOnlySpan<short> shortExpected =
        [

            // Operation 0.
            -7, 32767, -32768, 9, 0, -1, 42,
            10, 2, -3, -5, -32768, -1, 17,
            2, 2, -1, -7, 32767, -1, -103,

            // Operation 1.
            -7, 32767, -32768, 9, 0, -1, 42,
            10, 2, -3, -5, -32768, -1, 17,
            -5, -32767, 32767, 2, 32767, -2, -61,

            // Operation 2.
            -7, 32767, -32768, 9, 0, -1, 42,
            3, -32767, 32765, 4, -32768, -2, 59,
            2, 2, -1, -7, 32767, -1, -103,

            // Operation 3.
            -7, 32767, -32768, 9, 0, -1, 42,
            3, -32767, 32765, 4, -32768, -2, 59,
            -5, -32767, 32767, 2, 32767, -2, -61,

            // Operation 4.
            -7, 32767, -32768, 9, 0, -1, 42,
            7, 16386, -16388, -4, -16385, -2, -14,
            2, 2, -1, -7, 32767, -1, -103,

            // Operation 5.
            -7, 32767, -32768, 9, 0, -1, 42,
            4, 2, -4, 0, -16385, -3, 7,
            -5, -32767, 32767, 2, 32767, -2, -61,

            // Operation 6.
            -3, 32767, -32768, 11, -32767, 0, 103,
            -6, -32768, -32768, 6, 16384, -1, -9,
            -13, 32765, -32765, 16, 1, 1, 86
        ];

        short[] first16 = new short[length];
        short[] second16 = new short[length];
        short[] third16 = new short[length];
        for (int operation = 0; operation <= 6; operation++)
        {
            for (int i = 0; i < length; i++)
            {
                int sample = i % sampleCount;
                first16[i] = shortFirst[sample];
                second16[i] = shortSecond[sample];
                third16[i] = shortThird[sample];
            }

            JxlModularColorTransform.Apply<short>(operation, first16, second16, third16);

            int offset = operation * 3 * sampleCount;
            for (int i = 0; i < length; i++)
            {
                int sample = i % sampleCount;

                Assert.Equal(shortExpected[offset + sample], first16[i]);
                Assert.Equal(shortExpected[offset + sampleCount + sample], second16[i]);
                Assert.Equal(shortExpected[offset + (2 * sampleCount) + sample], third16[i]);
            }
        }
    }

    /// <summary>
    /// Checks fixed native squeeze equations with complete transposed batches and every smaller remainder.
    /// </summary>
    private static void ValidateInverseModularSqueeze()
    {
        ReadOnlySpan<int> averages = [-17, -9, -2, 4, 9, 21, 18, 7, -3, -14, -14, 5, 19, 13, 0, -8, 3, 18, 27, 9, -15, -21, -8, 0, 17, 29, 12, -4, -9, 6, 14];
        ReadOnlySpan<int> residuals = [-3, 1, -1, 4, 0, 3, -5, 7, -9, 1, 0, 4, -3, 2, -7, 3, 1, -5, 9, -3, 5, -1, 7, 0, -3, 5, -7, 1, 3, -5, -3];

        // Fixed results use libjxl's SmoothTendency equation: (4 * previous - 3 * next - average +/- 6) / 12,
        // followed by its two parity limits, then first = average + difference / 2 and second = first - difference.
        // Division truncates toward zero. The first pair has previous = average but a nonzero tendency of -1;
        // negative odd differences and flat neighboring averages distinguish this from a sign-bit-only trend test.
        ReadOnlySpan<int> expected =
        [
            -19, -15, -10, -7, -4, 0, 4, 3, 7, 12, 22, 19, 17, 19, 14, 0, -5, 0, -14, -15, -15, -12, 3, 8, 18, 21, 17, 9, -1, 1, -7, -10,
            0, 7, 13, 24, 31, 22, 12, 5, -9, -22, -23, -19, -8, -9, -3, 4, 12, 22, 31, 26, 13, 11, -1, -8, -8, -11, 0, 13, 13, 16
        ];

        ValidateSqueezeLayout<int>(averages, residuals, expected, true, false, true);
        ValidateSqueezeLayout<int>(averages, residuals, expected, true, true, true);
        ValidateSqueezeLayout<int>(averages, residuals, expected, false, false, true);
        ValidateSqueezeLayout<int>(averages, residuals, expected, false, true, true);

        // The same bounded sequences have the same exact reconstruction in signed 16-bit storage.
        // Keep the full-width extreme cases below on int storage; they do not fit short samples.
        ValidateSqueezeLayout<short>(averages, residuals, expected, true, false, true);
        ValidateSqueezeLayout<short>(averages, residuals, expected, true, true, true);
        ValidateSqueezeLayout<short>(averages, residuals, expected, false, false, true);
        ValidateSqueezeLayout<short>(averages, residuals, expected, false, true, true);

        ReadOnlySpan<int> extremes =
        [
            int.MinValue, int.MaxValue, 0, int.MaxValue, int.MinValue, -1, 1,
            int.MinValue, int.MaxValue, 0, int.MaxValue, int.MinValue, -1, 1,
            int.MinValue, int.MaxValue, 0, int.MaxValue, int.MinValue, -1, 1,
            int.MinValue, int.MaxValue, 0, int.MaxValue, int.MinValue, -1, 1,
            int.MinValue, int.MaxValue, 0
        ];

        // The native scalar equation uses wide signed intermediates, then stores signed 32-bit samples.
        // These fixed values detect premature subtraction, absolute-value or difference overflow in SIMD lanes.
        ReadOnlySpan<int> extremeExpected =
        [
            2147483646, -2147483646, 2147483647, 2147483646, 0, 1, -2147483647, 2147483645, int.MinValue, int.MinValue, -1, 0, -1, 4,
            -2147483645, 2147483644, 2147483643, -2147483644, -626349396, 626349396, int.MaxValue, int.MaxValue, -2147483646, 2147483646, -2, 1, 2, -1,
            2147483645, -2147483644, int.MinValue, 2147483645, 0, -1, 2147483645, -2147483646, -2147483644, 2147483643, -2, 1, 4, -2,
            int.MinValue, -2147483647, -2147483646, 2147483643, 0, 0, 2147483646, -2147483647, -2147483646, 2147483645, -4, 3, 4, -2,
            -2147483647, 2147483646, 2147483645, -2147483646, -1, 2
        ];

        ValidateSqueezeLayout<int>(extremes, residuals, extremeExpected, true, true, false);
        ValidateSqueezeLayout<int>(extremes, residuals, extremeExpected, false, true, false);

        // Squeezing a one-sample dimension produces a valid empty residual plane. Reconstruction copies the sole average.
        ValidateSqueezeLayout<int>([7], [], [], true, true, true);
        ValidateSqueezeLayout<int>([7], [], [], false, true, true);
        ValidateSqueezeLayout<short>([7], [], [], true, true, true);
        ValidateSqueezeLayout<short>([7], [], [], false, true, true);
    }

    /// <summary>
    /// Places an independently specified sequence in either orientation and verifies every output sample.
    /// </summary>
    /// <typeparam name="TSample">The signed int or short channel storage tested with the same fixed sequences.</typeparam>
    /// <param name="averageValues">The average sequence. An odd edge repeats its last value.</param>
    /// <param name="residualValues">The residual sequence.</param>
    /// <param name="expected">The fixed reconstructed pairs.</param>
    /// <param name="horizontal">Whether the sequence runs across columns instead of rows.</param>
    /// <param name="oddEdge">Whether one unpaired average follows the reconstructed pairs.</param>
    /// <param name="varyRows">Whether independent sequences receive distinct finite offsets.</param>
    private static void ValidateSqueezeLayout<TSample>(
        ReadOnlySpan<int> averageValues, ReadOnlySpan<int> residualValues, ReadOnlySpan<int> expected, bool horizontal, bool oddEdge, bool varyRows)
        where TSample : unmanaged, IBinaryInteger<TSample>
    {
        // Thirty-one independent sequences exercise horizontal row batches of 16, 8 and 4, then three scalar rows.
        // Transposing this setup exercises vertical SIMD blocks of the same widths and a three-column scalar tail.
        const int independentCount = 31;
        int pairCount = residualValues.Length;
        int averageCount = pairCount + (oddEdge ? 1 : 0);
        int outputCount = expected.Length + (oddEdge ? 1 : 0);
        Size averageSize = horizontal ? new(averageCount, independentCount) : new(independentCount, averageCount);
        Size residualSize = horizontal ? new(pairCount, independentCount) : new(independentCount, pairCount);
        Size outputSize = horizontal ? new(outputCount, independentCount) : new(independentCount, outputCount);
        using Buffer2D<TSample> averages = MemoryAllocator.Default.Allocate2D<TSample>(averageSize);
        using Buffer2D<TSample> residuals = pairCount == 0 ? null : MemoryAllocator.Default.Allocate2D<TSample>(residualSize);
        using Buffer2D<TSample> output = MemoryAllocator.Default.Allocate2D<TSample>(outputSize);
        Buffer2DRegion<TSample> residualRegion = residuals is null ? default : residuals.GetRegion();

        for (int sequence = 0; sequence < independentCount; sequence++)
        {
            int offset = varyRows ? sequence * 37 : 0;

            for (int i = 0; i < averageCount; i++)
            {
                int value = averageValues[Math.Min(i, averageValues.Length - 1)] + offset;
                averages[horizontal ? i : sequence, horizontal ? sequence : i] = TSample.CreateTruncating(value);
            }

            for (int i = 0; i < pairCount; i++)
            {
                residualRegion[horizontal ? i : sequence, horizontal ? sequence : i] = TSample.CreateTruncating(residualValues[i]);
            }
        }

        JxlModularSqueeze.Apply<TSample>(averages.GetRegion(), residualRegion, output.GetRegion(), horizontal, CancellationToken.None);

        for (int sequence = 0; sequence < independentCount; sequence++)
        {
            int offset = varyRows ? sequence * 37 : 0;

            for (int i = 0; i < expected.Length; i++)
            {
                Assert.Equal(TSample.CreateTruncating(expected[i] + offset), output[horizontal ? i : sequence, horizontal ? sequence : i]);
            }

            if (oddEdge)
            {
                Assert.Equal(
                    TSample.CreateTruncating(averageValues[^1] + offset),
                    output[horizontal ? outputCount - 1 : sequence, horizontal ? sequence : outputCount - 1]);
            }
        }
    }

    /// <summary>
    /// Checks fixed native cross-channel properties, including signed storage of wide residuals and absolute values.
    /// </summary>
    private static void ValidateModularCrossChannelProperties()
    {
        ReadOnlySpan<int> rowValues = [int.MinValue, int.MaxValue, -7, 0, 19, -31, int.MaxValue];
        ReadOnlySpan<int> topValues = [int.MaxValue, int.MinValue, -2, int.MinValue, 23, 17, -int.MaxValue];
        ReadOnlySpan<int> absoluteValues = [int.MinValue, int.MaxValue, 7, 0, 19, 31, int.MaxValue];

        // Native PrecomputeReferences uses gradient = left + top - topLeft, bounded between left and top.
        // The first row uses top = topLeft = left, so its prediction is left. These fixed results include
        // signed 32-bit property storage. For example, abs(MinValue - MaxValue) stores -1.
        ReadOnlySpan<int> firstAbsoluteResiduals = [-1, -1, -2147483642, 7, 19, 50, -2147483618];
        ReadOnlySpan<int> firstResiduals = [1, -1, 2147483642, 7, 19, -50, -2147483618];
        ReadOnlySpan<int> followingAbsoluteResiduals = [-1, -1, -2147483642, int.MinValue, 4, 48, -2];
        ReadOnlySpan<int> followingResiduals = [1, -1, 2147483642, int.MinValue, -4, -48, -2];
        int[] row = new int[64];
        int[] top = new int[64];
        int[] actualAbsoluteValues = new int[31];
        int[] actualValues = new int[31];
        int[] actualAbsoluteResiduals = new int[31];
        int[] actualResiduals = new int[31];

        for (int x = 0; x < row.Length; x++)
        {
            row[x] = rowValues[x % rowValues.Length];
            top[x] = topValues[x % topValues.Length];
        }

        // Start zero verifies the native left-edge rule. Starts one and 33 must borrow the real preceding sample,
        // not treat a new block as an image edge. Thirty-one outputs cover 16-, 8-, 4-lane blocks and scalar tails.
        ReadOnlySpan<int> starts = [0, 1, 33];
        for (int rowIndex = 0; rowIndex < 2; rowIndex++)
        {
            bool hasTop = rowIndex != 0;
            ReadOnlySpan<int> expectedAbsoluteResiduals = hasTop ? followingAbsoluteResiduals : firstAbsoluteResiduals;
            ReadOnlySpan<int> expectedResiduals = hasTop ? followingResiduals : firstResiduals;

            foreach (int start in starts)
            {
                JxlModularPredictor.PrepareReferences(
                    row, hasTop ? top : [], start, hasTop, actualAbsoluteValues, actualValues, actualAbsoluteResiduals, actualResiduals);

                for (int i = 0; i < actualValues.Length; i++)
                {
                    int x = start + i;
                    int sample = x % rowValues.Length;
                    int expectedAbsoluteResidual = x == 0 ? hasTop ? -1 : int.MinValue : expectedAbsoluteResiduals[sample];
                    int expectedResidual = x == 0 ? hasTop ? 1 : int.MinValue : expectedResiduals[sample];

                    // At column three, the following-row gradient is below Int32.MinValue before bounding.
                    // Exact expectations therefore detect overflow before clamping, not just scalar/SIMD agreement.
                    Assert.Equal(absoluteValues[sample], actualAbsoluteValues[i]);
                    Assert.Equal(rowValues[sample], actualValues[i]);
                    Assert.Equal(expectedAbsoluteResidual, actualAbsoluteResiduals[i]);
                    Assert.Equal(expectedResidual, actualResiduals[i]);
                }
            }
        }
    }
}
