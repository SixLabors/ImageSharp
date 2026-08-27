// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

/// <summary>
/// Writes the AV1 open bitstream units required for a single still-image frame.
/// </summary>
internal class ObuWriter
{
    /// <summary>
    /// Writes a temporal delimiter and the supplied sequence and frame OBUs.
    /// </summary>
    /// <param name="configuration">The configuration used to allocate temporary encoding memory.</param>
    /// <param name="stream">The destination stream.</param>
    /// <param name="sequenceHeader">The optional still-picture sequence header.</param>
    /// <param name="frameHeader">The optional intra-frame header.</param>
    /// <param name="tileWriter">The tile writer used when a frame header is supplied.</param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Preserves the existing writer instance contract.")]
    public void WriteAll(Configuration configuration, Stream stream, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameHeader, IAv1TileWriter tileWriter)
    {
        // The allocation expands when necessary; this initial size avoids repeated growth for
        // the small headers and tiles produced by the current still-image encoder.
        int initialBufferSize = 2000;
        AutoExpandingMemory<byte> buffer = new(configuration, initialBufferSize);
        Av1BitStreamWriter writer = new(buffer);
        WriteObuHeaderAndSize(stream, ObuType.TemporalDelimiter, []);

        if (sequenceHeader != null)
        {
            WriteSequenceHeader(ref writer, sequenceHeader);
            int bytesWritten = (writer.BitPosition + 7) >> 3;
            writer.Flush();
            WriteObuHeaderAndSize(stream, ObuType.SequenceHeader, buffer.GetSpan(bytesWritten));
        }

        if (frameHeader != null && sequenceHeader != null)
        {
            WriteFrameHeader(ref writer, sequenceHeader, frameHeader);
            if (frameHeader.TilesInfo != null)
            {
                WriteTileGroup(ref writer, frameHeader.TilesInfo, tileWriter);
            }

            int bytesWritten = (writer.BitPosition + 7) >> 3;
            writer.Flush();
            WriteObuHeaderAndSize(stream, ObuType.Frame, buffer.GetSpan(bytesWritten));
        }
    }

    /// <summary>
    /// Creates a byte-aligned OBU header with an explicit payload-size field and no extension.
    /// </summary>
    /// <param name="type">The OBU payload type.</param>
    /// <returns>The encoded OBU header byte.</returns>
    private static byte WriteObuHeader(ObuType type)
    {
        // The only set fields are the four-bit type and the has-size flag; forbidden,
        // extension, and reserved bits remain zero.
        return (byte)(((byte)type << 3) | 0x02);
    }

    /// <summary>
    /// Writes a complete byte-aligned OBU with a little-endian base-128 payload size.
    /// </summary>
    /// <param name="stream">The destination stream.</param>
    /// <param name="type">The OBU payload type.</param>
    /// <param name="payload">The complete OBU payload.</param>
    private static void WriteObuHeaderAndSize(Stream stream, ObuType type, ReadOnlySpan<byte> payload)
    {
        stream.WriteByte(WriteObuHeader(type));

        // A 32-bit OBU payload length requires at most five base-128 bytes.
        Span<byte> lengthBytes = stackalloc byte[5];
        int lengthLength = Av1BitStreamWriter.GetLittleEndianBytes128((uint)payload.Length, lengthBytes);
        stream.Write(lengthBytes, 0, lengthLength);
        stream.Write(payload);
    }

    /// <summary>
    /// Writes a trailing one bit followed by enough zero bits to reach a byte boundary.
    /// </summary>
    /// <param name="writer">The bit writer receiving the trailing bits.</param>
    /// <remarks>Writes an additional byte when the writer is already byte aligned.</remarks>
    private static void WriteTrailingBits(ref Av1BitStreamWriter writer)
    {
        int bitsBeforeAlignment = 8 - (writer.BitPosition & 0x7);
        writer.WriteLiteral(1U << (bitsBeforeAlignment - 1), bitsBeforeAlignment);
    }

    /// <summary>
    /// Writes zero padding until the output reaches a byte boundary.
    /// </summary>
    /// <param name="writer">The bit writer to align.</param>
    private static void AlignToByteBoundary(ref Av1BitStreamWriter writer)
    {
        while ((writer.BitPosition & 0x7) > 0)
        {
            writer.WriteBoolean(false);
        }
    }

