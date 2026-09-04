// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Text;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Heif;

internal sealed partial class HeifEncoderCore
{
    private const uint DefaultSequenceTimescale = 1000;
    private const uint FallbackSequenceTimescale = 1000000;
    private const uint UnityFixed16Point16 = 1U << 16;
    private const uint UnityFixed2Point30 = 1U << 30;
    private const ushort UnityFixed8Point8 = 1 << 8;
    private const ushort PackedUndeterminedLanguage = 0x55C4;

    private Av1EncodingSettings ResolveAv1Encoding<TPixel>(Image<TPixel> image)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        HeifMetadata metadata = image.Metadata.GetHeifMetadata();
        HeifBitDepth bitDepth = this.encoder.BitDepth ?? metadata.BitDepth;
        Av1BitDepth av1BitDepth = bitDepth switch
        {
            HeifBitDepth.Bit8 => Av1BitDepth.EightBit,
            HeifBitDepth.Bit10 => Av1BitDepth.TenBit,
            HeifBitDepth.Bit12 => Av1BitDepth.TwelveBit,
            _ => throw new NotSupportedException($"HEIF bit depth '{bitDepth}' is not supported.")
        };

        HeifChromaSubsampling defaultChromaSubsampling = this.encoder.Lossless
            ? HeifChromaSubsampling.Yuv444
            : HeifChromaSubsampling.Yuv420;

        HeifChromaSubsampling chromaSubsampling = this.encoder.ChromaSubsampling ??
            (metadata.IsMonochrome ? HeifChromaSubsampling.Monochrome : defaultChromaSubsampling);

        (bool isMonochrome, bool subsamplingX, bool subsamplingY) = chromaSubsampling switch
        {
            HeifChromaSubsampling.Monochrome => (true, true, true),
            HeifChromaSubsampling.Yuv420 => (false, true, true),
            HeifChromaSubsampling.Yuv422 => (false, true, false),
            HeifChromaSubsampling.Yuv444 => (false, false, false),
            _ => throw new NotSupportedException($"HEIF chroma sampling '{chromaSubsampling}' is not supported.")
        };

        CicpProfile? sourceColorProfile = image.Metadata.CicpProfile;
        CicpProfile colorProfile;
        if (sourceColorProfile is null)
        {
            colorProfile = new CicpProfile(2, 2, 6, false);
        }
        else
        {
            bool identityMatrix = sourceColorProfile.MatrixCoefficients == CicpMatrixCoefficients.Identity;
            bool legalIdentityMatrix = !isMonochrome
                && chromaSubsampling == HeifChromaSubsampling.Yuv444
                && sourceColorProfile.ColorPrimaries == CicpColorPrimaries.ItuRBt709_6
                && sourceColorProfile.TransferCharacteristics == CicpTransferCharacteristics.Iec61966_2_1;

            if (sourceColorProfile.MatrixCoefficients == CicpMatrixCoefficients.Unspecified
                || (identityMatrix && !legalIdentityMatrix))
            {
                // The converter uses BT.601 for unspecified or incompatible identity signaling, so record that actual matrix.
                colorProfile = new CicpProfile(
                    (byte)sourceColorProfile.ColorPrimaries,
                    (byte)sourceColorProfile.TransferCharacteristics,
                    (byte)CicpMatrixCoefficients.ItuRBt601_7_525,
                    sourceColorProfile.FullRange);
            }
            else if (identityMatrix && !sourceColorProfile.FullRange)
            {
                colorProfile = new CicpProfile(
                    (byte)sourceColorProfile.ColorPrimaries,
                    (byte)sourceColorProfile.TransferCharacteristics,
                    (byte)sourceColorProfile.MatrixCoefficients,
                    true);
            }
            else
            {
                colorProfile = sourceColorProfile;
            }
        }

        ObuColorConfig colorConfig = new()
        {
            IsColorDescriptionPresent = true,
            IsMonochrome = isMonochrome,
            ColorPrimaries = (ObuColorPrimaries)colorProfile.ColorPrimaries,
            TransferCharacteristics = (ObuTransferCharacteristics)colorProfile.TransferCharacteristics,
            MatrixCoefficients = (ObuMatrixCoefficients)colorProfile.MatrixCoefficients,
            ColorRange = colorProfile.FullRange,
            SubSamplingX = subsamplingX,
            SubSamplingY = subsamplingY,
            ChromaSamplePosition = ObuChromoSamplePosition.Unknown,
            BitDepth = av1BitDepth
        };

