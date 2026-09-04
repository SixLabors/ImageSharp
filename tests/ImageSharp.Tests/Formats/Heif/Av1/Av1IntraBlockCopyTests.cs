// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies AV1 intra-block-copy reference derivation and displacement-vector legality rules.
/// </summary>
[Trait("Format", "Heif")]
public class Av1IntraBlockCopyTests
{
    /// <summary>
    /// Verifies the horizontal fallback used in the tile's first superblock row.
    /// </summary>
    [Fact]
    public void FindReferenceUsesFirstRowFallback()
    {
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader();
        using Av1FrameInfo frameInfo = new(sequenceHeader);
        Av1SuperblockInfo superblockInfo = frameInfo.GetSuperblock(new Point(5, 0));
        Av1BlockModeInfo modeInfo = new(Av1BlockSize.Block16x16, Point.Empty);
        Av1PartitionInfo partitionInfo = new(modeInfo, superblockInfo, true, Av1PartitionType.None)
        {
            ColumnIndex = 80,
            RowIndex = 0,
        };

        Av1TileInfo tileInfo = CreateTileInfo();
        Av1MotionVector[] candidates = new Av1MotionVector[8];
        int[] weights = new int[8];

        Av1MotionVector actual = Av1IntraBlockCopy.FindReference(
            ref partitionInfo,
            tileInfo,
            sequenceHeader.SuperblockModeInfoSize,
            candidates,
            weights);

        Assert.Equal(new Av1MotionVector(0, -2560), actual);
    }

    /// <summary>
    /// Verifies the vertical fallback used after the tile's first superblock row when spatial candidates are absent.
    /// </summary>
    [Fact]
    public void FindReferenceUsesPreviousSuperblockRowFallback()
    {
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader();
        using Av1FrameInfo frameInfo = new(sequenceHeader);
        Av1SuperblockInfo aboveSuperblock = frameInfo.GetSuperblock(new Point(5, 0));
        Av1BlockModeInfo aboveModeInfo = new(Av1BlockSize.Block64x64, Point.Empty);
        frameInfo.UpdateModeInfo(aboveModeInfo, aboveSuperblock);

        Av1SuperblockInfo superblockInfo = frameInfo.GetSuperblock(new Point(5, 1));
        Av1BlockModeInfo modeInfo = new(Av1BlockSize.Block16x16, Point.Empty);
        Av1PartitionInfo partitionInfo = new(modeInfo, superblockInfo, true, Av1PartitionType.None)
        {
            AvailableAbove = true,
            ColumnIndex = 80,
            RowIndex = 16,
        };

        Av1TileInfo tileInfo = CreateTileInfo();
        Av1MotionVector[] candidates = new Av1MotionVector[8];
        int[] weights = new int[8];

        Av1MotionVector actual = Av1IntraBlockCopy.FindReference(
            ref partitionInfo,
            tileInfo,
            sequenceHeader.SuperblockModeInfoSize,
            candidates,
            weights);

        Assert.Equal(new Av1MotionVector(-512, 0), actual);
    }

