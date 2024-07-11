// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

internal class ObuWriter
{
    /// <summary>
    /// Encode a single frame into OBU's.
    /// </summary>
    public static void Write(Stream stream, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameInfo)
    {
        MemoryStream bufferStream = new(100);
        Av1BitStreamWriter writer = new(bufferStream);
        WriteObuHeaderAndSize(stream, ObuType.TemporalDelimiter, [], 0);

        WriteSequenceHeader(ref writer, sequenceHeader);
        writer.Flush();
        WriteObuHeaderAndSize(stream, ObuType.SequenceHeader, bufferStream.GetBuffer(), (int)bufferStream.Position);

        bufferStream.Position = 0;
        WriteFrameHeader(ref writer, sequenceHeader, frameInfo, true);
        writer.Flush();
        WriteObuHeaderAndSize(stream, ObuType.FrameHeader, bufferStream.GetBuffer(), (int)bufferStream.Position);

        bufferStream.Position = 0;
        WriteTileGroup(ref writer, frameInfo.TilesInfo);
        writer.Flush();
        WriteObuHeaderAndSize(stream, ObuType.TileGroup, bufferStream.GetBuffer(), (int)bufferStream.Position);
    }

    private static void WriteObuHeader(ref Av1BitStreamWriter writer, ObuType type)
    {
        writer.WriteBoolean(false); // Forbidden bit
        writer.WriteLiteral((uint)type, 4);
        writer.WriteBoolean(false); // Extension
        writer.WriteBoolean(true); // HasSize
        writer.WriteBoolean(false); // Reserved
    }

    /// <summary>
    /// Read OBU header and size.
    /// </summary>
    private static void WriteObuHeaderAndSize(Stream stream, ObuType type, Span<byte> payload, int length)
    {
        Av1BitStreamWriter writer = new(stream);
        WriteObuHeader(ref writer, type);
        writer.WriteLittleEndianBytes128((uint)length);
        stream.Write(payload, 0, length);
    }

    /// <summary>
    /// Write trsainling bits to end on a byte boundary, these trailing bits start with a 1 and end with 0s.
    /// </summary>
    /// <remarks>Write an additional byte, if already byte aligned before.</remarks>
    private static void WriteTrailingBits(ref Av1BitStreamWriter writer)
    {
        int bitsBeforeAlignment = 8 - (writer.BitPosition & 0x7);
        writer.WriteLiteral(0, bitsBeforeAlignment);
    }

    private static void AlignToByteBoundary(ref Av1BitStreamWriter writer)
    {
        while ((writer.BitPosition & 0x7) > 0)
        {
            writer.WriteBoolean(false);
        }
    }

    private static bool IsValidObuType(ObuType type) => type switch
    {
        ObuType.SequenceHeader or ObuType.TemporalDelimiter or ObuType.FrameHeader or
        ObuType.TileGroup or ObuType.Metadata or ObuType.Frame or ObuType.RedundantFrameHeader or
        ObuType.TileList or ObuType.Padding => true,
        _ => false,
    };

    private static void WriteSequenceHeader(ref Av1BitStreamWriter writer, ObuSequenceHeader sequenceHeader)
    {
        writer.WriteLiteral((uint)sequenceHeader.SequenceProfile, 3);
        writer.WriteBoolean(true); // IsStillPicture
        writer.WriteBoolean(true); // IsReducedStillPicture
        writer.WriteLiteral((uint)sequenceHeader.OperatingPoint[0].SequenceLevelIndex, Av1Constants.LevelBits);

        // Frame width and Height
        writer.WriteLiteral((uint)sequenceHeader.FrameWidthBits - 1, 4);
        writer.WriteLiteral((uint)sequenceHeader.FrameHeightBits - 1, 4);
        writer.WriteLiteral((uint)sequenceHeader.MaxFrameWidth - 1, sequenceHeader.FrameWidthBits);
        writer.WriteLiteral((uint)sequenceHeader.MaxFrameHeight - 1, sequenceHeader.FrameHeightBits);

        // Video related flags removed
        writer.WriteBoolean(sequenceHeader.Use128x128Superblock);
        writer.WriteBoolean(sequenceHeader.EnableFilterIntra);
        writer.WriteBoolean(sequenceHeader.EnableIntraEdgeFilter);

        // Video related flags removed
        writer.WriteBoolean(sequenceHeader.EnableSuperResolution);
        writer.WriteBoolean(sequenceHeader.EnableCdef);
        writer.WriteBoolean(sequenceHeader.EnableRestoration);
        WriteColorConfig(ref writer, sequenceHeader);
        writer.WriteBoolean(sequenceHeader.AreFilmGrainingParametersPresent);
    }

