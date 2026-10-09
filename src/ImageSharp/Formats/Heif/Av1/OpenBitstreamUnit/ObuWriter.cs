// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Buffers.Binary;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

/// <summary>
/// Writes the AV1 open bitstream units for one coded frame.
/// </summary>
internal sealed class ObuWriter : IDisposable
{
    // Sequence and uncompressed-frame syntax have fixed field and array limits. A 512-byte buffer holds the largest supported header. The
    // buffer never holds entropy-coded tile bytes.
    private const int MaximumHeaderLength = 512;

    private readonly IMemoryOwner<byte> headerOwner;

    /// <summary>
    /// Initializes a new instance of the <see cref="ObuWriter"/> class.
    /// </summary>
    /// <param name="configuration">The configuration providing reusable header memory.</param>
    public ObuWriter(Configuration configuration)
        => this.headerOwner = configuration.MemoryAllocator.Allocate<byte>(MaximumHeaderLength);

    /// <summary>
    /// Writes a temporal delimiter, sequence header, and coded frame for the first sample in a sequence.
    /// </summary>
    /// <typeparam name="TTileWriter">The non-boxed tile source type.</typeparam>
    /// <param name="stream">The destination stream.</param>
    /// <param name="sequenceHeader">The sequence header.</param>
    /// <param name="frameHeader">The frame header.</param>
    /// <param name="tileWriter">The encoded tile source.</param>
    public void WriteSequenceFrame<TTileWriter>(
        Stream stream,
        ObuSequenceHeader sequenceHeader,
        ObuFrameHeader frameHeader,
        TTileWriter tileWriter)
        where TTileWriter : IAv1TileWriter
    {
        Span<byte> headerBuffer = this.headerOwner.Memory.Span[..MaximumHeaderLength];
        Av1BitStreamWriter writer = new(headerBuffer);
        WriteObuHeaderAndSize(stream, ObuType.TemporalDelimiter, []);
        WriteSequenceHeader(ref writer, sequenceHeader);
        int bytesWritten = (writer.BitPosition + 7) >> 3;
        writer.Flush();
        WriteObuHeaderAndSize(stream, ObuType.SequenceHeader, headerBuffer[..bytesWritten]);
        WriteFrameObu(stream, sequenceHeader, frameHeader, tileWriter, headerBuffer, ref writer);
    }

    /// <summary>
    /// Writes an empty temporal-delimiter OBU.
    /// </summary>
    /// <param name="stream">The destination stream.</param>
    public static void WriteTemporalDelimiter(Stream stream)
        => WriteObuHeaderAndSize(stream, ObuType.TemporalDelimiter, []);

    /// <summary>
    /// Writes a temporal delimiter followed by one sequence-header OBU.
    /// </summary>
    /// <param name="configuration">The configuration used to allocate temporary encoding memory.</param>
    /// <param name="stream">The destination stream.</param>
    /// <param name="sequenceHeader">The sequence header.</param>
    public static void WriteSequenceHeader(Configuration configuration, Stream stream, ObuSequenceHeader sequenceHeader)
    {
        using IMemoryOwner<byte> headerOwner = configuration.MemoryAllocator.Allocate<byte>(MaximumHeaderLength);
        Span<byte> headerBuffer = headerOwner.Memory.Span[..MaximumHeaderLength];
        Av1BitStreamWriter writer = new(headerBuffer);
        WriteObuHeaderAndSize(stream, ObuType.TemporalDelimiter, []);
        WriteSequenceHeader(ref writer, sequenceHeader);
        int bytesWritten = (writer.BitPosition + 7) >> 3;
        writer.Flush();
        WriteObuHeaderAndSize(stream, ObuType.SequenceHeader, headerBuffer[..bytesWritten]);
    }

    /// <summary>
    /// Writes a temporal delimiter and coded frame that continues an established sequence.
    /// </summary>
    /// <typeparam name="TTileWriter">The non-boxed tile source type.</typeparam>
    /// <param name="stream">The destination stream.</param>
    /// <param name="sequenceHeader">The sequence header established by an earlier sample.</param>
    /// <param name="frameHeader">The frame header.</param>
    /// <param name="tileWriter">The encoded tile source.</param>
    public void WriteFrame<TTileWriter>(
        Stream stream,
        ObuSequenceHeader sequenceHeader,
        ObuFrameHeader frameHeader,
        TTileWriter tileWriter)
        where TTileWriter : IAv1TileWriter
    {
        Span<byte> headerBuffer = this.headerOwner.Memory.Span[..MaximumHeaderLength];
        Av1BitStreamWriter writer = new(headerBuffer);
        WriteObuHeaderAndSize(stream, ObuType.TemporalDelimiter, []);
        WriteFrameObu(stream, sequenceHeader, frameHeader, tileWriter, headerBuffer, ref writer);
    }

    /// <summary>
    /// Writes a coded frame that continues the current temporal unit, after a hidden frame of the same unit. No temporal delimiter comes before
    /// the frame.
    /// </summary>
    /// <typeparam name="TTileWriter">The non-boxed tile source type.</typeparam>
    /// <param name="stream">The destination stream.</param>
    /// <param name="sequenceHeader">The sequence header established by an earlier sample.</param>
    /// <param name="frameHeader">The frame header.</param>
    /// <param name="tileWriter">The encoded tile source.</param>
    public void WriteFrameWithoutDelimiter<TTileWriter>(
        Stream stream,
        ObuSequenceHeader sequenceHeader,
        ObuFrameHeader frameHeader,
        TTileWriter tileWriter)
        where TTileWriter : IAv1TileWriter
    {
        Span<byte> headerBuffer = this.headerOwner.Memory.Span[..MaximumHeaderLength];
        Av1BitStreamWriter writer = new(headerBuffer);
        WriteFrameObu(stream, sequenceHeader, frameHeader, tileWriter, headerBuffer, ref writer);
    }

