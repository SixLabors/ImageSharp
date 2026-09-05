// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Heif;

internal sealed partial class HeifEncoderCore
{
    /// <summary>
    /// The millisecond media timescale used when every frame delay can be represented exactly.
    /// </summary>
    private const uint DefaultSequenceTimescale = 1000;

    /// <summary>
    /// The microsecond fallback used when the exact common frame-delay timescale exceeds 32 bits.
    /// </summary>
    private const uint FallbackSequenceTimescale = 1000000;

    /// <summary>
    /// The identity value for signed 16.16 movie and track matrix entries.
    /// </summary>
    private const uint UnityFixed16Point16 = 1U << 16;

    /// <summary>
    /// The identity value for the signed 2.30 homogeneous movie and track matrix entry.
    /// </summary>
    private const uint UnityFixed2Point30 = 1U << 30;

    /// <summary>
    /// The identity value for unsigned 8.8 track volume.
    /// </summary>
    private const ushort UnityFixed8Point8 = 1 << 8;

    /// <summary>
    /// The packed ISO 639-2/T language code for undetermined content.
    /// </summary>
    private const ushort PackedUndeterminedLanguage = 0x55C4;

    /// <summary>
    /// The coding-constraints flag stating that every reference picture is intra.
    /// </summary>
    private const uint AllReferencePicturesIntraMask = 1U << 31;

    /// <summary>
    /// The coding-constraints flag stating that intra prediction is used.
    /// </summary>
    private const uint IntraPicturePredictionUsedMask = 1U << 30;

    /// <summary>
    /// The conventional 72-dpi horizontal and vertical resolution stored as unsigned 16.16.
    /// </summary>
    private const uint DefaultVisualSampleResolution = 72U << 16;

    /// <summary>
    /// The fixed visual-sample-entry compressor-name field length.
    /// </summary>
    private const int VisualSampleCompressorNameLength = 32;

    /// <summary>
    /// The visual-sample-entry depth used for color pictures.
    /// </summary>
    private const ushort VisualSampleDepth = 24;

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

        if (this.encoder.ChromaSubsampling is null
            && image.Frames.Count == 1
            && (image.Width > Av1Constants.MaxFrameDimension || image.Height > Av1Constants.MaxFrameDimension)
            && ((chromaSubsampling == HeifChromaSubsampling.Yuv420
                    && (((image.Width & 1) != 0) || ((image.Height & 1) != 0)))
                || (chromaSubsampling == HeifChromaSubsampling.Yuv422 && (image.Width & 1) != 0)))
        {
            // A derived grid requires even output dimensions on every subsampled axis. When sampling was not
            // explicitly requested, retain the complete image dimensions by selecting full-resolution chroma.
            chromaSubsampling = HeifChromaSubsampling.Yuv444;
        }

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
            colorProfile = new CicpProfile(
                (byte)CicpColorPrimaries.Unspecified,
                (byte)CicpTransferCharacteristics.Unspecified,
                (byte)CicpMatrixCoefficients.ItuRBt601_7_525,
                false);
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
        Memory<HeifSequenceSampleInfo> samples,
        int firstFrameIndex,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        byte[]? exifData = null;
        uint tiffHeaderOffset = 0;
        byte[]? xmpData = null;
        if (!this.encoder.SkipMetadata)
        {
            exifData = GetExifData(image.Metadata, out tiffHeaderOffset);
            byte[]? sourceXmpData = image.Metadata.XmpProfile?.Data;
            if (sourceXmpData is not null && sourceXmpData.Length > 0)
            {
                xmpData = sourceXmpData;
            }
        }

        int frameCount = image.Frames.Count - firstFrameIndex;
        uint timescale = GetSequenceTimescale(image, firstFrameIndex);

