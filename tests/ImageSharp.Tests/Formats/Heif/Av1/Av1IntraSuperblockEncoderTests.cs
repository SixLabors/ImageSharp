// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using SixLabors.ImageSharp.Formats.Heif;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Color;
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
using SixLabors.ImageSharp.Formats.Heif.Components;
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
    [Theory]
    [InlineData((int)Av1PredictionMode.DC, (int)Av1ChromaPredictionMode.DC)]
    [InlineData((int)Av1PredictionMode.DC, (int)Av1ChromaPredictionMode.Smooth)]
    [InlineData((int)Av1PredictionMode.Vertical, (int)Av1ChromaPredictionMode.Vertical)]
    [InlineData((int)Av1PredictionMode.Horizontal, (int)Av1ChromaPredictionMode.Horizontal)]
    [InlineData((int)Av1PredictionMode.Directional45Degrees, (int)Av1ChromaPredictionMode.Directional45Degrees)]
    [InlineData((int)Av1PredictionMode.Directional135Degrees, (int)Av1ChromaPredictionMode.Directional135Degrees)]
    [InlineData((int)Av1PredictionMode.Directional113Degrees, (int)Av1ChromaPredictionMode.Directional113Degrees)]
    [InlineData((int)Av1PredictionMode.Directional157Degrees, (int)Av1ChromaPredictionMode.Directional157Degrees)]
    [InlineData((int)Av1PredictionMode.Directional203Degrees, (int)Av1ChromaPredictionMode.Directional203Degrees)]
    [InlineData((int)Av1PredictionMode.Directional67Degrees, (int)Av1ChromaPredictionMode.Directional67Degrees)]
    [InlineData((int)Av1PredictionMode.SmoothVertical, (int)Av1ChromaPredictionMode.SmoothVertical)]
    [InlineData((int)Av1PredictionMode.SmoothHorizontal, (int)Av1ChromaPredictionMode.SmoothHorizontal)]
    [InlineData((int)Av1PredictionMode.Paeth, (int)Av1ChromaPredictionMode.Paeth)]
    public void SpeedFourChromaPruningRetainsLumaDerivedMode(int lumaModeValue, int chromaModeValue)
    {
        Av1PredictionMode lumaMode = (Av1PredictionMode)lumaModeValue;
        Av1ChromaPredictionMode chromaMode = (Av1ChromaPredictionMode)chromaModeValue;

        Assert.True(Av1IntraSuperblockEncoder.ModeDecision<byte, Av1IntraSuperblockEncoder.ByteOperator>.ShouldSearchChromaMode(
            HeifEncodingSpeed.Level4,
            lumaMode,
            chromaMode));

        Assert.True(Av1IntraSuperblockEncoder.ModeDecision<byte, Av1IntraSuperblockEncoder.ByteOperator>.ShouldSearchChromaMode(
            HeifEncodingSpeed.Level4,
            lumaMode,
            Av1ChromaPredictionMode.DC));

        Assert.True(Av1IntraSuperblockEncoder.ModeDecision<byte, Av1IntraSuperblockEncoder.ByteOperator>.ShouldSearchChromaMode(
            HeifEncodingSpeed.Level4,
            lumaMode,
            Av1ChromaPredictionMode.Smooth));
    }

    [Fact]
    public void ChromaPruningUsesLibaomSpeedBoundaryAndRejectsUnrelatedModes()
    {
        Assert.True(Av1IntraSuperblockEncoder.ModeDecision<byte, Av1IntraSuperblockEncoder.ByteOperator>.ShouldSearchChromaMode(
            HeifEncodingSpeed.Level3,
            Av1PredictionMode.Vertical,
            Av1ChromaPredictionMode.Paeth));

        Assert.False(Av1IntraSuperblockEncoder.ModeDecision<byte, Av1IntraSuperblockEncoder.ByteOperator>.ShouldSearchChromaMode(
            HeifEncodingSpeed.Level4,
            Av1PredictionMode.Vertical,
            Av1ChromaPredictionMode.Paeth));
    }

    [Theory]
    [InlineData((int)ObuFrameType.KeyFrame, true)]
    [InlineData((int)ObuFrameType.IntraOnlyFrame, true)]
    [InlineData((int)ObuFrameType.InterFrame, false)]
    public void ChromaPaletteHeaderTerminationMatchesIntraFrameClassification(int frameTypeValue, bool expected)
        => Assert.Equal(
            expected,
            Av1IntraSuperblockEncoder.ModeDecision<byte, Av1IntraSuperblockEncoder.ByteOperator>.ShouldPruneChromaPaletteByHeader(
                (ObuFrameType)frameTypeValue));

    [Theory]
    [InlineData((int)HeifEncodingSpeed.Level3, (int)Av1BlockSize.Block16x16, true, true, false)]
    [InlineData((int)HeifEncodingSpeed.Level4, (int)Av1BlockSize.Block64x64, true, true, false)]
    [InlineData((int)HeifEncodingSpeed.Level4, (int)Av1BlockSize.Block16x16, false, true, false)]
    [InlineData((int)HeifEncodingSpeed.Level4, (int)Av1BlockSize.Block16x16, true, false, false)]
    [InlineData((int)HeifEncodingSpeed.Level4, (int)Av1BlockSize.Block16x16, true, true, true)]
    public void PartitionSearchTerminationMatchesLibaomBounds(
        int speedValue,
        int blockSizeValue,
        bool noneInvalid,
        bool splitInvalid,
        bool expected)
        => Assert.Equal(
            expected,
            Av1IntraSuperblockEncoder.ModeDecision<byte, Av1IntraSuperblockEncoder.ByteOperator>.ShouldTerminatePartitionSearchAfterNoneAndSplit(
                new Av1EncoderSpeedSettings((HeifEncodingSpeed)speedValue, true, true, 0, new Size(64, 64)).TerminatePartitionSearchAfterInvalidNoneAndSplit,
                (Av1BlockSize)blockSizeValue,
                Av1BlockSize.Block64x64,
                noneInvalid,
                splitInvalid));

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
            speed: HeifEncodingSpeed.Level0);

        // Keep frame filtering outside this motion-search allocation and interpolation test.
        keyEncoder.SequenceHeader.EnableCdef = false;
        keyEncoder.SequenceHeader.EnableRestoration = false;

        // Independent filters per axis are a sequence tool. Good quality and real-time both clear it
        // (disable_dual_filter, speed_features.c L1145 and L2005), so this fixture states it directly.
        keyEncoder.SequenceHeader.EnableDualFilter = dualFilter;
        keyEncoder.EncodeKeyFrame(referenceImage.Frames.RootFrame, firstSample);
        ObuSequenceHeader sequenceHeader = keyEncoder.SequenceHeader;
        using Av1EncoderModeInfoBuffer modeInfo = new(configuration, Width, Height, disallow4x4AllFrames: false);
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

        using Av1EncoderPictureBuffer picture = new(configuration, sequenceHeader, frameHeader, Width, Height, 1 << sequenceHeader.SuperblockSizeLog2, disallow4x4AllFrames: false);
        using Av1EncoderCoefficientBuffer coefficients = new(configuration, sequenceHeader, Width, Height);
        using Av1EncoderSuperblockWorkspace superblockWorkspace = new(configuration);
        using Av1EncoderBlockWorkspace blockWorkspace = new(configuration, allocateInterMotionCosts: true, allocateDisplacementCosts: false, sequenceHeader.SuperblockSize);
        using Av1SymbolEncoder symbolEncoder = new(configuration, TileBufferLength, QIndex, updateCdf: true);
        Av1EncoderTileWorkspace tileWorkspace = new(frameHeader, superblockWorkspace);

        // LAST is the only distinct reference; GOLDEN aliases it and stays unavailable for compound prediction.
        Av1EncoderFrame<byte>[] references = new Av1EncoderFrame<byte>[Av1Constants.ReferenceFrameCount];
        references[(int)Av1ReferenceFrameType.Last] = reference.Frame;
        references[(int)Av1ReferenceFrameType.Golden] = reference.Frame;
        picture.Picture.Parent.AvailableReferenceMask = 1 << (int)Av1ReferenceFrameType.Last;
        int allocationCount = allocator.AllocationLog.Count;
        Av1TileEncoder tileWriter = new(
            symbolEncoder,
            source.Frame,
            references,
            reconstruction.Frame,
            picture.Picture,
            coefficients,
            tileWorkspace,
            blockWorkspace);

        // The partition tree rents its candidate storage when the first superblock states its coded extent, and
        // the deblocking level search rents one unfiltered plane copy per frame. Motion search, interpolation
        // selection, and reconstruction must not allocate for each block.
        Assert.InRange(allocator.AllocationLog.Count - allocationCount, 0, 2);
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
        using ImageFrame<L8> decodedFirst = new(configuration, Width, Height);
        decoder.DecodeSequenceFrame(
            firstSample.ToArray(),
            null,
            null,
            decodedFirst.Size,
            decodedFirst.Bounds,
            decodedFirst.PixelBuffer.GetRegion(decodedFirst.Bounds),
            default,
            null,
            null,
            false);
        using ImageFrame<L8> decodedSecond = new(configuration, Width, Height);
        decoder.DecodeSequenceFrame(
            secondSample.ToArray(),
            null,
            null,
            decodedSecond.Size,
            decodedSecond.Bounds,
            decodedSecond.PixelBuffer.GetRegion(decodedSecond.Bounds),
            default,
            null,
            null,
            false);

        for (int y = 0; y < Height; y++)
        {
            ReadOnlySpan<byte> expected = reference.Frame.View.GetPlane(Av1Plane.Y).DangerousGetRowSpan(y);
            ReadOnlySpan<byte> actual = MemoryMarshal.AsBytes(decodedFirst.PixelBuffer.DangerousGetRowSpan(y));
            Assert.Equal(expected, actual);
        }

        for (int y = 0; y < Height; y++)
        {
            ReadOnlySpan<byte> expected = reconstruction.Frame.View.GetPlane(Av1Plane.Y).DangerousGetRowSpan(y);
            ReadOnlySpan<byte> actual = MemoryMarshal.AsBytes(decodedSecond.PixelBuffer.DangerousGetRowSpan(y));
            Assert.Equal(expected, actual);
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
        // The reference never searches a mode whose threshold exceeds the best cost so far
        // (skip_inter_mode, rdopt.c L5258-L5262). At a near-lossless quantizer every cost falls
        // under that threshold, so only NEARESTMV is ever measured. This quantizer keeps the
        // exact two-axis predictor preferable while leaving NEWMV inside the search.
        const int QIndex = 37;
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
            speed: HeifEncodingSpeed.Level0);

        // Keep frame filtering outside this motion-search allocation and interpolation test.
        keyEncoder.SequenceHeader.EnableCdef = false;
        keyEncoder.SequenceHeader.EnableRestoration = false;

        // Independent filters per axis are a sequence tool. Good quality and real-time both clear it
        // (disable_dual_filter, speed_features.c L1145 and L2005), so this fixture states it directly.
        keyEncoder.SequenceHeader.EnableDualFilter = true;
        keyEncoder.EncodeKeyFrame(referenceImage.Frames.RootFrame, firstSample);
        ObuSequenceHeader sequenceHeader = keyEncoder.SequenceHeader;
        using Av1EncoderModeInfoBuffer modeInfo = new(configuration, Width, Height, disallow4x4AllFrames: false);
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

        using Av1EncoderPictureBuffer picture = new(configuration, sequenceHeader, frameHeader, Width, Height, 1 << sequenceHeader.SuperblockSizeLog2, disallow4x4AllFrames: false);
        using Av1EncoderCoefficientBuffer coefficients = new(configuration, sequenceHeader, Width, Height);
        using Av1EncoderSuperblockWorkspace superblockWorkspace = new(configuration);
        using Av1EncoderBlockWorkspace blockWorkspace = new(configuration, allocateInterMotionCosts: true, allocateDisplacementCosts: false, sequenceHeader.SuperblockSize);
        using Av1SymbolEncoder symbolEncoder = new(configuration, TileBufferLength, QIndex, updateCdf: true);
        Av1EncoderTileWorkspace tileWorkspace = new(frameHeader, superblockWorkspace);

        // LAST is the only distinct reference; GOLDEN aliases it and stays unavailable for compound prediction.
        Av1EncoderFrame<ushort>[] references = new Av1EncoderFrame<ushort>[Av1Constants.ReferenceFrameCount];
        references[(int)Av1ReferenceFrameType.Last] = reference.Frame;
        references[(int)Av1ReferenceFrameType.Golden] = reference.Frame;
        picture.Picture.Parent.AvailableReferenceMask = 1 << (int)Av1ReferenceFrameType.Last;
        int allocationCount = allocator.AllocationLog.Count;
        Av1TileEncoder tileWriter = new(
            symbolEncoder, source.Frame, references, reconstruction.Frame, picture.Picture, coefficients, tileWorkspace, blockWorkspace);

        // The partition tree rents its candidate storage when the first superblock states its coded extent, and
        // the deblocking level search rents one unfiltered plane copy per frame. Motion search, interpolation
        // selection, and reconstruction must not allocate for each block.
        Assert.InRange(allocator.AllocationLog.Count - allocationCount, 0, 2);

        // This block is at least three reference taps from every frame edge. It must retain two genuinely
        // fractional axes, not a zero-phase filter alias.
        Point targetPosition = new(TargetColumn >> Av1Constants.ModeInfoSizeLog2, TargetRow >> Av1Constants.ModeInfoSizeLog2);
        // A block larger than 4x4 stores its decision once, and the grid maps every covered
        // position to it. The displacement vector below already reads through that grid.
        ref Av1MacroBlockModeInfo targetMode = ref picture.Picture.GetFromModeInfoGrid(targetPosition);
        Assert.Equal(Av1ReferenceFrameType.Last, targetMode.Block.ReferenceFrame);
        Assert.Equal(horizontalFilter, targetMode.Block.HorizontalInterpolationFilter);
        Assert.Equal(verticalFilter, targetMode.Block.VerticalInterpolationFilter);
        Assert.Equal(4, picture.Picture.GetDisplacementVector(targetPosition).Column);
        Assert.Equal(4, picture.Picture.GetDisplacementVector(targetPosition).Row);
        // The source states the two-axis response of the periodic reference. The block that covers
        // this position reaches the frame border, where the reference plane holds replicated samples
        // instead of that periodic continuation, so its residual is not empty and the transform
        // spreads that border error over every sample of the block. The decoder comparison below
        // carries the precision claim instead: it proves that the retained reconstruction is exactly
        // what a decoder produces from this stream at the coded bit depth.

        using MemoryStream secondSample = new();
        using ObuWriter obuWriter = new(configuration);
        obuWriter.WriteFrame(secondSample, sequenceHeader, frameHeader, tileWriter);
        using Av1Decoder decoder = new(configuration);
        for (int frameIndex = 0; frameIndex < 2; frameIndex++)
        {
            decoder.DecodeSequenceReference((frameIndex == 0 ? firstSample : secondSample).ToArray(), null, null);
            Av1FrameBuffer<byte> decoded = Assert.IsType<Av1FrameBuffer<byte>>(decoder.FrameBuffer);
            Buffer2DRegion<ushort> expected = (frameIndex == 0 ? reference : reconstruction).Frame.View.GetPlane(Av1Plane.Y);
            for (int y = 0; y < Height; y++)
            {
                ReadOnlySpan<ushort> actualRow = decoded.GetHighBitDepthRowSpan(Av1Plane.Y, y, 0, 0);
                Assert.Equal(expected.DangerousGetRowSpan(y), actualRow);
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

        using Av1EncoderModeInfoBuffer modeInfo = new(Configuration.Default, Width, Height, disallow4x4AllFrames: false);
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
            1 << picture.Sequence.SequenceHeader.SuperblockSizeLog2,
            disallow4x4AllFrames: false);

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
            tileBlockWorkspace);

        // Both paths evaluate DC only, so differing bytes identify a traversal or writing defect rather than a different mode choice.
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
        Point modeInfoPosition = new(blockOrigin.X >> Av1Constants.ModeInfoSizeLog2, blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2);
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
                using Av1EncoderModeInfoBuffer modeInfoBuffer = new(Configuration.Default, frameWidth, Height, disallow4x4AllFrames: false);
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
                    1 << template.Sequence.SequenceHeader.SuperblockSizeLog2,
                    disallow4x4AllFrames: false);

                Av1PictureControlSet picture = pictureBuffer.Picture;
                if (isIntraBlockCopy)
                {
                    picture.IntraBlockCopySearch.Initialize<TSample, TOperator>(source.Frame.View.GetPlane(Av1Plane.Y));
                }

                using Av1EncoderCoefficientBuffer coefficients = new(Configuration.Default, template.Sequence.SequenceHeader, frameWidth, Height);
                using Av1EncoderSuperblockWorkspace superblockWorkspace = new(Configuration.Default);
                using Av1EncoderBlockWorkspace blockWorkspace = new(
                    Configuration.Default,
                    allocateInterMotionCosts: !isIntraBlockCopy,
                    allocateDisplacementCosts: isIntraBlockCopy,
                    template.Sequence.SequenceHeader.SuperblockSize);

                using Av1SymbolEncoder writer = new(Configuration.Default, 4096, qIndex, updateCdf: true);
                if (!isIntraBlockCopy)
                {
                    writer.FillMotionVectorCosts(blockWorkspace.GetMotionVectorCosts(picture.Parent.FrameHeader.MotionVectorPrecision));
                }
                else
                {
                    writer.FillDisplacementVectorCosts(blockWorkspace.GetDisplacementVectorCosts());
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
                        Av1ReferenceFrameType.Last,
                        Av1ReferenceFrameType.None);

                    // Thirty-two preceding global-motion symbols make the zero global predictor the cheapest
                    // mode. Keep every competing mode enabled so this fixture tests residual skipping independently
                    // of mode-search restrictions, while checking exact syntax rates, distortion, and reconstruction.
                    for (int index = 0; index < 32; index++)
                    {
                        writer.WriteInterMode<Av1SymbolEncoder.SymbolUpdateOperation>(Av1PredictionMode.GlobalMotionVector, references.ModeContext);
                    }

                    writer.RefreshCosts();
                    int globalRate = writer.GetInterModeCost(Av1PredictionMode.GlobalMotionVector, references.ModeContext);
                    Assert.True(globalRate < writer.GetInterModeCost(Av1PredictionMode.NearestMotionVector, references.ModeContext));
                    Assert.True(globalRate < writer.GetInterModeCost(Av1PredictionMode.NearMotionVector, references.ModeContext));
                    Assert.True(globalRate < writer.GetInterModeCost(Av1PredictionMode.NewMotionVector, references.ModeContext));
                }

                // The trials below stand in for the encoder's own transform search, so they need the frame
                // state and the speed settings it runs with. Coefficient refinement alone changes which
                // transform type wins and how many coefficients survive.
                picture.Parent.AvailableReferenceMask = isIntraBlockCopy ? (byte)0 : (byte)(1 << (int)Av1ReferenceFrameType.Last);
                Av1TileEncoder.PrepareFrame(picture, new Size(source.Frame.Width, source.Frame.Height), blockWorkspace);
                blockWorkspace.SpeedSettings = picture.Parent.SpeedSettings;

                int multiplier = isIntraBlockCopy
                    ? Av1RateDistortion.GetRateMultiplier(qIndex, bitDepth, Av1FrameUpdateType.Key)
                    : Av1RateDistortion.GetRateMultiplier(qIndex, bitDepth, Av1FrameUpdateType.Last);
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

                // Qualify the source at the quantizer boundary, before refinement can remove coefficients.
                // The complete candidate still determines whether prediction-only block syntax costs less.
                for (Av1TransformType transformType = Av1TransformType.DctDct;
                    transformType < Av1TransformType.AllTransformTypes;
                    transformType++)
                {
                    if (!transformType.IsExtendedSetUsed(transformSet))
                    {
                        continue;
                    }

                    Av1EncoderTransformBlockState state = default;
                    Av1TransformBlockEncoder.EncodeLossy(
                        blockWorkspace,
                        residual,
                        trialCoefficients,
                        Av1TransformSize.Size8x8,
                        transformType,
                        qIndex,
                        0,
                        0,
                        bitDepth,
                        ref state);

                    allTransformsAreNonzero &= state.EndOfBlock != 0;
                    long distortion = TOperator.EncodePredictionCandidate(
                        blockWorkspace,
                        writer,
                        default,
                        multiplier,
                        true,
                        picture.Sequence.SequenceHeader.IsStillPicture,
                        source.Frame.CodedView.GetPlane(Av1Plane.Y),
                        blockOrigin,
                        prediction,
                        residual,
                        Av1TransformSize.Size8x8.GetWidth(),
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

                // LAST is the only distinct reference; GOLDEN aliases it and stays unavailable for compound prediction.
                // The frame role follows the frame type, as it does when the tile encoder prepares a picture.
                Av1EncoderFrame<TSample>[] referenceFrames = new Av1EncoderFrame<TSample>[Av1Constants.ReferenceFrameCount];
                referenceFrames[(int)Av1ReferenceFrameType.Last] = reference.Frame;
                referenceFrames[(int)Av1ReferenceFrameType.Golden] = reference.Frame;
                Av1IntraSuperblockEncoder.ModeDecision<TSample, TOperator> decision = new(
                    source.Frame,
                    referenceFrames,
                    reconstruction.Frame,
                    picture,
                    superblock,
                    coefficients,
                    blockWorkspace);

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
                      blockWorkspace.GetDisplacementVectorCosts().GetDisplacementVectorCost(
                          picture.GetDisplacementVector(modeInfoPosition), displacementReference)
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

        Assert.True(verifiedCases > 0, "The input set must exercise skipping despite nonzero coefficients before refinement in every legal transform.");
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

        using Av1EncoderModeInfoBuffer modeInfo = new(Configuration.Default, Width, Height, disallow4x4AllFrames: false);
        Av1PictureControlSet pictureTemplate = CreatePicture(modeInfo, colorConfig, use128x128Superblock: false, qIndex: 37);
        using Av1EncoderPictureBuffer pictureBuffer = new(
            Configuration.Default,
            pictureTemplate.Sequence.SequenceHeader,
            pictureTemplate.Parent.FrameHeader,
            Width,
            Height,
            1 << pictureTemplate.Sequence.SequenceHeader.SuperblockSizeLog2,
            disallow4x4AllFrames: false);

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
            1 << pictureTemplate.Sequence.SequenceHeader.SuperblockSizeLog2,
            disallow4x4AllFrames: false);

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
            liveBlockWorkspace);

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
        using Av1EncoderModeInfoBuffer modeInfo = new(Configuration.Default, Width, Height, disallow4x4AllFrames: false);
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
            1 << picture.Sequence.SequenceHeader.SuperblockSizeLog2,
            disallow4x4AllFrames: false);

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
            tileBlockWorkspace);

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

        using Av1EncoderModeInfoBuffer modeInfoBuffer = new(Configuration.Default, Width, Height, disallow4x4AllFrames: false);
        Av1PictureControlSet template = CreatePicture(modeInfoBuffer, colorConfig, use128x128Superblock: false, QIndex);
        template.Parent.FrameHeader.TransformMode = Av1TransformMode.Largest;
        using Av1EncoderPictureBuffer pictureBuffer = new(
            Configuration.Default,
            template.Sequence.SequenceHeader,
            template.Parent.FrameHeader,
            Width,
            Height,
            1 << template.Sequence.SequenceHeader.SuperblockSizeLog2,
            disallow4x4AllFrames: false);

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

        // Block decisions read frame-level state that the tile encoder prepares once for each picture.
        Av1TileEncoder.PrepareFrame(picture, new Size(source.Frame.Width, source.Frame.Height), blockWorkspace);
        Av1IntraSuperblockEncoder.ModeDecision<byte, Av1IntraSuperblockEncoder.ByteOperator> decision = new(
            source.Frame,
            default,
            reconstruction.Frame,
            picture,
            superblock,
            coefficients,
            blockWorkspace);

        ref Av1MacroBlockModeInfo modeInfo = ref picture.GetMacroBlockModeInfo(Point.Empty);
        Av1EncoderBlockStruct block = default;
        Av1EncoderPaletteInfo palette = default;
        using Av1SymbolEncoder writer = new(Configuration.Default, 256, QIndex, updateCdf: true);
        decision.EncodeBlock(writer, macroBlock, Point.Empty, 0, ref modeInfo, ref block, ref palette);

        Assert.Equal(Av1TransformSize.Size8x8, modeInfo.Block.TransformSize);
        Assert.False(modeInfo.Block.Skip);
        int lumaAngleDelta = block.PredictionUnit.AngleDelta[(int)Av1PlaneType.Y];
        int expectedRate = writer.GetSkipCost(false, Av1TileWriter.GetSkipContext(macroBlock));
        expectedRate += Av1TileWriter.GetLumaModeCost(
            writer,
            macroBlock,
            Av1BlockSize.Block8x8,
            modeInfo.Block.Mode,
            lumaAngleDelta,
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
                modeInfo.Block.Mode,
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
                modeInfo.Block.Mode,
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
        int multiplier = Av1RateDistortion.GetRateMultiplier(QIndex, Av1BitDepth.EightBit, Av1FrameUpdateType.Key);
        long expectedCost = ((((long)expectedRate * multiplier) + 256) / 512) + (expectedDistortion * 128);
        Assert.Equal(textured, squaredError > 0);
        Assert.Equal(expectedRate, decision.SelectedBlockStatistics.Rate);
        Assert.Equal(expectedDistortion, decision.SelectedBlockStatistics.Distortion);
        Assert.Equal(expectedCost, decision.SelectedBlockStatistics.Cost);
    }

    [Fact]
    public void BlockDecisionRetainsRatesWithinSuperblock()
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

        using Av1EncoderModeInfoBuffer modeInfo = new(Configuration.Default, Width, Height, disallow4x4AllFrames: false);
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
            1 << pictureTemplate.Sequence.SequenceHeader.SuperblockSizeLog2,
            disallow4x4AllFrames: false);

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
        Assert.Equal(costs[0], costs[1]);
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
                1 << sequenceHeader.SuperblockSizeLog2,
                disallow4x4AllFrames: false);

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
                    blockWorkspace));

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
                    blockWorkspace));

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
                    blockWorkspace));
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
                    writer, source, reconstruction, picture, coefficients, superblockWorkspace, blockWorkspace));

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
                    writer, source, reconstruction, picture, coefficients, superblockWorkspace, blockWorkspace));
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
                    blockWorkspace));

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
                    blockWorkspace));
    }

    [Theory]
    [InlineData((int)Av1BlockSize.Block16x8, false)]
    [InlineData((int)Av1BlockSize.Block8x16, false)]
    [InlineData((int)Av1BlockSize.Block64x32, false)]
    [InlineData((int)Av1BlockSize.Block64x32, true)]
    public void RectangularPalettesPreservePixels(int blockSizeValue, bool lossless)
    {
        Av1BlockSize blockSize = (Av1BlockSize)blockSizeValue;
        int width = blockSize.GetWidth();
        int height = blockSize.GetHeight();
        int qIndex = lossless ? 0 : 4;
        ObuColorConfig colorConfig = new()
        {
            BitDepth = Av1BitDepth.EightBit,
            SubSamplingX = false,
            SubSamplingY = false
        };

        using Av1EncoderFrameBuffer<byte> source = new(Configuration.Default, width, height, 8, Av1ColorFormat.Yuv444, 0, 0, lumaBorder: 64);
        using Av1EncoderFrameBuffer<byte> reconstruction = new(Configuration.Default, width, height, 8, Av1ColorFormat.Yuv444, 0, 0, lumaBorder: 64);
        for (int planeIndex = 0; planeIndex < 3; planeIndex++)
        {
            Buffer2DRegion<byte> plane = source.Frame.CodedView.GetPlane((Av1Plane)planeIndex);
            for (int y = 0; y < height; y++)
            {
                Span<byte> row = plane.DangerousGetRowSpan(y);
                for (int x = 0; x < width; x++)
                {
                    // Luma and the UV pair use independent spatial patterns. The eight UV points lie
                    // along one line, allowing every centroid to converge without a local clustering minimum.
                    uint seed = (uint)((y * width) + x + (planeIndex == 0 ? 0 : width * height));
                    seed = unchecked((seed ^ (seed >> 16)) * 0x7FEB352DU);
                    seed = unchecked((seed ^ (seed >> 15)) * 0x846CA68BU);
                    int color = (int)((seed ^ (seed >> 16)) & 7);
                    row[x] = (byte)(16 + (color * 29) + planeIndex);
                }
            }
        }

        using Av1EncoderModeInfoBuffer modeInfoBuffer = new(Configuration.Default, width, height, disallow4x4AllFrames: false);
        Av1PictureControlSet template = CreatePicture(modeInfoBuffer, colorConfig, use128x128Superblock: false, qIndex);
        template.Parent.FrameHeader.AllowScreenContentTools = true;
        template.Parent.FrameHeader.CodedLossless = lossless;
        template.Parent.FrameHeader.TransformMode = Av1TransformMode.Select;
        using Av1EncoderPictureBuffer pictureBuffer = new(
            Configuration.Default, template.Sequence.SequenceHeader, template.Parent.FrameHeader, width, height, 1 << template.Sequence.SequenceHeader.SuperblockSizeLog2, disallow4x4AllFrames: false);

        Av1PictureControlSet picture = pictureBuffer.Picture;
        using Av1EncoderCoefficientBuffer coefficients = new(Configuration.Default, template.Sequence.SequenceHeader, width, height);
        using Av1EncoderSuperblockWorkspace superblockWorkspace = new(Configuration.Default);
        using Av1EncoderBlockWorkspace blockWorkspace = new(Configuration.Default);
        Av1Superblock superblock = new()
        {
            Workspace = superblockWorkspace,
            TileInfo = new Av1TileInfo(0, 0, picture.Parent.FrameHeader),
            Index = 0
        };

        Av1IntraSuperblockEncoder.Prepare(picture, superblock, Point.Empty);
        ref Av1MacroBlockModeInfo modeInfo = ref picture.GetMacroBlockModeInfo(Point.Empty);
        modeInfo.Block.BlockSize = blockSize;
        picture.MapModeInfoBlock(Point.Empty, blockSize);
        Av1MacroBlockD macroBlock = new() { Tile = superblock.TileInfo };
        Av1TileWriter.SetModeInfoRowAndColumn(
            picture,
            macroBlock,
            superblock.TileInfo,
            Point.Empty,
            blockSize,
            picture.Parent.Common.ModeInfoStride,
            picture.Parent.Common.ModeInfoRowCount,
            picture.Parent.Common.ModeInfoColumnCount);

        // Block decisions read frame-level state that the tile encoder prepares once for each picture.
        Av1TileEncoder.PrepareFrame(picture, new Size(source.Frame.Width, source.Frame.Height), blockWorkspace);
        Av1IntraSuperblockEncoder.ModeDecision<byte, Av1IntraSuperblockEncoder.ByteOperator> decision = new(
            source.Frame, default, reconstruction.Frame, picture, superblock, coefficients, blockWorkspace);

        Av1EncoderBlockStruct block = default;
        Av1EncoderPaletteInfo palette = default;
        using Av1SymbolEncoder writer = new(Configuration.Default, 8192, qIndex, updateCdf: true);
        decision.EncodeBlock(writer, macroBlock, Point.Empty, 0, ref modeInfo, ref block, ref palette);
        Assert.Equal(8, palette.PaletteSizes[0]);
        Assert.Equal(8, palette.PaletteSizes[1]);
        if (lossless)
        {
            Assert.Equal(Av1TransformSize.Size4x4, modeInfo.Block.TransformSize);
        }

        for (int planeIndex = 0; planeIndex < 3; planeIndex++)
        {
            Buffer2DRegion<byte> expected = source.Frame.CodedView.GetPlane((Av1Plane)planeIndex);
            Buffer2DRegion<byte> actual = reconstruction.Frame.CodedView.GetPlane((Av1Plane)planeIndex);
            for (int y = 0; y < height; y++)
            {
                Assert.Equal(expected.DangerousGetRowSpan(y)[..width], actual.DangerousGetRowSpan(y)[..width]);
            }
        }
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
            disallow4x4AllFrames: false);

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
            1 << pictureTemplate.Sequence.SequenceHeader.SuperblockSizeLog2,
            disallow4x4AllFrames: false);

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
            blockWorkspace);

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
        using Av1EncoderModeInfoBuffer modeInfo = new(Configuration.Default, Width, Height, disallow4x4AllFrames: false);
        Av1PictureControlSet pictureTemplate = CreatePicture(modeInfo, colorConfig, use128x128Superblock: false, QIndex);
        using Av1EncoderPictureBuffer picture = new(
            Configuration.Default,
            pictureTemplate.Sequence.SequenceHeader,
            pictureTemplate.Parent.FrameHeader,
            Width,
            Height,
            1 << pictureTemplate.Sequence.SequenceHeader.SuperblockSizeLog2,
            disallow4x4AllFrames: false);

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
            blockWorkspace);

        ref Av1MacroBlockModeInfo targetBlock = ref picture.Picture.GetFromModeInfoGrid(new Point(2, 2));
        Assert.Equal(Av1BlockSize.Block8x8, targetBlock.Block.BlockSize);
        Assert.Equal(expectedMode, targetBlock.Block.Mode);
        Assert.Equal(
            expectedAngleDelta,
            GetBlockEncoding(picture.Picture, new Point(2, 2)).PredictionUnit.AngleDelta[(int)Av1PlaneType.Y]);

        Av1EncoderTransformBlockState targetState = coefficients.GetTransformBlockSpan(0, Av1Plane.Y)[
            GetTransformStateIndex(picture.Picture, superblockWorkspace, new Point(2, 2), Av1Plane.Y)];

        // Every transform has the same skip cost for this exact-prediction target, so reference enum order
        // requires DCT-DCT to win even when the mode-derived first pass used another transform.
        Assert.Equal((ushort)0, targetState.EndOfBlock);
        Assert.Equal(Av1TransformType.DctDct, targetState.TransformType);
        Assert.NotEqual(0, tileWriter.GetTileData(0).Length);
    }

    /// <summary>
    /// Verifies that the production tile selects the chroma mode whose prediction from the current reconstruction
    /// matches the chroma target.
    /// </summary>
    /// <remarks>
    /// The chroma search evaluates the zero delta of a directional mode first and abandons the mode when that is
    /// not within one eighth of the best earlier candidate, so a target angle must lie closer to the zero delta of
    /// its own mode than to the extreme delta of a mode searched earlier: 194 degrees, the negative extreme of
    /// D203, is nearer to the positive extreme of the horizontal mode and is never reached. The narrow chroma
    /// blocks of the subsampled formats leave D45 too little top-right edge to separate its deltas.
    /// </remarks>
    [Theory]
    [InlineData((int)Av1ChromaPredictionMode.Vertical, 0, (int)Av1TransformType.AdstDct, (int)Av1ColorFormat.Yuv444)]
    [InlineData((int)Av1ChromaPredictionMode.Horizontal, 0, (int)Av1TransformType.DctAdst, (int)Av1ColorFormat.Yuv420)]
    [InlineData((int)Av1ChromaPredictionMode.Paeth, 0, (int)Av1TransformType.AdstAdst, (int)Av1ColorFormat.Yuv422)]
    [InlineData((int)Av1ChromaPredictionMode.Directional45Degrees, -3, (int)Av1TransformType.DctDct, (int)Av1ColorFormat.Yuv444)]
    [InlineData((int)Av1ChromaPredictionMode.Directional135Degrees, 3, (int)Av1TransformType.AdstAdst, (int)Av1ColorFormat.Yuv422)]
    [InlineData((int)Av1ChromaPredictionMode.Directional203Degrees, -1, (int)Av1TransformType.DctAdst, (int)Av1ColorFormat.Yuv420)]
    public void ProductionTileSelectsChromaModeFromCurrentReconstruction(
        int expectedModeValue,
        int expectedAngleDelta,
        int expectedTransformTypeValue,
        int colorFormatValue)
    {
        const int Width = 16;
        const int Height = 16;
        const int QIndex = 1;
        const int MaximumEncodeCount = 8;
        Av1ChromaPredictionMode expectedMode = (Av1ChromaPredictionMode)expectedModeValue;
        Av1TransformType expectedTransformType = (Av1TransformType)expectedTransformTypeValue;
        Av1ColorFormat colorFormat = (Av1ColorFormat)colorFormatValue;
        bool subsamplingX = colorFormat is Av1ColorFormat.Yuv420 or Av1ColorFormat.Yuv422;
        bool subsamplingY = colorFormat == Av1ColorFormat.Yuv420;
        int chromaSubsamplingX = subsamplingX ? 1 : 0;
        int chromaSubsamplingY = subsamplingY ? 1 : 0;
        Point targetPosition = new(2, 2);
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

        // The luma frame keeps the target an 8x8 block. The chroma target is the prediction of the expected mode
        // from the reconstructed chroma edges plus a checkerboard, and the edges come from the preceding encode, so
        // an encode is conclusive only when it reproduces the luma and chroma references of its own source.
        Span<byte> previousLuma = stackalloc byte[Width * Height];
        Span<byte> actualLuma = stackalloc byte[Width * Height];
        Span<byte> previousBlue = stackalloc byte[Width * Height];
        Span<byte> previousRed = stackalloc byte[Width * Height];
        Span<byte> filterScratch = stackalloc byte[Av1FilterIntraPredictorBase.ScratchLength];
        bool hasTarget = false;
        string blocks = string.Empty;
        for (int encodeIndex = 0; encodeIndex < MaximumEncodeCount; encodeIndex++)
        {
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

            FillToolActivationLuma(
                source.Frame.CodedView.GetPlane(Av1Plane.Y),
                hasTarget ? previousLuma : default,
                Av1FilterIntraMode.DC,
                8,
                static (mode, destination, stride, above, left, width, height, _, scratch) =>
                    Av1FilterIntraPredictorBase.GetPredictor(mode)
                        .Predict(destination, stride, above, left, width, height, scratch),
                filterScratch);

            Buffer2DRegion<byte> blue = source.Frame.CodedView.GetPlane(Av1Plane.U);
            Buffer2DRegion<byte> red = source.Frame.CodedView.GetPlane(Av1Plane.V);
            FillChromaModeSelectionPlane(blue, transformSize, expectedMode, expectedAngleDelta, hasTarget ? previousBlue : default);
            FillChromaModeSelectionPlane(red, transformSize, expectedMode, expectedAngleDelta, hasTarget ? previousRed : default);
            ClearPlane(reconstruction.Luma);
            ClearPlane(Assert.IsType<Buffer2D<byte>>(reconstruction.ChromaBlue));
            ClearPlane(Assert.IsType<Buffer2D<byte>>(reconstruction.ChromaRed));
            using Av1EncoderModeInfoBuffer modeInfo = new(Configuration.Default, Width, Height, disallow4x4AllFrames: false);
            Av1PictureControlSet pictureTemplate = CreatePicture(modeInfo, colorConfig, use128x128Superblock: false, QIndex);
            pictureTemplate.Sequence.SequenceHeader.EnableFilterIntra = true;

            // Still images select the all-intra search settings, as libavif does.
            pictureTemplate.Sequence.SequenceHeader.IsStillPicture = true;
            pictureTemplate.Parent.FrameHeader.AllowScreenContentTools = true;
            using Av1EncoderPictureBuffer picture = new(
                Configuration.Default,
                pictureTemplate.Sequence.SequenceHeader,
                pictureTemplate.Parent.FrameHeader,
                Width,
                Height,
                1 << pictureTemplate.Sequence.SequenceHeader.SuperblockSizeLog2,
                disallow4x4AllFrames: false);

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
                blockWorkspace);

            Assert.NotEqual(0, tileWriter.GetTileData(0).Length);
            Buffer2DRegion<byte> reconstructedLuma = reconstruction.Frame.CodedView.GetPlane(Av1Plane.Y);
            for (int y = 0; y < Height; y++)
            {
                reconstructedLuma.DangerousGetRowSpan(y).CopyTo(actualLuma[(y * Width)..]);
            }

            Buffer2DRegion<byte> reconstructedBlue = reconstruction.Frame.CodedView.GetPlane(Av1Plane.U);
            Buffer2DRegion<byte> reconstructedRed = reconstruction.Frame.CodedView.GetPlane(Av1Plane.V);
            blocks = DescribeBlocks(picture.Picture);
            bool stable = hasTarget && LumaReferencesEqual(actualLuma, previousLuma) &&
                ChromaReferencesEqual(reconstructedBlue, previousBlue, transformSize) &&
                ChromaReferencesEqual(reconstructedRed, previousRed, transformSize);
            if (!stable)
            {
                actualLuma.CopyTo(previousLuma);
                CopyPlane(reconstructedBlue, previousBlue);
                CopyPlane(reconstructedRed, previousRed);
                hasTarget = true;
                continue;
            }

            // The target must be its own 8x8 block. The description of every block explains a wrong selection.
            ref Av1MacroBlockModeInfo targetBlock = ref picture.Picture.GetFromModeInfoGrid(targetPosition);
            Assert.True(targetBlock.Block.BlockSize == Av1BlockSize.Block8x8, blocks);
            Assert.True(targetBlock.Block.UvMode == expectedMode, blocks);
            Assert.Equal(
                expectedAngleDelta,
                GetBlockEncoding(picture.Picture, targetPosition).PredictionUnit.AngleDelta[(int)Av1PlaneType.Uv]);

            Av1EncoderTransformBlockState blueState = coefficients.GetTransformBlockSpan(0, Av1Plane.U)[
                GetTransformStateIndex(picture.Picture, superblockWorkspace, targetPosition, Av1Plane.U)];

            Av1EncoderTransformBlockState redState = coefficients.GetTransformBlockSpan(0, Av1Plane.V)[
                GetTransformStateIndex(picture.Picture, superblockWorkspace, targetPosition, Av1Plane.V)];

            Assert.NotEqual((ushort)0, blueState.EndOfBlock);
            Assert.NotEqual((ushort)0, redState.EndOfBlock);
            Assert.Equal(expectedTransformType, blueState.TransformType);
            Assert.Equal(expectedTransformType, redState.TransformType);
            return;
        }

        Assert.Fail(
            $"The references of the target block changed in each of {MaximumEncodeCount} encodes, " +
            $"so no encode contains an exact prediction. Last encode: {blocks}");

        // Compares the chroma edges of the target: its top edge, its left edge, and their corner.
        static bool ChromaReferencesEqual(Buffer2DRegion<byte> reconstruction, ReadOnlySpan<byte> reference, Av1TransformSize transformSize)
        {
            int width = transformSize.GetWidth();
            int height = transformSize.GetHeight();
            int stride = reconstruction.Width;
            bool equal = reconstruction.DangerousGetRowSpan(height - 1).Slice(width - 1, width + 1).SequenceEqual(
                reference.Slice(((height - 1) * stride) + width - 1, width + 1));
            for (int row = 0; row < height; row++)
            {
                equal &= reconstruction.DangerousGetRowSpan(height + row)[width - 1] == reference[((height + row) * stride) + width - 1];
            }

            return equal;
        }

        // Retains a reconstructed plane in row-major order for the next source.
        static void CopyPlane(Buffer2DRegion<byte> plane, Span<byte> destination)
        {
            for (int y = 0; y < plane.Height; y++)
            {
                plane.DangerousGetRowSpan(y).CopyTo(destination[(y * plane.Width)..]);
            }
        }
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
                    blockWorkspace),
            static (mode, destination, stride, above, left, width, height, _, scratch) =>
                Av1FilterIntraPredictorBase.GetPredictor(mode)
                    .Predict(destination, stride, above, left, width, height, scratch));

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
                    blockWorkspace),
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

    private static void VerifyProductionTileSelectsChromaFromReconstructedLuma<TSample>(
        int colorFormatValue,
        int bitDepth,
        TileWriterFactory<TSample> createWriter,
        FilterPrediction<TSample> predictFilter)
        where TSample : unmanaged, IBinaryInteger<TSample>
    {
        const int Width = 16;
        const int Height = 16;
        const int QIndex = 1;
        const int TileBufferLength = 512;
        const int TargetX = 8;
        const int TargetY = 8;
        const int AlphaU = 16;
        const int AlphaV = -16;
        const int MaximumEncodeCount = 8;
        Av1ColorFormat colorFormat = (Av1ColorFormat)colorFormatValue;
        bool subsamplingX = colorFormat is Av1ColorFormat.Yuv420 or Av1ColorFormat.Yuv422;
        bool subsamplingY = colorFormat == Av1ColorFormat.Yuv420;
        int chromaSubsamplingX = subsamplingX ? 1 : 0;
        int chromaSubsamplingY = subsamplingY ? 1 : 0;
        int midpoint = 1 << (bitDepth - 1);
        int maxSample = (1 << bitDepth) - 1;
        Point targetPosition = new(TargetX >> Av1Constants.ModeInfoSizeLog2, TargetY >> Av1Constants.ModeInfoSizeLog2);
        Av1TransformSize transformSize = Av1BlockSize.Block8x8.GetMaxUvTransformSize(
            subsamplingX,
            subsamplingY);

        int chromaWidth = transformSize.GetWidth();
        int chromaHeight = transformSize.GetHeight();
        int sampleCount = transformSize.GetSize2d();
        int lumaScaleShift = 3 - chromaSubsamplingX - chromaSubsamplingY;
        ObuColorConfig colorConfig = new()
        {
            IsMonochrome = false,
            SubSamplingX = subsamplingX,
            SubSamplingY = subsamplingY,
            BitDepth = (Av1BitDepth)((bitDepth - 8) / 2)
        };

        // The luma target is an exact filter prediction, so its reconstruction is its source once its edges are
        // stable. Every chroma sample is the midpoint except the chroma target, which is the exact chroma-from-luma
        // prediction of the reconstructed luma target over the flat DC context. The luma reconstruction comes from
        // the preceding encode, so an encode is conclusive only when it reproduces the references of its own source.
        Span<TSample> previous = stackalloc TSample[Width * Height];
        Span<TSample> actual = stackalloc TSample[Width * Height];
        Span<TSample> filterScratch = stackalloc TSample[Av1FilterIntraPredictorBase.ScratchLength];
        Span<short> lumaQ3 = stackalloc short[64];
        bool hasTarget = false;
        string blocks = string.Empty;
        for (int encodeIndex = 0; encodeIndex < MaximumEncodeCount; encodeIndex++)
        {
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

            FillToolActivationLuma(
                source.Frame.CodedView.GetPlane(Av1Plane.Y),
                hasTarget ? previous : default,
                Av1FilterIntraMode.DC,
                bitDepth,
                predictFilter,
                filterScratch);

            Buffer2DRegion<TSample> blue = source.Frame.CodedView.GetPlane(Av1Plane.U);
            Buffer2DRegion<TSample> red = source.Frame.CodedView.GetPlane(Av1Plane.V);
            FillPlane(blue, TSample.CreateChecked(midpoint));
            FillPlane(red, TSample.CreateChecked(midpoint));
            if (hasTarget)
            {
                int sumQ3 = sampleCount >> 1;
                for (int row = 0; row < chromaHeight; row++)
                {
                    for (int column = 0; column < chromaWidth; column++)
                    {
                        int lumaSum = 0;
                        int lumaX = TargetX + (column << chromaSubsamplingX);
                        int lumaY = TargetY + (row << chromaSubsamplingY);
                        for (int offsetY = 0; offsetY <= chromaSubsamplingY; offsetY++)
                        {
                            for (int offsetX = 0; offsetX <= chromaSubsamplingX; offsetX++)
                            {
                                lumaSum += int.CreateChecked(previous[((lumaY + offsetY) * Width) + lumaX + offsetX]);
                            }
                        }

                        short sampleQ3 = (short)(lumaSum << lumaScaleShift);
                        lumaQ3[(row * chromaWidth) + column] = sampleQ3;
                        sumQ3 += sampleQ3;
                    }
                }

                int averageQ3 = sumQ3 >> (transformSize.GetBlockWidthLog2() + transformSize.GetBlockHeightLog2());
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
            }

            ClearPlane(reconstruction.Luma);
            ClearPlane(Assert.IsType<Buffer2D<TSample>>(reconstruction.ChromaBlue));
            ClearPlane(Assert.IsType<Buffer2D<TSample>>(reconstruction.ChromaRed));
            using Av1EncoderModeInfoBuffer modeInfo = new(Configuration.Default, Width, Height, disallow4x4AllFrames: false);
            Av1PictureControlSet pictureTemplate = CreatePicture(modeInfo, colorConfig, use128x128Superblock: false, QIndex);
            pictureTemplate.Sequence.SequenceHeader.EnableFilterIntra = true;

            // Still images select the all-intra search settings, as libavif does.
            pictureTemplate.Sequence.SequenceHeader.IsStillPicture = true;
            pictureTemplate.Parent.FrameHeader.AllowScreenContentTools = true;
            using Av1EncoderPictureBuffer picture = new(
                Configuration.Default,
                pictureTemplate.Sequence.SequenceHeader,
                pictureTemplate.Parent.FrameHeader,
                Width,
                Height,
                1 << pictureTemplate.Sequence.SequenceHeader.SuperblockSizeLog2,
                disallow4x4AllFrames: false);

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

            Assert.NotEqual(0, tileWriter.GetTileData(0).Length);
            Buffer2DRegion<TSample> actualLuma = reconstruction.Frame.CodedView.GetPlane(Av1Plane.Y);
            for (int y = 0; y < Height; y++)
            {
                actualLuma.DangerousGetRowSpan(y).CopyTo(actual[(y * Width)..]);
            }

            blocks = DescribeBlocks(picture.Picture);
            if (!hasTarget || !LumaReferencesEqual(actual, previous) || !TargetLumaEqual(actual, previous))
            {
                actual.CopyTo(previous);
                hasTarget = true;
                continue;
            }

            // The chroma target must be its own block whose DC context is flat, so the prediction is exactly the
            // midpoint plus the scaled luma deviation. The description of every block explains a wrong selection.
            ref Av1MacroBlockModeInfo targetBlock = ref picture.Picture.GetFromModeInfoGrid(targetPosition);
            Assert.True(targetBlock.Block.BlockSize == Av1BlockSize.Block8x8, blocks);
            Assert.True(targetBlock.Block.UvMode == Av1ChromaPredictionMode.ChromaFromLuma, blocks);
            Assert.Equal(
                Av1ChromaFromLumaMath.JointSign(
                    Av1ChromaFromLumaMath.SignPositive,
                    Av1ChromaFromLumaMath.SignNegative),
                GetBlockEncoding(picture.Picture, targetPosition).PredictionUnit.ChromaFromLumaSigns);

            Assert.Equal(
                Av1ChromaFromLumaMath.PackIndices(
                    Av1ChromaFromLumaMath.AlphaToMagnitudeIndex(AlphaU),
                    Av1ChromaFromLumaMath.AlphaToMagnitudeIndex(AlphaV)),
                GetBlockEncoding(picture.Picture, targetPosition).PredictionUnit.ChromaFromLumaIndex);

            Av1EncoderTransformBlockState blueState = coefficients.GetTransformBlockSpan(0, Av1Plane.U)[
                GetTransformStateIndex(picture.Picture, superblockWorkspace, targetPosition, Av1Plane.U)];

            Av1EncoderTransformBlockState redState = coefficients.GetTransformBlockSpan(0, Av1Plane.V)[
                GetTransformStateIndex(picture.Picture, superblockWorkspace, targetPosition, Av1Plane.V)];

            Assert.Equal((ushort)0, blueState.EndOfBlock);
            Assert.Equal((ushort)0, redState.EndOfBlock);
            Assert.Equal(Av1TransformType.DctDct, blueState.TransformType);
            Assert.Equal(Av1TransformType.DctDct, redState.TransformType);

            Buffer2DRegion<TSample> actualBlue = reconstruction.Frame.CodedView.GetPlane(Av1Plane.U);
            Buffer2DRegion<TSample> actualRed = reconstruction.Frame.CodedView.GetPlane(Av1Plane.V);
            TSample midpointSample = TSample.CreateChecked(midpoint);
            for (int column = 0; column < chromaWidth; column++)
            {
                Assert.Equal(midpointSample, actualBlue.DangerousGetRowSpan(chromaHeight - 1)[chromaWidth + column]);
                Assert.Equal(midpointSample, actualRed.DangerousGetRowSpan(chromaHeight - 1)[chromaWidth + column]);
            }

            for (int row = 0; row < chromaHeight; row++)
            {
                Assert.Equal(midpointSample, actualBlue.DangerousGetRowSpan(chromaHeight + row)[chromaWidth - 1]);
                Assert.Equal(midpointSample, actualRed.DangerousGetRowSpan(chromaHeight + row)[chromaWidth - 1]);
                Assert.Equal(
                    blue.DangerousGetRowSpan(chromaHeight + row).Slice(chromaWidth, chromaWidth),
                    actualBlue.DangerousGetRowSpan(chromaHeight + row).Slice(chromaWidth, chromaWidth));
                Assert.Equal(
                    red.DangerousGetRowSpan(chromaHeight + row).Slice(chromaWidth, chromaWidth),
                    actualRed.DangerousGetRowSpan(chromaHeight + row).Slice(chromaWidth, chromaWidth));
            }

            return;
        }

        Assert.Fail(
            $"The references of the predicted quadrants changed in each of {MaximumEncodeCount} encodes, " +
            $"so no encode contains an exact prediction. Last encode: {blocks}");

        // Compares the reconstructed luma target, which the chroma target scales.
        static bool TargetLumaEqual(ReadOnlySpan<TSample> reconstruction, ReadOnlySpan<TSample> reference)
        {
            bool equal = true;
            for (int y = TargetY; y < Height; y++)
            {
                equal &= reconstruction.Slice((y * Width) + TargetX, 8).SequenceEqual(reference.Slice((y * Width) + TargetX, 8));
            }

            return equal;
        }
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
            HeifEncodingSpeed.Level0,
            static (writer, source, reconstruction, picture, coefficients, superblockWorkspace, blockWorkspace) =>
                new(
                    writer,
                    source,
                    reconstruction,
                    picture,
                    coefficients,
                    superblockWorkspace,
                    blockWorkspace),
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
            HeifEncodingSpeed.Level0,
            static (writer, source, reconstruction, picture, coefficients, superblockWorkspace, blockWorkspace) =>
                new(
                    writer,
                    source,
                    reconstruction,
                    picture,
                    coefficients,
                    superblockWorkspace,
                    blockWorkspace),
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
            HeifEncodingSpeed.Level0,
            static (writer, source, reconstruction, picture, coefficients, superblockWorkspace, blockWorkspace) =>
                new(
                    writer,
                    source,
                    reconstruction,
                    picture,
                    coefficients,
                    superblockWorkspace,
                    blockWorkspace),
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
            HeifEncodingSpeed.Level0,
            static (writer, source, reconstruction, picture, coefficients, superblockWorkspace, blockWorkspace) =>
                new(
                    writer,
                    source,
                    reconstruction,
                    picture,
                    coefficients,
                    superblockWorkspace,
                    blockWorkspace),
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
        HeifEncodingSpeed speed,
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
        const int MaximumEncodeCount = 8;
        const int ResidualAmplitude = 40;
        const Av1TransformSize TransformSize = Av1TransformSize.Size8x8;
        Av1FilterIntraMode filterIntraMode = (Av1FilterIntraMode)filterIntraModeValue;
        int sampleScale = 1 << (bitDepth - 8);
        Point targetPosition = new(TargetX >> Av1Constants.ModeInfoSizeLog2, TargetY >> Av1Constants.ModeInfoSizeLog2);
        ObuColorConfig colorConfig = new()
        {
            IsMonochrome = true,
            SubSamplingX = true,
            SubSamplingY = true,
            BitDepth = (Av1BitDepth)((bitDepth - 8) / 2)
        };

        // Every predicted quadrant consumes the reconstruction of the preceding encode, so an encode is conclusive
        // only when it reproduces every reference sample of the encode that produced its source. Split transforms
        // also predict from the reconstruction of the earlier transforms of the same block.
        Span<TSample> previous = stackalloc TSample[Width * Height];
        Span<TSample> actual = stackalloc TSample[Width * Height];
        Span<TSample> filterScratch = stackalloc TSample[Av1FilterIntraPredictorBase.ScratchLength];
        long predictionOnlyError = 0;
        bool hasTarget = false;
        string blocks = string.Empty;
        for (int encodeIndex = 0; encodeIndex < MaximumEncodeCount; encodeIndex++)
        {
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
            FillToolActivationLuma(sourceLuma, hasTarget ? previous : default, filterIntraMode, bitDepth, predictFilter, filterScratch);
            if (hasTarget && useSplitTransform)
            {
                predictionOnlyError = BuildSplitTarget(previous, sourceLuma, filterScratch);
            }

            ClearPlane(reconstruction.Luma);
            using Av1EncoderModeInfoBuffer modeInfo = new(Configuration.Default, Width, Height, disallow4x4AllFrames: false);
            Av1PictureControlSet pictureTemplate = CreatePicture(modeInfo, colorConfig, use128x128Superblock: false, QIndex);
            pictureTemplate.Sequence.SequenceHeader.EnableFilterIntra = true;

            // Still images select the all-intra search settings, as libavif does.
            pictureTemplate.Sequence.SequenceHeader.IsStillPicture = true;
            pictureTemplate.Parent.FrameHeader.AllowScreenContentTools = true;
            pictureTemplate.Parent.FrameHeader.TransformMode = useSplitTransform
                ? Av1TransformMode.Select
                : Av1TransformMode.Largest;

            using Av1EncoderPictureBuffer picture = new(
                Configuration.Default,
                pictureTemplate.Sequence.SequenceHeader,
                pictureTemplate.Parent.FrameHeader,
                Width,
                Height,
                1 << pictureTemplate.Sequence.SequenceHeader.SuperblockSizeLog2,
                disallow4x4AllFrames: false);

            picture.Picture.Parent.EncodingSpeed = speed;
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
            symbolEncoder.EncodingSpeed = speed;

            Av1TileEncoder tileWriter = createWriter(
                symbolEncoder,
                source.Frame,
                reconstruction.Frame,
                picture.Picture,
                coefficients,
                superblockWorkspace,
                blockWorkspace);

            Assert.NotEqual(0, tileWriter.GetTileData(0).Length);
            Buffer2DRegion<TSample> actualLuma = reconstruction.Frame.CodedView.GetPlane(Av1Plane.Y);
            for (int y = 0; y < Height; y++)
            {
                actualLuma.DangerousGetRowSpan(y).CopyTo(actual[(y * Width)..]);
            }

            blocks = DescribeBlocks(picture.Picture);
            if (!hasTarget || !LumaReferencesEqual(actual, previous) || (useSplitTransform && !InteriorReferencesEqual(actual, previous)))
            {
                actual.CopyTo(previous);
                hasTarget = true;
                continue;
            }

            // The target must be its own 8x8 block: a larger block would predict it with another mode, and smaller
            // blocks would each carry their own syntax. The description of every block explains a wrong selection.
            ref Av1MacroBlockModeInfo targetBlock = ref picture.Picture.GetFromModeInfoGrid(targetPosition);
            Assert.True(targetBlock.Block.BlockSize == Av1BlockSize.Block8x8, blocks);
            Assert.True(targetBlock.Block.Mode == Av1PredictionMode.DC, blocks);
            Assert.True(GetBlockEncoding(picture.Picture, targetPosition).FilterIntraMode == filterIntraMode, blocks);
            Assert.True(
                targetBlock.Block.TransformSize == (useSplitTransform ? Av1TransformSize.Size4x4 : Av1TransformSize.Size8x8),
                blocks);

            int targetTransformIndex = GetTransformStateIndex(
                picture.Picture,
                superblockWorkspace,
                targetPosition,
                Av1Plane.Y);

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

            long reconstructionError = 0;
            for (int row = 0; row < 8; row++)
            {
                ReadOnlySpan<TSample> targetRow = sourceLuma.DangerousGetRowSpan(TargetY + row).Slice(TargetX, 8);
                ReadOnlySpan<TSample> actualRow = actual.Slice(((TargetY + row) * Width) + TargetX, 8);
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
                Assert.InRange(reconstructionError, 1, predictionOnlyError - 1);

                byte[] payload = WriteCompleteTileObu(pictureTemplate, tileWriter, Width, Height);
                using Av1Decoder decoder = new(Configuration.Default);
                using Av1FrameBuffer<byte> decodedPlanes = decoder.DecodeFrameBuffer(payload, null, null, out _);
                using Image<Rgba32> decoded = new(Configuration.Default, decodedPlanes.Width, decodedPlanes.Height);
                Av1YuvConverter.ConvertToRgb(
                    Configuration.Default,
                    decodedPlanes,
                    decoded.Bounds,
                    decoded.Frames.RootFrame.PixelBuffer.GetRegion(decoded.Bounds),
                    decoded.Size,
                    default,
                    null,
                    null,
                    default,
                    default,
                    false,
                    HeifChromaUpsampling.Auto,
                    decodedPlanes.ColorConfig.ColorRange);

                Assert.NotNull(decoder.FrameInfo);
                Av1BlockModeInfo decodedBlock = decoder.FrameInfo.GetModeInfoAt(targetPosition);
                Assert.True(decodedBlock.UseFilterIntra);
                Assert.Equal(filterIntraMode, decodedBlock.FilterIntraMode);
                Assert.Equal(4, decodedBlock.GetTransformUnitCount(Av1Plane.Y));
                Assert.Equal(new Size(Width, Height), decoded.Size);
            }

            return;
        }

        Assert.Fail(
            $"The references of the predicted quadrants changed in each of {MaximumEncodeCount} encodes, " +
            $"so no encode contains an exact prediction. Last encode: {blocks}");

        // Compares the interior samples that the later transforms of the split target predict from: the last row
        // and the last column of its first transforms.
        static bool InteriorReferencesEqual(ReadOnlySpan<TSample> reconstruction, ReadOnlySpan<TSample> reference)
        {
            bool equal = reconstruction.Slice((11 * Width) + TargetX, 8).SequenceEqual(reference.Slice((11 * Width) + TargetX, 8));
            for (int y = TargetY; y < Height; y++)
            {
                equal &= reconstruction[(y * Width) + 11] == reference[(y * Width) + 11];
            }

            return equal;
        }

        // Replaces the target by the prediction of each 4x4 transform from the reconstruction of the transforms before
        // it, plus a residual pattern for each transform. Returns the energy of the residual patterns.
        long BuildSplitTarget(ReadOnlySpan<TSample> reconstruction, Buffer2DRegion<TSample> plane, Span<TSample> scratch)
        {
            ReadOnlySpan<TSample> edgeAboveStorage = reconstruction.Slice((7 * Width) + 7, 9);
            ReadOnlySpan<TSample> interior = reconstruction[((TargetY * Width) + TargetX)..];
            Span<TSample> edgeLeft = stackalloc TSample[8];
            Span<TSample> prediction = stackalloc TSample[TransformSize.GetSize2d()];
            Span<TSample> transformAboveStorage = stackalloc TSample[5];
            Span<TSample> transformAbove = transformAboveStorage[1..];
            Span<TSample> transformLeft = stackalloc TSample[4];
            for (int row = 0; row < 8; row++)
            {
                edgeLeft[row] = reconstruction[((TargetY + row) * Width) + 7];
            }

            long residualEnergy = 0;
            for (int transformRow = 0; transformRow < 2; transformRow++)
            {
                int rowOffset = transformRow * 4;
                for (int transformColumn = 0; transformColumn < 2; transformColumn++)
                {
                    int columnOffset = transformColumn * 4;
                    ReadOnlySpan<TSample> availableAbove = transformRow == 0
                        ? edgeAboveStorage.Slice(1 + columnOffset, 4)
                        : interior.Slice(((rowOffset - 1) * Width) + columnOffset, 4);

                    // The predictor consumes the corner through the element immediately before the top-edge span.
                    // Later transforms use reconstructed samples of the same 8x8 block, which quantization moves away
                    // from the target, so those samples come from the preceding encode.
                    transformAboveStorage[0] = transformRow == 0
                        ? edgeAboveStorage[columnOffset]
                        : transformColumn == 0
                            ? edgeLeft[rowOffset - 1]
                            : interior[((rowOffset - 1) * Width) + columnOffset - 1];

                    availableAbove.CopyTo(transformAbove);
                    for (int row = 0; row < 4; row++)
                    {
                        transformLeft[row] = transformColumn == 0
                            ? edgeLeft[rowOffset + row]
                            : interior[((rowOffset + row) * Width) + columnOffset - 1];
                    }

                    predictFilter(
                        filterIntraMode,
                        prediction[((rowOffset * 8) + columnOffset)..],
                        8,
                        transformAbove,
                        transformLeft,
                        4,
                        4,
                        bitDepth,
                        scratch);

                    // Distinct transform-local frequency patterns remain compact in separate 4x4 bases but spread
                    // across coefficients when a single 8x8 transform spans the discontinuities between quadrants.
                    // The rows and columns that later transforms predict from stay at the prediction, so the
                    // whole-block model that ranks the filter against spatial modes before transform search sees
                    // residual only where no later prediction depends on it.
                    int transformIndex = (transformRow * 2) + transformColumn;
                    int patternRows = transformRow == 0 ? 3 : 4;
                    int patternColumns = transformColumn == 0 ? 3 : 4;
                    for (int row = 0; row < patternRows; row++)
                    {
                        Span<TSample> targetRow = prediction.Slice(((rowOffset + row) * 8) + columnOffset, patternColumns);
                        for (int column = 0; column < targetRow.Length; column++)
                        {
                            int residualSign = transformIndex switch
                            {
                                0 => row < 2 ? -1 : 1,
                                1 => column < 2 ? -1 : 1,
                                2 => (row < 2) == (column < 2) ? -1 : 1,
                                _ => ((row + column) & 1) == 0 ? -1 : 1
                            };

                            int residual = residualSign * ResidualAmplitude * sampleScale;
                            targetRow[column] = TSample.CreateChecked(int.CreateChecked(targetRow[column]) + residual);
                            residualEnergy += (long)residual * residual;
                        }
                    }
                }
            }

            for (int row = 0; row < 8; row++)
            {
                prediction.Slice(row * 8, 8).CopyTo(plane.DangerousGetRowSpan(TargetY + row).Slice(TargetX, 8));
            }

            return residualEnergy;
        }
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
        using Av1EncoderModeInfoBuffer modeInfo = new(Configuration.Default, Width, Height, disallow4x4AllFrames: false);
        Av1PictureControlSet pictureTemplate = CreatePicture(modeInfo, colorConfig, use128x128Superblock: false, QIndex);
        using Av1EncoderPictureBuffer picture = new(
            Configuration.Default,
            pictureTemplate.Sequence.SequenceHeader,
            pictureTemplate.Parent.FrameHeader,
            Width,
            Height,
            1 << pictureTemplate.Sequence.SequenceHeader.SuperblockSizeLog2,
            disallow4x4AllFrames: false);

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
            blockWorkspace);

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
                    blockWorkspace));

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
                    blockWorkspace));
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
            disallow4x4AllFrames: false);

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
            1 << pictureTemplate.Sequence.SequenceHeader.SuperblockSizeLog2,
            disallow4x4AllFrames: false);

        using Av1EncoderCoefficientBuffer coefficients = new(
            Configuration.Default,
            pictureTemplate.Sequence.SequenceHeader,
            Width,
            Height);

        using Av1EncoderSuperblockWorkspace superblockWorkspace = new(Configuration.Default);
        using Av1EncoderBlockWorkspace blockWorkspace = new(
            Configuration.Default,
            allocateInterMotionCosts: false,
            allocateDisplacementCosts: true,
            pictureTemplate.Sequence.SequenceHeader.SuperblockSize);

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
            disallow4x4AllFrames: false);

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
            1 << pictureTemplate.Sequence.SequenceHeader.SuperblockSizeLog2,
            disallow4x4AllFrames: false);

        using Av1EncoderCoefficientBuffer coefficients = new(
            Configuration.Default,
            pictureTemplate.Sequence.SequenceHeader,
            Width,
            Height);

        using Av1EncoderSuperblockWorkspace superblockWorkspace = new(Configuration.Default);
        using Av1EncoderBlockWorkspace blockWorkspace = new(
            Configuration.Default,
            allocateInterMotionCosts: false,
            allocateDisplacementCosts: true,
            pictureTemplate.Sequence.SequenceHeader.SuperblockSize);

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
            blockWorkspace);

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
            1 << sequenceHeader.SuperblockSizeLog2,
            disallow4x4AllFrames: false);

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
            blockWorkspace);

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

        // The last superblock holds 8x8 visible samples, which the partition search codes as one block or as
        // smaller blocks; the block that covers the last position must fit inside them.
        ref Av1MacroBlockModeInfo bottomRight = ref picture.Picture.GetFromModeInfoGrid(new Point(16, 16));
        Assert.True(bottomRight.Block.BlockSize.GetWidth() <= 8);
        Assert.True(bottomRight.Block.BlockSize.GetHeight() <= 8);
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
            Configuration.Default, template.Sequence.SequenceHeader, template.Parent.FrameHeader, size, size, 1 << template.Sequence.SequenceHeader.SuperblockSizeLog2, disallow4x4AllFrames: false);

        using Av1EncoderCoefficientBuffer coefficients = new(Configuration.Default, template.Sequence.SequenceHeader, size, size);
        using Av1EncoderSuperblockWorkspace superblockWorkspace = new(Configuration.Default);
        using Av1EncoderBlockWorkspace blockWorkspace = new(Configuration.Default);
        using Av1SymbolEncoder symbolEncoder = CreateTileSymbolEncoder(picture.Picture, 8192);
        Av1TileEncoder tileWriter = new(
            symbolEncoder, source.Frame, reconstruction.Frame, picture.Picture, coefficients, superblockWorkspace, blockWorkspace);

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
    }

    [Theory]
    [InlineData((int)Av1ColorFormat.Yuv400)]
    [InlineData((int)Av1ColorFormat.Yuv420)]
    [InlineData((int)Av1ColorFormat.Yuv422)]
    [InlineData((int)Av1ColorFormat.Yuv444)]
    public void ProductionDeblockingPreservesEightBitReconstruction(int colorFormatValue)
        => VerifyProductionDeblocking<byte, HeifByteSampleConverter>(
            colorFormatValue,
            8,
            false,
            false,
            HeifEncodingSpeed.Level0,
            static (writer, source, reconstruction, picture, coefficients, superblockWorkspace, blockWorkspace) =>
                new(writer, source, reconstruction, picture, coefficients, superblockWorkspace, blockWorkspace));

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
        => VerifyProductionDeblocking<ushort, HeifUShortSampleConverter>(
            colorFormatValue,
            bitDepth,
            false,
            false,
            HeifEncodingSpeed.Level0,
            static (writer, source, reconstruction, picture, coefficients, superblockWorkspace, blockWorkspace) =>
                new(writer, source, reconstruction, picture, coefficients, superblockWorkspace, blockWorkspace));

    [Theory]
    [InlineData((int)Av1ColorFormat.Yuv400, HeifEncodingSpeed.Level0)]
    [InlineData((int)Av1ColorFormat.Yuv420, HeifEncodingSpeed.Level6)]
    [InlineData((int)Av1ColorFormat.Yuv422, HeifEncodingSpeed.Level9)]
    [InlineData((int)Av1ColorFormat.Yuv444, HeifEncodingSpeed.Level0)]
    public void CdefPreservesEightBitReconstruction(int colorFormatValue, HeifEncodingSpeed speed)
        => VerifyProductionDeblocking<byte, HeifByteSampleConverter>(
            colorFormatValue,
            8,
            true,
            false,
            speed,
            static (writer, source, reconstruction, picture, coefficients, superblockWorkspace, blockWorkspace) =>
                new(writer, source, reconstruction, picture, coefficients, superblockWorkspace, blockWorkspace));

    [Theory]
    [InlineData((int)Av1ColorFormat.Yuv400, 10, HeifEncodingSpeed.Level0)]
    [InlineData((int)Av1ColorFormat.Yuv400, 12, HeifEncodingSpeed.Level0)]
    [InlineData((int)Av1ColorFormat.Yuv420, 10, HeifEncodingSpeed.Level6)]
    [InlineData((int)Av1ColorFormat.Yuv420, 12, HeifEncodingSpeed.Level6)]
    [InlineData((int)Av1ColorFormat.Yuv422, 10, HeifEncodingSpeed.Level9)]
    [InlineData((int)Av1ColorFormat.Yuv422, 12, HeifEncodingSpeed.Level9)]
    [InlineData((int)Av1ColorFormat.Yuv444, 10, HeifEncodingSpeed.Level0)]
    [InlineData((int)Av1ColorFormat.Yuv444, 12, HeifEncodingSpeed.Level0)]
    public void CdefPreservesHighBitDepthReconstruction(int colorFormatValue, int bitDepth, HeifEncodingSpeed speed)
        => VerifyProductionDeblocking<ushort, HeifUShortSampleConverter>(
            colorFormatValue,
            bitDepth,
            true,
            false,
            speed,
            static (writer, source, reconstruction, picture, coefficients, superblockWorkspace, blockWorkspace) =>
                new(writer, source, reconstruction, picture, coefficients, superblockWorkspace, blockWorkspace));

    [Theory]
    [InlineData((int)Av1ColorFormat.Yuv400)]
    [InlineData((int)Av1ColorFormat.Yuv420)]
    [InlineData((int)Av1ColorFormat.Yuv422)]
    [InlineData((int)Av1ColorFormat.Yuv444)]
    public void RestorationPreservesEightBitReconstruction(int colorFormatValue)
        => VerifyProductionDeblocking<byte, HeifByteSampleConverter>(
            colorFormatValue,
            8,
            true,
            true,
            HeifEncodingSpeed.Level0,
            static (writer, source, reconstruction, picture, coefficients, superblockWorkspace, blockWorkspace) =>
                new(writer, source, reconstruction, picture, coefficients, superblockWorkspace, blockWorkspace));

    [Theory]
    [InlineData((int)Av1ColorFormat.Yuv400, 10)]
    [InlineData((int)Av1ColorFormat.Yuv400, 12)]
    [InlineData((int)Av1ColorFormat.Yuv420, 10)]
    [InlineData((int)Av1ColorFormat.Yuv420, 12)]
    [InlineData((int)Av1ColorFormat.Yuv422, 10)]
    [InlineData((int)Av1ColorFormat.Yuv422, 12)]
    [InlineData((int)Av1ColorFormat.Yuv444, 10)]
    [InlineData((int)Av1ColorFormat.Yuv444, 12)]
    public void RestorationPreservesHighBitDepthReconstruction(int colorFormatValue, int bitDepth)
        => VerifyProductionDeblocking<ushort, HeifUShortSampleConverter>(
            colorFormatValue,
            bitDepth,
            true,
            true,
            HeifEncodingSpeed.Level0,
            static (writer, source, reconstruction, picture, coefficients, superblockWorkspace, blockWorkspace) =>
                new(writer, source, reconstruction, picture, coefficients, superblockWorkspace, blockWorkspace));

    /// <summary>
    /// Gets the retained block syntax at a mode-information position.
    /// </summary>
    /// <remarks>
    /// The encoder decides the partition, so the index of a block in coding order is not fixed.
    /// A test finds its target block by position, as the packing pass does.
    /// </remarks>
    /// <param name="picture">The encoded picture.</param>
    /// <param name="modeInfoPosition">The position in 4x4 units.</param>
    /// <returns>The block syntax retained for the block that covers the position.</returns>
    private static Av1EncoderBlockStruct GetBlockEncoding(Av1PictureControlSet picture, Point modeInfoPosition)
        => picture.BlockEncodings.Span[
            picture.ModeInfoGrid.Span[(modeInfoPosition.Y * picture.ModeInfoStride) + modeInfoPosition.X]];

    /// <summary>
    /// Fills the luma plane of a 16x16 tool-activation frame whose bottom-right 8x8 quadrant is the target block.
    /// </summary>
    /// <remarks>
    /// The top-left quadrant is a two-color pattern that a palette codes cheaply and a transform does not, so no
    /// block that merges it with a neighbor competes with separate 8x8 blocks. The top-right and bottom-left
    /// quadrants are exact filter predictions from its reconstruction: cheap as separate blocks, and textured edges
    /// for the target. Their modes differ from each other and from the target's mode, so no larger block predicts
    /// two quadrants with one mode, and they spread texture along both axes, which the vertical and horizontal
    /// filters would not. The target is the exact prediction of <paramref name="targetMode"/> from those edges.
    /// Without a reconstruction, the three predicted quadrants hold texture that the first encode reconstructs.
    /// </remarks>
    /// <typeparam name="TSample">The component sample type.</typeparam>
    /// <param name="plane">The 16x16 luma plane to fill.</param>
    /// <param name="reconstruction">The 16x16 luma reconstruction of the preceding encode, or empty for the first encode.</param>
    /// <param name="targetMode">The filter mode that predicts the target quadrant.</param>
    /// <param name="bitDepth">The component precision.</param>
    /// <param name="predictFilter">The typed production filter predictor.</param>
    /// <param name="scratch">The predictor workspace.</param>
    private static void FillToolActivationLuma<TSample>(
        Buffer2DRegion<TSample> plane,
        ReadOnlySpan<TSample> reconstruction,
        Av1FilterIntraMode targetMode,
        int bitDepth,
        FilterPrediction<TSample> predictFilter,
        Span<TSample> scratch)
        where TSample : unmanaged, IBinaryInteger<TSample>
    {
        const int Size = 8;
        int width = plane.Width;
        int sampleScale = 1 << (bitDepth - 8);
        for (int y = 0; y < plane.Height; y++)
        {
            Span<TSample> row = plane.DangerousGetRowSpan(y);
            for (int x = 0; x < row.Length; x++)
            {
                int value = x < Size && y < Size
                    ? UsesSecondColor(x, y) ? 208 : 48
                    : 64 + (((x * 71) + (y * 109) + (((x ^ y) & 3) * 37)) & 127);
                row[x] = TSample.CreateChecked(value * sampleScale);
            }
        }

        if (reconstruction.IsEmpty)
        {
            return;
        }

        Av1FilterIntraMode topRightMode = targetMode == Av1FilterIntraMode.DC
            ? Av1FilterIntraMode.Directional157
            : Av1FilterIntraMode.DC;
        Av1FilterIntraMode bottomLeftMode = targetMode == Av1FilterIntraMode.Paeth
            ? Av1FilterIntraMode.Directional157
            : Av1FilterIntraMode.Paeth;

        // The predictor consumes the corner through the element immediately before the top-edge span.
        Span<TSample> aboveStorage = stackalloc TSample[Size + 1];
        Span<TSample> above = aboveStorage[1..];
        Span<TSample> left = stackalloc TSample[Size];
        Span<TSample> prediction = stackalloc TSample[Size * Size];

        // The top-right quadrant has no row above it: the top edge and the corner repeat the first left sample.
        for (int row = 0; row < Size; row++)
        {
            left[row] = reconstruction[(row * width) + Size - 1];
        }

        aboveStorage.Fill(left[0]);
        predictFilter(topRightMode, prediction, Size, above, left, Size, Size, bitDepth, scratch);
        CopyQuadrant(prediction, plane, Size, 0);

        // The bottom-left quadrant has no column left of it: the left edge and the corner repeat the first top sample.
        reconstruction.Slice((Size - 1) * width, Size).CopyTo(above);
        aboveStorage[0] = above[0];
        left.Fill(above[0]);
        predictFilter(bottomLeftMode, prediction, Size, above, left, Size, Size, bitDepth, scratch);
        CopyQuadrant(prediction, plane, 0, Size);

        // The target has both edges and its corner.
        reconstruction.Slice(((Size - 1) * width) + Size - 1, Size + 1).CopyTo(aboveStorage);
        for (int row = 0; row < Size; row++)
        {
            left[row] = reconstruction[((Size + row) * width) + Size - 1];
        }

        predictFilter(targetMode, prediction, Size, above, left, Size, Size, bitDepth, scratch);
        CopyQuadrant(prediction, plane, Size, Size);

        static void CopyQuadrant(ReadOnlySpan<TSample> quadrant, Buffer2DRegion<TSample> plane, int x, int y)
        {
            for (int row = 0; row < Size; row++)
            {
                quadrant.Slice(row * Size, Size).CopyTo(plane.DangerousGetRowSpan(y + row).Slice(x, Size));
            }
        }
    }

    /// <summary>
    /// Selects the color of a sample in a two-color pattern that no intra predictor or transform codes cheaply.
    /// </summary>
    /// <param name="x">The sample column.</param>
    /// <param name="y">The sample row.</param>
    /// <returns><see langword="true"/> for the second color; otherwise, <see langword="false"/>.</returns>
    private static bool UsesSecondColor(int x, int y) => ((((x * 73) + (y * 151) + (x * y * 29)) >> 3) & 1) != 0;

    /// <summary>
    /// Compares the luma samples that the predicted quadrants of a tool-activation frame consume: the right column
    /// and bottom row of the top-left quadrant, and the top and left edges of the target with their corner.
    /// </summary>
    /// <typeparam name="TSample">The component sample type.</typeparam>
    /// <param name="reconstruction">The 16x16 luma reconstruction of an encode.</param>
    /// <param name="reference">The 16x16 luma reconstruction that produced the source of that encode.</param>
    /// <returns><see langword="true"/> when every reference sample is equal; otherwise, <see langword="false"/>.</returns>
    private static bool LumaReferencesEqual<TSample>(ReadOnlySpan<TSample> reconstruction, ReadOnlySpan<TSample> reference)
        where TSample : unmanaged, IBinaryInteger<TSample>
    {
        const int Size = 8;
        const int Width = 2 * Size;
        bool equal = reconstruction.Slice((Size - 1) * Width, Width).SequenceEqual(reference.Slice((Size - 1) * Width, Width));
        for (int y = 0; y < Width; y++)
        {
            equal &= reconstruction[(y * Width) + Size - 1] == reference[(y * Width) + Size - 1];
        }

        return equal;
    }

    /// <summary>
    /// Describes every coded block of a picture for the failure message of a tool-selection fixture.
    /// </summary>
    /// <param name="picture">The encoded picture. It must permit 4x4 blocks.</param>
    /// <returns>The position, size, modes, transform size, and skip state of each block in raster order.</returns>
    private static string DescribeBlocks(Av1PictureControlSet picture)
    {
        StringBuilder description = new();
        Av1EncoderCommon common = picture.Parent.Common;
        ReadOnlySpan<int> grid = picture.ModeInfoGrid.Span;
        for (int row = 0; row < common.ModeInfoRowCount; row++)
        {
            for (int column = 0; column < common.ModeInfoColumnCount; column++)
            {
                // Every position of a block maps to the allocation entry of the block origin.
                int offset = (row * picture.ModeInfoStride) + column;
                if (grid[offset] != offset)
                {
                    continue;
                }

                ref Av1MacroBlockModeInfo info = ref picture.ModeInfoAllocation.Span[offset];
                Av1FilterIntraMode filterMode = picture.BlockEncodings.Span[offset].FilterIntraMode;
                description.Append(CultureInfo.InvariantCulture, $"({column},{row}) {info.Block.BlockSize} {info.Block.Mode}/{info.Block.UvMode} ");
                description.Append(CultureInfo.InvariantCulture, $"filter={filterMode} transform={info.Block.TransformSize} skip={info.Block.Skip}; ");
            }
        }

        return description.ToString();
    }

    /// <summary>
    /// Gets the index of a block's first transform state in the coding-order storage of its superblock.
    /// </summary>
    /// <remarks>
    /// Transform states are packed in coding order with one entry for each 4x4 coefficient unit, so the index of a
    /// block is the plane area coded before it. The encoder decides the partition, so that area is not fixed.
    /// This helper replays the selected partition tree in the order of the packing pass. Every block must lie
    /// inside the frame, where the coded area of a block equals its plane area.
    /// </remarks>
    /// <param name="picture">The encoded picture.</param>
    /// <param name="superblockWorkspace">The workspace that retains the preorder partitions of the only superblock.</param>
    /// <param name="modeInfoPosition">A position in 4x4 units inside the target block.</param>
    /// <param name="plane">The component plane whose state storage is indexed.</param>
    /// <returns>The index of the first transform state of the block that covers the position.</returns>
    private static int GetTransformStateIndex(
        Av1PictureControlSet picture,
        Av1EncoderSuperblockWorkspace superblockWorkspace,
        Point modeInfoPosition,
        Av1Plane plane)
    {
        int partitionIndex = 0;
        int codedArea = 0;
        bool found = TryGetCodedAreaBefore(
            picture,
            superblockWorkspace.PartitionTypes,
            ref partitionIndex,
            picture.Sequence.SequenceHeader.SuperblockSize,
            default,
            modeInfoPosition,
            plane,
            ref codedArea);

        Assert.True(found, $"No coded block covers mode-information position {modeInfoPosition}.");
        return codedArea / Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount;
    }

    /// <summary>
    /// Accumulates the plane area of the blocks that one partition node codes before the target block.
    /// </summary>
    /// <param name="picture">The encoded picture.</param>
    /// <param name="partitionTypes">The selected partitions of the superblock in preorder.</param>
    /// <param name="partitionIndex">The preorder index of this node, advanced past every visited node.</param>
    /// <param name="blockSize">The square size of this node.</param>
    /// <param name="origin">The node origin in 4x4 units.</param>
    /// <param name="target">A position in 4x4 units inside the target block.</param>
    /// <param name="plane">The component plane whose area is accumulated.</param>
    /// <param name="codedArea">The plane area, in samples, coded before the target block.</param>
    /// <returns><see langword="true"/> when the node contains the target block; otherwise, <see langword="false"/>.</returns>
    private static bool TryGetCodedAreaBefore(
        Av1PictureControlSet picture,
        ReadOnlySpan<byte> partitionTypes,
        ref int partitionIndex,
        Av1BlockSize blockSize,
        Point origin,
        Point target,
        Av1Plane plane,
        ref int codedArea)
    {
        Av1EncoderCommon common = picture.Parent.Common;
        if (origin.Y >= common.ModeInfoRowCount || origin.X >= common.ModeInfoColumnCount)
        {
            // The packing pass does not visit a node that starts outside the frame, so it has no partition entry.
            return false;
        }

        Av1PartitionType partition = (Av1PartitionType)partitionTypes[partitionIndex++];
        int half = blockSize.Get4x4WideCount() >> 1;
        int quarter = half >> 1;
        if (partition == Av1PartitionType.Split && blockSize > Av1BlockSize.Block8x8)
        {
            Av1BlockSize subSize = partition.GetBlockSubSize(blockSize);
            for (int child = 0; child < 4; child++)
            {
                Point childOrigin = origin + new Size((child & 1) * half, (child >> 1) * half);
                if (TryGetCodedAreaBefore(picture, partitionTypes, ref partitionIndex, subSize, childOrigin, target, plane, ref codedArea))
                {
                    return true;
                }
            }

            return false;
        }

        // Every other partition ends in final blocks. Their offsets follow the coding order of the partition;
        // a split 8x8 node ends in four 4x4 blocks because AV1 has no partition symbol below that size.
        Size[] offsets = partition switch
        {
            Av1PartitionType.None => [new(0, 0)],
            Av1PartitionType.Horizontal => [new(0, 0), new(0, half)],
            Av1PartitionType.Vertical => [new(0, 0), new(half, 0)],
            Av1PartitionType.Split => [new(0, 0), new(half, 0), new(0, half), new(half, half)],
            Av1PartitionType.HorizontalA => [new(0, 0), new(half, 0), new(0, half)],
            Av1PartitionType.HorizontalB => [new(0, 0), new(0, half), new(half, half)],
            Av1PartitionType.VerticalA => [new(0, 0), new(0, half), new(half, 0)],
            Av1PartitionType.VerticalB => [new(0, 0), new(half, 0), new(half, half)],
            Av1PartitionType.Horizontal4 => [new(0, 0), new(0, quarter), new(0, 2 * quarter), new(0, 3 * quarter)],
            _ => [new(0, 0), new(quarter, 0), new(2 * quarter, 0), new(3 * quarter, 0)]
        };

        ObuColorConfig colorConfig = picture.Sequence.SequenceHeader.ColorConfig;
        foreach (Size offset in offsets)
        {
            Point position = origin + offset;
            if (position.Y >= common.ModeInfoRowCount || position.X >= common.ModeInfoColumnCount)
            {
                continue;
            }

            Av1BlockSize finalSize = picture.GetFromModeInfoGrid(position).Block.BlockSize;
            int right = position.X + finalSize.Get4x4WideCount();
            int bottom = position.Y + finalSize.Get4x4HighCount();
            if (target.X >= position.X && target.X < right && target.Y >= position.Y && target.Y < bottom)
            {
                return true;
            }

            Assert.True(right <= common.ModeInfoColumnCount && bottom <= common.ModeInfoRowCount);
            if (plane == Av1Plane.Y)
            {
                codedArea += finalSize.GetWidth() * finalSize.GetHeight();
            }
            else if (GetBlockEncoding(picture, position).HasChroma)
            {
                // Luma blocks smaller than the chroma sampling grid share one chroma block, owned by the last of them.
                Av1BlockSize chromaSize = finalSize.GetSubsampled(colorConfig.SubSamplingX, colorConfig.SubSamplingY);
                codedArea += chromaSize.GetWidth() * chromaSize.GetHeight();
            }
        }

        return false;
    }

    /// <summary>
    /// Verifies that decoding the emitted stream reproduces the retained, filtered reconstruction exactly.
    /// </summary>
    /// <typeparam name="TSample">The component sample type.</typeparam>
    /// <typeparam name="TConverter">The source pixel conversion operator.</typeparam>
    /// <param name="colorFormatValue">The component layout.</param>
    /// <param name="bitDepth">The component precision.</param>
    /// <param name="enableCdef">Whether directional enhancement follows deblocking.</param>
    /// <param name="enableRestoration">Whether restoration follows directional enhancement.</param>
    /// <param name="speed">The directional-enhancement selection policy.</param>
    /// <param name="createWriter">The typed production tile constructor.</param>
    private static void VerifyProductionDeblocking<TSample, TConverter>(
        int colorFormatValue,
        int bitDepth,
        bool enableCdef,
        bool enableRestoration,
        HeifEncodingSpeed speed,
        TileWriterFactory<TSample> createWriter)
        where TSample : unmanaged, IBinaryInteger<TSample>
        where TConverter : struct, IHeifSampleConverter<TSample>
    {
        // Odd dimensions and a height above 128 exercise chroma ownership, coded padding, and intersecting row bands.
        int width = enableRestoration ? 385 : enableCdef ? 129 : 33;
        const int Height = 137;
        const int QIndex = 128;
        Av1ColorFormat colorFormat = (Av1ColorFormat)colorFormatValue;
        ObuColorConfig colorConfig = new()
        {
            IsMonochrome = colorFormat == Av1ColorFormat.Yuv400,
            SubSamplingX = colorFormat is Av1ColorFormat.Yuv400 or Av1ColorFormat.Yuv420 or Av1ColorFormat.Yuv422,
            SubSamplingY = colorFormat is Av1ColorFormat.Yuv400 or Av1ColorFormat.Yuv420,
            BitDepth = bitDepth == 8 ? Av1BitDepth.EightBit : bitDepth == 10 ? Av1BitDepth.TenBit : Av1BitDepth.TwelveBit
        };

        using Av1EncoderFrameBuffer<TSample> source = new(Configuration.Default, width, Height, bitDepth, colorFormat, 1, 1, lumaBorder: 64);
        using Av1EncoderFrameBuffer<TSample> reconstruction = new(Configuration.Default, width, Height, bitDepth, colorFormat, 1, 1, lumaBorder: 64);
        int planeCount = colorConfig.PlaneCount;
        TSample[][] unfiltered = new TSample[planeCount][];
        for (int planeIndex = 0; planeIndex < planeCount; planeIndex++)
        {
            Buffer2DRegion<TSample> plane = source.Frame.View.GetPlane((Av1Plane)planeIndex);
            unfiltered[planeIndex] = new TSample[plane.Width * plane.Height];
        }

        // The encoder chooses its own deblocking levels, so the content must be detailed enough, at this
        // quantizer, for filtering to reduce the reconstruction error.
        colorConfig.MatrixCoefficients = ObuMatrixCoefficients.Bt709;
        using (Image<Rgba32> image = TestFile.Create(TestImages.Png.Bike).CreateRgba32Image())
        {
            HeifColorConversionParameters parameters = Av1YuvConverter.GetConversionParameters(colorConfig, colorConfig.ColorRange, out HeifColorConversionMode mode);
            HeifPlanarColorConverter.ConvertFromRgb<Rgba32, Av1EncoderFrame<TSample>.PlanarView, TSample, TConverter>(
                Configuration.Default,
                image.Frames.RootFrame,
                new Rectangle(0, 0, width, Height),
                source.Frame.View,
                in parameters,
                mode);
        }

        source.Frame.ExtendBorders();
        using Av1EncoderModeInfoBuffer modeInfo = new(Configuration.Default, width, Height, disallow4x4AllFrames: false);
        Av1PictureControlSet template = CreatePicture(modeInfo, colorConfig, use128x128Superblock: false, QIndex);
        ObuFrameHeader header = template.Parent.FrameHeader;
        header.FrameSize.FrameWidth = width;
        header.FrameSize.FrameHeight = Height;
        header.FrameSize.SuperResolutionUpscaledWidth = width;
        header.FrameSize.SuperResolutionDenominator = Av1Constants.ScaleNumerator;
        template.Sequence.SequenceHeader.EnableRestoration = enableRestoration;
        using Av1EncoderPictureBuffer picture = new(
            Configuration.Default, template.Sequence.SequenceHeader, header, width, Height, 1 << template.Sequence.SequenceHeader.SuperblockSizeLog2, disallow4x4AllFrames: false);

        using Av1EncoderCoefficientBuffer coefficients = new(Configuration.Default, template.Sequence.SequenceHeader, width, Height);
        using Av1EncoderSuperblockWorkspace superblockWorkspace = new(Configuration.Default);
        using Av1EncoderBlockWorkspace blockWorkspace = new(Configuration.Default);
        using Av1SymbolEncoder symbolEncoder = CreateTileSymbolEncoder(picture.Picture, 8192);

        // Reserve the final pass's restoration decisions, but measure the baseline before any in-loop filtering.
        template.Sequence.SequenceHeader.EnableRestoration = false;
        _ = createWriter(symbolEncoder, source.Frame, reconstruction.Frame, picture.Picture, coefficients, superblockWorkspace, blockWorkspace);
        for (int planeIndex = 0; planeIndex < planeCount; planeIndex++)
        {
            Buffer2DRegion<TSample> plane = reconstruction.Frame.View.GetPlane((Av1Plane)planeIndex);
            for (int y = 0; y < plane.Height; y++)
            {
                plane.DangerousGetRowSpan(y).CopyTo(unfiltered[planeIndex].AsSpan(y * plane.Width, plane.Width));
            }
        }

        // A nonzero sharpness exercises the edge limits of both the encoder filter and the decoder.
        header.LoopFilterParameters.SharpnessLevel = 3;
        template.Sequence.SequenceHeader.EnableCdef = enableCdef;
        template.Sequence.SequenceHeader.EnableRestoration = enableRestoration;
        template.Sequence.SequenceHeader.IsStillPicture = true;
        picture.Picture.Parent.EncodingSpeed = speed;
        picture.Reset(header);
        Av1TileEncoder tileWriter = createWriter(
            symbolEncoder, source.Frame, reconstruction.Frame, picture.Picture, coefficients, superblockWorkspace, blockWorkspace);

        // The level search must have chosen to deblock for the comparison to cover the filter.
        Assert.True(header.LoopFilterParameters.FilterLevel[0] != 0 || header.LoopFilterParameters.FilterLevel[1] != 0);
        if (enableCdef)
        {
            bool hasStrength = false;
            for (int index = 0; index < 1 << header.CdefParameters.BitCount; index++)
            {
                hasStrength |= header.CdefParameters.YStrength[index] != 0 ||
                    (planeCount > 1 && header.CdefParameters.UvStrength[index] != 0);
            }

            Assert.True(hasStrength);
        }

        if (enableRestoration)
        {
            Assert.True(header.LoopRestorationParameters.UsesLoopRestoration);
        }

        byte[] payload = WriteCompleteTileObu(picture.Picture, tileWriter, width, Height);
        using Av1Decoder decoder = new(Configuration.Default);
        using Av1FrameBuffer<byte> decodedFrame = decoder.DecodeFrameBuffer(payload, null, null, out _);
        int changedSamples = 0;
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
                if (!row.SequenceEqual(decodedRow))
                {
                    int column = 0;
                    while (row[column] == decodedRow[column])
                    {
                        column++;
                    }

                    Assert.Fail($"plane {planeIndex} row {y} column {column} retained {row[column]} decoded {decodedRow[column]}");
                }

                for (int x = 0; x < row.Length; x++)
                {
                    changedSamples += row[x] != unfiltered[planeIndex][(y * retained.Width) + x] ? 1 : 0;
                }
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

            // Two flat halves are cheap for any partition of spatial predictors once residual patterns keep the
            // transforms busy, so the split-transform variant scatters the two colors: a transform codes that
            // texture at a far higher rate than a palette map, in every partition.
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

                int baseColor = int.CreateChecked(UsesSecondColor(column, visibleRow) ? upperColor : lowerColor);
                sourceRow[column] = TSample.CreateChecked(
                    baseColor + (residual * 4 * (1 << (bitDepthValue - 8))));
            }
        }

        ClearPlane(reconstruction.Luma);
        using Av1EncoderModeInfoBuffer modeInfo = new(
            Configuration.Default,
            width,
            height,
            disallow4x4AllFrames: false);

        Av1PictureControlSet pictureTemplate = CreatePicture(
            modeInfo,
            colorConfig,
            use128x128Superblock: false,
            QIndex);

        // Still images select the all-intra search settings, as libavif does.
        pictureTemplate.Sequence.SequenceHeader.IsStillPicture = true;
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
            1 << pictureTemplate.Sequence.SequenceHeader.SuperblockSizeLog2,
            disallow4x4AllFrames: false);

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
        using Av1FrameBuffer<byte> decodedPlanes = decoder.DecodeFrameBuffer(payload, null, null, out _);
        using Image<Rgba32> decoded = new(Configuration.Default, decodedPlanes.Width, decodedPlanes.Height);
        Av1YuvConverter.ConvertToRgb(
            Configuration.Default,
            decodedPlanes,
            decoded.Bounds,
            decoded.Frames.RootFrame.PixelBuffer.GetRegion(decoded.Bounds),
            decoded.Size,
            default,
            null,
            null,
            default,
            default,
            false,
            HeifChromaUpsampling.Auto,
            decodedPlanes.ColorConfig.ColorRange);

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

        Assert.NotEqual(0, tileWriter.GetTileData(0).Length);
    }

    /// <summary>
    /// Fills a chroma plane of two transforms in each direction whose bottom-right transform is the prediction of
    /// one chroma mode from its edges plus a checkerboard.
    /// </summary>
    /// <param name="plane">The chroma plane to fill.</param>
    /// <param name="transformSize">The chroma transform of an 8x8 luma block, which is also the quadrant size.</param>
    /// <param name="expectedMode">The chroma mode whose prediction becomes the target.</param>
    /// <param name="expectedAngleDelta">The angle delta of a directional mode.</param>
    /// <param name="reconstruction">
    /// The reconstruction of the plane from the preceding encode in row-major order, from which the target is
    /// predicted, or empty to predict from the source values of the neighboring quadrants.
    /// </param>
    private static void FillChromaModeSelectionPlane(
        Buffer2DRegion<byte> plane,
        Av1TransformSize transformSize,
        Av1ChromaPredictionMode expectedMode,
        int expectedAngleDelta,
        ReadOnlySpan<byte> reconstruction)
    {
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();
        Span<byte> aboveStorage = stackalloc byte[17];
        Span<byte> above = aboveStorage.Slice(1, width * 2);
        Span<byte> leftStorage = stackalloc byte[17];
        Span<byte> left = leftStorage.Slice(1, height * 2);

        // The corner quadrant is flat, the top quadrant has vertical stripes, and the left quadrant has horizontal
        // stripes; both follow one period of a sine that starts at the corner value. A textured edge makes a small
        // change of angle change the prediction, which separates the angle deltas, while the continuous corner keeps
        // the zero delta of the expected mode close to the target: the angle search evaluates that delta first and
        // abandons the mode when it is not close to the best earlier candidate. No planar predictor reproduces the
        // curves. The target is predicted from the reconstruction of the three quadrants, which the first encode
        // approximates by their source values.
        aboveStorage[0] = 128;
        leftStorage[0] = 128;
        for (int column = 0; column < width; column++)
        {
            above[column] = (byte)(128 + (int)Math.Round(64 * Math.Sin(Math.PI * column / 4)));
        }

        for (int row = 0; row < height; row++)
        {
            left[row] = (byte)(128 - (int)Math.Round(64 * Math.Sin(Math.PI * row / 4)));
        }

        for (int row = 0; row < plane.Height; row++)
        {
            Span<byte> destination = plane.DangerousGetRowSpan(row);
            for (int column = 0; column < plane.Width; column++)
            {
                destination[column] = row < height
                    ? column < width ? (byte)128 : above[column - width]
                    : column < width ? left[row - height] : (byte)0;
            }
        }

        if (!reconstruction.IsEmpty)
        {
            int stride = plane.Width;
            reconstruction.Slice(((height - 1) * stride) + width - 1, width + 1).CopyTo(aboveStorage);
            for (int row = 0; row < height; row++)
            {
                left[row] = reconstruction[((height + row) * stride) + width - 1];
            }

            leftStorage[0] = aboveStorage[0];
        }

        // The target lies at the bottom-right of the frame, so its extended edges repeat their last sample.
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
            int corner = aboveStorage[0];
            for (int row = 0; row < height; row++)
            {
                for (int column = 0; column < width; column++)
                {
                    int top = above[column];
                    int leftSample = left[row];
                    int predictor = top + leftSample - corner;
                    int leftDistance = Math.Abs(predictor - leftSample);
                    int topDistance = Math.Abs(predictor - top);
                    int cornerDistance = Math.Abs(predictor - corner);
                    target[(row * width) + column] = expectedMode switch
                    {
                        Av1ChromaPredictionMode.Vertical => (byte)top,
                        Av1ChromaPredictionMode.Horizontal => (byte)leftSample,
                        _ => (byte)(leftDistance <= topDistance && leftDistance <= cornerDistance
                            ? leftSample
                            : topDistance <= cornerDistance ? top : corner)
                    };
                }
            }
        }

        // The checkerboard offset keeps coefficients nonzero so the implicit transform affects the stream.
        for (int row = 0; row < height; row++)
        {
            Span<byte> destination = plane.DangerousGetRowSpan(height + row);
            for (int column = 0; column < width; column++)
            {
                destination[width + column] = (byte)Math.Clamp(
                    target[(row * width) + column] + (((row + column) & 1) == 0 ? 5 : -5),
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
