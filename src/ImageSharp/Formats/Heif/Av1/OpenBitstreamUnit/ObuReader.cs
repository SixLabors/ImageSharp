// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantification;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

/// <summary>
/// Reader for Open Bitstream Units (OBU's).
/// </summary>
internal class ObuReader
{
    /// <summary>
    /// Maximum value used for loop filtering.
    /// </summary>
    private const int MaxLoopFilter = 63;

    /// <summary>
    /// Number of segments allowed in segmentation map.
    /// </summary>
    private const int MaxSegments = 0;

    /// <summary>
    /// Number of segment features.
    /// </summary>
    private const int SegLvlMax = 8;

    /// <summary>
    /// Index for reference frame segment feature.
    /// </summary>
    private const int SegLvlRefFrame = 5;

    private const int PrimaryRefNone = 7;

    private static readonly int[] SegmentationFeatureBits = [8, 6, 6, 6, 6, 3, 0, 0];

    private static readonly int[] SegmentationFeatureSigned = [1, 1, 1, 1, 1, 0, 0, 0];

    private static readonly int[] SegmentationFeatureMax = [255, MaxLoopFilter, MaxLoopFilter, MaxLoopFilter, MaxLoopFilter, 7, 0, 0];

    private IAv1TileReader? decoder;

    public ObuSequenceHeader? SequenceHeader { get; set; }

    public ObuFrameHeader? FrameHeader { get; set; }

    /// <summary>
    /// Decode all OBU's in a frame.
    /// </summary>
    public void ReadAll(ref Av1BitStreamReader reader, int dataSize, Func<IAv1TileReader> creator, bool isAnnexB = false)
    {
        bool seenFrameHeader = false;
        bool frameDecodingFinished = false;
        while (!frameDecodingFinished)
        {
            int lengthSize = 0;
            int payloadSize = 0;
            if (isAnnexB)
            {
                ReadObuSize(ref reader, out payloadSize, out lengthSize);
            }

            ObuHeader header = ReadObuHeaderSize(ref reader, out lengthSize);
            if (isAnnexB)
            {
                header.PayloadSize -= header.Size;
                dataSize -= lengthSize;
                lengthSize = 0;
            }

            payloadSize = header.PayloadSize;
            dataSize -= header.Size + lengthSize;
            if (isAnnexB && dataSize < payloadSize)
            {
                throw new InvalidImageContentException("Corrupt frame");
            }

            switch (header.Type)
            {
                case ObuType.SequenceHeader:
                    this.SequenceHeader = new();
                    ReadSequenceHeader(ref reader, this.SequenceHeader);
                    if (this.SequenceHeader.ColorConfig.BitDepth == Av1BitDepth.TwelveBit)
                    {
                        // TODO: Initialize 12 bit predictors
                    }

                    break;
                case ObuType.FrameHeader:
                case ObuType.RedundantFrameHeader:
                case ObuType.Frame:
                    if (header.Type != ObuType.Frame)
                    {
                        // Nothing to do here.
                    }
                    else if (header.Type != ObuType.FrameHeader)
                    {
                        Guard.IsFalse(seenFrameHeader, nameof(seenFrameHeader), "Frame header expected");
                    }
                    else
                    {
                        Guard.IsTrue(seenFrameHeader, nameof(seenFrameHeader), "Already decoded a frame header");
                    }

                    if (!seenFrameHeader)
                    {
                        seenFrameHeader = true;
                        this.FrameHeader = new();
                        this.ReadFrameHeader(ref reader, header, header.Type != ObuType.Frame);
                    }

                    if (header.Type != ObuType.Frame)
                    {
                        break; // For OBU_TILE_GROUP comes under OBU_FRAME
                    }

                    goto TILE_GROUP;
                case ObuType.TileGroup:
                    TILE_GROUP:
                    if (!seenFrameHeader)
                    {
                        throw new InvalidImageContentException("Corrupt frame");
                    }

                    this.decoder ??= creator();
                    this.ReadTileGroup(ref reader, this.decoder, header, out frameDecodingFinished);
                    if (frameDecodingFinished)
                    {
                        seenFrameHeader = false;
                    }

                    break;
                case ObuType.TemporalDelimiter:
                    // 5.6. Temporal delimiter obu syntax.
                    seenFrameHeader = false;
                    break;
                default:
                    // Ignore unknown OBU types.
                    // throw new InvalidImageContentException($"Unknown OBU header found: {header.Type.ToString()}");
                    break;
            }

            dataSize -= payloadSize;
            if (dataSize <= 0)
            {
                frameDecodingFinished = true;
            }
        }
    }

    /// <summary>
    /// 5.3.2. OBU header syntax.
    /// </summary>
    private static ObuHeader ReadObuHeader(ref Av1BitStreamReader reader)
    {
        ObuHeader header = new();
        if (reader.ReadBoolean())
        {
            throw new ImageFormatException("Forbidden bit in header should be unset.");
        }

        header.Size = 1;
        header.Type = (ObuType)reader.ReadLiteral(4);
        header.HasExtension = reader.ReadBoolean();
        header.HasSize = reader.ReadBoolean();
        if (reader.ReadBoolean())
        {
            throw new ImageFormatException("Reserved bit in header should be unset.");
        }

        if (header.HasExtension)
        {
            header.Size++;
            header.TemporalId = (int)reader.ReadLiteral(3);
            header.SpatialId = (int)reader.ReadLiteral(2);
            if (reader.ReadLiteral(3) != 0u)
            {
                throw new ImageFormatException("Reserved bits in header extension should be unset.");
            }
        }
        else
        {
            header.SpatialId = 0;
            header.TemporalId = 0;
        }

        return header;
    }

    private static void ReadObuSize(ref Av1BitStreamReader reader, out int obuSize, out int lengthSize)
    {
        ulong rawSize = reader.ReadLittleEndianBytes128(out lengthSize);
        if (rawSize > uint.MaxValue)
        {
            throw new ImageFormatException("OBU block too large.");
        }

        obuSize = (int)rawSize;
    }

    /// <summary>
    /// Read OBU header and size.
    /// </summary>
    private static ObuHeader ReadObuHeaderSize(ref Av1BitStreamReader reader, out int lengthSize)
    {
        ObuHeader header = ReadObuHeader(ref reader);
        lengthSize = 0;
        if (header.HasSize)
        {
            ReadObuSize(ref reader, out int payloadSize, out lengthSize);
            header.PayloadSize = payloadSize;
        }

        return header;
    }

    /// <summary>
    /// Check that the trailing bits start with a 1 and end with 0s.
    /// </summary>
    /// <remarks>Consumes a byte, if already byte aligned before the check.</remarks>
    private static void ReadTrailingBits(ref Av1BitStreamReader reader)
    {
        int bitsBeforeAlignment = 8 - (reader.BitPosition & 0x7);
        uint trailing = reader.ReadLiteral(bitsBeforeAlignment);
        if (trailing != (1U << (bitsBeforeAlignment - 1)))
        {
            throw new ImageFormatException("Trailing bits not properly formatted.");
        }
    }

    /// <summary>
    /// 5.3.5. Byte alignment syntax.
    /// </summary>
    private static void AlignToByteBoundary(ref Av1BitStreamReader reader)
    {
        while ((reader.BitPosition & 0x7) > 0)
        {
            if (reader.ReadBoolean())
            {
                throw new ImageFormatException("Incorrect byte alignment padding bits.");
            }
        }
    }

    /// <summary>
    /// 5.9.9. Compute image size function.
    /// </summary>
    /// <remarks>SVT: compute_image_size</remarks>
    private void ComputeImageSize(ObuSequenceHeader sequenceHeader)
    {
        ObuFrameHeader frameHeader = this.FrameHeader!;
        frameHeader.ModeInfoColumnCount = 2 * ((frameHeader.FrameSize.FrameWidth + 7) >> 3);
        frameHeader.ModeInfoRowCount = 2 * ((frameHeader.FrameSize.FrameHeight + 7) >> 3);
        frameHeader.ModeInfoStride = Av1Math.AlignPowerOf2(sequenceHeader.MaxFrameWidth, Av1Constants.MaxSuperBlockSizeLog2) >> Av1Constants.ModeInfoSizeLog2;
    }

