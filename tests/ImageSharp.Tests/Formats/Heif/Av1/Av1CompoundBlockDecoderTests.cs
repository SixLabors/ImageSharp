// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopFilter;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;
using SixLabors.ImageSharp.Formats.Heif.Av1.ReferenceFrames;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies compound prediction through the production block-reconstruction branch.
/// </summary>
[Trait("Format", "Avif")]
public class Av1CompoundBlockDecoderTests
{
    /// <summary>
    /// Verifies that two retained reference planes are predicted and averaged before residual reconstruction.
    /// </summary>
    /// <param name="bitDepthValue">The native sample depth.</param>
    [Theory]
    [InlineData((int)Av1BitDepth.EightBit)]
    [InlineData((int)Av1BitDepth.TenBit)]
    [InlineData((int)Av1BitDepth.TwelveBit)]
    public void DecodeBlockReconstructsEqualAverageCompoundPrediction(int bitDepthValue)
    {
        Av1BitDepth bitDepth = (Av1BitDepth)bitDepthValue;
        ushort firstValue = bitDepth == Av1BitDepth.EightBit ? (ushort)20 : (ushort)100;
        ushort secondValue = bitDepth switch
        {
            Av1BitDepth.EightBit => 41,
            Av1BitDepth.TenBit => 701,
            _ => 3001,
        };

        ushort expected = (ushort)((firstValue + secondValue + 1) >> 1);
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader(bitDepth);
        ObuFrameHeader frameHeader = CreateFrameHeader();
        frameHeader.GetReferenceFrameIndices()[0] = 0;
        frameHeader.GetReferenceFrameIndices()[1] = 1;

        using Av1ReferenceFrameStore referenceFrames = new();
        Assert.True(referenceFrames.Commit(1, CreateReferenceFrame(sequenceHeader, firstValue), showFrame: false));
        Assert.True(referenceFrames.Commit(2, CreateReferenceFrame(sequenceHeader, secondValue), showFrame: false));

        using Av1FrameBuffer<byte> frameBuffer = new(
            Configuration.Default,
            sequenceHeader,
            Av1ColorFormat.Yuv400,
            false);

        using Av1FrameInfo frameInfo = new(sequenceHeader);
        Av1SuperblockInfo superblockInfo = frameInfo.GetSuperblock(Point.Empty);
        superblockInfo.GetTransformInfoY()[0] = new Av1TransformInfo(Av1TransformSize.Size8x8, 0, 0);

        Av1BlockModeInfo modeInfo = new(Av1BlockSize.Block8x8, Point.Empty)
        {
            Skip = true,
            YMode = Av1PredictionMode.NearestNearestMotionVector,
            CompoundIndex = true,
            CompoundType = Av1CompoundType.Average,
        };

        modeInfo.ReferenceFrames[0] = Av1ReferenceFrameType.Last;
        modeInfo.ReferenceFrames[1] = Av1ReferenceFrameType.Last2;
        modeInfo.InterpolationFilters.Fill(Av1InterpolationFilter.Regular);
        modeInfo.SetTransformUnitCount(Av1PlaneType.Y, 1);

        Av1LoopFilterContext loopFilterContext = new(sequenceHeader);
        Av1InverseQuantizer inverseQuantizer = new(sequenceHeader, frameHeader);
        using Av1BlockDecoder decoder = new(
            sequenceHeader,
            frameHeader,
            frameBuffer,
            loopFilterContext,
            inverseQuantizer,
            referenceFrames);

        decoder.UpdateSuperblock(superblockInfo);
        decoder.DecodeBlock(
            modeInfo,
            Point.Empty,
            Av1BlockSize.Block8x8,
            superblockInfo,
            new Av1TileInfo(0, 0, frameHeader));

        for (int row = 0; row < 8; row++)
        {
            if (bitDepth == Av1BitDepth.EightBit)
            {
                Span<byte> samples = frameBuffer.DeriveBlockPointer(Av1Plane.Y, 0, 0).DangerousGetRowSpan(row);
                for (int column = 0; column < 8; column++)
                {
                    Assert.Equal((byte)expected, samples[column]);
                }
            }
            else
            {
                Span<ushort> samples = frameBuffer.GetHighBitDepthRowSpan(Av1Plane.Y, row, 0, 0);
                for (int column = 0; column < 8; column++)
                {
                    Assert.Equal(expected, samples[column]);
                }
            }
        }
    }

