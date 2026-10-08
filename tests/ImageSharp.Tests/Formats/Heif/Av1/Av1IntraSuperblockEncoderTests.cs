// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Heif;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Color;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Components;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies that the production decoder reproduces the retained, in-loop filtered reconstruction of the production
/// tile encoder exactly.
/// </summary>
[Trait("Format", "Avif")]
public class Av1IntraSuperblockEncoderTests
{
    [Theory]
    [InlineData((int)Av1ColorFormat.Yuv420)]
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
    [InlineData((int)Av1ColorFormat.Yuv444, 10)]
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
    [InlineData((int)Av1ColorFormat.Yuv420, HeifEncodingSpeed.Level6)]
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
    [InlineData((int)Av1ColorFormat.Yuv422, 12, HeifEncodingSpeed.Level9)]
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
    [InlineData((int)Av1ColorFormat.Yuv420)]
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
            Av1PlaneRegion<TSample> plane = source.Frame.View.GetPlane((Av1Plane)planeIndex);
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
        using Av1SymbolEncoder symbolEncoder = CreateTileSymbolEncoder(picture.Picture);

        // Reserve the final pass's restoration decisions, but measure the baseline before any in-loop filtering.
        template.Sequence.SequenceHeader.EnableRestoration = false;
        _ = createWriter(symbolEncoder, source.Frame, reconstruction.Frame, picture.Picture, coefficients, superblockWorkspace, blockWorkspace);
        for (int planeIndex = 0; planeIndex < planeCount; planeIndex++)
        {
            Av1PlaneRegion<TSample> plane = reconstruction.Frame.View.GetPlane((Av1Plane)planeIndex);
            for (int y = 0; y < plane.Height; y++)
            {
                plane.GetRowSpan(y).CopyTo(unfiltered[planeIndex].AsSpan(y * plane.Width, plane.Width));
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
            Av1PlaneRegion<TSample> retained = reconstruction.Frame.View.GetPlane(plane);
            int subX = plane == Av1Plane.Y ? 0 : reconstruction.Frame.ChromaSubsamplingX;
            int subY = plane == Av1Plane.Y ? 0 : reconstruction.Frame.ChromaSubsamplingY;
            Av1PlaneRegion<byte> decoded = decodedFrame.DeriveBlockPointer(plane, subX, subY);
            for (int y = 0; y < retained.Height; y++)
            {
                ReadOnlySpan<TSample> row = retained.GetRowSpan(y);
                ReadOnlySpan<TSample> decodedRow = MemoryMarshal.Cast<byte, TSample>(decoded.GetRowSpan(y));
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
            TileDataLengths = Memory<int>.Empty
        };
    }

    /// <summary>
    /// Creates the operation owner for a production tile's entropy state and tile buffers.
    /// </summary>
    /// <param name="picture">The picture supplying quantization and CDF-update settings.</param>
    /// <returns>The symbol encoder that must remain alive while the tile output is consumed.</returns>
    private static Av1SymbolEncoder CreateTileSymbolEncoder(Av1PictureControlSet picture)
    {
        ObuFrameHeader frameHeader = picture.Parent.FrameHeader;
        return new Av1SymbolEncoder(
            Configuration.Default,
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
}