        ObuColorConfig alphaConfig = new()
        {
            IsMonochrome = true,
            ColorRange = true,
            SubSamplingX = true,
            SubSamplingY = true,
            BitDepth = av1BitDepth
        };

        int quality = this.encoder.Quality ?? 75;
        int colorQIndex = this.encoder.Lossless ? 0 : GetAv1QuantizerIndex(quality);
        int alphaQuality = this.encoder.AlphaQuality ?? quality;
        int alphaQIndex = this.encoder.Lossless ? 0 : GetAv1QuantizerIndex(alphaQuality);
        bool hasAlpha = TPixel.GetPixelTypeInfo().AlphaRepresentation != PixelAlphaRepresentation.None;
        return new Av1EncodingSettings(
            bitDepth,
            chromaSubsampling,
            colorProfile,
            colorConfig,
            alphaConfig,
            colorQIndex,
            alphaQIndex,
            hasAlpha);
    }

    private HeifSequenceEncoding CompressAv1Sequence<TPixel>(
        Image<TPixel> image,
        ChunkedMemoryStream stream,
        Av1EncodingSettings settings,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (image.Width > ushort.MaxValue || image.Height > ushort.MaxValue)
        {
            throw new NotSupportedException("AV1 image-sequence dimensions cannot exceed 65535 pixels.");
        }

        int frameCount = image.Frames.Count;
        uint timescale = GetSequenceTimescale(image);
        int sampleCount = checked(frameCount * (settings.HasAlpha ? 2 : 1));

        // The container needs only offset, length, and duration after each frame is streamed. Color and alpha
        // share one compact table, with each track occupying one contiguous slice for its complete operation lifetime.
        HeifSequenceSampleInfo[] samples = new HeifSequenceSampleInfo[sampleCount];
        Span<HeifSequenceSampleInfo> colorSamples = samples.AsSpan(0, frameCount);
        ImageFrame<TPixel> rootFrame = image.Frames.RootFrame;
        uint duration = GetSequenceSampleDuration(rootFrame.Metadata.GetHeifMetadata().FrameDelay, timescale);
        cancellationToken.ThrowIfCancellationRequested();
        long colorOffset = stream.Length;
        ObuSequenceHeader colorHeader = Av1FrameEncoder.Encode(
            this.configuration,
            rootFrame,
            stream,
            settings.ColorConfig,
            settings.ColorQIndex,
            this.encoder.Effort);

        colorSamples[0] = new HeifSequenceSampleInfo(
            colorOffset,
            checked((int)(stream.Length - colorOffset)),
            duration);

        for (int frameIndex = 1; frameIndex < frameCount; frameIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ImageFrame<TPixel> frame = image.Frames[frameIndex];
            duration = GetSequenceSampleDuration(frame.Metadata.GetHeifMetadata().FrameDelay, timescale);
            colorOffset = stream.Length;
            _ = Av1FrameEncoder.Encode(
                this.configuration,
                frame,
                stream,
                settings.ColorConfig,
                settings.ColorQIndex,
                this.encoder.Effort);

            colorSamples[frameIndex] = new HeifSequenceSampleInfo(
                colorOffset,
                checked((int)(stream.Length - colorOffset)),
                duration);
        }

        HeifSequenceTrackEncoding colorTrack = new(
            new Av1CodecConfiguration(colorHeader),
            samples,
            0,
            frameCount,
            false);

        HeifSequenceTrackEncoding? alphaTrack = null;
        if (settings.HasAlpha)
        {
            Span<HeifSequenceSampleInfo> alphaSamples = samples.AsSpan(frameCount, frameCount);
            cancellationToken.ThrowIfCancellationRequested();
            long alphaOffset = stream.Length;
            ObuSequenceHeader alphaHeader = Av1FrameEncoder.EncodeAlpha(
                this.configuration,
                rootFrame,
                stream,
                settings.AlphaConfig,
                settings.AlphaQIndex,
                this.encoder.Effort);

            alphaSamples[0] = new HeifSequenceSampleInfo(
                alphaOffset,
                checked((int)(stream.Length - alphaOffset)),
                colorSamples[0].Duration);

            for (int frameIndex = 1; frameIndex < frameCount; frameIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                alphaOffset = stream.Length;
                _ = Av1FrameEncoder.EncodeAlpha(
                    this.configuration,
                    image.Frames[frameIndex],
                    stream,
                    settings.AlphaConfig,
                    settings.AlphaQIndex,
                    this.encoder.Effort);

                alphaSamples[frameIndex] = new HeifSequenceSampleInfo(
                    alphaOffset,
                    checked((int)(stream.Length - alphaOffset)),
                    colorSamples[frameIndex].Duration);
            }

            alphaTrack = new HeifSequenceTrackEncoding(
                new Av1CodecConfiguration(alphaHeader),
                samples,
                frameCount,
                frameCount,
                true);
        }

        return new HeifSequenceEncoding(
            image.Width,
            image.Height,
            image.Metadata.GetHeifMetadata().RepeatCount,
            timescale,
            colorTrack,
            alphaTrack,
            settings.ColorProfile,
            this.encoder.SkipMetadata ? null : image.Metadata.IccProfile);
    }

    private int WriteSequenceFileTypeBox(Stream stream)
    {
        Span<byte> buffer = stackalloc byte[32];
        int bytesWritten = WriteBoxHeader(buffer, Heif4CharCode.Ftyp);
        BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], (uint)Heif4CharCode.Avis);
        bytesWritten += sizeof(uint);
        BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], 0);
        bytesWritten += sizeof(uint);
        BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], (uint)Heif4CharCode.Avif);
        bytesWritten += sizeof(uint);
        BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], (uint)Heif4CharCode.Msf1);
        bytesWritten += sizeof(uint);
        BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], (uint)Heif4CharCode.Iso8);
        bytesWritten += sizeof(uint);
        BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], (uint)Heif4CharCode.Avio);
        bytesWritten += sizeof(uint);
        BinaryPrimitives.WriteUInt32BigEndian(buffer, (uint)bytesWritten);
        stream.Write(buffer[..bytesWritten]);
        return bytesWritten;
    }

    private void WriteSequenceMovieBox(HeifSequenceEncoding sequence, int fileTypeLength, Stream stream)
    {
        // Chunk offsets point past the completed movie box, so retain only this bounded metadata box and patch
        // its two offsets once its size is known. The encoded frame payload remains in allocator-backed chunks.
        using AutoExpandingMemory<byte> memory = new(this.configuration, 0x1000);
        int offset = 0;
        int movieStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Moov);
        ulong mediaDuration = GetSequenceMediaDuration(sequence.ColorTrack.Samples);
        ulong trackDuration = sequence.RepeatCount == 0
            ? ulong.MaxValue
            : checked(mediaDuration * sequence.RepeatCount);
        bool hasAlpha = sequence.AlphaTrack.HasValue;

        WriteSequenceMovieHeader(
            memory,
            ref offset,
            sequence.Timescale,
            trackDuration,
            hasAlpha ? 3U : 2U);
        int colorChunkOffsetPosition = WriteSequenceTrack(
            memory,
            ref offset,
            sequence,
            sequence.ColorTrack,
            1,
            mediaDuration,
            trackDuration);

        int alphaChunkOffsetPosition = -1;
        long alphaPayloadOffset = 0;
        if (hasAlpha)
        {
            HeifSequenceTrackEncoding alphaTrack = sequence.AlphaTrack.GetValueOrDefault();
            alphaPayloadOffset = alphaTrack.Samples[0].Offset;
            alphaChunkOffsetPosition = WriteSequenceTrack(
                memory,
                ref offset,
                sequence,
                alphaTrack,
                2,
                mediaDuration,
                trackDuration);
        }

        EndSequenceBox(memory, movieStart, offset);
        ulong mediaDataOffset = checked((ulong)fileTypeLength + (uint)offset + 8U);
        Span<byte> movie = memory.GetSpan(offset);
        BinaryPrimitives.WriteUInt64BigEndian(
            movie[colorChunkOffsetPosition..],
            checked(mediaDataOffset + (ulong)sequence.ColorTrack.Samples[0].Offset));

        if (alphaChunkOffsetPosition >= 0)
        {
            BinaryPrimitives.WriteUInt64BigEndian(
                movie[alphaChunkOffsetPosition..],
                checked(mediaDataOffset + (ulong)alphaPayloadOffset));
        }

        stream.Write(movie);
    }

    private static void WriteSequenceMovieHeader(
        AutoExpandingMemory<byte> memory,
        ref int offset,
        uint timescale,
        ulong duration,
        uint nextTrackId)
    {
        int movieHeaderStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Mvhd);
        WriteSequenceFullBoxHeader(memory, ref offset, 1, 0);
        WriteSequenceUInt64(memory, ref offset, 0);
        WriteSequenceUInt64(memory, ref offset, 0);
        WriteSequenceUInt32(memory, ref offset, timescale);
        WriteSequenceUInt64(memory, ref offset, duration);
        WriteSequenceUInt32(memory, ref offset, UnityFixed16Point16);
        WriteSequenceUInt16(memory, ref offset, UnityFixed8Point8);
        WriteSequenceUInt16(memory, ref offset, 0);
        WriteSequenceZeros(memory, ref offset, 2 * sizeof(uint));
        WriteSequenceIdentityMatrix(memory, ref offset);
        WriteSequenceZeros(memory, ref offset, 6 * sizeof(uint));
        WriteSequenceUInt32(memory, ref offset, nextTrackId);
        EndSequenceBox(memory, movieHeaderStart, offset);
    }

    private static int WriteSequenceTrack(
        AutoExpandingMemory<byte> memory,
        ref int offset,
        HeifSequenceEncoding sequence,
        HeifSequenceTrackEncoding track,
        uint trackId,
        ulong mediaDuration,
        ulong trackDuration)
    {
        int trackStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Trak);
        WriteSequenceTrackHeader(
            memory,
            ref offset,
            sequence.Width,
            sequence.Height,
            trackId,
            trackDuration);

        if (track.IsAlpha)
        {
            WriteSequenceTrackReference(memory, ref offset, Heif4CharCode.Auxl, 1);
        }

        if (sequence.RepeatCount != 1)
        {
            WriteSequenceEditList(memory, ref offset, mediaDuration);
        }

        int mediaStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Mdia);
        WriteSequenceMediaHeader(memory, ref offset, sequence.Timescale, mediaDuration);
        WriteSequenceHandler(memory, ref offset, track.IsAlpha ? Heif4CharCode.Auxv : Heif4CharCode.Pict);
        int mediaInformationStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Minf);
        WriteSequenceDataInformation(memory, ref offset);
        int chunkOffsetPosition = WriteSequenceSampleTable(memory, ref offset, sequence, track);
        EndSequenceBox(memory, mediaInformationStart, offset);
        EndSequenceBox(memory, mediaStart, offset);
        EndSequenceBox(memory, trackStart, offset);
        return chunkOffsetPosition;
    }

    private static void WriteSequenceTrackHeader(
        AutoExpandingMemory<byte> memory,
        ref int offset,
        int width,
        int height,
        uint trackId,
        ulong duration)
    {
        int trackHeaderStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Tkhd);
        WriteSequenceFullBoxHeader(memory, ref offset, 1, 1);
        WriteSequenceUInt64(memory, ref offset, 0);
        WriteSequenceUInt64(memory, ref offset, 0);
        WriteSequenceUInt32(memory, ref offset, trackId);
        WriteSequenceUInt32(memory, ref offset, 0);
        WriteSequenceUInt64(memory, ref offset, duration);
        WriteSequenceZeros(memory, ref offset, (2 * sizeof(uint)) + (4 * sizeof(ushort)));
        WriteSequenceIdentityMatrix(memory, ref offset);
        WriteSequenceUInt32(memory, ref offset, checked((uint)width << 16));
        WriteSequenceUInt32(memory, ref offset, checked((uint)height << 16));
        EndSequenceBox(memory, trackHeaderStart, offset);
    }

    private static void WriteSequenceTrackReference(
        AutoExpandingMemory<byte> memory,
        ref int offset,
        Heif4CharCode referenceType,
        uint referencedTrackId)
    {
        int referencesStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Tref);
        int referenceStart = BeginSequenceBox(memory, ref offset, referenceType);
        WriteSequenceUInt32(memory, ref offset, referencedTrackId);
        EndSequenceBox(memory, referenceStart, offset);
        EndSequenceBox(memory, referencesStart, offset);
    }

    private static void WriteSequenceEditList(
        AutoExpandingMemory<byte> memory,
        ref int offset,
        ulong mediaDuration)
    {
        int editStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Edts);
        int editListStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Elst);
        WriteSequenceFullBoxHeader(memory, ref offset, 1, 1);
        WriteSequenceUInt32(memory, ref offset, 1);
        WriteSequenceUInt64(memory, ref offset, mediaDuration);
        WriteSequenceUInt64(memory, ref offset, 0);
        WriteSequenceUInt16(memory, ref offset, 1);
        WriteSequenceUInt16(memory, ref offset, 0);
        EndSequenceBox(memory, editListStart, offset);
        EndSequenceBox(memory, editStart, offset);
    }

    private static void WriteSequenceMediaHeader(
        AutoExpandingMemory<byte> memory,
        ref int offset,
        uint timescale,
        ulong mediaDuration)
    {
        int mediaHeaderStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Mdhd);
        WriteSequenceFullBoxHeader(memory, ref offset, 1, 0);
        WriteSequenceUInt64(memory, ref offset, 0);
        WriteSequenceUInt64(memory, ref offset, 0);
        WriteSequenceUInt32(memory, ref offset, timescale);
        WriteSequenceUInt64(memory, ref offset, mediaDuration);
        WriteSequenceUInt16(memory, ref offset, PackedUndeterminedLanguage);
        WriteSequenceUInt16(memory, ref offset, 0);
        EndSequenceBox(memory, mediaHeaderStart, offset);
    }

    private static void WriteSequenceHandler(
        AutoExpandingMemory<byte> memory,
        ref int offset,
        Heif4CharCode handlerType)
    {
        int handlerStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Hdlr);
        WriteSequenceFullBoxHeader(memory, ref offset, 0, 0);
        WriteSequenceUInt32(memory, ref offset, 0);
        WriteSequenceUInt32(memory, ref offset, (uint)handlerType);
        WriteSequenceZeros(memory, ref offset, 12);
        memory.GetSpan(offset++, 1)[0] = 0;
        EndSequenceBox(memory, handlerStart, offset);
    }

    private static void WriteSequenceDataInformation(AutoExpandingMemory<byte> memory, ref int offset)
    {
        int dataInformationStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Dinf);
        int dataReferenceStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Dref);
        WriteSequenceFullBoxHeader(memory, ref offset, 0, 0);
        WriteSequenceUInt32(memory, ref offset, 1);
        int locationStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Url);
        WriteSequenceFullBoxHeader(memory, ref offset, 0, 1);
        EndSequenceBox(memory, locationStart, offset);
        EndSequenceBox(memory, dataReferenceStart, offset);
        EndSequenceBox(memory, dataInformationStart, offset);
    }

    private static int WriteSequenceSampleTable(
        AutoExpandingMemory<byte> memory,
        ref int offset,
        HeifSequenceEncoding sequence,
        HeifSequenceTrackEncoding track)
    {
        int sampleTableStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Stbl);
        WriteSequenceSampleDescription(memory, ref offset, sequence, track);
        WriteSequenceSampleTiming(memory, ref offset, track.Samples);

        // Payloads are emitted contiguously per track, so one chunk maps directly to every sample in that track.
        int sampleToChunkStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Stsc);
        WriteSequenceFullBoxHeader(memory, ref offset, 0, 0);
        WriteSequenceUInt32(memory, ref offset, 1);
        WriteSequenceUInt32(memory, ref offset, 1);
        WriteSequenceUInt32(memory, ref offset, checked((uint)track.Samples.Length));
        WriteSequenceUInt32(memory, ref offset, 1);
        EndSequenceBox(memory, sampleToChunkStart, offset);

        int sampleSizesStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Stsz);
        WriteSequenceFullBoxHeader(memory, ref offset, 0, 0);
        WriteSequenceUInt32(memory, ref offset, 0);
        WriteSequenceUInt32(memory, ref offset, checked((uint)track.Samples.Length));
        foreach (HeifSequenceSampleInfo sample in track.Samples)
        {
            WriteSequenceUInt32(memory, ref offset, checked((uint)sample.Length));
        }

        EndSequenceBox(memory, sampleSizesStart, offset);

        int chunkOffsetsStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Co64);
        WriteSequenceFullBoxHeader(memory, ref offset, 0, 0);
        WriteSequenceUInt32(memory, ref offset, 1);
        int chunkOffsetPosition = offset;
        WriteSequenceUInt64(memory, ref offset, 0);
        EndSequenceBox(memory, chunkOffsetsStart, offset);

        // The current bounded sequence encoder emits independent all-intra pictures; every sample is seekable.
        int syncSamplesStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Stss);
        WriteSequenceFullBoxHeader(memory, ref offset, 0, 0);
        WriteSequenceUInt32(memory, ref offset, checked((uint)track.Samples.Length));
        for (uint sampleIndex = 1; sampleIndex <= track.Samples.Length; sampleIndex++)
        {
            WriteSequenceUInt32(memory, ref offset, sampleIndex);
        }

        EndSequenceBox(memory, syncSamplesStart, offset);
        EndSequenceBox(memory, sampleTableStart, offset);
        return chunkOffsetPosition;
    }

    private static void WriteSequenceSampleDescription(
        AutoExpandingMemory<byte> memory,
        ref int offset,
        HeifSequenceEncoding sequence,
        HeifSequenceTrackEncoding track)
    {
        int descriptionStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Stsd);
        WriteSequenceFullBoxHeader(memory, ref offset, 0, 0);
        WriteSequenceUInt32(memory, ref offset, 1);
        int sampleEntryStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Av01);
        WriteSequenceZeros(memory, ref offset, 6);
        WriteSequenceUInt16(memory, ref offset, 1);
        WriteSequenceZeros(memory, ref offset, (2 * sizeof(ushort)) + (3 * sizeof(uint)));
        WriteSequenceUInt16(memory, ref offset, checked((ushort)sequence.Width));
        WriteSequenceUInt16(memory, ref offset, checked((ushort)sequence.Height));
        WriteSequenceUInt32(memory, ref offset, 72U << 16);
        WriteSequenceUInt32(memory, ref offset, 72U << 16);
        WriteSequenceUInt32(memory, ref offset, 0);
        WriteSequenceUInt16(memory, ref offset, 1);
        WriteSequenceZeros(memory, ref offset, 32);
        WriteSequenceUInt16(memory, ref offset, 24);
        WriteSequenceUInt16(memory, ref offset, ushort.MaxValue);

        int configurationStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Av1C);
        track.Configuration.WriteFixedHeader(memory.GetSpan(offset, Av1CodecConfiguration.FixedHeaderSize));
        offset += Av1CodecConfiguration.FixedHeaderSize;
        EndSequenceBox(memory, configurationStart, offset);

        if (track.IsAlpha)
        {
            int auxiliaryTypeStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Auxi);
            WriteSequenceFullBoxHeader(memory, ref offset, 0, 0);
            int auxiliaryTypeLength = Encoding.UTF8.GetByteCount(HeifConstants.AlphaAuxiliaryType);
            Span<byte> auxiliaryType = memory.GetSpan(offset, auxiliaryTypeLength + 1);
            offset += Encoding.UTF8.GetBytes(HeifConstants.AlphaAuxiliaryType, auxiliaryType);
            memory.GetSpan(offset++, 1)[0] = 0;
            EndSequenceBox(memory, auxiliaryTypeStart, offset);
        }
        else
        {
            if (sequence.IccProfile is not null)
            {
                offset += WriteIccColorInformationPropertyBox(memory, offset, sequence.IccProfile);
            }

            offset += WriteColorInformationPropertyBox(memory, offset, sequence.ColorProfile);
        }

        int codingConstraintsStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Ccst);
        WriteSequenceFullBoxHeader(memory, ref offset, 0, 0);

        // Every emitted sequence sample is independently decodable, while intra prediction remains available inside
        // each picture. No inter-picture reference slot is therefore advertised.
        WriteSequenceUInt32(memory, ref offset, 0xC0000000);
        EndSequenceBox(memory, codingConstraintsStart, offset);
        EndSequenceBox(memory, sampleEntryStart, offset);
        EndSequenceBox(memory, descriptionStart, offset);
    }

    private static void WriteSequenceSampleTiming(
        AutoExpandingMemory<byte> memory,
        ref int offset,
        ReadOnlySpan<HeifSequenceSampleInfo> samples)
    {
        // The time-to-sample table stores runs, not one entry per frame. Preserve exact resolved durations while
        // combining only adjacent frames whose delays are equal.
        int runCount = 1;
        for (int sampleIndex = 1; sampleIndex < samples.Length; sampleIndex++)
        {
            runCount += samples[sampleIndex].Duration == samples[sampleIndex - 1].Duration ? 0 : 1;
        }

        int timingStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Stts);
        WriteSequenceFullBoxHeader(memory, ref offset, 0, 0);
        WriteSequenceUInt32(memory, ref offset, checked((uint)runCount));
        uint runDuration = samples[0].Duration;
        uint runLength = 1;
        for (int sampleIndex = 1; sampleIndex <= samples.Length; sampleIndex++)
        {
            if (sampleIndex < samples.Length && samples[sampleIndex].Duration == runDuration)
            {
                runLength++;
                continue;
            }

            WriteSequenceUInt32(memory, ref offset, runLength);
            WriteSequenceUInt32(memory, ref offset, runDuration);
            if (sampleIndex < samples.Length)
            {
                runDuration = samples[sampleIndex].Duration;
                runLength = 1;
            }
        }

        EndSequenceBox(memory, timingStart, offset);
    }

    private static uint GetSequenceSampleDuration(Rational delay, uint timescale)
    {
        if (delay.Numerator == 0)
        {
            return 1;
        }

        ulong scaledDuration = ((ulong)delay.Numerator * timescale) + (delay.Denominator / 2U);
        return checked((uint)Math.Max(1UL, scaledDuration / delay.Denominator));
    }

    private static uint GetSequenceTimescale<TPixel>(Image<TPixel> image)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        uint timescale = DefaultSequenceTimescale;
        foreach (ImageFrame<TPixel> frame in image.Frames)
        {
            Rational delay = frame.Metadata.GetHeifMetadata().FrameDelay;
            if (delay.Numerator == 0)
            {
                continue;
            }

            uint commonDivisor = GetGreatestCommonDivisor(timescale, delay.Denominator);
            ulong commonTimescale = ((ulong)timescale / commonDivisor) * delay.Denominator;
            if (commonTimescale > uint.MaxValue)
            {
                // A media timescale is a 32-bit field. Microsecond fallback retains bounded timing precision when
                // the exact least common multiple of caller-provided rational delays cannot be represented.
                return FallbackSequenceTimescale;
            }

            timescale = (uint)commonTimescale;
        }

        return timescale;
    }

    private static uint GetGreatestCommonDivisor(uint left, uint right)
    {
        while (right != 0)
        {
            uint remainder = left % right;
            left = right;
            right = remainder;
        }

        return left;
    }

    private static ulong GetSequenceMediaDuration(ReadOnlySpan<HeifSequenceSampleInfo> samples)
    {
        ulong duration = 0;
        foreach (HeifSequenceSampleInfo sample in samples)
        {
            duration = checked(duration + sample.Duration);
        }

        return duration;
    }

    private static int BeginSequenceBox(
        AutoExpandingMemory<byte> memory,
        ref int offset,
        Heif4CharCode type)
    {
        // Reserve the size field now and patch it at the matching EndSequenceBox call after nested boxes expand.
        int start = offset;
        offset += WriteBoxHeader(memory.GetSpan(offset, 8), type);
        return start;
    }

    private static void EndSequenceBox(AutoExpandingMemory<byte> memory, int start, int offset)
        => BinaryPrimitives.WriteUInt32BigEndian(
            memory.GetSpan(start, sizeof(uint)),
            checked((uint)(offset - start)));

    private static void WriteSequenceFullBoxHeader(
        AutoExpandingMemory<byte> memory,
        ref int offset,
        byte version,
        uint flags)
    {
        Span<byte> destination = memory.GetSpan(offset, sizeof(uint));
        BinaryPrimitives.WriteUInt32BigEndian(destination, flags);
        destination[0] = version;
        offset += sizeof(uint);
    }

    private static void WriteSequenceIdentityMatrix(AutoExpandingMemory<byte> memory, ref int offset)
    {
        WriteSequenceUInt32(memory, ref offset, UnityFixed16Point16);
        WriteSequenceUInt32(memory, ref offset, 0);
        WriteSequenceUInt32(memory, ref offset, 0);
        WriteSequenceUInt32(memory, ref offset, 0);
        WriteSequenceUInt32(memory, ref offset, UnityFixed16Point16);
        WriteSequenceUInt32(memory, ref offset, 0);
        WriteSequenceUInt32(memory, ref offset, 0);
        WriteSequenceUInt32(memory, ref offset, 0);
        WriteSequenceUInt32(memory, ref offset, UnityFixed2Point30);
    }

    private static void WriteSequenceZeros(AutoExpandingMemory<byte> memory, ref int offset, int length)
    {
        memory.GetSpan(offset, length).Clear();
        offset += length;
    }

    private static void WriteSequenceUInt16(AutoExpandingMemory<byte> memory, ref int offset, ushort value)
    {
        BinaryPrimitives.WriteUInt16BigEndian(memory.GetSpan(offset, sizeof(ushort)), value);
        offset += sizeof(ushort);
    }

    private static void WriteSequenceUInt32(AutoExpandingMemory<byte> memory, ref int offset, uint value)
    {
        BinaryPrimitives.WriteUInt32BigEndian(memory.GetSpan(offset, sizeof(uint)), value);
        offset += sizeof(uint);
    }

    private static void WriteSequenceUInt64(AutoExpandingMemory<byte> memory, ref int offset, ulong value)
    {
        BinaryPrimitives.WriteUInt64BigEndian(memory.GetSpan(offset, sizeof(ulong)), value);
        offset += sizeof(ulong);
    }

    private readonly struct Av1EncodingSettings
    {
        public Av1EncodingSettings(
            HeifBitDepth bitDepth,
            HeifChromaSubsampling chromaSubsampling,
            CicpProfile colorProfile,
            ObuColorConfig colorConfig,
            ObuColorConfig alphaConfig,
            int colorQIndex,
            int alphaQIndex,
            bool hasAlpha)
        {
            this.BitDepth = bitDepth;
            this.ChromaSubsampling = chromaSubsampling;
            this.ColorProfile = colorProfile;
            this.ColorConfig = colorConfig;
            this.AlphaConfig = alphaConfig;
            this.ColorQIndex = colorQIndex;
            this.AlphaQIndex = alphaQIndex;
            this.HasAlpha = hasAlpha;
        }

        public HeifBitDepth BitDepth { get; }

        public HeifChromaSubsampling ChromaSubsampling { get; }

        public CicpProfile ColorProfile { get; }

        public ObuColorConfig ColorConfig { get; }

        public ObuColorConfig AlphaConfig { get; }

        public int ColorQIndex { get; }

        public int AlphaQIndex { get; }

        public bool HasAlpha { get; }
    }

    private readonly struct HeifSequenceSampleInfo
    {
        public HeifSequenceSampleInfo(long offset, int length, uint duration)
        {
            this.Offset = offset;
            this.Length = length;
            this.Duration = duration;
        }

        public long Offset { get; }

        public int Length { get; }

        public uint Duration { get; }
    }

    private readonly struct HeifSequenceEncoding
    {
        public HeifSequenceEncoding(
            int width,
            int height,
            ushort repeatCount,
            uint timescale,
            HeifSequenceTrackEncoding colorTrack,
            HeifSequenceTrackEncoding? alphaTrack,
            CicpProfile colorProfile,
            IccProfile? iccProfile)
        {
            this.Width = width;
            this.Height = height;
            this.RepeatCount = repeatCount;
            this.Timescale = timescale;
            this.ColorTrack = colorTrack;
            this.AlphaTrack = alphaTrack;
            this.ColorProfile = colorProfile;
            this.IccProfile = iccProfile;
        }

        public int Width { get; }

        public int Height { get; }

        public ushort RepeatCount { get; }

        public uint Timescale { get; }

        public HeifSequenceTrackEncoding ColorTrack { get; }

        public HeifSequenceTrackEncoding? AlphaTrack { get; }

        public CicpProfile ColorProfile { get; }

        public IccProfile? IccProfile { get; }
    }

    private readonly struct HeifSequenceTrackEncoding
    {
        private readonly HeifSequenceSampleInfo[] samples;
        private readonly int sampleOffset;
        private readonly int sampleCount;

        public HeifSequenceTrackEncoding(
            Av1CodecConfiguration configuration,
            HeifSequenceSampleInfo[] samples,
            int sampleOffset,
            int sampleCount,
            bool isAlpha)
        {
            this.Configuration = configuration;
            this.samples = samples;
            this.sampleOffset = sampleOffset;
            this.sampleCount = sampleCount;
            this.IsAlpha = isAlpha;
        }

        public Av1CodecConfiguration Configuration { get; }

        public ReadOnlySpan<HeifSequenceSampleInfo> Samples
            => this.samples.AsSpan(this.sampleOffset, this.sampleCount);

        public bool IsAlpha { get; }
    }
}
