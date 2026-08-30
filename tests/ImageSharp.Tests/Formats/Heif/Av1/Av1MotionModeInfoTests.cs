// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;
using SixLabors.ImageSharp.Formats.Heif.Av1.ReferenceFrames;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies inter-intra, motion-mode, and interpolation-filter syntax ordering in inter-frame mode parsing.
/// </summary>
[Trait("Format", "Avif")]
public class Av1MotionModeInfoTests
{
    /// <summary>
    /// Verifies that an extended 8x32 rectangle omits inter-intra syntax and reads the following interpolation filter.
    /// </summary>
    [Fact]
    public void ReadInterFrameModeInfoOmitsInterIntraFlagForExtendedRectangle()
    {
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader();
        sequenceHeader.EnableInterIntraCompound = true;
        ObuFrameHeader frameHeader = CreateFrameHeader();
        ConfigureForcedTranslationalGlobalMotion(frameHeader);

        using Av1TileReader tileReader = new(Configuration.Default, sequenceHeader, frameHeader);
        Av1BlockModeInfo modeInfo = new(Av1BlockSize.Block8x32, Point.Empty);
        Av1SuperblockInfo superblockInfo = new(tileReader.FrameInfo, Point.Empty);
        Av1PartitionInfo partitionInfo = new(modeInfo, superblockInfo, false, Av1PartitionType.None);

        using Av1SymbolWriter writer = new(Configuration.Default, 2, updateCdf: true);
        writer.WriteSymbol(false, Av1DefaultDistributions.Skip[0]);

        // With no matching above or left filter, a single-reference vertical filter uses context three. Writing the
        // filter immediately after Skip makes any accidental extended-rectangle inter-intra read desynchronize it.
        writer.WriteSymbol((int)Av1InterpolationFilter.Sharp, Av1DefaultDistributions.SwitchableInterpolation[3]);

        using IMemoryOwner<byte> encoded = writer.Exit();
        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.Memory.Span, 0, updateCdf: true);

        tileReader.ReadInterFrameModeInfo(ref decoder, ref partitionInfo, new Av1TileInfo(0, 0, frameHeader));
        modeInfo = partitionInfo.ModeInfo;