    /// <summary>
    /// 5.5.1. General sequence header OBU syntax.
    /// </summary>
    internal static void ReadSequenceHeader(ref Av1BitStreamReader reader, ObuSequenceHeader sequenceHeader)
    {
        sequenceHeader.SequenceProfile = (ObuSequenceProfile)reader.ReadLiteral(3);
        if (sequenceHeader.SequenceProfile > Av1Constants.MaxSequenceProfile)
        {
            throw new ImageFormatException("Unknown sequence profile.");
        }

        sequenceHeader.IsStillPicture = reader.ReadBoolean();
        sequenceHeader.IsReducedStillPictureHeader = reader.ReadBoolean();
        if (sequenceHeader.IsReducedStillPictureHeader)
        {
            sequenceHeader.TimingInfo = null;
            sequenceHeader.DecoderModelInfoPresentFlag = false;
            sequenceHeader.InitialDisplayDelayPresentFlag = false;
            sequenceHeader.OperatingPoint = new ObuOperatingPoint[1];
            ObuOperatingPoint operatingPoint = new();
            sequenceHeader.OperatingPoint[0] = operatingPoint;
            operatingPoint.OperatorIndex = 0;
            operatingPoint.SequenceLevelIndex = (int)reader.ReadLiteral(Av1Constants.LevelBits);
            if (!IsValidSequenceLevel(sequenceHeader.OperatingPoint[0].SequenceLevelIndex))
            {
                throw new ImageFormatException("Invalid sequence level.");
            }

            operatingPoint.SequenceTier = 0;
            operatingPoint.IsDecoderModelInfoPresent = false;
            operatingPoint.IsInitialDisplayDelayPresent = false;
        }
        else
        {
            sequenceHeader.TimingInfoPresentFlag = reader.ReadBoolean();
            if (sequenceHeader.TimingInfoPresentFlag)
            {
                ReadTimingInfo(ref reader, sequenceHeader);
                sequenceHeader.DecoderModelInfoPresentFlag = reader.ReadBoolean();
                if (sequenceHeader.DecoderModelInfoPresentFlag)
                {
                    ReadDecoderModelInfo(ref reader, sequenceHeader);
                }
                else
                {
                    sequenceHeader.DecoderModelInfoPresentFlag = false;
                }
            }

            sequenceHeader.InitialDisplayDelayPresentFlag = reader.ReadBoolean();
            uint operatingPointsCnt = reader.ReadLiteral(5) + 1;
            sequenceHeader.OperatingPoint = new ObuOperatingPoint[operatingPointsCnt];
            for (int i = 0; i < operatingPointsCnt; i++)
            {
                sequenceHeader.OperatingPoint[i] = new ObuOperatingPoint
                {
                    Idc = reader.ReadLiteral(12),
                    SequenceLevelIndex = (int)reader.ReadLiteral(5)
                };
                if (sequenceHeader.OperatingPoint[i].SequenceLevelIndex > 7)
                {
                    sequenceHeader.OperatingPoint[i].SequenceTier = (int)reader.ReadLiteral(1);
                }
                else
                {
                    sequenceHeader.OperatingPoint[i].SequenceTier = 0;
                }

                if (sequenceHeader.DecoderModelInfoPresentFlag)
                {
                    sequenceHeader.OperatingPoint[i].IsDecoderModelInfoPresent = reader.ReadBoolean();
                    if (sequenceHeader.OperatingPoint[i].IsDecoderModelInfoPresent)
                    {
                        // TODO: operating_parameters_info( i )
                    }
                }
                else
                {
                    sequenceHeader.OperatingPoint[i].IsDecoderModelInfoPresent = false;
                }

                if (sequenceHeader.InitialDisplayDelayPresentFlag)
                {
                    sequenceHeader.OperatingPoint[i].IsInitialDisplayDelayPresent = reader.ReadBoolean();
                    if (sequenceHeader.OperatingPoint[i].IsInitialDisplayDelayPresent)
                    {
                        sequenceHeader.OperatingPoint[i].InitialDisplayDelay = reader.ReadLiteral(4) + 1;
                    }
                }
            }
        }

        // Video related flags removed

        // SVT-TODO: int operatingPoint = this.ChooseOperatingPoint();
        // sequenceHeader.OperatingPointIndex = (int)operatingPointIndices[operatingPoint];
        sequenceHeader.FrameWidthBits = (int)reader.ReadLiteral(4) + 1;
        sequenceHeader.FrameHeightBits = (int)reader.ReadLiteral(4) + 1;
        sequenceHeader.MaxFrameWidth = (int)reader.ReadLiteral(sequenceHeader.FrameWidthBits) + 1;
        sequenceHeader.MaxFrameHeight = (int)reader.ReadLiteral(sequenceHeader.FrameHeightBits) + 1;
        if (sequenceHeader.IsReducedStillPictureHeader)
        {
            sequenceHeader.IsFrameIdNumbersPresent = false;
        }
        else
        {
            sequenceHeader.IsFrameIdNumbersPresent = reader.ReadBoolean();
        }

        if (sequenceHeader.IsFrameIdNumbersPresent)
        {
            sequenceHeader.DeltaFrameIdLength = (int)reader.ReadLiteral(4) + 2;
            sequenceHeader.AdditionalFrameIdLength = reader.ReadLiteral(3) + 1;
        }

        // Video related flags removed
        sequenceHeader.Use128x128Superblock = reader.ReadBoolean();
        sequenceHeader.EnableFilterIntra = reader.ReadBoolean();
        sequenceHeader.EnableIntraEdgeFilter = reader.ReadBoolean();

        if (sequenceHeader.IsReducedStillPictureHeader)
        {
            sequenceHeader.EnableInterIntraCompound = false;
            sequenceHeader.EnableMaskedCompound = false;
            sequenceHeader.EnableWarpedMotion = false;
            sequenceHeader.EnableDualFilter = false;
            sequenceHeader.OrderHintInfo.EnableJointCompound = false;
            sequenceHeader.OrderHintInfo.EnableReferenceFrameMotionVectors = false;
            sequenceHeader.ForceScreenContentTools = 2; // SELECT_SCREEN_CONTENT_TOOLS
            sequenceHeader.ForceIntegerMotionVector = 2; // SELECT_INTEGER_MV
            sequenceHeader.OrderHintInfo.OrderHintBits = 0;
        }
        else
        {
            sequenceHeader.EnableInterIntraCompound = reader.ReadBoolean();
            sequenceHeader.EnableMaskedCompound = reader.ReadBoolean();
            sequenceHeader.EnableWarpedMotion = reader.ReadBoolean();
            sequenceHeader.EnableDualFilter |= reader.ReadBoolean();
            sequenceHeader.EnableOrderHint = reader.ReadBoolean();
            if (sequenceHeader.EnableOrderHint)
            {
                sequenceHeader.OrderHintInfo.EnableJointCompound = reader.ReadBoolean();
                sequenceHeader.OrderHintInfo.EnableReferenceFrameMotionVectors = reader.ReadBoolean();
            }
            else
            {
                sequenceHeader.OrderHintInfo.EnableJointCompound = false;
                sequenceHeader.OrderHintInfo.EnableReferenceFrameMotionVectors = false;
            }

            bool seqChooseScreenContentTools = reader.ReadBoolean();
            if (seqChooseScreenContentTools)
            {
                sequenceHeader.ForceScreenContentTools = 2; // SELECT_SCREEN_CONTENT_TOOLS
            }
            else
            {
                sequenceHeader.ForceScreenContentTools = (int)reader.ReadLiteral(1);
            }

            if (sequenceHeader.ForceScreenContentTools > 0)
            {
                bool seqChooseIntegerMv = reader.ReadBoolean();
                if (seqChooseIntegerMv)
                {
                    sequenceHeader.ForceIntegerMotionVector = 2; // SELECT_INTEGER_MV
                }
                else
                {
                    sequenceHeader.ForceIntegerMotionVector = (int)reader.ReadLiteral(1);
                }
            }
            else
            {
                sequenceHeader.ForceIntegerMotionVector = 2; // SELECT_INTEGER_MV
            }

            if (sequenceHeader.EnableOrderHint)
            {
                sequenceHeader.OrderHintInfo.OrderHintBits = (int)reader.ReadLiteral(3) + 1;
            }
            else
            {
                sequenceHeader.OrderHintInfo.OrderHintBits = 0;
            }
        }

        // Video related flags removed
        sequenceHeader.EnableSuperResolution = reader.ReadBoolean();
        sequenceHeader.EnableCdef = reader.ReadBoolean();
        sequenceHeader.EnableRestoration = reader.ReadBoolean();
        sequenceHeader.ColorConfig = ReadColorConfig(ref reader, sequenceHeader);
        sequenceHeader.AreFilmGrainingParametersPresent = reader.ReadBoolean();
        ReadTrailingBits(ref reader);
    }

    /// <summary>
    /// 5.5.2. Color config syntax.
    /// </summary>
    private static ObuColorConfig ReadColorConfig(ref Av1BitStreamReader reader, ObuSequenceHeader sequenceHeader)
    {
        ObuColorConfig colorConfig = new();
        ReadBitDepth(ref reader, colorConfig, sequenceHeader);
        colorConfig.IsMonochrome = false;
        if (sequenceHeader.SequenceProfile != ObuSequenceProfile.High)
        {
            colorConfig.IsMonochrome = reader.ReadBoolean();
        }

        colorConfig.IsColorDescriptionPresent = reader.ReadBoolean();
        colorConfig.ColorPrimaries = ObuColorPrimaries.Unspecified;
        colorConfig.TransferCharacteristics = ObuTransferCharacteristics.Unspecified;
        colorConfig.MatrixCoefficients = ObuMatrixCoefficients.Unspecified;
        if (colorConfig.IsColorDescriptionPresent)
        {
            colorConfig.ColorPrimaries = (ObuColorPrimaries)reader.ReadLiteral(8);
            colorConfig.TransferCharacteristics = (ObuTransferCharacteristics)reader.ReadLiteral(8);
            colorConfig.MatrixCoefficients = (ObuMatrixCoefficients)reader.ReadLiteral(8);
        }

        colorConfig.ColorRange = false;
        colorConfig.SubSamplingX = false;
        colorConfig.SubSamplingY = false;
        colorConfig.ChromaSamplePosition = ObuChromoSamplePosition.Unknown;
        colorConfig.HasSeparateUvDelta = false;
        if (colorConfig.IsMonochrome)
        {
            colorConfig.ColorRange = reader.ReadBoolean();
            colorConfig.SubSamplingX = true;
            colorConfig.SubSamplingY = true;
            return colorConfig;
        }
        else if (
            colorConfig.ColorPrimaries == ObuColorPrimaries.Bt709 &&
            colorConfig.TransferCharacteristics == ObuTransferCharacteristics.Srgb &&
            colorConfig.MatrixCoefficients == ObuMatrixCoefficients.Identity)
        {
            colorConfig.ColorRange = true;
            colorConfig.SubSamplingX = false;
            colorConfig.SubSamplingY = false;
        }
        else
        {
            colorConfig.ColorRange = reader.ReadBoolean();
            switch (sequenceHeader.SequenceProfile)
            {
                case ObuSequenceProfile.Main:
                    colorConfig.SubSamplingX = true;
                    colorConfig.SubSamplingY = true;
                    break;
                case ObuSequenceProfile.High:
                    colorConfig.SubSamplingX = false;
                    colorConfig.SubSamplingY = false;
                    break;
                case ObuSequenceProfile.Professional:
                default:
                    if (colorConfig.BitDepth == Av1BitDepth.TwelveBit)
                    {
                        colorConfig.SubSamplingX = reader.ReadBoolean();
                        if (colorConfig.SubSamplingX)
                        {
                            colorConfig.SubSamplingY = reader.ReadBoolean();
                        }
                    }
                    else
                    {
                        colorConfig.SubSamplingX = true;
                        colorConfig.SubSamplingY = false;
                    }

                    break;
            }

            if (colorConfig.SubSamplingX && colorConfig.SubSamplingY)
            {
                colorConfig.ChromaSamplePosition = (ObuChromoSamplePosition)reader.ReadLiteral(2);
            }
        }

        colorConfig.HasSeparateUvDelta = reader.ReadBoolean();
        return colorConfig;
    }

