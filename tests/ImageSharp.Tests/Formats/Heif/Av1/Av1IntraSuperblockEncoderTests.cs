// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Heif;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.ChromaFromLuma;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.Memory;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies live intra superblock mode decisions, traversal, and reconstruction.
/// </summary>
[Trait("Format", "Avif")]
public class Av1IntraSuperblockEncoderTests
{
    /// <summary>
    /// Verifies non-regular filter selection, retained reconstruction, and allocation-free inter tile coding.
    /// </summary>
    [Theory]
    [InlineData((int)Av1InterpolationFilter.Smooth, false)]
    [InlineData((int)Av1InterpolationFilter.Sharp, false)]
    [InlineData((int)Av1InterpolationFilter.Smooth, true)]
    [InlineData((int)Av1InterpolationFilter.Sharp, true)]
    public void ProductionTileSelectsNonRegularInterpolation(int filterValue, bool dualFilter)
    {
        const int Width = 32;
        const int Height = 8;
        const int TargetColumn = 8;
        const int BlockWidth = 8;
        const int QIndex = 37;
        const int TileBufferLength = 4096;
        Av1InterpolationFilter filter = (Av1InterpolationFilter)filterValue;
        int effort = dualFilter ? 9 : 8;
        ReadOnlySpan<byte> referencePeriod = [128, 184, 208, 184, 128, 72, 48, 72];

        // These are fixed half-sample responses of the reference's eight-tap smooth and sharp kernels.
        // The horizontal pass rounds first by three bits and then by four; edge samples are replicated.
        // Keeping the results literal avoids using the predictor under test to manufacture its own target.
        ReadOnlySpan<byte> targetRow = filter == Av1InterpolationFilter.Smooth
            ? [159, 189, 190, 154, 102, 66, 66, 102, 154, 190, 190, 154, 102, 66, 66, 102,
               154, 190, 190, 154, 102, 66, 66, 102, 154, 190, 190, 154, 102, 67, 61, 69]
            : [153, 204, 200, 158, 98, 55, 55, 98, 158, 202, 202, 158, 98, 55, 55, 98,
               158, 202, 202, 158, 98, 55, 55, 98, 158, 202, 202, 158, 100, 53, 59, 75];

        TestMemoryAllocator allocator = new();
        allocator.EnableNonThreadSafeLogging();
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = allocator;
        ObuColorConfig colorConfig = new()
        {
            IsMonochrome = true,
            ColorRange = true,
            SubSamplingX = true,
            SubSamplingY = true,
            BitDepth = Av1BitDepth.EightBit
        };

        using Image<L8> referenceImage = new(Width, Height);
        using Av1EncoderFrameBuffer<byte> reference = new(configuration, Width, Height, 8, Av1ColorFormat.Yuv400, 0, 0, lumaBorder: 64);
        using Av1EncoderFrameBuffer<byte> source = new(configuration, Width, Height, 8, Av1ColorFormat.Yuv400, 0, 0, lumaBorder: 64);
        using Av1EncoderFrameBuffer<byte> reconstruction = new(configuration, Width, Height, 8, Av1ColorFormat.Yuv400, 0, 0, lumaBorder: 64);
        for (int y = 0; y < Height; y++)
        {
            Span<L8> pixels = referenceImage.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y);
            Span<byte> referenceRow = reference.Frame.CodedView.GetPlane(Av1Plane.Y).DangerousGetRowSpan(y);
            for (int x = 0; x < Width; x++)
            {
                byte sample = referencePeriod[x % referencePeriod.Length];
                referenceRow[x] = sample;
                pixels[x] = new L8(sample);
            }

            targetRow.CopyTo(source.Frame.CodedView.GetPlane(Av1Plane.Y).DangerousGetRowSpan(y));
        }

        reference.Frame.ExtendBorders();
        source.Frame.ExtendBorders();
        ClearPlane(reconstruction.Luma);

        // A lossless key frame gives an independent decoder exactly the reference samples used by tile search.
        using MemoryStream firstSample = new();
        using Av1FrameEncoder.SequenceEncoder keyEncoder = Av1FrameEncoder.CreateColorSequenceEncoder(
            configuration,
            Width,
            Height,
            colorConfig,
            qIndex: 0,
            effort,
            speed: HeifEncodingSpeed.Level0);

        keyEncoder.EncodeKeyFrame(referenceImage.Frames.RootFrame, firstSample);
        ObuSequenceHeader sequenceHeader = keyEncoder.SequenceHeader;
        using Av1EncoderModeInfoBuffer modeInfo = new(configuration, Width, Height, disallow4x4AllFrames: true);
        Av1PictureControlSet template = CreatePicture(modeInfo, colorConfig, use128x128Superblock: false, QIndex);
        ObuFrameHeader frameHeader = template.Parent.FrameHeader;
        frameHeader.FrameType = ObuFrameType.InterFrame;
        frameHeader.ShowFrame = true;
        frameHeader.ErrorResilientMode = true;
        frameHeader.RefreshFrameFlags = byte.MaxValue;
        frameHeader.DisableFrameEndUpdateCdf = true;
        frameHeader.ReferenceMode = ObuReferenceMode.SingleReference;
        frameHeader.InterpolationFilter = Av1InterpolationFilter.Switchable;
        frameHeader.AllowHighPrecisionMotionVector = true;
        frameHeader.TransformMode = Av1TransformMode.Select;
        frameHeader.FrameSize.FrameWidth = Width;
        frameHeader.FrameSize.FrameHeight = Height;
        frameHeader.FrameSize.SuperResolutionUpscaledWidth = Width;
        frameHeader.FrameSize.RenderWidth = Width;
        frameHeader.FrameSize.RenderHeight = Height;
        frameHeader.TilesInfo.HasUniformTileSpacing = true;
        Av1QuantizationLookup.UpdateFrameQuantizationState(frameHeader);

        using Av1EncoderPictureBuffer picture = new(configuration, sequenceHeader, frameHeader, Width, Height, disallow4x4AllFrames: true);
        using Av1EncoderCoefficientBuffer coefficients = new(configuration, sequenceHeader, Width, Height);
        using Av1EncoderSuperblockWorkspace superblockWorkspace = new(configuration);
        using Av1EncoderBlockWorkspace blockWorkspace = new(configuration, allocateInterMotionCosts: true);
        using Av1SymbolEncoder symbolEncoder = new(configuration, TileBufferLength, QIndex, updateCdf: true);
        Av1EncoderTileWorkspace tileWorkspace = new(frameHeader, superblockWorkspace);
        int allocationCount = allocator.AllocationLog.Count;
        Av1TileEncoder tileWriter = new(
            symbolEncoder,
            source.Frame,
            reference.Frame,
            reconstruction.Frame,
            picture.Picture,
            coefficients,
            tileWorkspace,
            blockWorkspace,
            effort);

        Assert.Equal(allocationCount, allocator.AllocationLog.Count);
        Point targetPosition = new(TargetColumn >> Av1Constants.ModeInfoSizeLog2, 0);
        ref Av1MacroBlockModeInfo targetMode = ref picture.Picture.GetMacroBlockModeInfo(targetPosition);
        Assert.Equal(Av1ReferenceFrameType.Last, targetMode.Block.ReferenceFrame);
        Assert.Equal(filter, targetMode.Block.HorizontalInterpolationFilter);
        Assert.Equal(dualFilter ? Av1InterpolationFilter.Regular : filter, targetMode.Block.VerticalInterpolationFilter);
        Assert.Equal(4, picture.Picture.GetDisplacementVector(targetPosition).Column);
        Assert.Equal(0, picture.Picture.GetDisplacementVector(targetPosition).Row);
        for (int y = 0; y < Height; y++)
        {
            Assert.Equal(
                targetRow.Slice(TargetColumn, BlockWidth),
                reconstruction.Frame.View.GetPlane(Av1Plane.Y).DangerousGetRowSpan(y).Slice(TargetColumn, BlockWidth));
        }

        using MemoryStream secondSample = new();
        using ObuWriter obuWriter = new(configuration);
        obuWriter.WriteFrame(secondSample, sequenceHeader, frameHeader, tileWriter);
        using Av1Decoder decoder = new(configuration);

        // Sequence decoding retains the first frame's reference slots. The still-image transfer API deliberately
        // releases those slots, so it cannot be used between dependent samples. Full-range monochrome L8 is exact.
        using ImageFrame<L8> decodedFirst = decoder.DecodeSequenceFrame<L8>(firstSample.ToArray(), null, null);
        using ImageFrame<L8> decodedSecond = decoder.DecodeSequenceFrame<L8>(secondSample.ToArray(), null, null);

        // Preserve both the production stream and every managed reconstructed luma sample for exact libaom comparison.
        string outputDirectory = TestEnvironment.CreateOutputDirectory("Heif", "Av1", nameof(this.ProductionTileSelectsNonRegularInterpolation));
        string outputName = $"{filter}-{dualFilter}";
        using FileStream output = File.Create(Path.Combine(outputDirectory, outputName + ".obu"));
        firstSample.Position = 0;
        firstSample.CopyTo(output);
        secondSample.Position = 0;
        secondSample.CopyTo(output);
        using FileStream rawOutput = File.Create(Path.Combine(outputDirectory, outputName + ".managed.yuv"));
        for (int y = 0; y < Height; y++)
        {
            ReadOnlySpan<byte> expected = reference.Frame.View.GetPlane(Av1Plane.Y).DangerousGetRowSpan(y);
            ReadOnlySpan<byte> actual = MemoryMarshal.AsBytes(decodedFirst.PixelBuffer.DangerousGetRowSpan(y));
            Assert.Equal(expected, actual);
            rawOutput.Write(actual);
        }