        // The container needs only offset, length, and duration after each frame is streamed. Color and alpha
        // share one allocator-owned table, with each track occupying one contiguous slice until moov is written.
        Span<HeifSequenceSampleInfo> colorSamples = samples.Span[..frameCount];
        ImageFrame<TPixel> firstFrame = image.Frames[firstFrameIndex];
        ObuSequenceHeader colorHeader;
        bool colorUsesInterPrediction = settings.ColorQIndex != 0;
        using (Av1FrameEncoder.SequenceEncoder colorEncoder = Av1FrameEncoder.CreateColorSequenceEncoder(
            this.configuration,
            image.Width,
            image.Height,
            settings.ColorConfig,
            settings.ColorQIndex,
            this.encoder.Effort))
        {
            cancellationToken.ThrowIfCancellationRequested();
            long colorOffset = stream.Length;
            colorEncoder.EncodeKeyFrame(firstFrame, stream);
            colorHeader = colorEncoder.SequenceHeader;

            colorSamples[0] = new HeifSequenceSampleInfo(
                colorOffset,
                checked((int)(stream.Length - colorOffset)),
                GetSequenceSampleDuration(firstFrame.Metadata.GetHeifMetadata().FrameDelay, timescale),
                isSyncSample: true);

            for (int sampleIndex = 1; sampleIndex < frameCount; sampleIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int frameIndex = firstFrameIndex + sampleIndex;
                ImageFrame<TPixel> frame = image.Frames[frameIndex];
                uint duration = GetSequenceSampleDuration(frame.Metadata.GetHeifMetadata().FrameDelay, timescale);
                colorOffset = stream.Length;
                if (colorUsesInterPrediction)
                {
                    colorEncoder.EncodeInterFrame(frame, stream);
                }
                else
                {
                    // Lossless AV1 requires 4x4 transforms. Until the inter path supports that reversible size,
                    // continuation samples remain independent key frames instead of weakening losslessness.
                    colorEncoder.EncodeKeyFrame(frame, stream);
                }

                colorSamples[sampleIndex] = new HeifSequenceSampleInfo(
                    colorOffset,
                    checked((int)(stream.Length - colorOffset)),
                    duration,
                    isSyncSample: !colorUsesInterPrediction);
            }
        }

        HeifSequenceTrackEncoding colorTrack = new(
            new Av1CodecConfiguration(colorHeader),
            samples[..frameCount],
            false);

        HeifSequenceTrackEncoding? alphaTrack = null;
        if (settings.HasAlpha)
        {
            Memory<HeifSequenceSampleInfo> alphaSampleMemory = samples.Slice(frameCount, frameCount);
            Span<HeifSequenceSampleInfo> alphaSamples = alphaSampleMemory.Span;
            ObuSequenceHeader alphaHeader;
            bool alphaUsesInterPrediction = settings.AlphaQIndex != 0;
            using (Av1FrameEncoder.SequenceEncoder alphaEncoder = Av1FrameEncoder.CreateAlphaSequenceEncoder(
                this.configuration,
                image.Width,
                image.Height,
                settings.AlphaConfig,
                settings.AlphaQIndex,
                this.encoder.Effort))
            {
                cancellationToken.ThrowIfCancellationRequested();
                long alphaOffset = stream.Length;
                alphaEncoder.EncodeKeyFrame(firstFrame, stream);
                alphaHeader = alphaEncoder.SequenceHeader;

                alphaSamples[0] = new HeifSequenceSampleInfo(
                    alphaOffset,
                    checked((int)(stream.Length - alphaOffset)),
                    colorSamples[0].Duration,
                    isSyncSample: true);

                for (int sampleIndex = 1; sampleIndex < frameCount; sampleIndex++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int frameIndex = firstFrameIndex + sampleIndex;
                    alphaOffset = stream.Length;
                    if (alphaUsesInterPrediction)
                    {
                        alphaEncoder.EncodeInterFrame(image.Frames[frameIndex], stream);
                    }
                    else
                    {
                        alphaEncoder.EncodeKeyFrame(image.Frames[frameIndex], stream);
                    }

                    alphaSamples[sampleIndex] = new HeifSequenceSampleInfo(
                        alphaOffset,
                        checked((int)(stream.Length - alphaOffset)),
                        colorSamples[sampleIndex].Duration,
                        isSyncSample: !alphaUsesInterPrediction);
                }
            }

            alphaTrack = new HeifSequenceTrackEncoding(
                new Av1CodecConfiguration(alphaHeader),
                alphaSampleMemory,
                true);
        }

