// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Formats.Webp.BitReader;
using SixLabors.ImageSharp.Formats.Webp.Lossy;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.Metadata.Profiles.Xmp;

namespace SixLabors.ImageSharp.Formats.Webp;

internal static class WebpChunkParsingUtils
{
    /// <summary>
    /// Reads the header of a lossy webp image.
    /// </summary>
    /// <param name="memoryAllocator">The memory allocator.</param>
    /// <param name="stream">The buffered read stream.</param>
    /// <param name="buffer">The scratch buffer to use while reading.</param>
    /// <param name="features">The webp features to parse.</param>
    /// <param name="containerEnd">The end of the containing RIFF or ANMF payload.</param>
    /// <param name="allowTruncatedPayload">Whether image-data recovery permits an incomplete compressed payload at EOF.</param>
    /// <returns>Information about this webp image.</returns>
    public static WebpImageInfo ReadVp8Header(
        MemoryAllocator memoryAllocator,
        BufferedReadStream stream,
        Span<byte> buffer,
        WebpFeatures features,
        long containerEnd,
        bool allowTruncatedPayload)
    {
        uint dataSize = ReadChunkSize(stream, buffer, containerEnd, WebpConstants.Vp8FrameHeaderSize, allowTruncatedPayload);
        long chunkEnd = Math.Min(stream.Position + dataSize + (dataSize & 1), stream.Length);
        int bytesRead;

        // Remaining counts the available image data payload.
        uint remaining = dataSize;

        // Paragraph 9.1 https://tools.ietf.org/html/rfc6386#page-30
        // Frame tag that contains four fields:
        // - A 1-bit frame type (0 for key frames, 1 for interframes).
        // - A 3-bit version number.
        // - A 1-bit show_frame flag.
        // - A 19-bit field containing the size of the first data partition in bytes.
        bytesRead = stream.Read(buffer, 0, 3);
        if (bytesRead != 3)
        {
            WebpThrowHelper.ThrowInvalidImageContentException("Not enough data to read the VP8 header");
        }

        uint frameTag = (uint)(buffer[0] | (buffer[1] << 8) | (buffer[2] << 16));
        remaining -= 3;
        bool isNoKeyFrame = (frameTag & 0x1) == 1;
        if (isNoKeyFrame)
        {
            WebpThrowHelper.ThrowImageFormatException("VP8 header indicates the image is not a key frame");
        }

        uint version = (frameTag >> 1) & 0x7;
        if (version > 3)
        {
            WebpThrowHelper.ThrowImageFormatException($"VP8 header indicates unknown profile {version}");
        }

        bool invisibleFrame = ((frameTag >> 4) & 0x1) == 0;
        if (invisibleFrame)
        {
            WebpThrowHelper.ThrowImageFormatException("VP8 header indicates that the first frame is invisible");
        }

        uint partitionLength = frameTag >> 5;
        if (partitionLength > dataSize)
        {
            WebpThrowHelper.ThrowImageFormatException("VP8 header contains inconsistent size information");
        }

        // Check for VP8 magic bytes.
        bytesRead = stream.Read(buffer, 0, 3);
        if (bytesRead != 3)
        {
            WebpThrowHelper.ThrowInvalidImageContentException("Not enough data to read the VP8 magic bytes");
        }

        if (!buffer[..3].SequenceEqual(WebpConstants.Vp8HeaderMagicBytes))
        {
            WebpThrowHelper.ThrowImageFormatException("VP8 magic bytes not found");
        }

        bytesRead = stream.Read(buffer, 0, 4);
        if (bytesRead != 4)
        {
            WebpThrowHelper.ThrowInvalidImageContentException("Not enough data to read the VP8 header, could not read width and height");
        }

        uint tmp = BinaryPrimitives.ReadUInt16LittleEndian(buffer);
        uint width = tmp & 0x3fff;
        sbyte xScale = (sbyte)(tmp >> 6);
        tmp = BinaryPrimitives.ReadUInt16LittleEndian(buffer[2..]);
        uint height = tmp & 0x3fff;
        sbyte yScale = (sbyte)(tmp >> 6);
        remaining -= 7;
        if (width == 0 || height == 0)
        {
            WebpThrowHelper.ThrowImageFormatException("width or height can not be zero");
        }

        if (partitionLength > remaining)
        {
            WebpThrowHelper.ThrowImageFormatException("bad partition length");
        }

        Vp8FrameHeader vp8FrameHeader = new()
        {
            KeyFrame = true,
            Profile = (sbyte)version,
            PartitionLength = partitionLength
        };

        Vp8BitReader bitReader = new(stream, remaining, memoryAllocator, partitionLength) { Remaining = remaining };

        stream.Position = chunkEnd;

        return new WebpImageInfo
        {
            DataSize = dataSize,
            Width = width,
            Height = height,
            XScale = xScale,
            YScale = yScale,

            // Vp8 header can be parsed during the processing of the Vp8X header.
            BitsPerPixel = features?.Alpha == true ? WebpBitsPerPixel.Bit32 : WebpBitsPerPixel.Bit24,
            IsLossless = false,
            Features = features,
            Vp8Profile = (sbyte)version,
            Vp8FrameHeader = vp8FrameHeader,
            Vp8BitReader = bitReader
        };
    }

