// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Numerics.Tensors;
using System.Runtime.Intrinsics;
using System.Security.Cryptography;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Exr;
using SixLabors.ImageSharp.Formats.Exr.Constants;
using SixLabors.ImageSharp.Formats.Jxl;
using SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;
using SixLabors.ImageSharp.Formats.Jxl.Components.Modular;
using SixLabors.ImageSharp.Formats.Jxl.Components.VarDct;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests.Memory;
using SixLabors.ImageSharp.Tests.TestUtilities;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs;
using BlendMode = SixLabors.ImageSharp.Formats.Jxl.Components.Decoder.JxlBlending.BlendMode;

namespace SixLabors.ImageSharp.Tests.Formats.Jxl;

[Trait("Format", "Jxl")]
[ValidateDisposedMemoryAllocations]
public class JxlDecoderTests
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
    /// Verifies all eight native blend equations and selected-weight behavior at every SIMD width and scalar fallback.
    /// </summary>
    [Fact]
    public void BlendEquationsMatchNativeAcrossSimdWidths()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateBlendEquations,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Verifies native blend weight boundaries and optional clamping at every SIMD width and scalar fallback.
    /// </summary>
    [Fact]
    public void BlendWeightBoundariesMatchNativeAcrossSimdWidths()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateBlendWeightBoundaries,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks literal results for background eight, foreground two, and equal half weights, then checks weight-channel outputs.
    /// </summary>
    private static void ValidateBlendEquations()
    {
        // These literal results follow libjxl blending.cc and alpha.cc. Binary-exact inputs retain exact
        // final results with either fused or separate multiply/add operations. This checks component arithmetic,
        // not encoded patch-mode coverage: the stored image fixtures currently exercise color Add and extra None.
        ReadOnlySpan<(BlendMode Mode, bool Associated, float Expected)> cases =
        [
            (BlendMode.None, false, 8F),
            (BlendMode.Replace, false, 2F),
            (BlendMode.Add, false, 10F),
            (BlendMode.Multiply, false, 16F),
            (BlendMode.BlendAbove, false, 4F),
            (BlendMode.BlendBelow, false, 6F),
            (BlendMode.AlphaWeightedAddAbove, false, 9F),
            (BlendMode.AlphaWeightedAddBelow, false, 6F),
            (BlendMode.BlendAbove, true, 6F),
            (BlendMode.BlendBelow, true, 9F)
        ];

        foreach ((BlendMode mode, bool associated, float expected) in cases)
        {
            AssertBlendResult(new JxlBlending(mode, 0, false), associated, false, [8F], [2F], [0.5F], [0.5F], [expected]);
        }

        ReadOnlySpan<(BlendMode Mode, float Expected)> weightCases =
        [
            (BlendMode.BlendAbove, 0.625F),
            (BlendMode.BlendBelow, 0.625F),
            (BlendMode.AlphaWeightedAddAbove, 0.25F),
            (BlendMode.AlphaWeightedAddBelow, 0.5F)
        ];

        foreach ((BlendMode mode, float expected) in weightCases)
        {
            // Native alpha.cc recognizes the selected weight channel itself. Alpha blending returns
            // the combined weight. Weighted addition preserves the bottom weight without weighting it again.
            AssertBlendResult(new JxlBlending(mode, 0, false), false, true, [0.25F], [0.5F], [0.25F], [0.5F], [expected]);
        }
    }

    /// <summary>
    /// Checks mixed positive, zero and negative combined weights, top-only clamping, and selected-weight preservation.
    /// </summary>
    private static void ValidateBlendWeightBoundaries()
    {
        ReadOnlySpan<float> backgroundWeights = [0.5F, 0F, -1F, 2F];
        ReadOnlySpan<float> foregroundWeights = [0.5F, 0F, 0F, 1.5F];

        // Native alpha.cc uses a zero reciprocal for nonpositive combined weights when restoring
        // unassociated color. Pixel unassociation instead preserves stored RGB at zero alpha.
        // The last lane also proves that only the selected top weight is bounded when clamping is enabled.
        ReadOnlySpan<(BlendMode Mode, bool Associated, bool SelectedWeight, bool Clamp, float[] Expected)> cases =
        [
            (BlendMode.BlendAbove, false, false, false, [4F, 0F, 0F, -10F]),
            (BlendMode.BlendBelow, false, false, false, [6F, 0F, 0F, 26F]),
            (BlendMode.BlendAbove, false, false, true, [4F, 0F, 0F, 2F]),
            (BlendMode.BlendBelow, false, false, true, [6F, 0F, 0F, 8F]),
            (BlendMode.BlendAbove, true, false, false, [6F, 10F, 10F, -2F]),
            (BlendMode.BlendBelow, true, false, false, [9F, 10F, 12F, 6F]),
            (BlendMode.BlendAbove, true, false, true, [6F, 10F, 10F, 2F]),
            (BlendMode.BlendBelow, true, false, true, [9F, 10F, 10F, 8F]),
            (BlendMode.AlphaWeightedAddAbove, false, false, false, [9F, 8F, 8F, 11F]),
            (BlendMode.AlphaWeightedAddBelow, false, false, false, [6F, 2F, -6F, 18F]),
            (BlendMode.AlphaWeightedAddAbove, false, false, true, [9F, 8F, 8F, 10F]),
            (BlendMode.AlphaWeightedAddBelow, false, false, true, [6F, 2F, 2F, 10F]),
            (BlendMode.BlendAbove, false, true, true, [0.75F, 0F, -1F, 1F]),
            (BlendMode.BlendBelow, false, true, true, [0.75F, 0F, 0F, 1F]),
            (BlendMode.AlphaWeightedAddAbove, false, true, true, [0.5F, 0F, -1F, 2F]),
            (BlendMode.AlphaWeightedAddBelow, false, true, true, [0.5F, 0F, 0F, 1.5F])
        ];

        foreach ((BlendMode mode, bool associated, bool selectedWeight, bool clamp, float[] expected) in cases)
        {
            ReadOnlySpan<float> background = selectedWeight ? backgroundWeights : [8F];
            ReadOnlySpan<float> foreground = selectedWeight ? foregroundWeights : [2F];
            AssertBlendResult(
                new JxlBlending(mode, 0, clamp),
                associated,
                selectedWeight,
                background,
                foreground,
                backgroundWeights,
                foregroundWeights,
                expected);
        }

        // Multiplication bounds the foreground component itself. It does not use a separate weight channel.
        AssertBlendResult(new JxlBlending(BlendMode.Multiply, 0, false), false, false, [8F], [-2F, 0F, 0.5F, 2F], [0F], [0F], [-16F, 0F, 4F, 16F]);
        AssertBlendResult(new JxlBlending(BlendMode.Multiply, 0, true), false, false, [8F], [-2F, 0F, 0.5F, 2F], [0F], [0F], [0F, 0F, 4F, 8F]);
    }

    /// <summary>
    /// Checks component and packed RGB blend results using fixed numeric patterns, complete SIMD blocks and three-component tails.
    /// </summary>
    /// <param name="operation">The selected codec equation and clamp setting.</param>
    /// <param name="associated">Whether the selected weight has associated-alpha semantics.</param>
    /// <param name="selectedWeight">Whether the component itself is the selected weight.</param>
    /// <param name="backgroundPattern">The literal background components, repeated without arithmetic.</param>
    /// <param name="foregroundPattern">The literal foreground components, repeated without arithmetic.</param>
    /// <param name="backgroundWeightPattern">The literal background weights.</param>
    /// <param name="foregroundWeightPattern">The literal foreground weights.</param>
    /// <param name="expectedPattern">The independently specified literal output components.</param>
    private static void AssertBlendResult(
        JxlBlending operation,
        bool associated,
        bool selectedWeight,
        ReadOnlySpan<float> backgroundPattern,
        ReadOnlySpan<float> foregroundPattern,
        ReadOnlySpan<float> backgroundWeightPattern,
        ReadOnlySpan<float> foregroundWeightPattern,
        ReadOnlySpan<float> expectedPattern)
    {
        const int length = 35;
        float[] background = new float[length];
        float[] foreground = new float[length];
        float[] backgroundWeight = new float[length];
        float[] foregroundWeight = new float[length];
        float[] expected = new float[length];
        float[] workspace = new float[6 * length];
        Span<float> destination = workspace.AsSpan(0, length);
        Span<float> bounded = workspace.AsSpan(length, length);
        Span<float> complement = workspace.AsSpan(2 * length, length);
        Span<float> alpha = workspace.AsSpan(3 * length, length);
        Span<float> contribution = workspace.AsSpan(4 * length, length);
        Span<float> reciprocal = workspace.AsSpan(5 * length, length);

        // Thirty-five lanes cover two 512-bit blocks, four 256-bit blocks or eight 128-bit blocks,
        // followed by three scalar values. The mixed patterns put zero and negative weights in that tail.
        for (int i = 0; i < length; i++)
        {
            background[i] = backgroundPattern[i % backgroundPattern.Length];
            foreground[i] = foregroundPattern[i % foregroundPattern.Length];
            backgroundWeight[i] = backgroundWeightPattern[i % backgroundWeightPattern.Length];
            foregroundWeight[i] = foregroundWeightPattern[i % foregroundWeightPattern.Length];
            expected[i] = expectedPattern[i % expectedPattern.Length];
        }

        if (operation.Mode >= BlendMode.BlendAbove)
        {
            operation.PrepareWeights(associated, selectedWeight, backgroundWeight, foregroundWeight, bounded, complement, alpha, reciprocal);
        }

        operation.Apply(
            associated,
            selectedWeight,
            background,
            foreground,
            backgroundWeight,
            foregroundWeight,
            destination,
            bounded,
            complement,
            alpha,
            contribution,
            reciprocal);

        Assert.Equal(expected.AsSpan(), destination);

        if (!selectedWeight && operation.Mode is BlendMode.BlendAbove or BlendMode.BlendBelow)
        {
            float[] backgroundGreen = new float[length];
            float[] backgroundBlue = new float[length];
            float[] foregroundGreen = new float[length];
            float[] foregroundBlue = new float[length];
            Vector4[] backgroundColors = new Vector4[length];
            Vector4[] foregroundColors = new Vector4[length];

            // Unequal channels expose packing or lane-order errors. Powers of two retain the
            // exact contract results without introducing another blend equation into the assertions.
            for (int i = 0; i < length; i++)
            {
                backgroundGreen[i] = background[i] * 2F;
                backgroundBlue[i] = background[i] * 4F;
                foregroundGreen[i] = foreground[i] * 2F;
                foregroundBlue[i] = foreground[i] * 4F;
            }

            // PrepareWeights bounds only the top input. Pack those prepared weights directly,
            // including negative or extended bottom weights, without pixel normalization or clipping.
            ReadOnlySpan<float> packedBackgroundWeight = operation.Clamp && operation.Mode == BlendMode.BlendBelow ? bounded : backgroundWeight;
            ReadOnlySpan<float> packedForegroundWeight = operation.Clamp && operation.Mode == BlendMode.BlendAbove ? bounded : foregroundWeight;
            SimdUtils.InterleaveFloatPlanes(background, backgroundGreen, backgroundBlue, packedBackgroundWeight, backgroundColors);
            SimdUtils.InterleaveFloatPlanes(foreground, foregroundGreen, foregroundBlue, packedForegroundWeight, foregroundColors);

            operation.ApplyColors(associated, backgroundColors, foregroundColors);

            // Both composition orders return RGB in the background buffer. The shared kernel's W
            // is not the native combined weight. Discard it and use the separately prepared reciprocal.
            SimdUtils.DeinterleaveFloatPlanes(backgroundColors, destination, backgroundGreen, backgroundBlue, []);
            if (!associated)
            {
                TensorPrimitives.Multiply(destination, reciprocal, destination);
                TensorPrimitives.Multiply(backgroundGreen, reciprocal, backgroundGreen);
                TensorPrimitives.Multiply(backgroundBlue, reciprocal, backgroundBlue);
            }

            Assert.Equal(expected.AsSpan(), destination);
            for (int i = 0; i < length; i++)
            {
                Assert.Equal(expected[i] * 2F, backgroundGreen[i]);
                Assert.Equal(expected[i] * 4F, backgroundBlue[i]);
            }
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
    /// Verifies that automatic decoding retains colored spot output and its sample precision on an eight-bit grayscale base.
    /// </summary>
    /// <param name="file">The stored native grayscale input with a red spot channel.</param>
    /// <param name="spotBits">The encoded spot sample precision.</param>
    [Theory]
    [InlineData(TestImages.Jxl.Gray8Spot8, 8)]
    [InlineData(TestImages.Jxl.Gray8Spot16, 16)]
    [InlineData(TestImages.Jxl.Gray8SpotFloat, 32)]
    public void DecodeGrayscaleSpotSelectsColorAndPrecision(string file, int spotBits)
    {
        Configuration configuration = new(new JxlConfigurationModule());
        DecoderOptions options = new(configuration);
        byte[] data = TestFile.Create(file).Bytes;
        using MemoryStream input = new(data, false);
        using Image automatic = Image.Load(options, input);
        using MemoryStream referenceInput = new(data, false);
        using Image<RgbaVector> reference = LibJxlReferenceDecoder.Instance.Decode<RgbaVector>(options, referenceInput);

        JxlMetadata metadata = automatic.Metadata.GetFormatMetadata(JxlFormat.Instance);
        Assert.Equal(JxlColorType.Luminance, metadata.ColorType);
        Assert.Equal((byte)8, metadata.BitDepth.BitsPerSample);
        Assert.Equal(new Size(4, 1), automatic.Size);

        // Native decoding supplies complete rendered pixels. Convert only the integer references to
        // the required destination format, so comparisons account for that format's quantization.
        switch (spotBits)
        {
            case 8:
                using (Image<Rgb24> expected = reference.CloneAs<Rgb24>())
                {
                    ImageComparer.Exact.VerifySimilarity(expected, Assert.IsType<Image<Rgb24>>(automatic));
                }

                break;
            case 16:
                using (Image<Rgb48> expected = reference.CloneAs<Rgb48>())
                {
                    ImageComparer.Exact.VerifySimilarity(expected, Assert.IsType<Image<Rgb48>>(automatic));
                }

                break;
            case 32:
                Image<RgbaVector> actual = Assert.IsType<Image<RgbaVector>>(automatic);
                ApproximateFloatComparer comparer = ApproximateFloatComparer.FromPrecision(4);

                // The stored spot reaches 1.25 coverage. Native output includes negative green and
                // blue, so this also proves that automatic storage retains extended float values.
                for (int x = 0; x < actual.Width; x++)
                {
                    Assert.Equal(reference[x, 0].ToVector4(), actual[x, 0].ToVector4(), comparer);
                }

                break;
        }
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
    /// Verifies physical EOF recovery keeps completed streamed rows and initializes every unread pixel.
    /// </summary>
    /// <param name="handling">The selected image-data integrity policy.</param>
    [Theory]
    [InlineData(SegmentIntegrityHandling.Strict)]
    [InlineData(SegmentIntegrityHandling.IgnoreAncillary)]
    [InlineData(SegmentIntegrityHandling.IgnoreImageData)]
    public void DecodeRetainsCompletedStreamedRowsAfterPhysicalEof(SegmentIntegrityHandling handling)
    {
        TestMemoryAllocator allocator = new();
        allocator.EnableNonThreadSafeLogging();
        Configuration configuration = new(new JxlConfigurationModule()) { MemoryAllocator = allocator };
        DecoderOptions options = new(configuration) { SegmentIntegrityHandling = handling };
        using MemoryStream stream = new(TestFile.Create(TestImages.Jxl.ProgressiveAcTruncatedRows).Bytes, false);

        if (handling != SegmentIntegrityHandling.IgnoreImageData)
        {
            Assert.Throws<InvalidImageContentException>(() =>
            {
                using Image<Rgba32> image = Image.Load<Rgba32>(options, stream);
            });
        }
        else
        {
            using Image<Rgba32> image = Image.Load<Rgba32>(options, stream);
            using MemoryStream complete = new(TestFile.Create(TestImages.Jxl.ProgressiveAc).Bytes, false);
            using Image<Rgba32> expected = Image.Load<Rgba32>(new DecoderOptions(configuration), complete);

            Assert.Equal(new Size(2268, 1512), image.Size);
            Assert.Equal(1, image.Frames.Count);

            // Native inspection places EOF inside the second group row's last AC pass. The first 256-row strip
            // has every pass. Gaborish retains one lookahead row and EPF1 retains two, so 253 rows reach pixel output.
            const int completedRows = 253;
            for (int y = completedRows; y < image.Height; y++)
            {
                Assert.Equal(-1, image.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y).IndexOfAnyExcept(default(Rgba32)));
            }

            // Compare only the completed prefix. The original full decode establishes recovery preservation;
            // independent native comparisons cover the fixture's color and reconstruction arithmetic elsewhere.
            Rectangle completedRegion = new(0, 0, image.Width, completedRows);
            expected.Mutate(context => context.Crop(completedRegion));
            image.Mutate(context => context.Crop(completedRegion));
            ImageComparer.Exact.VerifySimilarity(expected, image);
        }

        Assert.Equal(0, allocator.LiveBufferBytes);
        Assert.True(stream.CanRead);
    }

    /// <summary>
    /// Verifies a later incomplete frame header retains earlier complete canvases through both public load paths.
    /// </summary>
    /// <param name="handling">The selected image-data integrity policy.</param>
    /// <param name="automatic">Whether the decoder selects the destination pixel type.</param>
    [Theory]
    [InlineData(SegmentIntegrityHandling.Strict, false)]
    [InlineData(SegmentIntegrityHandling.IgnoreAncillary, false)]
    [InlineData(SegmentIntegrityHandling.IgnoreImageData, false)]
    [InlineData(SegmentIntegrityHandling.Strict, true)]
    [InlineData(SegmentIntegrityHandling.IgnoreAncillary, true)]
    [InlineData(SegmentIntegrityHandling.IgnoreImageData, true)]
    public void DecodeRetainsCompleteFramesAfterTruncatedHeader(SegmentIntegrityHandling handling, bool automatic)
    {
        Configuration configuration = new(new JxlConfigurationModule());
        DecoderOptions options = new(configuration) { SegmentIntegrityHandling = handling };
        using MemoryStream stream = new(TestFile.Create(TestImages.Jxl.CroppedTrafficLightTruncatedHeader).Bytes, false);

        if (handling != SegmentIntegrityHandling.IgnoreImageData)
        {
            Assert.Throws<InvalidImageContentException>(() =>
            {
                using Image image = automatic ? Image.Load(options, stream) : Image.Load<Rgba32>(options, stream);
            });
        }
        else
        {
            using Image image = automatic ? Image.Load(options, stream) : Image.Load<Rgba32>(options, stream);
            Image<Rgba32> actual = Assert.IsType<Image<Rgba32>>(image);
            using MemoryStream complete = new(TestFile.Create(TestImages.Jxl.CroppedTrafficLight).Bytes, false);
            using Image<Rgba32> expected = Image.Load<Rgba32>(new DecoderOptions(configuration) { MaxFrames = 3 }, complete);

            // Native inspection puts the fourth header at byte 704. The stored prefix contains only its first byte,
            // so recovery must retain all three completed canvases without appending an uninitialized fourth frame.
            Assert.Equal(3, actual.Frames.Count);
            ImageComparer.Exact.VerifySimilarity(expected, actual);

            for (int i = 0; i < actual.Frames.Count; i++)
            {
                Assert.Equal(
                    expected.Frames[i].Metadata.GetFormatMetadata(JxlFormat.Instance).FrameDelay,
                    actual.Frames[i].Metadata.GetFormatMetadata(JxlFormat.Instance).FrameDelay);
            }
        }

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
    /// Verifies cleanup when original-color composition requires a conversion that the reference decoder does not support.
    /// </summary>
    /// <param name="file">The stored cropped input with non-D65 grayscale or ICC-only P3 color.</param>
    /// <param name="handling">The selected segment error policy.</param>
    [Theory]
    [InlineData(TestImages.Jxl.CroppedD50GrayBlending, SegmentIntegrityHandling.Strict)]
    [InlineData(TestImages.Jxl.CroppedD50GrayBlending, SegmentIntegrityHandling.IgnoreAncillary)]
    [InlineData(TestImages.Jxl.CroppedD50GrayBlending, SegmentIntegrityHandling.IgnoreImageData)]
    [InlineData(TestImages.Jxl.CroppedP3IccBlending, SegmentIntegrityHandling.Strict)]
    [InlineData(TestImages.Jxl.CroppedP3IccBlending, SegmentIntegrityHandling.IgnoreAncillary)]
    [InlineData(TestImages.Jxl.CroppedP3IccBlending, SegmentIntegrityHandling.IgnoreImageData)]
    public void DecodeReportsUnsupportedOriginalColorComposition(string file, SegmentIntegrityHandling handling)
    {
        TestMemoryAllocator allocator = new();
        allocator.EnableNonThreadSafeLogging();
        Configuration configuration = new(new JxlConfigurationModule()) { MemoryAllocator = allocator };
        DecoderOptions options = new(configuration) { SegmentIntegrityHandling = handling };
        using MemoryStream stream = new(TestFile.Create(file).Bytes, false);

        // The native public decoder reports an error for both stored inputs when composition needs
        // their original encoding. This is an unsupported reconstruction, not invalid color metadata.
        // Segment recovery cannot replace a required color conversion or omit the requested blend.
        Assert.Throws<InvalidImageContentException>(() =>
        {
            using Image<RgbaVector> image = Image.Load<RgbaVector>(options, stream);
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
    /// Verifies cancellation after the displayed destination is allocated releases all owners and leaves the caller's stream open.
    /// </summary>
    [Fact]
    public async Task DecodeHonorsCancellationAfterDestinationAllocation()
    {
        TestMemoryAllocator allocator = new();
        allocator.EnableNonThreadSafeLogging();

        // Small buffered reads retain stream pauses while the stored progressive frame is being decoded.
        Configuration configuration = new(new JxlConfigurationModule()) { MemoryAllocator = allocator, StreamProcessingBufferSize = 128 };
        DecoderOptions options = new(configuration);
        using PausedMemoryStream stream = new(TestFile.Create(TestImages.Jxl.ProgressiveAc).Bytes);
        using CancellationTokenSource cancellation = new();

        stream.OnWaiting(() =>
        {
            // The Rgba32 allocation identifies the displayed destination after frame parsing.
            // Cancel at the next underlying read so the failure must release an existing pixel owner.
            foreach (TestMemoryAllocator.AllocationRequest request in allocator.AllocationLog)
            {
                if (request.ElementType == typeof(Rgba32))
                {
                    cancellation.Cancel();

                    // Release also disables further pauses, so cancellation occurs once.
                    stream.Release();
                    return;
                }
            }

            stream.Next();
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            using Image<Rgba32> image = await JxlDecoder.Instance.DecodeAsync<Rgba32>(options, stream, cancellation.Token);
        });

        Assert.True(cancellation.IsCancellationRequested);
        Assert.Equal(0, allocator.LiveBufferBytes);
        Assert.True(stream.CanRead);
    }

    /// <summary>
    /// Verifies displayed animation pixels, timing and MaxFrames against native decoding and header inspection.
    /// </summary>
    /// <param name="provider">The stored animation input and normal diagnostic output path.</param>
    /// <param name="maxFrames">The displayed frame limit.</param>
    /// <param name="firstSeconds">The first frame's duration reported by the native inspector, in seconds.</param>
    /// <param name="shortSeconds">The shorter later frame's duration, in seconds.</param>
    [Theory]
    [WithFile(TestImages.Jxl.CroppedTrafficLight, PixelTypes.Rgba32, 1, 3L, 1L)]
    [WithFile(TestImages.Jxl.CroppedTrafficLight, PixelTypes.Rgba32, 4, 3L, 1L)]
    [WithFile(TestImages.Jxl.CroppedTrafficLightLongDuration, PixelTypes.Rgba32, 4, 4398046510080L, 102400L)]
    public void DecodeAndIdentifyReportDisplayedAnimationFrames(TestImageProvider<Rgba32> provider, int maxFrames, long firstSeconds, long shortSeconds)
    {
        Configuration configuration = new(new JxlConfigurationModule());
        provider.Configuration = configuration;
        DecoderOptions options = new(configuration) { MaxFrames = (uint)maxFrames };
        TestFile input = TestFile.Create(provider.SourceFileOrDescription);
        using MemoryStream stream = new(input.Bytes, false);

        ImageInfo info = Image.Identify(options, stream);
        stream.Position = 0;
        using Image<Rgba32> image = Image.Load<Rgba32>(options, stream);
        image.DebugSaveMultiFrame(provider, maxFrames);

        // Magick uses libjxl's coalesced output. Compare every displayed canvas in memory through
        // the existing image comparer; matching metadata alone cannot prove reference-frame composition.
        using MemoryStream referenceStream = new(input.Bytes, false);
        using Image<Rgba32> reference = MagickReferenceDecoder.Jxl.Decode<Rgba32>(options, referenceStream);
        ImageComparer.Tolerant().VerifySimilarity(reference, image);

        // jxlinspect reports the exact durations for both files. The long variant uses the maximum native
        // tick count and a 1/1024 tick rate, which exceeds both uint seconds and TimeSpan's duration range.
        // Cropped intermediate content must not change the canvas size or add displayed frames.
        ReadOnlySpan<long> seconds = [firstSeconds, shortSeconds, 3 * shortSeconds, shortSeconds];

        Assert.Equal(new Size(50, 80), info.Size);
        Assert.Equal(maxFrames, info.FrameCount);
        Assert.Equal(maxFrames, image.Frames.Count);
        Assert.Equal(0U, info.Metadata.GetFormatMetadata(JxlFormat.Instance).RepeatCount);
        Assert.Equal(0U, image.Metadata.GetFormatMetadata(JxlFormat.Instance).RepeatCount);

        for (int i = 0; i < maxFrames; i++)
        {
            JxlFrameMetadata frame = info.FrameMetadataCollection[i].GetFormatMetadata(JxlFormat.Instance);

            Assert.Equal(new LongRational(seconds[i], 1), frame.FrameDelay);
            Assert.Equal(frame.FrameDelay, image.Frames[i].Metadata.GetFormatMetadata(JxlFormat.Instance).FrameDelay);
        }
    }

    /// <summary>
    /// Verifies public decoding and identification retain stored dimensions and orientation until the caller applies AutoOrient.
    /// </summary>
    [Fact]
    public void DecodeAndIdentifyRetainStoredOrientation()
    {
        Configuration configuration = new(new JxlConfigurationModule());
        DecoderOptions options = new(configuration);
        using MemoryStream stream = new(TestFile.Create(TestImages.Jxl.Orientation6).Bytes, false);

        ImageInfo info = Image.Identify(options, stream);
        stream.Position = 0;
        using Image<Rgb24> image = Image.Load<Rgb24>(options, stream);

        Assert.Equal(new Size(100, 256), info.Size);
        Assert.Equal(info.Size, image.Size);
        Assert.Equal(ExifOrientationMode.RightTop, info.Metadata.ExifProfile.GetValue(ExifTag.Orientation).Value);
        Assert.Equal(ExifOrientationMode.RightTop, image.Metadata.ExifProfile.GetValue(ExifTag.Orientation).Value);
    }

    /// <summary>
    /// Verifies that the optional preview is skipped during identification and public pixel decoding.
    /// </summary>
    [Fact]
    public void DecodeAndIdentifySkipPreviewFrame()
    {
        Configuration configuration = new(new JxlConfigurationModule());
        DecoderOptions options = new(configuration);
        using MemoryStream stream = new(TestFile.Create(TestImages.Jxl.WithPreview).Bytes, false);

        ImageInfo info = Image.Identify(options, stream);
        stream.Position = 0;
        using Image<RgbaVector> image = JxlDecoder.Instance.Decode<RgbaVector>(options, stream);

        Assert.Equal(new Size(64, 64), info.Size);
        Assert.Equal(1, info.FrameCount);
        Assert.Equal(info.Size, image.Size);
        Assert.Equal(1, image.Frames.Count);
    }

    /// <summary>
    /// Verifies typed and automatic decoding apply TargetSize and the caller's sampler through the shared resize operation.
    /// </summary>
    /// <param name="automatic">Whether the decoder selects the destination pixel type.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DecodeAppliesTargetSizeToMainImage(bool automatic)
    {
        Configuration configuration = new(new JxlConfigurationModule());
        DecoderOptions options = new(configuration) { TargetSize = new Size(21, 9), Sampler = KnownResamplers.NearestNeighbor };
        TestFile file = TestFile.Create(TestImages.Jxl.WithPreview);
        using MemoryStream complete = new(file.Bytes, false);
        using Image<Rgb24> expected = Image.Load<Rgb24>(new DecoderOptions(configuration), complete);

        // A non-square bound proves aspect-ratio preservation. The explicit sampler must reach the
        // shared resize owner after the main image is reconstructed, with no separate preview-size scaling.
        expected.Mutate(context => context.Resize(new ResizeOptions { Size = options.TargetSize.Value, Mode = ResizeMode.Max, Sampler = options.Sampler }));
        using MemoryStream stream = new(file.Bytes, false);
        using Image image = automatic ? Image.Load(options, stream) : JxlDecoder.Instance.Decode<Rgb24>(options, stream);
        Image<Rgb24> actual = Assert.IsType<Image<Rgb24>>(image);

        Assert.Equal(new Size(9, 9), actual.Size);
        Assert.Equal(1, actual.Frames.Count);
        ImageComparer.Exact.VerifySimilarity(expected, actual);
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
    /// Verifies stored patch modes, selected weights and clamping against complete native output at each SIMD width and scalar fallback.
    /// </summary>
    /// <param name="provider">The stored weighted-patch input.</param>
    [Theory]
    [WithFile(TestImages.Jxl.WeightedPatchModes, PixelTypes.RgbaVector)]
    public void DecodeWeightedPatchModesMatchesNativePixels(TestImageProvider<RgbaVector> provider)
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateDecodedWeightedPatchModes,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic,
            provider);

    /// <summary>
    /// Compares the complete stored patch result with native float output without quantizing signed RGB or transparent pixels.
    /// </summary>
    /// <param name="providerDump">The serialized file provider for one hardware configuration.</param>
    private static void ValidateDecodedWeightedPatchModes(string providerDump)
    {
        TestImageProvider<RgbaVector> provider = FeatureTestRunner.DeserializeForXunit<TestImageProvider<RgbaVector>>(providerDump);
        provider.Configuration = new(new JxlConfigurationModule());
        DecoderOptions options = new(provider.Configuration);
        using Image<RgbaVector> image = provider.GetImage(JxlDecoder.Instance, options);

        // Compare float pixels directly. The diagnostic dump does not supply expected values.
        TestFile file = TestFile.Create(provider.SourceFileOrDescription);
        using MemoryStream referenceStream = new(file.Bytes, false);
        using Image<RgbaVector> reference = LibJxlReferenceDecoder.Instance.Decode<RgbaVector>(options, referenceStream);
        SaveDecodedImage(image, provider);
        Assert.Equal(reference.Size, image.Size);
        ApproximateFloatComparer comparer = ApproximateFloatComparer.FromPrecision(4);

        // Check every component, including RGB at partial and zero alpha, with the four-decimal bound.
        for (int y = 0; y < image.Height; y++)
        {
            Span<RgbaVector> expected = reference.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y);
            Span<RgbaVector> actual = image.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y);
            for (int x = 0; x < actual.Length; x++)
            {
                Assert.Equal(expected[x].ToVector4(), actual[x].ToVector4(), comparer);
            }
        }
    }

    /// <summary>
    /// Verifies that public declared-XYB decoding retains native alpha at its stored eight-bit precision.
    /// </summary>
    /// <param name="provider">The stored cropped input with a declared XYB color profile.</param>
    [Theory]
    [WithFile(TestImages.Jxl.CroppedDeclaredXybBlending, PixelTypes.RgbaVector)]
    public void DecodeDeclaredXybPreservesNativeAlpha(TestImageProvider<RgbaVector> provider)
    {
        provider.Configuration = new(new JxlConfigurationModule());
        DecoderOptions options = new(provider.Configuration) { ColorProfileHandling = ColorProfileHandling.Convert, SkipMetadata = true };
        using Image<RgbaVector> image = provider.GetImage(JxlDecoder.Instance, options);
        SaveDecodedImage(image, provider);

        // Native alpha has eight-bit integer precision. The generated XYB ICC approximates the device transform,
        // so this test compares only alpha. ConvertDeclaredXybDeviceMatchesNativeLinearRgb checks the device color arithmetic.
        TestFile file = TestFile.Create(provider.SourceFileOrDescription);
        using MemoryStream referenceStream = new(file.Bytes, false);
        using Image<A8> expected = LibJxlReferenceDecoder.Instance.Decode<A8>(options, referenceStream);
        using Image<A8> actual = image.CloneAs<A8>();

        Assert.Equal(new Size(750, 1000), expected.Size);
        Assert.Equal(1, expected.Frames.Count);
        Assert.Equal(expected.Size, actual.Size);
        Assert.Equal(expected.Frames.Count, actual.Frames.Count);
        ImageComparer.Exact.VerifySimilarity(expected, actual);
    }

    /// <summary>
    /// Verifies native color conversion, preview skipping, profile retention and straight alpha through the public decoder.
    /// </summary>
    /// <param name="provider">The stored profiled or compact-color input.</param>
    [Theory]
    [WithFile(TestImages.Jxl.LinearRgbaWithIcc, PixelTypes.RgbaVector)]
    [WithFile(TestImages.Jxl.CmykFirstFrame, PixelTypes.RgbaVector)]
    [WithFile(TestImages.Jxl.CmykFourthFrame, PixelTypes.RgbaVector)]
    [WithFile(TestImages.Jxl.HdrPq, PixelTypes.RgbaVector)]
    [WithFile(TestImages.Jxl.HdrHlg, PixelTypes.RgbaVector)]
    [WithFile(TestImages.Jxl.WithPreview, PixelTypes.RgbaVector)]
    [WithFile(TestImages.Jxl.Splines, PixelTypes.RgbaVector)]
    [WithFile(TestImages.Jxl.Noise8x8, PixelTypes.RgbaVector)]
    [WithFile(TestImages.Jxl.Noise, PixelTypes.RgbaVector)]
    [WithFile(TestImages.Jxl.BlendModes, PixelTypes.RgbaVector)]
    [WithFile(TestImages.Jxl.CroppedXybBlending, PixelTypes.RgbaVector)]
    [WithFile(TestImages.Jxl.CroppedP3Blending, PixelTypes.RgbaVector)]
    [WithFile(TestImages.Jxl.CroppedSrgbIccBlending, PixelTypes.RgbaVector)]
    [WithFile(TestImages.Jxl.CroppedD65GrayBlending, PixelTypes.RgbaVector)]
    [WithFile(TestImages.Jxl.P3XybSpotComposition, PixelTypes.RgbaVector)]
    [WithFile(TestImages.Jxl.P3IccStandalonePostSave, PixelTypes.RgbaVector)]
    public void DecodeColorMatchesNativeSrgb(TestImageProvider<RgbaVector> provider)
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateDecodedColors,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic,
            provider);

    /// <summary>
    /// Verifies native spot rendering with RGB acceptance for the embedded profile's amplified Fast Pow error.
    /// </summary>
    /// <param name="provider">The stored spot image with a ProPhoto matrix/TRC profile.</param>
    [Theory]
    [WithFile(TestImages.Jxl.SpotColor, PixelTypes.RgbaVector)]
    public void DecodeSpotColorMatchesNativeSrgb(TestImageProvider<RgbaVector> provider)
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateDecodedColors,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic,
            provider);

    /// <summary>
    /// Compares complete native float images with each profile-retention and optional-metadata setting.
    /// </summary>
    /// <param name="providerDump">The serialized file provider for one hardware configuration.</param>
    /// <remarks>
    /// Libjxl decodes the same stored input to unassociated sRGB float pixels for each hardware configuration.
    /// The comparison checks the complete image, including extended RGB and hidden RGB at zero alpha.
    /// Diagnostic dumps are for visual inspection. They do not supply expected pixel values.
    /// </remarks>
    private static void ValidateDecodedColors(string providerDump)
    {
        TestImageProvider<RgbaVector> provider = FeatureTestRunner.DeserializeForXunit<TestImageProvider<RgbaVector>>(providerDump);
        string file = provider.SourceFileOrDescription;

        // The spot profile's gamma curve feeds a matrix that subtracts similar values when calculating green.
        // That subtraction and the output transfer curve amplify Fast Pow's error. Its separate RGB acceptance
        // bounds the independently measured maximum 0.0011096562 at 0.00112. Other cases retain their existing bound.
        float rgbTolerance = file == TestImages.Jxl.SpotColor ? .00112F : .00052F;
        Size size = file switch
        {
            TestImages.Jxl.LinearRgbaWithIcc => new Size(4, 1),
            TestImages.Jxl.WithPreview => new Size(64, 64),
            TestImages.Jxl.HdrPq or TestImages.Jxl.HdrHlg => new Size(1024, 1024),
            TestImages.Jxl.Splines => new Size(2048, 2048),
            TestImages.Jxl.Noise8x8 or TestImages.Jxl.P3IccStandalonePostSave => new Size(8, 8),
            TestImages.Jxl.Noise => new Size(500, 606),
            TestImages.Jxl.SpotColor => new Size(600, 400),
            TestImages.Jxl.BlendModes => new Size(1024, 1024),
            TestImages.Jxl.P3XybSpotComposition => new Size(35, 3),
            TestImages.Jxl.CroppedXybBlending or TestImages.Jxl.CroppedP3Blending or TestImages.Jxl.CroppedSrgbIccBlending
                or TestImages.Jxl.CroppedD65GrayBlending => new Size(750, 1000),
            TestImages.Jxl.CmykFirstFrame => new Size(512, 512),
            TestImages.Jxl.CmykFourthFrame => new Size(110, 68),
            _ => new Size(169, 118)
        };

        string profileHash = file switch
        {
            TestImages.Jxl.SpotColor => "CE0CAEE9506116EA94D7367D646F7FD6D0B7E82FEB8D1F3DE4EDB3BA57BAE07E",
            TestImages.Jxl.LinearRgbaWithIcc => "5FB4D2577884135C20D1E81FE1A0912907037B31486E5BCDA0029270C924E264",
            TestImages.Jxl.CroppedSrgbIccBlending => "87948CDB39DC5FE2044FA8A340B09B27AD06A3A0795F6EED5B8EF4CBCF6EA051",
            TestImages.Jxl.P3IccStandalonePostSave => "3F8CA934C4B3AE48B5B2522117C715FF390C00F887FD4CDFAD60720CCBEF3B5B",
            TestImages.Jxl.WithPreview => null,
            TestImages.Jxl.HdrPq or TestImages.Jxl.HdrHlg or TestImages.Jxl.Splines
                or TestImages.Jxl.Noise8x8 or TestImages.Jxl.Noise or TestImages.Jxl.BlendModes or TestImages.Jxl.CroppedXybBlending
                or TestImages.Jxl.CroppedP3Blending
                or TestImages.Jxl.CroppedD65GrayBlending or TestImages.Jxl.P3XybSpotComposition => null,
            TestImages.Jxl.CmykFirstFrame or TestImages.Jxl.CmykFourthFrame => "4855B8FABB96BDC6495D45D089BB8C8EFB1AE18389E0DC9E75A5F701A9C0B662",
            _ => "92AAEE40521D7671DBC901DFC88CC36CB83D41766B2C466FF97268EE5AB0C577"
        };

        Configuration configuration = new(new JxlConfigurationModule());
        provider.Configuration = configuration;

        // Read the reference from the original codestream. Keep float samples in memory so the
        // comparison does not depend on a second image format's transfer curve or storage precision.
        TestFile testFile = TestFile.Create(file);
        using MemoryStream referenceStream = new(testFile.Bytes, false);
        using Image<RgbaVector> reference = LibJxlReferenceDecoder.Instance.Decode<RgbaVector>(new DecoderOptions(configuration), referenceStream);

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
                using Image<RgbaVector> image = provider.GetImage(JxlDecoder.Instance, options);
                Assert.Equal(size, image.Size);

                // All policies convert pixels. Compact color descriptions have no embedded ICC profile.
                // Compact also removes the canonical sRGB ICC profile; Preserve keeps its exact original bytes.
                // SkipMetadata must retain color data needed for conversion and the selected retention policy.
                if (handling == ColorProfileHandling.Convert || profileHash is null
                    || (handling == ColorProfileHandling.Compact && file == TestImages.Jxl.CroppedSrgbIccBlending))
                {
                    Assert.Null(image.Metadata.IccProfile);
                }
                else
                {
                    Assert.NotNull(image.Metadata.IccProfile);
                    Assert.Equal(profileHash, Convert.ToHexString(SHA256.HashData(image.Metadata.IccProfile.ToByteArray())));
                }

                // The metadata assertions above verified profile retention. These pixels are already sRGB;
                // remove the retained source profile so output encoding does not convert them back to device values.
                image.Metadata.IccProfile = null;
                image.Frames.RootFrame.Metadata.IccProfile = null;
                SaveDecodedImage(image, provider);

                // Bound each RGB component by rgbTolerance * max(1, abs(expected)). Alpha uses an absolute
                // bound because its normalized range remains [0,1].
                bool matches = true;
                string firstFailure = null;
                float maximumRejectedRgbError = 0F;
                float maximumAlphaError = 0F;

                for (int y = 0; y < image.Height; y++)
                {
                    Span<RgbaVector> expected = reference.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y);
                    Span<RgbaVector> actual = image.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y);
                    for (int x = 0; x < actual.Length; x++)
                    {
                        Vector3 expectedRgb = new(expected[x].R, expected[x].G, expected[x].B);
                        Vector3 actualRgb = new(actual[x].R, actual[x].G, actual[x].B);
                        Vector3 scale = Vector3.Max(Vector3.One, Vector3.Abs(expectedRgb));
                        Vector3 difference = Vector3.Abs(actualRgb - expectedRgb);
                        Vector3 tolerance = rgbTolerance * scale;
                        float alphaError = MathF.Abs(actual[x].A - expected[x].A);
                        maximumAlphaError = MathF.Max(maximumAlphaError, alphaError);

                        // Positive bound checks also reject NaN. Normalize only rejected pixels, then finish
                        // this pass to report their largest RGB error and the complete image's largest alpha error.
                        if (!(difference.X <= tolerance.X && difference.Y <= tolerance.Y && difference.Z <= tolerance.Z
                            && alphaError <= .0000001F))
                        {
                            Vector3 normalized = difference / scale;
                            maximumRejectedRgbError = MathF.Max(
                                maximumRejectedRgbError, MathF.Max(normalized.X, MathF.Max(normalized.Y, normalized.Z)));

                            if (matches)
                            {
                                firstFailure = $"{file}: pixel ({x}, {y}); reference {expected[x].ToVector4():R}; ImageSharp {actual[x].ToVector4():R}.";
                            }

                            matches = false;
                        }
                    }
                }

                if (!matches)
                {
                    Assert.Fail(
                        $"{firstFailure} Maximum rejected-pixel normalized RGB error {maximumRejectedRgbError:R}; maximum alpha error {maximumAlphaError:R}.");
                }
            }
        }
    }

    /// <summary>
    /// Saves decoded pixels in a format that retains the stored case's independently verified component range.
    /// </summary>
    /// <param name="image">The decoded working RGB pixels, without a retained source ICC profile.</param>
    /// <param name="provider">The stored input and normal diagnostic output path.</param>
    private static void SaveDecodedImage(Image<RgbaVector> image, TestImageProvider<RgbaVector> provider)
    {
        switch (provider.SourceFileOrDescription)
        {
            case TestImages.Jxl.WeightedPatchModes:
            case TestImages.Jxl.CroppedDeclaredXybBlending:
            case TestImages.Jxl.CroppedXybBlending:
            case TestImages.Jxl.CroppedP3Blending:
            case TestImages.Jxl.CroppedSrgbIccBlending:
            case TestImages.Jxl.P3XybSpotComposition:
            case TestImages.Jxl.CmykFourthFrame:
                // These cases contain signed or extended RGB at zero alpha. Float TIFF retains those
                // unassociated samples. The EXR encoder associates straight RGB, which clears these colors at zero alpha.
                image.DebugSave(provider, extension: "tiff", encoder: new TiffEncoder
                {
                    SampleFormat = TiffSampleFormat.Float,
                    BitsPerPixel = TiffBitsPerPixel.Bit128,
                    Compression = TiffCompression.Deflate,
                    TransparentColorMode = TransparentColorMode.Preserve
                });

                break;

            case TestImages.Jxl.HdrPq:
            case TestImages.Jxl.HdrHlg:
            case TestImages.Jxl.Splines:
            case TestImages.Jxl.Noise8x8:
            case TestImages.Jxl.Noise:
            case TestImages.Jxl.SpotColor:
            case TestImages.Jxl.BlendModes:
            case TestImages.Jxl.CmykFirstFrame:
                // Native float output proves that these cases exceed [0,1]. ZIP-compressed float EXR
                // retains their range. Its encoder converts working RGB to the file's linear components.
                image.DebugSave(provider, extension: "exr", encoder: new ExrEncoder { PixelType = ExrPixelType.Float, Compression = ExrCompression.Zip });
                break;

            default:
                // The remaining stored cases fit [0,1], so the normal PNG dump retains their appearance.
                image.DebugSave(provider);
                break;
        }
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