        ReadOnlyMemory<byte> iccProfileData = ReadOnlyMemory<byte>.Empty;
        IccProfile? iccProfile = image.Metadata.IccProfile;
        if (!this.encoder.SkipMetadata && iccProfile is not null)
        {
            iccProfileData = iccProfile.GetDataForWriting();
        }

        return new HeifSequenceEncoding(
            image.Width,
            image.Height,
            this.encoder.RepeatCount ?? image.Metadata.GetHeifMetadata().RepeatCount,
            timescale,
            colorTrack,
            alphaTrack,
            settings.ColorProfile,
            iccProfileData,
            exifData,
            tiffHeaderOffset,
            xmpData);
    }

    private int WriteSequenceFileTypeBox(Stream stream)
    {
        Span<byte> buffer = stackalloc byte[44];
        int bytesWritten = WriteBoxHeader(buffer, Heif4CharCode.Ftyp);
        BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], (uint)Heif4CharCode.Avis);
        bytesWritten += sizeof(uint);
        BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], 0);
        bytesWritten += sizeof(uint);
        BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], (uint)Heif4CharCode.Avif);
        bytesWritten += sizeof(uint);
        BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], (uint)Heif4CharCode.Avio);
        bytesWritten += sizeof(uint);
        BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], (uint)Heif4CharCode.Avis);
        bytesWritten += sizeof(uint);
        BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], (uint)Heif4CharCode.Msf1);
        bytesWritten += sizeof(uint);
        BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], (uint)Heif4CharCode.Iso8);
        bytesWritten += sizeof(uint);
        BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], (uint)Heif4CharCode.Mif1);
        bytesWritten += sizeof(uint);
        BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], (uint)Heif4CharCode.Miaf);
        bytesWritten += sizeof(uint);
        BinaryPrimitives.WriteUInt32BigEndian(buffer, (uint)bytesWritten);
        stream.Write(buffer[..bytesWritten]);
        return bytesWritten;
    }

    private void WriteSequenceMovieBox(HeifSequenceEncoding sequence, int precedingBoxLength, Stream stream)
    {
        int movieLength = GetSequenceMovieBoxLength(sequence);
        using IMemoryOwner<byte> movieOwner = this.configuration.MemoryAllocator.Allocate<byte>(movieLength);
        Span<byte> memory = movieOwner.Memory.Span[..movieLength];
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
        ulong mediaDataOffset = checked((ulong)precedingBoxLength + (uint)offset + 8U);
        BinaryPrimitives.WriteUInt64BigEndian(
            memory[colorChunkOffsetPosition..],
            checked(mediaDataOffset + (ulong)sequence.ColorTrack.Samples[0].Offset));

        if (alphaChunkOffsetPosition >= 0)
        {
            BinaryPrimitives.WriteUInt64BigEndian(
                memory[alphaChunkOffsetPosition..],
                checked(mediaDataOffset + (ulong)alphaPayloadOffset));
        }

        stream.Write(memory);
    }

    private static int GetSequenceMovieBoxLength(HeifSequenceEncoding sequence)
    {
        const int movieHeaderBoxLength = 120;
        const int trackHeaderBoxLength = 104;
        const int trackReferenceBoxLength = 20;
        const int editListBoxLength = 44;
        const int mediaBoxFixedLength = 129;
        const int colorInformationBoxLength = 19;
        const int codecConfigurationBoxLength = 12;
        const int codingConstraintsBoxLength = 16;
        const int visualSampleEntryLength = 86;
        const int sampleDescriptionBoxLength = 16;
        const int sampleTableBoxHeaderLength = 8;
        const int timeToSampleBoxFixedLength = 16;
        const int sampleToChunkBoxLength = 28;
        const int sampleSizeBoxFixedLength = 20;
        const int chunkOffsetBoxLength = 24;
        const int syncSampleBoxFixedLength = 16;
        const int timingRunLength = 8;
        const int sampleSizeEntryLength = sizeof(uint);
        const int syncSampleEntryLength = sizeof(uint);
        const int sampleTableFixedLength =
            sampleTableBoxHeaderLength
            + sampleDescriptionBoxLength
            + visualSampleEntryLength
            + codecConfigurationBoxLength
            + codingConstraintsBoxLength
            + timeToSampleBoxFixedLength
            + sampleToChunkBoxLength
            + sampleSizeBoxFixedLength
            + chunkOffsetBoxLength
            + syncSampleBoxFixedLength;

        const int metadataFixedLength = 83;
        const int metadataLocationLength = 16;
        const int exifInformationLength = 25;
        const int xmpInformationLength = 44;
        const int exifOffsetLength = sizeof(uint);

        int repeatBoxLength = sequence.RepeatCount == 1 ? 0 : editListBoxLength;
        int colorRunCount = GetSequenceTimingRunCount(sequence.ColorTrack.Samples);
        int colorSyncSampleCount = GetSequenceSyncSampleCount(sequence.ColorTrack.Samples);
        long colorSampleTableLength =
            (long)sampleTableFixedLength
            + (colorRunCount * timingRunLength)
            + (sequence.ColorTrack.Samples.Length * sampleSizeEntryLength)
            + (colorSyncSampleCount * syncSampleEntryLength)
            + colorInformationBoxLength;

        if (!sequence.IccProfileData.IsEmpty)
        {
            colorSampleTableLength = colorSampleTableLength
                + IccColorInformationPropertyBoxFixedLength
                + sequence.IccProfileData.Length;
        }

        byte[]? exifData = sequence.ExifData;
        byte[]? xmpData = sequence.XmpData;
        long metadataLength = 0;
        if (exifData is not null || xmpData is not null)
        {
            int metadataItemCount = (exifData is not null ? 1 : 0) + (xmpData is not null ? 1 : 0);
            metadataLength = (long)metadataFixedLength
                + (metadataItemCount * metadataLocationLength)
                + (exifData is not null ? (long)exifInformationLength + exifOffsetLength + exifData.Length : 0)
                + (xmpData is not null ? (long)xmpInformationLength + xmpData.Length : 0);
        }

        long colorTrackLength =
            BasicBoxHeaderLength
            + trackHeaderBoxLength
            + repeatBoxLength
            + metadataLength
            + mediaBoxFixedLength
            + colorSampleTableLength;

        long alphaTrackLength = 0;
        if (sequence.AlphaTrack.HasValue)
        {
            HeifSequenceTrackEncoding alphaTrack = sequence.AlphaTrack.GetValueOrDefault();
            int alphaRunCount = GetSequenceTimingRunCount(alphaTrack.Samples);
            int alphaSyncSampleCount = GetSequenceSyncSampleCount(alphaTrack.Samples);
            int auxiliaryTypeBoxLength =
                FullBoxHeaderLength
                + Encoding.UTF8.GetByteCount(HeifConstants.AlphaAuxiliaryType)
                + 1;

            long alphaSampleTableLength =
                (long)sampleTableFixedLength
                + (alphaRunCount * timingRunLength)
                + (alphaTrack.Samples.Length * sampleSizeEntryLength)
                + (alphaSyncSampleCount * syncSampleEntryLength)
                + auxiliaryTypeBoxLength;

            alphaTrackLength =
                BasicBoxHeaderLength
                + trackHeaderBoxLength
                + trackReferenceBoxLength
                + repeatBoxLength
                + mediaBoxFixedLength
                + alphaSampleTableLength;
        }

        // The movie box contains one header and one or two tracks. Every nested variable-length field above is
        // resolved before this exact allocation, so container writing cannot re-rent or copy its buffer.
        long movieLength = BasicBoxHeaderLength + movieHeaderBoxLength + colorTrackLength + alphaTrackLength;
        return checked((int)movieLength);
    }

    private static void WriteSequenceMovieHeader(
        Span<byte> memory,
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
        Span<byte> memory,
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

        if (!track.IsAlpha && (sequence.ExifData is not null || sequence.XmpData is not null))
        {
            WriteSequenceTrackMetadata(memory, ref offset, sequence);
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
        Span<byte> memory,
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
        WriteSequenceUInt32(memory, ref offset, (uint)width << 16);
        WriteSequenceUInt32(memory, ref offset, (uint)height << 16);
        EndSequenceBox(memory, trackHeaderStart, offset);
    }

    private static void WriteSequenceTrackReference(
        Span<byte> memory,
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
        Span<byte> memory,
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

    private static void WriteSequenceTrackMetadata(
        Span<byte> memory,
        ref int offset,
        HeifSequenceEncoding sequence)
    {
        const byte fourByteOffsetAndLengthSizes = 0x44;
        byte[]? exifData = sequence.ExifData;
        byte[]? xmpData = sequence.XmpData;
        ushort itemCount = (ushort)((exifData is not null ? 1 : 0) + (xmpData is not null ? 1 : 0));
        int metadataStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Meta);
        WriteSequenceFullBoxHeader(memory, ref offset, 0, 0);
        WriteSequenceHandler(memory, ref offset, Heif4CharCode.Pict);

        // Construction method one makes each extent relative to the local idat payload, keeping metadata independent
        // of the final file and movie-box offsets.
        int locationsStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Iloc);
        WriteSequenceFullBoxHeader(memory, ref offset, 1, 0);
        memory[offset++] = fourByteOffsetAndLengthSizes;
        memory[offset++] = 0;
        WriteSequenceUInt16(memory, ref offset, itemCount);
        ushort itemId = 1;
        uint itemDataOffset = 0;
        if (exifData is not null)
        {
            uint exifLength = (uint)exifData.Length + sizeof(uint);
            WriteSequenceTrackMetadataLocation(memory, ref offset, itemId++, itemDataOffset, exifLength);
            itemDataOffset += exifLength;
        }

        if (xmpData is not null)
        {
            WriteSequenceTrackMetadataLocation(
                memory,
                ref offset,
                itemId,
                itemDataOffset,
                (uint)xmpData.Length);
        }

        EndSequenceBox(memory, locationsStart, offset);

        int informationStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Iinf);
        WriteSequenceFullBoxHeader(memory, ref offset, 0, 0);
        WriteSequenceUInt16(memory, ref offset, itemCount);
        itemId = 1;
        if (exifData is not null)
        {
            WriteSequenceTrackMetadataItem(memory, ref offset, itemId++, Heif4CharCode.Exif);
        }

        if (xmpData is not null)
        {
            WriteSequenceTrackMetadataItem(memory, ref offset, itemId, Heif4CharCode.Mime);
        }

        EndSequenceBox(memory, informationStart, offset);

        int itemDataStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Idat);
        if (exifData is not null)
        {
            WriteSequenceUInt32(memory, ref offset, sequence.ExifTiffHeaderOffset);
            WriteSequenceBytes(memory, ref offset, exifData);
        }

        if (xmpData is not null)
        {
            WriteSequenceBytes(memory, ref offset, xmpData);
        }

        EndSequenceBox(memory, itemDataStart, offset);
        EndSequenceBox(memory, metadataStart, offset);
    }

    private static void WriteSequenceTrackMetadataLocation(
        Span<byte> memory,
        ref int offset,
        ushort itemId,
        uint itemDataOffset,
        uint itemLength)
    {
        WriteSequenceUInt16(memory, ref offset, itemId);
        WriteSequenceUInt16(memory, ref offset, 1);
        WriteSequenceUInt16(memory, ref offset, 0);
        WriteSequenceUInt16(memory, ref offset, 1);
        WriteSequenceUInt32(memory, ref offset, itemDataOffset);
        WriteSequenceUInt32(memory, ref offset, itemLength);
    }

    private static void WriteSequenceTrackMetadataItem(
        Span<byte> memory,
        ref int offset,
        ushort itemId,
        Heif4CharCode itemType)
    {
        int itemStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Infe);
        WriteSequenceFullBoxHeader(memory, ref offset, 2, 0);
        WriteSequenceUInt16(memory, ref offset, itemId);
        WriteSequenceUInt16(memory, ref offset, 0);
        WriteSequenceUInt32(memory, ref offset, (uint)itemType);
        ReadOnlySpan<byte> itemName = itemType == Heif4CharCode.Exif ? "Exif"u8 : "XMP"u8;
        WriteSequenceBytes(memory, ref offset, itemName);
        memory[offset++] = 0;
        if (itemType == Heif4CharCode.Mime)
        {
            WriteSequenceBytes(memory, ref offset, "application/rdf+xml"u8);
            memory[offset++] = 0;
        }

        EndSequenceBox(memory, itemStart, offset);
    }

    private static void WriteSequenceMediaHeader(
        Span<byte> memory,
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
        Span<byte> memory,
        ref int offset,
        Heif4CharCode handlerType)
    {
        int handlerStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Hdlr);
        WriteSequenceFullBoxHeader(memory, ref offset, 0, 0);
        WriteSequenceUInt32(memory, ref offset, 0);
        WriteSequenceUInt32(memory, ref offset, (uint)handlerType);
        WriteSequenceZeros(memory, ref offset, 12);
        memory[offset++] = 0;
        EndSequenceBox(memory, handlerStart, offset);
    }

    private static void WriteSequenceDataInformation(Span<byte> memory, ref int offset)
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
        Span<byte> memory,
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
        WriteSequenceUInt32(memory, ref offset, (uint)track.Samples.Length);
        WriteSequenceUInt32(memory, ref offset, 1);
        EndSequenceBox(memory, sampleToChunkStart, offset);

        int sampleSizesStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Stsz);
        WriteSequenceFullBoxHeader(memory, ref offset, 0, 0);
        WriteSequenceUInt32(memory, ref offset, 0);
        WriteSequenceUInt32(memory, ref offset, (uint)track.Samples.Length);
        foreach (HeifSequenceSampleInfo sample in track.Samples)
        {
            WriteSequenceUInt32(memory, ref offset, (uint)sample.Length);
        }

        EndSequenceBox(memory, sampleSizesStart, offset);

        int chunkOffsetsStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Co64);
        WriteSequenceFullBoxHeader(memory, ref offset, 0, 0);
        WriteSequenceUInt32(memory, ref offset, 1);
        int chunkOffsetPosition = offset;
        WriteSequenceUInt64(memory, ref offset, 0);
        EndSequenceBox(memory, chunkOffsetsStart, offset);

        int syncSampleCount = GetSequenceSyncSampleCount(track.Samples);
        int syncSamplesStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Stss);
        WriteSequenceFullBoxHeader(memory, ref offset, 0, 0);
        WriteSequenceUInt32(memory, ref offset, (uint)syncSampleCount);
        uint sampleNumber = 1;
        foreach (HeifSequenceSampleInfo sample in track.Samples)
        {
            if (sample.IsSyncSample)
            {
                WriteSequenceUInt32(memory, ref offset, sampleNumber);
            }

            sampleNumber++;
        }

        EndSequenceBox(memory, syncSamplesStart, offset);
        EndSequenceBox(memory, sampleTableStart, offset);
        return chunkOffsetPosition;
    }

    private static void WriteSequenceSampleDescription(
        Span<byte> memory,
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
        WriteSequenceUInt16(memory, ref offset, (ushort)sequence.Width);
        WriteSequenceUInt16(memory, ref offset, (ushort)sequence.Height);
        WriteSequenceUInt32(memory, ref offset, DefaultVisualSampleResolution);
        WriteSequenceUInt32(memory, ref offset, DefaultVisualSampleResolution);
        WriteSequenceUInt32(memory, ref offset, 0);
        WriteSequenceUInt16(memory, ref offset, 1);
        WriteSequenceZeros(memory, ref offset, VisualSampleCompressorNameLength);
        WriteSequenceUInt16(memory, ref offset, VisualSampleDepth);
        WriteSequenceUInt16(memory, ref offset, ushort.MaxValue);

        int configurationStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Av1C);
        track.Configuration.WriteFixedHeader(memory.Slice(offset, Av1CodecConfiguration.FixedHeaderSize));
        offset += Av1CodecConfiguration.FixedHeaderSize;
        EndSequenceBox(memory, configurationStart, offset);

        if (track.IsAlpha)
        {
            int auxiliaryTypeStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Auxi);
            WriteSequenceFullBoxHeader(memory, ref offset, 0, 0);
            int auxiliaryTypeLength = Encoding.UTF8.GetByteCount(HeifConstants.AlphaAuxiliaryType);
            Span<byte> auxiliaryType = memory.Slice(offset, auxiliaryTypeLength + 1);
            offset += Encoding.UTF8.GetBytes(HeifConstants.AlphaAuxiliaryType, auxiliaryType);
            memory[offset++] = 0;
            EndSequenceBox(memory, auxiliaryTypeStart, offset);
        }
        else
        {
            if (!sequence.IccProfileData.IsEmpty)
            {
                offset += WriteIccColorInformationPropertyBox(memory, offset, sequence.IccProfileData);
            }

            offset += WriteColorInformationPropertyBox(memory, offset, sequence.ColorProfile);
        }

        int codingConstraintsStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Ccst);
        WriteSequenceFullBoxHeader(memory, ref offset, 0, 0);

        uint codingConstraints = IntraPicturePredictionUsedMask;
        if (GetSequenceSyncSampleCount(track.Samples) == track.Samples.Length)
        {
            codingConstraints |= AllReferencePicturesIntraMask;
        }

        // Sync samples are key frames in this encoder. The all-intra flag is therefore valid when every sample
        // is independently decodable, including lossless sequences that deliberately avoid inter transforms.
        WriteSequenceUInt32(memory, ref offset, codingConstraints);
        EndSequenceBox(memory, codingConstraintsStart, offset);
        EndSequenceBox(memory, sampleEntryStart, offset);
        EndSequenceBox(memory, descriptionStart, offset);
    }

    private static void WriteSequenceSampleTiming(
        Span<byte> memory,
        ref int offset,
        ReadOnlySpan<HeifSequenceSampleInfo> samples)
    {
        // The time-to-sample table stores runs, not one entry per frame. Preserve exact resolved durations while
        // combining only adjacent frames whose delays are equal.
        int runCount = GetSequenceTimingRunCount(samples);

        int timingStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Stts);
        WriteSequenceFullBoxHeader(memory, ref offset, 0, 0);
        WriteSequenceUInt32(memory, ref offset, (uint)runCount);
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

    private static int GetSequenceTimingRunCount(ReadOnlySpan<HeifSequenceSampleInfo> samples)
    {
        int runCount = 1;
        for (int sampleIndex = 1; sampleIndex < samples.Length; sampleIndex++)
        {
            runCount += samples[sampleIndex].Duration == samples[sampleIndex - 1].Duration ? 0 : 1;
        }

        return runCount;
    }

    private static int GetSequenceSyncSampleCount(ReadOnlySpan<HeifSequenceSampleInfo> samples)
    {
        int count = 0;
        foreach (HeifSequenceSampleInfo sample in samples)
        {
            if (sample.IsSyncSample)
            {
                count++;
            }
        }

        return count;
    }

    private static uint GetSequenceSampleDuration(Rational delay, uint timescale)
    {
        // HEIF metadata uses either a zero numerator or a zero denominator for an unspecified duration.
        // BMFF samples still require a finite positive duration, so encode the smallest representable value.
        if (delay.Numerator == 0 || delay.Denominator == 0)
        {
            return 1;
        }

        ulong scaledDuration = ((ulong)delay.Numerator * timescale) + (delay.Denominator / 2U);
        return checked((uint)Math.Max(1UL, scaledDuration / delay.Denominator));
    }

    private static uint GetSequenceTimescale<TPixel>(Image<TPixel> image, int firstFrameIndex)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        uint timescale = DefaultSequenceTimescale;
        for (int frameIndex = firstFrameIndex; frameIndex < image.Frames.Count; frameIndex++)
        {
            ImageFrame<TPixel> frame = image.Frames[frameIndex];
            Rational delay = frame.Metadata.GetHeifMetadata().FrameDelay;
            if (delay.Numerator == 0 || delay.Denominator == 0)
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
        Span<byte> memory,
        ref int offset,
        Heif4CharCode type)
    {
        // Reserve the size field now and patch it at the matching EndSequenceBox call after nested boxes expand.
        int start = offset;
        offset += WriteBoxHeader(memory[offset..], type);
        return start;
    }

    private static void EndSequenceBox(Span<byte> memory, int start, int offset)
        => BinaryPrimitives.WriteUInt32BigEndian(
            memory.Slice(start, sizeof(uint)),
            (uint)(offset - start));

    private static void WriteSequenceFullBoxHeader(
        Span<byte> memory,
        ref int offset,
        byte version,
        uint flags)
    {
        Span<byte> destination = memory.Slice(offset, sizeof(uint));
        BinaryPrimitives.WriteUInt32BigEndian(destination, flags);
        destination[0] = version;
        offset += sizeof(uint);
    }

    private static void WriteSequenceIdentityMatrix(Span<byte> memory, ref int offset)
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

    private static void WriteSequenceZeros(Span<byte> memory, ref int offset, int length)
    {
        memory.Slice(offset, length).Clear();
        offset += length;
    }

    private static void WriteSequenceBytes(
        Span<byte> memory,
        ref int offset,
        ReadOnlySpan<byte> source)
    {
        source.CopyTo(memory[offset..]);
        offset += source.Length;
    }

    private static void WriteSequenceUInt16(Span<byte> memory, ref int offset, ushort value)
    {
        BinaryPrimitives.WriteUInt16BigEndian(memory[offset..], value);
        offset += sizeof(ushort);
    }

    private static void WriteSequenceUInt32(Span<byte> memory, ref int offset, uint value)
    {
        BinaryPrimitives.WriteUInt32BigEndian(memory[offset..], value);
        offset += sizeof(uint);
    }

    private static void WriteSequenceUInt64(Span<byte> memory, ref int offset, ulong value)
    {
        BinaryPrimitives.WriteUInt64BigEndian(memory[offset..], value);
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
        public HeifSequenceSampleInfo(long offset, int length, uint duration, bool isSyncSample)
        {
            this.Offset = offset;
            this.Length = length;
            this.Duration = duration;
            this.IsSyncSample = isSyncSample;
        }

        public long Offset { get; }

        public int Length { get; }

        public uint Duration { get; }

        public bool IsSyncSample { get; }
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
            ReadOnlyMemory<byte> iccProfileData,
            byte[]? exifData,
            uint exifTiffHeaderOffset,
            byte[]? xmpData)
        {
            this.Width = width;
            this.Height = height;
            this.RepeatCount = repeatCount;
            this.Timescale = timescale;
            this.ColorTrack = colorTrack;
            this.AlphaTrack = alphaTrack;
            this.ColorProfile = colorProfile;
            this.IccProfileData = iccProfileData;
            this.ExifData = exifData;
            this.ExifTiffHeaderOffset = exifTiffHeaderOffset;
            this.XmpData = xmpData;
        }

        public int Width { get; }

        public int Height { get; }

        public ushort RepeatCount { get; }

        public uint Timescale { get; }

        public HeifSequenceTrackEncoding ColorTrack { get; }

        public HeifSequenceTrackEncoding? AlphaTrack { get; }

        public CicpProfile ColorProfile { get; }

        public ReadOnlyMemory<byte> IccProfileData { get; }

        public byte[]? ExifData { get; }

        public uint ExifTiffHeaderOffset { get; }

        public byte[]? XmpData { get; }
    }

    private readonly struct HeifSequenceTrackEncoding
    {
        private readonly ReadOnlyMemory<HeifSequenceSampleInfo> samples;

        public HeifSequenceTrackEncoding(
            Av1CodecConfiguration configuration,
            ReadOnlyMemory<HeifSequenceSampleInfo> samples,
            bool isAlpha)
        {
            this.Configuration = configuration;
            this.samples = samples;
            this.IsAlpha = isAlpha;
        }

        public Av1CodecConfiguration Configuration { get; }

        public ReadOnlySpan<HeifSequenceSampleInfo> Samples
            => this.samples.Span;

        public bool IsAlpha { get; }
    }
}
