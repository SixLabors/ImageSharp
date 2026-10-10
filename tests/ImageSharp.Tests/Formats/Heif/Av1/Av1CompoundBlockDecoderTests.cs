// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
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
    /// Inter luma becomes available to shared chroma prediction after all of the block's residuals are reconstructed.
    /// </summary>
    /// <param name="bitDepthValue">The reconstructed sample precision.</param>
    /// <param name="colorFormatValue">The chroma subsampling layout.</param>
    /// <param name="largeTransform">Whether the final transform extends below the visible frame.</param>
    [Fact]
    public void InterChromaFromLumaIsStoredAfterBlock()
    {
        InterChromaFromLumaIsStoredAfterBlockCase((int)Av1BitDepth.EightBit, (int)Av1ColorFormat.Yuv420, false);
        InterChromaFromLumaIsStoredAfterBlockCase((int)Av1BitDepth.TenBit, (int)Av1ColorFormat.Yuv422, true);
        InterChromaFromLumaIsStoredAfterBlockCase((int)Av1BitDepth.TwelveBit, (int)Av1ColorFormat.Yuv420, true);
    }

    private static void InterChromaFromLumaIsStoredAfterBlockCase(int bitDepthValue, int colorFormatValue, bool largeTransform)
    {
        Av1BitDepth bitDepth = (Av1BitDepth)bitDepthValue;
        Av1ColorFormat colorFormat = (Av1ColorFormat)colorFormatValue;
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader(bitDepth, 8, colorFormat);
        ObuFrameHeader frameHeader = CreateFrameHeader(8);
        frameHeader.GetReferenceFrameIndices()[0] = 0;

        using Av1ReferenceFrameStore referenceFrames = new();
        Assert.True(referenceFrames.Commit(1, CreatePatternReferenceFrame(sequenceHeader, frameHeader, colorFormat, 0), showFrame: false));

        using Av1FrameBuffer<byte> frameBuffer = new(Configuration.Default, sequenceHeader, colorFormat, false);
        using Av1FrameInfo frameInfo = new(sequenceHeader);
        Av1SuperblockInfo superblockInfo = frameInfo.GetSuperblock(Point.Empty);
        int transformCount = largeTransform ? 1 : 2;
        Span<Av1TransformInfo> transforms = superblockInfo.GetTransformInfoY();
        for (int i = 0; i < transformCount; i++)
        {
            transforms[i] = new Av1TransformInfo(largeTransform ? Av1TransformSize.Size4x16 : Av1TransformSize.Size4x4, 0, i);
        }

        // The four-pixel-wide block shares chroma with its right neighbor. Its coded height exceeds the frame,
        // allowing the test to distinguish visible rows from the extent rounded to the final transform height.
        Av1BlockModeInfo modeInfo = CreateSingleReferenceModeInfo(Av1BlockSize.Block4x16, Point.Empty);
        modeInfo.SetTransformUnitCount(Av1PlaneType.Y, transformCount);
        frameInfo.UpdateModeInfo(modeInfo, superblockInfo);
        Av1PartitionInfo partitionInfo = new(modeInfo, superblockInfo, false, modeInfo.PartitionType);
        Av1TileInfo tileInfo = new(0, 0, frameHeader);
        partitionInfo.ComputeBoundaryOffsets(sequenceHeader, frameHeader, tileInfo);
        partitionInfo.PopulateModeInfoNeighbors(sequenceHeader.ColorConfig);

        using IMemoryOwner<short> workspace = frameBuffer.MemoryAllocator.Allocate<short>(Av1BlockDecoder.GetWorkspaceLength(sequenceHeader));
        Av1BlockDecoder decoder = new(
            sequenceHeader,
            frameHeader,
            frameBuffer,
            referenceFrames,
            workspace.Memory,
            paletteColorIndexMaps: null);

        decoder.UpdateSuperblock(superblockInfo);
        Span<short> decoderWorkspace = decoder.Workspace;
        Span<byte> frameLuma = decoder.GetFramePlane(Av1Plane.Y);
        Span<byte> frameBlue = decoder.GetFramePlane(Av1Plane.U);
        Span<byte> frameRed = decoder.GetFramePlane(Av1Plane.V);
        decoder.BeginBlock(ref partitionInfo, decoderWorkspace, frameLuma, frameBlue, frameRed, tileInfo);
        var chromaFromLumaContext = partitionInfo.ChromaFromLumaContext;
        Assert.NotNull(chromaFromLumaContext);
        chromaFromLumaContext.Q3Buffer.Fill(short.MinValue);

        Span<int> lumaCoefficients = superblockInfo.CoefficientsY;
        for (int i = 0; i < transformCount; i++)
        {
            decoder.DecodeTransform(ref partitionInfo, 0, ref transforms[i], decoderWorkspace, frameLuma, frameBlue, frameRed, lumaCoefficients, tileInfo);
        }

        foreach (short value in chromaFromLumaContext.Q3Buffer)
        {
            Assert.Equal(short.MinValue, value);
        }

        decoder.EndBlock(ref partitionInfo, decoderWorkspace, frameLuma);

        int subY = colorFormat == Av1ColorFormat.Yuv420 ? 1 : 0;
        int storedHeight = (largeTransform ? 16 : 8) >> subY;
        Span<byte> byteSamples = default;
        Span<short> shortSamples = default;
        int stride;
        if (bitDepth == Av1BitDepth.EightBit)
        {
            byteSamples = frameBuffer.DeriveBlockPointer(Av1Plane.Y, Point.Empty, 0, 0, out stride);
        }
        else
        {
            shortSamples = frameBuffer.DeriveBlockPointer16(Av1Plane.Y, Point.Empty, 0, 0, out stride);
        }

        // Independently average each 2x1 or 2x2 luma footprint and retain three fractional bits.
        // The source view starts one row above the block; samples outside the completed region stay poisoned.
        for (int row = 0; row < 32; row++)
        {
            for (int column = 0; column < 32; column++)
            {
                short expected = short.MinValue;
                if (row < storedHeight && column < 2)
                {
                    int sum = 0;
                    for (int y = 0; y < (1 << subY); y++)
                    {
                        for (int x = 0; x < 2; x++)
                        {
                            int index = (((row << subY) + y + 1) * stride) + (column * 2) + x;
                            sum += bitDepth == Av1BitDepth.EightBit ? byteSamples[index] : shortSamples[index];
                        }
                    }

                    expected = (short)(sum << (2 - subY));
                }

                Assert.Equal(expected, chromaFromLumaContext.Q3Buffer[(row * 32) + column]);
            }
        }
    }

    /// <summary>
    /// Verifies selectable compound reconstruction through the production block branch at every supported bit depth.
    /// </summary>
    /// <param name="bitDepthValue">The native sample depth.</param>
    /// <param name="compoundTypeValue">The selected compound operation.</param>
    [Fact]
    public void DecodeBlockWithSelectableCompound()
    {
        DecodeBlockWithSelectableCompoundCase((int)Av1BitDepth.EightBit, (int)Av1CompoundType.DistanceWeighted);
        DecodeBlockWithSelectableCompoundCase((int)Av1BitDepth.TenBit, (int)Av1CompoundType.DistanceWeighted);
        DecodeBlockWithSelectableCompoundCase((int)Av1BitDepth.TwelveBit, (int)Av1CompoundType.DistanceWeighted);
        DecodeBlockWithSelectableCompoundCase((int)Av1BitDepth.EightBit, (int)Av1CompoundType.Wedge);
        DecodeBlockWithSelectableCompoundCase((int)Av1BitDepth.TenBit, (int)Av1CompoundType.Wedge);
        DecodeBlockWithSelectableCompoundCase((int)Av1BitDepth.TwelveBit, (int)Av1CompoundType.Wedge);
        DecodeBlockWithSelectableCompoundCase((int)Av1BitDepth.EightBit, (int)Av1CompoundType.DifferenceWeighted);
        DecodeBlockWithSelectableCompoundCase((int)Av1BitDepth.TenBit, (int)Av1CompoundType.DifferenceWeighted);
        DecodeBlockWithSelectableCompoundCase((int)Av1BitDepth.TwelveBit, (int)Av1CompoundType.DifferenceWeighted);
    }

    private static void DecodeBlockWithSelectableCompoundCase(int bitDepthValue, int compoundTypeValue)
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

        using IMemoryOwner<short> workspace = frameBuffer.MemoryAllocator.Allocate<short>(Av1BlockDecoder.GetWorkspaceLength(sequenceHeader));
        Av1BlockDecoder decoder = new(
            sequenceHeader,
            frameHeader,
            frameBuffer,
            referenceFrames,
            workspace.Memory,
            paletteColorIndexMaps: null);

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
                    Span<byte> samples = frameBuffer.DeriveBlockPointer(Av1Plane.Y, 0, 0).GetRowSpan(row);
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
    /// Verifies above-then-left OBMC reconstruction through the production block branch at every supported bit depth.
    /// </summary>
    /// <param name="bitDepthValue">The native sample depth.</param>
    [Theory]
    [InlineData((int)Av1BitDepth.TenBit)]
    public void DecodeBlockWithObmc(int bitDepthValue)
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

        using IMemoryOwner<short> workspace = frameBuffer.MemoryAllocator.Allocate<short>(Av1BlockDecoder.GetWorkspaceLength(sequenceHeader));
        Av1BlockDecoder decoder = new(
            sequenceHeader,
            frameHeader,
            frameBuffer,
            referenceFrames,
            workspace.Memory,
            paletteColorIndexMaps: null);

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
                    Span<byte> samples = frameBuffer.DeriveBlockPointer(Av1Plane.Y, 0, 0).GetRowSpan(blockOrigin + row);
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
                    Span<byte> samples = frameBuffer.DeriveBlockPointer((Av1Plane)plane, subX, subY).GetRowSpan(row);
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
                frameBuffer.DeriveBlockPointer(Av1Plane.Y, 0, 0).GetRowSpan(row).Fill((byte)value);
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