    /// <summary>
    /// Verifies selectable compound reconstruction through the production block branch at every supported bit depth.
    /// </summary>
    /// <param name="bitDepthValue">The native sample depth.</param>
    /// <param name="compoundTypeValue">The selected compound operation.</param>
    [Theory]
    [InlineData((int)Av1BitDepth.EightBit, (int)Av1CompoundType.DistanceWeighted)]
    [InlineData((int)Av1BitDepth.TenBit, (int)Av1CompoundType.DistanceWeighted)]
    [InlineData((int)Av1BitDepth.TwelveBit, (int)Av1CompoundType.DistanceWeighted)]
    [InlineData((int)Av1BitDepth.EightBit, (int)Av1CompoundType.Wedge)]
    [InlineData((int)Av1BitDepth.TenBit, (int)Av1CompoundType.Wedge)]
    [InlineData((int)Av1BitDepth.TwelveBit, (int)Av1CompoundType.Wedge)]
    [InlineData((int)Av1BitDepth.EightBit, (int)Av1CompoundType.DifferenceWeighted)]
    [InlineData((int)Av1BitDepth.TenBit, (int)Av1CompoundType.DifferenceWeighted)]
    [InlineData((int)Av1BitDepth.TwelveBit, (int)Av1CompoundType.DifferenceWeighted)]
    public void DecodeBlockReconstructsSelectableCompoundPrediction(int bitDepthValue, int compoundTypeValue)
    {
        Av1BitDepth bitDepth = (Av1BitDepth)bitDepthValue;
        Av1CompoundType compoundType = (Av1CompoundType)compoundTypeValue;
        ushort firstValue = bitDepth == Av1BitDepth.EightBit ? (ushort)20 : (ushort)100;
        ushort secondValue = bitDepth switch
        {
            Av1BitDepth.EightBit => 41,
            Av1BitDepth.TenBit => 701,
            _ => 3001,
        };

        ReadOnlySpan<byte> wedgeMask =
        [
            0, 0, 0, 1, 1, 2, 4, 6,
            0, 1, 1, 2, 4, 6, 11, 18,
            1, 2, 4, 6, 11, 18, 27, 37,
            4, 6, 11, 18, 27, 37, 46, 53,
            11, 18, 27, 37, 46, 53, 58, 60,
            27, 37, 46, 53, 58, 60, 62, 63,
            46, 53, 58, 60, 62, 63, 63, 64,
            58, 60, 62, 63, 63, 64, 64, 64,
        ];

        ObuSequenceHeader sequenceHeader = CreateSequenceHeader(bitDepth);
        sequenceHeader.OrderHintInfo.EnableOrderHint = true;
        sequenceHeader.OrderHintInfo.OrderHintBits = 5;
        ObuFrameHeader frameHeader = CreateFrameHeader();
        frameHeader.OrderHint = 10;
        frameHeader.GetReferenceFrameIndices()[0] = 0;
        frameHeader.GetReferenceFrameIndices()[1] = 1;
        frameHeader.GetReferenceOrderHints()[0] = 9;
        frameHeader.GetReferenceOrderHints()[1] = 5;

        using Av1ReferenceFrameStore referenceFrames = new();
        Assert.True(referenceFrames.Commit(1, CreateReferenceFrame(sequenceHeader, firstValue), showFrame: false));
        Assert.True(referenceFrames.Commit(2, CreateReferenceFrame(sequenceHeader, secondValue), showFrame: false));

        using Av1FrameBuffer<byte> frameBuffer = new(
            Configuration.Default,
            sequenceHeader,
            Av1ColorFormat.Yuv400,
            false);

        using Av1FrameInfo frameInfo = new(sequenceHeader);
        Av1SuperblockInfo superblockInfo = frameInfo.GetSuperblock(Point.Empty);
        superblockInfo.GetTransformInfoY()[0] = new Av1TransformInfo(Av1TransformSize.Size8x8, 0, 0);

        Av1BlockModeInfo modeInfo = new(Av1BlockSize.Block8x8, Point.Empty)
        {
            Skip = true,
            YMode = Av1PredictionMode.NearestNearestMotionVector,
            CompoundIndex = compoundType != Av1CompoundType.DistanceWeighted,
            CompoundType = compoundType,
            CompoundWedgeIndex = 0,
            CompoundWedgeSign = true,
            DifferenceWeightedMaskType = Av1DifferenceWeightedMaskType.Type38,
        };

        modeInfo.ReferenceFrames[0] = Av1ReferenceFrameType.Last;
        modeInfo.ReferenceFrames[1] = Av1ReferenceFrameType.Last2;
        modeInfo.InterpolationFilters.Clear();
        modeInfo.SetTransformUnitCount(Av1PlaneType.Y, 1);

        Av1LoopFilterContext loopFilterContext = new(sequenceHeader);
        Av1InverseQuantizer inverseQuantizer = new(sequenceHeader, frameHeader);
        using Av1BlockDecoder decoder = new(
            sequenceHeader,
            frameHeader,
            frameBuffer,
            loopFilterContext,
            inverseQuantizer,
            referenceFrames);

        decoder.UpdateSuperblock(superblockInfo);
        decoder.DecodeBlock(
            modeInfo,
            Point.Empty,
            Av1BlockSize.Block8x8,
            superblockInfo,
            new Av1TileInfo(0, 0, frameHeader));

        int differenceShift = bitDepth.GetBitCount() - 8 + 4;
        int differenceAlpha = Math.Min(64, 38 + (Math.Abs(firstValue - secondValue) >> differenceShift));
        for (int row = 0; row < 8; row++)
        {
            for (int column = 0; column < 8; column++)
            {
                int alpha = compoundType switch
                {
                    Av1CompoundType.Wedge => wedgeMask[(row * 8) + column],
                    Av1CompoundType.DifferenceWeighted => differenceAlpha,
                    _ => 52,
                };

                ushort expected = (ushort)(((alpha * firstValue) + ((64 - alpha) * secondValue) + 32) >> 6);
                if (bitDepth == Av1BitDepth.EightBit)
                {
                    Span<byte> samples = frameBuffer.DeriveBlockPointer(Av1Plane.Y, 0, 0).DangerousGetRowSpan(row);
                    Assert.Equal((byte)expected, samples[column]);
                }
                else
                {
                    Span<ushort> samples = frameBuffer.GetHighBitDepthRowSpan(Av1Plane.Y, row, 0, 0);
                    Assert.Equal(expected, samples[column]);
                }
            }
        }
    }