    /// <summary>
    /// 5.5.4. Decoder model info syntax.
    /// </summary>
    private static void ReadDecoderModelInfo(ref Av1BitStreamReader reader, ObuSequenceHeader sequenceHeader) => sequenceHeader.DecoderModelInfo = new ObuDecoderModelInfo
    {
        BufferDelayLength = reader.ReadLiteral(5) + 1,
        NumUnitsInDecodingTick = reader.ReadLiteral(16),
        BufferRemovalTimeLength = reader.ReadLiteral(5) + 1,
        FramePresentationTimeLength = reader.ReadLiteral(5) + 1
    };

    /// <summary>
    /// 5.5.3. Timing info syntax.
    /// </summary>
    private static void ReadTimingInfo(ref Av1BitStreamReader reader, ObuSequenceHeader sequenceHeader)
    {
        sequenceHeader.TimingInfo = new ObuTimingInfo
        {
            NumUnitsInDisplayTick = reader.ReadLiteral(32),
            TimeScale = reader.ReadLiteral(32),
            EqualPictureInterval = reader.ReadBoolean()
        };

        if (sequenceHeader.TimingInfo.EqualPictureInterval)
        {
            sequenceHeader.TimingInfo.NumTicksPerPicture = reader.ReadUnsignedVariableLength() + 1;
        }
    }

    private static void ReadBitDepth(ref Av1BitStreamReader reader, ObuColorConfig colorConfig, ObuSequenceHeader sequenceHeader)
    {
        bool hasHighBitDepth = reader.ReadBoolean();
        if (sequenceHeader.SequenceProfile == ObuSequenceProfile.Professional && hasHighBitDepth)
        {
            colorConfig.BitDepth = reader.ReadBoolean() ? Av1BitDepth.TwelveBit : Av1BitDepth.TenBit;
        }
        else if (sequenceHeader.SequenceProfile <= ObuSequenceProfile.Professional)
        {
            colorConfig.BitDepth = hasHighBitDepth ? Av1BitDepth.TenBit : Av1BitDepth.EightBit;
        }
        else
        {
            colorConfig.BitDepth = Av1BitDepth.EightBit;
        }
    }

    /// <summary>
    /// 5.9.8. Superres params syntax.
    /// </summary>
    private void ReadSuperResolutionParameters(ref Av1BitStreamReader reader)
    {
        ObuSequenceHeader sequenceHeader = this.SequenceHeader!;
        ObuFrameHeader frameHeader = this.FrameHeader!;
        bool useSuperResolution = false;
        if (sequenceHeader.EnableSuperResolution)
        {
            useSuperResolution = reader.ReadBoolean();
        }

        if (useSuperResolution)
        {
            frameHeader.FrameSize.SuperResolutionDenominator = (int)reader.ReadLiteral(Av1Constants.SuperResolutionScaleBits) + Av1Constants.SuperResolutionScaleDenominatorMinimum;
        }
        else
        {
            frameHeader.FrameSize.SuperResolutionDenominator = Av1Constants.ScaleNumerator;
        }

        frameHeader.FrameSize.SuperResolutionUpscaledWidth = frameHeader.FrameSize.FrameWidth;
        frameHeader.FrameSize.FrameWidth =
            ((frameHeader.FrameSize.SuperResolutionUpscaledWidth * Av1Constants.ScaleNumerator) +
            (frameHeader.FrameSize.SuperResolutionDenominator / 2)) /
            frameHeader.FrameSize.SuperResolutionDenominator;

        /*
        if (frameHeader.FrameSize.SuperResolutionDenominator != Av1Constants.ScaleNumerator)
        {
            int manWidth = Math.Min(16, frameHeader.FrameSize.SuperResolutionUpscaledWidth);
            frameHeader.FrameSize.FrameWidth = Math.Max(manWidth, frameHeader.FrameSize.FrameWidth);
        }
        */
    }

    /// <summary>
    /// 5.9.6. Render size syntax.
    /// </summary>
    private void ReadRenderSize(ref Av1BitStreamReader reader)
    {
        ObuFrameHeader frameHeader = this.FrameHeader!;
        bool renderSizeAndFrameSizeDifferent = reader.ReadBoolean();
        if (renderSizeAndFrameSizeDifferent)
        {
            frameHeader.FrameSize.RenderWidth = (int)reader.ReadLiteral(16) + 1;
            frameHeader.FrameSize.RenderHeight = (int)reader.ReadLiteral(16) + 1;
        }
        else
        {
            frameHeader.FrameSize.RenderWidth = frameHeader.FrameSize.SuperResolutionUpscaledWidth;
            frameHeader.FrameSize.RenderHeight = frameHeader.FrameSize.FrameHeight;
        }
    }

    /// <summary>
    /// 5.9.5. Frame size syntax.
    /// </summary>
    private void ReadFrameSize(ref Av1BitStreamReader reader, bool frameSizeOverrideFlag)
    {
        ObuSequenceHeader sequenceHeader = this.SequenceHeader!;
        ObuFrameHeader frameHeader = this.FrameHeader!;
        if (frameSizeOverrideFlag)
        {
            frameHeader.FrameSize.FrameWidth = (int)reader.ReadLiteral(sequenceHeader.FrameWidthBits) + 1;
            frameHeader.FrameSize.FrameHeight = (int)reader.ReadLiteral(sequenceHeader.FrameHeightBits) + 1;
        }
        else
        {
            frameHeader.FrameSize.FrameWidth = sequenceHeader.MaxFrameWidth;
            frameHeader.FrameSize.FrameHeight = sequenceHeader.MaxFrameHeight;
        }

        this.ReadSuperResolutionParameters(ref reader);
        this.ComputeImageSize(sequenceHeader);
    }