    /// <summary>
    /// Writes a frame header OBU that shows a frame already in a reference slot. The header ends with trailing bits.
    /// </summary>
    /// <param name="stream">The destination stream.</param>
    /// <param name="sequenceHeader">The sequence header established by an earlier sample.</param>
    /// <param name="frameHeader">The frame header with <see cref="ObuFrameHeader.ShowExistingFrame"/> set.</param>
    /// <param name="writeTemporalDelimiter">Whether the frame starts a temporal unit.</param>
    public void WriteShowExistingFrame(
        Stream stream,
        ObuSequenceHeader sequenceHeader,
        ObuFrameHeader frameHeader,
        bool writeTemporalDelimiter)
    {
        Span<byte> headerBuffer = this.headerOwner.Memory.Span[..MaximumHeaderLength];
        Av1BitStreamWriter writer = new(headerBuffer);
        if (writeTemporalDelimiter)
        {
            WriteObuHeaderAndSize(stream, ObuType.TemporalDelimiter, []);
        }

        WriteFrameHeader(ref writer, sequenceHeader, frameHeader);
        WriteTrailingBits(ref writer);
        int bytesWritten = (writer.BitPosition + 7) >> 3;
        writer.Flush();
        WriteObuHeaderAndSize(stream, ObuType.FrameHeader, (uint)bytesWritten, GetLayerExtension(sequenceHeader, frameHeader));
        stream.Write(headerBuffer[..bytesWritten]);
    }

    /// <inheritdoc/>
    public void Dispose() => this.headerOwner.Dispose();

    /// <summary>
    /// Writes the combined frame OBU header followed by each retained tile payload.
    /// </summary>
    /// <typeparam name="TTileWriter">The non-boxed tile source type.</typeparam>
    /// <param name="stream">The destination stream.</param>
    /// <param name="sequenceHeader">The sequence header governing frame syntax.</param>
    /// <param name="frameHeader">The uncompressed frame header.</param>
    /// <param name="tileWriter">The encoded tile source.</param>
    /// <param name="headerBuffer">The reusable OBU header scratch.</param>
    /// <param name="writer">The bit writer over <paramref name="headerBuffer"/>.</param>
    private static void WriteFrameObu<TTileWriter>(
        Stream stream,
        ObuSequenceHeader sequenceHeader,
        ObuFrameHeader frameHeader,
        TTileWriter tileWriter,
        Span<byte> headerBuffer,
        ref Av1BitStreamWriter writer)
        where TTileWriter : IAv1TileWriter
    {
        WriteFrameHeader(ref writer, sequenceHeader, frameHeader);
        ObuTileGroupHeader tileInfo = frameHeader.TilesInfo;
        WriteTileGroupHeader(ref writer, tileInfo);

        int frameHeaderBytes = (writer.BitPosition + 7) >> 3;
        writer.Flush();

        int tileCount = tileInfo.TileColumnCount * tileInfo.TileRowCount;
        uint framePayloadSize = (uint)(frameHeaderBytes + ((tileCount - 1) * tileInfo.TileSizeBytes));
        for (int tileNum = 0; tileNum < tileCount; tileNum++)
        {
            framePayloadSize += (uint)tileWriter.GetTileData(tileNum).Length;
        }

        WriteObuHeaderAndSize(stream, ObuType.Frame, framePayloadSize, GetLayerExtension(sequenceHeader, frameHeader));
        stream.Write(headerBuffer[..frameHeaderBytes]);
        WriteTileData(stream, tileInfo, tileWriter);
    }

    /// <summary>
    /// Returns the extension byte of a layer-specific OBU, which holds the temporal and spatial layer of the frame. If an operating point of the
    /// sequence selects layers, every layer-specific OBU needs the byte. If all operating points decode every layer, the OBU must not have it.
    /// </summary>
    /// <param name="sequenceHeader">The sequence header with the operating points.</param>
    /// <param name="frameHeader">The frame header with the layer of the frame.</param>
    /// <returns>The extension byte, or -1 when the OBU has no extension.</returns>
    private static int GetLayerExtension(ObuSequenceHeader sequenceHeader, ObuFrameHeader frameHeader)
    {
        foreach (ObuOperatingPoint operatingPoint in sequenceHeader.OperatingPoint)
        {
            if (operatingPoint.Idc != 0)
            {
                // Three bits of temporal layer, two bits of spatial layer, and three reserved zero bits.
                return (frameHeader.TemporalId << 5) | (frameHeader.SpatialId << 3);
            }
        }

        return -1;
    }

    /// <summary>
    /// Creates a byte-aligned OBU header with an explicit payload-size field.
    /// </summary>
    /// <param name="type">The OBU payload type.</param>
    /// <param name="hasExtension">Whether an extension byte follows the header.</param>
    /// <returns>The encoded OBU header byte.</returns>
    private static byte WriteObuHeader(ObuType type, bool hasExtension)
    {
        // The set fields are the four-bit type, the extension flag and the has-size flag. The forbidden bit and the reserved bit stay zero.
        return (byte)(((byte)type << 3) | (hasExtension ? 0x04 : 0) | 0x02);
    }

    /// <summary>
    /// Writes a complete byte-aligned OBU with a little-endian base-128 payload size.
    /// </summary>
    /// <param name="stream">The destination stream.</param>
    /// <param name="type">The OBU payload type.</param>
    /// <param name="payload">The complete OBU payload.</param>
    private static void WriteObuHeaderAndSize(Stream stream, ObuType type, ReadOnlySpan<byte> payload)
    {
        WriteObuHeaderAndSize(stream, type, (uint)payload.Length, -1);
        stream.Write(payload);
    }