    /// <summary>
    /// Verifies that encoder mode aliases and packed displacement storage feed the shared spatial ranking.
    /// </summary>
    [Fact]
    public void EncoderReferenceUsesMappedIntraBlockCopyNeighbor()
    {
        const int Width = 640;
        const int Height = 256;
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader();
        ObuFrameHeader frameHeader = CreateFrameHeader();
        frameHeader.AllowScreenContentTools = true;
        frameHeader.AllowIntraBlockCopy = true;

        using Av1EncoderPictureBuffer buffer = new(
            Configuration.Default,
            sequenceHeader,
            frameHeader,
            Width,
            Height,
            disallow4x4AllFrames: true);

        Av1PictureControlSet picture = buffer.Picture;
        Point candidatePosition = new(80, 12);
        ref Av1MacroBlockModeInfo candidate = ref picture.GetMacroBlockModeInfo(candidatePosition);
        candidate.Block = new Av1EncoderBlockModeInfo
        {
            BlockSize = Av1BlockSize.Block16x16,
            PartitionType = Av1PartitionType.None,
            UseIntraBlockCopy = true
        };

        Av1MotionVector displacement = new(0, -2560);
        picture.MapModeInfoBlock(candidatePosition, candidate.Block.BlockSize);
        picture.SetDisplacementVector(candidatePosition, displacement);

        Point currentPosition = new(80, 16);
        Av1TileInfo tileInfo = new(0, 0, frameHeader);
        Av1MacroBlockD macroBlock = new() { Tile = tileInfo };
        Av1TileWriter.SetModeInfoRowAndColumn(
            picture,
            macroBlock,
            tileInfo,
            currentPosition,
            Av1BlockSize.Block16x16,
            picture.ModeInfoStride,
            picture.Parent.Common.ModeInfoRowCount,
            picture.Parent.Common.ModeInfoColumnCount);

        Span<Av1MotionVector> candidates = stackalloc Av1MotionVector[8];
        Span<int> weights = stackalloc int[8];
        Av1MotionVector actual = Av1IntraBlockCopy.FindReference(
            picture,
            macroBlock,
            currentPosition,
            Av1BlockSize.Block16x16,
            Av1PartitionType.None,
            candidates,
            weights);

        Assert.Equal(displacement, actual);
    }