    /// <summary>
    /// 5.9.15. Tile info syntax.
    /// </summary>
    private static ObuTileGroupHeader ReadTileInfo(ref Av1BitStreamReader reader, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameHeader)
    {
        ObuTileGroupHeader tileInfo = new();
        int superblockColumnCount;
        int superblockRowCount;
        int superblockSizeLog2 = sequenceHeader.SuperblockSizeLog2;
        int superblockShift = superblockSizeLog2 - Av1Constants.ModeInfoSizeLog2;
        superblockColumnCount = (frameHeader.ModeInfoColumnCount + sequenceHeader.SuperblockModeInfoSize - 1) >> superblockShift;
        superblockRowCount = (frameHeader.ModeInfoRowCount + sequenceHeader.SuperblockModeInfoSize - 1) >> superblockShift;

        int maxTileAreaOfSuperBlock = Av1Constants.MaxTileArea >> (superblockSizeLog2 << 1);

        tileInfo.MaxTileWidthSuperblock = Av1Constants.MaxTileWidth >> superblockSizeLog2;
        tileInfo.MaxTileHeightSuperblock = (Av1Constants.MaxTileArea / Av1Constants.MaxTileWidth) >> superblockSizeLog2;
        tileInfo.MinLog2TileColumnCount = TileLog2(tileInfo.MaxTileWidthSuperblock, superblockColumnCount);
        tileInfo.MaxLog2TileColumnCount = (int)Av1Math.CeilLog2((uint)Math.Min(superblockColumnCount, Av1Constants.MaxTileColumnCount));
        tileInfo.MaxLog2TileRowCount = (int)Av1Math.CeilLog2((uint)Math.Min(superblockRowCount, Av1Constants.MaxTileRowCount));
        tileInfo.MinLog2TileCount = Math.Max(tileInfo.MinLog2TileColumnCount, TileLog2(maxTileAreaOfSuperBlock, superblockColumnCount * superblockRowCount));
        tileInfo.HasUniformTileSpacing = reader.ReadBoolean();
        if (tileInfo.HasUniformTileSpacing)
        {
            tileInfo.TileColumnCountLog2 = tileInfo.MinLog2TileColumnCount;
            while (tileInfo.TileColumnCountLog2 < tileInfo.MaxLog2TileColumnCount)
            {
                if (reader.ReadBoolean())
                {
                    tileInfo.TileColumnCountLog2++;
                }
                else
                {
                    break;
                }
            }

            int tileWidthSuperblock = Av1Math.DivideLog2Ceiling(superblockColumnCount, tileInfo.TileColumnCountLog2);
            DebugGuard.MustBeLessThanOrEqualTo(tileWidthSuperblock, tileInfo.MaxTileWidthSuperblock, nameof(tileWidthSuperblock));
            int i = 0;
            tileInfo.TileColumnStartModeInfo = new int[superblockColumnCount + 1];
            for (int startSuperblock = 0; startSuperblock < superblockColumnCount; startSuperblock += tileWidthSuperblock)
            {
                tileInfo.TileColumnStartModeInfo[i] = startSuperblock << superblockShift;
                i++;
            }

            tileInfo.TileColumnStartModeInfo[i] = frameHeader.ModeInfoColumnCount;
            tileInfo.TileColumnCount = i;

            tileInfo.MinLog2TileRowCount = Math.Max(tileInfo.MinLog2TileCount - tileInfo.TileColumnCountLog2, 0);
            tileInfo.TileRowCountLog2 = tileInfo.MinLog2TileRowCount;
            while (tileInfo.TileRowCountLog2 < tileInfo.MaxLog2TileRowCount)
            {
                if (reader.ReadBoolean())
                {
                    tileInfo.TileRowCountLog2++;
                }
                else
                {
                    break;
                }
            }

            int tileHeightSuperblock = Av1Math.DivideLog2Ceiling(superblockRowCount, tileInfo.TileRowCountLog2);
            DebugGuard.MustBeLessThanOrEqualTo(tileHeightSuperblock, tileInfo.MaxTileHeightSuperblock, nameof(tileHeightSuperblock));
            i = 0;
            tileInfo.TileRowStartModeInfo = new int[superblockRowCount + 1];
            for (int startSuperblock = 0; startSuperblock < superblockRowCount; startSuperblock += tileHeightSuperblock)
            {
                tileInfo.TileRowStartModeInfo[i] = startSuperblock << superblockShift;
                i++;
            }

            tileInfo.TileRowStartModeInfo[i] = frameHeader.ModeInfoRowCount;
            tileInfo.TileRowCount = i;
        }
        else
        {
            uint widestTileSuperBlock = 0U;
            int startSuperBlock = 0;
            int i = 0;
            for (; startSuperBlock < superblockColumnCount; i++)
            {
                tileInfo.TileColumnStartModeInfo[i] = startSuperBlock << superblockShift;
                uint maxWidth = (uint)Math.Min(superblockColumnCount - startSuperBlock, tileInfo.MaxTileWidthSuperblock);
                uint widthInSuperBlocks = reader.ReadNonSymmetric(maxWidth) + 1;
                widestTileSuperBlock = Math.Max(widthInSuperBlocks, widestTileSuperBlock);
                startSuperBlock += (int)widthInSuperBlocks;
            }

            if (startSuperBlock != superblockColumnCount)
            {
                throw new ImageFormatException("Super block tiles width does not add up to total width.");
            }

            tileInfo.TileColumnStartModeInfo[i] = frameHeader.ModeInfoColumnCount;
            tileInfo.TileColumnCount = i;
            tileInfo.TileColumnCountLog2 = TileLog2(1, tileInfo.TileColumnCount);
            if (tileInfo.MinLog2TileCount > 0)
            {
                maxTileAreaOfSuperBlock = (superblockRowCount * superblockColumnCount) >> (tileInfo.MinLog2TileCount + 1);
            }
            else
            {
                maxTileAreaOfSuperBlock = superblockRowCount * superblockColumnCount;
            }

            DebugGuard.MustBeGreaterThan(widestTileSuperBlock, 0U, nameof(widestTileSuperBlock));
            tileInfo.MaxTileHeightSuperblock = Math.Max(maxTileAreaOfSuperBlock / (int)widestTileSuperBlock, 1);

            startSuperBlock = 0;
            for (i = 0; startSuperBlock < superblockRowCount; i++)
            {
                tileInfo.TileRowStartModeInfo[i] = startSuperBlock << superblockShift;
                uint maxHeight = (uint)Math.Min(superblockRowCount - startSuperBlock, tileInfo.MaxTileHeightSuperblock);
                uint heightInSuperBlocks = reader.ReadNonSymmetric(maxHeight) + 1;
                startSuperBlock += (int)heightInSuperBlocks;
            }

            if (startSuperBlock != superblockRowCount)
            {
                throw new ImageFormatException("Super block tiles height does not add up to total height.");
            }

            tileInfo.TileRowStartModeInfo[i] = frameHeader.ModeInfoRowCount;
            tileInfo.TileRowCount = i;
            tileInfo.TileRowCountLog2 = TileLog2(1, tileInfo.TileRowCount);
        }

        if (tileInfo.TileColumnCount > Av1Constants.MaxTileColumnCount || tileInfo.TileRowCount > Av1Constants.MaxTileRowCount)
        {
            throw new ImageFormatException("Tile width or height too big.");
        }

        if (tileInfo.TileColumnCountLog2 > 0 || tileInfo.TileRowCountLog2 > 0)
        {
            tileInfo.ContextUpdateTileId = reader.ReadLiteral(tileInfo.TileRowCountLog2 + tileInfo.TileColumnCountLog2);
            tileInfo.TileSizeBytes = (int)reader.ReadLiteral(2) + 1;
        }
        else
        {
            tileInfo.ContextUpdateTileId = 0;
        }

        if (tileInfo.ContextUpdateTileId >= (tileInfo.TileColumnCount * tileInfo.TileRowCount))
        {
            throw new ImageFormatException("Context update Tile ID too large.");
        }

        return tileInfo;
    }