    private static void WriteColorConfig(ref Av1BitStreamWriter writer, ObuSequenceHeader sequenceHeader)
    {
        ObuColorConfig colorConfig = new();
        WriteBitDepth(ref writer, colorConfig, sequenceHeader);
        if (sequenceHeader.SequenceProfile != ObuSequenceProfile.High)
        {
            writer.WriteBoolean(colorConfig.IsMonochrome);
        }

        writer.WriteBoolean(false); // colorConfig.IsColorDescriptionPresent
        if (colorConfig.IsMonochrome)
        {
            writer.WriteBoolean(colorConfig.ColorRange);
            return;
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
            writer.WriteBoolean(colorConfig.ColorRange);
            if (sequenceHeader.SequenceProfile == ObuSequenceProfile.Professional && colorConfig.BitDepth == 12)
            {
                writer.WriteBoolean(colorConfig.SubSamplingX);
                if (colorConfig.SubSamplingX)
                {
                    writer.WriteBoolean(colorConfig.SubSamplingY);
                }
            }

            if (colorConfig.SubSamplingX && colorConfig.SubSamplingY)
            {
                writer.WriteLiteral((uint)colorConfig.ChromaSamplePosition, 2);
            }
        }

        writer.WriteBoolean(colorConfig.HasSeparateUvDelta);
    }

    private static void WriteBitDepth(ref Av1BitStreamWriter writer, ObuColorConfig colorConfig, ObuSequenceHeader sequenceHeader)
    {
        bool hasHighBitDepth = colorConfig.BitDepth > 8;
        writer.WriteBoolean(hasHighBitDepth);
        if (sequenceHeader.SequenceProfile == ObuSequenceProfile.Professional && hasHighBitDepth)
        {
            writer.WriteBoolean(colorConfig.BitDepth == 12);
        }
    }

    private static void WriteSuperResolutionParameters(ref Av1BitStreamWriter writer, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameInfo)
    {
        bool useSuperResolution = false;
        if (sequenceHeader.EnableSuperResolution)
        {
            writer.WriteBoolean(useSuperResolution);
        }

        if (useSuperResolution)
        {
            writer.WriteLiteral((uint)frameInfo.FrameSize.SuperResolutionDenominator - Av1Constants.SuperResolutionScaleDenominatorMinimum, Av1Constants.SuperResolutionScaleBits);
        }
    }

    private static void WriteRenderSize(ref Av1BitStreamWriter writer, ObuFrameHeader frameInfo)
    {
        bool renderSizeAndFrameSizeDifferent = false;
        writer.WriteBoolean(false);
        if (renderSizeAndFrameSizeDifferent)
        {
            writer.WriteLiteral((uint)frameInfo.FrameSize.RenderWidth - 1, 16);
            writer.WriteLiteral((uint)frameInfo.FrameSize.RenderHeight - 1, 16);
        }
    }

    private static void WriteFrameSizeWithReferences(ref Av1BitStreamWriter writer, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameInfo, bool frameSizeOverrideFlag)
    {
        bool foundReference = false;
        for (int i = 0; i < Av1Constants.ReferencesPerFrame; i++)
        {
            writer.WriteBoolean(foundReference);
            if (foundReference)
            {
                // Take values over from reference frame
                break;
            }
        }

        if (!foundReference)
        {
            WriteFrameSize(ref writer, sequenceHeader, frameInfo, frameSizeOverrideFlag);
            WriteRenderSize(ref writer, frameInfo);
        }
        else
        {
            WriteSuperResolutionParameters(ref writer, sequenceHeader, frameInfo);
        }
    }