    /// <summary>
    /// Writes a byte-aligned OBU header, its optional extension byte, and its little-endian base-128 payload size.
    /// </summary>
    /// <param name="stream">The destination stream.</param>
    /// <param name="type">The OBU payload type.</param>
    /// <param name="payloadSize">The number of payload bytes that follow the header.</param>
    /// <param name="extension">The extension byte, or -1 for an OBU without extension.</param>
    private static void WriteObuHeaderAndSize(Stream stream, ObuType type, uint payloadSize, int extension)
    {
        stream.WriteByte(WriteObuHeader(type, extension >= 0));
        if (extension >= 0)
        {
            stream.WriteByte((byte)extension);
        }

        // A 32-bit OBU payload length requires at most five base-128 bytes.
        Span<byte> lengthBytes = stackalloc byte[5];
        int lengthLength = Av1BitStreamWriter.GetLittleEndianBytes128(payloadSize, lengthBytes);
        stream.Write(lengthBytes, 0, lengthLength);
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
    /// Writes an AV1 sequence header.
    /// </summary>
    /// <param name="writer">The bit writer receiving the sequence header.</param>
    /// <param name="sequenceHeader">The sequence header to encode.</param>
    private static void WriteSequenceHeader(ref Av1BitStreamWriter writer, ObuSequenceHeader sequenceHeader)
    {
        writer.WriteLiteral((uint)sequenceHeader.SequenceProfile, 3);
        writer.WriteBoolean(sequenceHeader.IsStillPicture);
        writer.WriteBoolean(sequenceHeader.IsReducedStillPictureHeader);
        if (sequenceHeader.IsReducedStillPictureHeader)
        {
            writer.WriteLiteral((uint)sequenceHeader.OperatingPoint[0].SequenceLevelIndex, Av1Constants.LevelBits);
        }
        else
        {
            writer.WriteBoolean(sequenceHeader.TimingInfoPresentFlag);
            if (sequenceHeader.TimingInfoPresentFlag)
            {
                WriteTimingInfo(ref writer, sequenceHeader.GetTimingInfo());
                writer.WriteBoolean(sequenceHeader.DecoderModelInfoPresentFlag);
                if (sequenceHeader.DecoderModelInfoPresentFlag)
                {
                    WriteDecoderModelInfo(ref writer, sequenceHeader.GetDecoderModelInfo());
                }
            }

            writer.WriteBoolean(sequenceHeader.InitialDisplayDelayPresentFlag);
            writer.WriteLiteral(
                (uint)(sequenceHeader.OperatingPoint.Length - 1),
                Av1Constants.OperatingPointCountBits);

            foreach (ObuOperatingPoint operatingPoint in sequenceHeader.OperatingPoint)
            {
                writer.WriteLiteral(operatingPoint.Idc, Av1Constants.OperatingPointIdcBits);
                writer.WriteLiteral((uint)operatingPoint.SequenceLevelIndex, Av1Constants.LevelBits);
                if (operatingPoint.SequenceLevelIndex >= Av1Constants.SequenceTierMinimumLevelIndex)
                {
                    writer.WriteBoolean(operatingPoint.SequenceTier != 0);
                }

                if (sequenceHeader.DecoderModelInfoPresentFlag)
                {
                    writer.WriteBoolean(operatingPoint.IsDecoderModelInfoPresent);
                    if (operatingPoint.IsDecoderModelInfoPresent)
                    {
                        WriteOperatingParametersInfo(
                            ref writer,
                            sequenceHeader.GetDecoderModelInfo(),
                            operatingPoint);
                    }
                }

                if (sequenceHeader.InitialDisplayDelayPresentFlag)
                {
                    writer.WriteBoolean(operatingPoint.IsInitialDisplayDelayPresent);
                    if (operatingPoint.IsInitialDisplayDelayPresent)
                    {
                        writer.WriteLiteral(operatingPoint.InitialDisplayDelay - 1, 4);
                    }
                }
            }
        }

        // The maximum dimensions determine the fixed-width fields used by every frame in the sequence.
        writer.WriteLiteral((uint)sequenceHeader.FrameWidthBits - 1, 4);
        writer.WriteLiteral((uint)sequenceHeader.FrameHeightBits - 1, 4);
        writer.WriteLiteral((uint)sequenceHeader.MaxFrameWidth - 1, sequenceHeader.FrameWidthBits);
        writer.WriteLiteral((uint)sequenceHeader.MaxFrameHeight - 1, sequenceHeader.FrameHeightBits);
        if (!sequenceHeader.IsReducedStillPictureHeader)
        {
            writer.WriteBoolean(sequenceHeader.IsFrameIdNumbersPresent);
            if (sequenceHeader.IsFrameIdNumbersPresent)
            {
                writer.WriteLiteral((uint)sequenceHeader.DeltaFrameIdLength - 2, 4);
                writer.WriteLiteral(sequenceHeader.AdditionalFrameIdLength - 1, 3);
            }
        }

        writer.WriteBoolean(sequenceHeader.Use128x128Superblock);
        writer.WriteBoolean(sequenceHeader.EnableFilterIntra);
        writer.WriteBoolean(sequenceHeader.EnableIntraEdgeFilter);
        if (!sequenceHeader.IsReducedStillPictureHeader)
        {
            writer.WriteBoolean(sequenceHeader.EnableInterIntraCompound);
            writer.WriteBoolean(sequenceHeader.EnableMaskedCompound);
            writer.WriteBoolean(sequenceHeader.EnableWarpedMotion);
            writer.WriteBoolean(sequenceHeader.EnableDualFilter);
            writer.WriteBoolean(sequenceHeader.EnableOrderHint);
            if (sequenceHeader.EnableOrderHint)
            {
                writer.WriteBoolean(sequenceHeader.OrderHintInfo.EnableJointCompound);
                writer.WriteBoolean(sequenceHeader.OrderHintInfo.EnableReferenceFrameMotionVectors);
            }

            bool chooseScreenContentTools = sequenceHeader.ForceScreenContentTools == Av1Constants.SelectScreenContentTools;
            writer.WriteBoolean(chooseScreenContentTools);
            if (!chooseScreenContentTools)
            {
                writer.WriteBoolean(sequenceHeader.ForceScreenContentTools != 0);
            }

            if (sequenceHeader.ForceScreenContentTools > 0)
            {
                bool chooseIntegerMotionVector = sequenceHeader.ForceIntegerMotionVector == Av1Constants.SelectIntegerMotionVector;
                writer.WriteBoolean(chooseIntegerMotionVector);
                if (!chooseIntegerMotionVector)
                {
                    writer.WriteBoolean(sequenceHeader.ForceIntegerMotionVector != 0);
                }
            }

            if (sequenceHeader.EnableOrderHint)
            {
                writer.WriteLiteral((uint)sequenceHeader.OrderHintInfo.OrderHintBits - 1, 3);
            }
        }

        writer.WriteBoolean(sequenceHeader.EnableSuperResolution);
        writer.WriteBoolean(sequenceHeader.EnableCdef);
        writer.WriteBoolean(sequenceHeader.EnableRestoration);
        WriteColorConfig(ref writer, sequenceHeader);
        writer.WriteBoolean(sequenceHeader.AreFilmGrainingParametersPresent);
        WriteTrailingBits(ref writer);
    }

    /// <summary>
    /// Writes sequence timing in the fixed-width and unsigned-variable-length forms required by AV1.
    /// </summary>
    /// <param name="writer">The bit writer receiving the timing information.</param>
    /// <param name="timingInfo">The timing values to encode.</param>
    private static void WriteTimingInfo(ref Av1BitStreamWriter writer, ObuTimingInfo timingInfo)
    {
        writer.WriteLiteral(timingInfo.NumUnitsInDisplayTick, 32);
        writer.WriteLiteral(timingInfo.TimeScale, 32);
        writer.WriteBoolean(timingInfo.EqualPictureInterval);
        if (timingInfo.EqualPictureInterval)
        {
            WriteUnsignedVariableLength(ref writer, timingInfo.NumTicksPerPicture - 1);
        }
    }

    /// <summary>
    /// Writes decoder-buffer field widths and decoding-clock units.
    /// </summary>
    /// <param name="writer">The bit writer receiving the decoder-model information.</param>
    /// <param name="decoderModelInfo">The decoder-model values to encode.</param>
    private static void WriteDecoderModelInfo(ref Av1BitStreamWriter writer, ObuDecoderModelInfo decoderModelInfo)
    {
        writer.WriteLiteral(decoderModelInfo.BufferDelayLength - 1, 5);
        writer.WriteLiteral(decoderModelInfo.NumUnitsInDecodingTick, 32);
        writer.WriteLiteral(decoderModelInfo.BufferRemovalTimeLength - 1, 5);
        writer.WriteLiteral(decoderModelInfo.FramePresentationTimeLength - 1, 5);
    }

    /// <summary>
    /// Writes the decoder-model parameters for one operating point.
    /// </summary>
    /// <param name="writer">The bit writer receiving the operating-point parameters.</param>
    /// <param name="decoderModelInfo">The decoder model defining the delay field width.</param>
    /// <param name="operatingPoint">The operating-point values to encode.</param>
    private static void WriteOperatingParametersInfo(
        ref Av1BitStreamWriter writer,
        ObuDecoderModelInfo decoderModelInfo,
        ObuOperatingPoint operatingPoint)
    {
        int bufferDelayLength = (int)decoderModelInfo.BufferDelayLength;
        writer.WriteLiteral(operatingPoint.DecoderBufferDelay, bufferDelayLength);
        writer.WriteLiteral(operatingPoint.EncoderBufferDelay, bufferDelayLength);
        writer.WriteBoolean(operatingPoint.LowDelayMode);
    }

    /// <summary>
    /// Writes an AV1 unsigned variable-length value.
    /// </summary>
    /// <param name="writer">The bit writer receiving the value.</param>
    /// <param name="value">The value to encode.</param>
    private static void WriteUnsignedVariableLength(ref Av1BitStreamWriter writer, uint value)
    {
        uint encodedValue = value + 1;
        int leadingZeroCount = Av1Math.MostSignificantBit(encodedValue);
        writer.WriteLiteral(0, leadingZeroCount);
        writer.WriteLiteral(encodedValue, leadingZeroCount + 1);
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
            // AV1 fixes this RGB identity-matrix combination to full-range 4:4:4. It omits the range and subsampling fields of YUV configurations.
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
    /// Writes the size of an inter frame whose size differs from the sequence size. It writes one found flag per reference until a reference has
    /// the same upscaled size. The frame then inherits that size. If no reference matches, it writes the explicit frame size and render size.
    /// Every frame that this encoder writes has the sequence size as its render size. As a result, the method compares only the frame sizes.
    /// </summary>
    /// <param name="writer">The bit writer receiving the frame size.</param>
    /// <param name="sequenceHeader">The sequence header defining dimension field widths.</param>
    /// <param name="frameHeader">The frame header with the dimensions, the reference slots and the slot sizes.</param>
    private static void WriteFrameSizeWithReferences(ref Av1BitStreamWriter writer, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameHeader)
    {
        ReadOnlySpan<uint> referenceFrameIndices = frameHeader.GetReferenceFrameIndices();
        ReadOnlySpan<Size> referenceSizes = frameHeader.GetReferenceFrameSizes();
        Size frameSize = new(frameHeader.FrameSize.SuperResolutionUpscaledWidth, frameHeader.FrameSize.FrameHeight);
        for (int reference = 0; reference < Av1Constants.ReferencesPerFrame; reference++)
        {
            bool found = referenceSizes[(int)referenceFrameIndices[reference]] == frameSize;
            writer.WriteBoolean(found);
            if (found)
            {
                WriteSuperResolutionParameters(ref writer, sequenceHeader, frameHeader);
                return;
            }
        }

        WriteFrameSize(ref writer, sequenceHeader, frameHeader, true);
        WriteRenderSize(ref writer, frameHeader);
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

        // Uniform spacing codes the chosen logarithms. Explicit spacing derives them from the tile counts.
        int log2TileColumnCount = tileInfo.HasUniformTileSpacing
            ? tileInfo.TileColumnCountLog2
            : ObuReader.TileLog2(1, tileInfo.TileColumnCount);

        int log2TileRowCount = tileInfo.HasUniformTileSpacing
            ? tileInfo.TileRowCountLog2
            : ObuReader.TileLog2(1, tileInfo.TileRowCount);

        tileInfo.TileColumnCountLog2 = log2TileColumnCount;
        tileInfo.TileRowCountLog2 = log2TileRowCount;

        writer.WriteBoolean(tileInfo.HasUniformTileSpacing);
        if (tileInfo.HasUniformTileSpacing)
        {
            // Uniform spacing gives power-of-two tile counts. Each logarithm is a unary increment above its minimum, with a stop bit when the
            // value is less than the maximum. The columns come first.
            int ones = log2TileColumnCount - tileInfo.MinLog2TileColumnCount;
            while (ones-- > 0)
            {
                writer.WriteBoolean(true);
            }

            if (log2TileColumnCount < tileInfo.MaxLog2TileColumnCount)
            {
                writer.WriteBoolean(false);
            }

            // The minimum row logarithm depends on the column logarithm, because the total tile count has a minimum.
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
            for (int i = 0; i < tileInfo.TileColumnCount; i++)
            {
                int endSuperBlock = i == tileInfo.TileColumnCount - 1
                    ? superblockColumnCount
                    : tileInfo.TileColumnStartModeInfo[i + 1] >> superblockShift;

                // The stored end of the last tile is the frame width in 4x4 units, which can end inside a superblock. As a result, the last
                // tile ends at the superblock column count of the frame.
                uint widthInSuperBlocks = (uint)(endSuperBlock - startSuperBlock);
                uint maxWidth = (uint)Math.Min(superblockColumnCount - startSuperBlock, tileInfo.MaxTileWidthSuperblock);
                writer.WriteNonSymmetric(widthInSuperBlocks - 1, maxWidth);
                startSuperBlock = endSuperBlock;
            }

            if (startSuperBlock != superblockColumnCount)
            {
                throw new ImageFormatException("Super block tiles width does not add up to total width.");
            }

            startSuperBlock = 0;
            for (int i = 0; i < tileInfo.TileRowCount; i++)
            {
                int endSuperBlock = i == tileInfo.TileRowCount - 1
                    ? superblockRowCount
                    : tileInfo.TileRowStartModeInfo[i + 1] >> superblockShift;

                // As with columns, the stored end of the last tile can end inside a superblock.
                uint heightInSuperBlocks = (uint)(endSuperBlock - startSuperBlock);
                uint maxHeight = (uint)Math.Min(superblockRowCount - startSuperBlock, tileInfo.MaxTileHeightSuperblock);
                writer.WriteNonSymmetric(heightInSuperBlocks - 1, maxHeight);
                startSuperBlock = endSuperBlock;
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
    /// Writes the uncompressed header for an AV1 frame.
    /// </summary>
    /// <param name="writer">The bit writer receiving the uncompressed frame header.</param>
    /// <param name="sequenceHeader">The sequence header controlling available coding tools.</param>
    /// <param name="frameHeader">The frame header to encode.</param>
    private static void WriteUncompressedFrameHeader(ref Av1BitStreamWriter writer, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameHeader)
    {
        bool frameSizeOverrideFlag = false;
        if (!sequenceHeader.IsReducedStillPictureHeader)
        {
            writer.WriteBoolean(frameHeader.ShowExistingFrame);
            if (frameHeader.ShowExistingFrame)
            {
                writer.WriteLiteral(frameHeader.FrameToShowMapIdx, Av1Constants.ReferenceFrameIndexBits);
                if (sequenceHeader.DecoderModelInfoPresentFlag && !sequenceHeader.GetTimingInfo().EqualPictureInterval)
                {
                    writer.WriteLiteral(
                        frameHeader.FramePresentationTime,
                        (int)sequenceHeader.GetDecoderModelInfo().FramePresentationTimeLength);
                }

                if (sequenceHeader.IsFrameIdNumbersPresent)
                {
                    writer.WriteLiteral(frameHeader.DisplayFrameId, sequenceHeader.FrameIdLength);
                }

                return;
            }

            writer.WriteLiteral((uint)frameHeader.FrameType, Av1Constants.FrameTypeBits);
            writer.WriteBoolean(frameHeader.ShowFrame);
            if (frameHeader.ShowFrame &&
                sequenceHeader.DecoderModelInfoPresentFlag &&
                !sequenceHeader.GetTimingInfo().EqualPictureInterval)
            {
                writer.WriteLiteral(
                    frameHeader.FramePresentationTime,
                    (int)sequenceHeader.GetDecoderModelInfo().FramePresentationTimeLength);
            }

            if (!frameHeader.ShowFrame)
            {
                writer.WriteBoolean(frameHeader.ShowableFrame);
            }

            if (frameHeader.FrameType != ObuFrameType.SwitchFrame &&
                (frameHeader.FrameType != ObuFrameType.KeyFrame || !frameHeader.ShowFrame))
            {
                writer.WriteBoolean(frameHeader.ErrorResilientMode);
            }
        }

        writer.WriteBoolean(frameHeader.DisableCdfUpdate);
        if (sequenceHeader.ForceScreenContentTools == Av1Constants.SelectScreenContentTools)
        {
            writer.WriteBoolean(frameHeader.AllowScreenContentTools);
        }
        else
        {
            // The sequence forces the value, so the header omits the flag. The frame value must equal the sequence value.
        }

        if (frameHeader.AllowScreenContentTools)
        {
            if (sequenceHeader.ForceIntegerMotionVector == Av1Constants.SelectIntegerMotionVector)
            {
                writer.WriteBoolean(frameHeader.ForceIntegerMotionVector);
            }
            else
            {
                // The sequence forces the value, so the header omits the flag. The frame value must equal the sequence value.
            }
        }

        if (!sequenceHeader.IsReducedStillPictureHeader)
        {
            if (sequenceHeader.IsFrameIdNumbersPresent)
            {
                writer.WriteLiteral(frameHeader.CurrentFrameId, sequenceHeader.FrameIdLength);
            }

            frameSizeOverrideFlag = frameHeader.FrameType == ObuFrameType.SwitchFrame ||
                frameHeader.FrameSize.SuperResolutionUpscaledWidth != sequenceHeader.MaxFrameWidth ||
                frameHeader.FrameSize.FrameHeight != sequenceHeader.MaxFrameHeight;

            if (frameHeader.FrameType != ObuFrameType.SwitchFrame)
            {
                writer.WriteBoolean(frameSizeOverrideFlag);
            }

            writer.WriteLiteral(frameHeader.OrderHint, sequenceHeader.OrderHintInfo.OrderHintBits);
            if (!frameHeader.ErrorResilientMode && !frameHeader.IsIntra)
            {
                writer.WriteLiteral(frameHeader.PrimaryReferenceFrame, Av1Constants.PrimaryReferenceBits);
            }
        }

        if (sequenceHeader.DecoderModelInfoPresentFlag)
        {
            // The container track carries the timing of an image sequence, so encoded samples do not signal decoder-buffer removal times.
            writer.WriteBoolean(false);
        }

        if ((frameHeader.FrameType == ObuFrameType.KeyFrame && !frameHeader.ShowFrame) ||
            frameHeader.FrameType is ObuFrameType.InterFrame or ObuFrameType.IntraOnlyFrame)
        {
            writer.WriteLiteral(frameHeader.RefreshFrameFlags, Av1Constants.ReferenceFrameCount);
        }

        if ((!frameHeader.IsIntra || frameHeader.RefreshFrameFlags != byte.MaxValue) &&
            frameHeader.ErrorResilientMode &&
            sequenceHeader.OrderHintInfo.EnableOrderHint)
        {
            ReadOnlySpan<uint> referenceOrderHints = frameHeader.GetReferenceOrderHints();
            for (int slot = 0; slot < Av1Constants.ReferenceFrameCount; slot++)
            {
                writer.WriteLiteral(referenceOrderHints[slot], sequenceHeader.OrderHintInfo.OrderHintBits);
            }
        }

        if (frameHeader.FrameType == ObuFrameType.KeyFrame)
        {
            WriteFrameSize(ref writer, sequenceHeader, frameHeader, frameSizeOverrideFlag);
            WriteRenderSize(ref writer, frameHeader);
            if (frameHeader.AllowScreenContentTools)
            {
                writer.WriteBoolean(frameHeader.AllowIntraBlockCopy);
            }
        }
        else if (frameHeader.FrameType == ObuFrameType.IntraOnlyFrame)
        {
            WriteFrameSize(ref writer, sequenceHeader, frameHeader, frameSizeOverrideFlag);
            WriteRenderSize(ref writer, frameHeader);
            if (frameHeader.AllowScreenContentTools)
            {
                writer.WriteBoolean(frameHeader.AllowIntraBlockCopy);
            }
        }
        else
        {
            WriteReferenceFrameIndices(ref writer, sequenceHeader, frameHeader);
            if (!frameHeader.ErrorResilientMode && frameSizeOverrideFlag)
            {
                WriteFrameSizeWithReferences(ref writer, sequenceHeader, frameHeader);
            }
            else
            {
                WriteFrameSize(ref writer, sequenceHeader, frameHeader, frameSizeOverrideFlag);
                WriteRenderSize(ref writer, frameHeader);
            }

            if (!frameHeader.ForceIntegerMotionVector)
            {
                writer.WriteBoolean(frameHeader.AllowHighPrecisionMotionVector);
            }

            WriteFrameInterpolationFilter(ref writer, frameHeader.InterpolationFilter);
            writer.WriteBoolean(frameHeader.IsMotionModeSwitchable);
        }

        bool mightAllowReferenceFrameMotionVectors =
            !frameHeader.ErrorResilientMode &&
            sequenceHeader.OrderHintInfo.EnableReferenceFrameMotionVectors &&
            sequenceHeader.EnableOrderHint &&
            !frameHeader.IsIntra;

        if (mightAllowReferenceFrameMotionVectors)
        {
            writer.WriteBoolean(frameHeader.UseReferenceFrameMotionVectors);
        }

        if (!sequenceHeader.IsReducedStillPictureHeader && !frameHeader.DisableCdfUpdate)
        {
            writer.WriteBoolean(frameHeader.DisableFrameEndUpdateCdf);
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

        WriteTransformMode(ref writer, frameHeader);

        WriteFrameReferenceMode(ref writer, frameHeader);
        WriteSkipModeParameters(ref writer, frameHeader);
        if (!frameHeader.IsIntra && !frameHeader.ErrorResilientMode && sequenceHeader.EnableWarpedMotion)
        {
            writer.WriteBoolean(frameHeader.AllowWarpedMotion);
        }

        writer.WriteBoolean(frameHeader.UseReducedTransformSet);

        WriteGlobalMotionParameters(ref writer, frameHeader);
        WriteFilmGrainFilterParameters(ref writer, sequenceHeader, frameHeader);
    }

    /// <summary>
    /// Writes the seven reference-map slots selected by an inter frame.
    /// </summary>
    /// <param name="writer">The bit writer receiving the reference indices.</param>
    /// <param name="sequenceHeader">The sequence header defining frame-ID and order-hint syntax.</param>
    /// <param name="frameHeader">The frame header containing the selected reference slots.</param>
    private static void WriteReferenceFrameIndices(
        ref Av1BitStreamWriter writer,
        ObuSequenceHeader sequenceHeader,
        ObuFrameHeader frameHeader)
    {
        // The writer does not use short signaling. Explicit slot indices are deterministic and let every reference role select the same slot.
        if (sequenceHeader.EnableOrderHint)
        {
            writer.WriteBoolean(false);
        }

        Span<uint> referenceFrameIndices = frameHeader.GetReferenceFrameIndices();
        Span<uint> referenceFrameIds = frameHeader.GetReferenceFrameIds();
        uint frameIdModulus = 1U << sequenceHeader.FrameIdLength;
        for (int reference = 0; reference < Av1Constants.ReferencesPerFrame; reference++)
        {
            uint slot = referenceFrameIndices[reference];
            writer.WriteLiteral(slot, Av1Constants.ReferenceFrameIndexBits);
            if (sequenceHeader.IsFrameIdNumbersPresent)
            {
                uint deltaFrameId = (frameHeader.CurrentFrameId + frameIdModulus - referenceFrameIds[(int)slot]) % frameIdModulus;
                writer.WriteLiteral(deltaFrameId - 1, sequenceHeader.DeltaFrameIdLength);
            }
        }
    }

    /// <summary>
    /// Writes the frame-level interpolation-filter selection.
    /// </summary>
    /// <param name="writer">The bit writer receiving the filter selection.</param>
    /// <param name="filter">The fixed filter family or per-block selection.</param>
    private static void WriteFrameInterpolationFilter(ref Av1BitStreamWriter writer, Av1InterpolationFilter filter)
    {
        bool isSwitchable = filter == Av1InterpolationFilter.Switchable;
        writer.WriteBoolean(isSwitchable);
        if (!isSwitchable)
        {
            writer.WriteLiteral((uint)filter, 2);
        }
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
    /// Writes the byte-aligned tile-group header for a combined frame OBU.
    /// </summary>
    /// <param name="writer">The bit writer receiving the tile group.</param>
    /// <param name="tileInfo">The frame tile layout.</param>
    private static void WriteTileGroupHeader(ref Av1BitStreamWriter writer, ObuTileGroupHeader tileInfo)
    {
        int tileCount = tileInfo.TileColumnCount * tileInfo.TileRowCount;

        // The tile-group header starts at the next byte after the uncompressed frame header. The alignment comes before the optional flag,
        // because the flag belongs to the `tile_group_obu` syntax.
        AlignToByteBoundary(ref writer);

        if (tileCount > 1)
        {
            // A combined frame always carries all tiles in raster order. The zero bit selects these implicit bounds of the full frame. As a
            // result, the header has no explicit start and end tile indices.
            writer.WriteBoolean(false);
        }

        AlignToByteBoundary(ref writer);
    }

    /// <summary>
    /// Writes the size-prefixed tile payloads in raster order.
    /// </summary>
    /// <typeparam name="TTileWriter">The non-boxed tile source type.</typeparam>
    /// <param name="stream">The destination stream receiving tile data.</param>
    /// <param name="tileInfo">The frame tile layout and tile-size field width.</param>
    /// <param name="tileWriter">The writer that produces each tile payload.</param>
    private static void WriteTileData<TTileWriter>(
        Stream stream,
        ObuTileGroupHeader tileInfo,
        TTileWriter tileWriter)
        where TTileWriter : IAv1TileWriter
    {
        int tileCount = tileInfo.TileColumnCount * tileInfo.TileRowCount;
        Span<byte> tileSizeBuffer = stackalloc byte[sizeof(uint)];

        for (int tileNum = 0; tileNum < tileCount; tileNum++)
        {
            ReadOnlySpan<byte> tileData = tileWriter.GetTileData(tileNum);
            if (tileNum != tileCount - 1 && tileCount > 1)
            {
                // AV1 stores each non-final tile size minus one with the least-significant byte first.
                BinaryPrimitives.WriteUInt32LittleEndian(tileSizeBuffer, (uint)tileData.Length - 1U);
                stream.Write(tileSizeBuffer[..tileInfo.TileSizeBytes]);
            }

            stream.Write(tileData);
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
    /// Writes segmentation feature data for one coded frame.
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

        // A frame with no primary reference starts new segmentation state, so AV1 infers the update-map and update-data flags as enabled. Any
        // other frame signals the flags. That frame keeps the features of its primary reference unless it updates the data.
        if (frameHeader.PrimaryReferenceFrame != Av1Constants.PrimaryReferenceFrameNone)
        {
            writer.WriteBoolean(segmentation.SegmentationUpdateMap == 1);
            if (segmentation.SegmentationUpdateMap == 1)
            {
                writer.WriteBoolean(segmentation.SegmentationTemporalUpdate == 1);
            }

            writer.WriteBoolean(segmentation.SegmentationUpdateData == 1);
            if (segmentation.SegmentationUpdateData != 1)
            {
                return;
            }
        }

        for (int segmentId = 0; segmentId < Av1Constants.MaxSegmentCount; segmentId++)
        {
            for (int featureId = 0; featureId < Av1Constants.SegmentationLevelMax; featureId++)
            {
                bool enabled = segmentation.IsFeatureActive(segmentId, (ObuSegmentationLevelFeature)featureId);
                writer.WriteBoolean(enabled);
                if (!enabled)
                {
                    continue;
                }

                int bitCount = Av1Constants.SegmentationFeatureBits[featureId];
                int value = segmentation.GetFeatureData(segmentId, featureId);
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
        ObuLoopFilterParameters parameters = frameHeader.LoopFilterParameters;
        writer.WriteBoolean(parameters.ReferenceDeltaModeEnabled);
        if (!parameters.ReferenceDeltaModeEnabled)
        {
            return;
        }

        // The writer signals an update only when a delta differs from the deltas of the primary reference frame. If the frame has no primary
        // reference, the comparison uses the default deltas. The update then marks each delta that changed. This encoder always keeps the
        // default deltas, so every reference that it writes carries them. As a result, the defaults are the comparison for every frame.
        bool update = false;
        for (int i = 0; i < Av1Constants.TotalReferencesPerFrame; i++)
        {
            update |= parameters.ReferenceDeltas[i] != ObuLoopFilterParameters.GetDefaultReferenceDelta(i);
        }

        for (int i = 0; i < 2; i++)
        {
            update |= parameters.ModeDeltas[i] != 0;
        }

        writer.WriteBoolean(update);
        if (!update)
        {
            return;
        }

        for (int i = 0; i < Av1Constants.TotalReferencesPerFrame; i++)
        {
            bool changed = parameters.ReferenceDeltas[i] != ObuLoopFilterParameters.GetDefaultReferenceDelta(i);
            writer.WriteBoolean(changed);
            if (changed)
            {
                writer.WriteSignedFromUnsigned(parameters.ReferenceDeltas[i], 7);
            }
        }

        for (int i = 0; i < 2; i++)
        {
            bool changed = parameters.ModeDeltas[i] != 0;
            writer.WriteBoolean(changed);
            if (changed)
            {
                writer.WriteSignedFromUnsigned(parameters.ModeDeltas[i], 7);
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
    /// Writes global-motion parameters when the frame type permits them. Each model is coded relative to the model of the same reference in the
    /// primary reference frame.
    /// </summary>
    /// <param name="writer">The bit writer positioned at the global-motion syntax.</param>
    /// <param name="frameHeader">The current frame header.</param>
    private static void WriteGlobalMotionParameters(ref Av1BitStreamWriter writer, ObuFrameHeader frameHeader)
    {
        if (frameHeader.IsIntra)
        {
            return;
        }

        ReadOnlySpan<Av1GlobalMotionParameters> parameters = frameHeader.GetGlobalMotionParameters();
        ReadOnlySpan<Av1GlobalMotionParameters> referenceParameters = frameHeader.GetPreviousGlobalMotionParameters();
        for (int reference = 0; reference < Av1Constants.ReferencesPerFrame; reference++)
        {
            WriteGlobalMotionModel(
                ref writer,
                parameters[reference],
                referenceParameters[reference],
                frameHeader.AllowHighPrecisionMotionVector);
        }
    }

    /// <summary>
    /// Writes one global-motion model relative to the same-role model in the primary reference frame.
    /// </summary>
    /// <param name="writer">The bit writer positioned at the model syntax.</param>
    /// <param name="parameters">The model to write.</param>
    /// <param name="referenceParameters">The model of the same reference in the primary reference frame.</param>
    /// <param name="allowHighPrecisionMotionVector">Whether translation can keep one-eighth-sample precision.</param>
    private static void WriteGlobalMotionModel(
        ref Av1BitStreamWriter writer,
        Av1GlobalMotionParameters parameters,
        Av1GlobalMotionParameters referenceParameters,
        bool allowHighPrecisionMotionVector)
    {
        Av1GlobalMotionType type = parameters.Type;
        writer.WriteBoolean(type != Av1GlobalMotionType.Identity);
        if (type != Av1GlobalMotionType.Identity)
        {
            writer.WriteBoolean(type == Av1GlobalMotionType.RotationZoom);
            if (type != Av1GlobalMotionType.RotationZoom)
            {
                writer.WriteBoolean(type == Av1GlobalMotionType.Translation);
            }
        }

        WriteGlobalMotionModelParameters(ref writer, parameters, referenceParameters, allowHighPrecisionMotionVector);
    }

    /// <summary>
    /// Writes the parameters of one global-motion model, after its type. Each parameter is coded relative to the same parameter of the same-role
    /// model in the primary reference frame.
    /// </summary>
    /// <param name="writer">The bit writer positioned after the model type.</param>
    /// <param name="parameters">The model to write.</param>
    /// <param name="referenceParameters">The model of the same reference in the primary reference frame.</param>
    /// <param name="allowHighPrecisionMotionVector">Whether translation can keep one-eighth-sample precision.</param>
    private static void WriteGlobalMotionModelParameters(
        ref Av1BitStreamWriter writer,
        Av1GlobalMotionParameters parameters,
        Av1GlobalMotionParameters referenceParameters,
        bool allowHighPrecisionMotionVector)
    {
        Av1GlobalMotionType type = parameters.Type;
        if (type >= Av1GlobalMotionType.RotationZoom)
        {
            int horizontalScale =
                (parameters[2] >> Av1GlobalMotionParameters.AlphaPrecisionDifference) -
                (1 << Av1GlobalMotionParameters.AlphaPrecisionBits);

            int referenceHorizontalScale =
                (referenceParameters[2] >> Av1GlobalMotionParameters.AlphaPrecisionDifference) -
                (1 << Av1GlobalMotionParameters.AlphaPrecisionBits);

            writer.WriteSignedReferenceSubexponential(
                horizontalScale,
                Av1GlobalMotionParameters.AlphaValueMagnitude,
                Av1GlobalMotionParameters.SubexponentialGroupBitCount,
                referenceHorizontalScale);

            writer.WriteSignedReferenceSubexponential(
                parameters[3] >> Av1GlobalMotionParameters.AlphaPrecisionDifference,
                Av1GlobalMotionParameters.AlphaValueMagnitude,
                Av1GlobalMotionParameters.SubexponentialGroupBitCount,
                referenceParameters[3] >> Av1GlobalMotionParameters.AlphaPrecisionDifference);
        }

        if (type >= Av1GlobalMotionType.Affine)
        {
            int verticalScale =
                (parameters[5] >> Av1GlobalMotionParameters.AlphaPrecisionDifference) -
                (1 << Av1GlobalMotionParameters.AlphaPrecisionBits);

            int referenceVerticalScale =
                (referenceParameters[5] >> Av1GlobalMotionParameters.AlphaPrecisionDifference) -
                (1 << Av1GlobalMotionParameters.AlphaPrecisionBits);

            writer.WriteSignedReferenceSubexponential(
                parameters[4] >> Av1GlobalMotionParameters.AlphaPrecisionDifference,
                Av1GlobalMotionParameters.AlphaValueMagnitude,
                Av1GlobalMotionParameters.SubexponentialGroupBitCount,
                referenceParameters[4] >> Av1GlobalMotionParameters.AlphaPrecisionDifference);

            writer.WriteSignedReferenceSubexponential(
                verticalScale,
                Av1GlobalMotionParameters.AlphaValueMagnitude,
                Av1GlobalMotionParameters.SubexponentialGroupBitCount,
                referenceVerticalScale);
        }

        if (type >= Av1GlobalMotionType.Translation)
        {
            int precisionAdjustment =
                type == Av1GlobalMotionType.Translation && !allowHighPrecisionMotionVector ? 1 : 0;

            int translationBits = type == Av1GlobalMotionType.Translation
                ? Av1GlobalMotionParameters.AbsoluteTranslationOnlyBits - precisionAdjustment
                : Av1GlobalMotionParameters.AbsoluteTranslationBits;

            int translationPrecisionDifference = type == Av1GlobalMotionType.Translation
                ? Av1GlobalMotionParameters.ModelPrecisionBits -
                    Av1GlobalMotionParameters.TranslationOnlyPrecisionBits +
                    precisionAdjustment
                : Av1GlobalMotionParameters.ModelPrecisionBits -
                    Av1GlobalMotionParameters.TranslationPrecisionBits;

            int translationValueMagnitude = (1 << translationBits) + 1;
            writer.WriteSignedReferenceSubexponential(
                parameters[0] >> translationPrecisionDifference,
                translationValueMagnitude,
                Av1GlobalMotionParameters.SubexponentialGroupBitCount,
                referenceParameters[0] >> translationPrecisionDifference);

            writer.WriteSignedReferenceSubexponential(
                parameters[1] >> translationPrecisionDifference,
                translationValueMagnitude,
                Av1GlobalMotionParameters.SubexponentialGroupBitCount,
                referenceParameters[1] >> translationPrecisionDifference);
        }
    }

    /// <summary>
    /// Gets the number of header bits that the parameters of one global-motion model take, without its type bits. The result is a bit count,
    /// not a rate value.
    /// </summary>
    /// <param name="parameters">The model to measure.</param>
    /// <param name="referenceParameters">The model of the same reference in the primary reference frame.</param>
    /// <param name="allowHighPrecisionMotionVector">Whether translation can keep one-eighth-sample precision.</param>
    /// <returns>The parameter length in bits.</returns>
    internal static int GetGlobalMotionParameterBitCount(
        Av1GlobalMotionParameters parameters,
        Av1GlobalMotionParameters referenceParameters,
        bool allowHighPrecisionMotionVector)
    {
        InlineArray16<byte> storage = default;
        Span<byte> buffer = storage;
        Av1BitStreamWriter writer = new(buffer);
        WriteGlobalMotionModelParameters(
            ref writer,
            parameters,
            referenceParameters,
            allowHighPrecisionMotionVector);

        return writer.BitPosition;
    }

    /// <summary>
    /// Writes reference-mode selection when permitted by the frame type.
    /// </summary>
    /// <param name="writer">The bit writer positioned at the reference-mode syntax.</param>
    /// <param name="frameHeader">The current frame header.</param>
    private static void WriteFrameReferenceMode(ref Av1BitStreamWriter writer, ObuFrameHeader frameHeader)
    {
        if (frameHeader.IsIntra)
        {
            return;
        }

        writer.WriteBoolean(frameHeader.ReferenceMode == ObuReferenceMode.ReferenceModeSelect);
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
    /// Writes film-grain synthesis parameters for a displayed frame.
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

        // An inter frame can reuse the grain parameters of a reference slot. Other frame types always update them.
        if (frameHeader.FrameType == ObuFrameType.InterFrame)
        {
            writer.WriteBoolean(grainParams.UpdateGrain);
        }

        if (!grainParams.UpdateGrain)
        {
            writer.WriteLiteral(grainParams.FilmGrainParamsRefIdx, 3);
            return;
        }

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
