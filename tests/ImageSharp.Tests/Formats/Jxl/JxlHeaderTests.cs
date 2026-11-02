// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
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
public partial class JxlHeaderTests
{
    /// <summary>
    /// Verifies compact flat-tree storage and complete leaf payloads across signed threshold boundaries.
    /// </summary>
    /// <param name="outerProperty">The value compared with the root's threshold of seven.</param>
    /// <param name="innerProperty">The value compared with the lesser child's threshold of minus five.</param>
    /// <param name="offset">The expected signed prediction offset.</param>
    /// <param name="cluster">The expected sample entropy cluster.</param>
    /// <param name="predictor">The expected predictor code.</param>
    /// <param name="multiplier">The expected unsigned residual multiplier.</param>
    [Theory]
    [InlineData(8, 0, -123, 17, (byte)6, 0x12345678U)]
    [InlineData(7, -4, 321, 18, (byte)13, 0x23456789U)]
    [InlineData(7, -5, -456, 19, (byte)5, 0x3456789AU)]
    public void FlatTreeRetainsLeafPayload(int outerProperty, int innerProperty, int offset, int cluster, byte predictor, uint multiplier)
    {
        Assert.Equal(20, Unsafe.SizeOf<JxlModularTree.Node>());
        Assert.Equal(20, Unsafe.SizeOf<JxlModularTree.FlatNode>());

        // Greater values select the first child. Property 255 also proves that a full byte property ID
        // cannot be confused with the separate leaf tag. The second comparison includes equality at -5.
        JxlModularTree.Node[] nodes =
        [
            new(255, 7, 1, 0, 1),
            new(-1, -123, 17, 6, 0x12345678U),
            new(2, -5, 3, 0, 1),
            new(-1, 321, 18, 13, 0x23456789U),
            new(-1, -456, 19, 5, 0x3456789AU)
        ];

        using JxlModularWorkspace workspace = new(Configuration.Default.MemoryAllocator);
        ReadOnlySpan<JxlModularTree.FlatNode> flat = JxlModularTree.PrepareFlatTree(nodes, workspace);

        // Completed flat storage owns the copied leaf fields. Source-node reuse must not change the selected payload.
        nodes.AsSpan().Clear();
        InlineArray256<int> properties = default;
        properties[255] = outerProperty;
        properties[2] = innerProperty;
        ref readonly JxlModularTree.Node leaf = ref JxlModularTree.FindFlatLeaf(flat, ref properties);

        Assert.Equal(-1, leaf.Property);
        Assert.Equal(offset, leaf.Value);
        Assert.Equal(cluster, leaf.FirstChild);
        Assert.Equal(predictor, leaf.Predictor);
        Assert.Equal(multiplier, leaf.Multiplier);
    }

    /// <summary>
    /// Verifies output and coding dimensions across the original reference file's two LF levels and displayed frame.
    /// </summary>
    [Fact]
    public void ReadLowFrequencyOutputDimensionsMatchesReferences()
    {
        using MemoryStream stream = new(TestFile.Create(TestImages.Jxl.LowFrequencyPyramid).Bytes, false);
        using BufferedReadStream buffered = new(Configuration.Default, stream);
        JxlBitReader reader = new(new JxlCodestreamReader(buffered));
        MemoryAllocator allocator = Configuration.Default.MemoryAllocator;
        JxlHeader header = JxlHeader.Read(ref reader, allocator, SegmentIntegrityHandling.Strict);
        Assert.Equal(new Size(64, 64), header.Size);

        // Original libjxl ToFrameDimensions and jxl-rs size_upsampled agree on this existing three-frame pyramid.
        // Frame declarations keep the canvas size. Their output planes reduce by 8^level before color upsampling.
        ReadOnlySpan<int> levels = [2, 1, 0];
        ReadOnlySpan<int> lengths = [1, 8, 64];
        for (int i = 0; i < levels.Length; i++)
        {
            JxlFrameHeader frame = JxlFrameHeader.Read(ref reader, header, false, SegmentIntegrityHandling.Strict);
            Assert.Equal(new Size(64, 64), frame.Size);
            Assert.Equal(levels[i], frame.LowFrequencyLevel);
            Assert.Equal(new Size(lengths[i], lengths[i]), frame.OutputSize);
            Assert.Equal(frame.OutputSize, frame.CodingSize);
            Assert.Equal(i == 2 ? JxlFrameHeader.FrameType.Regular : JxlFrameHeader.FrameType.LowFrequency, frame.Type);
            Assert.Equal(i == 2, frame.IsLast);
            Assert.True(JxlFrameSections.Skip(ref reader, allocator, frame.SectionCount, SegmentIntegrityHandling.Strict, default));
        }
    }

