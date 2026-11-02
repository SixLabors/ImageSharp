// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jxl;
using SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;
using SixLabors.ImageSharp.Formats.Jxl.Components.Modular;
using SixLabors.ImageSharp.Formats.Jxl.Components.VarDct;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Jxl;

public partial class JxlHeaderTests
{
    /// <summary>
    /// Verifies all matrix modes, retained rectangular layouts and decoder reciprocals at every SIMD width and scalar fallback.
    /// </summary>
    [Fact]
    public void ReadQuantizationMatricesMatchesReferences()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateMatrixParsers,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Runs stored matrix descriptions through valid frame owners and AC metadata selectors.
    /// </summary>
    private static void ValidateMatrixParsers()
    {
        ValidateReadQuantizationMatrices(0, TestImages.Jxl.AcMetadataRawMatrix, TestImages.Jxl.VarDctCustomMatrices, 2143);
        ValidateReadQuantizationMatrices(1, TestImages.Jxl.AcMetadataIdentityMatrix, TestImages.Jxl.VarDctCustomMatrices, 2143);
        ValidateReadQuantizationMatrices(2, null, TestImages.Jxl.VarDctCustomMatrices, 2143);
        ValidateReadQuantizationMatrices(3, TestImages.Jxl.AcMetadataDct4Matrix, TestImages.Jxl.VarDctCustomMatrices, 2143);
        ValidateReadQuantizationMatrices(12, TestImages.Jxl.AcMetadataSaturation, TestImages.Jxl.VarDctCustomMatrices, 2143);
        ValidateReadQuantizationMatrices(14, TestImages.Jxl.AcMetadataAfvMatrix, TestImages.Jxl.VarDctCustomMatrices, 2143);
        ValidateReadQuantizationMatrices(0, TestImages.Jxl.AcMetadataRawMatrix, TestImages.Jxl.VarDctDefaultMatrices, 1);
        ValidateReadQuantizationMatrices(0, TestImages.Jxl.AcMetadataRawMatrix, TestImages.Jxl.VarDctRectangularMatrices, 753);
        ValidateReadRectangularQuantizationMatrices(6, TestImages.Jxl.AcMetadataRectangularDct, 128, 78);
        ValidateReadRectangularQuantizationMatrices(8, TestImages.Jxl.AcMetadataRectangularRaw, 256, 88);
    }