    private static void WriteFrameSize(ref Av1BitStreamWriter writer, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameInfo, bool frameSizeOverrideFlag)
    {
        if (frameSizeOverrideFlag)
        {
            writer.WriteLiteral((uint)frameInfo.FrameSize.FrameWidth - 1, sequenceHeader.FrameWidthBits + 1);
            writer.WriteLiteral((uint)frameInfo.FrameSize.FrameHeight - 1, sequenceHeader.FrameHeightBits + 1);
        }

        WriteSuperResolutionParameters(ref writer, sequenceHeader, frameInfo);
    }

    private static void WriteTileInfo(ref Av1BitStreamWriter writer, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameInfo, ObuTileGroupHeader tileInfo)
    {
        int superBlockColumnCount;
        int superBlockRowCount;
        int superBlockShift;
        if (sequenceHeader.Use128x128Superblock)
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
        tileInfo.MinLog2TileColumnCount = ObuReader.TileLog2(tileInfo.MaxTileWidthSuperBlock, superBlockColumnCount);
        tileInfo.MaxLog2TileColumnCount = ObuReader.TileLog2(1, Math.Min(superBlockColumnCount, Av1Constants.MaxTileColumnCount));
        tileInfo.MaxLog2TileRowCount = ObuReader.TileLog2(1, Math.Min(superBlockRowCount, Av1Constants.MaxTileRowCount));
        tileInfo.MinLog2TileCount = Math.Max(tileInfo.MinLog2TileColumnCount, ObuReader.TileLog2(maxTileAreaOfSuperBlock, superBlockColumnCount * superBlockRowCount));

        writer.WriteBoolean(tileInfo.HasUniformTileSpacing);
        if (tileInfo.HasUniformTileSpacing)
        {
            for (int i = 0; i < tileInfo.TileColumnCountLog2; i++)
            {
                writer.WriteBoolean(true);
            }

            if (tileInfo.TileColumnCountLog2 < tileInfo.MaxLog2TileColumnCount)
            {
                writer.WriteBoolean(false);
            }

            for (int i = 0; i < tileInfo.TileRowCountLog2; i++)
            {
                writer.WriteBoolean(true);
            }

            if (tileInfo.TileRowCountLog2 < tileInfo.MaxLog2TileRowCount)
            {
                writer.WriteBoolean(false);
            }
        }
        else
        {
            int startSuperBlock = 0;
            int i = 0;
            for (; startSuperBlock < superBlockColumnCount; i++)
            {
                uint widthInSuperBlocks = (uint)((tileInfo.TileColumnStartModeInfo[i] >> superBlockShift) - startSuperBlock);
                uint maxWidth = (uint)Math.Min(superBlockColumnCount - startSuperBlock, tileInfo.MaxTileWidthSuperBlock);
                writer.WriteNonSymmetric(widthInSuperBlocks - 1, maxWidth);
                startSuperBlock += (int)widthInSuperBlocks;
            }

            if (startSuperBlock != superBlockColumnCount)
            {
                throw new ImageFormatException("Super block tiles width does not add up to total width.");
            }

            startSuperBlock = 0;
            for (i = 0; startSuperBlock < superBlockRowCount; i++)
            {
                uint heightInSuperBlocks = (uint)((tileInfo.TileRowStartModeInfo[i] >> superBlockShift) - startSuperBlock);
                uint maxHeight = (uint)Math.Min(superBlockRowCount - startSuperBlock, tileInfo.MaxTileHeightSuperBlock);
                writer.WriteNonSymmetric(heightInSuperBlocks - 1, maxHeight);
                startSuperBlock += (int)heightInSuperBlocks;
            }

            if (startSuperBlock != superBlockRowCount)
            {
                throw new ImageFormatException("Super block tiles height does not add up to total height.");
            }
        }

        if (tileInfo.TileColumnCountLog2 > 0 || tileInfo.TileRowCountLog2 > 0)
        {
            writer.WriteLiteral(tileInfo.ContextUpdateTileId, tileInfo.TileRowCountLog2 + tileInfo.TileColumnCountLog2);
            writer.WriteLiteral((uint)tileInfo.TileSizeBytes - 1, 2);
        }
    }