    /// <summary>
    /// Verifies smooth inter-intra reconstruction through the production block branch at every supported bit depth.
    /// </summary>
    /// <param name="bitDepthValue">The native sample depth.</param>
    [Theory]
    [InlineData((int)Av1BitDepth.EightBit)]
    [InlineData((int)Av1BitDepth.TenBit)]
    [InlineData((int)Av1BitDepth.TwelveBit)]
    public void DecodeBlockReconstructsSmoothInterIntraPrediction(int bitDepthValue)
    {
        Av1BitDepth bitDepth = (Av1BitDepth)bitDepthValue;
        ushort interValue = bitDepth == Av1BitDepth.EightBit ? (ushort)20 : (ushort)100;
        ushort intraValue = (ushort)(1 << (bitDepth.GetBitCount() - 1));
        ushort expected = (ushort)((interValue + intraValue + 1) >> 1);
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader(bitDepth);
        ObuFrameHeader frameHeader = CreateFrameHeader();
        frameHeader.GetReferenceFrameIndices()[0] = 0;

        using Av1ReferenceFrameStore referenceFrames = new();
        Assert.True(referenceFrames.Commit(1, CreateReferenceFrame(sequenceHeader, interValue), showFrame: false));

        using Av1FrameBuffer<byte> frameBuffer = new(
            Configuration.Default,
            sequenceHeader,
            Av1ColorFormat.Yuv400,
            false);

        using Av1FrameInfo frameInfo = new(sequenceHeader);
        Av1SuperblockInfo superblockInfo = frameInfo.GetSuperblock(Point.Empty);
        superblockInfo.GetTransformInfoY()[0] = new Av1TransformInfo(Av1TransformSize.Size8x8, 0, 0);

        Av1BlockModeInfo modeInfo = new(Av1BlockSize.Block8x8, Point.Empty)
        {
            Skip = true,
            YMode = Av1PredictionMode.NearestMotionVector,
            InterIntraMode = Av1InterIntraMode.DC,
            UseInterIntraWedge = false,
        };

        modeInfo.ReferenceFrames[0] = Av1ReferenceFrameType.Last;
        modeInfo.ReferenceFrames[1] = Av1ReferenceFrameType.Intra;
        modeInfo.InterpolationFilters.Clear();
        modeInfo.SetTransformUnitCount(Av1PlaneType.Y, 1);

        Av1LoopFilterContext loopFilterContext = new(sequenceHeader);
        Av1InverseQuantizer inverseQuantizer = new(sequenceHeader, frameHeader);
        using Av1BlockDecoder decoder = new(
            sequenceHeader,
            frameHeader,
            frameBuffer,
            loopFilterContext,
            inverseQuantizer,
            referenceFrames);

        decoder.UpdateSuperblock(superblockInfo);
        decoder.DecodeBlock(
            modeInfo,
            Point.Empty,
            Av1BlockSize.Block8x8,
            superblockInfo,
            new Av1TileInfo(0, 0, frameHeader));

        for (int row = 0; row < 8; row++)
        {
            if (bitDepth == Av1BitDepth.EightBit)
            {
                Span<byte> samples = frameBuffer.DeriveBlockPointer(Av1Plane.Y, 0, 0).DangerousGetRowSpan(row);
                for (int column = 0; column < 8; column++)
                {
                    Assert.Equal((byte)expected, samples[column]);
                }
            }
            else
            {
                Span<ushort> samples = frameBuffer.GetHighBitDepthRowSpan(Av1Plane.Y, row, 0, 0);
                for (int column = 0; column < 8; column++)
                {
                    Assert.Equal(expected, samples[column]);
                }
            }
        }
    }