    /// <summary>
    /// Verifies DC and AC metadata, tables and quantized coefficients against the original reference decoders.
    /// </summary>
    /// <param name="file">The unchanged reference input with one combined frame section.</param>
    /// <param name="globalScale">The encoded global scale reported by both references.</param>
    /// <param name="dcQuantizer">The encoded DC quantizer reported by both references.</param>
    /// <param name="globalBits">The number of DC-global bits before the global modular tree.</param>
    /// <param name="modularBits">The number of bits consumed through the global modular data.</param>
    [Theory]
    [InlineData(TestImages.Jxl.Square, 4587, 16, 201, 317)]
    [InlineData(TestImages.Jxl.LossyRgb3x3, 32768, 100, 213, 321)]
    public void ReadVarDctGlobalMetadataMatchesNative(string file, int globalScale, int dcQuantizer, int globalBits, int modularBits)
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
        using JxlVarDctDecoder varDct = JxlVarDctDecoder.ReadGlobal(ref reader, header, frame, Configuration.Default.MemoryAllocator, factors, []);
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

        // This group has no modular extra channels. AC metadata follows the DC samples without byte alignment.
        // Native decoding selects strategy 12 / quantizer 11 for Square, and strategy 2 / quantizer 60 for LossyRgb3x3.
        varDct.ReadAcMetadata(ref reader, modular, 0, default);
        int strategy = file == TestImages.Jxl.Square ? 12 : 2;
        Assert.Equal(start + (file == TestImages.Jxl.Square ? 342 : 396), reader.Position);
        Assert.Equal((byte)((strategy << 1) | 1), varDct.Strategies.DangerousGetRowSpan(0)[0]);
        Assert.Equal(file == TestImages.Jxl.Square ? 11 : 60, varDct.Quantization.DangerousGetRowSpan(0)[0]);
        Assert.Equal((byte)4, varDct.Sharpness.DangerousGetRowSpan(0)[0]);
        Assert.Equal((sbyte)0, varDct.CorrelationX.DangerousGetRowSpan(0)[0]);
        Assert.Equal((sbyte)0, varDct.CorrelationB.DangerousGetRowSpan(0)[0]);
        Assert.Equal(1U << strategy, varDct.UsedStrategies);

        // Both references consume the complete AC-global description at these bit offsets. The remaining
        // bits belong to the AC coefficient group, not to padding between the global and group descriptions.
        varDct.ReadGlobalAc(ref reader, frame, modular, Configuration.Default.MemoryAllocator, default);
        varDct.PrepareReconstruction(Configuration.Default.MemoryAllocator, header, frame);
        Assert.Equal(start + (file == TestImages.Jxl.Square ? 360 : 618), reader.Position);
        Assert.Equal(1U, varDct.AcHistogramSetCount);
        Assert.Equal(1, varDct.AcPassCount);
        Assert.Equal(file == TestImages.Jxl.Square ? 4 : 13, varDct.MaximumAcValueBits);
        Assert.True(varDct.Uses16BitCoefficients);

        // These one-block transforms share the natural 8-by-8 order across X, Y and B. Retain one copy,
        // and leave every unused family unallocated. The native and Rust reports contain this complete order.
        ReadOnlySpan<uint> naturalOrder =
        [
            0, 1, 8, 16, 9, 2, 3, 10, 17, 24, 32, 25, 18, 11, 4, 5,
            12, 19, 26, 33, 40, 48, 41, 34, 27, 20, 13, 6, 7, 14, 21, 28,
            35, 42, 49, 56, 57, 50, 43, 36, 29, 22, 15, 23, 30, 37, 44, 51,
            58, 59, 52, 45, 38, 31, 39, 46, 53, 60, 61, 54, 47, 55, 62, 63
        ];