    /// <summary>
    /// 5.9.2. Uncompressed header syntax.
    /// </summary>
    private void ReadUncompressedFrameHeader(ref Av1BitStreamReader reader)
    {
        ObuSequenceHeader sequenceHeader = this.SequenceHeader!;
        ObuFrameHeader frameHeader = this.FrameHeader!;
        int idLength = 0;
        uint previousFrameId = 0;
        bool frameSizeOverrideFlag = false;
        if (sequenceHeader.IsFrameIdNumbersPresent)
        {
            idLength = sequenceHeader.FrameIdLength - 1 + sequenceHeader.DeltaFrameIdLength - 2 + 3;
            DebugGuard.MustBeLessThanOrEqualTo(idLength, 16, nameof(idLength));
        }

        if (sequenceHeader.IsReducedStillPictureHeader)
        {
            frameHeader.ShowExistingFrame = false;
            frameHeader.FrameType = ObuFrameType.KeyFrame;
            frameHeader.ShowFrame = true;
            frameHeader.ShowableFrame = false;
            frameHeader.ErrorResilientMode = true;
        }
        else
        {
            frameHeader.ShowExistingFrame = reader.ReadBoolean();
            if (frameHeader.ShowExistingFrame)
            {
                frameHeader.FrameToShowMapIdx = reader.ReadLiteral(3);

                if (sequenceHeader.DecoderModelInfoPresentFlag && sequenceHeader.TimingInfo?.EqualPictureInterval == false)
                {
                    // 5.9.31. Temporal point info syntax.
                    frameHeader.FramePresentationTime = reader.ReadLiteral((int)sequenceHeader!.DecoderModelInfo!.FramePresentationTimeLength);
                }

                if (sequenceHeader.IsFrameIdNumbersPresent)
                {
                    frameHeader.DisplayFrameId = reader.ReadLiteral(idLength);
                }

                // TODO: This is incomplete here, not sure how we can display an already decoded frame here or if this is really relevent for still pictures.
                throw new NotImplementedException("ShowExistingFrame is not yet implemented");
            }

            frameHeader.FrameType = (ObuFrameType)reader.ReadLiteral(2);
            frameHeader.ShowFrame = reader.ReadBoolean();

            if (frameHeader.ShowFrame && !sequenceHeader.DecoderModelInfoPresentFlag && sequenceHeader.TimingInfo?.EqualPictureInterval == false)
            {
                // 5.9.31. Temporal point info syntax.
                frameHeader.FramePresentationTime = reader.ReadLiteral((int)sequenceHeader!.DecoderModelInfo!.FramePresentationTimeLength);
            }

            if (frameHeader.ShowFrame)
            {
                frameHeader.ShowableFrame = frameHeader.FrameType != ObuFrameType.KeyFrame;
            }
            else
            {
                frameHeader.ShowableFrame = reader.ReadBoolean();
            }

            if (frameHeader.FrameType == ObuFrameType.SwitchFrame || (frameHeader.FrameType == ObuFrameType.KeyFrame && frameHeader.ShowFrame))
            {
                frameHeader.ErrorResilientMode = true;
            }
            else
            {
                frameHeader.ErrorResilientMode = reader.ReadBoolean();
            }
        }

        if (frameHeader.FrameType == ObuFrameType.KeyFrame && frameHeader.ShowFrame)
        {
            frameHeader.ReferenceValid = new bool[Av1Constants.ReferenceFrameCount];
            frameHeader.ReferenceOrderHint = new bool[Av1Constants.ReferenceFrameCount];
            Array.Fill(frameHeader.ReferenceValid, false);
            Array.Fill(frameHeader.ReferenceOrderHint, false);
        }

        frameHeader.DisableCdfUpdate = reader.ReadBoolean();
        frameHeader.AllowScreenContentTools = sequenceHeader.ForceScreenContentTools == 2;
        if (frameHeader.AllowScreenContentTools)
        {
            frameHeader.AllowScreenContentTools = reader.ReadBoolean();
        }

        if (frameHeader.AllowScreenContentTools)
        {
            if (sequenceHeader.ForceIntegerMotionVector == 1)
            {
                frameHeader.ForceIntegerMotionVector = reader.ReadBoolean();
            }
            else
            {
                frameHeader.ForceIntegerMotionVector = sequenceHeader.ForceIntegerMotionVector != 0;
            }
        }
        else
        {
            frameHeader.ForceIntegerMotionVector = false;
        }

        if (frameHeader.IsIntra)
        {
            frameHeader.ForceIntegerMotionVector = true;
        }

        bool havePreviousFrameId = !(frameHeader.FrameType == ObuFrameType.KeyFrame && frameHeader.ShowFrame);
        if (havePreviousFrameId)
        {
            previousFrameId = frameHeader.CurrentFrameId;
        }

        if (sequenceHeader.IsFrameIdNumbersPresent)
        {
            frameHeader.CurrentFrameId = reader.ReadLiteral(idLength);
            if (havePreviousFrameId)
            {
                uint diffFrameId = (frameHeader.CurrentFrameId > previousFrameId) ?
                    frameHeader.CurrentFrameId - previousFrameId :
                    (uint)((1 << idLength) + (int)frameHeader.CurrentFrameId - previousFrameId);
                if (frameHeader.CurrentFrameId == previousFrameId || diffFrameId >= 1 << (idLength - 1))
                {
                    throw new ImageFormatException("Current frame ID cannot be same as previous Frame ID");
                }
            }

            int diffLength = sequenceHeader.DeltaFrameIdLength;
            for (int i = 0; i < Av1Constants.ReferenceFrameCount; i++)
            {
                if (frameHeader.CurrentFrameId > (1U << diffLength))
                {
                    if ((frameHeader.ReferenceFrameIndex[i] > frameHeader.CurrentFrameId) ||
                        frameHeader.ReferenceFrameIndex[i] > (frameHeader.CurrentFrameId - (1 - diffLength)))
                    {
                        frameHeader.ReferenceValid[i] = false;
                    }
                }
                else if (frameHeader.ReferenceFrameIndex[i] > frameHeader.CurrentFrameId &&
                    frameHeader.ReferenceFrameIndex[i] < ((1 << idLength) + (frameHeader.CurrentFrameId - (1 << diffLength))))
                {
                    frameHeader.ReferenceValid[i] = false;
                }
            }
        }
        else
        {
            frameHeader.CurrentFrameId = 0;
        }

        if (frameHeader.FrameType == ObuFrameType.SwitchFrame)
        {
            frameSizeOverrideFlag = true;
        }
        else if (sequenceHeader.IsReducedStillPictureHeader)
        {
            frameSizeOverrideFlag = false;
        }
        else
        {
            frameSizeOverrideFlag = reader.ReadBoolean();
        }

        frameHeader.OrderHint = reader.ReadLiteral(sequenceHeader.OrderHintInfo.OrderHintBits);

        if (frameHeader.IsIntra || frameHeader.ErrorResilientMode)
        {
            frameHeader.PrimaryReferenceFrame = Av1Constants.PrimaryReferenceFrameNone;
        }
        else
        {
            frameHeader.PrimaryReferenceFrame = reader.ReadLiteral(Av1Constants.PimaryReferenceBits);
        }

        // Skipping, as no decoder info model present
        frameHeader.AllowHighPrecisionMotionVector = false;
        frameHeader.UseReferenceFrameMotionVectors = false;
        frameHeader.AllowIntraBlockCopy = false;
        if (frameHeader.FrameType == ObuFrameType.SwitchFrame || (frameHeader.FrameType == ObuFrameType.KeyFrame && frameHeader.ShowFrame))
        {
            frameHeader.RefreshFrameFlags = 0xFFU;
        }
        else
        {
            frameHeader.RefreshFrameFlags = reader.ReadLiteral(8);
        }

        if (frameHeader.FrameType == ObuFrameType.IntraOnlyFrame)
        {
            DebugGuard.IsTrue(frameHeader.RefreshFrameFlags != 0xFFU, nameof(frameHeader.RefreshFrameFlags));
        }

        if (!frameHeader.IsIntra || (frameHeader.RefreshFrameFlags != 0xFFU))
        {
            if (frameHeader.ErrorResilientMode && sequenceHeader.OrderHintInfo != null)
            {
                for (int i = 0; i < Av1Constants.ReferenceFrameCount; i++)
                {
                    int referenceOrderHint = (int)reader.ReadLiteral(sequenceHeader.OrderHintInfo.OrderHintBits);
                    if (referenceOrderHint != (frameHeader.ReferenceOrderHint[i] ? 1U : 0U))
                    {
                        frameHeader.ReferenceValid[i] = false;
                    }
                }
            }
        }

        if (frameHeader.IsIntra)
        {
            this.ReadFrameSize(ref reader, frameSizeOverrideFlag);
            this.ReadRenderSize(ref reader);
            if (frameHeader.AllowScreenContentTools && frameHeader.FrameSize.RenderWidth != 0)
            {
                if (frameHeader.FrameSize.FrameWidth == frameHeader.FrameSize.SuperResolutionUpscaledWidth)
                {
                    frameHeader.AllowIntraBlockCopy = reader.ReadBoolean();
                }
            }
        }
        else
        {
            // Single image is always Intra.
            throw new InvalidImageContentException("AVIF image can only contain INTRA frames.");
        }

        // SetupFrameBufferReferences(sequenceHeader, frameHeader);
        // CheckAddTemporalMotionVectorBuffer(sequenceHeader, frameHeader);

        // SetupFrameSignBias(sequenceHeader, frameHeader);
        if (sequenceHeader.IsReducedStillPictureHeader || frameHeader.DisableCdfUpdate)
        {
            frameHeader.DisableFrameEndUpdateCdf = true;
        }
        else
        {
            frameHeader.DisableFrameEndUpdateCdf = reader.ReadBoolean();
        }

        if (frameHeader.PrimaryReferenceFrame == Av1Constants.PrimaryReferenceFrameNone)
        {
            // InitConCoefficientCdfs();
            // SetupPastIndependence(frameHeader);
        }
        else
        {
            // LoadCdfs(frameHeader.PrimaryReferenceFrame);
            // LoadPrevious();
            throw new NotImplementedException();
        }

        if (frameHeader.UseReferenceFrameMotionVectors)
        {
            // MotionFieldEstimations();
            throw new NotImplementedException();
        }

        // GenerateNextReferenceFrameMap(sequenceHeader, frameHeader);
        frameHeader.TilesInfo = ReadTileInfo(ref reader, sequenceHeader, frameHeader);
        ReadQuantizationParameters(ref reader, sequenceHeader, frameHeader);
        ReadSegmentationParameters(ref reader, frameHeader);
        ReadFrameDeltaQParameters(ref reader, frameHeader);
        ReadFrameDeltaLoopFilterParameters(ref reader, frameHeader);

        // SetupSegmentationDequantization();
        if (frameHeader.PrimaryReferenceFrame == Av1Constants.PrimaryReferenceFrameNone)
        {
            // ResetParseContext(mainParseContext, frameHeader.QuantizationParameters.BaseQIndex);
        }
        else
        {
            // LoadPreviousSegmentIds();
            throw new NotImplementedException();
        }

        int tilesCount = frameHeader.TilesInfo.TileColumnCount * frameHeader.TilesInfo.TileRowCount;
        frameHeader.CodedLossless = true;
        frameHeader.SegmentationParameters.QMLevel[0] = new int[Av1Constants.MaxSegmentCount];
        frameHeader.SegmentationParameters.QMLevel[1] = new int[Av1Constants.MaxSegmentCount];
        frameHeader.SegmentationParameters.QMLevel[2] = new int[Av1Constants.MaxSegmentCount];
        for (int segmentId = 0; segmentId < Av1Constants.MaxSegmentCount; segmentId++)
        {
            int qIndex = Av1QuantizationLookup.GetQIndex(frameHeader.SegmentationParameters, segmentId, frameHeader.QuantizationParameters.BaseQIndex);
            frameHeader.QuantizationParameters.QIndex[segmentId] = qIndex;
            frameHeader.LosslessArray[segmentId] = qIndex == 0 &&
                frameHeader.QuantizationParameters.DeltaQDc[(int)Av1Plane.Y] == 0 &&
                frameHeader.QuantizationParameters.DeltaQAc[(int)Av1Plane.U] == 0 &&
                frameHeader.QuantizationParameters.DeltaQDc[(int)Av1Plane.U] == 0 &&
                frameHeader.QuantizationParameters.DeltaQAc[(int)Av1Plane.V] == 0 &&
                frameHeader.QuantizationParameters.DeltaQDc[(int)Av1Plane.V] == 0;
            if (!frameHeader.LosslessArray[segmentId])
            {
                frameHeader.CodedLossless = false;
            }

            if (frameHeader.QuantizationParameters.IsUsingQMatrix)
            {
                if (frameHeader.LosslessArray[segmentId])
                {
                    frameHeader.SegmentationParameters.QMLevel[0][segmentId] = 15;
                    frameHeader.SegmentationParameters.QMLevel[1][segmentId] = 15;
                    frameHeader.SegmentationParameters.QMLevel[2][segmentId] = 15;
                }
                else
                {
                    frameHeader.SegmentationParameters.QMLevel[0][segmentId] = frameHeader.QuantizationParameters.QMatrix[(int)Av1Plane.Y];
                    frameHeader.SegmentationParameters.QMLevel[1][segmentId] = frameHeader.QuantizationParameters.QMatrix[(int)Av1Plane.U];
                    frameHeader.SegmentationParameters.QMLevel[2][segmentId] = frameHeader.QuantizationParameters.QMatrix[(int)Av1Plane.V];
                }
            }
        }

        if (frameHeader.CodedLossless)
        {
            DebugGuard.IsFalse(frameHeader.DeltaQParameters.IsPresent, nameof(frameHeader.DeltaQParameters.IsPresent), "No Delta Q parameters are allowed for lossless frame.");
        }

        frameHeader.AllLossless = frameHeader.CodedLossless && frameHeader.FrameSize.FrameWidth == frameHeader.FrameSize.SuperResolutionUpscaledWidth;
        this.ReadLoopFilterParameters(ref reader, sequenceHeader);
        ReadCdefParameters(ref reader, sequenceHeader, frameHeader);
        ReadLoopRestorationParameters(ref reader, sequenceHeader, frameHeader);
        ReadTransformMode(ref reader, frameHeader);

        frameHeader.ReferenceMode = ReadFrameReferenceMode(ref reader, frameHeader);
        ReadSkipModeParameters(ref reader, sequenceHeader, frameHeader);
        if (frameHeader.IsIntra || frameHeader.ErrorResilientMode || !sequenceHeader.EnableWarpedMotion)
        {
            frameHeader.AllowWarpedMotion = false;
        }
        else
        {
            frameHeader.AllowWarpedMotion = reader.ReadBoolean();
        }

        frameHeader.UseReducedTransformSet = reader.ReadBoolean();
        ReadGlobalMotionParameters(ref reader, sequenceHeader, frameHeader);
        frameHeader.FilmGrainParameters = ReadFilmGrainFilterParameters(ref reader, sequenceHeader, frameHeader);
    }