    private static void WriteUncompressedFrameHeader(ref Av1BitStreamWriter writer, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameInfo, int planesCount)
    {
        uint previousFrameId = 0;
        bool isIntraFrame = true;
        int idLength = sequenceHeader.FrameIdLength - 1 + sequenceHeader.DeltaFrameIdLength - 2 + 3;
        writer.WriteBoolean(frameInfo.DisableCdfUpdate);
        if (frameInfo.AllowScreenContentTools)
        {
            writer.WriteBoolean(frameInfo.AllowScreenContentTools);
        }

        if (frameInfo.AllowScreenContentTools)
        {
            if (sequenceHeader.ForceIntegerMotionVector == 1)
            {
                writer.WriteBoolean(frameInfo.ForceIntegerMotionVector);
            }
        }

        bool havePreviousFrameId = !(frameInfo.FrameType == ObuFrameType.KeyFrame && frameInfo.ShowFrame);
        if (havePreviousFrameId)
        {
            previousFrameId = frameInfo.CurrentFrameId;
        }

        if (sequenceHeader.IsFrameIdNumbersPresent)
        {
            writer.WriteLiteral(frameInfo.CurrentFrameId, idLength);
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

        writer.WriteLiteral(frameInfo.OrderHint, sequenceHeader.OrderHintInfo.OrderHintBits);

        if (!isIntraFrame && !frameInfo.ErrorResilientMode)
        {
            writer.WriteLiteral(frameInfo.PrimaryReferenceFrame, Av1Constants.PimaryReferenceBits);
        }

        // Skipping, as no decoder info model present
        frameInfo.AllowHighPrecisionMotionVector = false;
        frameInfo.UseReferenceFrameMotionVectors = false;
        frameInfo.AllowIntraBlockCopy = false;
        if (frameInfo.FrameType != ObuFrameType.SwitchFrame && !(frameInfo.FrameType == ObuFrameType.KeyFrame && frameInfo.ShowFrame))
        {
            writer.WriteLiteral(frameInfo.RefreshFrameFlags, 8);
        }

        if (isIntraFrame)
        {
            WriteFrameSize(ref writer, sequenceHeader, frameInfo, false);
            WriteRenderSize(ref writer, frameInfo);
            if (frameInfo.AllowScreenContentTools && frameInfo.FrameSize.RenderWidth != 0)
            {
                if (frameInfo.FrameSize.FrameWidth == frameInfo.FrameSize.SuperResolutionUpscaledWidth)
                {
                    writer.WriteBoolean(frameInfo.AllowIntraBlockCopy);
                }
            }
        }

        if (frameInfo.PrimaryReferenceFrame == Av1Constants.PrimaryReferenceFrameNone)
        {
            SetupPastIndependence(frameInfo);
        }

        // GenerateNextReferenceFrameMap(sequenceHeader, frameInfo);
        WriteTileInfo(ref writer, sequenceHeader, frameInfo, frameInfo.TilesInfo);
        WriteQuantizationParameters(ref writer, frameInfo.QuantizationParameters, sequenceHeader.ColorConfig, planesCount);
        WriteSegmentationParameters(ref writer, sequenceHeader, frameInfo, planesCount);
        WriteFrameDeltaQParameters(ref writer, frameInfo);
        WriteFrameDeltaLoopFilterParameters(ref writer, frameInfo);

        WriteLoopFilterParameters(ref writer, sequenceHeader, frameInfo, planesCount);
        WriteCdefParameters(ref writer, sequenceHeader, frameInfo, planesCount);
        WriteLoopRestorationParameters(ref writer, sequenceHeader, frameInfo, planesCount);
        WriteTransformMode(ref writer, frameInfo);

        // Not applicable for INTRA frames.
        // WriteFrameReferenceMode(ref writer, frameInfo.ReferenceMode, isIntraFrame);
        // WriteSkipModeParameters(ref writer, sequenceHeader, frameInfo, isIntraFrame, frameInfo.ReferenceMode);
        writer.WriteBoolean(frameInfo.UseReducedTransformSet);

        // Not applicable for INTRA frames.
        // WriteGlobalMotionParameters(ref writer, sequenceHeader, frameInfo, isIntraFrame);
        WriteFilmGrainFilterParameters(ref writer, frameInfo.FilmGrainParameters);
    }

    private static void SetupPastIndependence(ObuFrameHeader frameInfo)
    {
        // TODO: Initialize the loop filter parameters.
    }

    private static bool IsSegmentationFeatureActive(ObuSegmentationParameters segmentationParameters, int segmentId, ObuSegmentationLevelFeature feature)
        => segmentationParameters.Enabled && segmentationParameters.FeatureEnabled[segmentId, (int)feature];

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

    private static int WriteFrameHeader(ref Av1BitStreamWriter writer, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameInfo, bool writeTrailingBits)
    {
        int planeCount = sequenceHeader.ColorConfig.IsMonochrome ? 1 : 3;
        int startBitPosition = writer.BitPosition;
        WriteUncompressedFrameHeader(ref writer, sequenceHeader, frameInfo, planeCount);
        if (writeTrailingBits)
        {
            WriteTrailingBits(ref writer);
        }

        AlignToByteBoundary(ref writer);

        int endPosition = writer.BitPosition;
        int headerBytes = (endPosition - startBitPosition) / 8;
        return headerBytes;
    }

    private static int WriteTileGroup(ref Av1BitStreamWriter writer, ObuTileGroupHeader tileInfo)
    {
        int tileCount = tileInfo.TileColumnCount * tileInfo.TileRowCount;
        int startBitPosition = writer.BitPosition;
        bool tileStartAndEndPresentFlag = tileCount != 0;
        writer.WriteBoolean(tileStartAndEndPresentFlag);

        uint tileGroupStart = 0U;
        uint tileGroupEnd = (uint)tileCount - 1U;
        if (tileCount != 1)
        {
            int tileBits = Av1Math.Log2(tileInfo.TileColumnCount) + Av1Math.Log2(tileInfo.TileRowCount);
            writer.WriteLiteral(tileGroupStart, tileBits);
            writer.WriteLiteral(tileGroupEnd, tileBits);
        }

        AlignToByteBoundary(ref writer);
        int endBitPosition = writer.BitPosition;
        int headerBytes = (endBitPosition - startBitPosition) / 8;
        return headerBytes;
    }

    private static int WriteDeltaQ(ref Av1BitStreamWriter writer, int deltaQ)
    {
        bool isCoded = deltaQ == 0;
        writer.WriteBoolean(isCoded);
        if (isCoded)
        {
            writer.WriteSignedFromUnsigned(deltaQ, 7);
        }

        return deltaQ;
    }

    private static void WriteFrameDeltaQParameters(ref Av1BitStreamWriter writer, ObuFrameHeader frameInfo)
    {
        if (frameInfo.QuantizationParameters.BaseQIndex > 0)
        {
            writer.WriteBoolean(frameInfo.DeltaQParameters.IsPresent);
        }

        if (frameInfo.DeltaQParameters.IsPresent)
        {
            writer.WriteLiteral((uint)frameInfo.DeltaQParameters.Resolution, 2);
        }
    }

    private static void WriteFrameDeltaLoopFilterParameters(ref Av1BitStreamWriter writer, ObuFrameHeader frameInfo)
    {
        if (frameInfo.DeltaQParameters.IsPresent)
        {
            if (!frameInfo.AllowIntraBlockCopy)
            {
                writer.WriteBoolean(frameInfo.DeltaLoopFilterParameters.IsPresent);
            }

            if (frameInfo.DeltaLoopFilterParameters.IsPresent)
            {
                writer.WriteLiteral((uint)frameInfo.DeltaLoopFilterParameters.Resolution, 2);
                writer.WriteBoolean(frameInfo.DeltaLoopFilterParameters.IsMulti);
            }
        }
    }

    /// <summary>
    /// See section 5.9.12.
    /// </summary>
    private static void WriteQuantizationParameters(ref Av1BitStreamWriter writer, ObuQuantizationParameters quantParams, ObuColorConfig colorInfo, int planesCount)
    {
        writer.WriteLiteral((uint)quantParams.BaseQIndex, 8);
        WriteDeltaQ(ref writer, quantParams.DeltaQDc[(int)Av1Plane.Y]);
        if (planesCount > 1)
        {
            bool areUvDeltaDifferent = false;
            if (colorInfo.HasSeparateUvDelta)
            {
                writer.WriteBoolean(colorInfo.HasSeparateUvDelta);
            }

            WriteDeltaQ(ref writer, quantParams.DeltaQDc[(int)Av1Plane.U]);
            WriteDeltaQ(ref writer, quantParams.DeltaQAc[(int)Av1Plane.U]);
            if (areUvDeltaDifferent)
            {
                WriteDeltaQ(ref writer, quantParams.DeltaQDc[(int)Av1Plane.V]);
                WriteDeltaQ(ref writer, quantParams.DeltaQAc[(int)Av1Plane.V]);
            }
        }

        writer.WriteBoolean(quantParams.IsUsingQMatrix);
        if (quantParams.IsUsingQMatrix)
        {
            writer.WriteLiteral((uint)quantParams.QMatrix[(int)Av1Plane.Y], 4);
            writer.WriteLiteral((uint)quantParams.QMatrix[(int)Av1Plane.U], 4);
            if (colorInfo.HasSeparateUvDelta)
            {
                writer.WriteLiteral((uint)quantParams.QMatrix[(int)Av1Plane.V], 4);
            }
        }
    }

    private static void WriteSegmentationParameters(ref Av1BitStreamWriter writer, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameInfo, int planesCount)
    {
        Guard.IsFalse(frameInfo.SegmentationParameters.Enabled, nameof(frameInfo.SegmentationParameters.Enabled), "Segmentatino not supported yet.");
        writer.WriteBoolean(false);
    }

    private static void WriteLoopFilterParameters(ref Av1BitStreamWriter writer, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameInfo, int planesCount)
    {
        _ = writer;
        _ = sequenceHeader;
        _ = frameInfo;
        _ = planesCount;

        // TODO: Parse more stuff.
    }

    private static void WriteTransformMode(ref Av1BitStreamWriter writer, ObuFrameHeader frameInfo)
    {
        if (!frameInfo.CodedLossless)
        {
            writer.WriteBoolean(frameInfo.TransformMode == Av1TransformMode.Select);
        }
    }

    private static void WriteLoopRestorationParameters(ref Av1BitStreamWriter writer, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameInfo, int planesCount)
    {
        _ = writer;
        _ = sequenceHeader;
        _ = frameInfo;
        _ = planesCount;

        // TODO: Parse more stuff.
    }

    private static void WriteCdefParameters(ref Av1BitStreamWriter writer, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameInfo, int planesCount)
    {
        _ = writer;
        _ = sequenceHeader;
        _ = frameInfo;
        _ = planesCount;

        // TODO: Parse more stuff.
    }

    private static void WriteGlobalMotionParameters(ref Av1BitStreamWriter writer, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameInfo, bool isIntraFrame)
    {
        _ = writer;
        _ = sequenceHeader;
        _ = frameInfo;

        // Nothing to be written for INTRA frames.
        Guard.IsTrue(isIntraFrame, nameof(isIntraFrame), "Still picture contains only INTRA frames.");
    }

    private static void WriteFrameReferenceMode(ref Av1BitStreamWriter writer, bool isIntraFrame)
    {
        _ = writer;

        // Nothing to be written for INTRA frames.
        Guard.IsTrue(isIntraFrame, nameof(isIntraFrame), "Still picture contains only INTRA frames.");
    }

    private static void WriteSkipModeParameters(ref Av1BitStreamWriter writer, bool isIntraFrame)
    {
        _ = writer;

        // Nothing to be written for INTRA frames.
        Guard.IsTrue(isIntraFrame, nameof(isIntraFrame), "Still picture contains only INTRA frames.");
    }

    private static void WriteFilmGrainFilterParameters(ref Av1BitStreamWriter writer, ObuFilmGrainParameters filmGrainInfo)
    {
        _ = writer;
        _ = filmGrainInfo;

        // Film grain filter not supported yet
    }
}