    /// <summary>
    /// Verifies every matrix mode through stored AC metadata selectors and original native-encoded matrix descriptions.
    /// </summary>
    /// <param name="strategy">The raw strategy selected by the stored AC metadata.</param>
    /// <param name="selection">A stored partial AC metadata stream, or null for the original frame's DCT2 metadata.</param>
    /// <param name="matrices">The stored partial matrix descriptions.</param>
    /// <param name="bits">The payload bit count independently reported by libjxl and jxl-rs.</param>
    private static void ValidateReadQuantizationMatrices(int strategy, string selection, string matrices, int bits)
    {
        using MemoryStream frameInput = new(TestFile.Create(TestImages.Jxl.LossyRgb3x3).Bytes, false);
        using BufferedReadStream frameBuffered = new(Configuration.Default, frameInput);
        JxlBitReader frameReader = new(new JxlCodestreamReader(frameBuffered));
        JxlHeader header = JxlHeader.Read(ref frameReader, Configuration.Default.MemoryAllocator, SegmentIntegrityHandling.Strict);
        JxlFrameHeader frame = JxlFrameHeader.Read(ref frameReader, header, false, SegmentIntegrityHandling.Strict);
        using JxlFrameSections sections = JxlFrameSections.Read(ref frameReader, Configuration.Default.MemoryAllocator, frame.SectionCount, default);
        frameReader.BeginSection(sections.Sizes.Span[0]);
        Vector3 factors = JxlDecoderCore.ReadDcQuantization(ref frameReader);
        using JxlVarDctDecoder varDct = JxlVarDctDecoder.ReadGlobal(ref frameReader, header, frame, Configuration.Default.MemoryAllocator, factors, []);
        using JxlModularDecoder modular = JxlModularDecoder.Read(ref frameReader, header, frame, Configuration.Default.MemoryAllocator, default);
        varDct.ReadDcGroup(ref frameReader, modular, 0, default);

        // Select an actual transform through its encoded metadata. No production setter bypasses strategy validation.
        // The owner and shared modular tree come from an unchanged reference image; selectors use its one-block geometry.
        if (selection is null)
        {
            varDct.ReadAcMetadata(ref frameReader, modular, 0, default);
        }
        else
        {
            using MemoryStream selectionInput = new(TestFile.Create(selection).Bytes, false);
            using BufferedReadStream selectionBuffered = new(Configuration.Default, selectionInput);
            JxlBitReader selectionReader = new(new JxlCodestreamReader(selectionBuffered));
            Assert.Equal(0x0AFFU, selectionReader.ReadBits(16));
            varDct.ReadAcMetadata(ref selectionReader, modular, 0, default);
        }

        using MemoryStream input = new(TestFile.Create(matrices).Bytes, false);
        using BufferedReadStream buffered = new(Configuration.Default, input);
        JxlBitReader reader = new(new JxlCodestreamReader(buffered));
        Assert.Equal(0x0AFFU, reader.ReadBits(16));

        // The rectangular case includes an unused RAW family with three 8-by-32 integer planes. It must consume
        // those planes without retaining them or replacing the selected default DCT8 matrix.
        varDct.ReadQuantizationMatrices(ref reader, modular, Configuration.Default.MemoryAllocator, default);
        Assert.Equal(16 + bits, reader.Position);
        Assert.Equal(1U << strategy, varDct.UsedStrategies);
        Assert.Equal(3 * 64, varDct.QuantizationMatrices.Length);

        int firstOffset = JxlVarDctDecoder.GetQuantizationMatrixIndex((byte)strategy, 0);
        for (int i = 0; i < varDct.QuantizationMatrixOffsets.Length; i++)
        {
            int expected = i >= firstOffset && i < firstOffset + 3 ? (i - firstOffset) * 64 : -1;
            Assert.Equal(expected, varDct.QuantizationMatrixOffsets[i]);
        }

        // Both references agree exactly for RAW, identity and DCT2. Their radial power approximations differ by
        // less than two parts per million on these stored tables, so compare each radial result with both references.
        // Points include AFV's five special coefficients, both interleaved DCT planes and sparse even/even frequencies.
        // RAW integer seven and denominator one-half give 0x405FFFFF after two reciprocals, rather than exact 3.5.
        int reference = matrices == TestImages.Jxl.VarDctCustomMatrices ? strategy : 100;
        bool exact = reference <= 2;
        ReadOnlySpan<int> points = [0, 1, 2, 4, 6, 8, 9, 15, 16, 17, 18, 32, 36, 54, 63];
        ReadOnlySpan<uint> native = NativeMatrixPoints(reference);
        ReadOnlySpan<uint> rust = RustMatrixPoints(reference);
        ReadOnlySpan<float> actual = varDct.QuantizationMatrices;
        for (int component = 0; component < 3; component++)
        {
            for (int i = 0; i < points.Length; i++)
            {
                float value = actual[(component * 64) + points[i]];
                int index = (component * points.Length) + i;
                if (exact)
                {
                    Assert.Equal(native[index], BitConverter.SingleToUInt32Bits(value));
                }
                else
                {
                    float nativeValue = BitConverter.UInt32BitsToSingle(native[index]);
                    float rustValue = BitConverter.UInt32BitsToSingle(rust[index]);
                    Assert.InRange(MathF.Abs((value - nativeValue) / nativeValue), 0F, 0.000002F);
                    Assert.InRange(MathF.Abs((value - rustValue) / rustValue), 0F, 0.000002F);
                }
            }
        }
    }