    /// <summary>
    /// Creates one independently owned retained frame filled with a constant visible luma value.
    /// </summary>
    private static Av1ReferenceFrame CreateReferenceFrame(ObuSequenceHeader sequenceHeader, ushort value)
    {
        Av1FrameBuffer<byte> frameBuffer = new(
            Configuration.Default,
            sequenceHeader,
            Av1ColorFormat.Yuv400,
            false);

        if (sequenceHeader.ColorConfig.BitDepth == Av1BitDepth.EightBit)
        {
            for (int row = 0; row < 8; row++)
            {
                frameBuffer.DeriveBlockPointer(Av1Plane.Y, 0, 0).DangerousGetRowSpan(row).Fill((byte)value);
            }
        }
        else
        {
            for (int row = 0; row < 8; row++)
            {
                frameBuffer.GetHighBitDepthRowSpan(Av1Plane.Y, row, 0, 0).Fill(value);
            }
        }

        using Av1FrameInfo frameInfo = new(sequenceHeader);
        return new Av1ReferenceFrame(frameBuffer, CreateFrameHeader(), frameInfo);
    }

    /// <summary>
    /// Creates the monochrome 8x8 sequence used by direct reconstruction tests.
    /// </summary>
    private static ObuSequenceHeader CreateSequenceHeader(Av1BitDepth bitDepth)
        => new()
        {
            MaxFrameWidth = 8,
            MaxFrameHeight = 8,
            Use128x128Superblock = false,
            ColorConfig = new ObuColorConfig
            {
                IsMonochrome = true,
                BitDepth = bitDepth,
            },
        };

    /// <summary>
    /// Creates an unscaled 8x8 inter-frame header with one complete tile.
    /// </summary>
    private static ObuFrameHeader CreateFrameHeader()
    {
        ObuFrameHeader frameHeader = new()
        {
            FrameType = ObuFrameType.InterFrame,
            ModeInfoColumnCount = 2,
            ModeInfoRowCount = 2,
            FrameSize = new ObuFrameSize
            {
                FrameWidth = 8,
                FrameHeight = 8,
            },
        };

        frameHeader.TilesInfo.TileColumnStartModeInfo[1] = frameHeader.ModeInfoColumnCount;
        frameHeader.TilesInfo.TileRowStartModeInfo[1] = frameHeader.ModeInfoRowCount;
        return frameHeader;
    }
}
