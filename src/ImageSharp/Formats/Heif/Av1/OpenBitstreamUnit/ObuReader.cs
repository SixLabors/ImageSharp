// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

internal class ObuReader
{
    private static readonly int[] WienerTapsMid = new[] { 3, -7, 15 };
    private static readonly int[] SgrprojXqdMid = new[] { -32, 31 };

    private static ObuHeader ReadObuHeader(Av1BitStreamReader reader)
    {
        ObuHeader header = new();
        if (!reader.ReadBoolean())
        {
            throw new ImageFormatException("Forbidden bit in header should be unset.");
        }

        header.Type = (ObuType)reader.ReadLiteral(4);
        header.HasExtension = reader.ReadBoolean();
        header.HasSize = reader.ReadBoolean();
        if (!reader.ReadBoolean())
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

    private static void ReadObuSize(Av1BitStreamReader reader, out int obuSize)
    {
        ulong rawSize = reader.ReadLittleEndianBytes128(out _);
        if (rawSize > uint.MaxValue)
        {
            throw new ImageFormatException("OBU block too large.");
        }

        obuSize = (int)rawSize;
    }

    /// <summary>
    /// Read OBU header and size.
    /// </summary>
    private static ObuHeader ReadObuHeaderSize(Av1BitStreamReader reader)
    {
        ObuHeader header = ReadObuHeader(reader);
        if (header.HasSize)
        {
            ReadObuSize(reader, out int payloadSize);
            header.PayloadSize = payloadSize;
        }

        return header;
    }

    /// <summary>
    /// Check that the trailing bits start with a 1 and end with 0s.
    /// </summary>
    /// <remarks>Consumes a byte, if already byte aligned before the check.</remarks>
    private static void ReadTrailingBits(Av1BitStreamReader reader)
    {
        int bitsBeforeAlignment = 8 - (reader.BitPosition & 0x7);
        uint trailing = reader.ReadLiteral(bitsBeforeAlignment);
        if (trailing != (1 << (bitsBeforeAlignment - 1)))
        {
            throw new ImageFormatException("Trailing bits not properly formatted.");
        }
    }

    private static void AlignToByteBoundary(Av1BitStreamReader reader)
    {
        while ((reader.BitPosition & 0x7) > 0)
        {
            if (reader.ReadBoolean())
            {
                throw new ImageFormatException("Incorrect byte alignment padding bits.");
            }
        }
    }

    private static void ComputeImageSize(ObuSequenceHeader sequenceHeader, ObuFrameHeader frameInfo)
    {
        frameInfo.ModeInfoColumnCount = 2 * ((frameInfo.FrameSize.FrameWidth + 7) >> 3);
        frameInfo.ModeInfoRowCount = 2 * ((frameInfo.FrameSize.FrameHeight + 7) >> 3);
        frameInfo.ModeInfoStride = Av1Math.AlignPowerOf2(sequenceHeader.MaxFrameWidth, ObuConstants.MaxSuperBlockSizeLog2) >> ObuConstants.ModeInfoSizeLog2;
    }

    private static bool IsValidObuType(ObuType type) => type switch
    {
        ObuType.SequenceHeader or ObuType.TemporalDelimiter or ObuType.FrameHeader or
        ObuType.TileGroup or ObuType.Metadata or ObuType.Frame or ObuType.RedundantFrameHeader or
        ObuType.TileList or ObuType.Padding => true,
        _ => false,
    };

    private static ObuSequenceHeader ReadSequenceHeader(Av1BitStreamReader reader)
    {
        ObuSequenceHeader sequenceHeader = new();
        sequenceHeader.SequenceProfile = (ObuSequenceProfile)reader.ReadLiteral(3);
        if (sequenceHeader.SequenceProfile > ObuConstants.MaxSequenceProfile)
        {
            throw new ImageFormatException("Unknown sequence profile.");
        }

        sequenceHeader.IsStillPicture = reader.ReadBoolean();
        sequenceHeader.IsReducedStillPictureHeader = reader.ReadBoolean();
        if (!sequenceHeader.IsStillPicture || !sequenceHeader.IsReducedStillPictureHeader)
        {
            throw new ImageFormatException("Not a picture header, is this a movie file ??");
        }

        sequenceHeader.TimingInfo = null;
        sequenceHeader.DecoderModelInfoPresentFlag = false;
        sequenceHeader.InitialDisplayDelayPresentFlag = false;
        sequenceHeader.OperatingPoint[0].OperatorIndex = 0;
        sequenceHeader.OperatingPoint[0].SequenceLevelIndex = (int)reader.ReadLiteral(ObuConstants.LevelBits);
        if (!IsValidSequenceLevel(sequenceHeader.OperatingPoint[0].SequenceLevelIndex))
        {
            throw new ImageFormatException("Unknown sequnce level.");
        }

        sequenceHeader.OperatingPoint[0].SequenceTier = 0;
        sequenceHeader.OperatingPoint[0].IsDecoderModelPresent = false;
        sequenceHeader.OperatingPoint[0].IsInitialDisplayDelayPresent = false;

        // Video related flags removed

        // SVT-TODO: int operatingPoint = this.ChooseOperatingPoint();
        // sequenceHeader.OperatingPointIndex = (int)operatingPointIndices[operatingPoint];
        sequenceHeader.FrameWidthBits = (int)reader.ReadLiteral(4) + 1;
        sequenceHeader.FrameHeightBits = (int)reader.ReadLiteral(4) + 1;
        sequenceHeader.MaxFrameWidth = (int)reader.ReadLiteral(sequenceHeader.FrameWidthBits) + 1;
        sequenceHeader.MaxFrameHeight = (int)reader.ReadLiteral(sequenceHeader.FrameHeightBits) + 1;
        sequenceHeader.IsFrameIdNumbersPresent = false;

        // Video related flags removed
        sequenceHeader.Use128x128SuperBlock = reader.ReadBoolean();
        sequenceHeader.SuperBlockSize = sequenceHeader.Use128x128SuperBlock ? Av1BlockSize.Block128x128 : Av1BlockSize.Block64x64;
        sequenceHeader.ModeInfoSize = sequenceHeader.Use128x128SuperBlock ? 32 : 16;
        sequenceHeader.SuperBlockSizeLog2 = sequenceHeader.Use128x128SuperBlock ? 7 : 6;
        sequenceHeader.FilterIntraLevel = (int)reader.ReadLiteral(1);
        sequenceHeader.EnableIntraEdgeFilter = reader.ReadBoolean();
        sequenceHeader.EnableInterIntraCompound = false;
        sequenceHeader.EnableMaskedCompound = false;
        sequenceHeader.EnableWarpedMotion = false;
        sequenceHeader.EnableDualFilter = false;
        sequenceHeader.OrderHintInfo.EnableJointCompound = false;
        sequenceHeader.OrderHintInfo.EnableReferenceFrameMotionVectors = false;
        sequenceHeader.SequenceForceScreenContentTools = 2;
        sequenceHeader.SequenceForceIntegerMotionVector = 2;
        sequenceHeader.OrderHintInfo.OrderHintBits = 0;

        // Video related flags removed
        sequenceHeader.EnableSuperResolution = reader.ReadBoolean();
        sequenceHeader.CdefLevel = (int)reader.ReadLiteral(1);
        sequenceHeader.EnableRestoration = reader.ReadBoolean();
        sequenceHeader.ColorConfig = ReadColorConfig(reader, sequenceHeader);
        sequenceHeader.AreFilmGrainingParametersPresent = reader.ReadBoolean();
        ReadTrailingBits(reader);
        return sequenceHeader;
    }

    private static ObuColorConfig ReadColorConfig(Av1BitStreamReader reader, ObuSequenceHeader sequenceHeader)
    {
        ObuColorConfig colorConfig = new();
        ReadBitDepth(reader, colorConfig, sequenceHeader);
        colorConfig.Monochrome = false;
        if (sequenceHeader.SequenceProfile == ObuSequenceProfile.High)
        {
            colorConfig.Monochrome = reader.ReadBoolean();
        }

        colorConfig.ChannelCount = colorConfig.Monochrome ? 1 : 3;
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
        if (colorConfig.Monochrome)
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
            if (sequenceHeader.SequenceProfile != ObuSequenceProfile.Main)
            {
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
            }

            if (colorConfig.SubSamplingX && colorConfig.SubSamplingY)
            {
                colorConfig.ChromaSamplePosition = (ObuChromoSamplePosition)reader.ReadLiteral(2);
            }
        }

        colorConfig.HasSeparateUvDeltaQ = reader.ReadBoolean();
        return colorConfig;
    }