    /// <summary>
    /// Verifies retained rectangular radial and RAW matrices through valid parsed eight-by-thirty-two frame geometry.
    /// </summary>
    /// <param name="strategy">The raw strategy encoded by the stored AC metadata.</param>
    /// <param name="selection">The native-encoded metadata that covers the four-block frame.</param>
    /// <param name="area">The number of coefficients in each retained component plane.</param>
    /// <param name="selectionBits">The selector's consumed payload bits, verified by both references.</param>
    private static void ValidateReadRectangularQuantizationMatrices(int strategy, string selection, int area, int selectionBits)
    {
        using MemoryStream ownerInput = new(TestFile.Create(TestImages.Jxl.VarDctRectangularOwner).Bytes, false);
        using BufferedReadStream ownerBuffered = new(Configuration.Default, ownerInput);
        JxlBitReader ownerReader = new(new JxlCodestreamReader(ownerBuffered));
        JxlHeader header = JxlHeader.Read(ref ownerReader, Configuration.Default.MemoryAllocator, SegmentIntegrityHandling.Strict);
        JxlFrameHeader frame = JxlFrameHeader.Read(ref ownerReader, header, false, SegmentIntegrityHandling.Strict);
        Assert.Equal(new Size(8, 32), header.Size);
        Assert.Equal(110, ownerReader.Position);

        // This stored component prefix contains image/frame headers and DC-global/DC data, without a section table.
        // Both original decoders consume exactly185 bits and reconstruct three zero-valued one-by-four DC planes.
        Vector3 factors = JxlDecoderCore.ReadDcQuantization(ref ownerReader);
        using JxlVarDctDecoder varDct = JxlVarDctDecoder.ReadGlobal(ref ownerReader, header, frame, Configuration.Default.MemoryAllocator, factors, []);
        using JxlModularDecoder modular = JxlModularDecoder.Read(ref ownerReader, header, frame, Configuration.Default.MemoryAllocator, default);
        varDct.ReadDcGroup(ref ownerReader, modular, 0, default);
        Assert.Equal(185, ownerReader.Position);

        using MemoryStream selectionInput = new(TestFile.Create(selection).Bytes, false);
        using BufferedReadStream selectionBuffered = new(Configuration.Default, selectionInput);
        JxlBitReader selectionReader = new(new JxlCodestreamReader(selectionBuffered));
        Assert.Equal(0x0AFFU, selectionReader.ReadBits(16));
        varDct.ReadAcMetadata(ref selectionReader, modular, 0, default);
        Assert.Equal(16 + selectionBits, selectionReader.Position);
        Assert.Equal(1U << strategy, varDct.UsedStrategies);

        using MemoryStream input = new(TestFile.Create(TestImages.Jxl.VarDctRectangularMatrices).Bytes, false);
        using BufferedReadStream buffered = new(Configuration.Default, input);
        JxlBitReader reader = new(new JxlCodestreamReader(buffered));
        Assert.Equal(0x0AFFU, reader.ReadBits(16));
        varDct.ReadQuantizationMatrices(ref reader, modular, Configuration.Default.MemoryAllocator, default);
        Assert.Equal(16 + 753, reader.Position);
        Assert.Equal(3 * area, varDct.QuantizationMatrices.Length);
        int firstOffset = JxlVarDctDecoder.GetQuantizationMatrixIndex((byte)strategy, 0);
        for (int i = 0; i < varDct.QuantizationMatrixOffsets.Length; i++)
        {
            int expected = i >= firstOffset && i < firstOffset + 3 ? (i - firstOffset) * area : -1;
            Assert.Equal(expected, varDct.QuantizationMatrixOffsets[i]);
        }

        // Radial matrices use sixteen contiguous columns here. RAW keeps its encoded eight-column row order.
        // Points on both sides of those row boundaries and in every component detect an unintended transpose or plane offset.
        ReadOnlySpan<int> points = [0, 1, 6, 7, 8, 15, 16, 17, 30, 31, 32, 63, 64, 111, area - 1];
        ReadOnlySpan<uint> native = NativeMatrixPoints(strategy);
        ReadOnlySpan<uint> rust = RustMatrixPoints(strategy);
        ReadOnlySpan<float> actual = varDct.QuantizationMatrices;
        for (int component = 0; component < 3; component++)
        {
            for (int i = 0; i < points.Length; i++)
            {
                float value = actual[(component * area) + points[i]];
                int index = (component * points.Length) + i;
                if (strategy == 8)
                {
                    Assert.Equal(native[index], BitConverter.SingleToUInt32Bits(value));
                }
                else
                {
                    float nativeValue = BitConverter.UInt32BitsToSingle(native[index]);
                    float rustValue = BitConverter.UInt32BitsToSingle(rust[index]);
                    Assert.InRange(MathF.Abs((value - nativeValue) / nativeValue), 0F, 0.000002F);
                    Assert.InRange(MathF.Abs((value - rustValue) / rustValue), 0F, 0.000002F);
                }
            }
        }
    }

