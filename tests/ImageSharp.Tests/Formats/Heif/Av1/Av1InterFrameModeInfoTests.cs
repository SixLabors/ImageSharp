// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies the common inter-frame mode prefix and its intra-coded-block branch.
/// </summary>
[Trait("Format", "Avif")]
public class Av1InterFrameModeInfoTests
{
    /// <summary>
    /// Verifies that an inter frame can select an intra-coded block using the block-size luma distribution.
    /// </summary>
    [Fact]
    public void ReadInterFrameModeInfoReadsIntraCodedBlock()
    {
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader();
        ObuFrameHeader frameHeader = CreateFrameHeader();
        using Av1TileReader tileReader = new(Configuration.Default, sequenceHeader, frameHeader);
        Av1BlockModeInfo modeInfo = new(Av1BlockSize.Block8x8, Point.Empty);
        Av1SuperblockInfo superblockInfo = new(tileReader.FrameInfo, Point.Empty);
        Av1PartitionInfo partitionInfo = new(modeInfo, superblockInfo, false, Av1PartitionType.None);

        Av1Distribution skip = Av1DefaultDistributions.Skip[0];
        Av1Distribution intraInter = Av1DefaultDistributions.IntraInter[0];
        Av1Distribution yMode = Av1DefaultDistributions.FrameYMode[1];
        using Av1SymbolWriter writer = new(Configuration.Default, 1, updateCdf: true);
        writer.WriteSymbol(false, skip);
        writer.WriteSymbol(false, intraInter);
        writer.WriteSymbol((int)Av1PredictionMode.DC, yMode);
        using IMemoryOwner<byte> encoded = writer.Exit();
        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.GetSpan(), 0, updateCdf: true);

        tileReader.ReadInterFrameModeInfo(ref decoder, ref partitionInfo);

        Assert.False(modeInfo.SkipMode);
        Assert.False(modeInfo.Skip);
        Assert.Equal(Av1ReferenceFrameType.Intra, modeInfo.ReferenceFrames[0]);
        Assert.Equal(Av1ReferenceFrameType.None, modeInfo.ReferenceFrames[1]);
        Assert.Equal(Av1PredictionMode.DC, modeInfo.YMode);
        Assert.Equal(Av1ChromaPredictionMode.DC, modeInfo.UvMode);
    }

    /// <summary>
    /// Verifies that skip mode omits the residual-skip and intra-inter symbols and marks the block as inter coded.
    /// </summary>
    [Fact]
    public void ReadInterFrameModeInfoSkipModeForcesInterBlockAndResidualSkip()
    {
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader();
        ObuFrameHeader frameHeader = CreateFrameHeader();
        frameHeader.SkipModeParameters.SkipModeFlag = true;
        using Av1TileReader tileReader = new(Configuration.Default, sequenceHeader, frameHeader);
        Av1BlockModeInfo aboveModeInfo = new(Av1BlockSize.Block8x8, Point.Empty) { SkipMode = true };
        Av1BlockModeInfo modeInfo = new(Av1BlockSize.Block8x8, Point.Empty);

        Av1Distribution skipMode = Av1DefaultDistributions.SkipMode[1];
        using Av1SymbolWriter writer = new(Configuration.Default, 1, updateCdf: true);
        writer.WriteSymbol(true, skipMode);
        using IMemoryOwner<byte> encoded = writer.Exit();
        Memory<byte> encodedMemory = encoded.Memory;

        Assert.Throws<NotSupportedException>(() => ReadInterFrameModeInfo(tileReader, encodedMemory, modeInfo, aboveModeInfo));
        Assert.True(modeInfo.SkipMode);
        Assert.True(modeInfo.Skip);
    }

    /// <summary>
    /// Invokes the ref-struct mode parser for exception assertions that cannot capture its parameters directly.
    /// </summary>
    /// <param name="tileReader">The tile reader.</param>
    /// <param name="encoded">The range-coded block-prefix symbols.</param>
    /// <param name="modeInfo">The current coding block.</param>
    /// <param name="aboveModeInfo">The available above block supplying skip-mode context.</param>
    private static void ReadInterFrameModeInfo(
        Av1TileReader tileReader,
        Memory<byte> encoded,
        Av1BlockModeInfo modeInfo,
        Av1BlockModeInfo aboveModeInfo)
    {
        Av1SuperblockInfo superblockInfo = new(tileReader.FrameInfo, Point.Empty);
        Av1PartitionInfo partitionInfo = new(modeInfo, superblockInfo, false, Av1PartitionType.None)
        {
            AvailableAbove = true,
            AboveModeInfo = aboveModeInfo,
        };

        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.Span, 0, updateCdf: true);
        tileReader.ReadInterFrameModeInfo(ref decoder, ref partitionInfo);
    }

    /// <summary>
    /// Creates the monochrome 64x64 sequence geometry used by direct mode-prefix tests.
    /// </summary>
    /// <returns>The initialized sequence header.</returns>
    private static ObuSequenceHeader CreateSequenceHeader()
        => new()
        {
            MaxFrameWidth = 64,
            MaxFrameHeight = 64,
            Use128x128Superblock = false,
            EnableCdef = false,
            EnableFilterIntra = false,
            ColorConfig = new ObuColorConfig
            {
                IsMonochrome = true,
                BitDepth = Av1BitDepth.EightBit,
            },
        };

    /// <summary>
    /// Creates an inter-frame header whose optional block-prefix tools are disabled.
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
        };
}