    private static void ReadBitDepth(Av1BitStreamReader reader, ObuColorConfig colorConfig, ObuSequenceHeader sequenceHeader)
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
    }

    private static void ReadSuperResolutionParameters(Av1BitStreamReader reader, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameInfo)
    {
        bool useSuperResolution = false;
        if (sequenceHeader.EnableSuperResolution)
        {
            useSuperResolution = reader.ReadBoolean();
        }

        if (useSuperResolution)
        {
            frameInfo.FrameSize.SuperResolutionDenominator = (int)reader.ReadLiteral(ObuConstants.SuperResolutionScaleBits) + ObuConstants.SuperResolutionScaleDenominatorMinimum;
        }
        else
        {
            frameInfo.FrameSize.SuperResolutionDenominator = ObuConstants.ScaleNumerator;
        }

        frameInfo.FrameSize.SuperResolutionUpscaledWidth = frameInfo.FrameSize.FrameWidth;
        frameInfo.FrameSize.FrameWidth =
            (frameInfo.FrameSize.SuperResolutionUpscaledWidth * ObuConstants.ScaleNumerator) +
            (frameInfo.FrameSize.SuperResolutionDenominator / 2);

        if (frameInfo.FrameSize.SuperResolutionDenominator != ObuConstants.ScaleNumerator)
        {
            int manWidth = Math.Min(16, frameInfo.FrameSize.SuperResolutionUpscaledWidth);
            frameInfo.FrameSize.FrameWidth = Math.Max(manWidth, frameInfo.FrameSize.FrameWidth);
        }
    }

    private static void ReadRenderSize(Av1BitStreamReader reader, ObuFrameHeader frameInfo)
    {
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

    private static void ReadFrameSizeWithReferences(Av1BitStreamReader reader, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameInfo, bool frameSizeOverrideFlag)
    {
        bool foundReference = false;
        for (int i = 0; i < ObuConstants.ReferencesPerFrame; i++)
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
            ReadFrameSize(reader, sequenceHeader, frameInfo, frameSizeOverrideFlag);
            ReadRenderSize(reader, frameInfo);
        }
        else
        {
            ReadSuperResolutionParameters(reader, sequenceHeader, frameInfo);
            ComputeImageSize(sequenceHeader, frameInfo);
        }
    }

    private static void ReadFrameSize(Av1BitStreamReader reader, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameInfo, bool frameSizeOverrideFlag)
    {
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

        ReadSuperResolutionParameters(reader, sequenceHeader, frameInfo);
        ComputeImageSize(sequenceHeader, frameInfo);
    }

    private static ObuTileInfo ReadTileInfo(Av1BitStreamReader reader, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameInfo)
    {
        ObuTileInfo tileInfo = new();
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
        int maxTileAreaOfSuperBlock = ObuConstants.MaxTileArea >> (2 * superBlockSize);

        tileInfo.MaxTileWidthSuperBlock = ObuConstants.MaxTileWidth >> superBlockSize;
        tileInfo.MaxTileHeightSuperBlock = (ObuConstants.MaxTileArea / ObuConstants.MaxTileWidth) >> superBlockSize;
        tileInfo.MinLog2TileColumnCount = TileLog2(tileInfo.MaxTileWidthSuperBlock, superBlockColumnCount);
        tileInfo.MaxLog2TileColumnCount = TileLog2(1, Math.Min(superBlockColumnCount, ObuConstants.MaxTileColumnCount));
        tileInfo.MaxLog2TileRowCount = TileLog2(1, Math.Min(superBlockRowCount, ObuConstants.MaxTileRowCount));
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

        if (tileInfo.TileColumnCount > ObuConstants.MaxTileColumnCount || tileInfo.TileRowCount > ObuConstants.MaxTileRowCount)
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

    private static void ReadUncompressedFrameHeader(Av1BitStreamReader reader, ObuHeader header, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameInfo, int planesCount)
    {
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

        if (frameInfo.FrameType == ObuFrameType.KeyFrame && frameInfo.ShowFrame)
        {
            frameInfo.ReferenceValid = new bool[ObuConstants.ReferenceFrameCount];
            frameInfo.ReferenceOrderHint = new bool[ObuConstants.ReferenceFrameCount];
            for (int i = 0; i < ObuConstants.ReferenceFrameCount; i++)
            {
                frameInfo.ReferenceValid[i] = false;
                frameInfo.ReferenceOrderHint[i] = false;
            }
        }

        frameInfo.DisableCdfUpdate = reader.ReadBoolean();
        frameInfo.AllowScreenContentTools = sequenceHeader.SequenceForceScreenContentTools == 1;
        if (frameInfo.AllowScreenContentTools)
        {
            frameInfo.AllowScreenContentTools = reader.ReadBoolean();
        }

        if (frameInfo.AllowScreenContentTools)
        {
            if (sequenceHeader.SequenceForceIntegerMotionVector == 1)
            {
                frameInfo.ForeceIntegerMotionVector = reader.ReadBoolean();
            }
            else
            {
                frameInfo.ForceIntegerMotionVector = sequenceHeader.SequenceForceIntegerMotionVector != 0;
            }
        }
        else
        {
            frameInfo.ForeceIntegerMotionVector = false;
        }

        if (isIntraFrame)
        {
            frameInfo.ForeceIntegerMotionVector = true;
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
            for (int i = 0; i < ObuConstants.ReferenceFrameCount; i++)
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

        frameInfo.OrderHint = reader.ReadLiteral(sequenceHeader.OrderHintInfo.OrderHintBits);
        if (isIntraFrame || frameInfo.ErrorResilientMode)
        {
            frameInfo.PrimaryReferenceFrame = ObuConstants.PrimaryReferenceFrameNone;
        }
        else
        {
            frameInfo.PrimaryReferenceFrame = reader.ReadLiteral(ObuConstants.PimaryReferenceBits);
        }

        // Skipping, as no decoder info model present
        frameInfo.AllowHighPrecisionMotionVector = false;
        frameInfo.UseReferenceFrameMotionVectors = false;
        frameInfo.AllowIntraBlockCopy = false;
        if (frameInfo.FrameType == ObuFrameType.SwitchFrame || (frameInfo.FrameType == ObuFrameType.KeyFrame && frameInfo.ShowFrame))
        {
            frameInfo.RefreshFrameFlags = 0xffU;
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
                for (int i = 0; i < ObuConstants.ReferenceFrameCount; i++)
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
            ReadFrameSize(reader, sequenceHeader, frameInfo, frameSizeOverrideFlag);
            ReadRenderSize(reader, frameInfo);
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

        if (frameInfo.PrimaryReferenceFrame == ObuConstants.PrimaryReferenceFrameNone)
        {
            // SetupPastIndependence();
        }

        // GenerateNextReferenceFrameMap(sequenceHeader, frameInfo);
        frameInfo.TilesInfo = ReadTileInfo(reader, sequenceHeader, frameInfo);
        ReadQuantizationParameters(reader, frameInfo.QuantizationParameters, sequenceHeader.ColorConfig, planesCount);
        ReadSegmentationParameters(reader, sequenceHeader, frameInfo, planesCount);
        ReadFrameDeltaQParameters(reader, frameInfo);
        ReadFrameDeltaLoopFilterParameters(reader, frameInfo);

        // SetupSegmentationDequantization();
        Av1MainParseContext mainParseContext = new();
        if (frameInfo.PrimaryReferenceFrame == ObuConstants.PrimaryReferenceFrameNone)
        {
            // ResetParseContext(mainParseContext, frameInfo.QuantizationParameters.BaseQIndex);
        }

        int tilesCount = frameInfo.TilesInfo.TileColumnCount * frameInfo.TilesInfo.TileRowCount;
        frameInfo.CodedLossless = true;
        for (int segmentId = 0; segmentId < ObuConstants.MaxSegmentCount; segmentId++)
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
                    frameInfo.SegmentationParameters.QMLevel[0, segmentId] = 15;
                    frameInfo.SegmentationParameters.QMLevel[1, segmentId] = 15;
                    frameInfo.SegmentationParameters.QMLevel[2, segmentId] = 15;
                }
                else
                {
                    frameInfo.SegmentationParameters.QMLevel[0, segmentId] = frameInfo.QuantizationParameters.QMatrix[(int)Av1Plane.Y];
                    frameInfo.SegmentationParameters.QMLevel[1, segmentId] = frameInfo.QuantizationParameters.QMatrix[(int)Av1Plane.U];
                    frameInfo.SegmentationParameters.QMLevel[2, segmentId] = frameInfo.QuantizationParameters.QMatrix[(int)Av1Plane.V];
                }
            }
        }

        frameInfo.AllLossless = frameInfo.CodedLossless && frameInfo.FrameSize.FrameWidth == frameInfo.FrameSize.SuperResolutionUpscaledWidth;
        ReadLoopFilterParameters(reader, sequenceHeader, frameInfo, planesCount);
        ReadCdefParameters(reader, sequenceHeader, frameInfo, planesCount);
        ReadLoopRestorationParameters(reader, sequenceHeader, frameInfo, planesCount);
        ReadTransformMode(reader, frameInfo);

        frameInfo.ReferenceMode = ReadFrameReferenceMode(reader, isIntraFrame);
        ReadSkipModeParameters(reader, sequenceHeader, frameInfo, isIntraFrame, frameInfo.ReferenceMode);
        if (isIntraFrame || frameInfo.ErrorResilientMode || !sequenceHeader.EnableWarpedMotion)
        {
            frameInfo.AllowWarpedMotion = false;
        }

        frameInfo.ReducedTransformSet = reader.ReadBoolean();
        ReadGlobalMotionParameters(reader, sequenceHeader, frameInfo, isIntraFrame);
        frameInfo.FilmGrainParameters = ReadFilmGrainFilterParameters(reader, sequenceHeader, frameInfo);
    }

    private static bool IsSegmentationFeatureActive(ObuSegmentationParameters segmentationParameters, int segmentId, ObuSegmentationLevelFeature feature)
        => segmentationParameters.SegmentationEnabled && segmentationParameters.FeatureEnabled[segmentId, (int)feature];

    private static int GetQIndex(ObuSegmentationParameters segmentationParameters, int segmentId, int baseQIndex)
    {
        if (IsSegmentationFeatureActive(segmentationParameters, segmentId, ObuSegmentationLevelFeature.AlternativeQuantizer))
        {
            int data = segmentationParameters.FeatureData[segmentId, (int)ObuSegmentationLevelFeature.AlternativeQuantizer];
            int qIndex = baseQIndex + data;
            return Av1Math.Clamp(qIndex, 0, ObuConstants.MaxQ);
        }
        else
        {
            return baseQIndex;
        }
    }

    private static void ReadFrameHeader(Av1BitStreamReader reader, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameInfo, ObuHeader header, bool trailingBit)
    {
        int planeCount = sequenceHeader.ColorConfig.Monochrome ? 1 : 3;
        int startBitPosition = reader.BitPosition;
        ReadUncompressedFrameHeader(reader, header, sequenceHeader, frameInfo, planeCount);
        if (trailingBit)
        {
            ReadTrailingBits(reader);
        }

        AlignToByteBoundary(reader);

        int endPosition = reader.BitPosition;
        int headerBytes = (endPosition - startBitPosition) / 8;
        header.PayloadSize -= headerBytes;
    }

    private static void ReadTileGroup(Av1BitStreamReader reader, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameInfo, ObuTileInfo tileInfo, ObuHeader header, out bool isLastTileGroup)
    {
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
            int tileBits = Av1Math.Log2(tileInfo.TileColumnCount) + Av1Math.Log2(tileInfo.TileRowCount);
            tileGroupStart = (int)reader.ReadLiteral(tileBits);
            tileGroupEnd = (int)reader.ReadLiteral(tileBits);
        }

        isLastTileGroup = (tileGroupEnd + 1) == tileCount;
        AlignToByteBoundary(reader);
        int endBitPosition = reader.BitPosition;
        int headerBytes = (endBitPosition - startBitPosition) / 8;
        header.PayloadSize -= headerBytes;

        bool noIbc = !frameInfo.AllowIntraBlockCopy;
        bool doLoopFilter = noIbc && (frameInfo.LoopFilterParameters.FilterLevel[0] != 0 || frameInfo.LoopFilterParameters.FilterLevel[1] != 0);
        bool doCdef = noIbc && (!frameInfo.CodedLossless &&
            (frameInfo.CdefParameters.BitCount != 0 ||
            frameInfo.CdefParameters.YStrength[0] != 0 ||
            frameInfo.CdefParameters.UVStrength[0] != 0));
        bool doLoopRestoration = noIbc &&
            (frameInfo.LoopRestorationParameters[(int)Av1Plane.Y].FrameRestorationType != ObuRestorationType.RestoreNone ||
            frameInfo.LoopRestorationParameters[(int)Av1Plane.U].FrameRestorationType != ObuRestorationType.RestoreNone ||
            frameInfo.LoopRestorationParameters[(int)Av1Plane.V].FrameRestorationType != ObuRestorationType.RestoreNone);

        for (int tileNum = tileGroupStart; tileNum <= tileGroupEnd; tileNum++)
        {
            int tileRow = tileNum / tileInfo.TileColumnCount;
            int tileColumn = tileNum % tileInfo.TileColumnCount;
            bool isLastTile = tileNum == tileGroupEnd;
            int tileSize = header.PayloadSize;
            if (!isLastTile)
            {
                tileSize = (int)reader.ReadLittleEndian(tileInfo.TileSizeBytes) + 1;
                header.PayloadSize -= tileSize + tileInfo.TileSizeBytes;
            }

            // TODO: Pass more info to the decoder.
            // DecodeTile(sequenceHeader, frameInfo, tileInfo, tileNum);
        }

        if (tileGroupEnd != tileCount - 1)
        {
            return;
        }

        // FinishDecodeTiles(sequenceHeader, frameInfo, doCdef, doLoopRestoration);
    }

    private static int ReadDeltaQ(Av1BitStreamReader reader)
    {
        int deltaQ = 0;
        if (reader.ReadBoolean())
        {
            deltaQ = (int)reader.ReadLiteral(7);
        }

        return deltaQ;
    }

    private static void ReadFrameDeltaQParameters(Av1BitStreamReader reader, ObuFrameHeader frameInfo)
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

    private static void ReadFrameDeltaLoopFilterParameters(Av1BitStreamReader reader, ObuFrameHeader frameInfo)
    {
        frameInfo.DeltaLoopFilterParameters.IsPresent = false;
        frameInfo.DeltaLoopFilterParameters.Resolution = 0;
        frameInfo.DeltaLoopFilterParameters.Multi = false;
        if (frameInfo.DeltaQParameters.IsPresent)
        {
            if (!frameInfo.AllowIntraBlockCopy)
            {
                frameInfo.DeltaLoopFilterParameters.IsPresent = reader.ReadBoolean();
            }

            if (frameInfo.DeltaLoopFilterParameters.IsPresent)
            {
                frameInfo.DeltaLoopFilterParameters.Resolution = (int)reader.ReadLiteral(4);
                frameInfo.DeltaLoopFilterParameters.Multi = reader.ReadBoolean();
            }
        }
    }

    private static void ReadQuantizationParameters(Av1BitStreamReader reader, ObuQuantizationParameters quantParams, ObuColorConfig colorInfo, int planesCount)
    {
        quantParams.BaseQIndex = (int)reader.ReadLiteral(8);
        quantParams.DeltaQDc[(int)Av1Plane.Y] = ReadDeltaQ(reader);
        quantParams.DeltaQAc[(int)Av1Plane.Y] = 0;
        if (planesCount > 1)
        {
            bool areUvDeltaDifferent = false;
            quantParams.DeltaQDc[(int)Av1Plane.U] = ReadDeltaQ(reader);
            quantParams.DeltaQAc[(int)Av1Plane.U] = ReadDeltaQ(reader);
            if (areUvDeltaDifferent)
            {
                quantParams.DeltaQDc[(int)Av1Plane.V] = ReadDeltaQ(reader);
                quantParams.DeltaQAc[(int)Av1Plane.V] = ReadDeltaQ(reader);
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
            if (!colorInfo.HasSeparateUvDeltaQ)
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

    private static void ReadSegmentationParameters(Av1BitStreamReader reader, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameInfo, int planesCount)
    {
        frameInfo.SegmentationParameters.SegmentationEnabled = reader.ReadBoolean();
        if (!frameInfo.SegmentationParameters.SegmentationEnabled)
        {
            // CopyFeatureInfo();
            return;
        }

        // TODO: Parse more stuff.
    }

    private static void ReadLoopFilterParameters(Av1BitStreamReader reader, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameInfo, int planesCount)
    {
        if (frameInfo.CodedLossless || frameInfo.AllowIntraBlockCopy)
        {
            frameInfo.LoopFilterParameters.FilterLevel[0] = 0;
            frameInfo.LoopFilterParameters.FilterLevel[1] = 0;
            return;
        }

        // TODO: Parse more stuff.
    }

    private static void ReadTransformMode(Av1BitStreamReader reader, ObuFrameHeader frameInfo)
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

    private static void ReadLoopRestorationParameters(Av1BitStreamReader reader, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameInfo, int planesCount)
    {
        _ = planesCount;
        if (frameInfo.CodedLossless || frameInfo.AllowIntraBlockCopy || !sequenceHeader.EnableRestoration)
        {
            frameInfo.LoopRestorationParameters[0].FrameRestorationType = ObuRestorationType.RestoreNone;
            frameInfo.LoopRestorationParameters[1].FrameRestorationType = ObuRestorationType.RestoreNone;
            frameInfo.LoopRestorationParameters[2].FrameRestorationType = ObuRestorationType.RestoreNone;
            return;
        }

        // TODO: Parse more stuff.
    }

    private static void ReadCdefParameters(Av1BitStreamReader reader, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameInfo, int planesCount)
    {
        _ = planesCount;
        if (frameInfo.CodedLossless || frameInfo.AllowIntraBlockCopy || sequenceHeader.CdefLevel == 0)
        {
            frameInfo.CdefParameters.BitCount = 0;
            frameInfo.CdefParameters.YStrength[0] = 0;
            frameInfo.CdefParameters.YStrength[4] = 0;
            frameInfo.CdefParameters.UVStrength[0] = 0;
            frameInfo.CdefParameters.UVStrength[4] = 0;
            frameInfo.CdefParameters.Damping = 0;
            return;
        }

        // TODO: Parse more stuff.
    }

    private static void ReadGlobalMotionParameters(Av1BitStreamReader reader, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameInfo, bool isIntraFrame)
    {
        _ = reader;
        _ = sequenceHeader;
        _ = frameInfo;
        if (isIntraFrame)
        {
            return;
        }

        // TODO: Parse more stuff.
    }

    private static ObuReferenceMode ReadFrameReferenceMode(Av1BitStreamReader reader, bool isIntraFrame)
    {
        if (isIntraFrame)
        {
            return ObuReferenceMode.SingleReference;
        }

        return (ObuReferenceMode)reader.ReadLiteral(1);
    }

    private static void ReadSkipModeParameters(Av1BitStreamReader reader, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameInfo, bool isIntraFrame, ObuReferenceMode referenceSelect)
    {
        if (isIntraFrame || referenceSelect == ObuReferenceMode.ReferenceModeSelect || !sequenceHeader.OrderHintInfo.EnableOrderHint)
        {
            frameInfo.SkipModeParameters.SkipModeAllowed = false;
        }
        else
        {
            // TODO: Parse more stuff.
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

    private static ObuFilmGrainParameters ReadFilmGrainFilterParameters(Av1BitStreamReader reader, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameInfo)
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

        // TODO: Implement parsing.
        return grainParams;
    }

    private static bool IsValidSequenceLevel(int sequenceLevelIndex)
        => sequenceLevelIndex is < 24 or 31;

    private static int TileLog2(int blockSize, int target)
    {
        int k;
        for (k = 0; (blockSize << k) < target; k++)
        {
        }

        return k;
    }
}