    /// <summary>
    /// Writes a reduced still-picture sequence header.
    /// </summary>
    /// <param name="writer">The bit writer receiving the sequence header.</param>
    /// <param name="sequenceHeader">The sequence header to encode.</param>
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
        WriteTrailingBits(ref writer);
    }

    /// <summary>
    /// Writes the sequence color configuration.
    /// </summary>
    /// <param name="writer">The bit writer receiving the color configuration.</param>
    /// <param name="sequenceHeader">The sequence header containing the color configuration.</param>
    private static void WriteColorConfig(ref Av1BitStreamWriter writer, ObuSequenceHeader sequenceHeader)
    {
        ObuColorConfig colorConfig = sequenceHeader.ColorConfig;
        WriteBitDepth(ref writer, colorConfig, sequenceHeader);
        if (sequenceHeader.SequenceProfile != ObuSequenceProfile.High)
        {
            writer.WriteBoolean(colorConfig.IsMonochrome);
        }

        writer.WriteBoolean(colorConfig.IsColorDescriptionPresent);
        if (colorConfig.IsColorDescriptionPresent)
        {
            writer.WriteLiteral((uint)colorConfig.ColorPrimaries, 8);
            writer.WriteLiteral((uint)colorConfig.TransferCharacteristics, 8);
            writer.WriteLiteral((uint)colorConfig.MatrixCoefficients, 8);
        }

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
            // AV1 fixes this RGB identity-matrix combination to full-range 4:4:4 and omits
            // the range and subsampling fields used by YUV configurations.
            colorConfig.ColorRange = true;
            colorConfig.SubSamplingX = false;
            colorConfig.SubSamplingY = false;
        }
        else
        {
            writer.WriteBoolean(colorConfig.ColorRange);
            if (sequenceHeader.SequenceProfile == ObuSequenceProfile.Professional && colorConfig.BitDepth == Av1BitDepth.TwelveBit)
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

    /// <summary>
    /// Writes the profile-dependent bit-depth flags.
    /// </summary>
    /// <param name="writer">The bit writer receiving the flags.</param>
    /// <param name="colorConfig">The color configuration containing the bit depth.</param>
    /// <param name="sequenceHeader">The sequence header containing the selected profile.</param>
    private static void WriteBitDepth(ref Av1BitStreamWriter writer, ObuColorConfig colorConfig, ObuSequenceHeader sequenceHeader)
    {
        bool hasHighBitDepth = colorConfig.BitDepth > Av1BitDepth.EightBit;
        writer.WriteBoolean(hasHighBitDepth);
        if (sequenceHeader.SequenceProfile == ObuSequenceProfile.Professional && hasHighBitDepth)
        {
            writer.WriteBoolean(colorConfig.BitDepth == Av1BitDepth.TwelveBit);
        }
    }

    /// <summary>
    /// Writes the super-resolution enablement and scale denominator.
    /// </summary>
    /// <param name="writer">The bit writer receiving the super-resolution syntax.</param>
    /// <param name="sequenceHeader">The sequence header controlling super-resolution availability.</param>
    /// <param name="frameHeader">The frame header containing the scale denominator.</param>
    private static void WriteSuperResolutionParameters(ref Av1BitStreamWriter writer, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameHeader)
    {
        bool useSuperResolution = sequenceHeader.EnableSuperResolution &&
            frameHeader.FrameSize.SuperResolutionDenominator != Av1Constants.ScaleNumerator;

        if (sequenceHeader.EnableSuperResolution)
        {
            writer.WriteBoolean(useSuperResolution);
        }

        if (useSuperResolution)
        {
            writer.WriteLiteral((uint)frameHeader.FrameSize.SuperResolutionDenominator - Av1Constants.SuperResolutionScaleDenominatorMinimum, Av1Constants.SuperResolutionScaleBits);
        }
    }

    /// <summary>
    /// Writes the render-size override when display dimensions differ from the upscaled frame.
    /// </summary>
    /// <param name="writer">The bit writer receiving the render-size syntax.</param>
    /// <param name="frameHeader">The frame header containing coded and render dimensions.</param>
    private static void WriteRenderSize(ref Av1BitStreamWriter writer, ObuFrameHeader frameHeader)
    {
        bool renderSizeAndFrameSizeDifferent =
            frameHeader.FrameSize.RenderWidth != frameHeader.FrameSize.SuperResolutionUpscaledWidth ||
            frameHeader.FrameSize.RenderHeight != frameHeader.FrameSize.FrameHeight;

        writer.WriteBoolean(renderSizeAndFrameSizeDifferent);
        if (renderSizeAndFrameSizeDifferent)
        {
            writer.WriteLiteral((uint)frameHeader.FrameSize.RenderWidth - 1, 16);
            writer.WriteLiteral((uint)frameHeader.FrameSize.RenderHeight - 1, 16);
        }
    }

    /// <summary>
    /// Writes an optional frame-size override followed by super-resolution syntax.
    /// </summary>
    /// <param name="writer">The bit writer receiving the frame size.</param>
    /// <param name="sequenceHeader">The sequence header defining dimension field widths.</param>
    /// <param name="frameHeader">The frame header containing the dimensions.</param>
    /// <param name="frameSizeOverrideFlag">A value indicating whether explicit dimensions are written.</param>
    private static void WriteFrameSize(ref Av1BitStreamWriter writer, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameHeader, bool frameSizeOverrideFlag)
    {
        if (frameSizeOverrideFlag)
        {
            writer.WriteLiteral((uint)frameHeader.FrameSize.FrameWidth - 1, sequenceHeader.FrameWidthBits);
            writer.WriteLiteral((uint)frameHeader.FrameSize.FrameHeight - 1, sequenceHeader.FrameHeightBits);
        }

        WriteSuperResolutionParameters(ref writer, sequenceHeader, frameHeader);
    }

    /// <summary>
    /// Writes the frame tile layout.
    /// </summary>
    /// <param name="writer">The bit writer receiving the tile information.</param>
    /// <param name="sequenceHeader">The sequence header defining superblock geometry.</param>
    /// <param name="frameHeader">The frame header containing tile boundaries.</param>
    private static void WriteTileInfo(ref Av1BitStreamWriter writer, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameHeader)
    {
        ObuTileGroupHeader tileInfo = frameHeader.TilesInfo;
        int superblockColumnCount;
        int superblockRowCount;
        int superblockSizeLog2 = sequenceHeader.SuperblockSizeLog2;
        int superblockShift = superblockSizeLog2 - Av1Constants.ModeInfoSizeLog2;
        superblockColumnCount = (frameHeader.ModeInfoColumnCount + sequenceHeader.SuperblockModeInfoSize - 1) >> superblockShift;
        superblockRowCount = (frameHeader.ModeInfoRowCount + sequenceHeader.SuperblockModeInfoSize - 1) >> superblockShift;
        int superBlockSize = superblockShift + 2;
        int maxTileAreaOfSuperBlock = Av1Constants.MaxTileArea >> (2 * superBlockSize);

        tileInfo.MaxTileWidthSuperblock = Av1Constants.MaxTileWidth >> superBlockSize;
        tileInfo.MaxTileHeightSuperblock = (Av1Constants.MaxTileArea / Av1Constants.MaxTileWidth) >> superBlockSize;
        tileInfo.MinLog2TileColumnCount = ObuReader.TileLog2(tileInfo.MaxTileWidthSuperblock, superblockColumnCount);
        tileInfo.MaxLog2TileColumnCount = ObuReader.TileLog2(1, Math.Min(superblockColumnCount, Av1Constants.MaxTileColumnCount));
        tileInfo.MaxLog2TileRowCount = ObuReader.TileLog2(1, Math.Min(superblockRowCount, Av1Constants.MaxTileRowCount));
        tileInfo.MinLog2TileCount = Math.Max(tileInfo.MinLog2TileColumnCount, ObuReader.TileLog2(maxTileAreaOfSuperBlock, superblockColumnCount * superblockRowCount));

        int log2TileColumnCount = ObuReader.TileLog2(1, tileInfo.TileColumnCount);
        int log2TileRowCount = ObuReader.TileLog2(1, tileInfo.TileRowCount);
        tileInfo.TileColumnCountLog2 = log2TileColumnCount;
        tileInfo.TileRowCountLog2 = log2TileRowCount;

        writer.WriteBoolean(tileInfo.HasUniformTileSpacing);
        if (tileInfo.HasUniformTileSpacing)
        {
            // Uniform spaced tiles with power-of-two number of rows and columns
            // tile columns
            int ones = log2TileColumnCount - tileInfo.MinLog2TileColumnCount;
            while (ones-- > 0)
            {
                writer.WriteBoolean(true);
            }

            if (log2TileColumnCount < tileInfo.MaxLog2TileColumnCount)
            {
                writer.WriteBoolean(false);
            }

            // rows
            tileInfo.MinLog2TileRowCount = Math.Max(tileInfo.MinLog2TileCount - log2TileColumnCount, 0);
            ones = log2TileRowCount - tileInfo.MinLog2TileRowCount;
            while (ones-- > 0)
            {
                writer.WriteBoolean(true);
            }

            if (log2TileRowCount < tileInfo.MaxLog2TileRowCount)
            {
                writer.WriteBoolean(false);
            }
        }
        else
        {
            int startSuperBlock = 0;
            int i = 0;
            for (; startSuperBlock < superblockColumnCount; i++)
            {
                uint widthInSuperBlocks = (uint)((tileInfo.TileColumnStartModeInfo[i] >> superblockShift) - startSuperBlock);
                uint maxWidth = (uint)Math.Min(superblockColumnCount - startSuperBlock, tileInfo.MaxTileWidthSuperblock);
                writer.WriteNonSymmetric(widthInSuperBlocks - 1, maxWidth);
                startSuperBlock += (int)widthInSuperBlocks;
            }

            if (startSuperBlock != superblockColumnCount)
            {
                throw new ImageFormatException("Super block tiles width does not add up to total width.");
            }

            startSuperBlock = 0;
            for (i = 0; startSuperBlock < superblockRowCount; i++)
            {
                uint heightInSuperBlocks = (uint)((tileInfo.TileRowStartModeInfo[i] >> superblockShift) - startSuperBlock);
                uint maxHeight = (uint)Math.Min(superblockRowCount - startSuperBlock, tileInfo.MaxTileHeightSuperblock);
                writer.WriteNonSymmetric(heightInSuperBlocks - 1, maxHeight);
                startSuperBlock += (int)heightInSuperBlocks;
            }

            if (startSuperBlock != superblockRowCount)
            {
                throw new ImageFormatException("Super block tiles height does not add up to total height.");
            }
        }

        if (tileInfo.TileColumnCountLog2 > 0 || tileInfo.TileRowCountLog2 > 0)
        {
            writer.WriteLiteral(tileInfo.ContextUpdateTileId, tileInfo.TileRowCountLog2 + tileInfo.TileColumnCountLog2);
            writer.WriteLiteral((uint)tileInfo.TileSizeBytes - 1, 2);
        }

        frameHeader.TilesInfo = tileInfo;
    }

    /// <summary>
    /// Writes the reduced uncompressed header for an intra still-image frame.
    /// </summary>
    /// <param name="writer">The bit writer receiving the uncompressed frame header.</param>
    /// <param name="sequenceHeader">The sequence header controlling available coding tools.</param>
    /// <param name="frameHeader">The frame header to encode.</param>
    private static void WriteUncompressedFrameHeader(ref Av1BitStreamWriter writer, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameHeader)
    {
        writer.WriteBoolean(frameHeader.DisableCdfUpdate);
        if (sequenceHeader.ForceScreenContentTools == 2)
        {
            writer.WriteBoolean(frameHeader.AllowScreenContentTools);
        }
        else
        {
            // Guard.IsTrue(frameHeader.AllowScreenContentTools == sequenceHeader.ForceScreenContentTools);
        }

        if (frameHeader.AllowScreenContentTools)
        {
            if (sequenceHeader.ForceIntegerMotionVector == 2)
            {
                writer.WriteBoolean(frameHeader.ForceIntegerMotionVector);
            }
            else
            {
                // Guard.IsTrue(frameHeader.ForceIntegerMotionVector == sequenceHeader.ForceIntegerMotionVector, nameof(frameHeader.ForceIntegerMotionVector), "Frame and sequence must be in sync");
            }
        }

        if (frameHeader.FrameType == ObuFrameType.KeyFrame)
        {
            if (!frameHeader.ShowFrame)
            {
                throw new NotImplementedException("No support for hidden frames.");
            }
        }
        else if (frameHeader.FrameType == ObuFrameType.IntraOnlyFrame)
        {
            throw new NotImplementedException("No IntraOnly frames supported.");
        }

        if (frameHeader.FrameType == ObuFrameType.KeyFrame)
        {
            WriteFrameSize(ref writer, sequenceHeader, frameHeader, false);
            WriteRenderSize(ref writer, frameHeader);
            if (frameHeader.AllowScreenContentTools)
            {
                writer.WriteBoolean(frameHeader.AllowIntraBlockCopy);
            }
        }
        else if (frameHeader.FrameType == ObuFrameType.IntraOnlyFrame)
        {
            WriteFrameSize(ref writer, sequenceHeader, frameHeader, false);
            WriteRenderSize(ref writer, frameHeader);
            if (frameHeader.AllowScreenContentTools)
            {
                writer.WriteBoolean(frameHeader.AllowIntraBlockCopy);
            }
        }
        else
        {
            throw new NotImplementedException("Inter frames not applicable for AVIF.");
        }

        WriteTileInfo(ref writer, sequenceHeader, frameHeader);
        WriteQuantizationParameters(ref writer, sequenceHeader, frameHeader);
        WriteSegmentationParameters(ref writer, frameHeader);
        Av1QuantizationLookup.UpdateFrameQuantizationState(frameHeader);

        if (frameHeader.QuantizationParameters.BaseQIndex > 0)
        {
            writer.WriteBoolean(frameHeader.DeltaQParameters.IsPresent);
            if (frameHeader.DeltaQParameters.IsPresent)
            {
                writer.WriteLiteral((uint)Av1Math.MostSignificantBit((uint)frameHeader.DeltaQParameters.Resolution), 2);

                if (frameHeader.AllowIntraBlockCopy)
                {
                    Guard.IsFalse(
                        frameHeader.DeltaLoopFilterParameters.IsPresent,
                        nameof(frameHeader.DeltaLoopFilterParameters.IsPresent),
                        "Allow INTRA block copy required Loop Filter.");
                }
                else
                {
                    writer.WriteBoolean(frameHeader.DeltaLoopFilterParameters.IsPresent);
                }

                if (frameHeader.DeltaLoopFilterParameters.IsPresent)
                {
                    writer.WriteLiteral((uint)Av1Math.MostSignificantBit((uint)frameHeader.DeltaLoopFilterParameters.Resolution), 2);
                    writer.WriteBoolean(frameHeader.DeltaLoopFilterParameters.IsMulti);
                }
            }
        }

        if (!frameHeader.AllLossless)
        {
            if (!frameHeader.CodedLossless)
            {
                WriteLoopFilterParameters(ref writer, sequenceHeader, frameHeader);
                if (sequenceHeader.EnableCdef)
                {
                    WriteCdefParameters(ref writer, sequenceHeader, frameHeader);
                }
            }

            if (sequenceHeader.EnableRestoration)
            {
                WriteLoopRestorationParameters(ref writer, sequenceHeader, frameHeader);
            }
        }

        // No Frame Reference mode selection for AVIF
        WriteTransformMode(ref writer, frameHeader);

        // No compound INTER-INTER for AVIF.
        WriteFrameReferenceMode(ref writer, frameHeader);
        WriteSkipModeParameters(ref writer, frameHeader);

        // No warp motion for AVIF.
        writer.WriteBoolean(frameHeader.UseReducedTransformSet);

        WriteGlobalMotionParameters(ref writer, frameHeader);
        WriteFilmGrainFilterParameters(ref writer, sequenceHeader, frameHeader);
    }

    /// <summary>
    /// Writes the frame-header portion of a combined frame OBU.
    /// </summary>
    /// <param name="writer">The bit writer receiving the frame header.</param>
    /// <param name="sequenceHeader">The sequence header controlling available coding tools.</param>
    /// <param name="frameHeader">The frame header to encode.</param>
    private static void WriteFrameHeader(ref Av1BitStreamWriter writer, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameHeader)
    {
        WriteUncompressedFrameHeader(ref writer, sequenceHeader, frameHeader);
    }

    /// <summary>
    /// Writes a tile-group header and all tile payloads for a combined frame OBU.
    /// </summary>
    /// <param name="writer">The bit writer receiving the tile group.</param>
    /// <param name="tileInfo">The frame tile layout.</param>
    /// <param name="tileWriter">The writer that produces each entropy-coded tile payload.</param>
    private static void WriteTileGroup(ref Av1BitStreamWriter writer, ObuTileGroupHeader tileInfo, IAv1TileWriter tileWriter)
    {
        int tileCount = tileInfo.TileColumnCount * tileInfo.TileRowCount;
        if (tileCount > 1)
        {
            // This writer places every tile in one group, so the optional range spans the
            // complete frame whenever the range syntax is present.
            writer.WriteBoolean(true);
            uint tileGroupStart = 0U;
            uint tileGroupEnd = (uint)tileCount - 1U;
            int tileBits = tileInfo.TileColumnCountLog2 + tileInfo.TileRowCountLog2;
            writer.WriteLiteral(tileGroupStart, tileBits);
            writer.WriteLiteral(tileGroupEnd, tileBits);
        }

        AlignToByteBoundary(ref writer);

        WriteTileData(ref writer, tileInfo, tileWriter);
    }

    /// <summary>
    /// Writes the size-prefixed tile payloads in raster order.
    /// </summary>
    /// <param name="writer">The byte-aligned bit writer receiving tile data.</param>
    /// <param name="tileInfo">The frame tile layout and tile-size field width.</param>
    /// <param name="tileWriter">The writer that produces each tile payload.</param>
    private static void WriteTileData(ref Av1BitStreamWriter writer, ObuTileGroupHeader tileInfo, IAv1TileWriter tileWriter)
    {
        int tileCount = tileInfo.TileColumnCount * tileInfo.TileRowCount;
        for (int tileNum = 0; tileNum < tileCount; tileNum++)
        {
            Span<byte> tileData = tileWriter.WriteTile(tileNum);
            if (tileNum != tileCount - 1 && tileCount > 1)
            {
                writer.WriteLittleEndian((uint)tileData.Length - 1U, tileInfo.TileSizeBytes);
            }

            writer.WriteBlob(tileData);
        }
    }

    /// <summary>
    /// Writes an optional signed quantizer-index delta.
    /// </summary>
    /// <param name="writer">The bit writer receiving the delta.</param>
    /// <param name="deltaQ">The quantizer-index delta.</param>
    private static void WriteDeltaQ(ref Av1BitStreamWriter writer, int deltaQ)
    {
        bool isCoded = deltaQ != 0;
        writer.WriteBoolean(isCoded);
        if (isCoded)
        {
            writer.WriteSignedFromUnsigned(deltaQ, 7);
        }
    }

    /// <summary>
    /// Writes the base index, plane deltas, and optional quantization matrices for a frame.
    /// </summary>
    /// <param name="writer">The bit writer receiving the quantization parameters.</param>
    /// <param name="sequenceHeader">The sequence header defining active color planes.</param>
    /// <param name="frameHeader">The frame header containing the quantization parameters.</param>
    private static void WriteQuantizationParameters(ref Av1BitStreamWriter writer, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameHeader)
    {
        ObuQuantizationParameters quantParams = frameHeader.QuantizationParameters;
        writer.WriteLiteral((uint)quantParams.BaseQIndex, 8);
        WriteDeltaQ(ref writer, quantParams.DeltaQDc[(int)Av1Plane.Y]);
        if (sequenceHeader.ColorConfig.PlaneCount > 1)
        {
            if (sequenceHeader.ColorConfig.HasSeparateUvDelta)
            {
                writer.WriteBoolean(quantParams.HasSeparateUvDelta);
            }

            WriteDeltaQ(ref writer, quantParams.DeltaQDc[(int)Av1Plane.U]);
            WriteDeltaQ(ref writer, quantParams.DeltaQAc[(int)Av1Plane.U]);
            if (quantParams.HasSeparateUvDelta)
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
            if (sequenceHeader.ColorConfig.HasSeparateUvDelta)
            {
                writer.WriteLiteral((uint)quantParams.QMatrix[(int)Av1Plane.V], 4);
            }
        }
    }

    /// <summary>
    /// Writes segmentation feature data for an independently decoded still-image frame.
    /// </summary>
    /// <param name="writer">The bit writer receiving the segmentation parameters.</param>
    /// <param name="frameHeader">The frame header containing segmentation feature data.</param>
    private static void WriteSegmentationParameters(ref Av1BitStreamWriter writer, ObuFrameHeader frameHeader)
    {
        ObuSegmentationParameters segmentation = frameHeader.SegmentationParameters;
        writer.WriteBoolean(segmentation.Enabled);
        if (!segmentation.Enabled)
        {
            return;
        }

        // The still-image writer emits independent intra frames with no primary reference.
        // AV1 therefore infers update-map and update-data as enabled and carries feature data
        // directly, without the inter-frame update flags.
        for (int segmentId = 0; segmentId < Av1Constants.MaxSegmentCount; segmentId++)
        {
            for (int featureId = 0; featureId < Av1Constants.SegmentationLevelMax; featureId++)
            {
                bool enabled = segmentation.FeatureEnabled[segmentId, featureId];
                writer.WriteBoolean(enabled);
                if (!enabled)
                {
                    continue;
                }

                int bitCount = Av1Constants.SegmentationFeatureBits[featureId];
                int value = segmentation.FeatureData[segmentId, featureId];
                if (Av1Constants.SegmentationFeatureSigned[featureId] == 1)
                {
                    writer.WriteSignedFromUnsigned(value, bitCount + 1);
                }
                else
                {
                    writer.WriteLiteral((uint)value, bitCount);
                }
            }
        }
    }

    /// <summary>
    /// Writes the deblocking-loop-filter levels and optional reference and mode deltas.
    /// </summary>
    /// <param name="writer">The bit writer receiving the loop-filter parameters.</param>
    /// <param name="sequenceHeader">The sequence header defining active color planes.</param>
    /// <param name="frameHeader">The frame header containing the loop-filter parameters.</param>
    private static void WriteLoopFilterParameters(ref Av1BitStreamWriter writer, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameHeader)
    {
        if (frameHeader.CodedLossless || frameHeader.AllowIntraBlockCopy)
        {
            return;
        }

        writer.WriteLiteral((uint)frameHeader.LoopFilterParameters.FilterLevel[0], 6);
        writer.WriteLiteral((uint)frameHeader.LoopFilterParameters.FilterLevel[1], 6);
        if (sequenceHeader.ColorConfig.PlaneCount > 1)
        {
            if (frameHeader.LoopFilterParameters.FilterLevel[0] > 0 || frameHeader.LoopFilterParameters.FilterLevel[1] > 0)
            {
                writer.WriteLiteral((uint)frameHeader.LoopFilterParameters.FilterLevelU, 6);
                writer.WriteLiteral((uint)frameHeader.LoopFilterParameters.FilterLevelV, 6);
            }
        }

        writer.WriteLiteral((uint)frameHeader.LoopFilterParameters.SharpnessLevel, 3);
        writer.WriteBoolean(frameHeader.LoopFilterParameters.ReferenceDeltaModeEnabled);
        if (frameHeader.LoopFilterParameters.ReferenceDeltaModeEnabled)
        {
            writer.WriteBoolean(frameHeader.LoopFilterParameters.ReferenceDeltaModeUpdate);
            if (frameHeader.LoopFilterParameters.ReferenceDeltaModeUpdate)
            {
                // An independent still frame can emit every current delta as an update. This is
                // slightly larger than comparing against retained state but requires no video
                // reference-frame state and produces the same observable filter parameters.
                for (int i = 0; i < Av1Constants.TotalReferencesPerFrame; i++)
                {
                    writer.WriteBoolean(true);
                    writer.WriteSignedFromUnsigned(frameHeader.LoopFilterParameters.ReferenceDeltas[i], 7);
                }

                for (int i = 0; i < 2; i++)
                {
                    writer.WriteBoolean(true);
                    writer.WriteSignedFromUnsigned(frameHeader.LoopFilterParameters.ModeDeltas[i], 7);
                }
            }
        }
    }

    /// <summary>
    /// Writes the transform-size selection mode when the frame is not lossless.
    /// </summary>
    /// <param name="writer">The bit writer receiving the transform-mode flag.</param>
    /// <param name="frameHeader">The frame header containing the transform mode.</param>
    private static void WriteTransformMode(ref Av1BitStreamWriter writer, ObuFrameHeader frameHeader)
    {
        if (!frameHeader.CodedLossless)
        {
            writer.WriteBoolean(frameHeader.TransformMode == Av1TransformMode.Select);
        }
    }

    /// <summary>
    /// Writes the loop-restoration type and restoration-unit size for each plane.
    /// </summary>
    /// <param name="writer">The bit writer receiving the loop-restoration parameters.</param>
    /// <param name="sequenceHeader">The sequence header defining restoration availability and color planes.</param>
    /// <param name="frameHeader">The frame header containing restoration parameters.</param>
    private static void WriteLoopRestorationParameters(ref Av1BitStreamWriter writer, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameHeader)
    {
        if (frameHeader.CodedLossless || frameHeader.AllowIntraBlockCopy || !sequenceHeader.EnableRestoration)
        {
            return;
        }

        int planesCount = sequenceHeader.ColorConfig.PlaneCount;
        for (int i = 0; i < planesCount; i++)
        {
            writer.WriteLiteral((uint)frameHeader.LoopRestorationParameters.Items[i].Type, 2);
        }

        if (frameHeader.LoopRestorationParameters.UsesLoopRestoration)
        {
            uint unitShift = (uint)frameHeader.LoopRestorationParameters.UnitShift;
            if (sequenceHeader.Use128x128Superblock)
            {
                writer.WriteLiteral(unitShift - 1, 1);
            }
            else
            {
                writer.WriteBoolean(unitShift > 0);
                if (unitShift > 0)
                {
                    writer.WriteLiteral(unitShift - 1, 1);
                }
            }

            if (sequenceHeader.ColorConfig.SubSamplingX && sequenceHeader.ColorConfig.SubSamplingY && frameHeader.LoopRestorationParameters.UsesChromaLoopRestoration)
            {
                writer.WriteLiteral((uint)frameHeader.LoopRestorationParameters.UVShift, 1);
            }
        }
    }

    /// <summary>
    /// Writes constrained directional enhancement filter strengths for the active planes.
    /// </summary>
    /// <param name="writer">The bit writer receiving the CDEF parameters.</param>
    /// <param name="sequenceHeader">The sequence header defining CDEF availability and color planes.</param>
    /// <param name="frameHeader">The frame header containing CDEF strengths.</param>
    private static void WriteCdefParameters(ref Av1BitStreamWriter writer, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameHeader)
    {
        if (frameHeader.CodedLossless || frameHeader.AllowIntraBlockCopy || !sequenceHeader.EnableCdef)
        {
            return;
        }

        ObuConstraintDirectionalEnhancementFilterParameters cdef = frameHeader.CdefParameters;
        writer.WriteLiteral((uint)cdef.Damping - 3, 2);
        writer.WriteLiteral((uint)cdef.BitCount, 2);
        int strengthCount = 1 << cdef.BitCount;
        bool hasChroma = sequenceHeader.ColorConfig.PlaneCount > 1;
        for (int i = 0; i < strengthCount; i++)
        {
            writer.WriteLiteral((uint)cdef.YStrength[i], 6);
            if (hasChroma)
            {
                writer.WriteLiteral((uint)cdef.UvStrength[i], 6);
            }
        }
    }

    /// <summary>
    /// Writes global-motion parameters when permitted by the frame type.
    /// </summary>
    /// <param name="writer">The bit writer positioned at the global-motion syntax.</param>
    /// <param name="frameHeader">The current frame header.</param>
    private static void WriteGlobalMotionParameters(ref Av1BitStreamWriter writer, ObuFrameHeader frameHeader)
    {
        _ = writer;

        if (frameHeader.IsIntra)
        {
            // Nothing to be written for INTRA frames.
            return;
        }

        throw new InvalidImageContentException("AVIF files can only contain INTRA frames.");
    }

    /// <summary>
    /// Writes reference-mode selection when permitted by the frame type.
    /// </summary>
    /// <param name="writer">The bit writer positioned at the reference-mode syntax.</param>
    /// <param name="frameHeader">The current frame header.</param>
    private static void WriteFrameReferenceMode(ref Av1BitStreamWriter writer, ObuFrameHeader frameHeader)
    {
        _ = writer;

        if (frameHeader.IsIntra)
        {
            // Nothing to be written for INTRA frames.
            return;
        }

        throw new InvalidImageContentException("AVIF files can only contain INTRA frames.");
    }

    /// <summary>
    /// Writes the skip-mode flag when skip mode is available.
    /// </summary>
    /// <param name="writer">The bit writer receiving the skip-mode flag.</param>
    /// <param name="frameHeader">The frame header containing skip-mode state.</param>
    private static void WriteSkipModeParameters(ref Av1BitStreamWriter writer, ObuFrameHeader frameHeader)
    {
        if (frameHeader.SkipModeParameters.SkipModeAllowed)
        {
            writer.WriteBoolean(frameHeader.SkipModeParameters.SkipModeFlag);
        }
    }

    /// <summary>
    /// Writes film-grain synthesis parameters for a displayed still-image frame.
    /// </summary>
    /// <param name="writer">The bit writer receiving the film-grain parameters.</param>
    /// <param name="sequenceHeader">The sequence header defining film-grain availability and color sampling.</param>
    /// <param name="frameHeader">The frame header containing film-grain parameters.</param>
    private static void WriteFilmGrainFilterParameters(ref Av1BitStreamWriter writer, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameHeader)
    {
        ObuFilmGrainParameters grainParams = frameHeader.FilmGrainParameters;
        if (!sequenceHeader.AreFilmGrainingParametersPresent || (!frameHeader.ShowFrame && !frameHeader.ShowableFrame))
        {
            return;
        }

        writer.WriteBoolean(grainParams.ApplyGrain);
        if (!grainParams.ApplyGrain)
        {
            return;
        }

        writer.WriteLiteral(grainParams.GrainSeed, 16);
        writer.WriteLiteral(grainParams.NumYPoints, 4);
        for (int i = 0; i < grainParams.NumYPoints; i++)
        {
            writer.WriteLiteral(grainParams.PointYValue[i], 8);
            writer.WriteLiteral(grainParams.PointYScaling[i], 8);
        }

        if (!sequenceHeader.ColorConfig.IsMonochrome)
        {
            writer.WriteBoolean(grainParams.ChromaScalingFromLuma);
        }

        if (!sequenceHeader.ColorConfig.IsMonochrome &&
            !grainParams.ChromaScalingFromLuma &&
            (!sequenceHeader.ColorConfig.SubSamplingX || !sequenceHeader.ColorConfig.SubSamplingY || grainParams.NumYPoints != 0))
        {
            writer.WriteLiteral(grainParams.NumCbPoints, 4);
            for (int i = 0; i < grainParams.NumCbPoints; i++)
            {
                writer.WriteLiteral(grainParams.PointCbValue[i], 8);
                writer.WriteLiteral(grainParams.PointCbScaling[i], 8);
            }

            writer.WriteLiteral(grainParams.NumCrPoints, 4);
            for (int i = 0; i < grainParams.NumCrPoints; i++)
            {
                writer.WriteLiteral(grainParams.PointCrValue[i], 8);
                writer.WriteLiteral(grainParams.PointCrScaling[i], 8);
            }
        }

        writer.WriteLiteral(grainParams.GrainScalingMinus8, 2);
        writer.WriteLiteral(grainParams.ArCoeffLag, 2);
        uint numPosLuma = 2 * grainParams.ArCoeffLag * (grainParams.ArCoeffLag + 1);

        uint numPosChroma = numPosLuma;
        if (grainParams.NumYPoints != 0)
        {
            numPosChroma++;
            for (int i = 0; i < numPosLuma; i++)
            {
                writer.WriteLiteral(grainParams.ArCoeffsYPlus128[i], 8);
            }
        }

        if (grainParams.ChromaScalingFromLuma || grainParams.NumCbPoints != 0)
        {
            for (int i = 0; i < numPosChroma; i++)
            {
                writer.WriteLiteral(grainParams.ArCoeffsCbPlus128[i], 8);
            }
        }

        if (grainParams.ChromaScalingFromLuma || grainParams.NumCrPoints != 0)
        {
            for (int i = 0; i < numPosChroma; i++)
            {
                writer.WriteLiteral(grainParams.ArCoeffsCrPlus128[i], 8);
            }
        }

        writer.WriteLiteral(grainParams.ArCoeffShiftMinus6, 2);
        writer.WriteLiteral(grainParams.GrainScaleShift, 2);
        if (grainParams.NumCbPoints != 0)
        {
            writer.WriteLiteral(grainParams.CbMult, 8);
            writer.WriteLiteral(grainParams.CbLumaMult, 8);
            writer.WriteLiteral(grainParams.CbOffset, 9);
        }

        if (grainParams.NumCrPoints != 0)
        {
            writer.WriteLiteral(grainParams.CrMult, 8);
            writer.WriteLiteral(grainParams.CrLumaMult, 8);
            writer.WriteLiteral(grainParams.CrOffset, 9);
        }

        writer.WriteBoolean(grainParams.OverlapFlag);
        writer.WriteBoolean(grainParams.ClipToRestrictedRange);
    }
}