    private static bool IsSegmentationFeatureActive(ObuSegmentationParameters segmentationParameters, int segmentId, ObuSegmentationLevelFeature feature)
        => segmentationParameters.Enabled && segmentationParameters.IsFeatureActive(segmentId, feature);

    /// <summary>
    /// 5.9.1. General frame header OBU syntax.
    /// </summary>
    internal void ReadFrameHeader(ref Av1BitStreamReader reader, ObuHeader header, bool trailingBit)
    {
        int startBitPosition = reader.BitPosition;
        this.ReadUncompressedFrameHeader(ref reader);
        if (trailingBit)
        {
            ReadTrailingBits(ref reader);
        }

        AlignToByteBoundary(ref reader);

        int endPosition = reader.BitPosition;
        int headerBytes = (endPosition - startBitPosition) / 8;
        header.PayloadSize -= headerBytes;
    }

    /// <summary>
    /// 5.11.1. General tile group OBU syntax.
    /// </summary>
    private void ReadTileGroup(ref Av1BitStreamReader reader, IAv1TileReader decoder, ObuHeader header, out bool isLastTileGroup)
    {
        ObuSequenceHeader sequenceHeader = this.SequenceHeader!;
        ObuFrameHeader frameHeader = this.FrameHeader!;
        ObuTileGroupHeader tileInfo = this.FrameHeader!.TilesInfo;
        int tileCount = tileInfo.TileColumnCount * tileInfo.TileRowCount;
        int startBitPosition = reader.BitPosition;
        bool tileStartAndEndPresentFlag = false;
        if (tileCount > 1)
        {
            tileStartAndEndPresentFlag = reader.ReadBoolean();
        }

        if (header.Type == ObuType.FrameHeader)
        {
            DebugGuard.IsFalse(tileStartAndEndPresentFlag, nameof(tileStartAndEndPresentFlag), "Frame header should not set 'tileStartAndEndPresentFlag'.");
        }

        int tileGroupStart = 0;
        int tileGroupEnd = tileCount - 1;
        if (tileCount != 1 && tileStartAndEndPresentFlag)
        {
            int tileBits = tileInfo.TileColumnCountLog2 + tileInfo.TileRowCountLog2;
            tileGroupStart = (int)reader.ReadLiteral(tileBits);
            tileGroupEnd = (int)reader.ReadLiteral(tileBits);
        }

        isLastTileGroup = (tileGroupEnd + 1) == tileCount;
        AlignToByteBoundary(ref reader);
        int endBitPosition = reader.BitPosition;
        int headerBytes = (endBitPosition - startBitPosition) / 8;
        header.PayloadSize -= headerBytes;

        bool noIbc = !frameHeader.AllowIntraBlockCopy;
        bool doLoopFilter = noIbc && (frameHeader.LoopFilterParameters.FilterLevel[0] != 0 || frameHeader.LoopFilterParameters.FilterLevel[1] != 0);
        bool doCdef = noIbc && (!frameHeader.CodedLossless &&
            (frameHeader.CdefParameters.BitCount != 0 ||
            frameHeader.CdefParameters.YStrength[0] != 0 ||
            frameHeader.CdefParameters.UvStrength[0] != 0));
        bool doLoopRestoration = noIbc &&
            (frameHeader.LoopRestorationParameters.Items[(int)Av1Plane.Y].Type != ObuRestorationType.None ||
            frameHeader.LoopRestorationParameters.Items[(int)Av1Plane.U].Type != ObuRestorationType.None ||
            frameHeader.LoopRestorationParameters.Items[(int)Av1Plane.V].Type != ObuRestorationType.None);

        for (int tileNum = tileGroupStart; tileNum <= tileGroupEnd; tileNum++)
        {
            bool isLastTile = tileNum == tileGroupEnd;
            int tileDataSize = header.PayloadSize;
            if (!isLastTile)
            {
                tileDataSize = (int)reader.ReadLittleEndian(tileInfo.TileSizeBytes) + 1;
                header.PayloadSize -= tileDataSize + tileInfo.TileSizeBytes;
            }

            Span<byte> tileData = reader.GetSymbolReader(tileDataSize);
            decoder.ReadTile(tileData, tileNum);
        }

        if (tileGroupEnd != tileCount - 1)
        {
            return;
        }

        // TODO: Share doCdef and doLoopRestoration
    }

    /// <summary>
    /// 5.9.13. Delta quantizer syntax.
    /// </summary>
    private static int ReadDeltaQ(ref Av1BitStreamReader reader)
    {
        int deltaQ = 0;
        if (reader.ReadBoolean())
        {
            deltaQ = reader.ReadSignedFromUnsigned(7);
        }

        return deltaQ;
    }

    /// <summary>
    /// 5.9.17. Quantizer index delta parameters syntax.
    /// </summary>
    private static void ReadFrameDeltaQParameters(ref Av1BitStreamReader reader, ObuFrameHeader frameHeader)
    {
        frameHeader.DeltaQParameters.Resolution = 0;
        frameHeader.DeltaQParameters.IsPresent = false;
        if (frameHeader.QuantizationParameters.BaseQIndex > 0)
        {
            frameHeader.DeltaQParameters.IsPresent = reader.ReadBoolean();
        }

        if (frameHeader.DeltaQParameters.IsPresent)
        {
            frameHeader.DeltaQParameters.Resolution = (int)reader.ReadLiteral(2);
        }
    }

    /// <summary>
    /// 5.9.18. Loop filter delta parameters syntax.
    /// </summary>
    private static void ReadFrameDeltaLoopFilterParameters(ref Av1BitStreamReader reader, ObuFrameHeader frameHeader)
    {
        frameHeader.DeltaLoopFilterParameters.IsPresent = false;
        frameHeader.DeltaLoopFilterParameters.Resolution = 0;
        frameHeader.DeltaLoopFilterParameters.IsMulti = false;
        if (frameHeader.DeltaQParameters.IsPresent)
        {
            if (!frameHeader.AllowIntraBlockCopy)
            {
                frameHeader.DeltaLoopFilterParameters.IsPresent = reader.ReadBoolean();
            }

            if (frameHeader.DeltaLoopFilterParameters.IsPresent)
            {
                frameHeader.DeltaLoopFilterParameters.Resolution = (int)reader.ReadLiteral(2);
                frameHeader.DeltaLoopFilterParameters.IsMulti = reader.ReadBoolean();
            }
        }
    }