    /// <summary>
    /// Verifies that the tile writer derives the encoder reference and emits the retained displacement.
    /// </summary>
    [Fact]
    public void TileWriterEmitsRetainedDisplacementAgainstDerivedReference()
    {
        const int Width = 640;
        const int Height = 256;
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader();
        ObuFrameHeader frameHeader = CreateFrameHeader();
        frameHeader.AllowScreenContentTools = true;
        frameHeader.AllowIntraBlockCopy = true;

        using Av1EncoderPictureBuffer buffer = new(
            Configuration.Default,
            sequenceHeader,
            frameHeader,
            Width,
            Height,
            disallow4x4AllFrames: true);

        Av1PictureControlSet picture = buffer.Picture;
        Point modeInfoPosition = new(80, 0);
        Av1MacroBlockModeInfo modeInfo = default;
        modeInfo.Block = new Av1EncoderBlockModeInfo
        {
            BlockSize = Av1BlockSize.Block16x16,
            PartitionType = Av1PartitionType.None,
            UseIntraBlockCopy = true
        };

        Av1MotionVector displacement = new(0, -3072);
        picture.MapModeInfoBlock(modeInfoPosition, modeInfo.Block.BlockSize);
        picture.SetDisplacementVector(modeInfoPosition, displacement);
        Av1TileInfo tileInfo = new(0, 0, frameHeader);
        Av1MacroBlockD macroBlock = new() { Tile = tileInfo };
        Av1TileWriter.SetModeInfoRowAndColumn(
            picture,
            macroBlock,
            tileInfo,
            modeInfoPosition,
            modeInfo.Block.BlockSize,
            picture.ModeInfoStride,
            picture.Parent.Common.ModeInfoRowCount,
            picture.Parent.Common.ModeInfoColumnCount);

        using Av1SymbolEncoder writer = new(Configuration.Default, 64, 0, updateCdf: true);
        Av1TileWriter.WriteIntraBlockCopyInfo(
            picture,
            writer,
            macroBlock,
            modeInfoPosition,
            modeInfo);

        using var encoded = writer.Exit();
        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.GetSpan(), 0);
        Assert.True(decoder.ReadUseIntraBlockCopy());
        Assert.Equal(
            displacement,
            decoder.ReadDisplacementVector(new Av1MotionVector(0, -2560)));
    }

    /// <summary>
    /// Verifies tile bounds, whole-sample precision, the four-block delay, and wavefront ordering.
    /// </summary>
    [Fact]
    public void IsValidEnforcesIntraBlockCopySourceRestrictions()
    {
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader();
        using Av1FrameInfo frameInfo = new(sequenceHeader);
        Av1SuperblockInfo superblockInfo = frameInfo.GetSuperblock(new Point(8, 2));
        Av1BlockModeInfo modeInfo = new(Av1BlockSize.Block16x16, Point.Empty);
        Av1PartitionInfo partitionInfo = new(modeInfo, superblockInfo, true, Av1PartitionType.None)
        {
            ColumnIndex = 128,
            RowIndex = 32,
        };

        Av1TileInfo tileInfo = CreateTileInfo();

        // A source five 64-sample columns earlier satisfies both the four-column delay and same-row wavefront limit.
        Assert.True(Av1IntraBlockCopy.IsValid(new Av1MotionVector(0, -2560), ref partitionInfo, tileInfo, sequenceHeader));

        // Moving the source one 64-sample column to the right reaches the forbidden delay boundary exactly.
        Assert.False(Av1IntraBlockCopy.IsValid(new Av1MotionVector(0, -2048), ref partitionInfo, tileInfo, sequenceHeader));
        Assert.False(Av1IntraBlockCopy.IsValid(new Av1MotionVector(0, -2559), ref partitionInfo, tileInfo, sequenceHeader));
        Assert.False(Av1IntraBlockCopy.IsValid(new Av1MotionVector(0, -4608), ref partitionInfo, tileInfo, sequenceHeader));
        Assert.False(Av1IntraBlockCopy.IsValid(new Av1MotionVector(512, -2560), ref partitionInfo, tileInfo, sequenceHeader));
    }

    /// <summary>
    /// Verifies exact hash matches at unaligned origins in both normative search regions.
    /// </summary>
    [Fact]
    public void SearchIndexFindsUnalignedAboveAndLeftMatches()
    {
        const int Width = 640;
        const int Height = 256;
        const int QIndex = 23;
        Point blockOrigin = new(512, 128);
        Point aboveOrigin = new(515, 57);
        Point leftOrigin = new(191, 131);
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader();
        ObuFrameHeader frameHeader = CreateFrameHeader();
        frameHeader.AllowScreenContentTools = true;
        frameHeader.AllowIntraBlockCopy = true;

        using Av1EncoderPictureBuffer pictureBuffer = new(
            Configuration.Default,
            sequenceHeader,
            frameHeader,
            Width,
            Height,
            disallow4x4AllFrames: true);

        using Av1EncoderFrameBuffer<byte> source = new(
            Configuration.Default,
            Width,
            Height,
            8,
            Av1ColorFormat.Yuv400,
            0,
            0);

        using Av1EncoderFrameBuffer<byte> reconstruction = new(
            Configuration.Default,
            Width,
            Height,
            8,
            Av1ColorFormat.Yuv400,
            0,
            0);

        Buffer2DRegion<byte> sourceLuma = source.Frame.View.GetPlane(Av1Plane.Y);
        Buffer2DRegion<byte> reconstructionLuma = reconstruction.Frame.View.GetPlane(Av1Plane.Y);
        uint randomState = 0x8F3A21C5;
        for (int row = 0; row < Height; row++)
        {
            Span<byte> sourceRow = sourceLuma.DangerousGetRowSpan(row);
            reconstructionLuma.DangerousGetRowSpan(row).Clear();
            for (int column = 0; column < Width; column++)
            {
                randomState = unchecked((randomState * 1_664_525) + 1_013_904_223);
                sourceRow[column] = (byte)(randomState >> 24);
            }
        }

        for (int row = 0; row < 8; row++)
        {
            ReadOnlySpan<byte> blockRow = sourceLuma.DangerousGetRowSpan(blockOrigin.Y + row).Slice(blockOrigin.X, 8);
            blockRow.CopyTo(sourceLuma.DangerousGetRowSpan(aboveOrigin.Y + row)[aboveOrigin.X..]);
            blockRow.CopyTo(sourceLuma.DangerousGetRowSpan(leftOrigin.Y + row)[leftOrigin.X..]);
            blockRow.CopyTo(reconstructionLuma.DangerousGetRowSpan(aboveOrigin.Y + row)[aboveOrigin.X..]);
            blockRow.CopyTo(reconstructionLuma.DangerousGetRowSpan(leftOrigin.Y + row)[leftOrigin.X..]);
        }

        Av1PictureControlSet picture = pictureBuffer.Picture;
        picture.IntraBlockCopySearch.Initialize<byte, Av1IntraSuperblockEncoder.ByteOperator>(sourceLuma);
        using Av1SymbolEncoder writer = new(Configuration.Default, 64, QIndex, updateCdf: true);
        Span<Av1MotionVector> candidates = stackalloc Av1MotionVector[2];
        Av1MotionVector reference = new(0, -2560);
        int candidateCount = picture.IntraBlockCopySearch.FindCandidates<byte, Av1IntraSuperblockEncoder.ByteOperator>(
            source.Frame.CodedView.GetPlane(Av1Plane.Y),
            reconstruction.Frame.CodedView.GetPlane(Av1Plane.Y),
            blockOrigin,
            new Av1TileInfo(0, 0, frameHeader),
            sequenceHeader,
            writer,
            reference,
            Av1RateDistortion.GetKeyFrameRateMultiplier(QIndex, Av1BitDepth.EightBit),
            candidates);

        Assert.Equal(2, candidateCount);
        Assert.Equal(new Av1MotionVector(-568, 24), candidates[0]);
        Assert.Equal(new Av1MotionVector(24, -2568), candidates[1]);
    }

    /// <summary>
    /// Verifies that NSTEP pixel search reaches an unaligned reconstructed match which has no exact source hash match.
    /// </summary>
    [Fact]
    public void PixelSearchFindsUnalignedNonHashMatch()
    {
        const int Width = 640;
        const int Height = 256;
        const int QIndex = 23;
        Point blockOrigin = new(0, 128);
        Point predictionOrigin = new(15, 80);
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader();
        ObuFrameHeader frameHeader = CreateFrameHeader();
        frameHeader.AllowScreenContentTools = true;
        frameHeader.AllowIntraBlockCopy = true;

        using Av1EncoderPictureBuffer pictureBuffer = new(
            Configuration.Default,
            sequenceHeader,
            frameHeader,
            Width,
            Height,
            disallow4x4AllFrames: true);

        using Av1EncoderFrameBuffer<byte> source = new(
            Configuration.Default,
            Width,
            Height,
            8,
            Av1ColorFormat.Yuv400,
            0,
            0);

        using Av1EncoderFrameBuffer<byte> reconstruction = new(
            Configuration.Default,
            Width,
            Height,
            8,
            Av1ColorFormat.Yuv400,
            0,
            0);

        Buffer2DRegion<byte> sourceLuma = source.Frame.View.GetPlane(Av1Plane.Y);
        Buffer2DRegion<byte> reconstructionLuma = reconstruction.Frame.View.GetPlane(Av1Plane.Y);
        for (int row = 0; row < Height; row++)
        {
            sourceLuma.DangerousGetRowSpan(row).Clear();
            reconstructionLuma.DangerousGetRowSpan(row).Clear();
        }

        for (int row = 0; row < 8; row++)
        {
            byte value = (byte)(100 + (row * 10));
            sourceLuma.DangerousGetRowSpan(blockOrigin.Y + row).Slice(blockOrigin.X, 8).Fill(value);
            reconstructionLuma.DangerousGetRowSpan(predictionOrigin.Y + row).Slice(predictionOrigin.X, 8).Fill(value);
        }

        Buffer2DRegion<byte> codedSourceLuma = source.Frame.CodedView.GetPlane(Av1Plane.Y);
        Buffer2DRegion<byte> codedReconstructionLuma = reconstruction.Frame.CodedView.GetPlane(Av1Plane.Y);
        Assert.False(Av1IntraSuperblockEncoder.ByteOperator.BlocksEqual(codedSourceLuma, blockOrigin, predictionOrigin));
        Assert.Equal(
            0,
            Av1IntraSuperblockEncoder.ByteOperator.GetSumOfAbsoluteDifferences(
                codedSourceLuma,
                blockOrigin,
                codedReconstructionLuma,
                predictionOrigin));

        Assert.Equal(
            8_640,
            Av1IntraSuperblockEncoder.ByteOperator.GetSumOfAbsoluteDifferences(
                codedSourceLuma,
                blockOrigin,
                codedReconstructionLuma,
                new Point(15, 120)));

        using Av1SymbolEncoder writer = new(Configuration.Default, 64, QIndex, updateCdf: true);
        Span<Av1MotionVector> candidates = stackalloc Av1MotionVector[2];
        int candidateCount = pictureBuffer.Picture.IntraBlockCopySearch
            .FindPixelCandidates<byte, Av1IntraSuperblockEncoder.ByteOperator>(
                codedSourceLuma,
                codedReconstructionLuma,
                blockOrigin,
                new Av1TileInfo(0, 0, frameHeader),
                sequenceHeader,
                writer,
                new Av1MotionVector(-64, 120),
                QIndex,
                Av1RateDistortion.GetKeyFrameRateMultiplier(QIndex, Av1BitDepth.EightBit),
                candidates);

        Assert.Equal(1, candidateCount);
        Assert.Equal(-384, candidates[0].Row);
        Assert.Equal(120, candidates[0].Column);
    }

    /// <summary>
    /// Verifies that the exhaustive mesh recovers an exact match outside every centered NSTEP search site.
    /// </summary>
    [Fact]
    public void PixelSearchFallsBackToExhaustiveMesh()
    {
        const int Width = 640;
        const int Height = 256;
        const int QIndex = 23;
        Point blockOrigin = new(0, 128);
        Point predictionOrigin = new(256, 8);
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader();
        ObuFrameHeader frameHeader = CreateFrameHeader();
        frameHeader.AllowScreenContentTools = true;
        frameHeader.AllowIntraBlockCopy = true;

        using Av1EncoderPictureBuffer pictureBuffer = new(
            Configuration.Default,
            sequenceHeader,
            frameHeader,
            Width,
            Height,
            disallow4x4AllFrames: true);

        using Av1EncoderFrameBuffer<byte> source = new(
            Configuration.Default,
            Width,
            Height,
            8,
            Av1ColorFormat.Yuv400,
            0,
            0);

        using Av1EncoderFrameBuffer<byte> reconstruction = new(
            Configuration.Default,
            Width,
            Height,
            8,
            Av1ColorFormat.Yuv400,
            0,
            0);

        Buffer2DRegion<byte> sourceLuma = source.Frame.View.GetPlane(Av1Plane.Y);
        Buffer2DRegion<byte> reconstructionLuma = reconstruction.Frame.View.GetPlane(Av1Plane.Y);
        for (int row = 0; row < Height; row++)
        {
            sourceLuma.DangerousGetRowSpan(row).Clear();
            reconstructionLuma.DangerousGetRowSpan(row).Clear();
        }

        for (int row = 0; row < 8; row++)
        {
            Span<byte> sourceRow = sourceLuma.DangerousGetRowSpan(blockOrigin.Y + row).Slice(blockOrigin.X, 8);
            Span<byte> predictionRow = reconstructionLuma.DangerousGetRowSpan(predictionOrigin.Y + row).Slice(predictionOrigin.X, 8);
            for (int column = 0; column < 8; column++)
            {
                byte value = (byte)((row + column) % 2 == 0 ? 255 : 0);
                sourceRow[column] = value;
                predictionRow[column] = value;
            }
        }

        Buffer2DRegion<byte> codedSourceLuma = source.Frame.CodedView.GetPlane(Av1Plane.Y);
        Buffer2DRegion<byte> codedReconstructionLuma = reconstruction.Frame.CodedView.GetPlane(Av1Plane.Y);
        using Av1SymbolEncoder writer = new(Configuration.Default, 64, QIndex, updateCdf: true);
        Span<Av1MotionVector> candidates = stackalloc Av1MotionVector[2];
        int candidateCount = pictureBuffer.Picture.IntraBlockCopySearch
            .FindPixelCandidates<byte, Av1IntraSuperblockEncoder.ByteOperator>(
                codedSourceLuma,
                codedReconstructionLuma,
                blockOrigin,
                new Av1TileInfo(0, 0, frameHeader),
                sequenceHeader,
                writer,
                new Av1MotionVector(-64, 0),
                QIndex,
                Av1RateDistortion.GetKeyFrameRateMultiplier(QIndex, Av1BitDepth.EightBit),
                candidates);

        Assert.Equal(1, candidateCount);
        Assert.Equal(-960, candidates[0].Row);
        Assert.Equal(2048, candidates[0].Column);
    }

    /// <summary>
    /// Verifies high-bit-depth SIMD variance normalization against the eight-bit search domain.
    /// </summary>
    [Fact]
    public void SearchVarianceMatchesTwelveBitReference()
    {
        const int Width = 11;
        using Av1EncoderFrameBuffer<ushort> source = new(
            Configuration.Default,
            Width,
            8,
            12,
            Av1ColorFormat.Yuv400,
            0,
            0);

        using Av1EncoderFrameBuffer<ushort> reconstruction = new(
            Configuration.Default,
            Width,
            8,
            12,
            Av1ColorFormat.Yuv400,
            0,
            0);

        Buffer2DRegion<ushort> sourceLuma = source.Frame.View.GetPlane(Av1Plane.Y);
        Buffer2DRegion<ushort> reconstructionLuma = reconstruction.Frame.View.GetPlane(Av1Plane.Y);
        for (int row = 0; row < 8; row++)
        {
            Span<ushort> sourceRow = sourceLuma.DangerousGetRowSpan(row);
            Span<ushort> reconstructionRow = reconstructionLuma.DangerousGetRowSpan(row);
            for (int column = 0; column < 8; column++)
            {
                sourceRow[column] = 1000;
                reconstructionRow[column] = (ushort)(1000 + (((row * 8) + column) % 2 == 0 ? 17 : 33));
            }
        }

        int sumOfAbsoluteDifferences = Av1IntraSuperblockEncoder.UInt16Operator.GetSumOfAbsoluteDifferences(
            sourceLuma,
            Point.Empty,
            reconstructionLuma,
            Point.Empty);

        int variance = Av1IntraSuperblockEncoder.UInt16Operator.GetVariance(
            sourceLuma,
            Point.Empty,
            reconstructionLuma,
            Point.Empty,
            Av1BitDepth.TwelveBit);

        Span<int> fourSumsOfAbsoluteDifferences = stackalloc int[4];
        Av1IntraSuperblockEncoder.UInt16Operator.GetFourSumsOfAbsoluteDifferences(
            sourceLuma,
            Point.Empty,
            reconstructionLuma,
            Point.Empty,
            fourSumsOfAbsoluteDifferences);

        for (int candidate = 0; candidate < fourSumsOfAbsoluteDifferences.Length; candidate++)
        {
            int expected = Av1IntraSuperblockEncoder.UInt16Operator.GetSumOfAbsoluteDifferences(
                sourceLuma,
                Point.Empty,
                reconstructionLuma,
                new Point(candidate, 0));

            Assert.Equal(expected, fourSumsOfAbsoluteDifferences[candidate]);
        }

        Assert.Equal(1600, sumOfAbsoluteDifferences);
        Assert.Equal(16, variance);
    }

    /// <summary>
    /// Creates the 640-by-256, 4:2:0 sequence geometry shared by the displacement tests.
    /// </summary>
    private static ObuSequenceHeader CreateSequenceHeader()
        => new()
        {
            MaxFrameWidth = 640,
            MaxFrameHeight = 256,
            Use128x128Superblock = false,
            ColorConfig = new ObuColorConfig
            {
                IsMonochrome = false,
                SubSamplingX = true,
                SubSamplingY = true,
                BitDepth = Av1BitDepth.EightBit,
            },
        };

    /// <summary>
    /// Creates one tile covering the complete test frame.
    /// </summary>
    private static Av1TileInfo CreateTileInfo()
        => new(0, 0, CreateFrameHeader());

    /// <summary>
    /// Creates the frame and tile geometry shared by reference and validity tests.
    /// </summary>
    private static ObuFrameHeader CreateFrameHeader()
    {
        ObuTileGroupHeader tilesInfo = new()
        {
            TileColumnCount = 1,
            TileRowCount = 1,
        };

        tilesInfo.TileColumnStartModeInfo[1] = 160;
        tilesInfo.TileRowStartModeInfo[1] = 64;

        return new ObuFrameHeader
        {
            ModeInfoColumnCount = 160,
            ModeInfoRowCount = 64,
            TilesInfo = tilesInfo,
        };
    }
}