    /// <summary>
    /// Verifies radial interpolation accuracy, destination bounds and RAW rounding at every SIMD width and scalar fallback.
    /// </summary>
    [Fact]
    public void QuantizationArithmeticMatchesAcrossSimdWidths()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateQuantizationArithmetic,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks single-precision radial interpolation against the specified geometric formula and RAW weights against exact float values.
    /// </summary>
    private static void ValidateQuantizationArithmetic()
    {
        ReadOnlySpan<int> raw =
        [
            1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33
        ];

        ReadOnlySpan<uint> rawWeightBits =
        [
            0x40000000U, 0x3F800000U, 0x3F2AAAABU, 0x3F000000U, 0x3ECCCCCDU, 0x3EAAAAABU, 0x3E924925U, 0x3E800000U, 0x3E638E39U, 0x3E4CCCCDU, 0x3E3A2E8CU,
            0x3E2AAAABU, 0x3E1D89D9U, 0x3E124925U, 0x3E088889U, 0x3E000000U, 0x3DF0F0F1U, 0x3DE38E39U, 0x3DD79436U, 0x3DCCCCCDU, 0x3DC30C31U, 0x3DBA2E8CU,
            0x3DB21643U, 0x3DAAAAABU, 0x3DA3D70AU, 0x3D9D89D9U, 0x3D97B426U, 0x3D924925U, 0x3D8D3DCBU, 0x3D888889U, 0x3D842108U, 0x3D800000U, 0x3D783E10U,
        ];

        ReadOnlySpan<int> lengths = [1, 7, 15, 29, 33];
        float[] rawOutput = new float[raw.Length + 2];
        foreach (int length in lengths)
        {
            rawOutput.AsSpan().Fill(float.NegativeInfinity);
            JxlVarDctDecoder.ConvertRawQuantizationRow(raw[..length], rawOutput.AsSpan(1, length), 0.5F);
            for (int i = 0; i < length; i++)
            {
                // RAW integers use the native expression 1 / (denominator * sample), before the decoder reciprocal.
                Assert.Equal(rawWeightBits[i], BitConverter.SingleToUInt32Bits(rawOutput[i + 1]));
            }

            Assert.Equal(float.NegativeInfinity, rawOutput[0]);
            Assert.Equal(float.NegativeInfinity, rawOutput[length + 1]);
        }

        int[] invalidRaw = raw.ToArray();
        ReadOnlySpan<int> invalidIndices = [0, raw.Length - 1];
        foreach (int index in invalidIndices)
        {
            invalidRaw[index] = index == 0 ? 0 : -1;
            Assert.Throws<InvalidImageContentException>(
                () => JxlVarDctDecoder.ConvertRawQuantizationRow(invalidRaw, rawOutput.AsSpan(1, invalidRaw.Length), 0.5F));

            invalidRaw[index] = raw[index];
        }

        // Half-precision band deltas bound adjacent factors between 1/65505 and 65505. Exercise both limits,
        // a constant band, and multiple positive/negative deltas. Non-power-of-two widths exercise SIMD remainders.
        ReadOnlySpan<int> widths = [4, 7, 8, 15, 16, 17, 33, 256];
        float[] output = new float[(4 * 256) + 2];
        for (int scenario = 0; scenario < 4; scenario++)
        {
            ReadOnlySpan<float> parameters = scenario switch
            {
                0 => [17F],
                1 => [1F, 65504F],
                2 => [1F, -65504F],
                _ => [1024F, -0.25F, 0.5F, -0.5F]
            };

            foreach (int width in widths)
            {
                const int Rows = 4;
                output.AsSpan().Fill(float.NegativeInfinity);
                JxlVarDctDecoder.GenerateRadialQuantization(parameters, Rows, width, output.AsSpan(1, Rows * width));
                for (int y = 0; y < Rows; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        // The specified radial position is sqrt((x/(width-1))^2 + (y/(height-1))^2),
                        // measured in bands across sqrt(2)+epsilon. MathF.Pow supplies an independent interpolation reference.
                        float scale = (parameters.Length - 1) / (MathF.Sqrt(2F) + 1e-6F);
                        float dx = x * (scale / (width - 1));
                        float dy = y * (scale / (Rows - 1));
                        float position = MathF.Sqrt((dx * dx) + (dy * dy));
                        int band = (int)position;
                        float lower = parameters[0];
                        for (int i = 1; i <= band; i++)
                        {
                            lower *= parameters[i] > 0F ? 1F + parameters[i] : 1F / (1F - parameters[i]);
                        }

                        float expected = lower;
                        if (parameters.Length > 1)
                        {
                            float delta = parameters[band + 1];
                            float upper = lower * (delta > 0F ? 1F + delta : 1F / (1F - delta));
                            expected *= MathF.Pow(upper / lower, position - band);
                        }

                        float value = output[1 + (y * width) + x];
                        Assert.True(float.IsFinite(value));
                        Assert.InRange(MathF.Abs((value - expected) / expected), 0F, 0.000003F);
                    }
                }

                Assert.Equal(float.NegativeInfinity, output[0]);
                Assert.Equal(float.NegativeInfinity, output[(Rows * width) + 1]);
            }
        }
    }

    /// <summary>
    /// Gets selected coefficient bits from original libjxl Decode and EnsureComputed for the stored matrix descriptions.
    /// </summary>
    /// <param name="strategy">The custom strategy, or one hundred for the default DCT8 description.</param>
    /// <returns>Three component groups in the control-point order used by the parser test.</returns>
    private static ReadOnlySpan<uint> NativeMatrixPoints(int strategy) => strategy switch
    {
        0 =>
        [
            0x3F000000U, 0x3F800000U, 0x3FC00000U, 0x40200000U, 0x405FFFFFU, 0x3F000000U, 0x3F800000U, 0x40800000U, 0x3F000000U, 0x3F800000U, 0x3FC00000U,
            0x3F000000U, 0x40200000U, 0x405FFFFFU, 0x40800000U,
            0x3F000000U, 0x3F800000U, 0x3FC00000U, 0x40200000U, 0x405FFFFFU, 0x3F000000U, 0x3F800000U, 0x40800000U, 0x3F000000U, 0x3F800000U, 0x3FC00000U,
            0x3F000000U, 0x40200000U, 0x405FFFFFU, 0x40800000U,
            0x3F000000U, 0x3F800000U, 0x3FC00000U, 0x40200000U, 0x405FFFFFU, 0x3F000000U, 0x3F800000U, 0x40800000U, 0x3F000000U, 0x3F800000U, 0x3FC00000U,
            0x3F000000U, 0x40200000U, 0x405FFFFFU, 0x40800000U,
        ],
        1 =>
        [
            0x3B6A0EA1U, 0x39A5E9F7U, 0x3B6A0EA1U, 0x3B6A0EA1U, 0x3B6A0EA1U, 0x39A5E9F7U, 0x39A5E9F7U, 0x3B6A0EA1U, 0x3B6A0EA1U, 0x3B6A0EA1U, 0x3B6A0EA1U,
            0x3B6A0EA1U, 0x3B6A0EA1U, 0x3B6A0EA1U, 0x3B6A0EA1U,
            0x3C888889U, 0x3A97B426U, 0x3C888889U, 0x3C888889U, 0x3C888889U, 0x3A97B426U, 0x3A97B426U, 0x3C888889U, 0x3C888889U, 0x3C888889U, 0x3C888889U,
            0x3C888889U, 0x3C888889U, 0x3C888889U, 0x3C888889U,
            0x3D638E39U, 0x3BA3D70AU, 0x3D638E39U, 0x3D638E39U, 0x3D638E39U, 0x3BA3D70AU, 0x3BA3D70AU, 0x3D638E39U, 0x3D638E39U, 0x3D638E39U, 0x3D638E39U,
            0x3D638E39U, 0x3D638E39U, 0x3D638E39U, 0x3D638E39U,
        ],
        2 =>
        [
            0x39AF67E4U, 0x39888889U, 0x3A4CCCCDU, 0x3B088889U, 0x3B088889U, 0x39888889U, 0x39CCCCCDU, 0x3B088889U, 0x3A4CCCCDU, 0x3A4CCCCDU, 0x3ACCCCCDU,
            0x3B088889U, 0x3B5A740EU, 0x3B5A740EU, 0x3B5A740EU,
            0x39AF67E4U, 0x3A888889U, 0x3B4CCCCDU, 0x3BEA0EA1U, 0x3BEA0EA1U, 0x3A888889U, 0x3ACCCCCDU, 0x3BEA0EA1U, 0x3B4CCCCDU, 0x3B4CCCCDU, 0x3BB60B61U,
            0x3BEA0EA1U, 0x3C088889U, 0x3C088889U, 0x3C088889U,
            0x39AF67E4U, 0x3ACCCCCDU, 0x3C000000U, 0x3D000000U, 0x3D000000U, 0x3ACCCCCDU, 0x3B4CCCCDU, 0x3D000000U, 0x3C000000U, 0x3C000000U, 0x3C800000U,
            0x3D000000U, 0x3D800000U, 0x3D800000U, 0x3D800000U,
        ],
        3 =>
        [
            0x39EE5011U, 0x39EE5011U, 0x39EE501EU, 0x39EE5018U, 0x39EE5012U, 0x39EE5011U, 0x39EE5011U, 0x39EE5012U, 0x39EE501EU, 0x39EE501EU, 0x39EE5024U,
            0x39EE5018U, 0x39EE5024U, 0x39EE5024U, 0x39EE5024U,
            0x3B272F06U, 0x3B272F06U, 0x3B272F0FU, 0x3B272F0CU, 0x3B272F07U, 0x3B272F06U, 0x3B272F06U, 0x3B272F07U, 0x3B272F0FU, 0x3B272F0FU, 0x3B272F15U,
            0x3B272F0CU, 0x3B272F15U, 0x3B272F15U, 0x3B272F15U,
            0x3C124925U, 0x3C124925U, 0x3C2B49C4U, 0x3C489067U, 0x3C701898U, 0x3C124925U, 0x3C124925U, 0x3C701898U, 0x3C2B49C4U, 0x3C2B49C4U, 0x3C36DB61U,
            0x3C489067U, 0x3C649236U, 0x3CAB6D9DU, 0x3CAB6D9DU,
        ],
        6 =>
        [
            0x3A800001U, 0x3A815A82U, 0x3A8856C9U, 0x3A89C7DEU, 0x3A8B3CD9U, 0x3A95E08BU, 0x3A82EB01U, 0x3A83394FU, 0x3A94903BU, 0x3A961E0DU, 0x3A85E70AU,
            0x3A97F8D0U, 0x3A8C13C1U, 0x3A9D90F6U, 0x3A9FFFF5U,
            0x3B000001U, 0x3AFB2742U, 0x3AE4431EU, 0x3ADFF0CFU, 0x3ADBB373U, 0x3AC02FFBU, 0x3AF5BA01U, 0x3AF4AFD2U, 0x3AC34962U, 0x3ABFA0FBU, 0x3AEBDD8CU,
            0x3ABB66A2U, 0x3AD9507FU, 0x3AAF7C92U, 0x3AAAAAB7U,
            0x3B800001U, 0x3B827858U, 0x3B8F8DE0U, 0x3B92530DU, 0x3B9525ECU, 0x3BAA8008U, 0x3B8559FAU, 0x3B85EB0AU, 0x3BA7CB56U, 0x3BAAFF45U, 0x3B8AED3AU,
            0x3BAEDADEU, 0x3B96C93DU, 0x3BBAB9EFU, 0x3BBFFFE7U,
        ],
        8 =>
        [
            0x3F000000U, 0x3F800000U, 0x405FFFFFU, 0x40800000U, 0x40900000U, 0x41000000U, 0x41080000U, 0x41100000U, 0x41780000U, 0x3F000000U, 0x3F800000U,
            0x3F800000U, 0x3FC00000U, 0x41180000U, 0x40800000U,
            0x40900000U, 0x40A00000U, 0x40EFFFFFU, 0x41000000U, 0x41080000U, 0x41400000U, 0x41480000U, 0x414FFFFFU, 0x40800000U, 0x40900000U, 0x40A00000U,
            0x40A00000U, 0x40B00000U, 0x41580000U, 0x41000000U,
            0x41080000U, 0x41100000U, 0x41380000U, 0x41400000U, 0x41480000U, 0x3F000000U, 0x3F800000U, 0x3FC00000U, 0x41000000U, 0x41080000U, 0x41100000U,
            0x41100000U, 0x41180000U, 0x40000000U, 0x41400000U,
        ],
        12 =>
        [
            0x39EE8794U, 0x3A124C7AU, 0x3A3375E5U, 0x3A83F6A7U, 0x3ABA001BU, 0x39EE8794U, 0x3A124C7AU, 0x3ADB26C5U, 0x3A401B9AU, 0x3A4855CEU, 0x3A5F748AU,
            0x3A93F56DU, 0x3ABEBB41U, 0x3B19C2ADU, 0x3B2A936BU,
            0x3AAB8F6BU, 0x3AD14424U, 0x3AFF425FU, 0x3B3EBF2EU, 0x3B8FBA15U, 0x3AAB8F6BU, 0x3AD14424U, 0x3BA77287U, 0x3B085E33U, 0x3B0E0B3BU, 0x3B1DF5DDU,
            0x3B5AB074U, 0x3B941B1EU, 0x3BC58497U, 0x3BCFC4CDU,
            0x3AF8B6A8U, 0x3B235668U, 0x3B568984U, 0x3BB8E6B6U, 0x3C1F1BFAU, 0x3AF8B6A8U, 0x3B235668U, 0x3C5217C4U, 0x3B6AF31CU, 0x3B787950U, 0x3B8FBB87U,
            0x3BDD9510U, 0x3C258EE6U, 0x3CC6EDB1U, 0x3CF1E8A9U,
        ],
        14 =>
        [
            0x3F800000U, 0x39AAAAABU, 0x3B800000U, 0x3B1E4CAEU, 0x3B1E4CAFU, 0x39AAAAABU, 0x3A124C7AU, 0x3ADB26C5U, 0x3B800000U, 0x39EE501EU, 0x3B800000U,
            0x3B1E4CB0U, 0x3B1E4CB6U, 0x3B1E4CBCU, 0x3B2A936BU,
            0x3F800000U, 0x3A800000U, 0x3CA3D70AU, 0x3C8D3DCCU, 0x3C8D3DCDU, 0x3A800000U, 0x3AD14424U, 0x3BA77287U, 0x3CA3D70AU, 0x3B272F0FU, 0x3CA3D70AU,
            0x3C8D3DCEU, 0x3C8D3DD2U, 0x3C8D3DD8U, 0x3BCFC4CDU,
            0x3F800000U, 0x3B2AAAABU, 0x3DAAAAABU, 0x3D3A2E8DU, 0x3D6F0F4BU, 0x3B2AAAABU, 0x3B235668U, 0x3C5217C4U, 0x3DAAAAABU, 0x3C2B49C4U, 0x3DAAAAABU,
            0x3D42ADFEU, 0x3D84BDA3U, 0x3DB5D167U, 0x3CF1E8A9U,
        ],
        100 =>
        [
            0x39A670CEU, 0x39A670D6U, 0x39A70299U, 0x39EA9D3EU, 0x3A24CABAU, 0x39A670D6U, 0x39A670D8U, 0x3A45B0F0U, 0x39A70299U, 0x39ADD8B8U, 0x39C04237U,
            0x39EA9D3EU, 0x3A1B7500U, 0x3A9C4791U, 0x3B2B4438U,
            0x3AEA0EA3U, 0x3AEA0EADU, 0x3AEAAE6FU, 0x3B18F322U, 0x3B475D6DU, 0x3AEA0EADU, 0x3AEA0EB0U, 0x3B65C483U, 0x3AEAAE6FU, 0x3AF22388U, 0x3B02F4AEU,
            0x3B18F322U, 0x3B3E80A7U, 0x3B8A9019U, 0x3BA71F5CU,
            0x3B000001U, 0x3B5EF12EU, 0x3BC15B17U, 0x3C400001U, 0x3C4418A4U, 0x3B5EF12EU, 0x3B8C4664U, 0x3C8EA7C2U, 0x3BC15B17U, 0x3BD203C7U, 0x3C0134D8U,
            0x3C400001U, 0x3C40000FU, 0x3D0365FBU, 0x3D8FFFCDU,
        ],
        _ => throw new ArgumentOutOfRangeException(nameof(strategy))
    };

    /// <summary>
    /// Gets the same coefficient points independently generated by original jxl-rs DequantMatrices.
    /// </summary>
    /// <param name="strategy">The custom strategy, or one hundred for the default DCT8 description.</param>
    /// <returns>Three component groups in the control-point order used by the parser test.</returns>
    private static ReadOnlySpan<uint> RustMatrixPoints(int strategy) => strategy switch
    {
        0 or 1 or 2 or 8 => NativeMatrixPoints(strategy),
        3 =>
        [
            0x39EE500FU, 0x39EE500FU, 0x39EE500FU, 0x39EE500FU, 0x39EE500FU, 0x39EE500FU, 0x39EE500FU, 0x39EE500FU, 0x39EE500FU, 0x39EE500FU, 0x39EE500FU,
            0x39EE500FU, 0x39EE500FU, 0x39EE500FU, 0x39EE500FU,
            0x3B272F05U, 0x3B272F05U, 0x3B272F05U, 0x3B272F05U, 0x3B272F05U, 0x3B272F05U, 0x3B272F05U, 0x3B272F05U, 0x3B272F05U, 0x3B272F05U, 0x3B272F05U,
            0x3B272F05U, 0x3B272F05U, 0x3B272F05U, 0x3B272F05U,
            0x3C124925U, 0x3C124925U, 0x3C2B49CCU, 0x3C48906CU, 0x3C70189CU, 0x3C124925U, 0x3C124925U, 0x3C70189CU, 0x3C2B49CCU, 0x3C2B49CCU, 0x3C36DB6CU,
            0x3C48906CU, 0x3C649244U, 0x3CAB6DAFU, 0x3CAB6DAFU,
        ],
        6 =>
        [
            0x3A800000U, 0x3A815A82U, 0x3A8856CCU, 0x3A89C7E1U, 0x3A8B3CDDU, 0x3A95E092U, 0x3A82EB02U, 0x3A833950U, 0x3A949042U, 0x3A961E15U, 0x3A85E70BU,
            0x3A97F8D8U, 0x3A8C13C5U, 0x3A9D9100U, 0x3A9FFFFEU,
            0x3B000000U, 0x3AFB2742U, 0x3AE4431AU, 0x3ADFF0CCU, 0x3ADBB36FU, 0x3AC02FF4U, 0x3AF5B9FFU, 0x3AF4AFD0U, 0x3AC3495BU, 0x3ABFA0F3U, 0x3AEBDD89U,
            0x3ABB669BU, 0x3AD95079U, 0x3AAF7C8BU, 0x3AAAAAAEU,
            0x3B800000U, 0x3B827858U, 0x3B8F8DE4U, 0x3B925313U, 0x3B9525F2U, 0x3BAA8015U, 0x3B8559FBU, 0x3B85EB0CU, 0x3BA7CB62U, 0x3BAAFF51U, 0x3B8AED3DU,
            0x3BAEDAEDU, 0x3B96C943U, 0x3BBABA04U, 0x3BBFFFFCU,
        ],
        12 =>
        [
            0x39EE8792U, 0x3A124C75U, 0x3A3375D9U, 0x3A83F6A6U, 0x3ABA0014U, 0x39EE8792U, 0x3A124C75U, 0x3ADB26C7U, 0x3A401B8CU, 0x3A4855BEU, 0x3A5F7473U,
            0x3A93F56BU, 0x3ABEBB3BU, 0x3B19C2B7U, 0x3B2A9379U,
            0x3AAB8F6AU, 0x3AD1441DU, 0x3AFF424DU, 0x3B3EBF28U, 0x3B8FBA09U, 0x3AAB8F6AU, 0x3AD1441DU, 0x3BA77287U, 0x3B085E27U, 0x3B0E0B2EU, 0x3B1DF5CEU,
            0x3B5AB06CU, 0x3B941B11U, 0x3BC5849CU, 0x3BCFC4D3U,
            0x3AF8B6A6U, 0x3B23566CU, 0x3B56898FU, 0x3BB8E6B7U, 0x3C1F1C05U, 0x3AF8B6A6U, 0x3B23566CU, 0x3C5217C5U, 0x3B6AF32BU, 0x3B78795FU, 0x3B8FBB94U,
            0x3BDD9517U, 0x3C258EF2U, 0x3CC6EDB2U, 0x3CF1E8ACU,
        ],
        14 =>
        [
            0x3F800000U, 0x39AAAAABU, 0x3B800000U, 0x3B1E4CADU, 0x3B1E4CADU, 0x39AAAAABU, 0x3A124C75U, 0x3ADB26C7U, 0x3B800000U, 0x39EE500FU, 0x3B800000U,
            0x3B1E4CADU, 0x3B1E4CADU, 0x3B1E4CADU, 0x3B2A9379U,
            0x3F800000U, 0x3A800000U, 0x3CA3D70AU, 0x3C8D3DCBU, 0x3C8D3DCBU, 0x3A800000U, 0x3AD1441DU, 0x3BA77287U, 0x3CA3D70AU, 0x3B272F05U, 0x3CA3D70AU,
            0x3C8D3DCBU, 0x3C8D3DCBU, 0x3C8D3DCBU, 0x3BCFC4D3U,
            0x3F800000U, 0x3B2AAAABU, 0x3DAAAAABU, 0x3D3A2E8CU, 0x3D6F0F4BU, 0x3B2AAAABU, 0x3B23566CU, 0x3C5217C5U, 0x3DAAAAABU, 0x3C2B49CCU, 0x3DAAAAABU,
            0x3D42AE00U, 0x3D84BDA7U, 0x3DB5D172U, 0x3CF1E8ACU,
        ],
        100 =>
        [
            0x39A670CDU, 0x39A670CDU, 0x39A70298U, 0x39EA9D3EU, 0x3A24CAB9U, 0x39A670CDU, 0x39A670CDU, 0x3A45B0E3U, 0x39A70298U, 0x39ADD8B6U, 0x39C0422CU,
            0x39EA9D3EU, 0x3A1B74F1U, 0x3A9C4796U, 0x3B2B444CU,
            0x3AEA0EA1U, 0x3AEA0EA1U, 0x3AEAAE6FU, 0x3B18F322U, 0x3B475D6DU, 0x3AEA0EA1U, 0x3AEA0EA1U, 0x3B65C482U, 0x3AEAAE6FU, 0x3AF22388U, 0x3B02F4AEU,
            0x3B18F322U, 0x3B3E80A6U, 0x3B8A9018U, 0x3BA71F5CU,
            0x3B000000U, 0x3B5EF139U, 0x3BC15B16U, 0x3C400000U, 0x3C4418A3U, 0x3B5EF139U, 0x3B8C466EU, 0x3C8EA7BCU, 0x3BC15B16U, 0x3BD203C4U, 0x3C0134D2U,
            0x3C400000U, 0x3C400000U, 0x3D0365FFU, 0x3D8FFFDDU,
        ],
        _ => throw new ArgumentOutOfRangeException(nameof(strategy))
    };
}