        Assert.Equal(naturalOrder, varDct.GetCoefficientOrders(0).Span);
        int orderIndex = JxlVarDctDecoder.GetCoefficientOrderIndex((byte)strategy, 0);
        ReadOnlySpan<int> offsets = varDct.GetCoefficientOrderOffsets(0);
        for (int i = 0; i < offsets.Length; i++)
        {
            Assert.Equal(i >= orderIndex && i < orderIndex + 3 ? 0 : -1, offsets[i]);
        }

        // The selected matrices contain three 64-coefficient planes. The unused quantization families
        // have no retained storage. Matrix arithmetic has separate native-backed all-mode and SIMD checks.
        Assert.Equal(3 * naturalOrder.Length, varDct.QuantizationMatrices.Length);
        int matrixIndex = JxlVarDctDecoder.GetQuantizationMatrixIndex((byte)strategy, 0);
        ReadOnlySpan<int> matrixOffsets = varDct.QuantizationMatrixOffsets;
        for (int i = 0; i < matrixOffsets.Length; i++)
        {
            Assert.Equal(i >= matrixIndex && i < matrixIndex + 3 ? (i - matrixIndex) * naturalOrder.Length : -1, matrixOffsets[i]);
        }

        // Original libjxl DecodeACVarBlock consumes these complete group payloads and validates their final ANS states.
        // Square has no nonzero AC values. The other file has 28, 18, 23 nonzeros in encoded Y, X, B component order.
        varDct.ReadAcGroup(ref reader, 0, 0, default);
        Assert.Equal(start + (file == TestImages.Jxl.Square ? 360 : 1472), reader.Position);
        for (int component = 0; component < 3; component++)
        {
            ReadOnlySpan<short> coefficients = varDct.GetCoefficients16(component);
            Assert.Equal(64, coefficients.Length);
            if (file == TestImages.Jxl.Square)
            {
                Assert.False(coefficients.ContainsAnyExcept((short)0));
                continue;
            }

            // These complete X, Y, B planes come from original libjxl, independently of this entropy implementation.
            // Their first slot remains zero because the inverse transform restores its LF value from the DC plane.
            ReadOnlySpan<short> expectedCoefficients = component switch
            {
                0 =>
                [
                    0, 35, 47, 0, 52, 10, 0, 0, 35, 23, 0, 0, -4, 2, 0, 0, 1, 0, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 430, 40, 0, 0, 132, 7, 0, 0,
                    -5, 4, 0, 0, -5, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                ],
                1 =>
                [
                    0, 1103, 153, 1494, 994, -83, -58, -58, 3882, -523, 0, 0, 195, -131, -153, -153, 2405, 0, -756, 0, 0, 0, 0, 0, 3465, 0, 0, 0, 0, 0,
                    0, 0, -753, -214, 0, 0, -166, -28, 0, 0, 1009, -233, 0, 0, 93, 19, 0, 0, 924, -247, 0, 0, 0, 0, 0, 0, 924, -247, 0, 0, 0, 0, 0, 0,
                ],
                _ =>
                [
                    0, 802, 155, 518, -74, 184, 166, 166, -429, -214, 0, 0, 4, -10, -12, -12, -440, 0, -230, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0,
                    191, 66, 0, 0, 43, 11, 0, 0, 1, 3, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                ]
            };

            Assert.Equal(expectedCoefficients, coefficients);
        }

        // libjxl DequantBlock and TransformToPixels at revision 91837342b24034078780b687f57f670a7def8153
        // produce these visible 3-by-3 samples for the unchanged lossy input, in X/Y/B plane order.
        // Original jxl-rs differs by at most 5.96046e-8. An 8e-8 absolute bound includes that measured difference
        // and float multiply-add variation. These samples precede restoration, color conversion and pixel packing.
        ReadOnlySpan<uint> spatialBits =
        [
            0x3D267ADCU, 0xBCFAAC3EU, 0x3BA91E93U, 0x3C3AC971U, 0xBC53876DU, 0x3B383647U, 0xBA8F4634U, 0x39B3D114U, 0x3A2661F6U,
            0x3EF7C0A4U, 0x3F6F477EU, 0x3E313E6AU, 0x3DD27E51U, 0x3EBCFE8EU, 0x3E69A42BU, 0x3F844168U, 0x3F00485BU, 0xBE111608U,
            0x3F0A49C5U, 0x3ED3EC7AU, 0x3F451DC7U, 0x3E094DFDU, 0x3E3A08ABU, 0x3ECABA1DU, 0x3F85624EU, 0x3F020871U, 0xBE1B5B2EU
        ];