    /// <summary>
    /// 5.9.12. Quantization params syntax.
    /// </summary>
    private static void ReadQuantizationParameters(ref Av1BitStreamReader reader, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameHeader)
    {
        ObuQuantizationParameters quantParams = frameHeader.QuantizationParameters;
        ObuColorConfig colorInfo = sequenceHeader.ColorConfig;
        quantParams.BaseQIndex = (int)reader.ReadLiteral(8);
        quantParams.DeltaQDc[(int)Av1Plane.Y] = ReadDeltaQ(ref reader);
        quantParams.DeltaQAc[(int)Av1Plane.Y] = 0;
        if (colorInfo.PlaneCount > 1)
        {
            quantParams.HasSeparateUvDelta = false;
            if (colorInfo.HasSeparateUvDelta)
            {
                quantParams.HasSeparateUvDelta = reader.ReadBoolean();
            }

            quantParams.DeltaQDc[(int)Av1Plane.U] = ReadDeltaQ(ref reader);
            quantParams.DeltaQAc[(int)Av1Plane.U] = ReadDeltaQ(ref reader);
            if (quantParams.HasSeparateUvDelta)
            {
                quantParams.DeltaQDc[(int)Av1Plane.V] = ReadDeltaQ(ref reader);
                quantParams.DeltaQAc[(int)Av1Plane.V] = ReadDeltaQ(ref reader);
            }
            else
            {
                quantParams.DeltaQDc[(int)Av1Plane.V] = quantParams.DeltaQDc[(int)Av1Plane.U];
                quantParams.DeltaQAc[(int)Av1Plane.V] = quantParams.DeltaQAc[(int)Av1Plane.U];
            }
        }
        else
        {
            quantParams.DeltaQDc[(int)Av1Plane.U] = 0;
            quantParams.DeltaQAc[(int)Av1Plane.U] = 0;
            quantParams.DeltaQDc[(int)Av1Plane.V] = 0;
            quantParams.DeltaQAc[(int)Av1Plane.V] = 0;
        }

        quantParams.IsUsingQMatrix = reader.ReadBoolean();
        if (quantParams.IsUsingQMatrix)
        {
            quantParams.QMatrix[(int)Av1Plane.Y] = (int)reader.ReadLiteral(4);
            quantParams.QMatrix[(int)Av1Plane.U] = (int)reader.ReadLiteral(4);
            if (!colorInfo.HasSeparateUvDelta)
            {
                quantParams.QMatrix[(int)Av1Plane.V] = quantParams.QMatrix[(int)Av1Plane.U];
            }
            else
            {
                quantParams.QMatrix[(int)Av1Plane.V] = (int)reader.ReadLiteral(4);
            }
        }
        else
        {
            quantParams.QMatrix[(int)Av1Plane.Y] = 0;
            quantParams.QMatrix[(int)Av1Plane.U] = 0;
            quantParams.QMatrix[(int)Av1Plane.V] = 0;
        }
    }

    /// <summary>
    /// 5.9.14. Segmentation params syntax.
    /// </summary>
    private static void ReadSegmentationParameters(ref Av1BitStreamReader reader, ObuFrameHeader frameHeader)
    {
        frameHeader.SegmentationParameters.Enabled = reader.ReadBoolean();

        if (frameHeader.SegmentationParameters.Enabled)
        {
            if (frameHeader.PrimaryReferenceFrame == PrimaryRefNone)
            {
                frameHeader.SegmentationParameters.SegmentationUpdateMap = 1;
                frameHeader.SegmentationParameters.SegmentationTemporalUpdate = 0;
                frameHeader.SegmentationParameters.SegmentationUpdateData = 1;
            }
            else
            {
                frameHeader.SegmentationParameters.SegmentationUpdateMap = reader.ReadBoolean() ? 1 : 0;
                if (frameHeader.SegmentationParameters.SegmentationUpdateMap == 1)
                {
                    frameHeader.SegmentationParameters.SegmentationTemporalUpdate = reader.ReadBoolean() ? 1 : 0;
                }

                frameHeader.SegmentationParameters.SegmentationUpdateData = reader.ReadBoolean() ? 1 : 0;
            }

            if (frameHeader.SegmentationParameters.SegmentationUpdateData == 1)
            {
                for (int i = 0; i < MaxSegments; i++)
                {
                    for (int j = 0; j < SegLvlMax; j++)
                    {
                        int featureValue = 0;
                        bool featureEnabled = reader.ReadBoolean();
                        frameHeader.SegmentationParameters.FeatureEnabled[i, j] = featureEnabled;
                        int clippedValue = 0;
                        if (featureEnabled)
                        {
                            int bitsToRead = SegmentationFeatureBits[j];
                            int limit = SegmentationFeatureMax[j];
                            if (SegmentationFeatureSigned[j] == 1)
                            {
                                featureValue = reader.ReadSignedFromUnsigned(1 + bitsToRead);
                                clippedValue = Av1Math.Clip3(-limit, limit, featureValue);
                            }
                            else
                            {
                                featureValue = (int)reader.ReadLiteral(bitsToRead);
                            }
                        }

                        frameHeader.SegmentationParameters.FeatureData[i, j] = clippedValue;
                    }
                }
            }
        }
        else
        {
            for (int i = 0; i < Av1Constants.MaxSegmentCount; i++)
            {
                for (int j = 0; j < Av1Constants.SegmentationLevelMax; j++)
                {
                    frameHeader.SegmentationParameters.FeatureEnabled[i, j] = false;
                    frameHeader.SegmentationParameters.FeatureData[i, j] = 0;
                }
            }
        }

        frameHeader.SegmentationParameters.SegmentIdPrecedesSkip = false;
        frameHeader.SegmentationParameters.LastActiveSegmentId = 0;
        for (int i = 0; i < Av1Constants.MaxSegmentCount; i++)
        {
            for (int j = 0; j < Av1Constants.SegmentationLevelMax; j++)
            {
                if (frameHeader.SegmentationParameters.FeatureEnabled[i, j])
                {
                    frameHeader.SegmentationParameters.LastActiveSegmentId = i;
                    if (j >= SegLvlRefFrame)
                    {
                        frameHeader.SegmentationParameters.SegmentIdPrecedesSkip = true;
                    }
                }
            }
        }
    }

    /// <summary>
    /// 5.9.11. Loop filter params syntax
    /// </summary>
    private void ReadLoopFilterParameters(ref Av1BitStreamReader reader, ObuSequenceHeader sequenceHeader)
    {
        ObuFrameHeader frameHeader = this.FrameHeader!;
        if (frameHeader.CodedLossless || frameHeader.AllowIntraBlockCopy)
        {
            return;
        }

        frameHeader.LoopFilterParameters.FilterLevel[0] = (int)reader.ReadLiteral(6);
        frameHeader.LoopFilterParameters.FilterLevel[1] = (int)reader.ReadLiteral(6);

        if (sequenceHeader.ColorConfig.PlaneCount > 1)
        {
            if (frameHeader.LoopFilterParameters.FilterLevel[0] > 0 || frameHeader.LoopFilterParameters.FilterLevel[1] > 0)
            {
                frameHeader.LoopFilterParameters.FilterLevelU = (int)reader.ReadLiteral(6);
                frameHeader.LoopFilterParameters.FilterLevelV = (int)reader.ReadLiteral(6);
            }
        }

        frameHeader.LoopFilterParameters.SharpnessLevel = (int)reader.ReadLiteral(3);
        frameHeader.LoopFilterParameters.ReferenceDeltaModeEnabled = reader.ReadBoolean();
        if (frameHeader.LoopFilterParameters.ReferenceDeltaModeEnabled)
        {
            frameHeader.LoopFilterParameters.ReferenceDeltaModeUpdate = reader.ReadBoolean();
            if (frameHeader.LoopFilterParameters.ReferenceDeltaModeUpdate)
            {
                for (int i = 0; i < Av1Constants.TotalReferencesPerFrame; i++)
                {
                    if (reader.ReadBoolean())
                    {
                        frameHeader.LoopFilterParameters.ReferenceDeltas[i] = reader.ReadSignedFromUnsigned(7);
                    }
                }

                for (int i = 0; i < 2; i++)
                {
                    if (reader.ReadBoolean())
                    {
                        frameHeader.LoopFilterParameters.ModeDeltas[i] = reader.ReadSignedFromUnsigned(7);
                    }
                }
            }
        }
    }

    /// <summary>
    /// 5.9.21. TX mode syntax.
    /// </summary>
    private static void ReadTransformMode(ref Av1BitStreamReader reader, ObuFrameHeader frameHeader)
    {
        if (frameHeader.CodedLossless)
        {
            frameHeader.TransformMode = Av1TransformMode.Only4x4;
        }
        else
        {
            if (reader.ReadBoolean())
            {
                frameHeader.TransformMode = Av1TransformMode.Select;
            }
            else
            {
                frameHeader.TransformMode = Av1TransformMode.Largest;
            }
        }
    }

    /// <summary>
    /// See section 5.9.20. Loop restoration params syntax.
    /// </summary>
    private static void ReadLoopRestorationParameters(ref Av1BitStreamReader reader, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameHeader)
    {
        if (frameHeader.CodedLossless || frameHeader.AllowIntraBlockCopy || !sequenceHeader.EnableRestoration)
        {
            return;
        }

        frameHeader.LoopRestorationParameters.UsesLoopRestoration = false;
        frameHeader.LoopRestorationParameters.UsesChromaLoopRestoration = false;
        int planesCount = sequenceHeader.ColorConfig.PlaneCount;
        for (int i = 0; i < planesCount; i++)
        {
            frameHeader.LoopRestorationParameters.Items[i].Type = (ObuRestorationType)reader.ReadLiteral(2);

            if (frameHeader.LoopRestorationParameters.Items[i].Type != ObuRestorationType.None)
            {
                frameHeader.LoopRestorationParameters.UsesLoopRestoration = true;
                if (i > 0)
                {
                    frameHeader.LoopRestorationParameters.UsesChromaLoopRestoration = true;
                }
            }
        }

        if (frameHeader.LoopRestorationParameters.UsesLoopRestoration)
        {
            frameHeader.LoopRestorationParameters.UnitShift = (int)reader.ReadLiteral(1);
            if (sequenceHeader.Use128x128Superblock)
            {
                frameHeader.LoopRestorationParameters.UnitShift++;
            }
            else
            {
                if (reader.ReadBoolean())
                {
                    frameHeader.LoopRestorationParameters.UnitShift += (int)reader.ReadLiteral(1);
                }
            }

            frameHeader.LoopRestorationParameters.Items[0].Size = Av1Constants.RestorationMaxTileSize >> (2 - frameHeader.LoopRestorationParameters.UnitShift);
            frameHeader.LoopRestorationParameters.UVShift = 0;
            if (sequenceHeader.ColorConfig.SubSamplingX && sequenceHeader.ColorConfig.SubSamplingY && frameHeader.LoopRestorationParameters.UsesChromaLoopRestoration)
            {
                frameHeader.LoopRestorationParameters.UVShift = (int)reader.ReadLiteral(1);
            }

            frameHeader.LoopRestorationParameters.Items[1].Size = frameHeader.LoopRestorationParameters.Items[0].Size >> frameHeader.LoopRestorationParameters.UVShift;
            frameHeader.LoopRestorationParameters.Items[2].Size = frameHeader.LoopRestorationParameters.Items[0].Size >> frameHeader.LoopRestorationParameters.UVShift;
        }
    }