    /// <summary>
    /// Reads the header of a lossless webp image.
    /// </summary>
    /// <param name="memoryAllocator">The memory allocator.</param>
    /// <param name="stream">The buffered read stream.</param>
    /// <param name="buffer">The scratch buffer to use while reading.</param>
    /// <param name="features">The webp features to parse.</param>
    /// <param name="containerEnd">The end of the containing RIFF or ANMF payload.</param>
    /// <param name="allowTruncatedPayload">Whether image-data recovery permits an incomplete compressed payload at EOF.</param>
    public static WebpImageInfo ReadVp8LHeader(
        MemoryAllocator memoryAllocator,
        BufferedReadStream stream,
        Span<byte> buffer,
        WebpFeatures features,
        long containerEnd,
        bool allowTruncatedPayload)
    {
        // The five-byte VP8L header must fit inside its own chunk, not in following chunks or trailing data.
        uint imageDataSize = ReadChunkSize(stream, buffer, containerEnd, 5, allowTruncatedPayload);
        long chunkEnd = Math.Min(stream.Position + imageDataSize + (imageDataSize & 1), stream.Length);
        Vp8LBitReader bitReader = new(stream, imageDataSize, memoryAllocator);
        try
        {
            // One byte signature, should be 0x2f.
            uint signature = bitReader.ReadValue(8);
            if (signature != WebpConstants.Vp8LHeaderMagicByte)
            {
                WebpThrowHelper.ThrowImageFormatException("Invalid VP8L signature");
            }

            // The first 28 bits of the bitstream specify the width and height of the image.
            uint width = bitReader.ReadValue(WebpConstants.Vp8LImageSizeBits) + 1;
            uint height = bitReader.ReadValue(WebpConstants.Vp8LImageSizeBits) + 1;
            if (width == 0 || height == 0)
            {
                WebpThrowHelper.ThrowImageFormatException("invalid width or height read");
            }

            // The alphaIsUsed flag should be set to 0 when all alpha values are 255 in the picture, and 1 otherwise.
            // Alpha may have already been set by the VP8X chunk.
            features.Alpha |= bitReader.ReadBit();

            // The next 3 bits are the version. The version number is a 3 bit code that must be set to 0.
            // Any other value should be treated as an error.
            uint version = bitReader.ReadValue(WebpConstants.Vp8LVersionBits);
            if (version != 0)
            {
                WebpThrowHelper.ThrowNotSupportedException($"Unexpected version number {version} found in VP8L header");
            }

            stream.Position = chunkEnd;

            return new WebpImageInfo
            {
                DataSize = imageDataSize,
                Width = width,
                Height = height,
                BitsPerPixel = features.Alpha ? WebpBitsPerPixel.Bit32 : WebpBitsPerPixel.Bit24,
                IsLossless = true,
                Features = features,
                Vp8LBitReader = bitReader
            };
        }
        catch
        {
            bitReader.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Reads an the extended webp file header. An extended file header consists of:
    /// - A 'VP8X' chunk with information about features used in the file.
    /// - An optional 'ICCP' chunk with color profile.
    /// - An optional 'XMP' chunk with metadata.
    /// - An optional 'ANIM' chunk with animation control data.
    /// - An optional 'ALPH' chunk with alpha channel data.
    /// After the image header, image data will follow. After that optional image metadata chunks (EXIF and XMP) can follow.
    /// </summary>
    /// <param name="stream">The buffered read stream.</param>
    /// <param name="buffer">The scratch buffer to use while reading.</param>
    /// <param name="features">The webp features to parse.</param>
    /// <param name="containerEnd">The end of the containing RIFF or ANMF payload.</param>
    /// <returns>Information about this webp image.</returns>
    public static WebpImageInfo ReadVp8XHeader(BufferedReadStream stream, Span<byte> buffer, WebpFeatures features, long containerEnd)
    {
        uint size = ReadChunkSize(stream, buffer, containerEnd, 10, false);
        if (size != 10)
        {
            WebpThrowHelper.ThrowInvalidImageContentException("The VP8X payload must contain exactly 10 bytes.");
        }

        // The first byte contains information about the image features used.
        // Its two most significant bits and its least significant bit are reserved. Writers set them to zero,
        // but the container specification requires readers to ignore them, so they are not validated.
        byte imageFeatures = (byte)stream.ReadByte();

        // If bit 3 is set, a ICC Profile Chunk should be present.
        features.IccProfile = (imageFeatures & (1 << 5)) != 0;

        // If bit 4 is set, any of the frames of the image contain transparency information ("alpha" chunk).
        features.Alpha = (imageFeatures & (1 << 4)) != 0;

        // If bit 5 is set, a EXIF metadata should be present.
        features.ExifProfile = (imageFeatures & (1 << 3)) != 0;

        // If bit 6 is set, XMP metadata should be present.
        features.XmpMetaData = (imageFeatures & (1 << 2)) != 0;

        // If bit 7 is set, animation should be present.
        features.Animation = (imageFeatures & (1 << 1)) != 0;

        // Readers ignore these three reserved bytes, as they ignore reserved feature bits.
        stream.Read(buffer, 0, 3);

        // 3 bytes for the width.
        uint width = ReadUInt24LittleEndian(stream, buffer) + 1;

        // 3 bytes for the height.
        uint height = ReadUInt24LittleEndian(stream, buffer) + 1;

        // Read all the chunks in the order they occur.
        return new WebpImageInfo
        {
            Width = width,
            Height = height,
            Features = features

            // Additional properties are set during the parsing of the VP8 or VP8L headers.
        };
    }

    /// <summary>
    /// Reads a unsigned 24 bit integer.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="buffer">The buffer to store the read data into.</param>
    /// <returns>A unsigned 24 bit integer.</returns>
    /// <exception cref="InvalidImageContentException">
    /// Thrown if the stream ends before the integer.
    /// </exception>
    public static uint ReadUInt24LittleEndian(BufferedReadStream stream, Span<byte> buffer)
    {
        if (stream.Read(buffer, 0, 3) == 3)
        {
            buffer[3] = 0;
            return BinaryPrimitives.ReadUInt32LittleEndian(buffer);
        }

        throw new InvalidImageContentException("Invalid Webp data, could not read unsigned 24 bit integer.");
    }

    /// <summary>
    /// Writes a unsigned 24 bit integer.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="data">The uint24 data to write.</param>
    /// <exception cref="InvalidDataException">
    /// Thrown if the data is not a valid unsigned 24 bit integer.
    /// </exception>
    public static unsafe void WriteUInt24LittleEndian(Stream stream, uint data)
    {
        if (data >= 1 << 24)
        {
            throw new InvalidDataException($"Invalid data, {data} is not a unsigned 24 bit integer.");
        }

        uint* ptr = &data;
        byte* b = (byte*)ptr;

        // Write the data in little endian.
        stream.WriteByte(b[0]);
        stream.WriteByte(b[1]);
        stream.WriteByte(b[2]);
    }

    /// <summary>
    /// Reads a required chunk size and validates its complete extent before any payload allocation or read.
    /// </summary>
    /// <param name="stream">The stream positioned at the size field.</param>
    /// <param name="buffer">The four-byte size buffer.</param>
    /// <param name="containerEnd">The end of the containing RIFF or ANMF payload.</param>
    /// <param name="minimumSize">The minimum payload size required by the chunk header.</param>
    /// <param name="allowTruncatedPayload">Whether to retain the bit reader's EOF recovery for compressed image data.</param>
    /// <returns>The payload size, excluding its RIFF padding byte.</returns>
    public static uint ReadChunkSize(BufferedReadStream stream, Span<byte> buffer, long containerEnd, uint minimumSize, bool allowTruncatedPayload)
    {
        ulong size = ReadChunkPayloadSize(stream, buffer, true);

        // Widen before adding padding. The parent end also excludes bytes outside RIFF and adjacent ANMF frames.
        ulong paddedSize = size + (size & 1);
        if (size < minimumSize || paddedSize > (ulong)(containerEnd - stream.Position) || stream.RemainingBytes < minimumSize)
        {
            WebpThrowHelper.ThrowInvalidImageContentException("WebP chunk extends beyond its container or has an incomplete header.");
        }

        // Only compressed image data has EOF recovery. Required headers and parent boundaries remain fatal.
        // Bit readers retain their existing zero-filled tail when IgnoreImageData permits that recovery.
        if (!allowTruncatedPayload && paddedSize > (ulong)stream.RemainingBytes)
        {
            WebpThrowHelper.ThrowInvalidImageContentException("Not enough data to read the WebP chunk payload.");
        }

        return (uint)size;
    }

    /// <summary>
    /// Skips an unknown chunk without reading beyond its containing RIFF payload.
    /// </summary>
    /// <param name="stream">The stream positioned at the size field.</param>
    /// <param name="buffer">The four-byte size buffer.</param>
    /// <param name="containerEnd">The end of the containing RIFF payload.</param>
    public static void SkipChunk(BufferedReadStream stream, Span<byte> buffer, long containerEnd)
    {
        ulong size = ReadChunkPayloadSize(stream, buffer, true);
        stream.Position += (long)Math.Min(size + (size & 1), (ulong)(Math.Min(containerEnd, stream.Length) - stream.Position));
    }

    /// <summary>
    /// Reads the payload size stored in a chunk header. RIFF follows an odd-sized payload with a padding byte
    /// that belongs to the chunk but not to its payload, so the returned size excludes it.
    /// </summary>
    /// <param name="stream">The input stream.</param>
    /// <param name="buffer">The four-byte size buffer.</param>
    /// <param name="required">Whether an incomplete size field is an error.</param>
    /// <returns>The payload size, or remaining bytes when an optional size field is incomplete.</returns>
    private static ulong ReadChunkPayloadSize(BufferedReadStream stream, Span<byte> buffer, bool required)
    {
        if (stream.Read(buffer) is 4)
        {
            return BinaryPrimitives.ReadUInt32LittleEndian(buffer);
        }

        if (required)
        {
            // Only a truncated stream leaves fewer than four bytes for the size field.
            throw new InvalidImageContentException("Invalid Webp data, could not read chunk size.");
        }

        // Return the size of the remaining data in the stream.
        return (ulong)stream.RemainingBytes;
    }

    /// <summary>
    /// Identifies the chunk type from the chunk.
    /// </summary>
    /// <param name="stream">The stream to read the data from.</param>
    /// <param name="buffer">Buffer to store the data read from the stream.</param>
    /// <exception cref="InvalidImageContentException">
    /// Thrown if the stream ends inside the chunk type.
    /// </exception>
    public static WebpChunkType ReadChunkType(BufferedReadStream stream, Span<byte> buffer)
    {
        if (stream.Read(buffer) == 4)
        {
            return (WebpChunkType)BinaryPrimitives.ReadUInt32BigEndian(buffer);
        }

        // While we ignore unknown chunks we still need a to be a ble to read a chunk type
        // known or otherwise from the stream. Only a truncated stream leaves fewer than four bytes.
        throw new InvalidImageContentException("Invalid Webp data, could not read chunk type.");
    }

    /// <summary>
    /// Reads the ICCP chunk from the stream.
    /// </summary>
    /// <param name="stream">The stream to decode from.</param>
    /// <param name="metadata">The image metadata.</param>
    /// <param name="ignoreMetadata">If true, metadata will be ignored.</param>
    /// <param name="executeAncillarySegmentAction">Executes profile parsing under the decoder's integrity policy.</param>
    /// <param name="containerEnd">The end of the RIFF payload.</param>
    public static void ReadIccProfile(
        BufferedReadStream stream,
        ImageMetadata metadata,
        bool ignoreMetadata,
        Action<Action> executeAncillarySegmentAction,
        long containerEnd)
    {
        ulong payloadSize = ReadChunkPayloadSize(stream, stackalloc byte[4], true);

        // ICCP precedes image/frame data. Its framing, including the padding byte of an odd-sized payload,
        // must be readable even when metadata is skipped; otherwise there is no safe location to resume decoding.
        if (payloadSize + (payloadSize & 1) > (ulong)(Math.Min(containerEnd, stream.Length) - stream.Position))
        {
            WebpThrowHelper.ThrowInvalidImageContentException("Not enough data to read the ICCP chunk.");
        }

        executeAncillarySegmentAction(() =>
        {
            byte[]? iccpData = ReadMetadataChunk(stream, payloadSize, ignoreMetadata || metadata.IccProfile != null, containerEnd);
            if (iccpData is not null)
            {
                IccProfile profile = new(iccpData);
                if (!profile.CheckIsValid())
                {
                    throw new InvalidIccProfileException("Invalid ICC profile.");
                }

                metadata.IccProfile = profile;
            }
        });
    }

    /// <summary>
    /// Reads the EXIF profile from the stream.
    /// </summary>
    /// <param name="stream">The stream to decode from.</param>
    /// <param name="metadata">The image metadata.</param>
    /// <param name="ignoreMetadata">If true, metadata will be ignored.</param>
    /// <param name="containerEnd">The end of the RIFF payload.</param>
    public static void ReadExifProfile(
        BufferedReadStream stream,
        ImageMetadata metadata,
        bool ignoreMetadata,
        long containerEnd)
    {
        ulong payloadSize = ReadChunkPayloadSize(stream, stackalloc byte[4], !ignoreMetadata);
        byte[]? exifData = ReadMetadataChunk(stream, payloadSize, ignoreMetadata || metadata.ExifProfile != null, containerEnd);
        if (exifData is not null)
        {
            ExifProfile exifProfile = new(exifData);

            // Set the resolution from the metadata.
            double horizontalValue = GetExifResolutionValue(exifProfile, ExifTag.XResolution);
            double verticalValue = GetExifResolutionValue(exifProfile, ExifTag.YResolution);

            if (horizontalValue > 0 && verticalValue > 0)
            {
                metadata.HorizontalResolution = horizontalValue;
                metadata.VerticalResolution = verticalValue;
                metadata.ResolutionUnits = UnitConverter.ExifProfileToResolutionUnit(exifProfile);
            }

            metadata.ExifProfile = exifProfile;
        }
    }

    /// <summary>
    /// Reads the XMP profile the stream.
    /// </summary>
    /// <param name="stream">The stream to decode from.</param>
    /// <param name="metadata">The image metadata.</param>
    /// <param name="ignoreMetadata">If true, metadata will be ignored.</param>
    /// <param name="containerEnd">The end of the RIFF payload.</param>
    public static void ReadXmpProfile(
        BufferedReadStream stream,
        ImageMetadata metadata,
        bool ignoreMetadata,
        long containerEnd)
    {
        ulong payloadSize = ReadChunkPayloadSize(stream, stackalloc byte[4], !ignoreMetadata);
        byte[]? xmpData = ReadMetadataChunk(stream, payloadSize, ignoreMetadata || metadata.XmpProfile != null, containerEnd);
        if (xmpData is not null)
        {
            metadata.XmpProfile = new XmpProfile(xmpData);
        }
    }

    /// <summary>
    /// Reads a metadata payload, leaving the stream at the next chunk or EOF on a recoverable error.
    /// Callers execute metadata parsing under the decoder's ancillary integrity policy.
    /// </summary>
    /// <param name="stream">The input stream positioned at the chunk payload.</param>
    /// <param name="payloadLength">The declared payload length, excluding its padding byte.</param>
    /// <param name="ignoreMetadata">Whether to skip the payload without parsing it.</param>
    /// <param name="containerEnd">The end of the RIFF payload.</param>
    /// <returns>The payload, or null when metadata is skipped.</returns>
    private static byte[]? ReadMetadataChunk(BufferedReadStream stream, ulong payloadLength, bool ignoreMetadata, long containerEnd)
    {
        // An odd-sized payload is followed by a padding byte that is part of the chunk but not of the payload.
        // The next chunk starts after it, clamped to the end of a truncated stream.
        ulong paddedLength = payloadLength + (payloadLength & 1);
        long chunkEnd = stream.Position + (long)Math.Min(paddedLength, (ulong)(Math.Min(containerEnd, stream.Length) - stream.Position));
        if (ignoreMetadata)
        {
            stream.Position = chunkEnd;
            return null;
        }

        if (paddedLength > (ulong)(Math.Min(containerEnd, stream.Length) - stream.Position) || !stream.TryGetReadLength(payloadLength, out _))
        {
            // Ignoring an ancillary error must not make the next parser interpret
            // this payload as another chunk header. A truncated chunk consumes EOF.
            stream.Position = chunkEnd;
            WebpThrowHelper.ThrowInvalidImageContentException("Not enough data to read the metadata chunk.");
        }

        int bufferLength = (int)payloadLength;
        byte[] data = new byte[bufferLength];
        if (stream.Read(data) != bufferLength)
        {
            WebpThrowHelper.ThrowInvalidImageContentException("Not enough data to read the metadata chunk.");
        }

        // Skip the padding byte, so it is neither returned as payload nor read as the next chunk header.
        stream.Position = chunkEnd;
        return data;
    }

    private static double GetExifResolutionValue(ExifProfile exifProfile, ExifTag<Rational> tag)
    {
        if (exifProfile.TryGetValue(tag, out IExifValue<Rational>? resolution))
        {
            return resolution.Value.ToDouble();
        }

        return 0;
    }

    /// <summary>
    /// Determines if the chunk type is an optional VP8X chunk.
    /// </summary>
    /// <param name="chunkType">The chunk type.</param>
    /// <returns>True, if its an optional chunk type.</returns>
    public static bool IsOptionalVp8XChunk(WebpChunkType chunkType) => chunkType switch
    {
        WebpChunkType.Alpha => true,
        WebpChunkType.AnimationParameter => true,
        WebpChunkType.Exif => true,
        WebpChunkType.Iccp => true,
        WebpChunkType.Xmp => true,
        _ => false
    };
}