        for (int y = 0; y < Height; y++)
        {
            ReadOnlySpan<byte> expected = reconstruction.Frame.View.GetPlane(Av1Plane.Y).DangerousGetRowSpan(y);
            ReadOnlySpan<byte> actual = MemoryMarshal.AsBytes(decodedSecond.PixelBuffer.DangerousGetRowSpan(y));
            Assert.Equal(expected, actual);
            rawOutput.Write(actual);
        }
    }

    /// <summary>
    /// Verifies independent half-sample filters on both axes without discarding native sample precision.
    /// </summary>
    [Theory]
    [InlineData(10, false)]
    [InlineData(10, true)]
    [InlineData(12, false)]
    [InlineData(12, true)]
    public void ProductionTileSelectsDualAxisInterpolationHighBitDepth(int bitDepth, bool reverseFilters)
    {
        const int Width = 48;
        const int Height = 24;
        const int TargetColumn = 16;
        const int TargetRow = 8;
        const int BlockSize = 8;
        const int QIndex = 1;
        const int Effort = 9;
        const int TileBufferLength = 8192;
        const int FilterScale = 128;
        int sampleScale = 1 << (bitDepth - 8);
        int maximumSample = (1 << bitDepth) - 1;
        Av1InterpolationFilter horizontalFilter = reverseFilters ? Av1InterpolationFilter.Sharp : Av1InterpolationFilter.Smooth;
        Av1InterpolationFilter verticalFilter = reverseFilters ? Av1InterpolationFilter.Smooth : Av1InterpolationFilter.Sharp;
        ReadOnlySpan<int> referencePeriod = [0, 28, 40, 28, 0, -28, -40, -12];

        // These Q7 sums are the fixed half-sample responses of the periodic reference to libaom's eight-tap
        // kernels. The source is separable: 128 + horizontal period + vertical period. Each horizontal sum
        // is divisible by the first-pass rounding unit (including the five-bit shift at 12 bits), so the
        // two-axis result is the sum of these responses with one final Q7 rounding, not two rounded pixels.
        // The asymmetric final phase separates sharp from regular after eight-bit error normalization. A low
        // quantizer makes retaining the exact two-axis predictor preferable to saving a filter symbol.
        ReadOnlySpan<int> smoothResponse = [1872, 3952, 3984, 1648, -1680, -3760, -3152, -816];
        ReadOnlySpan<int> sharpResponse = [1536, 4896, 4640, 1856, -1728, -5088, -3424, -640];
        ReadOnlySpan<int> horizontalResponse = reverseFilters ? sharpResponse : smoothResponse;
        ReadOnlySpan<int> verticalResponse = reverseFilters ? smoothResponse : sharpResponse;
        TestMemoryAllocator allocator = new();
        allocator.EnableNonThreadSafeLogging();
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = allocator;
        ObuColorConfig colorConfig = new()
        {
            IsMonochrome = true,
            ColorRange = true,
            SubSamplingX = true,
            SubSamplingY = true,
            BitDepth = (Av1BitDepth)((bitDepth - 8) / 2)
        };

        using Image<L16> referenceImage = new(Width, Height);
        using Av1EncoderFrameBuffer<ushort> reference = new(configuration, Width, Height, bitDepth, Av1ColorFormat.Yuv400, 0, 0, lumaBorder: 64);
        using Av1EncoderFrameBuffer<ushort> source = new(configuration, Width, Height, bitDepth, Av1ColorFormat.Yuv400, 0, 0, lumaBorder: 64);
        using Av1EncoderFrameBuffer<ushort> reconstruction = new(configuration, Width, Height, bitDepth, Av1ColorFormat.Yuv400, 0, 0, lumaBorder: 64);
        for (int y = 0; y < Height; y++)
        {
            Span<L16> pixels = referenceImage.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y);
            Span<ushort> referenceRow = reference.Frame.CodedView.GetPlane(Av1Plane.Y).DangerousGetRowSpan(y);
            Span<ushort> sourceRow = source.Frame.CodedView.GetPlane(Av1Plane.Y).DangerousGetRowSpan(y);
            for (int x = 0; x < Width; x++)
            {
                int sample = (128 + referencePeriod[x % BlockSize] + referencePeriod[y % BlockSize]) * sampleScale;
                referenceRow[x] = (ushort)sample;

                // Map native samples to L16's complete range. The lossless key-frame comparison below proves
                // that the public pixel conversion recovers every original 10/12-bit reference sample.
                pixels[x] = new L16((ushort)(((sample * ushort.MaxValue) + (maximumSample / 2)) / maximumSample));
                int response = (128 * FilterScale) + horizontalResponse[x % BlockSize] + verticalResponse[y % BlockSize];
                sourceRow[x] = (ushort)(((response * sampleScale) + (FilterScale / 2)) / FilterScale);
            }
        }

        reference.Frame.ExtendBorders();
        source.Frame.ExtendBorders();
        ClearPlane(reconstruction.Luma);
        using MemoryStream firstSample = new();
        using Av1FrameEncoder.SequenceEncoder keyEncoder = Av1FrameEncoder.CreateColorSequenceEncoder(
            configuration,
            Width,
            Height,
            colorConfig,
            qIndex: 0,
            Effort,
            speed: HeifEncodingSpeed.Level0);

        keyEncoder.EncodeKeyFrame(referenceImage.Frames.RootFrame, firstSample);
        ObuSequenceHeader sequenceHeader = keyEncoder.SequenceHeader;
        using Av1EncoderModeInfoBuffer modeInfo = new(configuration, Width, Height, disallow4x4AllFrames: true);
        Av1PictureControlSet template = CreatePicture(modeInfo, colorConfig, use128x128Superblock: false, QIndex);
        ObuFrameHeader frameHeader = template.Parent.FrameHeader;
        frameHeader.FrameType = ObuFrameType.InterFrame;
        frameHeader.ShowFrame = true;
        frameHeader.ErrorResilientMode = true;
        frameHeader.RefreshFrameFlags = byte.MaxValue;
        frameHeader.DisableFrameEndUpdateCdf = true;
        frameHeader.ReferenceMode = ObuReferenceMode.SingleReference;
        frameHeader.InterpolationFilter = Av1InterpolationFilter.Switchable;
        frameHeader.AllowHighPrecisionMotionVector = true;
        frameHeader.TransformMode = Av1TransformMode.Select;
        frameHeader.FrameSize.FrameWidth = Width;
        frameHeader.FrameSize.FrameHeight = Height;
        frameHeader.FrameSize.SuperResolutionUpscaledWidth = Width;
        frameHeader.FrameSize.RenderWidth = Width;
        frameHeader.FrameSize.RenderHeight = Height;
        frameHeader.TilesInfo.HasUniformTileSpacing = true;
        Av1QuantizationLookup.UpdateFrameQuantizationState(frameHeader);

        using Av1EncoderPictureBuffer picture = new(configuration, sequenceHeader, frameHeader, Width, Height, disallow4x4AllFrames: true);
        using Av1EncoderCoefficientBuffer coefficients = new(configuration, sequenceHeader, Width, Height);
        using Av1EncoderSuperblockWorkspace superblockWorkspace = new(configuration);
        using Av1EncoderBlockWorkspace blockWorkspace = new(configuration, allocateInterMotionCosts: true);
        using Av1SymbolEncoder symbolEncoder = new(configuration, TileBufferLength, QIndex, updateCdf: true);
        Av1EncoderTileWorkspace tileWorkspace = new(frameHeader, superblockWorkspace);
        int allocationCount = allocator.AllocationLog.Count;
        Av1TileEncoder tileWriter = new(
            symbolEncoder, source.Frame, reference.Frame, reconstruction.Frame, picture.Picture, coefficients, tileWorkspace, blockWorkspace, Effort);

        Assert.Equal(allocationCount, allocator.AllocationLog.Count);

        // This block is at least three reference taps from every frame edge. It must retain two genuinely
        // fractional axes, not a zero-phase filter alias.
        Point targetPosition = new(TargetColumn >> Av1Constants.ModeInfoSizeLog2, TargetRow >> Av1Constants.ModeInfoSizeLog2);
        ref Av1MacroBlockModeInfo targetMode = ref picture.Picture.GetMacroBlockModeInfo(targetPosition);
        Assert.Equal(Av1ReferenceFrameType.Last, targetMode.Block.ReferenceFrame);
        Assert.Equal(horizontalFilter, targetMode.Block.HorizontalInterpolationFilter);
        Assert.Equal(verticalFilter, targetMode.Block.VerticalInterpolationFilter);
        Assert.Equal(4, picture.Picture.GetDisplacementVector(targetPosition).Column);
        Assert.Equal(4, picture.Picture.GetDisplacementVector(targetPosition).Row);
        for (int y = TargetRow; y < TargetRow + BlockSize; y++)
        {
            Assert.Equal(
                source.Frame.View.GetPlane(Av1Plane.Y).DangerousGetRowSpan(y).Slice(TargetColumn, BlockSize),
                reconstruction.Frame.View.GetPlane(Av1Plane.Y).DangerousGetRowSpan(y).Slice(TargetColumn, BlockSize));
        }

        using MemoryStream secondSample = new();
        using ObuWriter obuWriter = new(configuration);
        obuWriter.WriteFrame(secondSample, sequenceHeader, frameHeader, tileWriter);
        string outputDirectory = TestEnvironment.CreateOutputDirectory("Heif", "Av1", nameof(this.ProductionTileSelectsDualAxisInterpolationHighBitDepth));
        string outputName = $"{bitDepth}-{horizontalFilter}-{verticalFilter}";
        using FileStream output = File.Create(Path.Combine(outputDirectory, outputName + ".obu"));
        firstSample.Position = 0;
        firstSample.CopyTo(output);
        secondSample.Position = 0;
        secondSample.CopyTo(output);
        using BinaryWriter rawOutput = new(File.Create(Path.Combine(outputDirectory, outputName + ".managed.yuv")));
        using Av1Decoder decoder = new(configuration);
        for (int frameIndex = 0; frameIndex < 2; frameIndex++)
        {
            // Consume native retained planes before the next sample can replace them. BinaryWriter emits explicit
            // little-endian UInt16 samples, matching the raw reference-decoder output independently of host byte order.
            decoder.DecodeSequenceReference((frameIndex == 0 ? firstSample : secondSample).ToArray(), null, null);
            Av1FrameBuffer<byte> decoded = Assert.IsType<Av1FrameBuffer<byte>>(decoder.FrameBuffer);
            Buffer2DRegion<ushort> expected = (frameIndex == 0 ? reference : reconstruction).Frame.View.GetPlane(Av1Plane.Y);
            for (int y = 0; y < Height; y++)
            {
                ReadOnlySpan<ushort> actualRow = decoded.GetHighBitDepthRowSpan(Av1Plane.Y, y, 0, 0);
                Assert.Equal(expected.DangerousGetRowSpan(y), actualRow);
                foreach (ushort sample in actualRow)
                {
                    rawOutput.Write(sample);
                }
            }
        }
    }

    /// <summary>
    /// Gets the normative eight-sample weights used to build independent smooth-mode fixtures.
    /// </summary>
    private static ReadOnlySpan<int> Smooth8Weights => [255, 197, 146, 105, 73, 50, 37, 32];

    [Fact]
    public void EncodesClipped128SuperblockInWriterPreorderWithoutAllocation()
    {
        const int Width = 16;
        const int Height = 16;
        ObuColorConfig colorConfig = new()
        {
            IsMonochrome = false,
            SubSamplingX = true,
            SubSamplingY = true,
            BitDepth = Av1BitDepth.EightBit
        };

        using Av1EncoderFrameBuffer<byte> source = new(
            Configuration.Default,
            Width,
            Height,
            8,
            Av1ColorFormat.Yuv420,
            1,
            1,
            lumaBorder: 64);

        using Av1EncoderFrameBuffer<byte> reconstruction = new(
            Configuration.Default,
            Width,
            Height,
            8,
            Av1ColorFormat.Yuv420,
            1,
            1,
            lumaBorder: 64);

        FillPlane(source.Frame.CodedView.GetPlane(Av1Plane.Y), (byte)128);
        FillPlane(source.Frame.CodedView.GetPlane(Av1Plane.U), (byte)128);
        FillPlane(source.Frame.CodedView.GetPlane(Av1Plane.V), (byte)128);
        ClearPlane(reconstruction.Luma);
        ClearPlane(Assert.IsType<Buffer2D<byte>>(reconstruction.ChromaBlue));
        ClearPlane(Assert.IsType<Buffer2D<byte>>(reconstruction.ChromaRed));

        using Av1EncoderModeInfoBuffer modeInfo = new(Configuration.Default, Width, Height, disallow4x4AllFrames: true);
        Av1PictureControlSet picture = CreatePicture(modeInfo, colorConfig, use128x128Superblock: true, qIndex: 73);
        picture.Sequence.SequenceHeader.EnableFilterIntra = true;
        using Av1EncoderCoefficientBuffer coefficients = new(
            Configuration.Default,
            picture.Sequence.SequenceHeader,
            Width,
            Height);

        using Av1EncoderSuperblockWorkspace superblockWorkspace = new(Configuration.Default);
        using Av1EncoderBlockWorkspace blockWorkspace = new(Configuration.Default);
        Av1Superblock superblock = new()
        {
            Workspace = superblockWorkspace,
            TileInfo = new Av1TileInfo(0, 0, picture.Parent.FrameHeader),
            Index = 0
        };

        Av1IntraSuperblockEncoder.Encode(
            source.Frame,
            reconstruction.Frame,
            picture,
            superblock,
            coefficients,
            blockWorkspace);

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int iteration = 0; iteration < 8; iteration++)
        {
            Av1IntraSuperblockEncoder.Encode(
                source.Frame,
                reconstruction.Frame,
                picture,
                superblock,
                coefficients,
                blockWorkspace);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Av1PartitionType[] expectedPartitions =
        [
            Av1PartitionType.Split,
            Av1PartitionType.Split,
            Av1PartitionType.Split,
            Av1PartitionType.Split,
            Av1PartitionType.None,
            Av1PartitionType.None,
            Av1PartitionType.None,
            Av1PartitionType.None
        ];

        for (int index = 0; index < expectedPartitions.Length; index++)
        {
            Assert.Equal(expectedPartitions[index], (Av1PartitionType)superblock.CodingUnitPartitionTypes[index]);
        }

        for (int index = 0; index < 4; index++)
        {
            Assert.True(superblock.FinalBlocks[index].HasChroma);
            Assert.Equal(73, superblock.FinalBlocks[index].QuantizationIndex);
            Assert.Equal(Av1FilterIntraMode.AllFilterIntraModes, superblock.FinalBlocks[index].FilterIntraMode);
        }

        Point[] modeInfoPositions = [new(0, 0), new(2, 0), new(0, 2), new(2, 2)];
        foreach (Point position in modeInfoPositions)
        {
            ref Av1MacroBlockModeInfo block = ref picture.GetMacroBlockModeInfo(position);
            Assert.Equal(Av1BlockSize.Block8x8, block.Block.BlockSize);
            Assert.Equal(Av1TransformSize.Size8x8, block.Block.TransformSize);
            Assert.Equal(Av1PredictionMode.DC, block.Block.Mode);
            Assert.Equal(Av1ChromaPredictionMode.DC, block.Block.UvMode);
            Assert.False(block.Block.Skip);
        }

        Span<Av1EncoderTransformBlockState> lumaStates = coefficients.GetTransformBlockSpan(0, Av1Plane.Y);
        Span<Av1EncoderTransformBlockState> blueStates = coefficients.GetTransformBlockSpan(0, Av1Plane.U);
        Span<Av1EncoderTransformBlockState> redStates = coefficients.GetTransformBlockSpan(0, Av1Plane.V);
        int[] lumaStateIndices = [0, 4, 8, 12];
        for (int index = 0; index < 4; index++)
        {
            Assert.Equal((ushort)0, lumaStates[lumaStateIndices[index]].EndOfBlock);
            Assert.Equal(Av1TransformType.DctDct, lumaStates[lumaStateIndices[index]].TransformType);
            Assert.Equal((ushort)0, blueStates[index].EndOfBlock);
            Assert.Equal((ushort)0, redStates[index].EndOfBlock);
        }

        AssertContainsNonzero(reconstruction.Frame.CodedView.GetPlane(Av1Plane.Y));
        AssertContainsNonzero(reconstruction.Frame.CodedView.GetPlane(Av1Plane.U));
        AssertContainsNonzero(reconstruction.Frame.CodedView.GetPlane(Av1Plane.V));

        // Edge contexts cover the complete 128x128 superblock because partition updates retain the coded geometry
        // even when most of the superblock lies beyond this deliberately clipped frame.
        const int ContextUnitCount = 128 >> Av1Constants.ModeInfoSizeLog2;
        using Av1NeighborArrayUnit<Av1PartitionContext> partitions = new(
            Configuration.Default,
            ContextUnitCount,
            ContextUnitCount)
        {
            GranularityNormalLog2 = Av1Constants.ModeInfoSizeLog2
        };

        using Av1NeighborArrayUnit<byte> lumaContexts = new(
            Configuration.Default,
            ContextUnitCount,
            ContextUnitCount)
        {
            GranularityNormalLog2 = Av1Constants.ModeInfoSizeLog2
        };

        using Av1NeighborArrayUnit<byte> blueContexts = new(
            Configuration.Default,
            ContextUnitCount,
            ContextUnitCount)
        {
            GranularityNormalLog2 = Av1Constants.ModeInfoSizeLog2
        };

        using Av1NeighborArrayUnit<byte> redContexts = new(
            Configuration.Default,
            ContextUnitCount,
            ContextUnitCount)
        {
            GranularityNormalLog2 = Av1Constants.ModeInfoSizeLog2
        };

        using Av1NeighborArrayUnit<byte> transformContexts = new(
            Configuration.Default,
            ContextUnitCount,
            ContextUnitCount)
        {
            GranularityNormalLog2 = Av1Constants.ModeInfoSizeLog2
        };

        picture.PartitionContexts = [partitions];
        picture.LuminanceDcSignLevelCoefficientNeighbors = [lumaContexts];
        picture.CbDcSignLevelCoefficientNeighbors = [blueContexts];
        picture.CrDcSignLevelCoefficientNeighbors = [redContexts];
        picture.TransformFunctionContexts = [transformContexts];
        Av1TileWriter.Av1EntropyCodingContext entropyContext = new()
        {
            MacroBlock = new Av1MacroBlockD { Tile = superblock.TileInfo },
            MacroBlockModeInfo = picture.GetMacroBlockModeInfo(default),
            SuperblockOrigin = default
        };

        using Av1SymbolEncoder writer = new(Configuration.Default, 512, 73, updateCdf: true);
        Av1TileWriter.WriteSuperblock(
            picture,
            entropyContext,
            writer,
            superblock,
            coefficients,
            tileIndex: 0);

        using IMemoryOwner<byte> encoded = writer.Exit();

        // The writer must consume exactly the transform areas populated above, proving both traversals stay synchronized.
        Assert.Equal(256, entropyContext.CodedAreaSuperblock);
        Assert.Equal(64, entropyContext.CodedAreaSuperblockUv);
        Assert.NotEqual(0, encoded.GetSpan().Length);

        using Av1EncoderFrameBuffer<byte> tileReconstruction = new(
            Configuration.Default,
            Width,
            Height,
            8,
            Av1ColorFormat.Yuv420,
            1,
            1,
            lumaBorder: 64);

        ClearPlane(tileReconstruction.Luma);
        ClearPlane(Assert.IsType<Buffer2D<byte>>(tileReconstruction.ChromaBlue));
        ClearPlane(Assert.IsType<Buffer2D<byte>>(tileReconstruction.ChromaRed));
        using Av1EncoderPictureBuffer tilePicture = new(
            Configuration.Default,
            picture.Sequence.SequenceHeader,
            picture.Parent.FrameHeader,
            Width,
            Height,
            disallow4x4AllFrames: true);

        using Av1EncoderCoefficientBuffer tileCoefficients = new(
            Configuration.Default,
            picture.Sequence.SequenceHeader,
            Width,
            Height);

        using Av1EncoderSuperblockWorkspace tileSuperblockWorkspace = new(Configuration.Default);
        using Av1EncoderBlockWorkspace tileBlockWorkspace = new(Configuration.Default);
        using Av1SymbolEncoder tileSymbolEncoder = CreateTileSymbolEncoder(
            tilePicture.Picture,
            512);

        Av1TileEncoder tileWriter = new(
            tileSymbolEncoder,
            source.Frame,
            tileReconstruction.Frame,
            tilePicture.Picture,
            tileCoefficients,
            tileSuperblockWorkspace,
            tileBlockWorkspace,
            effort: 5);

        // The production tile traversal must be byte-identical to the explicit analyze-then-write composition above.
        Assert.True(encoded.GetSpan().SequenceEqual(tileWriter.GetTileData(0)));
    }

    [Theory]
    [InlineData(8)]
    [InlineData(10)]
    [InlineData(12)]
    public void InterBlockCanSkipNonzeroQuantizedResiduals(int bitDepthValue)
    {
        if (bitDepthValue == 8)
        {
            VerifyReferenceBlockCanSkipNonzeroQuantizedResiduals<byte, Av1IntraSuperblockEncoder.ByteOperator>(
                Av1BitDepth.EightBit,
                bitDepthValue,
                isIntraBlockCopy: false,
                static value => (byte)value);
        }
        else
        {
            VerifyReferenceBlockCanSkipNonzeroQuantizedResiduals<ushort, Av1IntraSuperblockEncoder.UInt16Operator>(
                bitDepthValue == 10 ? Av1BitDepth.TenBit : Av1BitDepth.TwelveBit,
                bitDepthValue,
                isIntraBlockCopy: false,
                static value => (ushort)value);
        }
    }

    [Theory]
    [InlineData(8)]
    [InlineData(10)]
    [InlineData(12)]
    public void IntraBlockCopyCanSkipNonzeroQuantizedResiduals(int bitDepthValue)
    {
        if (bitDepthValue == 8)
        {
            VerifyReferenceBlockCanSkipNonzeroQuantizedResiduals<byte, Av1IntraSuperblockEncoder.ByteOperator>(
                Av1BitDepth.EightBit,
                bitDepthValue,
                isIntraBlockCopy: true,
                static value => (byte)value);
        }
        else
        {
            VerifyReferenceBlockCanSkipNonzeroQuantizedResiduals<ushort, Av1IntraSuperblockEncoder.UInt16Operator>(
                bitDepthValue == 10 ? Av1BitDepth.TenBit : Av1BitDepth.TwelveBit,
                bitDepthValue,
                isIntraBlockCopy: true,
                static value => (ushort)value);
        }
    }

    private static void VerifyReferenceBlockCanSkipNonzeroQuantizedResiduals<TSample, TOperator>(
        Av1BitDepth bitDepth,
        int bitDepthValue,
        bool isIntraBlockCopy,
        SampleFactory<TSample> createSample)
        where TSample : unmanaged
        where TOperator : struct, Av1IntraSuperblockEncoder.IBlockEncodingOperator<TSample>
    {
        const int Width = 8;
        const int Height = 8;
        Point blockOrigin = new(isIntraBlockCopy ? 320 : 0, 0);
        Point modeInfoPosition = blockOrigin >> Av1Constants.ModeInfoSizeLog2;
        int frameWidth = blockOrigin.X + Width;
        int superblockIndex = blockOrigin.X / 64;
        ObuColorConfig colorConfig = new()
        {
            IsMonochrome = true,
            SubSamplingX = true,
            SubSamplingY = true,
            BitDepth = bitDepth
        };

        using Av1EncoderFrameBuffer<TSample> source = new(
            Configuration.Default,
            frameWidth,
            Height,
            bitDepthValue,
            Av1ColorFormat.Yuv400,
            0,
            0,
            lumaBorder: 64);

        using Av1EncoderFrameBuffer<TSample> reference = new(
            Configuration.Default,
            frameWidth,
            Height,
            bitDepthValue,
            Av1ColorFormat.Yuv400,
            0,
            0,
            lumaBorder: 64);

        using Av1EncoderFrameBuffer<TSample> reconstruction = new(
            Configuration.Default,
            frameWidth,
            Height,
            bitDepthValue,
            Av1ColorFormat.Yuv400,
            0,
            0,
            lumaBorder: 64);

        TSample[] prediction = new TSample[Width * Height];
        short[] residual = new short[Width * Height];
        TSample[] trialReconstruction = new TSample[Width * Height];
        int[] trialCoefficients = new int[Width * Height];
        int verifiedCases = 0;
        for (int qIndex = 64; qIndex <= 192; qIndex += 32)
        {
            for (int amplitude = 2; amplitude <= 12; amplitude += 2)
            {
                // Independent texture and small, signed perturbations distinguish temporal prediction from spatial DC.
                // Samples remain inside the coded range. Unaligned high-depth perturbations exercise SSE rounding.
                long squaredError = 0;
                for (int y = 0; y < Height; y++)
                {
                    Span<TSample> sourceRow = source.Frame.CodedView.GetPlane(Av1Plane.Y).DangerousGetRowSpan(y);
                    Span<TSample> referenceRow = reference.Frame.CodedView.GetPlane(Av1Plane.Y).DangerousGetRowSpan(y);
                    if (isIntraBlockCopy)
                    {
                        // Completed blocks provide a repeated reconstructed reference before the current superblock.
                        // The target differs from it, so pixel search must supply the candidate without an exact hash match.
                        Span<TSample> reconstructionRow = reconstruction.Frame.CodedView.GetPlane(Av1Plane.Y).DangerousGetRowSpan(y);
                        for (int x = 0; x < frameWidth; x++)
                        {
                            int phase = x % Width;
                            int sample = 64 + (((phase * 37) + (y * 53) + (phase * y * 19)) % 128);
                            sourceRow[x] = reconstructionRow[x] = createSample(sample << (bitDepthValue - 8));
                        }
                    }

                    for (int x = 0; x < Width; x++)
                    {
                        int index = (y * Width) + x;
                        int sample = 64 + (((x * 37) + (y * 53) + (x * y * 19)) % 128);
                        int difference = (((x * 13) + (y * 7) + (x * y * 3)) % ((amplitude * 2) + 1)) - amplitude;
                        int precisionShift = bitDepthValue - 8;
                        sample <<= precisionShift;
                        difference = (difference << precisionShift) + (precisionShift > 0 && index % 3 == 0 ? 1 : 0);
                        prediction[index] = referenceRow[x] = createSample(sample);
                        sourceRow[blockOrigin.X + x] = createSample(sample + difference);
                        residual[index] = (short)difference;
                        squaredError += difference * difference;
                    }
                }

                source.Frame.ExtendBorders();
                reference.Frame.ExtendBorders();
                using Av1EncoderModeInfoBuffer modeInfoBuffer = new(Configuration.Default, frameWidth, Height, disallow4x4AllFrames: true);
                Av1PictureControlSet template = CreatePicture(modeInfoBuffer, colorConfig, use128x128Superblock: false, qIndex);
                template.Parent.FrameHeader.FrameType = isIntraBlockCopy ? ObuFrameType.KeyFrame : ObuFrameType.InterFrame;
                template.Parent.FrameHeader.AllowIntraBlockCopy = isIntraBlockCopy;
                template.Parent.FrameHeader.AllowScreenContentTools = isIntraBlockCopy;
                template.Parent.FrameHeader.FrameSize.FrameWidth = frameWidth;
                template.Parent.FrameHeader.FrameSize.FrameHeight = Height;
                template.Parent.FrameHeader.TransformMode = Av1TransformMode.Largest;
                template.Parent.FrameHeader.InterpolationFilter = Av1InterpolationFilter.Regular;
                template.Parent.FrameHeader.ReferenceMode = ObuReferenceMode.SingleReference;
                using Av1EncoderPictureBuffer pictureBuffer = new(
                    Configuration.Default,
                    template.Sequence.SequenceHeader,
                    template.Parent.FrameHeader,
                    frameWidth,
                    Height,
                    disallow4x4AllFrames: true);

                Av1PictureControlSet picture = pictureBuffer.Picture;
                if (isIntraBlockCopy)
                {
                    picture.IntraBlockCopySearch.Initialize<TSample, TOperator>(source.Frame.View.GetPlane(Av1Plane.Y));
                }

                using Av1EncoderCoefficientBuffer coefficients = new(Configuration.Default, template.Sequence.SequenceHeader, frameWidth, Height);
                using Av1EncoderSuperblockWorkspace superblockWorkspace = new(Configuration.Default);
                using Av1EncoderBlockWorkspace blockWorkspace = new(Configuration.Default, allocateInterMotionCosts: !isIntraBlockCopy);
                using Av1SymbolEncoder writer = new(Configuration.Default, 4096, qIndex, updateCdf: true);
                if (!isIntraBlockCopy)
                {
                    writer.FillMotionVectorCosts(blockWorkspace.GetMotionVectorCosts(picture.Parent.FrameHeader.MotionVectorPrecision));
                }

                Av1Superblock superblock = new()
                {
                    Workspace = superblockWorkspace,
                    TileInfo = new Av1TileInfo(0, 0, picture.Parent.FrameHeader),
                    Index = superblockIndex
                };

                Av1IntraSuperblockEncoder.Prepare(picture, superblock, blockOrigin);
                picture.MapModeInfoBlock(modeInfoPosition, Av1BlockSize.Block8x8);
                Av1MacroBlockD macroBlock = new() { Tile = superblock.TileInfo };
                Av1TileWriter.SetModeInfoRowAndColumn(
                    picture,
                    macroBlock,
                    superblock.TileInfo,
                    modeInfoPosition,
                    Av1BlockSize.Block8x8,
                    picture.Parent.Common.ModeInfoStride,
                    picture.Parent.Common.ModeInfoRowCount,
                    picture.Parent.Common.ModeInfoColumnCount);

                if (!isIntraBlockCopy)
                {
                    picture.Parent.MotionSearchSettings = new Av1MotionSearchSettings(
                        HeifEncodingSpeed.Level0, false, new Size(frameWidth, Height), qIndex, false, false);

                    picture.Parent.MotionSearchStepParameter = Av1MotionSearchBase.GetInitialStepParameter(Math.Max(frameWidth, Height));

                    ref Av1ReferenceMotionVectors references = ref blockWorkspace.ReferenceMotionVectors;
                    references.Build(
                        picture,
                        macroBlock,
                        modeInfoPosition,
                        Av1BlockSize.Block8x8,
                        Av1PartitionType.None,
                        template.Sequence.SequenceHeader,
                        picture.Parent.FrameHeader,
                        Av1ReferenceFrameType.Last);

                    // Thirty-two preceding global-motion symbols make the zero global predictor the cheapest
                    // mode. Keep every competing mode enabled so this fixture tests residual skipping independently
                    // of mode-search restrictions, while checking exact syntax rates, distortion, and reconstruction.
                    for (int index = 0; index < 32; index++)
                    {
                        writer.WriteInterMode<Av1SymbolEncoder.SymbolUpdateOperation>(Av1PredictionMode.GlobalMotionVector, references.ModeContext);
                    }

                    int globalRate = writer.GetInterModeCost(Av1PredictionMode.GlobalMotionVector, references.ModeContext);
                    Assert.True(globalRate < writer.GetInterModeCost(Av1PredictionMode.NearestMotionVector, references.ModeContext));
                    Assert.True(globalRate < writer.GetInterModeCost(Av1PredictionMode.NearMotionVector, references.ModeContext));
                    Assert.True(globalRate < writer.GetInterModeCost(Av1PredictionMode.NewMotionVector, references.ModeContext));
                }

                int multiplier = isIntraBlockCopy
                    ? Av1RateDistortion.GetKeyFrameRateMultiplier(qIndex, bitDepth)
                    : Av1RateDistortion.GetInterFrameRateMultiplier(qIndex, bitDepth);
                int skipContext = Av1TileWriter.GetSkipContext(macroBlock);
                int skipRate = writer.GetSkipCost(true, skipContext);
                int squaredPrecisionScale = 1 << ((bitDepthValue - 8) * 2);
                long expectedDistortion = ((squaredError + (squaredPrecisionScale / 2)) / squaredPrecisionScale) * 16;
                long skipCost = ((((long)skipRate * multiplier) + 256) / 512) + (expectedDistortion * 128);
                long bestCodedCost = long.MaxValue;
                bool allTransformsAreNonzero = true;
                Av1TransformSetType transformSet = Av1SymbolContextHelper.GetExtendedTransformSetType(
                    Av1TransformSize.Size8x8,
                    isInter: true,
                    picture.Parent.FrameHeader.UseReducedTransformSet);

                // Evaluate residual coding independently of the block decision. Qualifying cases must have
                // nonzero coefficients in every legal transform, yet cost more than prediction-only reconstruction.
                for (Av1TransformType transformType = Av1TransformType.DctDct;
                    transformType < Av1TransformType.AllTransformTypes;
                    transformType++)
                {
                    if (!transformType.IsExtendedSetUsed(transformSet))
                    {
                        continue;
                    }

                    Av1EncoderTransformBlockState state = default;
                    long distortion = TOperator.EncodePredictionCandidate(
                        blockWorkspace,
                        source.Frame.CodedView.GetPlane(Av1Plane.Y),
                        blockOrigin,
                        prediction,
                        residual,
                        trialReconstruction,
                        Width,
                        trialCoefficients,
                        Av1TransformSize.Size8x8,
                        transformType,
                        Av1Plane.Y,
                        qIndex,
                        0,
                        0,
                        bitDepth,
                        ref state);

                    allTransformsAreNonzero &= state.EndOfBlock != 0;
                    int rate = writer.GetSkipCost(false, skipContext) + writer.GetCoefficientCost(
                        Av1TransformSize.Size8x8,
                        transformType,
                        isIntraBlockCopy ? Av1PredictionMode.DC : Av1PredictionMode.GlobalMotionVector,
                        trialCoefficients,
                        Av1ComponentType.Luminance,
                        default,
                        state.EndOfBlock,
                        picture.Parent.FrameHeader.UseReducedTransformSet,
                        Av1FilterIntraMode.AllFilterIntraModes,
                        usesInterTransformSet: true);

                    long cost = ((((long)rate * multiplier) + 256) / 512) + (distortion * 128);
                    bestCodedCost = Math.Min(bestCodedCost, cost);
                }

                if (!allTransformsAreNonzero || skipCost > bestCodedCost)
                {
                    continue;
                }

                Av1IntraSuperblockEncoder.ModeDecision<TSample, TOperator> decision = new(
                    source.Frame,
                    reference.Frame,
                    reconstruction.Frame,
                    picture,
                    superblock,
                    coefficients,
                    blockWorkspace,
                    effort: 0);

                ref Av1MacroBlockModeInfo modeInfo = ref picture.GetMacroBlockModeInfo(modeInfoPosition);
                Av1EncoderBlockStruct block = default;
                Av1EncoderPaletteInfo palette = default;
                Av1MotionVector displacementReference = default;
                if (isIntraBlockCopy)
                {
                    displacementReference = Av1IntraBlockCopy.FindReference(
                        picture,
                        macroBlock,
                        modeInfoPosition,
                        Av1BlockSize.Block8x8,
                        Av1PartitionType.None,
                        new Av1MotionVector[8],
                        new int[8]);
                }

                decision.EncodeBlock(writer, macroBlock, blockOrigin, 0, ref modeInfo, ref block, ref palette);
                Assert.Equal(isIntraBlockCopy, modeInfo.Block.UseIntraBlockCopy);
                if (!isIntraBlockCopy)
                {
                    Assert.Equal(Av1ReferenceFrameType.Last, modeInfo.Block.ReferenceFrame);
                }

                Assert.True(modeInfo.Block.Skip, $"qIndex={qIndex}, amplitude={amplitude}, skipCost={skipCost}, codedCost={bestCodedCost}");
                Assert.Equal(isIntraBlockCopy ? Av1PredictionMode.DC : Av1PredictionMode.GlobalMotionVector, modeInfo.Block.Mode);
                Assert.Equal(expectedDistortion, decision.SelectedBlockStatistics.Distortion);
                int expectedRate = skipRate + (isIntraBlockCopy
                    ? writer.GetUseIntraBlockCopyCost(true) +
                      writer.GetDisplacementVectorCost(picture.GetDisplacementVector(modeInfoPosition), displacementReference)
                    : writer.GetIsInterCost(true, Av1TileWriter.GetIntraInterContext(macroBlock)) +
                      writer.GetSingleReferenceCost(Av1ReferenceFrameType.Last, new byte[Av1Constants.ReferenceFrameCount]) +
                      writer.GetInterModeCost(Av1PredictionMode.GlobalMotionVector, blockWorkspace.ReferenceMotionVectors.ModeContext));

                Assert.Equal(expectedRate, decision.SelectedBlockStatistics.Rate);
                Assert.Equal(
                    ((((long)expectedRate * multiplier) + 256) / 512) + (expectedDistortion * 128),
                    decision.SelectedBlockStatistics.Cost);
                Assert.Equal((ushort)0, coefficients.GetTransformBlockSpan(superblockIndex, Av1Plane.Y)[0].EndOfBlock);
                Assert.Equal(Av1TransformType.DctDct, coefficients.GetTransformBlockSpan(superblockIndex, Av1Plane.Y)[0].TransformType);
                Assert.Equal((byte)0, coefficients.GetTransformBlockSpan(superblockIndex, Av1Plane.Y)[0].EntropyContext);
                Assert.All(coefficients.GetPlaneSpan(superblockIndex, Av1Plane.Y)[..64].ToArray(), value => Assert.Equal(0, value));
                for (int y = 0; y < Height; y++)
                {
                    Assert.Equal(
                        MemoryMarshal.AsBytes(prediction.AsSpan(y * Width, Width)),
                        MemoryMarshal.AsBytes(reconstruction.Frame.CodedView.GetPlane(Av1Plane.Y).DangerousGetRowSpan(y).Slice(blockOrigin.X, Width)));
                }

                verifiedCases++;
            }
        }

        Assert.True(verifiedCases > 0, "The input set must exercise skipping despite nonzero coefficients in every legal transform.");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PreservesIntraNonSkipForAllZeroTransforms(bool isMonochrome)
    {
        const int Width = 8;
        const int Height = 8;
        Av1ColorFormat colorFormat = isMonochrome ? Av1ColorFormat.Yuv400 : Av1ColorFormat.Yuv420;
        ObuColorConfig colorConfig = new()
        {
            IsMonochrome = isMonochrome,
            SubSamplingX = true,
            SubSamplingY = true,
            BitDepth = Av1BitDepth.EightBit
        };

        using Av1EncoderFrameBuffer<byte> source = new(
            Configuration.Default,
            Width,
            Height,
            8,
            colorFormat,
            1,
            1,
            lumaBorder: 64);

        using Av1EncoderFrameBuffer<byte> reconstruction = new(
            Configuration.Default,
            Width,
            Height,
            8,
            colorFormat,
            1,
            1,
            lumaBorder: 64);

        FillPlane(source.Frame.CodedView.GetPlane(Av1Plane.Y), (byte)128);
        ClearPlane(reconstruction.Luma);
        if (!isMonochrome)
        {
            FillPlane(source.Frame.CodedView.GetPlane(Av1Plane.U), (byte)128);
            FillPlane(source.Frame.CodedView.GetPlane(Av1Plane.V), (byte)128);
            ClearPlane(Assert.IsType<Buffer2D<byte>>(reconstruction.ChromaBlue));
            ClearPlane(Assert.IsType<Buffer2D<byte>>(reconstruction.ChromaRed));
        }

        using Av1EncoderModeInfoBuffer modeInfo = new(Configuration.Default, Width, Height, disallow4x4AllFrames: true);
        Av1PictureControlSet pictureTemplate = CreatePicture(modeInfo, colorConfig, use128x128Superblock: false, qIndex: 37);
        using Av1EncoderPictureBuffer pictureBuffer = new(
            Configuration.Default,
            pictureTemplate.Sequence.SequenceHeader,
            pictureTemplate.Parent.FrameHeader,
            Width,
            Height,
            disallow4x4AllFrames: true);

        Av1PictureControlSet picture = pictureBuffer.Picture;
        using Av1EncoderCoefficientBuffer coefficients = new(
            Configuration.Default,
            pictureTemplate.Sequence.SequenceHeader,
            Width,
            Height);

        using Av1EncoderSuperblockWorkspace superblockWorkspace = new(Configuration.Default);
        using Av1EncoderBlockWorkspace blockWorkspace = new(Configuration.Default);
        Av1Superblock superblock = new()
        {
            Workspace = superblockWorkspace,
            TileInfo = new Av1TileInfo(0, 0, picture.Parent.FrameHeader),
            Index = 0
        };

        Av1IntraSuperblockEncoder.Encode(
            source.Frame,
            reconstruction.Frame,
            picture,
            superblock,
            coefficients,
            blockWorkspace);

        ref Av1MacroBlockModeInfo block = ref picture.GetMacroBlockModeInfo(default);

        // Ordinary intra blocks retain the non-skip flag and empty transform symbols. Libaom applies
        // this policy before final coding even when skipping would reconstruct the same samples.
        Assert.False(block.Block.Skip);
        Assert.Equal((ushort)0, coefficients.GetTransformBlockSpan(0, Av1Plane.Y)[0].EndOfBlock);
        if (!isMonochrome)
        {
            Assert.Equal((ushort)0, coefficients.GetTransformBlockSpan(0, Av1Plane.U)[0].EndOfBlock);
            Assert.Equal((ushort)0, coefficients.GetTransformBlockSpan(0, Av1Plane.V)[0].EndOfBlock);
        }

        Av1TileWriter.Av1EntropyCodingContext entropyContext = new()
        {
            MacroBlock = new Av1MacroBlockD { Tile = superblock.TileInfo },
            MacroBlockModeInfo = picture.GetMacroBlockModeInfo(default),
            SuperblockOrigin = default
        };

        using Av1SymbolEncoder writer = new(Configuration.Default, 256, 37, updateCdf: true);
        Av1TileWriter.WriteSuperblock(
            picture,
            entropyContext,
            writer,
            superblock,
            coefficients,
            tileIndex: 0);

        using IMemoryOwner<byte> precomputedTile = writer.Exit();
        using Av1EncoderFrameBuffer<byte> liveReconstruction = new(
            Configuration.Default,
            Width,
            Height,
            8,
            colorFormat,
            1,
            1,
            lumaBorder: 64);

        ClearPlane(liveReconstruction.Luma);
        if (!isMonochrome)
        {
            ClearPlane(Assert.IsType<Buffer2D<byte>>(liveReconstruction.ChromaBlue));
            ClearPlane(Assert.IsType<Buffer2D<byte>>(liveReconstruction.ChromaRed));
        }

        using Av1EncoderPictureBuffer livePicture = new(
            Configuration.Default,
            pictureTemplate.Sequence.SequenceHeader,
            pictureTemplate.Parent.FrameHeader,
            Width,
            Height,
            disallow4x4AllFrames: true);

        using Av1EncoderCoefficientBuffer liveCoefficients = new(
            Configuration.Default,
            pictureTemplate.Sequence.SequenceHeader,
            Width,
            Height);

        using Av1EncoderSuperblockWorkspace liveSuperblockWorkspace = new(Configuration.Default);
        using Av1EncoderBlockWorkspace liveBlockWorkspace = new(Configuration.Default);
        using Av1SymbolEncoder liveSymbolEncoder = CreateTileSymbolEncoder(
            livePicture.Picture,
            256);

        Av1TileEncoder liveTileWriter = new(
            liveSymbolEncoder,
            source.Frame,
            liveReconstruction.Frame,
            livePicture.Picture,
            liveCoefficients,
            liveSuperblockWorkspace,
            liveBlockWorkspace,
            effort: 5);

        Assert.True(precomputedTile.GetSpan().SequenceEqual(liveTileWriter.GetTileData(0)));
    }

    [Fact]
    public void PreservesTwelveBitMonochromeReconstructionPrecision()
    {
        const int Width = 8;
        const int Height = 8;
        ObuColorConfig colorConfig = new()
        {
            IsMonochrome = true,
            SubSamplingX = true,
            SubSamplingY = true,
            BitDepth = Av1BitDepth.TwelveBit
        };

        using Av1EncoderFrameBuffer<ushort> source = new(
            Configuration.Default,
            Width,
            Height,
            12,
            Av1ColorFormat.Yuv400,
            0,
            0,
            lumaBorder: 64);

        using Av1EncoderFrameBuffer<ushort> reconstruction = new(
            Configuration.Default,
            Width,
            Height,
            12,
            Av1ColorFormat.Yuv400,
            0,
            0,
            lumaBorder: 64);

        Buffer2DRegion<ushort> sourcePlane = source.Frame.CodedView.GetPlane(Av1Plane.Y);
        for (int y = 0; y < sourcePlane.Height; y++)
        {
            Span<ushort> row = sourcePlane.DangerousGetRowSpan(y);
            for (int x = 0; x < row.Length; x++)
            {
                row[x] = (ushort)(3000 + (((x * 71) + (y * 113)) % 1000));
            }
        }

        ClearPlane(reconstruction.Luma);
        using Av1EncoderModeInfoBuffer modeInfo = new(Configuration.Default, Width, Height, disallow4x4AllFrames: true);
        Av1PictureControlSet picture = CreatePicture(modeInfo, colorConfig, use128x128Superblock: false, qIndex: 37);
        using Av1EncoderCoefficientBuffer coefficients = new(
            Configuration.Default,
            picture.Sequence.SequenceHeader,
            Width,
            Height);

        using Av1EncoderSuperblockWorkspace superblockWorkspace = new(Configuration.Default);
        using Av1EncoderBlockWorkspace blockWorkspace = new(Configuration.Default);
        Av1Superblock superblock = new()
        {
            Workspace = superblockWorkspace,
            TileInfo = new Av1TileInfo(0, 0, picture.Parent.FrameHeader),
            Index = 0
        };

        Av1IntraSuperblockEncoder.Encode(
            source.Frame,
            reconstruction.Frame,
            picture,
            superblock,
            coefficients,
            blockWorkspace);

        Buffer2DRegion<ushort> reconstructionPlane = reconstruction.Frame.CodedView.GetPlane(Av1Plane.Y);
        ushort maximum = 0;
        for (int y = 0; y < reconstructionPlane.Height; y++)
        {
            foreach (ushort sample in reconstructionPlane.DangerousGetRowSpan(y))
            {
                maximum = Math.Max(maximum, sample);
                Assert.InRange(sample, (ushort)0, (ushort)4095);
            }
        }

        Assert.InRange(maximum, (ushort)(byte.MaxValue + 1), (ushort)4095);
        Assert.False(superblock.FinalBlocks[0].HasChroma);
        Assert.Equal(37, superblock.FinalBlocks[0].QuantizationIndex);
        Assert.NotEqual((ushort)0, coefficients.GetTransformBlockSpan(0, Av1Plane.Y)[0].EndOfBlock);
        Assert.Equal(0, coefficients.GetPlaneSpan(0, Av1Plane.U).Length);
        Assert.Equal(0, coefficients.GetPlaneSpan(0, Av1Plane.V).Length);

        using Av1EncoderFrameBuffer<ushort> tileReconstruction = new(
            Configuration.Default,
            Width,
            Height,
            12,
            Av1ColorFormat.Yuv400,
            0,
            0,
            lumaBorder: 64);

        ClearPlane(tileReconstruction.Luma);
        using Av1EncoderPictureBuffer tilePicture = new(
            Configuration.Default,
            picture.Sequence.SequenceHeader,
            picture.Parent.FrameHeader,
            Width,
            Height,
            disallow4x4AllFrames: true);

        using Av1EncoderCoefficientBuffer tileCoefficients = new(
            Configuration.Default,
            picture.Sequence.SequenceHeader,
            Width,
            Height);

        using Av1EncoderSuperblockWorkspace tileSuperblockWorkspace = new(Configuration.Default);
        using Av1EncoderBlockWorkspace tileBlockWorkspace = new(Configuration.Default);
        using Av1SymbolEncoder tileSymbolEncoder = CreateTileSymbolEncoder(
            tilePicture.Picture,
            256);

        Av1TileEncoder tileWriter = new(
            tileSymbolEncoder,
            source.Frame,
            tileReconstruction.Frame,
            tilePicture.Picture,
            tileCoefficients,
            tileSuperblockWorkspace,
            tileBlockWorkspace,
            effort: 5);

        Assert.NotEqual(0, tileWriter.GetTileData(0).Length);
        ushort reconstructedSample = tileReconstruction.Frame.CodedView
            .GetPlane(Av1Plane.Y)
            .DangerousGetRowSpan(0)[0];

        Assert.InRange(reconstructedSample, (ushort)(byte.MaxValue + 1), (ushort)4095);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public void BlockDecisionRetainsUnroundedRateAndDistortion(bool isMonochrome, bool textured)
    {
        const int Width = 8;
        const int Height = 8;
        const int QIndex = 37;
        Av1ColorFormat colorFormat = isMonochrome ? Av1ColorFormat.Yuv400 : Av1ColorFormat.Yuv420;
        ObuColorConfig colorConfig = new()
        {
            IsMonochrome = isMonochrome,
            SubSamplingX = true,
            SubSamplingY = true,
            BitDepth = Av1BitDepth.EightBit
        };

        using Av1EncoderFrameBuffer<byte> source = new(Configuration.Default, Width, Height, 8, colorFormat, 1, 1, lumaBorder: 64);
        using Av1EncoderFrameBuffer<byte> reconstruction = new(Configuration.Default, Width, Height, 8, colorFormat, 1, 1, lumaBorder: 64);
        int planeCount = isMonochrome ? 1 : 3;
        for (int planeIndex = 0; planeIndex < planeCount; planeIndex++)
        {
            Buffer2DRegion<byte> plane = source.Frame.CodedView.GetPlane((Av1Plane)planeIndex);
            int length = planeIndex == 0 ? 8 : 4;
            for (int y = 0; y < length; y++)
            {
                Span<byte> row = plane.DangerousGetRowSpan(y);
                for (int x = 0; x < length; x++)
                {
                    row[x] = textured ? (byte)(114 + (((x * 13) + (y * 7) + (planeIndex * 5)) % 29)) : (byte)128;
                }
            }
        }

        using Av1EncoderModeInfoBuffer modeInfoBuffer = new(Configuration.Default, Width, Height, disallow4x4AllFrames: true);
        Av1PictureControlSet template = CreatePicture(modeInfoBuffer, colorConfig, use128x128Superblock: false, QIndex);
        template.Parent.FrameHeader.TransformMode = Av1TransformMode.Largest;
        using Av1EncoderPictureBuffer pictureBuffer = new(
            Configuration.Default,
            template.Sequence.SequenceHeader,
            template.Parent.FrameHeader,
            Width,
            Height,
            disallow4x4AllFrames: true);

        Av1PictureControlSet picture = pictureBuffer.Picture;
        using Av1EncoderCoefficientBuffer coefficients = new(Configuration.Default, template.Sequence.SequenceHeader, Width, Height);
        using Av1EncoderSuperblockWorkspace superblockWorkspace = new(Configuration.Default);
        using Av1EncoderBlockWorkspace blockWorkspace = new(Configuration.Default);
        Av1Superblock superblock = new()
        {
            Workspace = superblockWorkspace,
            TileInfo = new Av1TileInfo(0, 0, picture.Parent.FrameHeader),
            Index = 0
        };

        Av1IntraSuperblockEncoder.Prepare(picture, superblock, Point.Empty);
        Av1MacroBlockD macroBlock = new() { Tile = superblock.TileInfo };
        Av1TileWriter.SetModeInfoRowAndColumn(
            picture,
            macroBlock,
            superblock.TileInfo,
            Point.Empty,
            Av1BlockSize.Block8x8,
            picture.Parent.Common.ModeInfoStride,
            picture.Parent.Common.ModeInfoRowCount,
            picture.Parent.Common.ModeInfoColumnCount);

        Av1IntraSuperblockEncoder.ModeDecision<byte, Av1IntraSuperblockEncoder.ByteOperator> decision = new(
            source.Frame,
            reconstruction.Frame,
            reconstruction.Frame,
            picture,
            superblock,
            coefficients,
            blockWorkspace,
            effort: 0);

        ref Av1MacroBlockModeInfo modeInfo = ref picture.GetMacroBlockModeInfo(Point.Empty);
        Av1EncoderBlockStruct block = default;
        Av1EncoderPaletteInfo palette = default;
        using Av1SymbolEncoder writer = new(Configuration.Default, 256, QIndex, updateCdf: true);
        decision.EncodeBlock(writer, macroBlock, Point.Empty, 0, ref modeInfo, ref block, ref palette);

        Assert.Equal(Av1PredictionMode.DC, modeInfo.Block.Mode);
        Assert.Equal(Av1TransformSize.Size8x8, modeInfo.Block.TransformSize);
        Assert.False(modeInfo.Block.Skip);
        int expectedRate = writer.GetSkipCost(false, Av1TileWriter.GetSkipContext(macroBlock));
        expectedRate += Av1TileWriter.GetLumaModeCost(
            writer,
            macroBlock,
            Av1BlockSize.Block8x8,
            Av1PredictionMode.DC,
            0,
            isIntraFrame: true);

        if (!isMonochrome)
        {
            Assert.Equal(Av1ChromaPredictionMode.DC, modeInfo.Block.UvMode);
            expectedRate += Av1TileWriter.GetChromaModeCost(
                writer,
                picture.Parent.FrameHeader,
                colorConfig,
                modeInfo,
                Av1BlockSize.Block8x8,
                Av1PredictionMode.DC,
                Av1ChromaPredictionMode.DC,
                0);
        }

        long squaredError = 0;
        for (int planeIndex = 0; planeIndex < planeCount; planeIndex++)
        {
            Av1Plane plane = (Av1Plane)planeIndex;
            Av1TransformSize transformSize = planeIndex == 0 ? Av1TransformSize.Size8x8 : Av1TransformSize.Size4x4;
            Av1BlockSize blockSize = planeIndex == 0 ? Av1BlockSize.Block8x8 : Av1BlockSize.Block4x4;
            Av1ComponentType component = planeIndex == 0 ? Av1ComponentType.Luminance : Av1ComponentType.Chroma;
            Av1NeighborArrayUnit<byte> neighbors = planeIndex switch
            {
                0 => picture.LuminanceDcSignLevelCoefficientNeighbors[0],
                1 => picture.CbDcSignLevelCoefficientNeighbors[0],
                _ => picture.CrDcSignLevelCoefficientNeighbors[0]
            };

            Av1EncoderTransformBlockState state = coefficients.GetTransformBlockSpan(0, plane)[0];
            expectedRate += writer.GetCoefficientCost(
                transformSize,
                state.TransformType,
                Av1PredictionMode.DC,
                coefficients.GetPlaneSpan(0, plane)[..transformSize.GetSize2d()],
                component,
                Av1TileWriter.GetTransformBlockContexts(component, neighbors, Point.Empty, blockSize, transformSize),
                state.EndOfBlock,
                picture.Parent.FrameHeader.UseReducedTransformSet,
                Av1FilterIntraMode.AllFilterIntraModes,
                usesInterTransformSet: false);

            Buffer2DRegion<byte> sourcePlane = source.Frame.CodedView.GetPlane(plane);
            Buffer2DRegion<byte> reconstructedPlane = reconstruction.Frame.CodedView.GetPlane(plane);
            int length = planeIndex == 0 ? 8 : 4;
            for (int y = 0; y < length; y++)
            {
                ReadOnlySpan<byte> sourceRow = sourcePlane.DangerousGetRowSpan(y);
                ReadOnlySpan<byte> reconstructedRow = reconstructedPlane.DangerousGetRowSpan(y);
                for (int x = 0; x < length; x++)
                {
                    int difference = sourceRow[x] - reconstructedRow[x];
                    squaredError += difference * difference;
                }
            }
        }

        // Pixel-domain SSE uses the reference's four fractional distortion bits. The probability rate
        // is rounded after all planes and block syntax have been counted, before adding scaled distortion.
        long expectedDistortion = squaredError * 16;
        int multiplier = Av1RateDistortion.GetKeyFrameRateMultiplier(QIndex, Av1BitDepth.EightBit);
        long expectedCost = ((((long)expectedRate * multiplier) + 256) / 512) + (expectedDistortion * 128);
        Assert.Equal(textured, squaredError > 0);
        Assert.Equal(expectedRate, decision.SelectedBlockStatistics.Rate);
        Assert.Equal(expectedDistortion, decision.SelectedBlockStatistics.Distortion);
        Assert.Equal(expectedCost, decision.SelectedBlockStatistics.Cost);
    }

    [Fact]
    public void BlockDecisionObservesLiveCdfInWriterOrder()
    {
        const int Width = 16;
        const int Height = 8;
        const int QIndex = 37;
        ObuColorConfig colorConfig = new()
        {
            IsMonochrome = true,
            SubSamplingX = true,
            SubSamplingY = true,
            BitDepth = Av1BitDepth.EightBit,
        };

        using Av1EncoderModeInfoBuffer modeInfo = new(Configuration.Default, Width, Height, disallow4x4AllFrames: true);
        Av1PictureControlSet pictureTemplate = CreatePicture(
            modeInfo,
            colorConfig,
            use128x128Superblock: false,
            QIndex);

        using Av1EncoderPictureBuffer picture = new(
            Configuration.Default,
            pictureTemplate.Sequence.SequenceHeader,
            pictureTemplate.Parent.FrameHeader,
            Width,
            Height,
            disallow4x4AllFrames: true);

        using Av1EncoderCoefficientBuffer coefficients = new(
            Configuration.Default,
            pictureTemplate.Sequence.SequenceHeader,
            Width,
            Height);

        using Av1EncoderSuperblockWorkspace superblockWorkspace = new(Configuration.Default);
        Av1Superblock superblock = new()
        {
            Workspace = superblockWorkspace,
            TileInfo = new Av1TileInfo(0, 0, picture.Picture.Parent.FrameHeader),
            Index = 0,
        };

        Av1IntraSuperblockEncoder.Prepare(picture.Picture, superblock, Point.Empty);
        Av1TileWriter.Av1EntropyCodingContext entropyContext = new()
        {
            MacroBlock = new Av1MacroBlockD { Tile = superblock.TileInfo },
            MacroBlockModeInfo = picture.Picture.GetMacroBlockModeInfo(default),
            SuperblockOrigin = default,
        };

        int[] costs = new int[2];
        BlockCostRecorder blockEncoder = new(costs, QIndex);
        using Av1SymbolEncoder writer = new(Configuration.Default, 256, QIndex, updateCdf: true);
        Av1TileWriter.WriteSuperblock(
            picture.Picture,
            entropyContext,
            writer,
            superblock,
            coefficients,
            tileIndex: 0,
            ref blockEncoder);

        using IMemoryOwner<byte> encoded = writer.Exit();
        Assert.Equal(2, blockEncoder.Count);
        Assert.True(costs[1] < costs[0]);
        Assert.NotEqual(0, encoded.GetSpan().Length);
    }

    [Fact]
    public void ProductionWriterConsumesPaletteMapAndPublishesPaletteEdges()
    {
        const int Width = 8;
        const int Height = 8;
        const int QIndex = 23;
        ObuColorConfig colorConfig = new()
        {
            IsMonochrome = true,
            SubSamplingX = true,
            SubSamplingY = true,
            BitDepth = Av1BitDepth.EightBit
        };

        ObuTileGroupHeader tiles = new()
        {
            TileColumnCount = 1,
            TileRowCount = 1
        };

        tiles.TileColumnStartModeInfo[1] = Width >> Av1Constants.ModeInfoSizeLog2;
        tiles.TileRowStartModeInfo[1] = Height >> Av1Constants.ModeInfoSizeLog2;
        ObuSequenceHeader sequenceHeader = new()
        {
            ColorConfig = colorConfig
        };

        ObuFrameHeader frameHeader = new()
        {
            AllowScreenContentTools = true,
            ModeInfoColumnCount = Width >> Av1Constants.ModeInfoSizeLog2,
            ModeInfoRowCount = Height >> Av1Constants.ModeInfoSizeLog2,
            FrameSize = new ObuFrameSize
            {
                FrameWidth = Width,
                FrameHeight = Height
            },
            TilesInfo = tiles
        };

        frameHeader.QuantizationParameters.BaseQIndex = QIndex;
        frameHeader.QuantizationParameters.QIndex.Fill(QIndex);
        byte[][] payloads = new byte[2][];
        for (int mapVariant = 0; mapVariant < payloads.Length; mapVariant++)
        {
            using Av1EncoderPictureBuffer pictureBuffer = new(
                Configuration.Default,
                sequenceHeader,
                frameHeader,
                Width,
                Height,
                disallow4x4AllFrames: true);

            using Av1EncoderCoefficientBuffer coefficients = new(
                Configuration.Default,
                sequenceHeader,
                Width,
                Height);

            using Av1EncoderSuperblockWorkspace workspace = new(Configuration.Default);
            Av1Superblock superblock = new()
            {
                Workspace = workspace,
                TileInfo = new Av1TileInfo(0, 0, frameHeader),
                Index = 0
            };

            Av1PictureControlSet picture = pictureBuffer.Picture;
            Av1IntraSuperblockEncoder.Prepare(picture, superblock, Point.Empty);
            Av1TileWriter.Av1EntropyCodingContext entropyContext = new()
            {
                MacroBlock = new Av1MacroBlockD { Tile = superblock.TileInfo },
                MacroBlockModeInfo = picture.GetMacroBlockModeInfo(default),
                SuperblockOrigin = default
            };

            PaletteBlockEncoder blockEncoder = new(workspace, QIndex, mapVariant);
            using Av1SymbolEncoder writer = new(Configuration.Default, 128, QIndex, updateCdf: true);
            Av1TileWriter.WriteSuperblock(
                picture,
                entropyContext,
                writer,
                superblock,
                coefficients,
                tileIndex: 0,
                ref blockEncoder);

            using IMemoryOwner<byte> encoded = writer.Exit();
            payloads[mapVariant] = encoded.GetSpan().ToArray();
            Assert.Equal(1, blockEncoder.Count);
            Av1NeighborArrayUnit<Av1EncoderPaletteInfo> paletteContext = Assert.Single(picture.PaletteContexts);
            for (int index = 0; index < 2; index++)
            {
                Assert.Equal(3, paletteContext.Top[index].PaletteSizes[0]);
                Assert.Equal(3, paletteContext.Left[index].PaletteSizes[0]);
                Assert.Equal([16, 128, 240], paletteContext.Top[index].GetColors(Av1Plane.Y).ToArray());
                Assert.Equal([16, 128, 240], paletteContext.Left[index].GetColors(Av1Plane.Y).ToArray());
            }

            Assert.Equal(0, paletteContext.Top[2].PaletteSizes[0]);
            Assert.Equal(0, paletteContext.Left[2].PaletteSizes[0]);
        }

        // Changing only the selected color indices must change the range-coded tile payload.
        Assert.False(payloads[0].SequenceEqual(payloads[1]));
    }

    [Fact]
    public void ProductionTileSelectsExactLumaPaletteAtFullAndClippedSizes()
    {
        AssertProductionTileSelectsExactLumaPalette(
            Av1BitDepth.EightBit,
            8,
            8,
            8,
            false,
            (byte)32,
            (byte)224,
            32,
            224,
            static (writer, source, reconstruction, picture, coefficients, superblockWorkspace, blockWorkspace) =>
                new Av1TileEncoder(
                    writer,
                    source,
                    reconstruction,
                    picture,
                    coefficients,
                    superblockWorkspace,
                    blockWorkspace,
                    effort: 5));

        AssertProductionTileSelectsExactLumaPalette(
            Av1BitDepth.TwelveBit,
            12,
            8,
            8,
            false,
            (ushort)512,
            (ushort)3584,
            512,
            3584,
            static (writer, source, reconstruction, picture, coefficients, superblockWorkspace, blockWorkspace) =>
                new Av1TileEncoder(
                    writer,
                    source,
                    reconstruction,
                    picture,
                    coefficients,
                    superblockWorkspace,
                    blockWorkspace,
                    effort: 5));

        AssertProductionTileSelectsExactLumaPalette(
            Av1BitDepth.EightBit,
            8,
            5,
            3,
            false,
            (byte)48,
            (byte)208,
            48,
            208,
            static (writer, source, reconstruction, picture, coefficients, superblockWorkspace, blockWorkspace) =>
                new Av1TileEncoder(
                    writer,
                    source,
                    reconstruction,
                    picture,
                    coefficients,
                    superblockWorkspace,
                    blockWorkspace,
                    effort: 5));
    }

    [Theory]
    [InlineData(5, 3)]
    [InlineData(3, 5)]
    [InlineData(1, 5)]
    public void ProductionTileSelectsExactLumaPaletteAtClippedHighBitDepths(int width, int height)
    {
        AssertProductionTileSelectsExactLumaPalette(
            Av1BitDepth.TenBit,
            10,
            width,
            height,
            false,
            (ushort)128,
            (ushort)896,
            128,
            896,
            static (writer, source, reconstruction, picture, coefficients, superblockWorkspace, blockWorkspace) =>
                new Av1TileEncoder(
                    writer, source, reconstruction, picture, coefficients, superblockWorkspace, blockWorkspace, effort: 5));

        AssertProductionTileSelectsExactLumaPalette(
            Av1BitDepth.TwelveBit,
            12,
            width,
            height,
            false,
            (ushort)512,
            (ushort)3584,
            512,
            3584,
            static (writer, source, reconstruction, picture, coefficients, superblockWorkspace, blockWorkspace) =>
                new Av1TileEncoder(
                    writer, source, reconstruction, picture, coefficients, superblockWorkspace, blockWorkspace, effort: 5));
    }

    [Fact]
    public void ProductionTileSelectsLumaPaletteWithFourByFourTransforms()
    {
        AssertProductionTileSelectsExactLumaPalette(
            Av1BitDepth.EightBit,
            8,
            8,
            8,
            true,
            (byte)64,
            (byte)192,
            64,
            192,
            static (writer, source, reconstruction, picture, coefficients, superblockWorkspace, blockWorkspace) =>
                new Av1TileEncoder(
                    writer,
                    source,
                    reconstruction,
                    picture,
                    coefficients,
                    superblockWorkspace,
                    blockWorkspace,
                    effort: 6));

        AssertProductionTileSelectsExactLumaPalette(
            Av1BitDepth.TwelveBit,
            12,
            8,
            8,
            true,
            (ushort)1024,
            (ushort)3072,
            1024,
            3072,
            static (writer, source, reconstruction, picture, coefficients, superblockWorkspace, blockWorkspace) =>
                new Av1TileEncoder(
                    writer,
                    source,
                    reconstruction,
                    picture,
                    coefficients,
                    superblockWorkspace,
                    blockWorkspace,
                    effort: 6));
    }

    [Fact]
    public void ProductionTileSelectsExactPairedChromaPalette()
    {
        AssertProductionTileSelectsExactPairedChromaPalette(false, 8, 8, false, false);
        AssertProductionTileSelectsExactPairedChromaPalette(true, 8, 8, false, false);
    }

    [Theory]
    [InlineData(5, 3, false, false)]
    [InlineData(3, 5, false, false)]
    [InlineData(5, 3, true, false)]
    [InlineData(3, 5, true, false)]
    [InlineData(5, 3, true, true)]
    [InlineData(3, 5, true, true)]
    [InlineData(1, 5, true, false)]
    [InlineData(5, 1, true, false)]
    [InlineData(1, 5, true, true)]
    [InlineData(5, 1, true, true)]
    public void ProductionTileSelectsExactPairedChromaPaletteAtClippedSizes(int width, int height, bool subX, bool subY)
        => AssertProductionTileSelectsExactPairedChromaPalette(false, width, height, subX, subY);

    private static void AssertProductionTileSelectsExactPairedChromaPalette(
        bool useLumaPalette,
        int width,
        int height,
        bool subX,
        bool subY)
    {
        const int QIndex = 37;
        ObuColorConfig colorConfig = new()
        {
            IsMonochrome = false,
            SubSamplingX = subX,
            SubSamplingY = subY,
            BitDepth = Av1BitDepth.EightBit
        };

        Av1ColorFormat colorFormat = subY ? Av1ColorFormat.Yuv420 : subX ? Av1ColorFormat.Yuv422 : Av1ColorFormat.Yuv444;
        using Av1EncoderFrameBuffer<byte> source = new(
            Configuration.Default,
            width,
            height,
            8,
            colorFormat,
            0,
            0,
            lumaBorder: 64);

        using Av1EncoderFrameBuffer<byte> reconstruction = new(
            Configuration.Default,
            width,
            height,
            8,
            colorFormat,
            0,
            0,
            lumaBorder: 64);

        Buffer2DRegion<byte> lumaSource = source.Frame.CodedView.GetPlane(Av1Plane.Y);
        Buffer2DRegion<byte> blueSource = source.Frame.CodedView.GetPlane(Av1Plane.U);
        Buffer2DRegion<byte> redSource = source.Frame.CodedView.GetPlane(Av1Plane.V);
        for (int row = 0; row < lumaSource.Height; row++)
        {
            Span<byte> lumaRow = lumaSource.DangerousGetRowSpan(row);
            if (useLumaPalette)
            {
                for (int column = 0; column < lumaRow.Length; column++)
                {
                    lumaRow[column] = column < width / 2 ? (byte)64 : (byte)192;
                }
            }
            else
            {
                lumaRow.Fill(128);
            }
        }

        int chromaWidth = (width + (subX ? 1 : 0)) >> (subX ? 1 : 0);
        int chromaHeight = (height + (subY ? 1 : 0)) >> (subY ? 1 : 0);
        for (int row = 0; row < blueSource.Height; row++)
        {
            for (int column = 0; column < blueSource.Width; column++)
            {
                // Repeat the last visible sample into coded alignment, including one-pixel source axes.
                bool firstColor = chromaHeight > 1
                    ? Math.Min(row, chromaHeight - 1) < chromaHeight / 2
                    : Math.Min(column, chromaWidth - 1) < chromaWidth / 2;

                blueSource.DangerousGetRowSpan(row)[column] = firstColor ? (byte)32 : (byte)224;
                redSource.DangerousGetRowSpan(row)[column] = firstColor ? (byte)200 : (byte)40;
            }
        }

        ClearPlane(reconstruction.Luma);
        ClearPlane(Assert.IsType<Buffer2D<byte>>(reconstruction.ChromaBlue));
        ClearPlane(Assert.IsType<Buffer2D<byte>>(reconstruction.ChromaRed));
        using Av1EncoderModeInfoBuffer modeInfo = new(
            Configuration.Default,
            width,
            height,
            disallow4x4AllFrames: true);

        Av1PictureControlSet pictureTemplate = CreatePicture(
            modeInfo,
            colorConfig,
            use128x128Superblock: false,
            QIndex);

        pictureTemplate.Parent.FrameHeader.AllowScreenContentTools = true;
        pictureTemplate.Parent.FrameHeader.FrameSize.FrameWidth = width;
        pictureTemplate.Parent.FrameHeader.FrameSize.FrameHeight = height;
        using Av1EncoderPictureBuffer picture = new(
            Configuration.Default,
            pictureTemplate.Sequence.SequenceHeader,
            pictureTemplate.Parent.FrameHeader,
            width,
            height,
            disallow4x4AllFrames: true);

        using Av1EncoderCoefficientBuffer coefficients = new(
            Configuration.Default,
            pictureTemplate.Sequence.SequenceHeader,
            width,
            height);

        using Av1EncoderSuperblockWorkspace superblockWorkspace = new(Configuration.Default);
        using Av1EncoderBlockWorkspace blockWorkspace = new(Configuration.Default);
        using Av1SymbolEncoder symbolEncoder = CreateTileSymbolEncoder(
            picture.Picture,
            256);

        Av1TileEncoder tileWriter = new(
            symbolEncoder,
            source.Frame,
            reconstruction.Frame,
            picture.Picture,
            coefficients,
            superblockWorkspace,
            blockWorkspace,
            effort: 5);

        ref Av1MacroBlockModeInfo mode = ref picture.Picture.GetMacroBlockModeInfo(default);
        Assert.Equal(Av1ChromaPredictionMode.DC, mode.Block.UvMode);
        Assert.Equal(useLumaPalette ? 2 : 0, superblockWorkspace.PaletteInfo.PaletteSizes[0]);
        Assert.Equal(2, superblockWorkspace.PaletteInfo.PaletteSizes[1]);
        Assert.Equal([32, 224], superblockWorkspace.PaletteInfo.GetColors(Av1Plane.U).ToArray());
        Assert.Equal([200, 40], superblockWorkspace.PaletteInfo.GetColors(Av1Plane.V).ToArray());
        Assert.Equal((ushort)0, coefficients.GetTransformBlockSpan(0, Av1Plane.U)[0].EndOfBlock);
        Assert.Equal((ushort)0, coefficients.GetTransformBlockSpan(0, Av1Plane.V)[0].EndOfBlock);

        Buffer2DRegion<byte> colorIndexMap = superblockWorkspace
            .GetPaletteMaps()
            .GetMap(Av1PlaneType.Uv, blueSource.Width, blueSource.Height);

        Buffer2DRegion<byte> blueReconstruction = reconstruction.Frame.CodedView.GetPlane(Av1Plane.U);
        Buffer2DRegion<byte> redReconstruction = reconstruction.Frame.CodedView.GetPlane(Av1Plane.V);
        for (int row = 0; row < blueSource.Height; row++)
        {
            for (int column = 0; column < blueSource.Width; column++)
            {
                byte expectedIndex = blueSource.DangerousGetRowSpan(row)[column] == 32 ? (byte)0 : (byte)1;
                Assert.Equal(expectedIndex, colorIndexMap.DangerousGetRowSpan(row)[column]);
            }

            Assert.True(blueSource.DangerousGetRowSpan(row).SequenceEqual(blueReconstruction.DangerousGetRowSpan(row)));
            Assert.True(redSource.DangerousGetRowSpan(row).SequenceEqual(redReconstruction.DangerousGetRowSpan(row)));
        }

        byte[] payload = WriteCompleteTileObu(pictureTemplate, tileWriter, width, height);
        using Av1Decoder decoder = new(Configuration.Default);
        using Av1FrameBuffer<byte> decodedFrame = decoder.DecodeFrameBuffer(payload, null, null, out _);
        Assert.Equal(width, decodedFrame.Width);
        Assert.Equal(height, decodedFrame.Height);
        Assert.NotNull(decoder.FrameInfo);
        Assert.Equal(2, decoder.FrameInfo.GetModeInfoAt(default).GetPaletteSize(Av1PlaneType.Uv));
        for (int plane = 0; plane < 3; plane++)
        {
            int planeSubX = plane > 0 && subX ? 1 : 0;
            int planeSubY = plane > 0 && subY ? 1 : 0;
            Buffer2DRegion<byte> actual = decodedFrame.DeriveBlockPointer((Av1Plane)plane, planeSubX, planeSubY);
            Buffer2DRegion<byte> expected = reconstruction.Frame.View.GetPlane((Av1Plane)plane);
            for (int row = 0; row < expected.Height; row++)
            {
                Assert.Equal(expected.DangerousGetRowSpan(row), actual.DangerousGetRowSpan(row));
            }
        }

        string outputDirectory = Path.Combine(TestEnvironment.ActualOutputDirectoryFullPath, "Formats", "Heif", "Av1");
        string outputName = $"encoder-palette-chroma-{width}x{height}-{subX}-{subY}-{useLumaPalette}";
        Directory.CreateDirectory(outputDirectory);
        File.WriteAllBytes(Path.Combine(outputDirectory, outputName + ".obu"), payload);
        using FileStream raw = File.Create(Path.Combine(outputDirectory, outputName + ".retained.yuv"));
        for (int plane = 0; plane < 3; plane++)
        {
            Buffer2DRegion<byte> retained = reconstruction.Frame.View.GetPlane((Av1Plane)plane);
            for (int row = 0; row < retained.Height; row++)
            {
                raw.Write(retained.DangerousGetRowSpan(row));
            }
        }

        Assert.NotEqual(0, tileWriter.GetTileData(0).Length);
    }

    [Theory]
    [InlineData((int)Av1PredictionMode.Vertical, 0)]
    [InlineData((int)Av1PredictionMode.Horizontal, 0)]
    [InlineData((int)Av1PredictionMode.Smooth, 0)]
    [InlineData((int)Av1PredictionMode.Paeth, 0)]
    [InlineData((int)Av1PredictionMode.SmoothVertical, 0)]
    [InlineData((int)Av1PredictionMode.SmoothHorizontal, 0)]
    [InlineData((int)Av1PredictionMode.Directional135Degrees, 0)]
    [InlineData((int)Av1PredictionMode.Directional203Degrees, 0)]
    [InlineData((int)Av1PredictionMode.Directional157Degrees, 0)]
    [InlineData((int)Av1PredictionMode.Directional67Degrees, 0)]
    [InlineData((int)Av1PredictionMode.Directional113Degrees, 0)]
    [InlineData((int)Av1PredictionMode.Directional45Degrees, 0)]
    [InlineData((int)Av1PredictionMode.Directional45Degrees, -3)]
    [InlineData((int)Av1PredictionMode.Directional45Degrees, 3)]
    [InlineData((int)Av1PredictionMode.Directional135Degrees, -3)]
    [InlineData((int)Av1PredictionMode.Directional135Degrees, 3)]
    [InlineData((int)Av1PredictionMode.Directional203Degrees, -3)]
    [InlineData((int)Av1PredictionMode.Directional203Degrees, 3)]
    public void ProductionTileSelectsModeFromCurrentReconstruction(int expectedModeValue, int expectedAngleDelta)
    {
        const int Width = 16;
        const int Height = 16;
        const byte TopReference = 48;
        const byte LeftReference = 208;
        const int QIndex = 1;
        Av1PredictionMode expectedMode = (Av1PredictionMode)expectedModeValue;
        bool isDiagonal = expectedMode is >= Av1PredictionMode.Directional45Degrees and <= Av1PredictionMode.Directional67Degrees;
        int cornerReference = expectedMode == Av1PredictionMode.Horizontal
            ? LeftReference
            : expectedMode == Av1PredictionMode.Vertical ? TopReference : 128;

        Span<byte> directionalTarget = stackalloc byte[64];
        if (isDiagonal)
        {
            Span<byte> aboveStorage = stackalloc byte[17];
            Span<byte> above = aboveStorage[1..];
            Span<byte> leftStorage = stackalloc byte[17];
            Span<byte> left = leftStorage[1..];
            aboveStorage[0] = 128;
            leftStorage[0] = 128;
            for (int i = 0; i < 8; i++)
            {
                above[i] = (byte)(32 + (i * 24));
                left[i] = (byte)(224 - (i * 24));
            }

            above[8..].Fill(above[7]);
            left[8..].Fill(left[7]);

            // Directional arithmetic has separate byte-exact reference coverage. This fixture uses its scalar
            // path only to isolate production mode traversal, reference gathering, and rate-distortion selection.
            Av1DirectionalIntraPredictor.PredictScalar(
                directionalTarget,
                8,
                Av1TransformSize.Size8x8,
                above,
                left,
                false,
                false,
                expectedMode.ToAngle() + (expectedAngleDelta * Av1Constants.AngleStep));
        }

        ObuColorConfig colorConfig = new()
        {
            IsMonochrome = true,
            SubSamplingX = true,
            SubSamplingY = true,
            BitDepth = Av1BitDepth.EightBit
        };

        using Av1EncoderFrameBuffer<byte> source = new(
            Configuration.Default,
            Width,
            Height,
            8,
            Av1ColorFormat.Yuv400,
            0,
            0,
            lumaBorder: 64);

        using Av1EncoderFrameBuffer<byte> reconstruction = new(
            Configuration.Default,
            Width,
            Height,
            8,
            Av1ColorFormat.Yuv400,
            0,
            0,
            lumaBorder: 64);

        Buffer2DRegion<byte> sourcePlane = source.Frame.CodedView.GetPlane(Av1Plane.Y);

        // The first three 8x8 blocks establish the corner, top, and left reconstruction consumed by
        // the bottom-right target. This makes the assertion exercise production traversal and live state.
        for (int y = 0; y < Height; y++)
        {
            Span<byte> row = sourcePlane.DangerousGetRowSpan(y);
            for (int x = 0; x < Width; x++)
            {
                int rowIndex = y - 8;
                int columnIndex = x - 8;
                int value;
                if (y < 8)
                {
                    value = x < 8
                        ? cornerReference
                        : isDiagonal
                            ? 32 + (columnIndex * 24)
                            : expectedMode == Av1PredictionMode.Paeth ? 40 + (columnIndex * 20) : TopReference;
                }
                else if (x < 8)
                {
                    value = isDiagonal
                        ? 224 - (rowIndex * 24)
                        : expectedMode == Av1PredictionMode.Paeth ? 200 - (rowIndex * 20) : LeftReference;
                }
                else if (isDiagonal)
                {
                    value = directionalTarget[(rowIndex * 8) + columnIndex];
                }
                else if (expectedMode == Av1PredictionMode.Paeth)
                {
                    // Build the target from the nearest of left, top, and corner without calling the production predictor.
                    int top = 40 + (columnIndex * 20);
                    int left = 200 - (rowIndex * 20);
                    int predictor = top + left - 128;
                    int leftDistance = Math.Abs(predictor - left);
                    int topDistance = Math.Abs(predictor - top);
                    int cornerDistance = Math.Abs(predictor - 128);

                    value = leftDistance <= topDistance && leftDistance <= cornerDistance
                        ? left
                        : topDistance <= cornerDistance ? top : 128;
                }
                else
                {
                    // Apply the normative interpolation directly so a production predictor cannot generate its own fixture.
                    int rowWeight = Smooth8Weights[rowIndex];
                    int columnWeight = Smooth8Weights[columnIndex];
                    value = expectedMode switch
                    {
                        Av1PredictionMode.Horizontal => LeftReference,
                        Av1PredictionMode.Vertical => TopReference,
                        Av1PredictionMode.SmoothVertical => ((rowWeight * TopReference) + ((256 - rowWeight) * LeftReference) + 128) >> 8,
                        Av1PredictionMode.SmoothHorizontal => ((columnWeight * LeftReference) + ((256 - columnWeight) * TopReference) + 128) >> 8,
                        _ => ((rowWeight * TopReference) + ((256 - rowWeight) * LeftReference) +
                            (columnWeight * LeftReference) + ((256 - columnWeight) * TopReference) + 256) >> 9
                    };
                }

                row[x] = (byte)value;
            }
        }

        ClearPlane(reconstruction.Luma);
        using Av1EncoderModeInfoBuffer modeInfo = new(Configuration.Default, Width, Height, disallow4x4AllFrames: true);
        Av1PictureControlSet pictureTemplate = CreatePicture(modeInfo, colorConfig, use128x128Superblock: false, QIndex);
        using Av1EncoderPictureBuffer picture = new(
            Configuration.Default,
            pictureTemplate.Sequence.SequenceHeader,
            pictureTemplate.Parent.FrameHeader,
            Width,
            Height,
            disallow4x4AllFrames: true);

        using Av1EncoderCoefficientBuffer coefficients = new(
            Configuration.Default,
            pictureTemplate.Sequence.SequenceHeader,
            Width,
            Height);

        using Av1EncoderSuperblockWorkspace superblockWorkspace = new(Configuration.Default);
        using Av1EncoderBlockWorkspace blockWorkspace = new(Configuration.Default);
        using Av1SymbolEncoder symbolEncoder = CreateTileSymbolEncoder(
            picture.Picture,
            512);

        Av1TileEncoder tileWriter = new(
            symbolEncoder,
            source.Frame,
            reconstruction.Frame,
            picture.Picture,
            coefficients,
            superblockWorkspace,
            blockWorkspace,
            effort: 5);

        ref Av1MacroBlockModeInfo targetBlock = ref picture.Picture.GetMacroBlockModeInfo(new Point(2, 2));
        Assert.Equal(expectedMode, targetBlock.Block.Mode);
        Assert.Equal(
            expectedAngleDelta,
            superblockWorkspace.FinalBlocks[3].PredictionUnit.AngleDelta[(int)Av1PlaneType.Y]);

        Av1EncoderTransformBlockState targetState =
            coefficients.GetTransformBlockSpan(0, Av1Plane.Y)[12];

        // Every transform has the same skip cost for this exact-prediction target, so reference enum order
        // requires DCT-DCT to win even when the mode-derived first pass used another transform.
        Assert.Equal((ushort)0, targetState.EndOfBlock);
        Assert.Equal(Av1TransformType.DctDct, targetState.TransformType);
        Assert.NotEqual(0, tileWriter.GetTileData(0).Length);
    }

    [Theory]
    [InlineData((int)Av1ChromaPredictionMode.Vertical, 0, (int)Av1TransformType.AdstDct, (int)Av1ColorFormat.Yuv444)]
    [InlineData((int)Av1ChromaPredictionMode.Horizontal, 0, (int)Av1TransformType.DctAdst, (int)Av1ColorFormat.Yuv420)]
    [InlineData((int)Av1ChromaPredictionMode.Paeth, 0, (int)Av1TransformType.AdstAdst, (int)Av1ColorFormat.Yuv422)]
    [InlineData((int)Av1ChromaPredictionMode.Directional45Degrees, -3, (int)Av1TransformType.DctDct, (int)Av1ColorFormat.Yuv420)]
    [InlineData((int)Av1ChromaPredictionMode.Directional135Degrees, 3, (int)Av1TransformType.AdstAdst, (int)Av1ColorFormat.Yuv422)]
    [InlineData((int)Av1ChromaPredictionMode.Directional203Degrees, -3, (int)Av1TransformType.DctAdst, (int)Av1ColorFormat.Yuv444)]
    public void ProductionTileSelectsChromaModeFromCurrentReconstruction(
        int expectedModeValue,
        int expectedAngleDelta,
        int expectedTransformTypeValue,
        int colorFormatValue)
    {
        const int Width = 16;
        const int Height = 16;
        const int QIndex = 1;
        Av1ChromaPredictionMode expectedMode = (Av1ChromaPredictionMode)expectedModeValue;
        Av1TransformType expectedTransformType = (Av1TransformType)expectedTransformTypeValue;
        Av1ColorFormat colorFormat = (Av1ColorFormat)colorFormatValue;
        bool subsamplingX = colorFormat is Av1ColorFormat.Yuv420 or Av1ColorFormat.Yuv422;
        bool subsamplingY = colorFormat == Av1ColorFormat.Yuv420;
        int chromaSubsamplingX = subsamplingX ? 1 : 0;
        int chromaSubsamplingY = subsamplingY ? 1 : 0;
        Av1TransformSize transformSize = Av1BlockSize.Block8x8.GetMaxUvTransformSize(
            subsamplingX,
            subsamplingY);

        ObuColorConfig colorConfig = new()
        {
            IsMonochrome = false,
            SubSamplingX = subsamplingX,
            SubSamplingY = subsamplingY,
            BitDepth = Av1BitDepth.EightBit
        };

        using Av1EncoderFrameBuffer<byte> source = new(
            Configuration.Default,
            Width,
            Height,
            8,
            colorFormat,
            chromaSubsamplingX,
            chromaSubsamplingY,
            lumaBorder: 64);

        using Av1EncoderFrameBuffer<byte> reconstruction = new(
            Configuration.Default,
            Width,
            Height,
            8,
            colorFormat,
            chromaSubsamplingX,
            chromaSubsamplingY,
            lumaBorder: 64);

        FillPlane(source.Frame.CodedView.GetPlane(Av1Plane.Y), (byte)128);
        FillChromaModeSelectionPlane(
            source.Frame.CodedView.GetPlane(Av1Plane.U),
            transformSize,
            expectedMode,
            expectedAngleDelta);

        FillChromaModeSelectionPlane(
            source.Frame.CodedView.GetPlane(Av1Plane.V),
            transformSize,
            expectedMode,
            expectedAngleDelta);

        ClearPlane(reconstruction.Luma);
        ClearPlane(Assert.IsType<Buffer2D<byte>>(reconstruction.ChromaBlue));
        ClearPlane(Assert.IsType<Buffer2D<byte>>(reconstruction.ChromaRed));
        using Av1EncoderModeInfoBuffer modeInfo = new(Configuration.Default, Width, Height, disallow4x4AllFrames: true);
        Av1PictureControlSet pictureTemplate = CreatePicture(modeInfo, colorConfig, use128x128Superblock: false, QIndex);
        using Av1EncoderPictureBuffer picture = new(
            Configuration.Default,
            pictureTemplate.Sequence.SequenceHeader,
            pictureTemplate.Parent.FrameHeader,
            Width,
            Height,
            disallow4x4AllFrames: true);

        using Av1EncoderCoefficientBuffer coefficients = new(
            Configuration.Default,
            pictureTemplate.Sequence.SequenceHeader,
            Width,
            Height);

        using Av1EncoderSuperblockWorkspace superblockWorkspace = new(Configuration.Default);
        using Av1EncoderBlockWorkspace blockWorkspace = new(Configuration.Default);
        using Av1SymbolEncoder symbolEncoder = CreateTileSymbolEncoder(
            picture.Picture,
            512);

        Av1TileEncoder tileWriter = new(
            symbolEncoder,
            source.Frame,
            reconstruction.Frame,
            picture.Picture,
            coefficients,
            superblockWorkspace,
            blockWorkspace,
            effort: 5);

        ref Av1MacroBlockModeInfo targetBlock = ref picture.Picture.GetMacroBlockModeInfo(new Point(2, 2));
        Assert.Equal(expectedMode, targetBlock.Block.UvMode);
        Assert.Equal(
            expectedAngleDelta,
            superblockWorkspace.FinalBlocks[3].PredictionUnit.AngleDelta[(int)Av1PlaneType.Uv]);

        int targetTransformIndex = (3 * transformSize.GetSize2d()) /
            Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount;

        Av1EncoderTransformBlockState blueState =
            coefficients.GetTransformBlockSpan(0, Av1Plane.U)[targetTransformIndex];

        Av1EncoderTransformBlockState redState =
            coefficients.GetTransformBlockSpan(0, Av1Plane.V)[targetTransformIndex];

        Assert.NotEqual((ushort)0, blueState.EndOfBlock);
        Assert.NotEqual((ushort)0, redState.EndOfBlock);
        Assert.Equal(expectedTransformType, blueState.TransformType);
        Assert.Equal(expectedTransformType, redState.TransformType);
        Assert.NotEqual(0, tileWriter.GetTileData(0).Length);
    }

    [Theory]
    [InlineData((int)Av1ColorFormat.Yuv420)]
    [InlineData((int)Av1ColorFormat.Yuv422)]
    [InlineData((int)Av1ColorFormat.Yuv444)]
    public void ProductionTileSelectsChromaFromReconstructedLuma(int colorFormatValue)
        => VerifyProductionTileSelectsChromaFromReconstructedLuma<byte>(
            colorFormatValue,
            8,
            static (writer, source, reconstruction, picture, coefficients, superblockWorkspace, blockWorkspace) =>
                new(
                    writer,
                    source,
                    reconstruction,
                    picture,
                    coefficients,
                    superblockWorkspace,
                    blockWorkspace,
                    effort: 5));

    [Theory]
    [InlineData((int)Av1ColorFormat.Yuv420, 10)]
    [InlineData((int)Av1ColorFormat.Yuv420, 12)]
    [InlineData((int)Av1ColorFormat.Yuv422, 10)]
    [InlineData((int)Av1ColorFormat.Yuv422, 12)]
    [InlineData((int)Av1ColorFormat.Yuv444, 10)]
    [InlineData((int)Av1ColorFormat.Yuv444, 12)]
    public void ProductionTileSelectsChromaFromReconstructedLumaHighBitDepth(
        int colorFormatValue,
        int bitDepth)
        => VerifyProductionTileSelectsChromaFromReconstructedLuma<ushort>(
            colorFormatValue,
            bitDepth,
            static (writer, source, reconstruction, picture, coefficients, superblockWorkspace, blockWorkspace) =>
                new(
                    writer,
                    source,
                    reconstruction,
                    picture,
                    coefficients,
                    superblockWorkspace,
                    blockWorkspace,
                    effort: 5));

    private static void VerifyProductionTileSelectsChromaFromReconstructedLuma<TSample>(
        int colorFormatValue,
        int bitDepth,
        TileWriterFactory<TSample> createWriter)
        where TSample : unmanaged, IBinaryInteger<TSample>
    {
        const int Width = 16;
        const int Height = 16;
        const int QIndex = 1;
        const int TileBufferLength = 512;
        const int AlphaU = 16;
        const int AlphaV = -16;
        Av1ColorFormat colorFormat = (Av1ColorFormat)colorFormatValue;
        bool subsamplingX = colorFormat is Av1ColorFormat.Yuv420 or Av1ColorFormat.Yuv422;
        bool subsamplingY = colorFormat == Av1ColorFormat.Yuv420;
        int chromaSubsamplingX = subsamplingX ? 1 : 0;
        int chromaSubsamplingY = subsamplingY ? 1 : 0;
        int sampleScale = 1 << (bitDepth - 8);
        int midpoint = 1 << (bitDepth - 1);
        int maxSample = (1 << bitDepth) - 1;
        Av1TransformSize transformSize = Av1BlockSize.Block8x8.GetMaxUvTransformSize(
            subsamplingX,
            subsamplingY);

        ObuColorConfig colorConfig = new()
        {
            IsMonochrome = false,
            SubSamplingX = subsamplingX,
            SubSamplingY = subsamplingY,
            BitDepth = (Av1BitDepth)((bitDepth - 8) / 2)
        };

        using Av1EncoderFrameBuffer<TSample> pilotSource = new(
            Configuration.Default,
            Width,
            Height,
            bitDepth,
            colorFormat,
            chromaSubsamplingX,
            chromaSubsamplingY,
            lumaBorder: 64);

        using Av1EncoderFrameBuffer<TSample> pilotReconstruction = new(
            Configuration.Default,
            Width,
            Height,
            bitDepth,
            colorFormat,
            chromaSubsamplingX,
            chromaSubsamplingY,
            lumaBorder: 64);

        Buffer2DRegion<TSample> pilotLuma = pilotSource.Frame.CodedView.GetPlane(Av1Plane.Y);
        for (int y = 0; y < pilotLuma.Height; y++)
        {
            Span<TSample> row = pilotLuma.DangerousGetRowSpan(y);
            for (int x = 0; x < row.Length; x++)
            {
                row[x] = TSample.CreateChecked(
                    (96 + (((x * 29) + (y * 47) + (((x ^ y) & 1) * 53)) & 63)) * sampleScale);
            }
        }

        FillPlane(pilotSource.Frame.CodedView.GetPlane(Av1Plane.U), TSample.CreateChecked(midpoint));
        FillPlane(pilotSource.Frame.CodedView.GetPlane(Av1Plane.V), TSample.CreateChecked(midpoint));
        ClearPlane(pilotReconstruction.Luma);
        ClearPlane(Assert.IsType<Buffer2D<TSample>>(pilotReconstruction.ChromaBlue));
        ClearPlane(Assert.IsType<Buffer2D<TSample>>(pilotReconstruction.ChromaRed));
        using Av1EncoderModeInfoBuffer pilotModeInfo = new(Configuration.Default, Width, Height, disallow4x4AllFrames: true);
        Av1PictureControlSet pilotTemplate = CreatePicture(pilotModeInfo, colorConfig, use128x128Superblock: false, QIndex);
        using Av1EncoderPictureBuffer pilotPicture = new(
            Configuration.Default,
            pilotTemplate.Sequence.SequenceHeader,
            pilotTemplate.Parent.FrameHeader,
            Width,
            Height,
            disallow4x4AllFrames: true);

        using Av1EncoderCoefficientBuffer pilotCoefficients = new(
            Configuration.Default,
            pilotTemplate.Sequence.SequenceHeader,
            Width,
            Height);

        using Av1EncoderSuperblockWorkspace pilotSuperblockWorkspace = new(Configuration.Default);
        using Av1EncoderBlockWorkspace pilotBlockWorkspace = new(Configuration.Default);
        using Av1SymbolEncoder pilotSymbolEncoder = CreateTileSymbolEncoder(
            pilotPicture.Picture,
            TileBufferLength);

        Av1TileEncoder pilotWriter = createWriter(
            pilotSymbolEncoder,
            pilotSource.Frame,
            pilotReconstruction.Frame,
            pilotPicture.Picture,
            pilotCoefficients,
            pilotSuperblockWorkspace,
            pilotBlockWorkspace);

        Buffer2DRegion<TSample> reconstructedLuma = pilotReconstruction.Frame.CodedView.GetPlane(Av1Plane.Y);
        int chromaWidth = transformSize.GetWidth();
        int chromaHeight = transformSize.GetHeight();
        int sampleCount = transformSize.GetSize2d();
        int lumaScaleShift = 3 - chromaSubsamplingX - chromaSubsamplingY;
        Span<short> lumaQ3 = stackalloc short[64];
        int sumQ3 = sampleCount >> 1;
        for (int row = 0; row < chromaHeight; row++)
        {
            for (int column = 0; column < chromaWidth; column++)
            {
                int lumaSum = 0;
                int lumaX = 8 + (column << chromaSubsamplingX);
                int lumaY = 8 + (row << chromaSubsamplingY);
                for (int offsetY = 0; offsetY <= chromaSubsamplingY; offsetY++)
                {
                    ReadOnlySpan<TSample> lumaRow = reconstructedLuma.DangerousGetRowSpan(lumaY + offsetY);
                    for (int offsetX = 0; offsetX <= chromaSubsamplingX; offsetX++)
                    {
                        lumaSum += int.CreateChecked(lumaRow[lumaX + offsetX]);
                    }
                }

                short sampleQ3 = (short)(lumaSum << lumaScaleShift);
                lumaQ3[(row * chromaWidth) + column] = sampleQ3;
                sumQ3 += sampleQ3;
            }
        }

        int averageQ3 = sumQ3 >> (transformSize.GetBlockWidthLog2() + transformSize.GetBlockHeightLog2());
        using Av1EncoderFrameBuffer<TSample> source = new(
            Configuration.Default,
            Width,
            Height,
            bitDepth,
            colorFormat,
            chromaSubsamplingX,
            chromaSubsamplingY,
            lumaBorder: 64);

        using Av1EncoderFrameBuffer<TSample> reconstruction = new(
            Configuration.Default,
            Width,
            Height,
            bitDepth,
            colorFormat,
            chromaSubsamplingX,
            chromaSubsamplingY,
            lumaBorder: 64);

        for (int y = 0; y < pilotLuma.Height; y++)
        {
            pilotLuma.DangerousGetRowSpan(y).CopyTo(source.Frame.CodedView.GetPlane(Av1Plane.Y).DangerousGetRowSpan(y));
        }

        Buffer2DRegion<TSample> blue = source.Frame.CodedView.GetPlane(Av1Plane.U);
        Buffer2DRegion<TSample> red = source.Frame.CodedView.GetPlane(Av1Plane.V);
        FillPlane(blue, TSample.CreateChecked(midpoint));
        FillPlane(red, TSample.CreateChecked(midpoint));
        for (int row = 0; row < chromaHeight; row++)
        {
            Span<TSample> blueRow = blue.DangerousGetRowSpan(chromaHeight + row);
            Span<TSample> redRow = red.DangerousGetRowSpan(chromaHeight + row);
            for (int column = 0; column < chromaWidth; column++)
            {
                int acQ3 = lumaQ3[(row * chromaWidth) + column] - averageQ3;
                int blueProduct = AlphaU * acQ3;
                int redProduct = AlphaV * acQ3;
                int blueAdjustment = (blueProduct + 32 + (blueProduct >> 31)) >> 6;
                int redAdjustment = (redProduct + 32 + (redProduct >> 31)) >> 6;
                blueRow[chromaWidth + column] = TSample.CreateChecked(Math.Clamp(midpoint + blueAdjustment, 0, maxSample));
                redRow[chromaWidth + column] = TSample.CreateChecked(Math.Clamp(midpoint + redAdjustment, 0, maxSample));
            }
        }

        ClearPlane(reconstruction.Luma);
        ClearPlane(Assert.IsType<Buffer2D<TSample>>(reconstruction.ChromaBlue));
        ClearPlane(Assert.IsType<Buffer2D<TSample>>(reconstruction.ChromaRed));
        using Av1EncoderModeInfoBuffer modeInfo = new(Configuration.Default, Width, Height, disallow4x4AllFrames: true);
        Av1PictureControlSet pictureTemplate = CreatePicture(modeInfo, colorConfig, use128x128Superblock: false, QIndex);
        using Av1EncoderPictureBuffer picture = new(
            Configuration.Default,
            pictureTemplate.Sequence.SequenceHeader,
            pictureTemplate.Parent.FrameHeader,
            Width,
            Height,
            disallow4x4AllFrames: true);

        using Av1EncoderCoefficientBuffer coefficients = new(
            Configuration.Default,
            pictureTemplate.Sequence.SequenceHeader,
            Width,
            Height);

        using Av1EncoderSuperblockWorkspace superblockWorkspace = new(Configuration.Default);
        using Av1EncoderBlockWorkspace blockWorkspace = new(Configuration.Default);
        using Av1SymbolEncoder symbolEncoder = CreateTileSymbolEncoder(
            picture.Picture,
            TileBufferLength);

        Av1TileEncoder tileWriter = createWriter(
            symbolEncoder,
            source.Frame,
            reconstruction.Frame,
            picture.Picture,
            coefficients,
            superblockWorkspace,
            blockWorkspace);

        Buffer2DRegion<TSample> actualLuma = reconstruction.Frame.CodedView.GetPlane(Av1Plane.Y);
        for (int y = 0; y < reconstructedLuma.Height; y++)
        {
            Assert.Equal(reconstructedLuma.DangerousGetRowSpan(y), actualLuma.DangerousGetRowSpan(y));
        }

        ref Av1MacroBlockModeInfo targetBlock = ref picture.Picture.GetMacroBlockModeInfo(new Point(2, 2));
        Assert.Equal(Av1ChromaPredictionMode.ChromaFromLuma, targetBlock.Block.UvMode);
        Assert.Equal(
            Av1ChromaFromLumaMath.JointSign(
                Av1ChromaFromLumaMath.SignPositive,
                Av1ChromaFromLumaMath.SignNegative),
            superblockWorkspace.FinalBlocks[3].PredictionUnit.ChromaFromLumaSigns);

        Assert.Equal(
            Av1ChromaFromLumaMath.PackIndices(
                Av1ChromaFromLumaMath.AlphaToMagnitudeIndex(AlphaU),
                Av1ChromaFromLumaMath.AlphaToMagnitudeIndex(AlphaV)),
            superblockWorkspace.FinalBlocks[3].PredictionUnit.ChromaFromLumaIndex);

        int targetTransformIndex = (3 * sampleCount) /
            Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount;

        Av1EncoderTransformBlockState blueState =
            coefficients.GetTransformBlockSpan(0, Av1Plane.U)[targetTransformIndex];

        Av1EncoderTransformBlockState redState =
            coefficients.GetTransformBlockSpan(0, Av1Plane.V)[targetTransformIndex];

        Assert.Equal((ushort)0, blueState.EndOfBlock);
        Assert.Equal((ushort)0, redState.EndOfBlock);
        Assert.Equal(Av1TransformType.DctDct, blueState.TransformType);
        Assert.Equal(Av1TransformType.DctDct, redState.TransformType);
        Assert.NotEqual(0, pilotWriter.GetTileData(0).Length);
        Assert.NotEqual(0, tileWriter.GetTileData(0).Length);
    }

    [Theory]
    [InlineData((int)Av1FilterIntraMode.DC)]
    [InlineData((int)Av1FilterIntraMode.Vertical)]
    [InlineData((int)Av1FilterIntraMode.Horizontal)]
    [InlineData((int)Av1FilterIntraMode.Directional157)]
    [InlineData((int)Av1FilterIntraMode.Paeth)]
    public void ProductionTileSelectsFilterIntraMode(int filterIntraModeValue)
        => VerifyProductionTileSelectsFilterIntraMode<byte>(
            filterIntraModeValue,
            8,
            false,
            static (writer, source, reconstruction, picture, coefficients, superblockWorkspace, blockWorkspace) =>
                new(
                    writer,
                    source,
                    reconstruction,
                    picture,
                    coefficients,
                    superblockWorkspace,
                    blockWorkspace,
                    effort: 5),
            static (mode, destination, stride, above, left, width, height, _, scratch) =>
                Av1FilterIntraPredictorBase.GetPredictor(mode)
                    .Predict(destination, stride, above, left, width, height, scratch));

    [Theory]
    [InlineData((int)Av1FilterIntraMode.DC, 10)]
    [InlineData((int)Av1FilterIntraMode.DC, 12)]
    [InlineData((int)Av1FilterIntraMode.Vertical, 10)]
    [InlineData((int)Av1FilterIntraMode.Vertical, 12)]
    [InlineData((int)Av1FilterIntraMode.Horizontal, 10)]
    [InlineData((int)Av1FilterIntraMode.Horizontal, 12)]
    [InlineData((int)Av1FilterIntraMode.Directional157, 10)]
    [InlineData((int)Av1FilterIntraMode.Directional157, 12)]
    [InlineData((int)Av1FilterIntraMode.Paeth, 10)]
    [InlineData((int)Av1FilterIntraMode.Paeth, 12)]
    public void ProductionTileSelectsFilterIntraModeHighBitDepth(
        int filterIntraModeValue,
        int bitDepth)
        => VerifyProductionTileSelectsFilterIntraMode<ushort>(
            filterIntraModeValue,
            bitDepth,
            false,
            static (writer, source, reconstruction, picture, coefficients, superblockWorkspace, blockWorkspace) =>
                new(
                    writer,
                    source,
                    reconstruction,
                    picture,
                    coefficients,
                    superblockWorkspace,
                    blockWorkspace,
                    effort: 5),
            static (mode, destination, stride, above, left, width, height, sampleBitDepth, scratch) =>
                Av1FilterIntraPredictorBase.GetPredictor(mode)
                    .Predict(
                        MemoryMarshal.Cast<ushort, short>(destination),
                        stride,
                        MemoryMarshal.Cast<ushort, short>(above),
                        MemoryMarshal.Cast<ushort, short>(left),
                        width,
                        height,
                        sampleBitDepth,
                        MemoryMarshal.Cast<ushort, short>(scratch)));

    [Fact]
    public void ProductionTileSelectsFilterIntraWithFourByFourTransforms()
        => VerifyProductionTileSelectsFilterIntraMode<byte>(
            (int)Av1FilterIntraMode.DC,
            8,
            true,
            static (writer, source, reconstruction, picture, coefficients, superblockWorkspace, blockWorkspace) =>
                new(
                    writer,
                    source,
                    reconstruction,
                    picture,
                    coefficients,
                    superblockWorkspace,
                    blockWorkspace,
                    effort: 6),
            static (mode, destination, stride, above, left, width, height, _, scratch) =>
                Av1FilterIntraPredictorBase.GetPredictor(mode)
                    .Predict(destination, stride, above, left, width, height, scratch));

    [Theory]
    [InlineData(10)]
    [InlineData(12)]
    public void ProductionTileSelectsFilterIntraWithFourByFourTransformsHighBitDepth(int bitDepth)
        => VerifyProductionTileSelectsFilterIntraMode<ushort>(
            (int)Av1FilterIntraMode.DC,
            bitDepth,
            true,
            static (writer, source, reconstruction, picture, coefficients, superblockWorkspace, blockWorkspace) =>
                new(
                    writer,
                    source,
                    reconstruction,
                    picture,
                    coefficients,
                    superblockWorkspace,
                    blockWorkspace,
                    effort: 6),
            static (mode, destination, stride, above, left, width, height, sampleBitDepth, scratch) =>
                Av1FilterIntraPredictorBase.GetPredictor(mode)
                    .Predict(
                        MemoryMarshal.Cast<ushort, short>(destination),
                        stride,
                        MemoryMarshal.Cast<ushort, short>(above),
                        MemoryMarshal.Cast<ushort, short>(left),
                        width,
                        height,
                        sampleBitDepth,
                        MemoryMarshal.Cast<ushort, short>(scratch)));

    private static void VerifyProductionTileSelectsFilterIntraMode<TSample>(
        int filterIntraModeValue,
        int bitDepth,
        bool useSplitTransform,
        TileWriterFactory<TSample> createWriter,
        FilterPrediction<TSample> predictFilter)
        where TSample : unmanaged, IBinaryInteger<TSample>
    {
        const int Width = 16;
        const int Height = 16;
        const int QIndex = 37;
        const int TileBufferLength = 512;
        const int TargetX = 8;
        const int TargetY = 8;
        const Av1TransformSize TransformSize = Av1TransformSize.Size8x8;
        Av1FilterIntraMode filterIntraMode = (Av1FilterIntraMode)filterIntraModeValue;
        int sampleScale = 1 << (bitDepth - 8);
        ObuColorConfig colorConfig = new()
        {
            IsMonochrome = true,
            SubSamplingX = true,
            SubSamplingY = true,
            BitDepth = (Av1BitDepth)((bitDepth - 8) / 2)
        };

        using Av1EncoderFrameBuffer<TSample> pilotSource = new(
            Configuration.Default,
            Width,
            Height,
            bitDepth,
            Av1ColorFormat.Yuv400,
            1,
            1,
            lumaBorder: 64);

        using Av1EncoderFrameBuffer<TSample> pilotReconstruction = new(
            Configuration.Default,
            Width,
            Height,
            bitDepth,
            Av1ColorFormat.Yuv400,
            1,
            1,
            lumaBorder: 64);

        Buffer2DRegion<TSample> pilotLuma = pilotSource.Frame.CodedView.GetPlane(Av1Plane.Y);
        for (int y = 0; y < pilotLuma.Height; y++)
        {
            Span<TSample> row = pilotLuma.DangerousGetRowSpan(y);
            for (int x = 0; x < row.Length; x++)
            {
                row[x] = TSample.CreateChecked(
                    (64 + (((x * 71) + (y * 109) + (((x ^ y) & 3) * 37)) & 127)) * sampleScale);
            }
        }

        ClearPlane(pilotReconstruction.Luma);
        using Av1EncoderModeInfoBuffer pilotModeInfo = new(Configuration.Default, Width, Height, disallow4x4AllFrames: true);
        Av1PictureControlSet pilotTemplate = CreatePicture(pilotModeInfo, colorConfig, use128x128Superblock: false, QIndex);
        pilotTemplate.Sequence.SequenceHeader.EnableFilterIntra = true;
        pilotTemplate.Parent.FrameHeader.TransformMode = useSplitTransform
            ? Av1TransformMode.Select
            : Av1TransformMode.Largest;

        using Av1EncoderPictureBuffer pilotPicture = new(
            Configuration.Default,
            pilotTemplate.Sequence.SequenceHeader,
            pilotTemplate.Parent.FrameHeader,
            Width,
            Height,
            disallow4x4AllFrames: true);

        using Av1EncoderCoefficientBuffer pilotCoefficients = new(
            Configuration.Default,
            pilotTemplate.Sequence.SequenceHeader,
            Width,
            Height);

        using Av1EncoderSuperblockWorkspace pilotSuperblockWorkspace = new(Configuration.Default);
        using Av1EncoderBlockWorkspace pilotBlockWorkspace = new(Configuration.Default);
        using Av1SymbolEncoder pilotSymbolEncoder = CreateTileSymbolEncoder(
            pilotPicture.Picture,
            TileBufferLength);

        Av1TileEncoder pilotWriter = createWriter(
            pilotSymbolEncoder,
            pilotSource.Frame,
            pilotReconstruction.Frame,
            pilotPicture.Picture,
            pilotCoefficients,
            pilotSuperblockWorkspace,
            pilotBlockWorkspace);

        Buffer2DRegion<TSample> reconstructedLuma = pilotReconstruction.Frame.CodedView.GetPlane(Av1Plane.Y);
        Span<TSample> aboveStorage = stackalloc TSample[9];
        Span<TSample> above = aboveStorage[1..];
        Span<TSample> left = stackalloc TSample[8];
        ReadOnlySpan<TSample> reconstructedAbove = reconstructedLuma.DangerousGetRowSpan(TargetY - 1);
        aboveStorage[0] = reconstructedAbove[TargetX - 1];
        reconstructedAbove.Slice(TargetX, 8).CopyTo(above);
        for (int row = 0; row < 8; row++)
        {
            left[row] = reconstructedLuma.DangerousGetRowSpan(TargetY + row)[TargetX - 1];
        }

        Span<TSample> target = stackalloc TSample[TransformSize.GetSize2d()];
        Span<TSample> filterScratch = stackalloc TSample[Av1FilterIntraPredictorBase.ScratchLength];
        if (useSplitTransform)
        {
            Span<TSample> transformAboveStorage = stackalloc TSample[5];
            Span<TSample> transformAbove = transformAboveStorage[1..];
            Span<TSample> transformLeft = stackalloc TSample[4];
            for (int transformRow = 0; transformRow < 2; transformRow++)
            {
                int rowOffset = transformRow * 4;
                for (int transformColumn = 0; transformColumn < 2; transformColumn++)
                {
                    int columnOffset = transformColumn * 4;
                    ReadOnlySpan<TSample> availableAbove = transformRow == 0
                        ? above.Slice(columnOffset, 4)
                        : target.Slice(((rowOffset - 1) * 8) + columnOffset, 4);

                    // The predictor consumes the corner through the element immediately before the top-edge span.
                    // Later transforms therefore use already reconstructed samples from the same 8-by-8 block.
                    transformAboveStorage[0] = transformRow == 0
                        ? transformColumn == 0 ? aboveStorage[0] : above[columnOffset - 1]
                        : transformColumn == 0 ? left[rowOffset - 1] : target[((rowOffset - 1) * 8) + columnOffset - 1];

                    availableAbove.CopyTo(transformAbove);
                    for (int row = 0; row < 4; row++)
                    {
                        transformLeft[row] = transformColumn == 0
                            ? left[rowOffset + row]
                            : target[((rowOffset + row) * 8) + columnOffset - 1];
                    }

                    int destinationOffset = (rowOffset * 8) + columnOffset;
                    predictFilter(
                        filterIntraMode,
                        target[destinationOffset..],
                        8,
                        transformAbove,
                        transformLeft,
                        4,
                        4,
                        bitDepth,
                        filterScratch);
                }
            }
        }
        else
        {
            predictFilter(filterIntraMode, target, 8, above, left, 8, 8, bitDepth, filterScratch);
        }

        if (useSplitTransform)
        {
            for (int transformRow = 0; transformRow < 2; transformRow++)
            {
                for (int transformColumn = 0; transformColumn < 2; transformColumn++)
                {
                    int transformIndex = (transformRow * 2) + transformColumn;

                    // Distinct transform-local frequency patterns remain compact in separate 4-by-4 bases but spread
                    // across coefficients when a single 8-by-8 transform spans the discontinuities between quadrants.
                    for (int row = 0; row < 4; row++)
                    {
                        Span<TSample> targetRow = target.Slice(
                            (((transformRow * 4) + row) * 8) + (transformColumn * 4),
                            4);

                        for (int column = 0; column < targetRow.Length; column++)
                        {
                            int residualSign = transformIndex switch
                            {
                                0 => row < 2 ? -1 : 1,
                                1 => column < 2 ? -1 : 1,
                                2 => (row < 2) == (column < 2) ? -1 : 1,
                                _ => ((row + column) & 1) == 0 ? -1 : 1
                            };

                            targetRow[column] = TSample.CreateChecked(
                                int.CreateChecked(targetRow[column]) + (residualSign * 40 * sampleScale));
                        }
                    }
                }
            }
        }

        using Av1EncoderFrameBuffer<TSample> source = new(
            Configuration.Default,
            Width,
            Height,
            bitDepth,
            Av1ColorFormat.Yuv400,
            1,
            1,
            lumaBorder: 64);

        using Av1EncoderFrameBuffer<TSample> reconstruction = new(
            Configuration.Default,
            Width,
            Height,
            bitDepth,
            Av1ColorFormat.Yuv400,
            1,
            1,
            lumaBorder: 64);

        Buffer2DRegion<TSample> sourceLuma = source.Frame.CodedView.GetPlane(Av1Plane.Y);
        for (int y = 0; y < pilotLuma.Height; y++)
        {
            pilotLuma.DangerousGetRowSpan(y).CopyTo(sourceLuma.DangerousGetRowSpan(y));
        }

        for (int row = 0; row < 8; row++)
        {
            target.Slice(row * 8, 8).CopyTo(sourceLuma.DangerousGetRowSpan(TargetY + row).Slice(TargetX, 8));
        }

        ClearPlane(reconstruction.Luma);
        using Av1EncoderModeInfoBuffer modeInfo = new(Configuration.Default, Width, Height, disallow4x4AllFrames: true);
        Av1PictureControlSet pictureTemplate = CreatePicture(modeInfo, colorConfig, use128x128Superblock: false, QIndex);
        pictureTemplate.Sequence.SequenceHeader.EnableFilterIntra = true;
        pictureTemplate.Parent.FrameHeader.TransformMode = useSplitTransform
            ? Av1TransformMode.Select
            : Av1TransformMode.Largest;

        using Av1EncoderPictureBuffer picture = new(
            Configuration.Default,
            pictureTemplate.Sequence.SequenceHeader,
            pictureTemplate.Parent.FrameHeader,
            Width,
            Height,
            disallow4x4AllFrames: true);

        using Av1EncoderCoefficientBuffer coefficients = new(
            Configuration.Default,
            pictureTemplate.Sequence.SequenceHeader,
            Width,
            Height);

        using Av1EncoderSuperblockWorkspace superblockWorkspace = new(Configuration.Default);
        using Av1EncoderBlockWorkspace blockWorkspace = new(Configuration.Default);
        using Av1SymbolEncoder symbolEncoder = CreateTileSymbolEncoder(
            picture.Picture,
            TileBufferLength);

        Av1TileEncoder tileWriter = createWriter(
            symbolEncoder,
            source.Frame,
            reconstruction.Frame,
            picture.Picture,
            coefficients,
            superblockWorkspace,
            blockWorkspace);

        ref Av1MacroBlockModeInfo targetBlock = ref picture.Picture.GetMacroBlockModeInfo(new Point(2, 2));
        Assert.Equal(Av1PredictionMode.DC, targetBlock.Block.Mode);
        Assert.Equal(filterIntraMode, superblockWorkspace.FinalBlocks[3].FilterIntraMode);
        Assert.Equal(
            useSplitTransform ? Av1TransformSize.Size4x4 : Av1TransformSize.Size8x8,
            targetBlock.Block.TransformSize);

        int targetTransformIndex = (3 * TransformSize.GetSize2d()) /
            Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount;

        int targetTransformCount = useSplitTransform ? 4 : 1;
        Span<Av1EncoderTransformBlockState> targetStates = coefficients
            .GetTransformBlockSpan(0, Av1Plane.Y)
            .Slice(targetTransformIndex, targetTransformCount);

        foreach (Av1EncoderTransformBlockState targetState in targetStates)
        {
            if (useSplitTransform)
            {
                Assert.NotEqual((ushort)0, targetState.EndOfBlock);
            }
            else
            {
                Assert.Equal((ushort)0, targetState.EndOfBlock);
                Assert.Equal(Av1TransformType.DctDct, targetState.TransformType);
            }
        }

        Buffer2DRegion<TSample> actualLuma = reconstruction.Frame.CodedView.GetPlane(Av1Plane.Y);
        Assert.Equal(above, actualLuma.DangerousGetRowSpan(TargetY - 1).Slice(TargetX, 8));
        long reconstructionError = 0;
        for (int row = 0; row < 8; row++)
        {
            Assert.Equal(left[row], actualLuma.DangerousGetRowSpan(TargetY + row)[TargetX - 1]);
            ReadOnlySpan<TSample> targetRow = target.Slice(row * 8, 8);
            ReadOnlySpan<TSample> actualRow = actualLuma.DangerousGetRowSpan(TargetY + row).Slice(TargetX, 8);
            if (useSplitTransform)
            {
                for (int column = 0; column < targetRow.Length; column++)
                {
                    long difference = long.CreateChecked(targetRow[column]) - long.CreateChecked(actualRow[column]);
                    reconstructionError += difference * difference;
                }
            }
            else
            {
                Assert.Equal(targetRow, actualRow);
            }
        }

        if (useSplitTransform)
        {
            // The chosen transforms must reduce the source error below leaving the known residual entirely uncoded.
            long predictionOnlyError = 64L * 40 * 40 * sampleScale * sampleScale;
            Assert.InRange(reconstructionError, 1, predictionOnlyError - 1);

            byte[] payload = WriteCompleteTileObu(pictureTemplate, tileWriter, Width, Height);
            using Av1Decoder decoder = new(Configuration.Default);
            using Image<Rgba32> decoded = decoder.Decode<Rgba32>(payload);
            Assert.NotNull(decoder.FrameInfo);
            Av1BlockModeInfo decodedBlock = decoder.FrameInfo.GetModeInfoAt(new Point(2, 2));
            Assert.True(decodedBlock.UseFilterIntra);
            Assert.Equal(filterIntraMode, decodedBlock.FilterIntraMode);
            Assert.Equal(4, decodedBlock.GetTransformUnitCount(Av1Plane.Y));
            Assert.Equal(new Size(Width, Height), decoded.Size);

            string outputDirectory = Path.Combine(
                TestEnvironment.ActualOutputDirectoryFullPath,
                "Formats",
                "Heif",
                "Av1");

            Directory.CreateDirectory(outputDirectory);
            File.WriteAllBytes(
                Path.Combine(outputDirectory, $"encoder-filter-intra-transform-size-select-{bitDepth}b.obu"),
                payload);
        }

        Assert.NotEqual(0, pilotWriter.GetTileData(0).Length);
        Assert.NotEqual(0, tileWriter.GetTileData(0).Length);
    }

    [Fact]
    public void ProductionDirectionalModesConsumeAvailableExtendedEdges()
    {
        const int Width = 72;
        const int Height = 16;
        const int QIndex = 1;
        ObuColorConfig colorConfig = new()
        {
            IsMonochrome = true,
            SubSamplingX = true,
            SubSamplingY = true,
            BitDepth = Av1BitDepth.EightBit
        };

        using Av1EncoderFrameBuffer<byte> source = new(
            Configuration.Default,
            Width,
            Height,
            8,
            Av1ColorFormat.Yuv400,
            0,
            0,
            lumaBorder: 64);

        using Av1EncoderFrameBuffer<byte> reconstruction = new(
            Configuration.Default,
            Width,
            Height,
            8,
            Av1ColorFormat.Yuv400,
            0,
            0,
            lumaBorder: 64);

        Buffer2DRegion<byte> sourcePlane = source.Frame.CodedView.GetPlane(Av1Plane.Y);
        FillPlane(sourcePlane, (byte)128);

        Span<byte> aboveStorage = stackalloc byte[17];
        Span<byte> above = aboveStorage[1..];
        Span<byte> leftStorage = stackalloc byte[17];
        Span<byte> left = leftStorage[1..];
        aboveStorage[0] = 128;
        leftStorage[0] = 128;
        for (int i = 0; i < 16; i++)
        {
            above[i] = (byte)(32 + (i * 12));
            left[i] = (byte)(224 - (i * 12));
        }

        Span<byte> topRightTarget = stackalloc byte[64];
        Span<byte> bottomLeftTarget = stackalloc byte[64];
        Span<byte> predictionScratch = stackalloc byte[64];
        Av1DirectionalIntraPredictor.Predict(
            topRightTarget,
            8,
            Av1TransformSize.Size8x8,
            above,
            left,
            false,
            false,
            45,
            predictionScratch);

        Av1DirectionalIntraPredictor.Predict(
            bottomLeftTarget,
            8,
            Av1TransformSize.Size8x8,
            above,
            left,
            false,
            false,
            203,
            predictionScratch);

        // The lower-left target consumes top-right samples from the already reconstructed row above.
        // The upper-right superblock target consumes bottom-left samples from the completed superblock to its left.
        for (int y = 0; y < Height; y++)
        {
            Span<byte> row = sourcePlane.DangerousGetRowSpan(y);
            if (y < 8)
            {
                above.CopyTo(row[..16]);
                bottomLeftTarget.Slice(y * 8, 8).CopyTo(row.Slice(64, 8));
            }
            else
            {
                topRightTarget.Slice((y - 8) * 8, 8).CopyTo(row[..8]);
            }

            row.Slice(56, 8).Fill(left[y]);
        }

        ClearPlane(reconstruction.Luma);
        using Av1EncoderModeInfoBuffer modeInfo = new(Configuration.Default, Width, Height, disallow4x4AllFrames: true);
        Av1PictureControlSet pictureTemplate = CreatePicture(modeInfo, colorConfig, use128x128Superblock: false, QIndex);
        using Av1EncoderPictureBuffer picture = new(
            Configuration.Default,
            pictureTemplate.Sequence.SequenceHeader,
            pictureTemplate.Parent.FrameHeader,
            Width,
            Height,
            disallow4x4AllFrames: true);

        using Av1EncoderCoefficientBuffer coefficients = new(
            Configuration.Default,
            pictureTemplate.Sequence.SequenceHeader,
            Width,
            Height);

        using Av1EncoderSuperblockWorkspace superblockWorkspace = new(Configuration.Default);
        using Av1EncoderBlockWorkspace blockWorkspace = new(Configuration.Default);
        using Av1SymbolEncoder symbolEncoder = CreateTileSymbolEncoder(
            picture.Picture,
            2048);

        Av1TileEncoder tileWriter = new(
            symbolEncoder,
            source.Frame,
            reconstruction.Frame,
            picture.Picture,
            coefficients,
            superblockWorkspace,
            blockWorkspace,
            effort: 5);

        ref Av1MacroBlockModeInfo topRightBlock = ref picture.Picture.GetMacroBlockModeInfo(new Point(0, 2));
        ref Av1MacroBlockModeInfo bottomLeftBlock = ref picture.Picture.GetMacroBlockModeInfo(new Point(16, 0));
        Assert.Equal(Av1PredictionMode.Directional45Degrees, topRightBlock.Block.Mode);
        Assert.Equal(Av1PredictionMode.Directional203Degrees, bottomLeftBlock.Block.Mode);
        Assert.NotEqual(0, tileWriter.GetTileData(0).Length);
    }

    [Fact]
    public void ProductionTileSelectsIntraBlockCopyByFullRateDistortion()
    {
        VerifyProductionTileSelectsIntraBlockCopy(
            Av1BitDepth.EightBit,
            8,
            static value => (byte)value,
            static (writer, source, reconstruction, picture, coefficients, superblockWorkspace, blockWorkspace) =>
                new Av1TileEncoder(
                    writer,
                    source,
                    reconstruction,
                    picture,
                    coefficients,
                    superblockWorkspace,
                    blockWorkspace,
                    effort: 5));

        VerifyProductionTileSelectsIntraBlockCopy(
            Av1BitDepth.TwelveBit,
            12,
            static value => (ushort)(value << 4),
            static (writer, source, reconstruction, picture, coefficients, superblockWorkspace, blockWorkspace) =>
                new Av1TileEncoder(
                    writer,
                    source,
                    reconstruction,
                    picture,
                    coefficients,
                    superblockWorkspace,
                    blockWorkspace,
                    effort: 5));
    }

    private static void VerifyProductionTileSelectsIntraBlockCopy<TSample>(
        Av1BitDepth bitDepth,
        int bitDepthValue,
        SampleFactory<TSample> createSample,
        TileWriterFactory<TSample> createTileWriter)
        where TSample : unmanaged
    {
        const int Width = 328;
        const int Height = 8;
        const int QIndex = 1;
        const int TileBufferLength = 4096;
        const int ReferenceColumn = 0;
        const int TargetColumn = 320;
        ObuColorConfig colorConfig = new()
        {
            IsMonochrome = true,
            SubSamplingX = true,
            SubSamplingY = true,
            BitDepth = bitDepth
        };

        using Av1EncoderFrameBuffer<TSample> source = new(
            Configuration.Default,
            Width,
            Height,
            bitDepthValue,
            Av1ColorFormat.Yuv400,
            0,
            0,
            lumaBorder: 64);

        using Av1EncoderFrameBuffer<TSample> reconstruction = new(
            Configuration.Default,
            Width,
            Height,
            bitDepthValue,
            Av1ColorFormat.Yuv400,
            0,
            0,
            lumaBorder: 64);

        Buffer2DRegion<TSample> sourcePlane = source.Frame.CodedView.GetPlane(Av1Plane.Y);
        for (int row = 0; row < Height; row++)
        {
            Span<TSample> sourceRow = sourcePlane.DangerousGetRowSpan(row);
            for (int column = 0; column < Width; column++)
            {
                sourceRow[column] = createSample(17 + (((column * 29) + (row * 43)) % 211));
            }

            for (int column = 0; column < 8; column++)
            {
                // The repeated high-contrast block has one legal hash match five completed 64-pixel regions earlier.
                TSample sample = createSample(((column * 73) + (row * 109) + (((column + row) & 1) * 127)) & 255);
                sourceRow[ReferenceColumn + column] = sample;
                sourceRow[TargetColumn + column] = sample;
            }
        }

        ClearPlane(reconstruction.Luma);
        using Av1EncoderModeInfoBuffer modeInfo = new(
            Configuration.Default,
            Width,
            Height,
            disallow4x4AllFrames: true);

        Av1PictureControlSet pictureTemplate = CreatePicture(
            modeInfo,
            colorConfig,
            use128x128Superblock: false,
            QIndex);

        pictureTemplate.Parent.FrameHeader.AllowScreenContentTools = true;
        pictureTemplate.Parent.FrameHeader.AllowIntraBlockCopy = true;
        pictureTemplate.Parent.FrameHeader.FrameSize.FrameWidth = Width;
        pictureTemplate.Parent.FrameHeader.FrameSize.FrameHeight = Height;
        using Av1EncoderPictureBuffer picture = new(
            Configuration.Default,
            pictureTemplate.Sequence.SequenceHeader,
            pictureTemplate.Parent.FrameHeader,
            Width,
            Height,
            disallow4x4AllFrames: true);

        using Av1EncoderCoefficientBuffer coefficients = new(
            Configuration.Default,
            pictureTemplate.Sequence.SequenceHeader,
            Width,
            Height);

        using Av1EncoderSuperblockWorkspace superblockWorkspace = new(Configuration.Default);
        using Av1EncoderBlockWorkspace blockWorkspace = new(Configuration.Default);
        using Av1SymbolEncoder symbolEncoder = CreateTileSymbolEncoder(
            picture.Picture,
            TileBufferLength);

        Av1TileEncoder tileWriter = createTileWriter(
            symbolEncoder,
            source.Frame,
            reconstruction.Frame,
            picture.Picture,
            coefficients,
            superblockWorkspace,
            blockWorkspace);

        Point targetModeInfoPosition = new(TargetColumn >> Av1Constants.ModeInfoSizeLog2, 0);
        ref Av1MacroBlockModeInfo targetMode = ref picture.Picture.GetMacroBlockModeInfo(targetModeInfoPosition);
        Assert.True(targetMode.Block.UseIntraBlockCopy);
        Assert.Equal(Av1PredictionMode.DC, targetMode.Block.Mode);
        Assert.Equal(Av1ChromaPredictionMode.DC, targetMode.Block.UvMode);
        var displacementVector = picture.Picture.GetDisplacementVector(targetModeInfoPosition);
        Assert.Equal(0, displacementVector.Row);
        Assert.Equal((ReferenceColumn - TargetColumn) * 8, displacementVector.Column);
        Assert.Equal(Av1FilterIntraMode.AllFilterIntraModes, superblockWorkspace.FinalBlocks[0].FilterIntraMode);
        Assert.Equal(0, superblockWorkspace.PaletteInfo.PaletteSizes[0]);
        Assert.NotEqual(0, tileWriter.GetTileData(0).Length);
    }

    [Fact]
    public void ProductionTileRetainsHalfSampleChromaIntraBlockCopy()
    {
        const int Width = 328;
        const int Height = 8;
        const int QIndex = 1;
        const int ReferenceColumn = 1;
        const int TargetColumn = 320;
        ObuColorConfig colorConfig = new()
        {
            IsMonochrome = false,
            SubSamplingX = true,
            SubSamplingY = true,
            BitDepth = Av1BitDepth.EightBit
        };

        using Av1EncoderFrameBuffer<byte> source = new(
            Configuration.Default,
            Width,
            Height,
            8,
            Av1ColorFormat.Yuv420,
            1,
            1,
            lumaBorder: 64);

        using Av1EncoderFrameBuffer<byte> reconstruction = new(
            Configuration.Default,
            Width,
            Height,
            8,
            Av1ColorFormat.Yuv420,
            1,
            1,
            lumaBorder: 64);

        Buffer2DRegion<byte> lumaSource = source.Frame.CodedView.GetPlane(Av1Plane.Y);
        for (int row = 0; row < Height; row++)
        {
            Span<byte> lumaRow = lumaSource.DangerousGetRowSpan(row);
            for (int column = 0; column < Width; column++)
            {
                lumaRow[column] = (byte)(23 + (((column * 31) + (row * 47)) % 197));
            }

            for (int column = 0; column < 8; column++)
            {
                byte sample = (byte)(((column * 79) + (row * 113) + (((column + row) & 1) * 127)) & 255);
                lumaRow[ReferenceColumn + column] = sample;
                lumaRow[TargetColumn + column] = sample;
            }
        }

        int chromaTargetColumn = TargetColumn >> 1;
        Buffer2DRegion<byte> blueSource = source.Frame.CodedView.GetPlane(Av1Plane.U);
        Buffer2DRegion<byte> redSource = source.Frame.CodedView.GetPlane(Av1Plane.V);
        for (int row = 0; row < Height >> 1; row++)
        {
            Span<byte> blueRow = blueSource.DangerousGetRowSpan(row);
            Span<byte> redRow = redSource.DangerousGetRowSpan(row);
            for (int column = 0; column < Width >> 1; column++)
            {
                blueRow[column] = (byte)(32 + (((column * 17) + (row * 29)) % 160));
                redRow[column] = (byte)(40 + (((column * 23) + (row * 37)) % 152));
            }

            for (int column = 0; column < 4; column++)
            {
                // An odd luma displacement maps 4:2:0 chroma between adjacent reference samples.
                blueRow[chromaTargetColumn + column] = (byte)((blueRow[column] + blueRow[column + 1] + 1) >> 1);
                redRow[chromaTargetColumn + column] = (byte)((redRow[column] + redRow[column + 1] + 1) >> 1);
            }
        }

        ClearPlane(reconstruction.Luma);
        ClearPlane(Assert.IsType<Buffer2D<byte>>(reconstruction.ChromaBlue));
        ClearPlane(Assert.IsType<Buffer2D<byte>>(reconstruction.ChromaRed));
        using Av1EncoderModeInfoBuffer modeInfo = new(
            Configuration.Default,
            Width,
            Height,
            disallow4x4AllFrames: true);

        Av1PictureControlSet pictureTemplate = CreatePicture(
            modeInfo,
            colorConfig,
            use128x128Superblock: false,
            QIndex);

        pictureTemplate.Parent.FrameHeader.AllowScreenContentTools = true;
        pictureTemplate.Parent.FrameHeader.AllowIntraBlockCopy = true;
        pictureTemplate.Parent.FrameHeader.FrameSize.FrameWidth = Width;
        pictureTemplate.Parent.FrameHeader.FrameSize.FrameHeight = Height;
        using Av1EncoderPictureBuffer picture = new(
            Configuration.Default,
            pictureTemplate.Sequence.SequenceHeader,
            pictureTemplate.Parent.FrameHeader,
            Width,
            Height,
            disallow4x4AllFrames: true);

        using Av1EncoderCoefficientBuffer coefficients = new(
            Configuration.Default,
            pictureTemplate.Sequence.SequenceHeader,
            Width,
            Height);

        using Av1EncoderSuperblockWorkspace superblockWorkspace = new(Configuration.Default);
        using Av1EncoderBlockWorkspace blockWorkspace = new(Configuration.Default);
        using Av1SymbolEncoder symbolEncoder = CreateTileSymbolEncoder(
            picture.Picture,
            4096);

        Av1TileEncoder tileWriter = new(
            symbolEncoder,
            source.Frame,
            reconstruction.Frame,
            picture.Picture,
            coefficients,
            superblockWorkspace,
            blockWorkspace,
            effort: 5);

        Point targetModeInfoPosition = new(TargetColumn >> Av1Constants.ModeInfoSizeLog2, 0);
        ref Av1MacroBlockModeInfo targetMode = ref picture.Picture.GetMacroBlockModeInfo(targetModeInfoPosition);
        Assert.True(targetMode.Block.UseIntraBlockCopy);
        var displacementVector = picture.Picture.GetDisplacementVector(targetModeInfoPosition);
        Assert.Equal(0, displacementVector.Row);
        Assert.Equal((ReferenceColumn - TargetColumn) * 8, displacementVector.Column);
        Assert.Equal(8, displacementVector.Column & 15);
        Av1TransformSetType interTransformSet = Av1SymbolContextHelper.GetExtendedTransformSetType(
            Av1TransformSize.Size4x4,
            isInter: true,
            useReducedSet: false);

        Assert.True(coefficients.GetTransformBlockSpan(5, Av1Plane.U)[0].TransformType.IsExtendedSetUsed(interTransformSet));
        Assert.True(coefficients.GetTransformBlockSpan(5, Av1Plane.V)[0].TransformType.IsExtendedSetUsed(interTransformSet));
        Assert.NotEqual(
            (byte)0,
            reconstruction.Frame.CodedView.GetPlane(Av1Plane.U).DangerousGetRowSpan(0)[chromaTargetColumn]);

        Assert.NotEqual(
            (byte)0,
            reconstruction.Frame.CodedView.GetPlane(Av1Plane.V).DangerousGetRowSpan(0)[chromaTargetColumn]);

        Assert.NotEqual(0, tileWriter.GetTileData(0).Length);
    }

    [Fact]
    public void TileWriterMapsClippedRasterTraversalToEverySuperblockCoefficientSegment()
    {
        const int Width = 72;
        const int Height = 72;
        const int QIndex = 53;
        ObuColorConfig colorConfig = new()
        {
            IsMonochrome = true,
            SubSamplingX = true,
            SubSamplingY = true,
            BitDepth = Av1BitDepth.EightBit
        };

        ObuTileGroupHeader tiles = new()
        {
            TileColumnCount = 1,
            TileRowCount = 1
        };

        int modeInfoColumnCount = Width >> Av1Constants.ModeInfoSizeLog2;
        int modeInfoRowCount = Height >> Av1Constants.ModeInfoSizeLog2;
        tiles.TileColumnStartModeInfo[1] = modeInfoColumnCount;
        tiles.TileRowStartModeInfo[1] = modeInfoRowCount;
        ObuSequenceHeader sequenceHeader = new()
        {
            Use128x128Superblock = false,
            ColorConfig = colorConfig
        };

        ObuFrameHeader frameHeader = new()
        {
            ModeInfoColumnCount = modeInfoColumnCount,
            ModeInfoRowCount = modeInfoRowCount,
            TilesInfo = tiles
        };

        frameHeader.QuantizationParameters.BaseQIndex = QIndex;
        frameHeader.QuantizationParameters.QIndex.Fill(QIndex);
        using Av1EncoderFrameBuffer<byte> source = new(
            Configuration.Default,
            Width,
            Height,
            8,
            Av1ColorFormat.Yuv400,
            0,
            0,
            lumaBorder: 64);

        using Av1EncoderFrameBuffer<byte> reconstruction = new(
            Configuration.Default,
            Width,
            Height,
            8,
            Av1ColorFormat.Yuv400,
            0,
            0,
            lumaBorder: 64);

        FillPlane(source.Frame.CodedView.GetPlane(Av1Plane.Y), 251, 29);
        ClearPlane(reconstruction.Luma);
        using Av1EncoderPictureBuffer picture = new(
            Configuration.Default,
            sequenceHeader,
            frameHeader,
            Width,
            Height,
            disallow4x4AllFrames: true);

        using Av1EncoderCoefficientBuffer coefficients = new(
            Configuration.Default,
            sequenceHeader,
            Width,
            Height);

        using Av1EncoderSuperblockWorkspace superblockWorkspace = new(Configuration.Default);
        using Av1EncoderBlockWorkspace blockWorkspace = new(Configuration.Default);
        using Av1SymbolEncoder symbolEncoder = CreateTileSymbolEncoder(
            picture.Picture,
            4096);

        Av1TileEncoder tileWriter = new(
            symbolEncoder,
            source.Frame,
            reconstruction.Frame,
            picture.Picture,
            coefficients,
            superblockWorkspace,
            blockWorkspace,
            effort: 5);

        Assert.Equal(4, coefficients.SuperblockCount);
        bool usesNonDctTransform = false;
        for (int superblockIndex = 0; superblockIndex < coefficients.SuperblockCount; superblockIndex++)
        {
            Span<Av1EncoderTransformBlockState> transformBlocks =
                coefficients.GetTransformBlockSpan(superblockIndex, Av1Plane.Y);

            Assert.NotEqual((ushort)0, transformBlocks[0].EndOfBlock);
            foreach (Av1EncoderTransformBlockState transformBlock in transformBlocks)
            {
                usesNonDctTransform |=
                    transformBlock.EndOfBlock > 0 && transformBlock.TransformType != Av1TransformType.DctDct;
            }
        }

        Assert.True(usesNonDctTransform);
        Assert.NotEqual(
            (byte)0,
            reconstruction.Frame.CodedView.GetPlane(Av1Plane.Y).DangerousGetRowSpan(Height - 1)[Width - 1]);

        ref Av1MacroBlockModeInfo bottomRight = ref picture.Picture.GetMacroBlockModeInfo(new Point(16, 16));
        Assert.Equal(Av1BlockSize.Block8x8, bottomRight.Block.BlockSize);
        Assert.NotEqual(0, tileWriter.GetTileData(0).Length);
    }

    /// <summary>
    /// Verifies that mixed partition trials and final writing retain the decoder's reconstruction order.
    /// </summary>
    [Theory]
    [InlineData(false, 32, false)]
    [InlineData(true, 32, false)]
    [InlineData(false, 56, false)]
    [InlineData(true, 56, false)]
    [InlineData(false, 32, true)]
    [InlineData(true, 32, true)]
    [InlineData(false, 56, true)]
    [InlineData(true, 56, true)]
    public void ProductionMixedPartitionsPreserveReconstructionOrder(bool transpose, int size, bool enableIntraEdgeFilter)
    {
        const int QIndex = 4;
        ObuColorConfig colorConfig = new()
        {
            IsMonochrome = true,
            SubSamplingX = true,
            SubSamplingY = true,
            BitDepth = Av1BitDepth.EightBit
        };

        using Av1EncoderFrameBuffer<byte> source = new(Configuration.Default, size, size, 8, Av1ColorFormat.Yuv400, 0, 0, lumaBorder: 64);
        using Av1EncoderFrameBuffer<byte> reconstruction = new(Configuration.Default, size, size, 8, Av1ColorFormat.Yuv400, 0, 0, lumaBorder: 64);
        Buffer2DRegion<byte> sourcePlane = source.Frame.CodedView.GetPlane(Av1Plane.Y);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // The lower-right quadrant contains two different square surfaces beside one vertical
                // surface. Transposition exercises the corresponding horizontal reconstruction order.
                int value = x < 16 && y < 16 ? 128
                    : y < 16 ? 16 + ((x - 16) * 12)
                    : x < 16 ? 16 + ((y - 16) * 12)
                    : x >= 24 ? 16 + ((x - 16) * 12)
                    : y < 24 ? 16 + ((x + y - 31) * 12)
                    : 16 + ((x - 8) * 12);

                sourcePlane.DangerousGetRowSpan(transpose ? x : y)[transpose ? y : x] = (byte)value;
            }
        }

        ClearPlane(reconstruction.Luma);
        using Av1EncoderModeInfoBuffer modeInfo = new(Configuration.Default, size, size, disallow4x4AllFrames: false);
        Av1PictureControlSet template = CreatePicture(modeInfo, colorConfig, use128x128Superblock: false, QIndex);
        template.Sequence.SequenceHeader.EnableIntraEdgeFilter = enableIntraEdgeFilter;
        using Av1EncoderPictureBuffer picture = new(
            Configuration.Default, template.Sequence.SequenceHeader, template.Parent.FrameHeader, size, size, disallow4x4AllFrames: false);

        using Av1EncoderCoefficientBuffer coefficients = new(Configuration.Default, template.Sequence.SequenceHeader, size, size);
        using Av1EncoderSuperblockWorkspace superblockWorkspace = new(Configuration.Default);
        using Av1EncoderBlockWorkspace blockWorkspace = new(Configuration.Default);
        using Av1SymbolEncoder symbolEncoder = CreateTileSymbolEncoder(picture.Picture, 8192);
        Av1TileEncoder tileWriter = new(
            symbolEncoder, source.Frame, reconstruction.Frame, picture.Picture, coefficients, superblockWorkspace, blockWorkspace, effort: 9);

        byte[] payload = WriteCompleteTileObu(picture.Picture, tileWriter, size, size);
        using Av1Decoder decoder = new(Configuration.Default);
        decoder.DecodeSequenceReference(payload, null, null);
        Assert.Equal(enableIntraEdgeFilter, Assert.IsType<ObuSequenceHeader>(decoder.SequenceHeader).EnableIntraEdgeFilter);
        Av1FrameInfo decodedInfo = Assert.IsType<Av1FrameInfo>(decoder.FrameInfo);
        Av1FrameBuffer<byte> decodedFrame = Assert.IsType<Av1FrameBuffer<byte>>(decoder.FrameBuffer);
        Buffer2DRegion<byte> decodedPlane = decodedFrame.DeriveBlockPointer(Av1Plane.Y, 0, 0);
        Buffer2DRegion<byte> retainedPlane = reconstruction.Frame.CodedView.GetPlane(Av1Plane.Y);
        bool hasMixedPartition = false;
        for (int y = 0; y < size; y++)
        {
            Assert.Equal(retainedPlane.DangerousGetRowSpan(y).ToArray(), decodedPlane.DangerousGetRowSpan(y).ToArray());
            for (int x = 0; x < size; x += 4)
            {
                Point position = new(x >> 2, y >> 2);
                Av1PartitionType partition = decodedInfo.GetModeInfoAt(position).PartitionType;

                // Interior 4x4 entries alias the block origin through the live grid; unused allocation
                // slots may still contain rejected trial data and are not retained block state.
                int allocationIndex = picture.Picture.ModeInfoGrid.Span[(position.Y * picture.Picture.ModeInfoStride) + position.X];
                Assert.Equal(partition, picture.Picture.ModeInfoAllocation.Span[allocationIndex].Block.PartitionType);
                hasMixedPartition |= partition is Av1PartitionType.HorizontalA or Av1PartitionType.HorizontalB
                    or Av1PartitionType.VerticalA or Av1PartitionType.VerticalB;
            }
        }

        Assert.True(hasMixedPartition);
        string directory = Path.Combine(
            TestEnvironment.ActualOutputDirectoryFullPath, "Heif", "Av1", nameof(this.ProductionMixedPartitionsPreserveReconstructionOrder));

        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, $"{size}-{transpose}-{enableIntraEdgeFilter}.obu"), payload);
        using FileStream raw = File.Create(Path.Combine(directory, $"{size}-{transpose}-{enableIntraEdgeFilter}.retained.yuv"));
        for (int y = 0; y < size; y++)
        {
            raw.Write(retainedPlane.DangerousGetRowSpan(y));
        }
    }

    [Theory]
    [InlineData((int)Av1ColorFormat.Yuv400)]
    [InlineData((int)Av1ColorFormat.Yuv420)]
    [InlineData((int)Av1ColorFormat.Yuv422)]
    [InlineData((int)Av1ColorFormat.Yuv444)]
    public void ProductionDeblockingPreservesEightBitReconstruction(int colorFormatValue)
        => VerifyProductionDeblocking<byte>(
            colorFormatValue,
            8,
            static (writer, source, reconstruction, picture, coefficients, superblockWorkspace, blockWorkspace) =>
                new(writer, source, reconstruction, picture, coefficients, superblockWorkspace, blockWorkspace, effort: 5));

    [Theory]
    [InlineData((int)Av1ColorFormat.Yuv400, 10)]
    [InlineData((int)Av1ColorFormat.Yuv400, 12)]
    [InlineData((int)Av1ColorFormat.Yuv420, 10)]
    [InlineData((int)Av1ColorFormat.Yuv420, 12)]
    [InlineData((int)Av1ColorFormat.Yuv422, 10)]
    [InlineData((int)Av1ColorFormat.Yuv422, 12)]
    [InlineData((int)Av1ColorFormat.Yuv444, 10)]
    [InlineData((int)Av1ColorFormat.Yuv444, 12)]
    public void ProductionDeblockingPreservesHighBitDepthReconstruction(int colorFormatValue, int bitDepth)
        => VerifyProductionDeblocking<ushort>(
            colorFormatValue,
            bitDepth,
            static (writer, source, reconstruction, picture, coefficients, superblockWorkspace, blockWorkspace) =>
                new(writer, source, reconstruction, picture, coefficients, superblockWorkspace, blockWorkspace, effort: 5));

    /// <summary>
    /// Verifies retained reconstruction and exports the same encoded stream for an independent decoder comparison.
    /// </summary>
    /// <typeparam name="TSample">The component sample type.</typeparam>
    /// <param name="colorFormatValue">The component layout.</param>
    /// <param name="bitDepth">The component precision.</param>
    /// <param name="createWriter">The typed production tile constructor.</param>
    private static void VerifyProductionDeblocking<TSample>(int colorFormatValue, int bitDepth, TileWriterFactory<TSample> createWriter)
        where TSample : unmanaged, IBinaryInteger<TSample>
    {
        const int Width = 33;
        const int Height = 137;
        const int QIndex = 37;
        Av1ColorFormat colorFormat = (Av1ColorFormat)colorFormatValue;
        ObuColorConfig colorConfig = new()
        {
            IsMonochrome = colorFormat == Av1ColorFormat.Yuv400,
            SubSamplingX = colorFormat is Av1ColorFormat.Yuv400 or Av1ColorFormat.Yuv420 or Av1ColorFormat.Yuv422,
            SubSamplingY = colorFormat is Av1ColorFormat.Yuv400 or Av1ColorFormat.Yuv420,
            BitDepth = bitDepth == 8 ? Av1BitDepth.EightBit : bitDepth == 10 ? Av1BitDepth.TenBit : Av1BitDepth.TwelveBit
        };

        using Av1EncoderFrameBuffer<TSample> source = new(Configuration.Default, Width, Height, bitDepth, colorFormat, 1, 1, lumaBorder: 64);
        using Av1EncoderFrameBuffer<TSample> reconstruction = new(Configuration.Default, Width, Height, bitDepth, colorFormat, 1, 1, lumaBorder: 64);
        int planeCount = colorConfig.PlaneCount;
        TSample[][] unfiltered = new TSample[planeCount][];
        for (int planeIndex = 0; planeIndex < planeCount; planeIndex++)
        {
            Buffer2DRegion<TSample> plane = source.Frame.View.GetPlane((Av1Plane)planeIndex);
            unfiltered[planeIndex] = new TSample[plane.Width * plane.Height];
            for (int y = 0; y < plane.Height; y++)
            {
                Span<TSample> row = plane.DangerousGetRowSpan(y);
                for (int x = 0; x < row.Length; x++)
                {
                    // Small discontinuities at coding boundaries activate deblocking. Odd dimensions and a
                    // height above 128 exercise chroma ownership, coded padding, and intersecting row bands.
                    int value = 96 + (4 * ((x / 8) + (y / 8))) + (planeIndex * 8);
                    row[x] = TSample.CreateChecked(value << (bitDepth - 8));
                }
            }
        }

        source.Frame.ExtendBorders();
        using Av1EncoderModeInfoBuffer modeInfo = new(Configuration.Default, Width, Height, disallow4x4AllFrames: true);
        Av1PictureControlSet template = CreatePicture(modeInfo, colorConfig, use128x128Superblock: false, QIndex);
        ObuFrameHeader header = template.Parent.FrameHeader;
        header.FrameSize.FrameWidth = Width;
        header.FrameSize.FrameHeight = Height;
        using Av1EncoderPictureBuffer picture = new(
            Configuration.Default, template.Sequence.SequenceHeader, header, Width, Height, disallow4x4AllFrames: true);

        using Av1EncoderCoefficientBuffer coefficients = new(Configuration.Default, template.Sequence.SequenceHeader, Width, Height);
        using Av1EncoderSuperblockWorkspace superblockWorkspace = new(Configuration.Default);
        using Av1EncoderBlockWorkspace blockWorkspace = new(Configuration.Default);
        using Av1SymbolEncoder symbolEncoder = CreateTileSymbolEncoder(picture.Picture, 8192);
        _ = createWriter(symbolEncoder, source.Frame, reconstruction.Frame, picture.Picture, coefficients, superblockWorkspace, blockWorkspace);
        for (int planeIndex = 0; planeIndex < planeCount; planeIndex++)
        {
            Buffer2DRegion<TSample> plane = reconstruction.Frame.View.GetPlane((Av1Plane)planeIndex);
            for (int y = 0; y < plane.Height; y++)
            {
                plane.DangerousGetRowSpan(y).CopyTo(unfiltered[planeIndex].AsSpan(y * plane.Width, plane.Width));
            }
        }

        header.LoopFilterParameters.FilterLevel[0] = 63;
        header.LoopFilterParameters.FilterLevel[1] = 37;
        header.LoopFilterParameters.FilterLevelU = 31;
        header.LoopFilterParameters.FilterLevelV = 47;
        header.LoopFilterParameters.SharpnessLevel = 3;
        header.LoopFilterParameters.ReferenceDeltaModeEnabled = true;
        picture.Reset(header);
        Av1TileEncoder tileWriter = createWriter(
            symbolEncoder, source.Frame, reconstruction.Frame, picture.Picture, coefficients, superblockWorkspace, blockWorkspace);

        byte[] payload = WriteCompleteTileObu(picture.Picture, tileWriter, Width, Height);
        using Av1Decoder decoder = new(Configuration.Default);
        using Av1FrameBuffer<byte> decodedFrame = decoder.DecodeFrameBuffer(payload, null, null, out _);
        int changedSamples = 0;
        string directory = Path.Combine(TestEnvironment.ActualOutputDirectoryFullPath, "Heif", "Av1", "ProductionDeblocking");
        Directory.CreateDirectory(directory);
        string name = $"{bitDepth}-{colorFormat}";
        File.WriteAllBytes(Path.Combine(directory, name + ".obu"), payload);
        using FileStream raw = File.Create(Path.Combine(directory, name + ".retained.yuv"));
        for (int planeIndex = 0; planeIndex < planeCount; planeIndex++)
        {
            Av1Plane plane = (Av1Plane)planeIndex;
            Buffer2DRegion<TSample> retained = reconstruction.Frame.View.GetPlane(plane);
            int subX = plane == Av1Plane.Y ? 0 : reconstruction.Frame.ChromaSubsamplingX;
            int subY = plane == Av1Plane.Y ? 0 : reconstruction.Frame.ChromaSubsamplingY;
            Buffer2DRegion<byte> decoded = decodedFrame.DeriveBlockPointer(plane, subX, subY);
            for (int y = 0; y < retained.Height; y++)
            {
                ReadOnlySpan<TSample> row = retained.DangerousGetRowSpan(y);
                ReadOnlySpan<TSample> decodedRow = MemoryMarshal.Cast<byte, TSample>(decoded.DangerousGetRowSpan(y));
                Assert.Equal(row, decodedRow);
                for (int x = 0; x < row.Length; x++)
                {
                    changedSamples += row[x] != unfiltered[planeIndex][(y * retained.Width) + x] ? 1 : 0;
                }

                raw.Write(MemoryMarshal.AsBytes(row));
            }
        }

        Assert.True(changedSamples > 0);
    }

    private static byte[] WriteCompleteTileObu(
        Av1PictureControlSet pictureTemplate,
        IAv1TileWriter tileWriter,
        int width,
        int height)
    {
        // Tile fixtures initialize only entropy state. Complete the same still-picture headers as the frame
        // encoder before serializing so independent decoders validate the real OBU syntax.
        ObuSequenceHeader sequenceHeader = pictureTemplate.Sequence.SequenceHeader;
        ObuColorConfig colorConfig = sequenceHeader.ColorConfig;
        Av1ColorFormat colorFormat = colorConfig.GetColorFormat();
        sequenceHeader.IsStillPicture = true;
        sequenceHeader.IsReducedStillPictureHeader = true;
        sequenceHeader.SequenceProfile = colorConfig.BitDepth == Av1BitDepth.TwelveBit ||
            colorFormat == Av1ColorFormat.Yuv422
                ? ObuSequenceProfile.Professional
                : colorFormat == Av1ColorFormat.Yuv444
                    ? ObuSequenceProfile.High
                    : ObuSequenceProfile.Main;

        sequenceHeader.OperatingPoint = [new ObuOperatingPoint { SequenceLevelIndex = 31 }];
        sequenceHeader.FrameWidthBits = width > 1 ? Av1Math.MostSignificantBit((uint)(width - 1)) + 1 : 1;
        sequenceHeader.FrameHeightBits = height > 1 ? Av1Math.MostSignificantBit((uint)(height - 1)) + 1 : 1;
        sequenceHeader.MaxFrameWidth = width;
        sequenceHeader.MaxFrameHeight = height;
        sequenceHeader.ForceScreenContentTools = 2;
        sequenceHeader.ForceIntegerMotionVector = 2;

        ObuFrameHeader frameHeader = pictureTemplate.Parent.FrameHeader;
        frameHeader.FrameType = ObuFrameType.KeyFrame;
        frameHeader.ShowFrame = true;
        frameHeader.ErrorResilientMode = true;
        frameHeader.RefreshFrameFlags = byte.MaxValue;
        frameHeader.DisableFrameEndUpdateCdf = true;
        frameHeader.FrameSize = new ObuFrameSize
        {
            FrameWidth = width,
            FrameHeight = height,
            SuperResolutionDenominator = Av1Constants.ScaleNumerator,
            SuperResolutionUpscaledWidth = width,
            RenderWidth = width,
            RenderHeight = height
        };

        frameHeader.TilesInfo.HasUniformTileSpacing = true;
        using MemoryStream stream = new();
        using ObuWriter obuWriter = new(Configuration.Default);
        obuWriter.WriteSequenceFrame(
            stream,
            sequenceHeader,
            frameHeader,
            tileWriter);

        return stream.ToArray();
    }

    private static Av1PictureControlSet CreatePicture(
        Av1EncoderModeInfoBuffer modeInfo,
        ObuColorConfig colorConfig,
        bool use128x128Superblock,
        int qIndex)
    {
        ObuTileGroupHeader tiles = new()
        {
            TileColumnCount = 1,
            TileRowCount = 1
        };

        tiles.TileColumnStartModeInfo[1] = modeInfo.ModeInfoColumnCount;
        tiles.TileRowStartModeInfo[1] = modeInfo.ModeInfoRowCount;
        ObuSequenceHeader sequenceHeader = new()
        {
            Use128x128Superblock = use128x128Superblock,
            ColorConfig = colorConfig
        };

        ObuFrameHeader frameHeader = new()
        {
            ModeInfoColumnCount = modeInfo.ModeInfoColumnCount,
            ModeInfoRowCount = modeInfo.ModeInfoRowCount,
            TilesInfo = tiles
        };

        frameHeader.QuantizationParameters.BaseQIndex = qIndex;
        frameHeader.QuantizationParameters.QIndex.Fill(qIndex);
        return new Av1PictureControlSet
        {
            PartitionContexts = [],
            LuminanceDcSignLevelCoefficientNeighbors = [],
            CrDcSignLevelCoefficientNeighbors = [],
            CbDcSignLevelCoefficientNeighbors = [],
            TransformFunctionContexts = [],
            Sequence = new Av1SequenceControlSet { SequenceHeader = sequenceHeader },
            Parent = new Av1PictureParentControlSet
            {
                Common = new Av1EncoderCommon
                {
                    ModeInfoColumnCount = modeInfo.ModeInfoColumnCount,
                    ModeInfoRowCount = modeInfo.ModeInfoRowCount,
                    ModeInfoStride = modeInfo.ModeInfoStride,
                    TilesInfo = tiles,
                    FrameSize = new ObuFrameSize()
                },
                FrameHeader = frameHeader,
                PreviousQIndex = new int[] { qIndex }
            },
            SegmentationNeighborMap = new byte[modeInfo.ModeInfoColumnCount * modeInfo.ModeInfoRowCount],
            ModeInfoGrid = modeInfo.Grid,
            ModeInfoAllocation = modeInfo.Allocation,
            ModeInfoStride = modeInfo.ModeInfoStride,
            Disallow4x4AllFrames = modeInfo.Disallow4x4AllFrames,
            CdefPreset = new int[] { -1, -1, -1, -1 },
            TileDataOffsets = Memory<int>.Empty,
            TileDataLengths = Memory<int>.Empty
        };
    }

    private static void AssertProductionTileSelectsExactLumaPalette<TSample>(
        Av1BitDepth bitDepth,
        int bitDepthValue,
        int width,
        int height,
        bool useSplitTransform,
        TSample lowerColor,
        TSample upperColor,
        ushort expectedLowerColor,
        ushort expectedUpperColor,
        TileWriterFactory<TSample> createTileWriter)
        where TSample : unmanaged, IBinaryInteger<TSample>
    {
        const int QIndex = 37;
        const int TileBufferLength = 256;
        ObuColorConfig colorConfig = new()
        {
            IsMonochrome = true,
            SubSamplingX = true,
            SubSamplingY = true,
            BitDepth = bitDepth
        };

        using Av1EncoderFrameBuffer<TSample> source = new(
            Configuration.Default,
            width,
            height,
            bitDepthValue,
            Av1ColorFormat.Yuv400,
            0,
            0,
            lumaBorder: 64);

        using Av1EncoderFrameBuffer<TSample> reconstruction = new(
            Configuration.Default,
            width,
            height,
            bitDepthValue,
            Av1ColorFormat.Yuv400,
            0,
            0,
            lumaBorder: 64);

        Buffer2DRegion<TSample> sourcePlane = source.Frame.CodedView.GetPlane(Av1Plane.Y);
        for (int row = 0; row < sourcePlane.Height; row++)
        {
            int visibleRow = Math.Min(row, height - 1);
            Span<TSample> sourceRow = sourcePlane.DangerousGetRowSpan(row);
            if (!useSplitTransform)
            {
                sourceRow.Fill(visibleRow < height / 2 ? lowerColor : upperColor);
                continue;
            }

            int transformRow = visibleRow >> 2;
            int localRow = visibleRow & 3;
            for (int column = 0; column < sourceRow.Length; column++)
            {
                int transformColumn = column >> 2;
                int localColumn = column & 3;
                int transformIndex = (transformRow * 2) + transformColumn;
                int residual = transformIndex switch
                {
                    0 => (localRow * 2) - 3,
                    1 => (localColumn * 2) - 3,
                    2 => (localRow + localColumn) - 3,
                    _ => localRow - localColumn
                };

                int baseColor = int.CreateChecked(visibleRow < height / 2 ? lowerColor : upperColor);
                sourceRow[column] = TSample.CreateChecked(
                    baseColor + (residual * 4 * (1 << (bitDepthValue - 8))));
            }
        }

        ClearPlane(reconstruction.Luma);
        using Av1EncoderModeInfoBuffer modeInfo = new(
            Configuration.Default,
            width,
            height,
            disallow4x4AllFrames: true);

        Av1PictureControlSet pictureTemplate = CreatePicture(
            modeInfo,
            colorConfig,
            use128x128Superblock: false,
            QIndex);

        pictureTemplate.Parent.FrameHeader.AllowScreenContentTools = true;
        pictureTemplate.Parent.FrameHeader.TransformMode = useSplitTransform
            ? Av1TransformMode.Select
            : Av1TransformMode.Largest;

        pictureTemplate.Parent.FrameHeader.FrameSize.FrameWidth = width;
        pictureTemplate.Parent.FrameHeader.FrameSize.FrameHeight = height;
        using Av1EncoderPictureBuffer picture = new(
            Configuration.Default,
            pictureTemplate.Sequence.SequenceHeader,
            pictureTemplate.Parent.FrameHeader,
            width,
            height,
            disallow4x4AllFrames: true);

        using Av1EncoderCoefficientBuffer coefficients = new(
            Configuration.Default,
            pictureTemplate.Sequence.SequenceHeader,
            width,
            height);

        using Av1EncoderSuperblockWorkspace superblockWorkspace = new(Configuration.Default);
        using Av1EncoderBlockWorkspace blockWorkspace = new(Configuration.Default);
        using Av1SymbolEncoder symbolEncoder = CreateTileSymbolEncoder(
            picture.Picture,
            TileBufferLength);

        Av1TileEncoder tileWriter = createTileWriter(
            symbolEncoder,
            source.Frame,
            reconstruction.Frame,
            picture.Picture,
            coefficients,
            superblockWorkspace,
            blockWorkspace);

        ref Av1MacroBlockModeInfo mode = ref picture.Picture.GetMacroBlockModeInfo(default);
        Assert.Equal(Av1PredictionMode.DC, mode.Block.Mode);
        Assert.Equal(Av1FilterIntraMode.AllFilterIntraModes, superblockWorkspace.FinalBlocks[0].FilterIntraMode);
        Assert.Equal(
            useSplitTransform ? Av1TransformSize.Size4x4 : Av1TransformSize.Size8x8,
            mode.Block.TransformSize);

        Span<Av1EncoderTransformBlockState> transformStates = coefficients
            .GetTransformBlockSpan(0, Av1Plane.Y)[..(useSplitTransform ? 4 : 1)];

        if (useSplitTransform)
        {
            int coefficientBearingTransformCount = 0;
            foreach (Av1EncoderTransformBlockState transformState in transformStates)
            {
                if (transformState.EndOfBlock > 0)
                {
                    coefficientBearingTransformCount++;
                }
            }

            Assert.InRange(coefficientBearingTransformCount, 1, transformStates.Length);
            Assert.InRange(superblockWorkspace.PaletteInfo.PaletteSizes[0], 2, Av1Constants.PaletteMaxSize);
        }
        else
        {
            Assert.Equal((ushort)0, transformStates[0].EndOfBlock);
            Assert.Equal(2, superblockWorkspace.PaletteInfo.PaletteSizes[0]);
            Assert.Equal(
                [expectedLowerColor, expectedUpperColor],
                superblockWorkspace.PaletteInfo.GetColors(Av1Plane.Y).ToArray());
        }

        Buffer2DRegion<byte> colorIndexMap = superblockWorkspace
            .GetPaletteMaps()
            .GetMap(Av1PlaneType.Y, 8, 8);

        Buffer2DRegion<TSample> reconstructionPlane = reconstruction.Frame.CodedView.GetPlane(Av1Plane.Y);
        ReadOnlySpan<ushort> selectedPaletteColors = superblockWorkspace.PaletteInfo.GetColors(Av1Plane.Y);
        long predictionOnlyError = 0;
        long reconstructionError = 0;
        for (int row = 0; row < reconstructionPlane.Height; row++)
        {
            if (useSplitTransform)
            {
                ReadOnlySpan<TSample> sourceRow = sourcePlane.DangerousGetRowSpan(row);
                ReadOnlySpan<TSample> reconstructionRow = reconstructionPlane.DangerousGetRowSpan(row);
                ReadOnlySpan<byte> mapRow = colorIndexMap.DangerousGetRowSpan(row);
                for (int column = 0; column < reconstructionRow.Length; column++)
                {
                    long sourceSample = long.CreateChecked(sourceRow[column]);
                    long predictionDifference = sourceSample - selectedPaletteColors[mapRow[column]];
                    long reconstructionDifference = sourceSample - long.CreateChecked(reconstructionRow[column]);
                    predictionOnlyError += predictionDifference * predictionDifference;
                    reconstructionError += reconstructionDifference * reconstructionDifference;
                }
            }
            else
            {
                int visibleRow = Math.Min(row, height - 1);
                byte expectedIndex = (byte)(visibleRow < height / 2 ? 0 : 1);
                foreach (byte index in colorIndexMap.DangerousGetRowSpan(row))
                {
                    Assert.Equal(expectedIndex, index);
                }

                Assert.True(sourcePlane.DangerousGetRowSpan(row).SequenceEqual(reconstructionPlane.DangerousGetRowSpan(row)));
            }
        }

        // Decode every payload, including clipped maps: retained reconstruction alone cannot reveal missing map symbols.
        byte[] payload = WriteCompleteTileObu(pictureTemplate, tileWriter, width, height);
        using Av1Decoder decoder = new(Configuration.Default);
        using Image<Rgba32> decoded = decoder.Decode<Rgba32>(payload);
        Assert.NotNull(decoder.FrameInfo);
        Av1BlockModeInfo decodedBlock = decoder.FrameInfo.GetModeInfoAt(default);
        Assert.True(decodedBlock.GetPaletteSize(Av1Plane.Y) > 0);
        Assert.Equal(new Size(width, height), decoded.Size);
        using Av1FrameBuffer<byte> decodedFrame = decoder.DecodeFrameBuffer(payload, null, null, out _);
        Buffer2DRegion<byte> decodedPlane = decodedFrame.DeriveBlockPointer(Av1Plane.Y, 0, 0);
        for (int row = 0; row < height; row++)
        {
            ReadOnlySpan<TSample> decodedSamples = MemoryMarshal.Cast<byte, TSample>(decodedPlane.DangerousGetRowSpan(row));
            Assert.Equal(reconstructionPlane.DangerousGetRowSpan(row)[..width], decodedSamples);
        }

        if (useSplitTransform)
        {
            Assert.True(predictionOnlyError > 0);
            Assert.True(reconstructionError < predictionOnlyError);
            Assert.Equal(4, decodedBlock.GetTransformUnitCount(Av1Plane.Y));
        }

        string outputDirectory = Path.Combine(TestEnvironment.ActualOutputDirectoryFullPath, "Formats", "Heif", "Av1");
        string outputName = useSplitTransform
            ? $"encoder-palette-transform-size-select-{bitDepthValue}b"
            : $"encoder-palette-luma-{bitDepthValue}b-{width}x{height}";

        Directory.CreateDirectory(outputDirectory);
        File.WriteAllBytes(Path.Combine(outputDirectory, outputName + ".obu"), payload);
        using FileStream raw = File.Create(Path.Combine(outputDirectory, outputName + ".retained.yuv"));
        for (int row = 0; row < height; row++)
        {
            raw.Write(MemoryMarshal.AsBytes(reconstructionPlane.DangerousGetRowSpan(row)[..width]));
        }

        Assert.NotEqual(0, tileWriter.GetTileData(0).Length);
    }

    private static void FillChromaModeSelectionPlane(
        Buffer2DRegion<byte> plane,
        Av1TransformSize transformSize,
        Av1ChromaPredictionMode expectedMode,
        int expectedAngleDelta)
    {
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();
        Span<byte> aboveStorage = stackalloc byte[17];
        Span<byte> above = aboveStorage.Slice(1, width * 2);
        Span<byte> leftStorage = stackalloc byte[17];
        Span<byte> left = leftStorage.Slice(1, height * 2);
        aboveStorage[0] = 128;
        leftStorage[0] = 128;
        for (int column = 0; column < width; column++)
        {
            above[column] = (byte)(32 + ((192 * column) / (width - 1)));
        }

        for (int row = 0; row < height; row++)
        {
            left[row] = (byte)(224 - ((192 * row) / (height - 1)));
        }

        above[width..].Fill(above[width - 1]);
        left[height..].Fill(left[height - 1]);
        Span<byte> target = stackalloc byte[64];
        int sampleCount = transformSize.GetSize2d();
        if (expectedMode.IsDirectional())
        {
            // Directional arithmetic has separate byte-exact reference coverage. This fixture uses its scalar
            // path only to isolate chroma traversal, joint U/V rate-distortion selection, and packed mode state.
            Av1DirectionalIntraPredictor.PredictScalar(
                target[..sampleCount],
                width,
                transformSize,
                above,
                left,
                false,
                false,
                expectedMode.ToLumaMode().ToAngle() + (expectedAngleDelta * Av1Constants.AngleStep));
        }
        else
        {
            // Build the supported non-directional targets directly so production prediction cannot self-validate.
            for (int row = 0; row < height; row++)
            {
                for (int column = 0; column < width; column++)
                {
                    int top = above[column];
                    int leftSample = left[row];
                    int predictor = top + leftSample - 128;
                    int leftDistance = Math.Abs(predictor - leftSample);
                    int topDistance = Math.Abs(predictor - top);
                    int cornerDistance = Math.Abs(predictor - 128);
                    target[(row * width) + column] = expectedMode switch
                    {
                        Av1ChromaPredictionMode.Vertical => (byte)top,
                        Av1ChromaPredictionMode.Horizontal => (byte)leftSample,
                        _ => (byte)(leftDistance <= topDistance && leftDistance <= cornerDistance
                            ? leftSample
                            : topDistance <= cornerDistance ? top : 128)
                    };
                }
            }
        }

        // The first three transform-sized quadrants establish the references consumed by the bottom-right
        // target. Its checkerboard offset keeps coefficients nonzero so the implicit transform affects the stream.
        for (int row = 0; row < plane.Height; row++)
        {
            Span<byte> destination = plane.DangerousGetRowSpan(row);
            for (int column = 0; column < plane.Width; column++)
            {
                destination[column] = row < height
                    ? column < width ? (byte)128 : above[column - width]
                    : column < width
                        ? left[row - height]
                        : (byte)Math.Clamp(
                            target[((row - height) * width) + column - width] +
                                ((((row - height) + column - width) & 1) == 0 ? 5 : -5),
                            0,
                            255);
            }
        }
    }

    private static void FillPlane(Buffer2DRegion<byte> plane, int modulus, int seed)
    {
        for (int y = 0; y < plane.Height; y++)
        {
            Span<byte> row = plane.DangerousGetRowSpan(y);
            for (int x = 0; x < row.Length; x++)
            {
                row[x] = (byte)(1 + ((seed + (x * 43) + (y * 79)) % modulus));
            }
        }
    }

    private static void FillPlane<TSample>(Buffer2DRegion<TSample> plane, TSample value)
        where TSample : unmanaged
    {
        for (int y = 0; y < plane.Height; y++)
        {
            plane.DangerousGetRowSpan(y).Fill(value);
        }
    }

    /// <summary>
    /// Creates the operation owner for a production tile's entropy state and bounded output memory.
    /// </summary>
    /// <param name="picture">The picture supplying quantization and CDF-update settings.</param>
    /// <param name="bufferLength">The bounded output allocation length in bytes.</param>
    /// <returns>The symbol encoder that must remain alive while the tile output is consumed.</returns>
    private static Av1SymbolEncoder CreateTileSymbolEncoder(Av1PictureControlSet picture, int bufferLength)
    {
        ObuFrameHeader frameHeader = picture.Parent.FrameHeader;
        return new Av1SymbolEncoder(
            Configuration.Default,
            bufferLength,
            frameHeader.QuantizationParameters.BaseQIndex,
            updateCdf: !frameHeader.DisableCdfUpdate);
    }

    private delegate Av1TileEncoder TileWriterFactory<TSample>(
        Av1SymbolEncoder writer,
        Av1EncoderFrame<TSample> source,
        Av1EncoderFrame<TSample> reconstruction,
        Av1PictureControlSet picture,
        Av1EncoderCoefficientBuffer coefficients,
        Av1EncoderSuperblockWorkspace superblockWorkspace,
        Av1EncoderBlockWorkspace blockWorkspace)
        where TSample : unmanaged;

    private delegate TSample SampleFactory<TSample>(int value)
        where TSample : unmanaged;

    private delegate void FilterPrediction<TSample>(
        Av1FilterIntraMode mode,
        Span<TSample> destination,
        int destinationStride,
        ReadOnlySpan<TSample> above,
        ReadOnlySpan<TSample> left,
        int width,
        int height,
        int bitDepth,
        Span<TSample> scratch)
        where TSample : unmanaged;

    private static void ClearPlane<TSample>(Buffer2D<TSample> plane)
        where TSample : unmanaged
    {
        for (int y = 0; y < plane.Height; y++)
        {
            plane.DangerousGetRowSpan(y).Clear();
        }
    }

    private static void AssertContainsNonzero<TSample>(Buffer2DRegion<TSample> plane)
        where TSample : unmanaged, IEquatable<TSample>
    {
        bool containsNonzero = false;
        for (int y = 0; y < plane.Height; y++)
        {
            foreach (TSample sample in plane.DangerousGetRowSpan(y))
            {
                containsNonzero |= !sample.Equals(default);
            }
        }

        Assert.True(containsNonzero);
    }

    /// <summary>
    /// Records the live luma-mode cost while supplying an all-skipped final block.
    /// </summary>
    private struct BlockCostRecorder : Av1TileWriter.IBlockEncodingHandler
    {
        private readonly int[] costs;
        private readonly int qIndex;

        /// <summary>
        /// Initializes a new instance of the <see cref="BlockCostRecorder"/> struct.
        /// </summary>
        /// <param name="costs">The destination for costs observed in writer order.</param>
        /// <param name="qIndex">The block quantizer index.</param>
        public BlockCostRecorder(int[] costs, int qIndex)
        {
            this.costs = costs;
            this.qIndex = qIndex;
            this.Count = 0;
        }

        public static bool UsesRetainedDecisions => false;

        /// <summary>
        /// Gets the number of final blocks visited by the writer.
        /// </summary>
        public int Count { get; private set; }

        /// <inheritdoc/>
        public readonly Av1PartitionType SelectPartition(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ushort tileIndex,
            Av1BlockSize blockSize,
            Av1PartitionType preparedPartition)
            => preparedPartition;

        /// <inheritdoc/>
        public void EncodeBlock(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ushort tileIndex,
            ref Av1MacroBlockModeInfo modeInfo,
            ref Av1EncoderBlockStruct block,
            ref Av1EncoderPaletteInfo paletteInfo)
        {
            this.costs[this.Count++] = Av1TileWriter.GetLumaModeCost(
                writer,
                macroBlock,
                Av1BlockSize.Block8x8,
                Av1PredictionMode.DC,
                0,
                isIntraFrame: true);

            modeInfo.Block = new Av1EncoderBlockModeInfo
            {
                BlockSize = Av1BlockSize.Block8x8,
                PartitionType = Av1PartitionType.None,
                SegmentId = 0,
                Skip = true,
                TransformSize = Av1TransformSize.Size8x8,
                Mode = Av1PredictionMode.DC,
                UvMode = Av1ChromaPredictionMode.DC,
            };

            block.HasChroma = false;
            block.QuantizationIndex = this.qIndex;
            block.SegmentId = 0;
        }
    }

    /// <summary>
    /// Supplies one skipped monochrome palette block to the production tile writer.
    /// </summary>
    private struct PaletteBlockEncoder : Av1TileWriter.IBlockEncodingHandler
    {
        private readonly Av1EncoderSuperblockWorkspace workspace;
        private readonly int qIndex;
        private readonly int mapVariant;

        /// <summary>
        /// Initializes a new instance of the <see cref="PaletteBlockEncoder"/> struct.
        /// </summary>
        /// <param name="workspace">The workspace that owns the palette index map.</param>
        /// <param name="qIndex">The block quantizer index.</param>
        /// <param name="mapVariant">The map pattern selected by the test.</param>
        public PaletteBlockEncoder(
            Av1EncoderSuperblockWorkspace workspace,
            int qIndex,
            int mapVariant)
        {
            this.workspace = workspace;
            this.qIndex = qIndex;
            this.mapVariant = mapVariant;
            this.Count = 0;
        }

        public static bool UsesRetainedDecisions => false;

        /// <summary>
        /// Gets the number of final blocks visited by the writer.
        /// </summary>
        public int Count { get; private set; }

        /// <inheritdoc/>
        public readonly Av1PartitionType SelectPartition(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ushort tileIndex,
            Av1BlockSize blockSize,
            Av1PartitionType preparedPartition)
            => preparedPartition;

        /// <inheritdoc/>
        public void EncodeBlock(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ushort tileIndex,
            ref Av1MacroBlockModeInfo modeInfo,
            ref Av1EncoderBlockStruct block,
            ref Av1EncoderPaletteInfo paletteInfo)
        {
            this.Count++;
            modeInfo.Block = new Av1EncoderBlockModeInfo
            {
                BlockSize = Av1BlockSize.Block8x8,
                PartitionType = Av1PartitionType.None,
                SegmentId = 0,
                Skip = true,
                TransformSize = Av1TransformSize.Size8x8,
                Mode = Av1PredictionMode.DC,
                UvMode = Av1ChromaPredictionMode.DC
            };

            block.HasChroma = false;
            block.QuantizationIndex = this.qIndex;
            block.SegmentId = 0;
            paletteInfo.PaletteSizes[0] = 3;
            paletteInfo.SetColors(Av1Plane.Y, [16, 128, 240]);
            Buffer2DRegion<byte> map = this.workspace
                .GetPaletteMaps()
                .GetMap(Av1PlaneType.Y, 8, 8);

            for (int row = 0; row < map.Height; row++)
            {
                Span<byte> mapRow = map.DangerousGetRowSpan(row);
                for (int column = 0; column < map.Width; column++)
                {
                    mapRow[column] = this.mapVariant == 0
                        ? (byte)0
                        : (byte)((row + column) % 3);
                }
            }
        }
    }
}