        Assert.Equal(Av1MotionMode.SimpleTranslation, modeInfo.MotionMode);
        Assert.Equal(Av1InterpolationFilter.Sharp, modeInfo.InterpolationFilters[0]);
        Assert.Equal(Av1InterpolationFilter.Sharp, modeInfo.InterpolationFilters[1]);
    }

    /// <summary>
    /// Verifies that a false inter-intra flag continues through omitted, binary, and ternary motion-mode syntax into interpolation.
    /// </summary>
    /// <param name="isMotionModeSwitchable">Whether the frame enables per-block motion-mode syntax.</param>
    /// <param name="allowWarpedMotion">Whether the eligible block uses the ternary rather than binary motion-mode distribution.</param>
    /// <param name="selectedMotionModeValue">The motion mode written when syntax is present.</param>
    [Theory]
    [InlineData(false, false, (int)Av1MotionMode.SimpleTranslation)]
    [InlineData(true, false, (int)Av1MotionMode.SimpleTranslation)]
    [InlineData(true, true, (int)Av1MotionMode.SimpleTranslation)]
    [InlineData(true, false, (int)Av1MotionMode.Obmc)]
    [InlineData(true, true, (int)Av1MotionMode.Obmc)]
    public void ReadInterFrameModeInfoContinuesFromFalseInterIntraThroughMotionModeIntoInterpolation(
        bool isMotionModeSwitchable,
        bool allowWarpedMotion,
        int selectedMotionModeValue)
    {
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader();
        sequenceHeader.EnableInterIntraCompound = true;
        ObuFrameHeader frameHeader = CreateFrameHeader();
        frameHeader.IsMotionModeSwitchable = isMotionModeSwitchable;
        frameHeader.AllowWarpedMotion = allowWarpedMotion;
        ConfigureForcedTranslationalGlobalMotion(frameHeader);

        using Av1ReferenceFrameStore referenceFrames = new();
        using Av1FrameInfo retainedFrameInfo = new(sequenceHeader);
        Av1ReferenceFrame retainedFrame = new(
            new Av1FrameBuffer<byte>(Configuration.Default, sequenceHeader, Av1ColorFormat.Yuv400, false),
            CreateFrameHeader(),
            retainedFrameInfo);

        Assert.True(referenceFrames.Commit(1, retainedFrame, showFrame: false));

        Av1FrameEntropyContexts entropyContexts = new(0);
        using Av1TileReader tileReader = new(
            Configuration.Default,
            sequenceHeader,
            frameHeader,
            entropyContexts,
            null,
            referenceFrames);

        Av1SuperblockInfo superblockInfo = tileReader.FrameInfo.GetSuperblock(Point.Empty);
        Av1BlockModeInfo aboveModeInfo = new(Av1BlockSize.Block8x8, Point.Empty)
        {
            YMode = Av1PredictionMode.NearestMotionVector,
        };

        aboveModeInfo.ReferenceFrames[0] = Av1ReferenceFrameType.Last;
        aboveModeInfo.ReferenceFrames[1] = Av1ReferenceFrameType.None;
        aboveModeInfo.InterpolationFilters.Clear();
        tileReader.FrameInfo.UpdateModeInfo(aboveModeInfo, superblockInfo);
        superblockInfo.BlockCount++;

        Av1BlockModeInfo modeInfo = new(Av1BlockSize.Block8x8, new Point(0, 2));
        Av1PartitionInfo partitionInfo = new(modeInfo, superblockInfo, false, Av1PartitionType.None)
        {
            ColumnIndex = 0,
            RowIndex = 2,
            AvailableAbove = true,
            AboveModeInfo = aboveModeInfo,
        };

        using Av1SymbolWriter writer = new(Configuration.Default, 4, updateCdf: true);
        writer.WriteSymbol(false, Av1DefaultDistributions.Skip[0]);
        writer.WriteSymbol(false, Av1DefaultDistributions.InterIntra[Av1BlockSize.Block8x8.GetSizeGroup()]);

        if (isMotionModeSwitchable)
        {
            Av1Distribution motionModeDistribution = allowWarpedMotion
                ? Av1DefaultDistributions.MotionMode[(int)Av1BlockSize.Block8x8]
                : Av1DefaultDistributions.Obmc[(int)Av1BlockSize.Block8x8];

            writer.WriteSymbol(selectedMotionModeValue, motionModeDistribution);
        }

        // The matching regular above neighbor selects vertical context zero. Sharp is deliberately non-default so the
        // assertion proves that every preceding conditional symbol consumed exactly its own range-coded interval.
        writer.WriteSymbol((int)Av1InterpolationFilter.Sharp, Av1DefaultDistributions.SwitchableInterpolation[0]);

        using IMemoryOwner<byte> encoded = writer.Exit();
        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.Memory.Span, 0, updateCdf: true);

        tileReader.ReadInterFrameModeInfo(ref decoder, ref partitionInfo, new Av1TileInfo(0, 0, frameHeader));
        modeInfo = partitionInfo.ModeInfo;

        Assert.Equal(Av1ReferenceFrameType.Last, modeInfo.ReferenceFrames[0]);
        Assert.Equal(Av1ReferenceFrameType.None, modeInfo.ReferenceFrames[1]);
        Av1MotionMode expectedMotionMode = isMotionModeSwitchable
            ? (Av1MotionMode)selectedMotionModeValue
            : Av1MotionMode.SimpleTranslation;

        Assert.Equal(expectedMotionMode, modeInfo.MotionMode);
        Assert.Equal(Av1InterpolationFilter.Sharp, modeInfo.InterpolationFilters[0]);
        Assert.Equal(Av1InterpolationFilter.Sharp, modeInfo.InterpolationFilters[1]);
    }

    /// <summary>
    /// Verifies selected smooth and wedge inter-intra syntax before switchable interpolation.
    /// </summary>
    /// <param name="useWedge">Whether the block selects an inter-intra wedge.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReadInterFrameModeInfoReadsSelectedInterIntraBeforeInterpolation(bool useWedge)
    {
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader();
        sequenceHeader.EnableInterIntraCompound = true;
        ObuFrameHeader frameHeader = CreateFrameHeader();
        ConfigureForcedTranslationalGlobalMotion(frameHeader);

        using Av1TileReader tileReader = new(Configuration.Default, sequenceHeader, frameHeader);
        Av1BlockModeInfo modeInfo = new(Av1BlockSize.Block8x8, Point.Empty);
        Av1SuperblockInfo superblockInfo = new(tileReader.FrameInfo, Point.Empty);
        Av1PartitionInfo partitionInfo = new(modeInfo, superblockInfo, false, Av1PartitionType.None);

        using Av1SymbolWriter writer = new(Configuration.Default, 6, updateCdf: true);
        writer.WriteSymbol(false, Av1DefaultDistributions.Skip[0]);
        writer.WriteSymbol(true, Av1DefaultDistributions.InterIntra[Av1BlockSize.Block8x8.GetSizeGroup()]);
        writer.WriteSymbol(
            (int)Av1InterIntraMode.Smooth,
            Av1DefaultDistributions.InterIntraMode[Av1BlockSize.Block8x8.GetSizeGroup()]);

        writer.WriteSymbol(useWedge, Av1DefaultDistributions.WedgeInterIntra[(int)Av1BlockSize.Block8x8]);
        if (useWedge)
        {
            writer.WriteSymbol(13, Av1DefaultDistributions.WedgeIndex[(int)Av1BlockSize.Block8x8]);
        }

        writer.WriteSymbol((int)Av1InterpolationFilter.Sharp, Av1DefaultDistributions.SwitchableInterpolation[3]);

        using IMemoryOwner<byte> encoded = writer.Exit();
        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.Memory.Span, 0, updateCdf: true);

        tileReader.ReadInterFrameModeInfo(ref decoder, ref partitionInfo, new Av1TileInfo(0, 0, frameHeader));
        modeInfo = partitionInfo.ModeInfo;

        Assert.Equal(Av1ReferenceFrameType.Last, modeInfo.ReferenceFrames[0]);
        Assert.Equal(Av1ReferenceFrameType.Intra, modeInfo.ReferenceFrames[1]);
        Assert.Equal(Av1InterIntraMode.Smooth, modeInfo.InterIntraMode);
        Assert.Equal(useWedge, modeInfo.UseInterIntraWedge);
        Assert.Equal(useWedge ? 13 : 0, modeInfo.InterIntraWedgeIndex);
        Assert.Equal(Av1MotionMode.SimpleTranslation, modeInfo.MotionMode);
        Assert.Equal(Av1InterpolationFilter.Sharp, modeInfo.InterpolationFilters[0]);
        Assert.Equal(Av1InterpolationFilter.Sharp, modeInfo.InterpolationFilters[1]);
    }

    /// <summary>
    /// Creates the monochrome 64x64 sequence geometry used by direct inter-mode syntax tests.
    /// </summary>
    /// <returns>The initialized sequence header.</returns>
    private static ObuSequenceHeader CreateSequenceHeader()
        => new()
        {
            MaxFrameWidth = 64,
            MaxFrameHeight = 64,
            Use128x128Superblock = false,
            EnableDualFilter = false,
            EnableCdef = false,
            EnableFilterIntra = false,
            ColorConfig = new ObuColorConfig
            {
                IsMonochrome = true,
                BitDepth = Av1BitDepth.EightBit,
            },
        };

    /// <summary>
    /// Creates an inter-frame header whose one tile and coded dimensions cover the complete test frame.
    /// </summary>
    /// <returns>The initialized frame header.</returns>
    private static ObuFrameHeader CreateFrameHeader()
        => new()
        {
            FrameType = ObuFrameType.InterFrame,
            ModeInfoColumnCount = 16,
            ModeInfoRowCount = 16,
            CodedLossless = true,
            AllowScreenContentTools = false,
            InterpolationFilter = Av1InterpolationFilter.Switchable,
            FrameSize = new ObuFrameSize
            {
                FrameWidth = 64,
                FrameHeight = 64,
                SuperResolutionUpscaledWidth = 64,
                RenderWidth = 64,
                RenderHeight = 64,
            },
            TilesInfo = new ObuTileGroupHeader
            {
                TileColumnCount = 1,
                TileRowCount = 1,
                TileColumnStartModeInfo = [0, 16],
                TileRowStartModeInfo = [0, 16],
            },
        };

    /// <summary>
    /// Forces segment zero to a translational global-motion mode that omits reference and inter-mode symbols but still carries interpolation.
    /// </summary>
    /// <param name="frameHeader">The frame header to configure.</param>
    private static void ConfigureForcedTranslationalGlobalMotion(ObuFrameHeader frameHeader)
    {
        ObuSegmentationParameters segmentationParameters = frameHeader.SegmentationParameters;
        segmentationParameters.Enabled = true;
        segmentationParameters.FeatureEnabled[0, (int)ObuSegmentationLevelFeature.GlobalMotionVector] = true;
        frameHeader.GetGlobalMotionParameters()[0].Type = Av1GlobalMotionType.Translation;
        frameHeader.GetReferenceFrameIndices()[0] = 0;
    }
}