    /// <summary>
    /// See section 5.9.19. CDEF params syntax.
    /// </summary>
    private static void ReadCdefParameters(ref Av1BitStreamReader reader, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameHeader)
    {
        ObuConstraintDirectionalEnhancementFilterParameters cdefInfo = frameHeader.CdefParameters;
        bool multiPlane = sequenceHeader.ColorConfig.PlaneCount > 1;
        if (frameHeader.CodedLossless || frameHeader.AllowIntraBlockCopy || sequenceHeader.CdefLevel == 0)
        {
            cdefInfo.BitCount = 0;
            cdefInfo.YStrength[0] = 0;
            cdefInfo.YStrength[4] = 0;
            cdefInfo.UvStrength[0] = 0;
            cdefInfo.UvStrength[4] = 0;
            cdefInfo.Damping = 0;
            return;
        }

        cdefInfo.Damping = (int)reader.ReadLiteral(2) + 3;
        cdefInfo.BitCount = (int)reader.ReadLiteral(2);
        for (int i = 0; i < (1 << frameHeader.CdefParameters.BitCount); i++)
        {
            cdefInfo.YStrength[i] = (int)reader.ReadLiteral(6);

            if (multiPlane)
            {
                cdefInfo.UvStrength[i] = (int)reader.ReadLiteral(6);
            }
        }
    }

    /// <summary>
    /// 5.9.24. Global motion params syntax.
    /// </summary>
    private static void ReadGlobalMotionParameters(ref Av1BitStreamReader reader, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameHeader)
    {
        _ = reader;
        _ = sequenceHeader;

        if (frameHeader.IsIntra)
        {
            return;
        }

        // Not applicable for INTRA frames.
        throw new NotImplementedException();
    }

    /// <summary>
    /// 5.9.23. Frame reference mode syntax
    /// </summary>
    private static ObuReferenceMode ReadFrameReferenceMode(ref Av1BitStreamReader reader, ObuFrameHeader frameHeader)
    {
        if (frameHeader.IsIntra)
        {
            return ObuReferenceMode.SingleReference;
        }

        return (ObuReferenceMode)reader.ReadLiteral(1);
    }

    /// <summary>
    /// 5.11.10. Skip mode syntax.
    /// </summary>
    private static void ReadSkipModeParameters(ref Av1BitStreamReader reader, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameHeader)
    {
        if (frameHeader.IsIntra || frameHeader.ReferenceMode == ObuReferenceMode.ReferenceModeSelect || !sequenceHeader.OrderHintInfo.EnableOrderHint)
        {
            frameHeader.SkipModeParameters.SkipModeAllowed = false;
        }
        else
        {
            // Not applicable for INTRA frames.
        }

        if (frameHeader.SkipModeParameters.SkipModeAllowed)
        {
            frameHeader.SkipModeParameters.SkipModeFlag = reader.ReadBoolean();
        }
        else
        {
            frameHeader.SkipModeParameters.SkipModeFlag = false;
        }
    }

    /// <summary>
    /// 5.9.30. Film grain params syntax.
    /// </summary>
    private static ObuFilmGrainParameters ReadFilmGrainFilterParameters(ref Av1BitStreamReader reader, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameHeader)
    {
        ObuFilmGrainParameters grainParams = new();
        if (!sequenceHeader.AreFilmGrainingParametersPresent || (!frameHeader.ShowFrame && !frameHeader.ShowableFrame))
        {
            return grainParams;
        }

        grainParams.ApplyGrain = reader.ReadBoolean();
        if (!grainParams.ApplyGrain)
        {
            return grainParams;
        }

        grainParams.GrainSeed = reader.ReadLiteral(16);

        if (frameHeader.FrameType == ObuFrameType.InterFrame)
        {
            grainParams.UpdateGrain = reader.ReadBoolean();
        }
        else
        {
            grainParams.UpdateGrain = false;
        }

        if (!grainParams.UpdateGrain)
        {
            grainParams.FilmGrainParamsRefidx = reader.ReadLiteral(3);
            uint tempGrainSeed = grainParams.GrainSeed;

            // TODO: implement load_grain_params
            // load_grain_params(film_grain_params_ref_idx)
            grainParams.GrainSeed = tempGrainSeed;
            return grainParams;
        }

        grainParams.NumYPoints = reader.ReadLiteral(4);
        grainParams.PointYValue = new uint[grainParams.NumYPoints];
        grainParams.PointYScaling = new uint[grainParams.NumYPoints];
        for (int i = 0; i < grainParams.NumYPoints; i++)
        {
            grainParams.PointYValue[i] = reader.ReadLiteral(8);
            grainParams.PointYScaling[i] = reader.ReadLiteral(8);
        }

        if (sequenceHeader.ColorConfig.IsMonochrome)
        {
            grainParams.ChromaScalingFromLuma = false;
        }
        else
        {
            grainParams.ChromaScalingFromLuma = reader.ReadBoolean();
        }

        if (sequenceHeader.ColorConfig.IsMonochrome ||
            grainParams.ChromaScalingFromLuma ||
            (sequenceHeader.ColorConfig.SubSamplingX && sequenceHeader.ColorConfig.SubSamplingY && grainParams.NumYPoints == 0))
        {
            grainParams.NumCbPoints = 0;
            grainParams.NumCrPoints = 0;
        }
        else
        {
            grainParams.NumCbPoints = reader.ReadLiteral(4);
            grainParams.PointCbValue = new uint[grainParams.NumCbPoints];
            grainParams.PointCbScaling = new uint[grainParams.NumCbPoints];
            for (int i = 0; i < grainParams.NumCbPoints; i++)
            {
                grainParams.PointCbValue[i] = reader.ReadLiteral(8);
                grainParams.PointCbScaling[i] = reader.ReadLiteral(8);
            }

            grainParams.NumCrPoints = reader.ReadLiteral(4);
            grainParams.PointCrValue = new uint[grainParams.NumCrPoints];
            grainParams.PointCrScaling = new uint[grainParams.NumCrPoints];
            for (int i = 0; i < grainParams.NumCbPoints; i++)
            {
                grainParams.PointCrValue[i] = reader.ReadLiteral(8);
                grainParams.PointCrScaling[i] = reader.ReadLiteral(8);
            }
        }

        grainParams.GrainScalingMinus8 = reader.ReadLiteral(2);
        grainParams.ArCoeffLag = reader.ReadLiteral(2);
        uint numPosLuma = 2 * grainParams.ArCoeffLag * (grainParams.ArCoeffLag + 1);

        uint numPosChroma = 0;
        if (grainParams.NumYPoints != 0)
        {
            numPosChroma = numPosLuma + 1;
            grainParams.ArCoeffsYPlus128 = new uint[numPosLuma];
            for (int i = 0; i < numPosLuma; i++)
            {
                grainParams.ArCoeffsYPlus128[i] = reader.ReadLiteral(8);
            }
        }
        else
        {
            numPosChroma = numPosLuma;
        }

        if (grainParams.ChromaScalingFromLuma || grainParams.NumCbPoints != 0)
        {
            grainParams.ArCoeffsCbPlus128 = new uint[numPosChroma];
            for (int i = 0; i < numPosChroma; i++)
            {
                grainParams.ArCoeffsCbPlus128[i] = reader.ReadLiteral(8);
            }
        }

        if (grainParams.ChromaScalingFromLuma || grainParams.NumCrPoints != 0)
        {
            grainParams.ArCoeffsCrPlus128 = new uint[numPosChroma];
            for (int i = 0; i < numPosChroma; i++)
            {
                grainParams.ArCoeffsCrPlus128[i] = reader.ReadLiteral(8);
            }
        }

        grainParams.ArCoeffShiftMinus6 = reader.ReadLiteral(2);
        grainParams.GrainScaleShift = reader.ReadLiteral(2);
        if (grainParams.NumCbPoints != 0)
        {
            grainParams.CbMult = reader.ReadLiteral(8);
            grainParams.CbLumaMult = reader.ReadLiteral(8);
            grainParams.CbOffset = reader.ReadLiteral(9);
        }

        if (grainParams.NumCrPoints != 0)
        {
            grainParams.CrMult = reader.ReadLiteral(8);
            grainParams.CrLumaMult = reader.ReadLiteral(8);
            grainParams.CrOffset = reader.ReadLiteral(9);
        }

        grainParams.OverlapFlag = reader.ReadBoolean();
        grainParams.ClipToRestrictedRange = reader.ReadBoolean();

        return grainParams;
    }

    private static bool IsValidSequenceLevel(int sequenceLevelIndex)
        => sequenceLevelIndex is < 24 or 31;

    /// <summary>
    /// Returns the smallest value for k such that blockSize &lt;&lt; k is greater than or equal to target.
    /// </summary>
    public static int TileLog2(int blockSize, int target)
    {
        int k;
        for (k = 0; (blockSize << k) < target; k++)
        {
        }

        return k;
    }
}
