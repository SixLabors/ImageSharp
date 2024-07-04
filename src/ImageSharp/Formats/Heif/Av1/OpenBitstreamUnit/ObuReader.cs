// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

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

    public ObuSequenceHeader? SequenceHeader { get; set; }

    public ObuFrameHeader? FrameHeader { get; set; }

    /// <summary>
    /// Decode all OBU's in a frame.
    /// </summary>
    public void Read(ref Av1BitStreamReader reader, int dataSize, IAv1TileDecoder decoder, bool isAnnexB = false)
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
                    if (this.SequenceHeader.ColorConfig.BitDepth == 12)
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

                    this.ReadTileGroup(ref reader, decoder, header, out frameDecodingFinished);
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
            header.SpatialId = (int)reader.ReadLiteral(3);
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

    private void ComputeImageSize(ObuSequenceHeader sequenceHeader)
    {
        ObuFrameHeader frameInfo = this.FrameHeader!;
        frameInfo.ModeInfoColumnCount = 2 * ((frameInfo.FrameSize.FrameWidth + 7) >> 3);
        frameInfo.ModeInfoRowCount = 2 * ((frameInfo.FrameSize.FrameHeight + 7) >> 3);
        frameInfo.ModeInfoStride = Av1Math.AlignPowerOf2(sequenceHeader.MaxFrameWidth, Av1Constants.MaxSuperBlockSizeLog2) >> Av1Constants.ModeInfoSizeLog2;
    }

    private static bool IsValidObuType(ObuType type) => type switch
    {
        ObuType.SequenceHeader or ObuType.TemporalDelimiter or ObuType.FrameHeader or
        ObuType.TileGroup or ObuType.Metadata or ObuType.Frame or ObuType.RedundantFrameHeader or
        ObuType.TileList or ObuType.Padding => true,
        _ => false,
    };

    /// <summary>
    /// 5.5.1. General sequence header OBU syntax.
    /// </summary>
    private static void ReadSequenceHeader(ref Av1BitStreamReader reader, ObuSequenceHeader sequenceHeader)
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
                ReadTimingInfo(ref reader,  sequenceHeader);
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
                sequenceHeader.OperatingPoint[i] = new ObuOperatingPoint();
                sequenceHeader.OperatingPoint[i].Idc = reader.ReadLiteral(12);
                sequenceHeader.OperatingPoint[i].SequenceLevelIndex = (int)reader.ReadLiteral(5);
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
        sequenceHeader.Use128x128SuperBlock = reader.ReadBoolean();
        sequenceHeader.SuperBlockSize = sequenceHeader.Use128x128SuperBlock ? Av1BlockSize.Block128x128 : Av1BlockSize.Block64x64;
        sequenceHeader.ModeInfoSize = sequenceHeader.Use128x128SuperBlock ? 32 : 16;
        sequenceHeader.SuperBlockSizeLog2 = sequenceHeader.Use128x128SuperBlock ? 7 : 6;
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

        colorConfig.ChannelCount = colorConfig.IsMonochrome ? 1 : 3;
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
                    if (colorConfig.BitDepth == 12)
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
            colorConfig.BitDepth = reader.ReadBoolean() ? 12 : 10;
        }
        else if (sequenceHeader.SequenceProfile <= ObuSequenceProfile.Professional)
        {
            colorConfig.BitDepth = hasHighBitDepth ? 10 : 8;
        }
        else
        {
            colorConfig.BitDepth = 8;
        }
    }

    /// <summary>
    /// 5.9.8. Superres params syntax.
    /// </summary>
    private void ReadSuperResolutionParameters(ref Av1BitStreamReader reader)
    {
        ObuSequenceHeader sequenceHeader = this.SequenceHeader!;
        ObuFrameHeader frameInfo = this.FrameHeader!;
        bool useSuperResolution = false;
        if (sequenceHeader.EnableSuperResolution)
        {
            useSuperResolution = reader.ReadBoolean();
        }

        if (useSuperResolution)
        {
            frameInfo.FrameSize.SuperResolutionDenominator = (int)reader.ReadLiteral(Av1Constants.SuperResolutionScaleBits) + Av1Constants.SuperResolutionScaleDenominatorMinimum;
        }
        else
        {
            frameInfo.FrameSize.SuperResolutionDenominator = Av1Constants.ScaleNumerator;
        }

        frameInfo.FrameSize.SuperResolutionUpscaledWidth = frameInfo.FrameSize.FrameWidth;
        frameInfo.FrameSize.FrameWidth =
            ((frameInfo.FrameSize.SuperResolutionUpscaledWidth * Av1Constants.ScaleNumerator) +
            (frameInfo.FrameSize.SuperResolutionDenominator / 2)) /
            frameInfo.FrameSize.SuperResolutionDenominator;

        if (frameInfo.FrameSize.SuperResolutionDenominator != Av1Constants.ScaleNumerator)
        {
            int manWidth = Math.Min(16, frameInfo.FrameSize.SuperResolutionUpscaledWidth);
            frameInfo.FrameSize.FrameWidth = Math.Max(manWidth, frameInfo.FrameSize.FrameWidth);
        }
    }

    /// <summary>
    /// 5.9.6. Render size syntax.
    /// </summary>
    private void ReadRenderSize(ref Av1BitStreamReader reader)
    {
        ObuFrameHeader frameInfo = this.FrameHeader!;
        bool renderSizeAndFrameSizeDifferent = reader.ReadBoolean();
        if (renderSizeAndFrameSizeDifferent)
        {
            frameInfo.FrameSize.RenderWidth = (int)reader.ReadLiteral(16) + 1;
            frameInfo.FrameSize.RenderHeight = (int)reader.ReadLiteral(16) + 1;
        }
        else
        {
            frameInfo.FrameSize.RenderWidth = frameInfo.FrameSize.SuperResolutionUpscaledWidth;
            frameInfo.FrameSize.RenderHeight = frameInfo.FrameSize.FrameHeight;
        }
    }

    /// <summary>
    /// 5.9.7. Frame size with refs syntax.
    /// </summary>
    private void ReadFrameSizeWithReferences(ref Av1BitStreamReader reader, bool frameSizeOverrideFlag)
    {
        ObuSequenceHeader sequenceHeader = this.SequenceHeader!;
        ObuFrameHeader frameInfo = this.FrameHeader!;
        bool foundReference = false;
        for (int i = 0; i < Av1Constants.ReferencesPerFrame; i++)
        {
            foundReference = reader.ReadBoolean();
            if (foundReference)
            {
                // Take values over from reference frame
                break;
            }
        }

        if (!foundReference)
        {
            this.ReadFrameSize(ref reader, frameSizeOverrideFlag);
            this.ReadRenderSize(ref reader);
        }
        else
        {
            this.ReadSuperResolutionParameters(ref reader);
            this.ComputeImageSize(sequenceHeader);
        }
    }

    /// <summary>
    /// 5.9.5. Frame size syntax.
    /// </summary>
    private void ReadFrameSize(ref Av1BitStreamReader reader, bool frameSizeOverrideFlag)
    {
        ObuSequenceHeader sequenceHeader = this.SequenceHeader!;
        ObuFrameHeader frameInfo = this.FrameHeader!;
        if (frameSizeOverrideFlag)
        {
            frameInfo.FrameSize.FrameWidth = (int)reader.ReadLiteral(sequenceHeader.FrameWidthBits) + 1;
            frameInfo.FrameSize.FrameHeight = (int)reader.ReadLiteral(sequenceHeader.FrameHeightBits) + 1;
        }
        else
        {
            frameInfo.FrameSize.FrameWidth = sequenceHeader.MaxFrameWidth;
            frameInfo.FrameSize.FrameHeight = sequenceHeader.MaxFrameHeight;
        }

        this.ReadSuperResolutionParameters(ref reader);
        this.ComputeImageSize(sequenceHeader);
    }

    /// <summary>
    /// 5.9.15. Tile info syntax.
    /// </summary>
    private static ObuTileGroupHeader ReadTileInfo(ref Av1BitStreamReader reader, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameInfo)
    {
        ObuTileGroupHeader tileInfo = new();
        int superBlockColumnCount;
        int superBlockRowCount;
        int superBlockShift;
        if (sequenceHeader.Use128x128SuperBlock)
        {
            superBlockColumnCount = (frameInfo.ModeInfoColumnCount + 31) >> 5;
            superBlockRowCount = (frameInfo.ModeInfoRowCount + 31) >> 5;
            superBlockShift = 5;
        }
        else
        {
            superBlockColumnCount = (frameInfo.ModeInfoColumnCount + 15) >> 4;
            superBlockRowCount = (frameInfo.ModeInfoRowCount + 15) >> 4;
            superBlockShift = 4;
        }

        int superBlockSize = superBlockShift + 2;
        int maxTileAreaOfSuperBlock = Av1Constants.MaxTileArea >> (2 * superBlockSize);

        tileInfo.MaxTileWidthSuperBlock = Av1Constants.MaxTileWidth >> superBlockSize;
        tileInfo.MaxTileHeightSuperBlock = (Av1Constants.MaxTileArea / Av1Constants.MaxTileWidth) >> superBlockSize;
        tileInfo.MinLog2TileColumnCount = TileLog2(tileInfo.MaxTileWidthSuperBlock, superBlockColumnCount);
        tileInfo.MaxLog2TileColumnCount = TileLog2(1, Math.Min(superBlockColumnCount, Av1Constants.MaxTileColumnCount));
        tileInfo.MaxLog2TileRowCount = TileLog2(1, Math.Min(superBlockRowCount, Av1Constants.MaxTileRowCount));
        tileInfo.MinLog2TileCount = Math.Max(tileInfo.MinLog2TileColumnCount, TileLog2(maxTileAreaOfSuperBlock, superBlockColumnCount * superBlockRowCount));
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

            int tileWidthSuperBlock = (superBlockColumnCount + (1 << tileInfo.TileColumnCountLog2) - 1) >> tileInfo.TileColumnCountLog2;
            if (tileWidthSuperBlock > tileInfo.MaxTileWidthSuperBlock)
            {
                throw new ImageFormatException("Invalid tile width specified.");
            }

            int i = 0;
            tileInfo.TileColumnStartModeInfo = new int[superBlockColumnCount + 1];
            for (int startSuperBlock = 0; startSuperBlock < superBlockColumnCount; startSuperBlock += tileWidthSuperBlock)
            {
                tileInfo.TileColumnStartModeInfo[i] = startSuperBlock << superBlockShift;
                i++;
            }

            tileInfo.TileColumnStartModeInfo[i] = frameInfo.ModeInfoColumnCount;
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

            int tileHeightSuperBlock = (superBlockRowCount + (1 << tileInfo.TileRowCountLog2) - 1) >> tileInfo.TileRowCountLog2;
            if (tileHeightSuperBlock > tileInfo.MaxTileHeightSuperBlock)
            {
                throw new ImageFormatException("Invalid tile height specified.");
            }

            i = 0;
            tileInfo.TileRowStartModeInfo = new int[superBlockRowCount + 1];
            for (int startSuperBlock = 0; startSuperBlock < superBlockRowCount; startSuperBlock += tileHeightSuperBlock)
            {
                tileInfo.TileRowStartModeInfo[i] = startSuperBlock << superBlockShift;
                i++;
            }

            tileInfo.TileRowStartModeInfo[i] = frameInfo.ModeInfoRowCount;
            tileInfo.TileRowCount = i;
        }
        else
        {
            uint widestTileSuperBlock = 0U;
            int startSuperBlock = 0;
            int i = 0;
            for (; startSuperBlock < superBlockColumnCount; i++)
            {
                tileInfo.TileColumnStartModeInfo[i] = startSuperBlock << superBlockShift;
                uint maxWidth = (uint)Math.Min(superBlockColumnCount - startSuperBlock, tileInfo.MaxTileWidthSuperBlock);
                uint widthInSuperBlocks = reader.ReadNonSymmetric(maxWidth) + 1;
                widestTileSuperBlock = Math.Max(widthInSuperBlocks, widestTileSuperBlock);
                startSuperBlock += (int)widthInSuperBlocks;
            }

            if (startSuperBlock != superBlockColumnCount)
            {
                throw new ImageFormatException("Super block tiles width does not add up to total width.");
            }

            tileInfo.TileColumnStartModeInfo[i] = frameInfo.ModeInfoColumnCount;
            tileInfo.TileColumnCount = i;
            tileInfo.TileColumnCountLog2 = TileLog2(1, tileInfo.TileColumnCount);
            if (tileInfo.MinLog2TileCount > 0)
            {
                maxTileAreaOfSuperBlock = (superBlockRowCount * superBlockColumnCount) >> (tileInfo.MinLog2TileCount + 1);
            }
            else
            {
                maxTileAreaOfSuperBlock = superBlockRowCount * superBlockColumnCount;
            }

            DebugGuard.MustBeGreaterThan(widestTileSuperBlock, 0U, nameof(widestTileSuperBlock));
            tileInfo.MaxTileHeightSuperBlock = Math.Max(maxTileAreaOfSuperBlock / (int)widestTileSuperBlock, 1);

            startSuperBlock = 0;
            for (i = 0; startSuperBlock < superBlockRowCount; i++)
            {
                tileInfo.TileRowStartModeInfo[i] = startSuperBlock << superBlockShift;
                uint maxHeight = (uint)Math.Min(superBlockRowCount - startSuperBlock, tileInfo.MaxTileHeightSuperBlock);
                uint heightInSuperBlocks = reader.ReadNonSymmetric(maxHeight) + 1;
                startSuperBlock += (int)heightInSuperBlocks;
            }

            if (startSuperBlock != superBlockRowCount)
            {
                throw new ImageFormatException("Super block tiles height does not add up to total height.");
            }

            tileInfo.TileRowStartModeInfo[i] = frameInfo.ModeInfoRowCount;
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
    private void ReadUncompressedFrameHeader(ref Av1BitStreamReader reader, ObuHeader header, int planesCount)
    {
        ObuSequenceHeader sequenceHeader = this.SequenceHeader!;
        ObuFrameHeader frameInfo = this.FrameHeader!;
        int idLength = 0;
        uint previousFrameId = 0;
        bool isIntraFrame = false;
        bool frameSizeOverrideFlag = false;
        if (sequenceHeader.IsFrameIdNumbersPresent)
        {
            idLength = sequenceHeader.FrameIdLength - 1 + sequenceHeader.DeltaFrameIdLength - 2 + 3;
            DebugGuard.MustBeLessThanOrEqualTo(idLength, 16, nameof(idLength));
        }

        if (sequenceHeader.IsReducedStillPictureHeader)
        {
            frameInfo.ShowExistingFrame = false;
            frameInfo.FrameType = ObuFrameType.KeyFrame;
            isIntraFrame = true;
            frameInfo.ShowFrame = true;
            frameInfo.ShowableFrame = false;
            frameInfo.ErrorResilientMode = true;
        }
        else
        {
            frameInfo.ShowExistingFrame = reader.ReadBoolean();
            if (frameInfo.ShowExistingFrame)
            {
                frameInfo.FrameToShowMapIdx = reader.ReadLiteral(3);
            }

            if (sequenceHeader.DecoderModelInfoPresentFlag && sequenceHeader.TimingInfo?.EqualPictureInterval == false)
            {
                // 5.9.31. Temporal point info syntax.
                frameInfo.FramePresentationTime = reader.ReadLiteral((int)sequenceHeader!.DecoderModelInfo!.FramePresentationTimeLength);
            }

            // int refreshFrameFlags = 0;
            if (sequenceHeader.IsFrameIdNumbersPresent)
            {
                frameInfo.DisplayFrameId = reader.ReadLiteral(idLength);
            }

            // TODO: This is incomplete here, not sure how we can display an already decoded frame here.
            throw new NotImplementedException("ShowExistingFrame is not yet implemented");
        }

        if (frameInfo.FrameType == ObuFrameType.KeyFrame && frameInfo.ShowFrame)
        {
            frameInfo.ReferenceValid = new bool[Av1Constants.ReferenceFrameCount];
            frameInfo.ReferenceOrderHint = new bool[Av1Constants.ReferenceFrameCount];
            Array.Fill(frameInfo.ReferenceValid, false);
            Array.Fill(frameInfo.ReferenceOrderHint, false);
        }

        frameInfo.DisableCdfUpdate = reader.ReadBoolean();
        frameInfo.AllowScreenContentTools = sequenceHeader.ForceScreenContentTools == 2;
        if (frameInfo.AllowScreenContentTools)
        {
            frameInfo.AllowScreenContentTools = reader.ReadBoolean();
        }

        if (frameInfo.AllowScreenContentTools)
        {
            if (sequenceHeader.ForceIntegerMotionVector == 1)
            {
                frameInfo.ForceIntegerMotionVector = reader.ReadBoolean();
            }
            else
            {
                frameInfo.ForceIntegerMotionVector = sequenceHeader.ForceIntegerMotionVector != 0;
            }
        }
        else
        {
            frameInfo.ForceIntegerMotionVector = false;
        }

        if (isIntraFrame)
        {
            frameInfo.ForceIntegerMotionVector = true;
        }

        bool havePreviousFrameId = !(frameInfo.FrameType == ObuFrameType.KeyFrame && frameInfo.ShowFrame);
        if (havePreviousFrameId)
        {
            previousFrameId = frameInfo.CurrentFrameId;
        }

        if (sequenceHeader.IsFrameIdNumbersPresent)
        {
            frameInfo.CurrentFrameId = reader.ReadLiteral(idLength);
            if (havePreviousFrameId)
            {
                uint diffFrameId = (frameInfo.CurrentFrameId > previousFrameId) ?
                    frameInfo.CurrentFrameId - previousFrameId :
                    (uint)((1 << idLength) + (int)frameInfo.CurrentFrameId - previousFrameId);
                if (frameInfo.CurrentFrameId == previousFrameId || diffFrameId >= 1 << (idLength - 1))
                {
                    throw new ImageFormatException("Current frame ID cannot be same as previous Frame ID");
                }
            }

            int diffLength = sequenceHeader.DeltaFrameIdLength;
            for (int i = 0; i < Av1Constants.ReferenceFrameCount; i++)
            {
                if (frameInfo.CurrentFrameId > (1U << diffLength))
                {
                    if ((frameInfo.ReferenceFrameIndex[i] > frameInfo.CurrentFrameId) ||
                        frameInfo.ReferenceFrameIndex[i] > (frameInfo.CurrentFrameId - (1 - diffLength)))
                    {
                        frameInfo.ReferenceValid[i] = false;
                    }
                }
                else if (frameInfo.ReferenceFrameIndex[i] > frameInfo.CurrentFrameId &&
                    frameInfo.ReferenceFrameIndex[i] < ((1 << idLength) + (frameInfo.CurrentFrameId - (1 << diffLength))))
                {
                    frameInfo.ReferenceValid[i] = false;
                }
            }
        }
        else
        {
            frameInfo.CurrentFrameId = 0;
        }

        if (frameInfo.FrameType == ObuFrameType.SwitchFrame)
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

        if (sequenceHeader.OrderHintInfo.OrderHintBits > 0)
        {
            frameInfo.OrderHint = reader.ReadLiteral(sequenceHeader.OrderHintInfo.OrderHintBits);
        }
        else
        {
            frameInfo.OrderHint = 0;
        }

        if (isIntraFrame || frameInfo.ErrorResilientMode)
        {
            frameInfo.PrimaryReferenceFrame = Av1Constants.PrimaryReferenceFrameNone;
        }
        else
        {
            frameInfo.PrimaryReferenceFrame = reader.ReadLiteral(Av1Constants.PimaryReferenceBits);
        }

        // Skipping, as no decoder info model present
        frameInfo.AllowHighPrecisionMotionVector = false;
        frameInfo.UseReferenceFrameMotionVectors = false;
        frameInfo.AllowIntraBlockCopy = false;
        if (frameInfo.FrameType == ObuFrameType.SwitchFrame || (frameInfo.FrameType == ObuFrameType.KeyFrame && frameInfo.ShowFrame))
        {
            frameInfo.RefreshFrameFlags = 0xFFU;
        }
        else
        {
            frameInfo.RefreshFrameFlags = reader.ReadLiteral(8);
        }

        if (frameInfo.FrameType == ObuFrameType.IntraOnlyFrame)
        {
            DebugGuard.IsTrue(frameInfo.RefreshFrameFlags != 0xFFU, nameof(frameInfo.RefreshFrameFlags));
        }

        if (!isIntraFrame || (frameInfo.RefreshFrameFlags != 0xFFU))
        {
            if (frameInfo.ErrorResilientMode && sequenceHeader.OrderHintInfo != null)
            {
                for (int i = 0; i < Av1Constants.ReferenceFrameCount; i++)
                {
                    int referenceOrderHint = (int)reader.ReadLiteral(sequenceHeader.OrderHintInfo.OrderHintBits);
                    if (referenceOrderHint != (frameInfo.ReferenceOrderHint[i] ? 1U : 0U))
                    {
                        frameInfo.ReferenceValid[i] = false;
                    }
                }
            }
        }

        if (isIntraFrame)
        {
            this.ReadFrameSize(ref reader, frameSizeOverrideFlag);
            this.ReadRenderSize(ref reader);
            if (frameInfo.AllowScreenContentTools && frameInfo.FrameSize.RenderWidth != 0)
            {
                if (frameInfo.FrameSize.FrameWidth == frameInfo.FrameSize.SuperResolutionUpscaledWidth)
                {
                    frameInfo.AllowIntraBlockCopy = reader.ReadBoolean();
                }
            }
        }
        else
        {
            // Single image is always Intra.
        }

        // SetupFrameBufferReferences(sequenceHeader, frameInfo);
        // CheckAddTemporalMotionVectorBuffer(sequenceHeader, frameInfo);

        // SetupFrameSignBias(sequenceHeader, frameInfo);
        if (sequenceHeader.IsReducedStillPictureHeader || frameInfo.DisableCdfUpdate)
        {
            frameInfo.DisableFrameEndUpdateCdf = true;
        }

        if (frameInfo.PrimaryReferenceFrame == Av1Constants.PrimaryReferenceFrameNone)
        {
            SetupPastIndependence(frameInfo);
        }

        // GenerateNextReferenceFrameMap(sequenceHeader, frameInfo);
        frameInfo.TilesInfo = ReadTileInfo(ref reader, sequenceHeader, frameInfo);
        ReadQuantizationParameters(ref reader, frameInfo.QuantizationParameters, sequenceHeader.ColorConfig, planesCount);
        ReadSegmentationParameters(ref reader, sequenceHeader, frameInfo, planesCount);
        ReadFrameDeltaQParameters(ref reader, frameInfo);
        ReadFrameDeltaLoopFilterParameters(ref reader, frameInfo);

        // SetupSegmentationDequantization();
        if (frameInfo.PrimaryReferenceFrame == Av1Constants.PrimaryReferenceFrameNone)
        {
            // ResetParseContext(mainParseContext, frameInfo.QuantizationParameters.BaseQIndex);
        }

        int tilesCount = frameInfo.TilesInfo.TileColumnCount * frameInfo.TilesInfo.TileRowCount;
        frameInfo.CodedLossless = true;
        frameInfo.SegmentationParameters.QMLevel[0] = new int[Av1Constants.MaxSegmentCount];
        frameInfo.SegmentationParameters.QMLevel[1] = new int[Av1Constants.MaxSegmentCount];
        frameInfo.SegmentationParameters.QMLevel[2] = new int[Av1Constants.MaxSegmentCount];
        for (int segmentId = 0; segmentId < Av1Constants.MaxSegmentCount; segmentId++)
        {
            int qIndex = GetQIndex(frameInfo.SegmentationParameters, segmentId, frameInfo.QuantizationParameters.BaseQIndex);
            frameInfo.QuantizationParameters.QIndex[segmentId] = qIndex;
            frameInfo.LosslessArray[segmentId] = qIndex == 0 &&
                frameInfo.QuantizationParameters.DeltaQDc[(int)Av1Plane.Y] == 0 &&
                frameInfo.QuantizationParameters.DeltaQAc[(int)Av1Plane.U] == 0 &&
                frameInfo.QuantizationParameters.DeltaQDc[(int)Av1Plane.U] == 0 &&
                frameInfo.QuantizationParameters.DeltaQAc[(int)Av1Plane.V] == 0 &&
                frameInfo.QuantizationParameters.DeltaQDc[(int)Av1Plane.V] == 0;
            if (!frameInfo.LosslessArray[segmentId])
            {
                frameInfo.CodedLossless = false;
            }

            if (frameInfo.QuantizationParameters.IsUsingQMatrix)
            {
                if (frameInfo.LosslessArray[segmentId])
                {
                    frameInfo.SegmentationParameters.QMLevel[0][segmentId] = 15;
                    frameInfo.SegmentationParameters.QMLevel[1][segmentId] = 15;
                    frameInfo.SegmentationParameters.QMLevel[2][segmentId] = 15;
                }
                else
                {
                    frameInfo.SegmentationParameters.QMLevel[0][segmentId] = frameInfo.QuantizationParameters.QMatrix[(int)Av1Plane.Y];
                    frameInfo.SegmentationParameters.QMLevel[1][segmentId] = frameInfo.QuantizationParameters.QMatrix[(int)Av1Plane.U];
                    frameInfo.SegmentationParameters.QMLevel[2][segmentId] = frameInfo.QuantizationParameters.QMatrix[(int)Av1Plane.V];
                }
            }
        }

        if (frameInfo.CodedLossless)
        {
            DebugGuard.IsFalse(frameInfo.DeltaQParameters.IsPresent, nameof(frameInfo.DeltaQParameters.IsPresent), "No Delta Q parameters are allowed for lossless frame.");
        }

        frameInfo.AllLossless = frameInfo.CodedLossless && frameInfo.FrameSize.FrameWidth == frameInfo.FrameSize.SuperResolutionUpscaledWidth;
        this.ReadLoopFilterParameters(ref reader, planesCount);
        ReadCdefParameters(ref reader, sequenceHeader, frameInfo, planesCount);
        ReadLoopRestorationParameters(ref reader, sequenceHeader, frameInfo, planesCount);
        ReadTransformMode(ref reader, frameInfo);

        frameInfo.ReferenceMode = ReadFrameReferenceMode(ref reader, isIntraFrame);
        ReadSkipModeParameters(ref reader, sequenceHeader, frameInfo, isIntraFrame, frameInfo.ReferenceMode);
        if (isIntraFrame || frameInfo.ErrorResilientMode || !sequenceHeader.EnableWarpedMotion)
        {
            frameInfo.AllowWarpedMotion = false;
        }
        else
        {
            frameInfo.AllowWarpedMotion = reader.ReadBoolean();
        }

        frameInfo.ReducedTransformSet = reader.ReadBoolean();
        ReadGlobalMotionParameters(ref reader, sequenceHeader, frameInfo, isIntraFrame);
        frameInfo.FilmGrainParameters = ReadFilmGrainFilterParameters(ref reader, sequenceHeader, frameInfo);
    }

    private static void SetupPastIndependence(ObuFrameHeader frameInfo)
    {
        // TODO: Initialize the loop filter parameters.
    }

    private static bool IsSegmentationFeatureActive(ObuSegmentationParameters segmentationParameters, int segmentId, ObuSegmentationLevelFeature feature)
        => segmentationParameters.Enabled && segmentationParameters.IsFeatureActive(segmentId, feature);

    private static int GetQIndex(ObuSegmentationParameters segmentationParameters, int segmentId, int baseQIndex)
    {
        if (IsSegmentationFeatureActive(segmentationParameters, segmentId, ObuSegmentationLevelFeature.AlternativeQuantizer))
        {
            int data = segmentationParameters.FeatureData[segmentId, (int)ObuSegmentationLevelFeature.AlternativeQuantizer];
            int qIndex = baseQIndex + data;
            return Av1Math.Clamp(qIndex, 0, Av1Constants.MaxQ);
        }
        else
        {
            return baseQIndex;
        }
    }

    /// <summary>
    /// 5.9.1. General frame header OBU syntax.
    /// </summary>
    private void ReadFrameHeader(ref Av1BitStreamReader reader, ObuHeader header, bool trailingBit)
    {
        int planeCount = this.SequenceHeader!.ColorConfig.IsMonochrome ? 1 : 3;
        int startBitPosition = reader.BitPosition;
        this.ReadUncompressedFrameHeader(ref reader, header, planeCount);
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
    private void ReadTileGroup(ref Av1BitStreamReader reader, IAv1TileDecoder decoder, ObuHeader header, out bool isLastTileGroup)
    {
        ObuSequenceHeader sequenceHeader = this.SequenceHeader!;
        ObuFrameHeader frameInfo = this.FrameHeader!;
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

        bool noIbc = !frameInfo.AllowIntraBlockCopy;
        bool doLoopFilter = noIbc && (frameInfo.LoopFilterParameters.FilterLevel[0] != 0 || frameInfo.LoopFilterParameters.FilterLevel[1] != 0);
        bool doCdef = noIbc && (!frameInfo.CodedLossless &&
            (frameInfo.CdefParameters.BitCount != 0 ||
            frameInfo.CdefParameters.YStrength[0] != 0 ||
            frameInfo.CdefParameters.UvStrength[0] != 0));
        bool doLoopRestoration = noIbc &&
            (frameInfo.LoopRestorationParameters[(int)Av1Plane.Y].Type != ObuRestorationType.None ||
            frameInfo.LoopRestorationParameters[(int)Av1Plane.U].Type != ObuRestorationType.None ||
            frameInfo.LoopRestorationParameters[(int)Av1Plane.V].Type != ObuRestorationType.None);

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
            decoder.DecodeTile(tileData, tileNum);
        }

        if (tileGroupEnd != tileCount - 1)
        {
            return;
        }

        decoder.FinishDecodeTiles(doCdef, doLoopRestoration);
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
    private static void ReadFrameDeltaQParameters(ref Av1BitStreamReader reader, ObuFrameHeader frameInfo)
    {
        frameInfo.DeltaQParameters.Resolution = 0;
        frameInfo.DeltaQParameters.IsPresent = false;
        if (frameInfo.QuantizationParameters.BaseQIndex > 0)
        {
            frameInfo.DeltaQParameters.IsPresent = reader.ReadBoolean();
        }

        if (frameInfo.DeltaQParameters.IsPresent)
        {
            frameInfo.DeltaQParameters.Resolution = (int)reader.ReadLiteral(2);
        }
    }

    /// <summary>
    /// 5.9.18. Loop filter delta parameters syntax.
    /// </summary>
    private static void ReadFrameDeltaLoopFilterParameters(ref Av1BitStreamReader reader, ObuFrameHeader frameInfo)
    {
        frameInfo.DeltaLoopFilterParameters.IsPresent = false;
        frameInfo.DeltaLoopFilterParameters.Resolution = 0;
        frameInfo.DeltaLoopFilterParameters.IsMulti = false;
        if (frameInfo.DeltaQParameters.IsPresent)
        {
            if (!frameInfo.AllowIntraBlockCopy)
            {
                frameInfo.DeltaLoopFilterParameters.IsPresent = reader.ReadBoolean();
            }

            if (frameInfo.DeltaLoopFilterParameters.IsPresent)
            {
                frameInfo.DeltaLoopFilterParameters.Resolution = (int)reader.ReadLiteral(2);
                frameInfo.DeltaLoopFilterParameters.IsMulti = reader.ReadBoolean();
            }
        }
    }

    /// <summary>
    /// 5.9.12. Quantization params syntax.
    /// </summary>
    private static void ReadQuantizationParameters(ref Av1BitStreamReader reader, ObuQuantizationParameters quantParams, ObuColorConfig colorInfo, int planesCount)
    {
        quantParams.BaseQIndex = (int)reader.ReadLiteral(8);
        quantParams.DeltaQDc[(int)Av1Plane.Y] = ReadDeltaQ(ref reader);
        quantParams.DeltaQAc[(int)Av1Plane.Y] = 0;
        if (planesCount > 1)
        {
            bool areUvDeltaDifferent = false;
            if (colorInfo.HasSeparateUvDelta)
            {
                areUvDeltaDifferent = reader.ReadBoolean();
            }

            quantParams.DeltaQDc[(int)Av1Plane.U] = ReadDeltaQ(ref reader);
            quantParams.DeltaQAc[(int)Av1Plane.U] = ReadDeltaQ(ref reader);
            if (areUvDeltaDifferent)
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
    private static void ReadSegmentationParameters(ref Av1BitStreamReader reader, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameInfo, int planesCount)
    {
        frameInfo.SegmentationParameters.Enabled = reader.ReadBoolean();

        if (frameInfo.SegmentationParameters.Enabled)
        {
            if (frameInfo.PrimaryReferenceFrame == PrimaryRefNone)
            {
                frameInfo.SegmentationParameters.SegmentationUpdateMap = 1;
                frameInfo.SegmentationParameters.SegmentationTemporalUpdate = 0;
                frameInfo.SegmentationParameters.SegmentationUpdateData = 1;
            }
            else
            {
                frameInfo.SegmentationParameters.SegmentationUpdateMap = reader.ReadBoolean() ? 1 : 0;
                if (frameInfo.SegmentationParameters.SegmentationUpdateMap == 1)
                {
                    frameInfo.SegmentationParameters.SegmentationTemporalUpdate = reader.ReadBoolean() ? 1 : 0;
                }

                frameInfo.SegmentationParameters.SegmentationUpdateData = reader.ReadBoolean() ? 1 : 0;
            }

            if (frameInfo.SegmentationParameters.SegmentationUpdateData == 1)
            {
                for (int i = 0; i < MaxSegments; i++)
                {
                    for (int j = 0; j < SegLvlMax; j++)
                    {
                        int featureValue = 0;
                        bool featureEnabled = reader.ReadBoolean();
                        frameInfo.SegmentationParameters.FeatureEnabled[i, j] = featureEnabled;
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

                        frameInfo.SegmentationParameters.FeatureData[i, j] = clippedValue;
                    }
                }
            }
        }
        else
        {
            for (int i = 0; i < MaxSegments; i++)
            {
                for (int j = 0; j < SegLvlMax; j++)
                {
                    frameInfo.SegmentationParameters.FeatureEnabled[i, j] = false;
                    frameInfo.SegmentationParameters.FeatureData[i, j] = 0;
                }
            }
        }

        frameInfo.SegmentationParameters.SegmentIdPrecedesSkip = false;
        frameInfo.SegmentationParameters.LastActiveSegmentId = 0;
        for (int i = 0; i < MaxSegments; i++)
        {
            for (int j = 0; j < SegLvlMax; j++)
            {
                if (frameInfo.SegmentationParameters.FeatureEnabled[i, j])
                {
                    frameInfo.SegmentationParameters.LastActiveSegmentId = i;
                    if (j >= SegLvlRefFrame)
                    {
                        frameInfo.SegmentationParameters.SegmentIdPrecedesSkip = true;
                    }
                }
            }
        }
    }

    /// <summary>
    /// 5.9.11. Loop filter params syntax
    /// </summary>
    private void ReadLoopFilterParameters(ref Av1BitStreamReader reader, int planesCount)
    {
        ObuFrameHeader frameInfo = this.FrameHeader!;
        frameInfo.LoopFilterParameters.FilterLevel = new int[2];
        if (frameInfo.CodedLossless || frameInfo.AllowIntraBlockCopy)
        {
            return;
        }

        frameInfo.LoopFilterParameters.FilterLevel[0] = (int)reader.ReadLiteral(6);
        frameInfo.LoopFilterParameters.FilterLevel[1] = (int)reader.ReadLiteral(6);

        if (planesCount > 1)
        {
            if (frameInfo.LoopFilterParameters.FilterLevel[0] > 0 || frameInfo.LoopFilterParameters.FilterLevel[1] > 0)
            {
                frameInfo.LoopFilterParameters.FilterLevelU = (int)reader.ReadLiteral(6);
                frameInfo.LoopFilterParameters.FilterLevelV = (int)reader.ReadLiteral(6);
            }
        }

        frameInfo.LoopFilterParameters.SharpnessLevel = (int)reader.ReadLiteral(3);
        frameInfo.LoopFilterParameters.ReferenceDeltaModeEnabled = reader.ReadBoolean();
        if (frameInfo.LoopFilterParameters.ReferenceDeltaModeEnabled)
        {
            frameInfo.LoopFilterParameters.ReferenceDeltaModeUpdate = reader.ReadBoolean();
            if (frameInfo.LoopFilterParameters.ReferenceDeltaModeUpdate)
            {
                for (int i = 0; i < Av1Constants.TotalReferencesPerFrame; i++)
                {
                    if (reader.ReadBoolean())
                    {
                        frameInfo.LoopFilterParameters.ReferenceDeltas[i] = reader.ReadSignedFromUnsigned(7);
                    }
                }

                for (int i = 0; i < 2; i++)
                {
                    if (reader.ReadBoolean())
                    {
                        frameInfo.LoopFilterParameters.ModeDeltas[i] = reader.ReadSignedFromUnsigned(7);
                    }
                }
            }
        }
    }

    /// <summary>
    /// 5.9.21. TX mode syntax.
    /// </summary>
    private static void ReadTransformMode(ref Av1BitStreamReader reader, ObuFrameHeader frameInfo)
    {
        if (frameInfo.CodedLossless)
        {
            frameInfo.TransformMode = Av1TransformMode.Only4x4;
        }
        else
        {
            if (reader.ReadBoolean())
            {
                frameInfo.TransformMode = Av1TransformMode.Select;
            }
            else
            {
                frameInfo.TransformMode = Av1TransformMode.Largest;
            }
        }
    }

    /// <summary>
    /// See section 5.9.20. Loop restoration params syntax.
    /// </summary>
    private static void ReadLoopRestorationParameters(ref Av1BitStreamReader reader, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameInfo, int planesCount)
    {
        _ = planesCount;
        if (frameInfo.CodedLossless || frameInfo.AllowIntraBlockCopy || !sequenceHeader.EnableRestoration)
        {
            frameInfo.LoopRestorationParameters[0] = new ObuLoopRestorationParameters();
            frameInfo.LoopRestorationParameters[1] = new ObuLoopRestorationParameters();
            frameInfo.LoopRestorationParameters[2] = new ObuLoopRestorationParameters();
            return;
        }

        bool usesLoopRestoration = false;
        bool usesChromaLoopRestoration = false;
        for (int i = 0; i < planesCount; i++)
        {
            frameInfo.LoopRestorationParameters[i].Type = (ObuRestorationType)reader.ReadLiteral(2);
            if (frameInfo.LoopRestorationParameters[i].Type != ObuRestorationType.None)
            {
                usesLoopRestoration = true;
                if (i > 0)
                {
                    usesChromaLoopRestoration = true;
                }
            }
        }

        if (usesLoopRestoration)
        {
            uint loopRestorationShift = reader.ReadLiteral(1);
            if (sequenceHeader.Use128x128SuperBlock)
            {
                loopRestorationShift++;
            }
            else
            {
                if (reader.ReadBoolean())
                {
                    loopRestorationShift += reader.ReadLiteral(1);
                }
            }

            frameInfo.LoopRestorationParameters[0].Size = Av1Constants.RestorationMaxTileSize >> (int)(2 - loopRestorationShift);
            int uvShift = 0;
            if (sequenceHeader.ColorConfig.SubSamplingX && sequenceHeader.ColorConfig.SubSamplingY && usesChromaLoopRestoration)
            {
                uvShift = (int)reader.ReadLiteral(1);
            }

            frameInfo.LoopRestorationParameters[1].Size = frameInfo.LoopRestorationParameters[0].Size >> uvShift;
            frameInfo.LoopRestorationParameters[2].Size = frameInfo.LoopRestorationParameters[0].Size >> uvShift;
        }
    }

    /// <summary>
    /// See section 5.9.19. CDEF params syntax.
    /// </summary>
    private static void ReadCdefParameters(ref Av1BitStreamReader reader, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameInfo, int planesCount)
    {
        ObuConstraintDirectionalEnhancementFilterParameters cdefInfo = frameInfo.CdefParameters;
        if (frameInfo.CodedLossless || frameInfo.AllowIntraBlockCopy || sequenceHeader.CdefLevel == 0)
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
        for (int i = 0; i < (1 << frameInfo.CdefParameters.BitCount); i++)
        {
            cdefInfo.YStrength[i] = (int)reader.ReadLiteral(6);

            if (planesCount > 1)
            {
                cdefInfo.UvStrength[i] = (int)reader.ReadLiteral(6);
            }
        }
    }

    /// <summary>
    /// 5.9.24. Global motion params syntax.
    /// </summary>
    private static void ReadGlobalMotionParameters(ref Av1BitStreamReader reader, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameInfo, bool isIntraFrame)
    {
        if (isIntraFrame)
        {
            return;
        }

        // Not applicable for INTRA frames.
        throw new NotImplementedException();
    }

    /// <summary>
    /// 5.9.23. Frame reference mode syntax
    /// </summary>
    private static ObuReferenceMode ReadFrameReferenceMode(ref Av1BitStreamReader reader, bool isIntraFrame)
    {
        if (isIntraFrame)
        {
            return ObuReferenceMode.SingleReference;
        }

        return (ObuReferenceMode)reader.ReadLiteral(1);
    }

    /// <summary>
    /// 5.11.10. Skip mode syntax.
    /// </summary>
    private static void ReadSkipModeParameters(ref Av1BitStreamReader reader, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameInfo, bool isIntraFrame, ObuReferenceMode referenceSelect)
    {
        if (isIntraFrame || referenceSelect == ObuReferenceMode.ReferenceModeSelect || !sequenceHeader.OrderHintInfo.EnableOrderHint)
        {
            frameInfo.SkipModeParameters.SkipModeAllowed = false;
        }
        else
        {
            // Not applicable for INTRA frames.
        }

        if (frameInfo.SkipModeParameters.SkipModeAllowed)
        {
            frameInfo.SkipModeParameters.SkipModeFlag = reader.ReadBoolean();
        }
        else
        {
            frameInfo.SkipModeParameters.SkipModeFlag = false;
        }
    }

    /// <summary>
    /// 5.9.30. Film grain params syntax.
    /// </summary>
    private static ObuFilmGrainParameters ReadFilmGrainFilterParameters(ref Av1BitStreamReader reader, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameInfo)
    {
        ObuFilmGrainParameters grainParams = new();
        if (!sequenceHeader.AreFilmGrainingParametersPresent || (!frameInfo.ShowFrame && !frameInfo.ShowableFrame))
        {
            return grainParams;
        }

        grainParams.ApplyGrain = reader.ReadBoolean();
        if (!grainParams.ApplyGrain)
        {
            return grainParams;
        }

        grainParams.GrainSeed = reader.ReadLiteral(16);

        if (frameInfo.FrameType == ObuFrameType.InterFrame)
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
            grainParams.CbOffset = reader.ReadLiteral(8);
        }

        if (grainParams.NumCrPoints != 0)
        {
            grainParams.CrMult = reader.ReadLiteral(8);
            grainParams.CrLumaMult = reader.ReadLiteral(8);
            grainParams.CrOffset = reader.ReadLiteral(8);
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