        for (int component = 0; component < 3; component++)
        {
            Buffer2DRegion<float> reconstructed = varDct.GetReconstructedChannel(component);
            Assert.Equal(frame.CodingSize, new Size(reconstructed.Width, reconstructed.Height));
            for (int y = 0; y < reconstructed.Height; y++)
            {
                ReadOnlySpan<float> row = reconstructed.DangerousGetRowSpan(y);
                if (file == TestImages.Jxl.Square)
                {
                    Assert.False(row.ContainsAnyExcept(0F));
                    continue;
                }

                for (int x = 0; x < row.Length; x++)
                {
                    float expectedSample = BitConverter.UInt32BitsToSingle(spatialBits[(component * 9) + (y * 3) + x]);
                    Assert.InRange(MathF.Abs(row[x] - expectedSample), 0F, 0.00000008F);
                }
            }
        }
    }

    /// <summary>
    /// Verifies progressive coefficient shifts and accumulation through an LZ77-coded second pass.
    /// </summary>
    /// <remarks>
    /// The stored component stream was generated with libjxl revision 91837342b24034078780b687f57f670a7def8153,
    /// using ProgressiveSplitter, TokenizeCoefficients, BuildAndEncodeHistograms and WriteTokens.
    /// Its shifts are 2 and 0. Every AC coefficient starts at X=20, Y=-20, B=20, then becomes X=23, Y=-23, B=21.
    /// The original libjxl and jxl-rs decoders agree on every coefficient and the recorded bit positions.
    /// The second pass uses the encoder's LZ77 run-length mode, so history must restart at that pass's first symbol.
    /// </remarks>
    [Fact]
    public void ReadAcGroupAccumulatesProgressiveLzPasses()
    {
        using MemoryStream input = new(TestFile.Create(TestImages.Jxl.VarDctProgressiveLzOwner).Bytes, false);
        using BufferedReadStream buffered = new(Configuration.Default, input);
        JxlBitReader reader = new(new JxlCodestreamReader(buffered));
        JxlHeader header = JxlHeader.Read(ref reader, Configuration.Default.MemoryAllocator, SegmentIntegrityHandling.Strict);
        JxlFrameHeader frame = JxlFrameHeader.Read(ref reader, header, false, SegmentIntegrityHandling.Strict);
        Assert.Equal((byte)2, frame.PassCount);
        Assert.Equal((ReadOnlySpan<byte>)[2, 0], frame.PassShifts);
        Vector3 factors = JxlDecoderCore.ReadDcQuantization(ref reader);
        using JxlVarDctDecoder varDct = JxlVarDctDecoder.ReadGlobal(ref reader, header, frame, Configuration.Default.MemoryAllocator, factors, []);
        using JxlModularDecoder modular = JxlModularDecoder.Read(ref reader, header, frame, Configuration.Default.MemoryAllocator, default);
        varDct.ReadDcGroup(ref reader, modular, 0, default);
        varDct.ReadAcMetadata(ref reader, modular, 0, default);
        varDct.ReadGlobalAc(ref reader, frame, modular, Configuration.Default.MemoryAllocator, default);
        varDct.PrepareReconstruction(Configuration.Default.MemoryAllocator, header, frame);
        Assert.Equal(363, reader.Position);

        // One cluster serves both literal and distance contexts. Native width selection interprets copy token
        // 518 with the shared normal hybrid configuration (4,2,0), giving a conservative bound of 130 bits.
        // That selects int storage even though these actual coefficients fit short. jxl-rs counts the bound differently.
        Assert.Equal(130, varDct.MaximumAcValueBits);
        Assert.False(varDct.Uses16BitCoefficients);

        // This partial stream omits a TOC. Its two coefficient packets are separately byte-aligned,
        // as ordinary frame sections would be. Preserve the reader and group workspace across passes.
        reader.AlignToByte();
        for (int pass = 0; pass < frame.PassCount; pass++)
        {
            varDct.ReadAcGroup(ref reader, 0, pass, default);
            Assert.Equal(pass == 0 ? 635 : 751, reader.Position);
            ReadOnlySpan<int> expected = pass == 0 ? [20, -20, 20] : [23, -23, 21];
            for (int component = 0; component < 3; component++)
            {
                ReadOnlySpan<int> coefficients = varDct.GetCoefficients32(component);
                Assert.Equal(64, coefficients.Length);
                Assert.Equal(0, coefficients[0]);
                Assert.False(coefficients[1..].ContainsAnyExcept(expected[component]));
            }

            reader.AlignToByte();
        }
    }

    /// <summary>
    /// Verifies thirty-two-bit coefficient selection and complete signed values through native-encoded entropy tables.
    /// </summary>
    [Fact]
    public void ReadAcGroupPreservesWideCoefficients()
    {
        using MemoryStream input = new(TestFile.Create(TestImages.Jxl.VarDctWideCoefficientsOwner).Bytes, false);
        using BufferedReadStream buffered = new(Configuration.Default, input);
        JxlBitReader reader = new(new JxlCodestreamReader(buffered));
        JxlHeader header = JxlHeader.Read(ref reader, Configuration.Default.MemoryAllocator, SegmentIntegrityHandling.Strict);
        JxlFrameHeader frame = JxlFrameHeader.Read(ref reader, header, false, SegmentIntegrityHandling.Strict);
        Vector3 factors = JxlDecoderCore.ReadDcQuantization(ref reader);
        using JxlVarDctDecoder varDct = JxlVarDctDecoder.ReadGlobal(ref reader, header, frame, Configuration.Default.MemoryAllocator, factors, []);
        using JxlModularDecoder modular = JxlModularDecoder.Read(ref reader, header, frame, Configuration.Default.MemoryAllocator, default);
        varDct.ReadDcGroup(ref reader, modular, 0, default);
        varDct.ReadAcMetadata(ref reader, modular, 0, default);
        varDct.ReadGlobalAc(ref reader, frame, modular, Configuration.Default.MemoryAllocator, default);
        varDct.PrepareReconstruction(Configuration.Default.MemoryAllocator, header, frame);
        Assert.Equal(272, reader.Position);
        Assert.Equal(32, varDct.MaximumAcValueBits);
        Assert.False(varDct.Uses16BitCoefficients);

        // The original native decoder preserves both signs outside the short range and the complete uint32 zigzag endpoint.
        // All other positions, including the LF prefix, remain zero. This fixture contains component fields without a TOC.
        varDct.ReadAcGroup(ref reader, 0, 0, default);
        Assert.Equal(407, reader.Position);
        ReadOnlySpan<int> indices = [1, 8, 63];
        ReadOnlySpan<int> values = [32768, -32769, int.MinValue];
        for (int component = 0; component < 3; component++)
        {
            ReadOnlySpan<int> coefficients = varDct.GetCoefficients32(component);
            Assert.Equal(64, coefficients.Length);
            int index = indices[component];
            Assert.Equal(values[component], coefficients[index]);
            Assert.False(coefficients[..index].ContainsAnyExcept(0));
            Assert.False(coefficients[(index + 1)..].ContainsAnyExcept(0));
        }
    }

    /// <summary>
    /// Verifies that subsampled AC contexts use the native encoder's component-scaled horizontal quantizer coordinate.
    /// </summary>
    [Fact]
    public void ReadAcGroupUsesSubsampledQuantizerContexts()
    {
        using MemoryStream input = new(TestFile.Create(TestImages.Jxl.VarDctSubsampledQfOwner).Bytes, false);
        using BufferedReadStream buffered = new(Configuration.Default, input);
        JxlBitReader reader = new(new JxlCodestreamReader(buffered));
        JxlHeader header = JxlHeader.Read(ref reader, Configuration.Default.MemoryAllocator, SegmentIntegrityHandling.Strict);
        JxlFrameHeader frame = JxlFrameHeader.Read(ref reader, header, false, SegmentIntegrityHandling.Strict);
        Assert.Equal(new Size(32, 8), frame.CodingSize);
        Assert.Equal(79, reader.Position);

        // This native-encoded component fixture omits the section table and stores all required fields in parser order.
        // Its four block quantizers are [1, 2, 200, 1], with threshold 100. X/B at block two use reduced coordinate one.
        Vector3 factors = JxlDecoderCore.ReadDcQuantization(ref reader);
        using JxlVarDctDecoder varDct = JxlVarDctDecoder.ReadGlobal(ref reader, header, frame, Configuration.Default.MemoryAllocator, factors, []);
        using JxlModularDecoder modular = JxlModularDecoder.Read(ref reader, header, frame, Configuration.Default.MemoryAllocator, default);
        varDct.ReadDcGroup(ref reader, modular, 0, default);
        varDct.ReadAcMetadata(ref reader, modular, 0, default);
        varDct.ReadGlobalAc(ref reader, frame, modular, Configuration.Default.MemoryAllocator, default);
        varDct.PrepareReconstruction(Configuration.Default.MemoryAllocator, header, frame);
        Assert.Equal(590, reader.Position);
        Assert.True(varDct.Uses16BitCoefficients);

        // The original native encoder and decoder agree on one Y coefficient and zero X/B coefficients.
        // Singleton histograms consume no group bits. Reading QF at full-grid x would select a different X/B histogram.
        varDct.ReadAcGroup(ref reader, 0, 0, default);
        Assert.Equal(590, reader.Position);
        for (int component = 0; component < 3; component++)
        {
            ReadOnlySpan<short> coefficients = varDct.GetCoefficients16(component);
            Assert.Equal(component == 1 ? 256 : 128, coefficients.Length);
            if (component == 1)
            {
                Assert.Equal((short)1, coefficients[129]);
                Assert.False(coefficients[..129].ContainsAnyExcept((short)0));
                Assert.False(coefficients[130..].ContainsAnyExcept((short)0));
            }
            else
            {
                Assert.False(coefficients.ContainsAnyExcept((short)0));
            }
        }
    }

    /// <summary>
    /// Verifies the format's saturation rules for encoded correlation and quantization fields.
    /// </summary>
    [Fact]
    public void ReadAcMetadataSaturatesStoredFields()
    {
        (sbyte correlationX, sbyte correlationB, int quantizer, byte sharpness, byte strategy) = ReadAcMetadataFixture(TestImages.Jxl.AcMetadataSaturation);

        // Original libjxl accepts these out-of-range modular integers and bounds the stored metadata fields.
        // Sharpness seven is valid. The packed strategy stores raw code 12 and its first-block bit.
        Assert.Equal(sbyte.MaxValue, correlationX);
        Assert.Equal(sbyte.MinValue, correlationB);
        Assert.Equal(256, quantizer);
        Assert.Equal((byte)7, sharpness);
        Assert.Equal((byte)25, strategy);
    }

    /// <summary>
    /// Verifies native rejection rules for invalid sharpness, strategy codes and transform dimensions.
    /// </summary>
    /// <param name="file">The stored native-encoded malformed metadata.</param>
    /// <param name="field">The field whose semantic validation must fail after entropy decoding.</param>
    [Theory]
    [InlineData(TestImages.Jxl.AcMetadataInvalidSharpness, "sharpness")]
    [InlineData(TestImages.Jxl.AcMetadataInvalidStrategy, "strategy")]
    [InlineData(TestImages.Jxl.AcMetadataStrategyOverflow, "boundary")]
    public void ReadAcMetadataRejectsInvalidFields(string file, string field)
    {
        InvalidImageContentException error = Assert.Throws<InvalidImageContentException>(() => ReadAcMetadataFixture(file));
        Assert.Contains(field, error.Message);
    }

    /// <summary>
    /// Reads a stored partial AC metadata stream using the real frame geometry and shared modular decoder.
    /// </summary>
    /// <param name="file">The native-encoded partial stream, including its raw signature.</param>
    /// <returns>The first block's decoded correlation, quantizer, sharpness and packed strategy fields.</returns>
    private static (sbyte CorrelationX, sbyte CorrelationB, int Quantizer, byte Sharpness, byte Strategy) ReadAcMetadataFixture(string file)
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

        using MemoryStream input = new(TestFile.Create(file).Bytes, false);
        using BufferedReadStream buffered = new(Configuration.Default, input);
        JxlBitReader reader = new(new JxlCodestreamReader(buffered));
        Assert.Equal(0x0AFFU, reader.ReadBits(16));
        varDct.ReadAcMetadata(ref reader, modular, 0, default);

        return (
            varDct.CorrelationX.DangerousGetRowSpan(0)[0],
            varDct.CorrelationB.DangerousGetRowSpan(0)[0],
            varDct.Quantization.DangerousGetRowSpan(0)[0],
            varDct.Sharpness.DangerousGetRowSpan(0)[0],
            varDct.Strategies.DangerousGetRowSpan(0)[0]);
    }

    /// <summary>
    /// Verifies direct tokens and the largest valid literal-bit width in fixed 4/2/0 expansion.
    /// </summary>
    /// <param name="token">The entropy token before fixed hybrid expansion.</param>
    /// <param name="expected">The unsigned value derived from the unchanged stored prefix.</param>
    /// <param name="bitsRead">The required literal-bit count.</param>
    [Theory]
    [InlineData(0U, 0U, 0)]
    [InlineData(15U, 15U, 0)]
    [InlineData(16U, 19U, 2)]
    [InlineData(127U, 0xFFFA0AFFU, 29)]
    public void ExpandAns420PreservesLiteralBoundaries(uint token, uint expected, int bitsRead)
    {
        using MemoryStream stream = new(TestFile.Create(TestImages.Jxl.Splines).Bytes, false);
        using BufferedReadStream buffered = new(Configuration.Default, stream);
        JxlBitReader reader = new(new JxlCodestreamReader(buffered));

        // This raw codestream starts FF 0A FA 3F. Its first two low-order bits are 3, and its first
        // 29 bits are 0x1FFA0AFF. Token 16 supplies high bits 4, giving (4<<2)|3 = 19.
        // Token 127 supplies high bits 7, giving (7<<29)|0x1FFA0AFF = 0xFFFA0AFF in 32 bits.
        // The fixed operator does not read its configuration argument; its type selects all three widths.
        uint actual = JxlEntropyDecoder.Ans420Operator.Expand(ref reader, token, default);

        Assert.Equal(expected, actual);
        Assert.Equal(bitsRead, reader.Position);
    }

    /// <summary>
    /// Verifies that fixed 4/2/0 expansion rejects a thirty-three-bit value before consuming literal bits.
    /// </summary>
    [Fact]
    public void ExpandAns420RejectsOverflowBeforeReading()
    {
        using MemoryStream stream = new(TestFile.Create(TestImages.Jxl.Splines).Bytes, false);
        using BufferedReadStream buffered = new(Configuration.Default, stream);
        JxlBitReader reader = new(new JxlCodestreamReader(buffered));

        // Token 128 requires 30 literal bits plus the three high bits, which cannot fit in uint.
        InvalidImageContentException exception = Assert.Throws<InvalidImageContentException>(
            () => JxlEntropyDecoder.Ans420Operator.Expand(ref reader, 128, default));

        Assert.Equal("The JPEG XL hybrid integer exceeds 32 bits.", exception.Message);
        Assert.Equal(0, reader.Position);
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
        InlineArray4<byte> byteStorage = default;
        InlineArray4<uint> integerStorage = default;
        Span<byte> byteHistory = byteStorage;
        Span<uint> integerHistory = integerStorage;
        JxlEntropyDecoder.Reader symbols = entropy.CreateReader(ref reader);

        // Context zero emits literal 7. Context one emits a length-three copy at distance one.
        // Both references return [7, 7] and accept the final state with two copied values still unused.
        uint first;
        uint second;
        if (byteValues)
        {
            byteHistory[0] = symbols.ReadByte(ref reader, 0, []);
            first = byteHistory[0];
            second = symbols.ReadByte(ref reader, 1, byteHistory[..1]);
        }
        else
        {
            first = symbols.ReadUInt32(ref reader, 0, integerHistory, 0);
            second = symbols.ReadUInt32(ref reader, 1, integerHistory, 1);
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
        JxlEntropyDecoder.Reader symbols = entropy.CreateReader(ref reader);
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
