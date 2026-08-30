// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopFilter;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;
using SixLabors.ImageSharp.Formats.Heif.Av1.ReferenceFrames;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies compound prediction through the production block-reconstruction branch.
/// </summary>
[Trait("Format", "Avif")]
public class Av1CompoundBlockDecoderTests
{
    /// <summary>
    /// The hardware configurations covering the warped predictor's vector and scalar paths.
    /// </summary>
    private const HwIntrinsics GlobalWarpConfigurations = HwIntrinsics.AllowAll | HwIntrinsics.DisableHWIntrinsic;

    /// <summary>
    /// The hardware configurations covering every compound-prediction vector width and the scalar fallback.
    /// </summary>
    private const HwIntrinsics CompoundPredictionConfigurations =
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic;

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
    /// Verifies that high-bit-depth subpixel predictors retain their no-round precision until the compound average.
    /// </summary>
    [Fact]
    public void DecodeBlockReconstructsSubpixelHighBitDepthEqualAverageCompoundPrediction()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateSubpixelHighBitDepthEqualAverageCompoundPrediction,
            CompoundPredictionConfigurations);

    /// <summary>
    /// Verifies that high-bit-depth subpixel predictors retain no-round precision until distance weighting.
    /// </summary>
    [Fact]
    public void DecodeBlockReconstructsSubpixelHighBitDepthDistanceWeightedCompoundPrediction()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateSubpixelHighBitDepthDistanceWeightedCompoundPrediction,
            CompoundPredictionConfigurations);

    /// <summary>
    /// Verifies that high-bit-depth subpixel predictors retain no-round precision until wedge blending.
    /// </summary>
    [Fact]
    public void DecodeBlockReconstructsSubpixelHighBitDepthWedgeCompoundPrediction()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateSubpixelHighBitDepthWedgeCompoundPrediction,
            CompoundPredictionConfigurations);

    /// <summary>
    /// Verifies that high-bit-depth subpixel predictors retain no-round precision through difference masking and blending.
    /// </summary>
    [Fact]
    public void DecodeBlockReconstructsSubpixelHighBitDepthDifferenceWeightedCompoundPrediction()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateSubpixelHighBitDepthDifferenceWeightedCompoundPrediction,
            CompoundPredictionConfigurations);

    /// <summary>
    /// Verifies that both references of a GLOBAL_GLOBALMV block use their complete matrix before compound averaging.
    /// </summary>
    [Fact]
    public void DecodeBlockReconstructsCompoundGlobalWarpPrediction()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateCompoundGlobalWarpPrediction,
            GlobalWarpConfigurations);

    /// <summary>
    /// Verifies that the production block branch maps a smaller current frame into a larger retained reference.
    /// </summary>
    /// <param name="bitDepthValue">The native sample depth.</param>
    [Theory]
    [InlineData((int)Av1BitDepth.EightBit)]
    [InlineData((int)Av1BitDepth.TenBit)]
    [InlineData((int)Av1BitDepth.TwelveBit)]
    public void DecodeBlockReconstructsScaledSingleReferencePrediction(int bitDepthValue)
    {
        Av1BitDepth bitDepth = (Av1BitDepth)bitDepthValue;
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader(bitDepth, 16);
        ObuFrameHeader frameHeader = CreateFrameHeader();
        frameHeader.GetReferenceFrameIndices()[0] = 0;

        using Av1ReferenceFrameStore referenceFrames = new();
        Assert.True(referenceFrames.Commit(1, CreateScaledPatternReferenceFrame(sequenceHeader), showFrame: false));

        using Av1FrameBuffer<byte> frameBuffer = new(
            Configuration.Default,
            sequenceHeader,
            Av1ColorFormat.Yuv400,
            false);

        frameBuffer.Width = 8;
        frameBuffer.Height = 8;
        using Av1FrameInfo frameInfo = new(sequenceHeader);
        Av1SuperblockInfo superblockInfo = frameInfo.GetSuperblock(Point.Empty);
        superblockInfo.GetTransformInfoY()[0] = new Av1TransformInfo(Av1TransformSize.Size8x8, 0, 0);

        Av1BlockModeInfo modeInfo = CreateSingleReferenceModeInfo(Av1BlockSize.Block8x8, Point.Empty);
        modeInfo.InterpolationFilters.Fill(Av1InterpolationFilter.Bilinear);
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
            for (int column = 0; column < 8; column++)
            {
                ushort expected = (ushort)(5 + (column * 4) + (row * 16));
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
    /// Verifies above-then-left OBMC reconstruction through the production block branch at every supported bit depth.
    /// </summary>
    /// <param name="bitDepthValue">The native sample depth.</param>
    [Theory]
    [InlineData((int)Av1BitDepth.EightBit)]
    [InlineData((int)Av1BitDepth.TenBit)]
    [InlineData((int)Av1BitDepth.TwelveBit)]
    public void DecodeBlockReconstructsOverlappedMotionCompensation(int bitDepthValue)
    {
        const int frameSize = 24;
        const int blockOrigin = 8;
        Av1BitDepth bitDepth = (Av1BitDepth)bitDepthValue;
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader(bitDepth, frameSize);
        ObuFrameHeader frameHeader = CreateFrameHeader(frameSize);
        frameHeader.GetReferenceFrameIndices()[0] = 0;

        using Av1ReferenceFrameStore referenceFrames = new();
        Assert.True(referenceFrames.Commit(1, CreatePatternReferenceFrame(sequenceHeader, frameHeader), showFrame: false));

        using Av1FrameBuffer<byte> frameBuffer = new(
            Configuration.Default,
            sequenceHeader,
            Av1ColorFormat.Yuv400,
            false);

        using Av1FrameInfo frameInfo = new(sequenceHeader);
        Av1SuperblockInfo superblockInfo = frameInfo.GetSuperblock(Point.Empty);
        superblockInfo.GetTransformInfoY()[0] = new Av1TransformInfo(Av1TransformSize.Size8x8, 0, 0);

        Av1BlockModeInfo above = CreateSingleReferenceModeInfo(Av1BlockSize.Block8x8, new Point(2, 0));
        above.MotionVectors[0] = new Av1MotionVector(0, 8);
        frameInfo.UpdateModeInfo(above, superblockInfo);

        Av1BlockModeInfo left = CreateSingleReferenceModeInfo(Av1BlockSize.Block8x8, new Point(0, 2));
        left.MotionVectors[0] = new Av1MotionVector(8, 0);
        frameInfo.UpdateModeInfo(left, superblockInfo);

        Av1BlockModeInfo current = CreateSingleReferenceModeInfo(Av1BlockSize.Block8x8, new Point(2, 2));
        current.MotionMode = Av1MotionMode.Obmc;
        current.SetTransformUnitCount(Av1PlaneType.Y, 1);
        frameInfo.UpdateModeInfo(current, superblockInfo);

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
            current,
            new Point(2, 2),
            Av1BlockSize.Block8x8,
            superblockInfo,
            new Av1TileInfo(0, 0, frameHeader));

        ReadOnlySpan<byte> mask = [39, 50, 59, 64];
        for (int row = 0; row < 8; row++)
        {
            for (int column = 0; column < 8; column++)
            {
                int first = GetPatternValue(blockOrigin + column, blockOrigin + row);
                if (row < mask.Length)
                {
                    int aboveValue = GetPatternValue(blockOrigin + column + 1, blockOrigin + row);
                    first = ((mask[row] * first) + ((64 - mask[row]) * aboveValue) + 32) >> 6;
                }

                if (column < mask.Length)
                {
                    int leftValue = GetPatternValue(blockOrigin + column, blockOrigin + row + 1);
                    first = ((mask[column] * first) + ((64 - mask[column]) * leftValue) + 32) >> 6;
                }

                if (bitDepth == Av1BitDepth.EightBit)
                {
                    Span<byte> samples = frameBuffer.DeriveBlockPointer(Av1Plane.Y, 0, 0).DangerousGetRowSpan(blockOrigin + row);
                    Assert.Equal((byte)first, samples[blockOrigin + column]);
                }
                else
                {
                    Span<ushort> samples = frameBuffer.GetHighBitDepthRowSpan(Av1Plane.Y, blockOrigin + row, 0, 0);
                    Assert.Equal((ushort)first, samples[blockOrigin + column]);
                }
            }
        }
    }

    /// <summary>
    /// Verifies the above and left OBMC rectangles on horizontally and vertically subsampled chroma planes.
    /// </summary>
    /// <param name="colorFormatValue">The chroma-subsampling layout to reconstruct.</param>
    [Theory]
    [InlineData((int)Av1ColorFormat.Yuv420)]
    [InlineData((int)Av1ColorFormat.Yuv422)]
    public void DecodeBlockReconstructsSubsampledOverlappedMotionCompensation(int colorFormatValue)
    {
        const int frameSize = 48;
        const int blockOrigin = 16;
        Av1ColorFormat colorFormat = (Av1ColorFormat)colorFormatValue;
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader(Av1BitDepth.EightBit, frameSize, colorFormat);
        ObuFrameHeader frameHeader = CreateFrameHeader(frameSize);
        frameHeader.GetReferenceFrameIndices()[0] = 0;

        using Av1ReferenceFrameStore referenceFrames = new();
        Assert.True(referenceFrames.Commit(1, CreatePatternReferenceFrame(sequenceHeader, frameHeader, colorFormat), showFrame: false));

        using Av1FrameBuffer<byte> frameBuffer = new(
            Configuration.Default,
            sequenceHeader,
            colorFormat,
            false);

        using Av1FrameInfo frameInfo = new(sequenceHeader);
        Av1SuperblockInfo superblockInfo = frameInfo.GetSuperblock(Point.Empty);
        superblockInfo.GetTransformInfoY()[0] = new Av1TransformInfo(Av1TransformSize.Size16x16, 0, 0);
        superblockInfo.GetTransformInfoUv()[0] = new Av1TransformInfo(Av1TransformSize.Size8x8, 0, 0);
        superblockInfo.GetTransformInfoUv()[1] = new Av1TransformInfo(Av1TransformSize.Size8x8, 0, 0);

        Av1BlockModeInfo above = CreateSingleReferenceModeInfo(Av1BlockSize.Block16x16, new Point(4, 0));
        above.MotionVectors[0] = new Av1MotionVector(0, 16);
        frameInfo.UpdateModeInfo(above, superblockInfo);

        Av1BlockModeInfo left = CreateSingleReferenceModeInfo(Av1BlockSize.Block16x16, new Point(0, 4));
        left.MotionVectors[0] = new Av1MotionVector(16, 0);
        frameInfo.UpdateModeInfo(left, superblockInfo);

        Av1BlockModeInfo current = CreateSingleReferenceModeInfo(Av1BlockSize.Block16x16, new Point(4, 4));
        current.MotionMode = Av1MotionMode.Obmc;
        current.SetTransformUnitCount(Av1PlaneType.Y, 1);
        current.SetTransformUnitCount(Av1PlaneType.Uv, 1);
        frameInfo.UpdateModeInfo(current, superblockInfo);

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
            current,
            new Point(4, 4),
            Av1BlockSize.Block16x16,
            superblockInfo,
            new Av1TileInfo(0, 0, frameHeader));

        ReadOnlySpan<byte> mask4 = [39, 50, 59, 64];

        ReadOnlySpan<byte> mask8 = [36, 42, 48, 53, 57, 61, 64, 64];

        for (int plane = (int)Av1Plane.U; plane <= (int)Av1Plane.V; plane++)
        {
            int subY = colorFormat == Av1ColorFormat.Yuv420 ? 1 : 0;
            int planeOriginX = blockOrigin >> 1;
            int planeOriginY = blockOrigin >> subY;
            int predictionWidth = Av1BlockSize.Block16x16.GetWidth() >> 1;
            int predictionHeight = Av1BlockSize.Block16x16.GetHeight() >> subY;
            int leftSourceRowOffset = 2 >> subY;
            ReadOnlySpan<byte> verticalMask = subY == 0 ? mask8 : mask4;

            for (int row = 0; row < predictionHeight; row++)
            {
                Span<byte> samples = frameBuffer.DeriveBlockPointer((Av1Plane)plane, 1, subY).DangerousGetRowSpan(planeOriginY + row);
                for (int column = 0; column < predictionWidth; column++)
                {
                    int expected = GetPlanePatternValue(plane, planeOriginX + column, planeOriginY + row);
                    if (row < verticalMask.Length)
                    {
                        int aboveValue = GetPlanePatternValue(plane, planeOriginX + column + 1, planeOriginY + row);
                        expected = ((verticalMask[row] * expected) + ((64 - verticalMask[row]) * aboveValue) + 32) >> 6;
                    }

                    if (column < mask4.Length)
                    {
                        int leftValue = GetPlanePatternValue(plane, planeOriginX + column, planeOriginY + row + leftSourceRowOffset);
                        expected = ((mask4[column] * expected) + ((64 - mask4[column]) * leftValue) + 32) >> 6;
                    }

                    Assert.Equal((byte)expected, samples[planeOriginX + column]);
                }
            }
        }
    }

    /// <summary>
    /// Creates one skipped single-reference mode record for direct block-reconstruction tests.
    /// </summary>
    private static Av1BlockModeInfo CreateSingleReferenceModeInfo(Av1BlockSize blockSize, Point position)
    {
        Av1BlockModeInfo modeInfo = new(blockSize, position)
        {
            Skip = true,
            YMode = Av1PredictionMode.NearestMotionVector,
        };

        modeInfo.ReferenceFrames[0] = Av1ReferenceFrameType.Last;
        modeInfo.ReferenceFrames[1] = Av1ReferenceFrameType.None;
        modeInfo.InterpolationFilters.Clear();
        return modeInfo;
    }

    /// <summary>
    /// Reconstructs a compound global-warp block at every supported native sample depth.
    /// </summary>
    private static void ValidateCompoundGlobalWarpPrediction()
    {
        foreach (Av1BitDepth bitDepth in new[] { Av1BitDepth.EightBit, Av1BitDepth.TenBit, Av1BitDepth.TwelveBit })
        {
            ValidateCompoundGlobalWarpPredictionAtBitDepth(bitDepth);
        }
    }

    /// <summary>
    /// Reconstructs the high-bit-depth subpixel compound regression at every supported source precision.
    /// </summary>
    private static void ValidateSubpixelHighBitDepthEqualAverageCompoundPrediction()
    {
        foreach (Av1BitDepth bitDepth in new[] { Av1BitDepth.TenBit, Av1BitDepth.TwelveBit })
        {
            ValidateSubpixelHighBitDepthCompoundPredictionAtBitDepth(bitDepth, Av1CompoundType.Average);
        }
    }

    /// <summary>
    /// Reconstructs the high-bit-depth subpixel distance-weighted regression at every supported source precision.
    /// </summary>
    private static void ValidateSubpixelHighBitDepthDistanceWeightedCompoundPrediction()
    {
        foreach (Av1BitDepth bitDepth in new[] { Av1BitDepth.TenBit, Av1BitDepth.TwelveBit })
        {
            ValidateSubpixelHighBitDepthCompoundPredictionAtBitDepth(bitDepth, Av1CompoundType.DistanceWeighted);
        }
    }

    /// <summary>
    /// Reconstructs the high-bit-depth subpixel wedge regression at every supported source precision.
    /// </summary>
    private static void ValidateSubpixelHighBitDepthWedgeCompoundPrediction()
    {
        foreach (Av1BitDepth bitDepth in new[] { Av1BitDepth.TenBit, Av1BitDepth.TwelveBit })
        {
            ValidateSubpixelHighBitDepthCompoundPredictionAtBitDepth(bitDepth, Av1CompoundType.Wedge);
        }
    }

    /// <summary>
    /// Reconstructs both high-bit-depth subpixel difference-mask orientations at every supported source precision.
    /// </summary>
    private static void ValidateSubpixelHighBitDepthDifferenceWeightedCompoundPrediction()
    {
        foreach (Av1BitDepth bitDepth in new[] { Av1BitDepth.TenBit, Av1BitDepth.TwelveBit })
        {
            foreach (Av1DifferenceWeightedMaskType maskType in Enum.GetValues<Av1DifferenceWeightedMaskType>())
            {
                ValidateSubpixelHighBitDepthCompoundPredictionAtBitDepth(
                    bitDepth,
                    Av1CompoundType.DifferenceWeighted,
                    maskType);
            }
        }
    }

    /// <summary>
    /// Reconstructs one high-bit-depth half-sample compound block and compares it with the scalar no-round pipeline.
    /// </summary>
    /// <param name="bitDepth">The native sample depth.</param>
    /// <param name="compoundType">The final compound operation.</param>
    /// <param name="differenceWeightedMaskType">The difference-mask orientation when that compound operation is selected.</param>
    private static void ValidateSubpixelHighBitDepthCompoundPredictionAtBitDepth(
        Av1BitDepth bitDepth,
        Av1CompoundType compoundType,
        Av1DifferenceWeightedMaskType differenceWeightedMaskType = Av1DifferenceWeightedMaskType.Type38)
    {
        const int frameSize = 32;
        const int blockOrigin = 8;
        const int blockSize = 8;
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader(bitDepth, frameSize);
        sequenceHeader.OrderHintInfo.EnableOrderHint = true;
        sequenceHeader.OrderHintInfo.OrderHintBits = 5;
        ObuFrameHeader frameHeader = CreateFrameHeader(frameSize);
        frameHeader.OrderHint = 10;
        frameHeader.GetReferenceFrameIndices()[0] = 0;
        frameHeader.GetReferenceFrameIndices()[1] = 1;
        frameHeader.GetReferenceOrderHints()[0] = 9;
        frameHeader.GetReferenceOrderHints()[1] = 5;

        using Av1ReferenceFrameStore referenceFrames = new();
        Assert.True(referenceFrames.Commit(
            1,
            CreatePatternReferenceFrame(sequenceHeader, CreateFrameHeader(frameSize)),
            showFrame: false));

        Assert.True(referenceFrames.Commit(
            2,
            CreatePatternReferenceFrame(sequenceHeader, CreateFrameHeader(frameSize), sampleOffset: 40),
            showFrame: false));

        Av1BlockModeInfo modeInfo = new(Av1BlockSize.Block8x8, new Point(2, 2))
        {
            Skip = true,
            YMode = Av1PredictionMode.NearestNearestMotionVector,
            CompoundIndex = compoundType != Av1CompoundType.DistanceWeighted,
            CompoundType = compoundType,
            CompoundWedgeIndex = 0,
            CompoundWedgeSign = true,
            DifferenceWeightedMaskType = differenceWeightedMaskType,
        };

        modeInfo.ReferenceFrames[0] = Av1ReferenceFrameType.Last;
        modeInfo.ReferenceFrames[1] = Av1ReferenceFrameType.Last2;
        modeInfo.MotionVectors[0] = new Av1MotionVector(0, 0);

        // The second predictor lands exactly halfway between horizontal samples. Rounding it before combining the
        // references changes every result by one, so this vector distinguishes the required no-round production path.
        modeInfo.MotionVectors[1] = new Av1MotionVector(0, 4);
        modeInfo.InterpolationFilters.Fill(Av1InterpolationFilter.Bilinear);
        modeInfo.SetTransformUnitCount(Av1PlaneType.Y, 1);

        ushort[] expectedFirst = new ushort[blockSize * blockSize];
        ushort[] expectedSecond = new ushort[blockSize * blockSize];
        Span<ushort> expectedPredictions = expectedFirst;
        ushort[] actualFirst = new ushort[blockSize * blockSize];
        ushort[] actualSecond = new ushort[blockSize * blockSize];
        Span<ushort> actualPredictions = actualFirst;
        short[] predictionScratch = new short[128 * (blockSize + 8)];
        for (int referenceIndex = 0; referenceIndex < 2; referenceIndex++)
        {
            Av1FrameBuffer<byte> reference = referenceFrames.Resolve(referenceIndex)!.FrameBuffer;
            Span<ushort> source = reference.GetPaddedPlaneSpan16(
                Av1Plane.Y,
                0,
                0,
                out int sourceStride,
                out Point sourceOrigin);

            Av1MotionVector motionVector = modeInfo.MotionVectors[referenceIndex];
            int sourceColumnQ4 = (blockOrigin << 4) + (motionVector.Column << 1);
            int sourceRowQ4 = (blockOrigin << 4) + (motionVector.Row << 1);
            int sourceIndex =
                ((sourceOrigin.Y + (sourceRowQ4 >> 4)) * sourceStride) + sourceOrigin.X + (sourceColumnQ4 >> 4);

            Av1CompoundInterPredictor.PredictCompoundScalar(
                source,
                sourceStride,
                sourceIndex,
                expectedPredictions,
                blockSize,
                blockSize,
                blockSize,
                Av1InterpolationFilter.Bilinear,
                Av1InterpolationFilter.Bilinear,
                sourceColumnQ4 & 15,
                sourceRowQ4 & 15,
                bitDepth.GetBitCount(),
                predictionScratch);

            Av1CompoundInterPredictor.PredictCompound(
                source,
                sourceStride,
                sourceIndex,
                actualPredictions,
                blockSize,
                blockSize,
                blockSize,
                Av1InterpolationFilter.Bilinear,
                Av1InterpolationFilter.Bilinear,
                sourceColumnQ4 & 15,
                sourceRowQ4 & 15,
                bitDepth.GetBitCount(),
                predictionScratch);

            expectedPredictions = expectedSecond;
            actualPredictions = actualSecond;
        }

        Assert.Equal(expectedFirst, actualFirst);
        Assert.Equal(expectedSecond, actualSecond);

        ushort[] expected = new ushort[blockSize * blockSize];
        if (compoundType == Av1CompoundType.DistanceWeighted)
        {
            Av1CompoundDistanceWeights.Derive(
                sequenceHeader.OrderHintInfo,
                frameHeader,
                modeInfo.ReferenceFrames[0],
                modeInfo.ReferenceFrames[1],
                out int firstWeight,
                out int secondWeight);

            Av1CompoundIntermediateDistanceWeightedPredictor.DistanceWeightedIntermediate(
                expected,
                blockSize,
                expectedFirst,
                blockSize,
                expectedSecond,
                blockSize,
                blockSize,
                blockSize,
                firstWeight,
                secondWeight,
                bitDepth.GetBitCount());

            Assert.NotEqual((ushort)60, expected[0]);
        }
        else if (compoundType == Av1CompoundType.DifferenceWeighted)
        {
            int bitCount = bitDepth.GetBitCount();
            int intermediateRange = bitCount + 7 - 3 + 2;
            int round0 = 3 + Math.Max(intermediateRange - 16, 0);
            int roundBits = 14 - round0 - 7;
            int offsetBits = bitCount + 14 - round0;
            int roundOffset = (1 << (offsetBits - 7)) + (1 << (offsetBits - 8));
            int differenceRound = roundBits + bitCount - 8;
            int maximum = (1 << bitCount) - 1;
            byte[] expectedMask = new byte[blockSize * blockSize];
            byte[] actualMask = new byte[blockSize * blockSize];

            for (int index = 0; index < expected.Length; index++)
            {
                int difference = Math.Abs(expectedFirst[index] - expectedSecond[index]);
                difference = (difference + (1 << (differenceRound - 1))) >> differenceRound;
                int alpha = Math.Min(64, 38 + (difference >> 4));
                if (differenceWeightedMaskType == Av1DifferenceWeightedMaskType.Type38Inverse)
                {
                    alpha = 64 - alpha;
                }

                expectedMask[index] = (byte)alpha;
                int result = ((alpha * expectedFirst[index]) + ((64 - alpha) * expectedSecond[index])) >> 6;
                result -= roundOffset;
                if (roundBits != 0)
                {
                    result = (result + (1 << (roundBits - 1))) >> roundBits;
                }

                expected[index] = (ushort)Math.Clamp(result, 0, maximum);
            }

            Av1CompoundIntermediateDifferenceWeightedMaskBuilder.FillDifferenceWeightedIntermediateMask(
                actualMask,
                blockSize,
                expectedFirst,
                blockSize,
                expectedSecond,
                blockSize,
                blockSize,
                blockSize,
                bitCount,
                differenceWeightedMaskType);

            Assert.Equal(expectedMask, actualMask);
        }
        else if (compoundType == Av1CompoundType.Wedge)
        {
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

            byte[] generatedWedgeMask = new byte[blockSize * blockSize];
            Av1WedgeMask.Fill(
                generatedWedgeMask,
                blockSize,
                Av1BlockSize.Block8x8,
                wedgeIndex: 0,
                wedgeSign: true,
                subX: 0,
                subY: 0,
                invert: false);

            Assert.Equal(wedgeMask, generatedWedgeMask);

            Av1CompoundIntermediateMaskBlendPredictor.BlendIntermediate(
                expected,
                blockSize,
                expectedFirst,
                blockSize,
                expectedSecond,
                blockSize,
                wedgeMask,
                blockSize,
                blockSize,
                blockSize,
                subX: 0,
                subY: 0,
                bitDepth.GetBitCount());

            Assert.NotEqual((ushort)60, expected[0]);
        }
        else
        {
            Av1CompoundIntermediateAveragePredictor.AverageIntermediate(
                expected,
                blockSize,
                expectedFirst,
                blockSize,
                expectedSecond,
                blockSize,
                blockSize,
                blockSize,
                bitDepth.GetBitCount());

            Assert.Equal((ushort)60, expected[0]);
        }

        using Av1FrameBuffer<byte> frameBuffer = new(
            Configuration.Default,
            sequenceHeader,
            Av1ColorFormat.Yuv400,
            false);

        using Av1FrameInfo frameInfo = new(sequenceHeader);
        Av1SuperblockInfo superblockInfo = frameInfo.GetSuperblock(Point.Empty);
        superblockInfo.GetTransformInfoY()[0] = new Av1TransformInfo(Av1TransformSize.Size8x8, 0, 0);
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
            new Point(2, 2),
            Av1BlockSize.Block8x8,
            superblockInfo,
            new Av1TileInfo(0, 0, frameHeader));

        for (int row = 0; row < blockSize; row++)
        {
            Span<ushort> actual = frameBuffer.GetHighBitDepthRowSpan(Av1Plane.Y, blockOrigin + row, 0, 0);
            Assert.Equal(
                expected.AsSpan(row * blockSize, blockSize),
                actual.Slice(blockOrigin, blockSize));
        }
    }

    /// <summary>
    /// Reconstructs one compound global-warp block and compares it with independently invoked scalar predictors.
    /// </summary>
    /// <param name="bitDepth">The native sample depth.</param>
    private static void ValidateCompoundGlobalWarpPredictionAtBitDepth(Av1BitDepth bitDepth)
    {
        const int frameSize = 32;
        const int blockOrigin = 8;
        const int blockSize = 8;
        const int compoundRoundBits = 4;
        const int compoundRoundOffset = (1 << 12) + (1 << 11);
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader(bitDepth, frameSize);
        ObuFrameHeader frameHeader = CreateFrameHeader(frameSize);
        frameHeader.GetReferenceFrameIndices()[0] = 0;
        frameHeader.GetReferenceFrameIndices()[1] = 1;

        Av1GlobalMotionParameters globalMotionParameters = Av1GlobalMotionParameters.Identity;
        globalMotionParameters.Type = Av1GlobalMotionType.RotationZoom;
        globalMotionParameters[0] = -357376;
        globalMotionParameters[1] = 372736;
        globalMotionParameters[2] = 65468;
        globalMotionParameters[3] = 2856;
        globalMotionParameters[4] = -2856;
        globalMotionParameters[5] = 65468;
        globalMotionParameters.UpdateShearParameters();
        Assert.False(globalMotionParameters.IsInvalid);
        frameHeader.GetGlobalMotionParameters()[0] = globalMotionParameters;
        frameHeader.GetGlobalMotionParameters()[1] = globalMotionParameters;

        using Av1ReferenceFrameStore referenceFrames = new();
        Assert.True(referenceFrames.Commit(
            1,
            CreatePatternReferenceFrame(sequenceHeader, CreateFrameHeader(frameSize)),
            showFrame: false));

        Assert.True(referenceFrames.Commit(
            2,
            CreatePatternReferenceFrame(sequenceHeader, CreateFrameHeader(frameSize), sampleOffset: 40),
            showFrame: false));

        Av1FrameBuffer<byte> firstReference = referenceFrames.Resolve(0)!.FrameBuffer;
        Av1FrameBuffer<byte> secondReference = referenceFrames.Resolve(1)!.FrameBuffer;
        ushort[] firstHighBitDepthPrediction = new ushort[blockSize * blockSize];
        ushort[] secondHighBitDepthPrediction = new ushort[blockSize * blockSize];
        short[] firstScratch = new short[Av1WarpedInterPredictor.WarpedScratchLength];
        short[] secondScratch = new short[Av1WarpedInterPredictor.WarpedScratchLength];
        Point blockPosition = new(blockOrigin, blockOrigin);
        if (bitDepth == Av1BitDepth.EightBit)
        {
            Span<byte> firstSource = firstReference.GetPaddedPlaneSpan(
                Av1Plane.Y,
                0,
                0,
                out int firstStride,
                out Point firstOrigin);

            Span<byte> secondSource = secondReference.GetPaddedPlaneSpan(
                Av1Plane.Y,
                0,
                0,
                out int secondStride,
                out Point secondOrigin);

            Av1WarpedInterPredictor.PredictWarpedCompoundScalar(
                firstSource,
                firstStride,
                firstOrigin,
                frameSize,
                frameSize,
                firstHighBitDepthPrediction,
                blockSize,
                blockPosition,
                blockSize,
                blockSize,
                0,
                0,
                globalMotionParameters,
                firstScratch);

            Av1WarpedInterPredictor.PredictWarpedCompoundScalar(
                secondSource,
                secondStride,
                secondOrigin,
                frameSize,
                frameSize,
                secondHighBitDepthPrediction,
                blockSize,
                blockPosition,
                blockSize,
                blockSize,
                0,
                0,
                globalMotionParameters,
                secondScratch);
        }
        else
        {
            Span<ushort> firstSource = firstReference.GetPaddedPlaneSpan16(
                Av1Plane.Y,
                0,
                0,
                out int firstStride,
                out Point firstOrigin);

            Span<ushort> secondSource = secondReference.GetPaddedPlaneSpan16(
                Av1Plane.Y,
                0,
                0,
                out int secondStride,
                out Point secondOrigin);

            int bitDepthValue = bitDepth.GetBitCount();
            Av1WarpedInterPredictor.PredictWarpedScalar(
                firstSource,
                firstStride,
                firstOrigin,
                frameSize,
                frameSize,
                firstHighBitDepthPrediction,
                blockSize,
                blockPosition,
                blockSize,
                blockSize,
                0,
                0,
                bitDepthValue,
                globalMotionParameters,
                firstScratch);

            Av1WarpedInterPredictor.PredictWarpedScalar(
                secondSource,
                secondStride,
                secondOrigin,
                frameSize,
                frameSize,
                secondHighBitDepthPrediction,
                blockSize,
                blockPosition,
                blockSize,
                blockSize,
                0,
                0,
                bitDepthValue,
                globalMotionParameters,
                secondScratch);
        }

        using Av1FrameBuffer<byte> frameBuffer = new(
            Configuration.Default,
            sequenceHeader,
            Av1ColorFormat.Yuv400,
            false);

        using Av1FrameInfo frameInfo = new(sequenceHeader);
        Av1SuperblockInfo superblockInfo = frameInfo.GetSuperblock(Point.Empty);
        superblockInfo.GetTransformInfoY()[0] = new Av1TransformInfo(Av1TransformSize.Size8x8, 0, 0);

        Av1BlockModeInfo modeInfo = new(Av1BlockSize.Block8x8, new Point(2, 2))
        {
            Skip = true,
            YMode = Av1PredictionMode.GlobalGlobalMotionVector,
            CompoundIndex = true,
            CompoundType = Av1CompoundType.Average,
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
            new Point(2, 2),
            Av1BlockSize.Block8x8,
            superblockInfo,
            new Av1TileInfo(0, 0, frameHeader));

        for (int row = 0; row < blockSize; row++)
        {
            for (int column = 0; column < blockSize; column++)
            {
                int predictionIndex = (row * blockSize) + column;
                if (bitDepth == Av1BitDepth.EightBit)
                {
                    // Libaom truncates the equal average before removing the Q4 compound bias, then performs the
                    // sole final rounding step. Averaging two already reconstructed pixels can differ by one.
                    int intermediate = ((firstHighBitDepthPrediction[predictionIndex] +
                        secondHighBitDepthPrediction[predictionIndex]) >> 1) - compoundRoundOffset;

                    byte expected = (byte)Math.Clamp(
                        (intermediate + (1 << (compoundRoundBits - 1))) >> compoundRoundBits,
                        0,
                        byte.MaxValue);

                    Span<byte> samples = frameBuffer.DeriveBlockPointer(Av1Plane.Y, 0, 0).DangerousGetRowSpan(blockOrigin + row);
                    Assert.Equal(expected, samples[blockOrigin + column]);
                }
                else
                {
                    ushort expected = (ushort)((firstHighBitDepthPrediction[predictionIndex] +
                        secondHighBitDepthPrediction[predictionIndex] + 1) >> 1);

                    Span<ushort> samples = frameBuffer.GetHighBitDepthRowSpan(Av1Plane.Y, blockOrigin + row, 0, 0);
                    Assert.Equal(expected, samples[blockOrigin + column]);
                }
            }
        }
    }

    /// <summary>
    /// Creates one retained frame whose integer-coordinate luma samples make both OBMC axes observable.
    /// </summary>
    private static Av1ReferenceFrame CreatePatternReferenceFrame(
        ObuSequenceHeader sequenceHeader,
        ObuFrameHeader frameHeader,
        Av1ColorFormat colorFormat = Av1ColorFormat.Yuv400,
        int sampleOffset = 0)
    {
        Av1FrameBuffer<byte> frameBuffer = new(
            Configuration.Default,
            sequenceHeader,
            colorFormat,
            false);

        for (int plane = 0; plane < sequenceHeader.ColorConfig.PlaneCount; plane++)
        {
            int subX = plane > 0 && sequenceHeader.ColorConfig.SubSamplingX ? 1 : 0;
            int subY = plane > 0 && sequenceHeader.ColorConfig.SubSamplingY ? 1 : 0;
            int planeWidth = sequenceHeader.MaxFrameWidth >> subX;
            int planeHeight = sequenceHeader.MaxFrameHeight >> subY;
            for (int row = 0; row < planeHeight; row++)
            {
                if (sequenceHeader.ColorConfig.BitDepth == Av1BitDepth.EightBit)
                {
                    Span<byte> samples = frameBuffer.DeriveBlockPointer((Av1Plane)plane, subX, subY).DangerousGetRowSpan(row);
                    for (int column = 0; column < planeWidth; column++)
                    {
                        samples[column] = (byte)(GetPlanePatternValue(plane, column, row) + sampleOffset);
                    }
                }
                else
                {
                    Span<ushort> samples = frameBuffer.GetHighBitDepthRowSpan((Av1Plane)plane, row, subX, subY);
                    for (int column = 0; column < planeWidth; column++)
                    {
                        samples[column] = (ushort)(GetPlanePatternValue(plane, column, row) + sampleOffset);
                    }
                }
            }
        }

        Av1ReferenceFrameBorder.Extend(frameBuffer);
        using Av1FrameInfo frameInfo = new(sequenceHeader);
        return new Av1ReferenceFrame(frameBuffer, frameHeader, frameInfo);
    }

    /// <summary>
    /// Creates a 16x16 retained frame whose linear pattern has an exact half-sample bilinear result.
    /// </summary>
    private static Av1ReferenceFrame CreateScaledPatternReferenceFrame(ObuSequenceHeader sequenceHeader)
    {
        Av1FrameBuffer<byte> frameBuffer = new(
            Configuration.Default,
            sequenceHeader,
            Av1ColorFormat.Yuv400,
            false);

        frameBuffer.Width = 16;
        frameBuffer.Height = 16;
        for (int row = 0; row < 16; row++)
        {
            if (sequenceHeader.ColorConfig.BitDepth == Av1BitDepth.EightBit)
            {
                Span<byte> samples = frameBuffer.DeriveBlockPointer(Av1Plane.Y, 0, 0).DangerousGetRowSpan(row);
                for (int column = 0; column < 16; column++)
                {
                    samples[column] = (byte)((column * 2) + (row * 8));
                }
            }
            else
            {
                Span<ushort> samples = frameBuffer.GetHighBitDepthRowSpan(Av1Plane.Y, row, 0, 0);
                for (int column = 0; column < 16; column++)
                {
                    samples[column] = (ushort)((column * 2) + (row * 8));
                }
            }
        }

        Av1ReferenceFrameBorder.Extend(frameBuffer);
        using Av1FrameInfo frameInfo = new(sequenceHeader);
        return new Av1ReferenceFrame(frameBuffer, CreateFrameHeader(16), frameInfo);
    }

    /// <summary>
    /// Gets the deterministic luma value stored at one reference-frame coordinate.
    /// </summary>
    private static int GetPatternValue(int column, int row) => column + (row * 4);

    /// <summary>
    /// Gets the deterministic plane value stored at one reference-frame coordinate.
    /// </summary>
    private static int GetPlanePatternValue(int plane, int column, int row) => GetPatternValue(column, row) + (plane * 20);

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
    private static ObuSequenceHeader CreateSequenceHeader(
        Av1BitDepth bitDepth,
        int frameSize = 8,
        Av1ColorFormat colorFormat = Av1ColorFormat.Yuv400)
        => new()
        {
            MaxFrameWidth = frameSize,
            MaxFrameHeight = frameSize,
            Use128x128Superblock = false,
            ColorConfig = new ObuColorConfig
            {
                IsMonochrome = colorFormat == Av1ColorFormat.Yuv400,
                BitDepth = bitDepth,
                SubSamplingX = colorFormat is Av1ColorFormat.Yuv420 or Av1ColorFormat.Yuv422,
                SubSamplingY = colorFormat == Av1ColorFormat.Yuv420,
            },
        };

    /// <summary>
    /// Creates an unscaled 8x8 inter-frame header with one complete tile.
    /// </summary>
    private static ObuFrameHeader CreateFrameHeader(int frameSize = 8)
    {
        ObuFrameHeader frameHeader = new()
        {
            FrameType = ObuFrameType.InterFrame,
            ModeInfoColumnCount = frameSize >> Av1Constants.ModeInfoSizeLog2,
            ModeInfoRowCount = frameSize >> Av1Constants.ModeInfoSizeLog2,
            FrameSize = new ObuFrameSize
            {
                FrameWidth = frameSize,
                FrameHeight = frameSize,
            },
        };

        frameHeader.TilesInfo.TileColumnStartModeInfo[1] = frameHeader.ModeInfoColumnCount;
        frameHeader.TilesInfo.TileRowStartModeInfo[1] = frameHeader.ModeInfoRowCount;
        return frameHeader;
    }
}
