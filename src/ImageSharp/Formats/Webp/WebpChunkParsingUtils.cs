// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
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
    /// Reads the header of a VP8 or a VP8L chunk. The stream is at the size field of the chunk.
    /// </summary>
    /// <remarks>
    /// Before the image exists, a damaged header is a structural error, and the method throws in each mode. In a later animation frame, the image exists, so
    /// the integrity mode decides. When the mode ignores the error, the method returns <see langword="false"/>, and the caller keeps the frames that it
    /// decoded.
    /// </remarks>
    /// <param name="chunkType">The chunk type, <see cref="WebpChunkType.Vp8"/> or <see cref="WebpChunkType.Vp8L"/>.</param>
    /// <param name="memoryAllocator">The memory allocator.</param>
    /// <param name="stream">The buffered read stream.</param>
    /// <param name="buffer">The scratch buffer for the reads.</param>
    /// <param name="features">The webp features to parse.</param>
    /// <param name="containerEnd">The end of the RIFF or ANMF payload that contains the chunk.</param>
    /// <param name="segmentIntegrityHandling">The image data integrity mode. It controls damaged or truncated data.</param>
    /// <param name="imageExists">A value that tells if the decoder created the image before this header.</param>
    /// <param name="info">Information about this webp image, or <see langword="null"/> when the header is damaged.</param>
    /// <returns><see langword="true"/> if the header is valid. Otherwise <see langword="false"/>.</returns>
    public static bool TryReadImageChunk(
        WebpChunkType chunkType,
        MemoryAllocator memoryAllocator,
        BufferedReadStream stream,
        Span<byte> buffer,
        WebpFeatures features,
        long containerEnd,
        SegmentIntegrityHandling segmentIntegrityHandling,
        bool imageExists,
        [NotNullWhen(true)] out WebpImageInfo? info)
        => chunkType == WebpChunkType.Vp8
            ? TryReadVp8Header(memoryAllocator, stream, buffer, features, containerEnd, segmentIntegrityHandling, imageExists, out info)
            : TryReadVp8LHeader(memoryAllocator, stream, buffer, features, containerEnd, segmentIntegrityHandling, imageExists, out info);

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
        // The VP8X chunk is not image data, and the image does not exist yet. Thus, the method throws in each mode for a chunk that does not fit, and the call
        // returns true here.
        _ = TryReadChunkSize(stream, buffer, containerEnd, 10, SegmentIntegrityHandling.Strict, false, out uint size);
        if (size != 10)
        {
            WebpThrowHelper.ThrowInvalidImageContentException("The VP8X payload must contain exactly 10 bytes.");
        }

        // The first byte contains information about the image features used.
        // Its two most significant bits and its least significant bit are reserved. Writers set them to zero,
        // but the container specification requires readers to ignore them, so they are not validated.
        byte imageFeatures = (byte)stream.ReadByte();

        // If bit 5 is set, an ICC Profile Chunk should be present.
        features.IccProfile = (imageFeatures & (1 << 5)) != 0;

        // If bit 4 is set, any of the frames of the image contain transparency information ("alpha" chunk).
        features.Alpha = (imageFeatures & (1 << 4)) != 0;

        // If bit 3 is set, EXIF metadata should be present.
        features.ExifProfile = (imageFeatures & (1 << 3)) != 0;

        // If bit 2 is set, XMP metadata should be present.
        features.XmpMetaData = (imageFeatures & (1 << 2)) != 0;

        // If bit 1 is set, animation should be present.
        features.Animation = (imageFeatures & (1 << 1)) != 0;

        // Readers ignore these three reserved bytes, as they ignore reserved feature bits.
        stream.Read(buffer, 0, 3);

        // 3 bytes for the width.
        uint width = ReadUInt24LittleEndian(stream, buffer) + 1;

        // 3 bytes for the height.
        uint height = ReadUInt24LittleEndian(stream, buffer) + 1;

        // libwebp rejects a canvas area of 2^32 or more.
        if ((ulong)width * height >= 1UL << 32)
        {
            WebpThrowHelper.ThrowInvalidImageContentException("The VP8X canvas is too large.");
        }

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
    /// Reads an unsigned 24-bit integer.
    /// </summary>
    /// <remarks>
    /// The callers check the chunk size before the read, so the stream holds the three bytes.
    /// </remarks>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="buffer">The buffer for the data.</param>
    /// <returns>An unsigned 24-bit integer.</returns>
    public static uint ReadUInt24LittleEndian(BufferedReadStream stream, Span<byte> buffer)
    {
        stream.Read(buffer, 0, 3);
        buffer[3] = 0;
        return BinaryPrimitives.ReadUInt32LittleEndian(buffer);
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
    /// Reads a required chunk size. The method checks that the complete chunk fits in its container before it allocates or reads the payload.
    /// </summary>
    /// <remarks>
    /// A chunk that does not fit in its container, or that is too small for its header, is a structural error before the image exists. In a later animation
    /// frame, the image exists, so the integrity mode decides, as for the other damage in the frame.
    /// </remarks>
    /// <param name="stream">The stream positioned at the size field.</param>
    /// <param name="buffer">The four-byte size buffer.</param>
    /// <param name="containerEnd">The end of the RIFF or ANMF payload that contains the chunk.</param>
    /// <param name="minimumSize">The minimum payload size that the chunk header requires.</param>
    /// <param name="segmentIntegrityHandling">
    /// The image data integrity mode. It controls a damaged frame and a payload that ends at EOF. Pass <see cref="SegmentIntegrityHandling.Strict"/> for a
    /// chunk that does not hold compressed image data.
    /// </param>
    /// <param name="imageExists">A value that tells if the decoder created the image before this chunk.</param>
    /// <param name="size">The payload size, without its RIFF padding byte.</param>
    /// <returns><see langword="true"/> if the chunk fits in its container. Otherwise <see langword="false"/>.</returns>
    public static bool TryReadChunkSize(
        BufferedReadStream stream,
        Span<byte> buffer,
        long containerEnd,
        uint minimumSize,
        SegmentIntegrityHandling segmentIntegrityHandling,
        bool imageExists,
        out uint size)
    {
        size = ReadChunkPayloadSize(stream, buffer);

        // Widen before adding padding. The parent end also excludes bytes outside RIFF and adjacent ANMF frames.
        ulong paddedSize = (ulong)size + (size & 1);
        if (size < minimumSize || paddedSize > (ulong)(containerEnd - stream.Position) || stream.RemainingBytes < minimumSize)
        {
            segmentIntegrityHandling.ThrowOrIgnoreImageDataError(imageExists, "WebP chunk extends beyond its container or has an incomplete header.");
            return false;
        }

        // Only compressed image data has EOF recovery. Required headers and parent boundaries remain fatal.
        // A payload that ends at EOF is truncated image data. The integrity mode decides if it throws.
        if (paddedSize > (ulong)stream.RemainingBytes)
        {
            segmentIntegrityHandling.ThrowOrIgnoreImageDataError("Not enough data to read the WebP chunk payload.");
        }

        return true;
    }

    /// <summary>
    /// Reads an ALPH chunk: the alpha header byte and the alpha data. The stream is at the size field of the chunk.
    /// </summary>
    /// <remarks>
    /// Alpha data is image data. The color data does not need it. Thus the image data integrity policy applies to a header byte that is not valid, and to data
    /// that ends at EOF. This is also true before the image exists. When the policy ignores a header byte that is not valid, the returned data is empty. Thus,
    /// the alpha decoder sets each alpha value to 0, the transparent fill of damaged data.
    /// </remarks>
    /// <param name="stream">The input stream.</param>
    /// <param name="buffer">The four-byte size buffer.</param>
    /// <param name="memoryAllocator">The memory allocator.</param>
    /// <param name="containerEnd">The end of the RIFF or ANMF payload that contains the chunk.</param>
    /// <param name="segmentIntegrityHandling">The image data integrity mode.</param>
    /// <param name="imageExists">A value that tells if the decoder created the image before this chunk.</param>
    /// <param name="alphaData">The alpha data, or <see langword="null"/> when the chunk does not fit. The caller owns it.</param>
    /// <param name="alphaChunkHeader">The alpha header byte. It has only defined values.</param>
    /// <returns><see langword="true"/> if the chunk fits in its container. Otherwise <see langword="false"/>.</returns>
    public static bool TryReadAlphaChunk(
        BufferedReadStream stream,
        Span<byte> buffer,
        MemoryAllocator memoryAllocator,
        long containerEnd,
        SegmentIntegrityHandling segmentIntegrityHandling,
        bool imageExists,
        [NotNullWhen(true)] out IMemoryOwner<byte>? alphaData,
        out byte alphaChunkHeader)
    {
        alphaData = null;
        alphaChunkHeader = 0;
        if (!TryReadChunkSize(stream, buffer, containerEnd, 1, segmentIntegrityHandling, imageExists, out uint alphaChunkSize))
        {
            return false;
        }

        // The IgnoreImageData mode accepts a chunk that ends at EOF. In that case, the chunk end is the end of the stream.
        long chunkEnd = Math.Min(stream.Position + alphaChunkSize + (alphaChunkSize & 1), stream.Length);

        // Header bits 0 and 1 are the compression method. Bits 2 and 3 are the filter, bits 4 and 5 are the pre-processing, and bits 6 and 7 are reserved. Only
        // compression 0 and 1 and pre-processing 0 and 1 are defined, and the reserved bits are 0. Each filter value is defined. See the WebP Container
        // Specification, section "Alpha".
        int header = stream.ReadByte();
        if ((header & 0x03) > 1 || ((header >> 4) & 0x03) > 1 || (header >> 6) != 0)
        {
            segmentIntegrityHandling.ThrowOrIgnoreImageDataError("The ALPH chunk header has a value that is not defined.");
            alphaData = memoryAllocator.Allocate<byte>(0);
        }
        else
        {
            alphaData = ReadAlphaData(stream, memoryAllocator, (int)(alphaChunkSize - 1), segmentIntegrityHandling);
            alphaChunkHeader = (byte)header;
        }

        stream.Position = chunkEnd;
        return true;
    }

    /// <summary>
    /// Skips a chunk that the decoder does not know. The method does not read past the end of the RIFF payload that contains the chunk.
    /// </summary>
    /// <param name="stream">The stream positioned at the size field.</param>
    /// <param name="buffer">The four-byte size buffer.</param>
    /// <param name="containerEnd">The end of the RIFF payload that contains the chunk.</param>
    public static void SkipChunk(BufferedReadStream stream, Span<byte> buffer, long containerEnd)
    {
        ulong size = ReadChunkPayloadSize(stream, buffer);
        stream.Position += (long)Math.Min(size + (size & 1), (ulong)(Math.Min(containerEnd, stream.Length) - stream.Position));
    }

    /// <summary>
    /// Identifies the chunk type from the chunk.
    /// </summary>
    /// <remarks>
    /// The callers check that a complete chunk header is in the stream before the read.
    /// </remarks>
    /// <param name="stream">The stream to read the data from.</param>
    /// <param name="buffer">The buffer for the data from the stream.</param>
    /// <returns>The chunk type.</returns>
    public static WebpChunkType ReadChunkType(BufferedReadStream stream, Span<byte> buffer)
    {
        stream.Read(buffer);
        return (WebpChunkType)BinaryPrimitives.ReadUInt32BigEndian(buffer);
    }

    /// <summary>
    /// Reads the type of the next chunk that is not metadata. The method reads the ICCP, EXIF and XMP chunks before it into the metadata.
    /// </summary>
    /// <remarks>
    /// This method is the one chunk walker of the still and the animated decoders. Each caller decides what to do with the returned chunk and skips each chunk
    /// that it does not use, as the container specification requires.
    /// </remarks>
    /// <param name="stream">The stream positioned at the next chunk header.</param>
    /// <param name="buffer">The four-byte scratch buffer.</param>
    /// <param name="metadata">The image metadata.</param>
    /// <param name="skipMetadata">A value that tells if the decoder skips the EXIF and XMP contents. The decoder always reads ICC color data.</param>
    /// <param name="containerEnd">The end of the RIFF payload.</param>
    /// <param name="segmentIntegrityHandling">The segment integrity mode. It controls damaged metadata.</param>
    /// <param name="chunkType">The type of the next chunk that is not metadata.</param>
    /// <returns>
    /// <see langword="true"/> if the stream is at the size field of the returned chunk. <see langword="false"/> at the end of the container.
    /// </returns>
    public static bool TryReadNextChunkType(
        BufferedReadStream stream,
        Span<byte> buffer,
        ImageMetadata metadata,
        bool skipMetadata,
        long containerEnd,
        SegmentIntegrityHandling segmentIntegrityHandling,
        out WebpChunkType chunkType)
    {
        // Fewer bytes than a chunk header before the end of the RIFF payload cannot start another chunk.
        while (Math.Min(containerEnd, stream.Length) - stream.Position >= WebpConstants.ChunkHeaderSize)
        {
            chunkType = ReadChunkType(stream, buffer);
            switch (chunkType)
            {
                case WebpChunkType.Iccp:
                    ReadIccProfile(stream, buffer, metadata, segmentIntegrityHandling, containerEnd);
                    break;
                case WebpChunkType.Exif:
                    ReadExifProfile(stream, buffer, metadata, skipMetadata, containerEnd, segmentIntegrityHandling);
                    break;
                case WebpChunkType.Xmp:
                    ReadXmpProfile(stream, buffer, metadata, skipMetadata, containerEnd, segmentIntegrityHandling);
                    break;
                default:
                    return true;
            }
        }

        chunkType = default;
        return false;
    }

    /// <summary>
    /// Reads the header of a lossy webp image.
    /// </summary>
    /// <param name="memoryAllocator">The memory allocator.</param>
    /// <param name="stream">The buffered read stream.</param>
    /// <param name="buffer">The scratch buffer for the reads.</param>
    /// <param name="features">The webp features to parse.</param>
    /// <param name="containerEnd">The end of the RIFF or ANMF payload that contains the chunk.</param>
    /// <param name="segmentIntegrityHandling">The image data integrity mode. It controls damaged or truncated data.</param>
    /// <param name="imageExists">A value that tells if the decoder created the image before this header.</param>
    /// <param name="info">Information about this webp image, or <see langword="null"/> when the header is damaged.</param>
    /// <returns><see langword="true"/> if the header is valid. Otherwise <see langword="false"/>.</returns>
    private static bool TryReadVp8Header(
        MemoryAllocator memoryAllocator,
        BufferedReadStream stream,
        Span<byte> buffer,
        WebpFeatures features,
        long containerEnd,
        SegmentIntegrityHandling segmentIntegrityHandling,
        bool imageExists,
        [NotNullWhen(true)] out WebpImageInfo? info)
    {
        info = null;

        // The size check keeps the 10-byte frame header in the stream, so the reads below do not end early.
        if (!TryReadChunkSize(stream, buffer, containerEnd, WebpConstants.Vp8FrameHeaderSize, segmentIntegrityHandling, imageExists, out uint dataSize))
        {
            return false;
        }

        long chunkEnd = Math.Min(stream.Position + dataSize + (dataSize & 1), stream.Length);

        // Paragraph 9.1 https://tools.ietf.org/html/rfc6386#page-30
        // The frame tag contains four fields:
        // - A 1-bit frame type (0 for key frames, 1 for interframes).
        // - A 3-bit version number.
        // - A 1-bit show_frame flag.
        // - A 19-bit field with the size of the first data partition in bytes.
        stream.Read(buffer, 0, 3);
        uint frameTag = (uint)(buffer[0] | (buffer[1] << 8) | (buffer[2] << 16));
        if ((frameTag & 0x1) == 1)
        {
            segmentIntegrityHandling.ThrowOrIgnoreImageDataError(imageExists, "VP8 header indicates the image is not a key frame");
            return false;
        }

        uint version = (frameTag >> 1) & 0x7;
        if (version > 3)
        {
            segmentIntegrityHandling.ThrowOrIgnoreImageDataError(imageExists, $"VP8 header indicates unknown profile {version}");
            return false;
        }

        if (((frameTag >> 4) & 0x1) == 0)
        {
            segmentIntegrityHandling.ThrowOrIgnoreImageDataError(imageExists, "VP8 header indicates that the first frame is invisible");
            return false;
        }

        uint partitionLength = frameTag >> 5;

        // The method checks for the VP8 magic bytes.
        stream.Read(buffer, 0, 3);
        if (!buffer[..3].SequenceEqual(WebpConstants.Vp8HeaderMagicBytes))
        {
            segmentIntegrityHandling.ThrowOrIgnoreImageDataError(imageExists, "VP8 magic bytes not found");
            return false;
        }

        stream.Read(buffer, 0, 4);
        uint tmp = BinaryPrimitives.ReadUInt16LittleEndian(buffer);
        uint width = tmp & 0x3fff;
        sbyte xScale = (sbyte)(tmp >> 6);
        tmp = BinaryPrimitives.ReadUInt16LittleEndian(buffer[2..]);
        uint height = tmp & 0x3fff;
        sbyte yScale = (sbyte)(tmp >> 6);
        if (width == 0 || height == 0)
        {
            segmentIntegrityHandling.ThrowOrIgnoreImageDataError(imageExists, "width or height can not be zero");
            return false;
        }

        // The first partition follows the 10-byte frame header and must fit in the payload.
        uint remaining = dataSize - WebpConstants.Vp8FrameHeaderSize;
        if (partitionLength > remaining)
        {
            segmentIntegrityHandling.ThrowOrIgnoreImageDataError(imageExists, "bad partition length");
            return false;
        }

        Vp8FrameHeader vp8FrameHeader = new()
        {
            KeyFrame = true,
            Profile = (sbyte)version,
            PartitionLength = partitionLength
        };

        // Only in the IgnoreImageData mode, a payload that ends at EOF gets here. The bit readers then use only the bytes in the stream. Thus, the decoder
        // finds the real end of the data and keeps the rows before it.
        uint available = (uint)Math.Min(remaining, stream.RemainingBytes);
        Vp8BitReader bitReader = new(stream, available, memoryAllocator, Math.Min(partitionLength, available));

        stream.Position = chunkEnd;

        info = new WebpImageInfo
        {
            DataSize = dataSize,
            Width = width,
            Height = height,
            XScale = xScale,
            YScale = yScale,

            // The decoder can parse the Vp8 header while it processes the Vp8X header.
            BitsPerPixel = features?.Alpha == true ? WebpBitsPerPixel.Bit32 : WebpBitsPerPixel.Bit24,
            IsLossless = false,
            Features = features,
            Vp8Profile = (sbyte)version,
            Vp8FrameHeader = vp8FrameHeader,
            Vp8BitReader = bitReader
        };

        return true;
    }

    /// <summary>
    /// Reads the header of a lossless webp image.
    /// </summary>
    /// <param name="memoryAllocator">The memory allocator.</param>
    /// <param name="stream">The buffered read stream.</param>
    /// <param name="buffer">The scratch buffer for the reads.</param>
    /// <param name="features">The webp features to parse.</param>
    /// <param name="containerEnd">The end of the RIFF or ANMF payload that contains the chunk.</param>
    /// <param name="segmentIntegrityHandling">The image data integrity mode. It controls damaged or truncated data.</param>
    /// <param name="imageExists">A value that tells if the decoder created the image before this header.</param>
    /// <param name="info">Information about this webp image, or <see langword="null"/> when the header is damaged.</param>
    /// <returns><see langword="true"/> if the header is valid. Otherwise <see langword="false"/>.</returns>
    private static bool TryReadVp8LHeader(
        MemoryAllocator memoryAllocator,
        BufferedReadStream stream,
        Span<byte> buffer,
        WebpFeatures features,
        long containerEnd,
        SegmentIntegrityHandling segmentIntegrityHandling,
        bool imageExists,
        [NotNullWhen(true)] out WebpImageInfo? info)
    {
        info = null;

        // The five-byte VP8L header must fit inside its own chunk, not in the chunks or data after it.
        if (!TryReadChunkSize(stream, buffer, containerEnd, 5, segmentIntegrityHandling, imageExists, out uint imageDataSize))
        {
            return false;
        }

        long chunkEnd = Math.Min(stream.Position + imageDataSize + (imageDataSize & 1), stream.Length);

        // Only in the IgnoreImageData mode, a payload that ends at EOF gets here. The bit reader then uses only the bytes in the stream. Thus, the decoder
        // finds the real end of the data and keeps the rows before it.
        Vp8LBitReader bitReader = new(stream, (uint)Math.Min(imageDataSize, stream.RemainingBytes), memoryAllocator);

        // The one-byte signature must be 0x2f.
        if (bitReader.ReadValue(8) != WebpConstants.Vp8LHeaderMagicByte)
        {
            bitReader.Dispose();
            segmentIntegrityHandling.ThrowOrIgnoreImageDataError(imageExists, "Invalid VP8L signature");
            return false;
        }

        // The first 28 bits of the bitstream specify the width and height of the image. Each value is the stored value plus 1, so it is never 0.
        uint width = bitReader.ReadValue(WebpConstants.Vp8LImageSizeBits) + 1;
        uint height = bitReader.ReadValue(WebpConstants.Vp8LImageSizeBits) + 1;

        // The alphaIsUsed flag is 0 when all alpha values in the picture are 255, and 1 otherwise. The VP8X chunk can also set alpha before this point.
        features.Alpha |= bitReader.ReadBit();

        // The next 3 bits are the version. The version number is a 3-bit code, and it must be 0. Any other value is an error in the bitstream, not a feature
        // that the decoder does not support. libwebp also rejects it, as a bitstream error. See the WebP Lossless Bitstream Specification,
        // section 3 "RIFF Header" (https://developers.google.com/speed/webp/docs/webp_lossless_bitstream_specification).
        uint version = bitReader.ReadValue(WebpConstants.Vp8LVersionBits);
        if (version != 0)
        {
            bitReader.Dispose();
            segmentIntegrityHandling.ThrowOrIgnoreImageDataError(imageExists, $"Unexpected version number {version} found in VP8L header");
            return false;
        }

        stream.Position = chunkEnd;

        info = new WebpImageInfo
        {
            DataSize = imageDataSize,
            Width = width,
            Height = height,
            BitsPerPixel = features.Alpha ? WebpBitsPerPixel.Bit32 : WebpBitsPerPixel.Bit24,
            IsLossless = true,
            Features = features,
            Vp8LBitReader = bitReader
        };

        return true;
    }

    /// <summary>
    /// Reads the alpha data of an ALPH chunk. The stream is at the first byte after the ALPH header byte.
    /// </summary>
    /// <remarks>
    /// Alpha data is image data. Data that ends at EOF goes to the image data integrity policy.
    /// When the policy ignores the error, the returned buffer holds only the bytes that the method read. The alpha
    /// decoder then finds the real end of the data, and the alpha values that it cannot decode get 0.
    /// </remarks>
    /// <param name="stream">The input stream.</param>
    /// <param name="memoryAllocator">The memory allocator.</param>
    /// <param name="alphaDataSize">The declared size of the alpha data.</param>
    /// <param name="segmentIntegrityHandling">The image data integrity mode.</param>
    /// <returns>The alpha data. The caller owns the buffer.</returns>
    private static IMemoryOwner<byte> ReadAlphaData(
        BufferedReadStream stream,
        MemoryAllocator memoryAllocator,
        int alphaDataSize,
        SegmentIntegrityHandling segmentIntegrityHandling)
    {
        IMemoryOwner<byte> alphaData = memoryAllocator.Allocate<byte>(alphaDataSize);
        try
        {
            int bytesRead = stream.Read(alphaData.GetSpan(), 0, alphaDataSize);
            if (bytesRead == alphaDataSize)
            {
                return alphaData;
            }

            segmentIntegrityHandling.ThrowOrIgnoreImageDataError("Not enough data to read the alpha data from the stream.");

            // The allocation is not clean. The method copies only the bytes that it read, so the decoder decodes no stale bytes.
            IMemoryOwner<byte> readData = memoryAllocator.Allocate<byte>(bytesRead);
            alphaData.GetSpan()[..bytesRead].CopyTo(readData.GetSpan());
            alphaData.Dispose();
            return readData;
        }
        catch
        {
            alphaData.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Reads the payload size stored in a chunk header. RIFF puts a padding byte after a payload of odd size. The padding byte belongs to the chunk but not to
    /// its payload, so the returned size does not include it.
    /// </summary>
    /// <remarks>
    /// The callers check that a complete chunk header is in the stream before they read the chunk type.
    /// </remarks>
    /// <param name="stream">The input stream.</param>
    /// <param name="buffer">The four-byte size buffer.</param>
    /// <returns>The payload size.</returns>
    private static uint ReadChunkPayloadSize(BufferedReadStream stream, Span<byte> buffer)
    {
        stream.Read(buffer);
        return BinaryPrimitives.ReadUInt32LittleEndian(buffer);
    }

    /// <summary>
    /// Reads the ICCP chunk from the stream.
    /// </summary>
    /// <remarks>
    /// The ICC profile is optional, but the decoder cannot skip a chunk whose extent is not in the container. Thus, the method throws in each mode for a chunk
    /// that does not fit. Only damage in the contents of a complete chunk goes to the ancillary integrity policy.
    /// </remarks>
    /// <param name="stream">The stream to decode from.</param>
    /// <param name="buffer">The four-byte size buffer.</param>
    /// <param name="metadata">The image metadata.</param>
    /// <param name="segmentIntegrityHandling">
    /// The segment integrity mode. It controls if the decoder throws for an error in the profile contents, and if the decoder keeps the readable tags of an
    /// incomplete profile.
    /// </param>
    /// <param name="containerEnd">The end of the RIFF payload.</param>
    private static void ReadIccProfile(
        BufferedReadStream stream,
        Span<byte> buffer,
        ImageMetadata metadata,
        SegmentIntegrityHandling segmentIntegrityHandling,
        long containerEnd)
    {
        uint payloadSize = ReadChunkPayloadSize(stream, buffer);

        // Validate framing outside the ancillary error policy. The next chunk must remain inside RIFF.
        ulong paddedSize = (ulong)payloadSize + (payloadSize & 1);
        if (paddedSize > (ulong)(Math.Min(containerEnd, stream.Length) - stream.Position))
        {
            WebpThrowHelper.ThrowInvalidImageContentException("Not enough data to read the ICCP chunk.");
        }

        long chunkEnd = stream.Position + (long)paddedSize;

        // The ICC profile is color metadata, so the decoder reads it also when SkipMetadata is enabled. The decoder keeps only the first profile.
        if (metadata.IccProfile is null)
        {
            IccReader reader = new();
            try
            {
                // The check above keeps the payload in the stream. The reader throws for a payload that is larger than the ICC size limit.
                if (reader.TryReadSegment(stream, (int)Math.Min(payloadSize, int.MaxValue)))
                {
                    metadata.IccProfile = reader.ToProfile(segmentIntegrityHandling.IgnoresAncillaryErrors());
                }
                else
                {
                    segmentIntegrityHandling.ThrowOrIgnoreAncillaryError("Not enough data to read the ICCP chunk.");
                }
            }
            catch (InvalidIccProfileException ex)
            {
                // The ICC reader reports a damaged profile. The Strict mode throws it inside an InvalidImageContentException, as the other decoders do. The
                // other modes continue without the profile.
                segmentIntegrityHandling.ThrowOrIgnoreAncillaryError("Invalid WEBP ICC profile.", ex);
            }
        }

        // The parser can reject a profile before it reads all of its payload. Thus the method moves the stream to the end of the payload and its RIFF padding.
        stream.Position = chunkEnd;
    }

    /// <summary>
    /// Reads the EXIF profile from the stream.
    /// </summary>
    /// <param name="stream">The stream to decode from.</param>
    /// <param name="buffer">The four-byte size buffer.</param>
    /// <param name="metadata">The image metadata.</param>
    /// <param name="ignoreMetadata">If <see langword="true"/>, the method ignores the metadata.</param>
    /// <param name="containerEnd">The end of the RIFF payload.</param>
    /// <param name="segmentIntegrityHandling">The segment integrity mode. It controls if a damaged EXIF packet throws.</param>
    private static void ReadExifProfile(
        BufferedReadStream stream,
        Span<byte> buffer,
        ImageMetadata metadata,
        bool ignoreMetadata,
        long containerEnd,
        SegmentIntegrityHandling segmentIntegrityHandling)
    {
        uint payloadSize = ReadChunkPayloadSize(stream, buffer);
        long chunkEnd = GetChunkEnd(stream, payloadSize, containerEnd, out bool isComplete);
        if (ignoreMetadata || metadata.ExifProfile is not null)
        {
            stream.Position = chunkEnd;
            return;
        }

        if (!isComplete)
        {
            // The partial payload is not another chunk header. Thus the method moves the stream to the end of the container.
            stream.Position = chunkEnd;
            segmentIntegrityHandling.ThrowOrIgnoreAncillaryError("Not enough data to read the metadata chunk.");
            return;
        }

        if (!ExifReader.TryReadProfile(stream, payloadSize, out ExifProfile? exifProfile))
        {
            stream.Position = chunkEnd;
            segmentIntegrityHandling.ThrowOrIgnoreAncillaryError("Not enough data to read the EXIF packet.");
            return;
        }

        if (metadata.TrySetExifProfile(exifProfile))
        {
            // Set the resolution from the admitted EXIF profile.
            double horizontalValue = GetExifResolutionValue(exifProfile, ExifTag.XResolution);
            double verticalValue = GetExifResolutionValue(exifProfile, ExifTag.YResolution);

            if (horizontalValue > 0 && verticalValue > 0)
            {
                metadata.HorizontalResolution = horizontalValue;
                metadata.VerticalResolution = verticalValue;
                metadata.ResolutionUnits = UnitConverter.ExifProfileToResolutionUnit(exifProfile);
            }
        }

        stream.Position = chunkEnd;
    }

    /// <summary>
    /// Reads the XMP profile from the stream.
    /// </summary>
    /// <param name="stream">The stream to decode from.</param>
    /// <param name="buffer">The four-byte size buffer.</param>
    /// <param name="metadata">The image metadata.</param>
    /// <param name="ignoreMetadata">If <see langword="true"/>, the method ignores the metadata.</param>
    /// <param name="containerEnd">The end of the RIFF payload.</param>
    /// <param name="segmentIntegrityHandling">The segment integrity mode. It controls if a damaged XMP packet throws.</param>
    private static void ReadXmpProfile(
        BufferedReadStream stream,
        Span<byte> buffer,
        ImageMetadata metadata,
        bool ignoreMetadata,
        long containerEnd,
        SegmentIntegrityHandling segmentIntegrityHandling)
    {
        uint payloadSize = ReadChunkPayloadSize(stream, buffer);
        long chunkEnd = GetChunkEnd(stream, payloadSize, containerEnd, out bool isComplete);
        if (ignoreMetadata || metadata.XmpProfile is not null)
        {
            stream.Position = chunkEnd;
            return;
        }

        if (!isComplete)
        {
            // The partial payload is not another chunk header. Thus the method moves the stream to the end of the container.
            stream.Position = chunkEnd;
            segmentIntegrityHandling.ThrowOrIgnoreAncillaryError("Not enough data to read the metadata chunk.");
            return;
        }

        if (!XmpReader.TryReadProfile(stream, payloadSize, out XmpProfile? profile))
        {
            stream.Position = chunkEnd;
            segmentIntegrityHandling.ThrowOrIgnoreAncillaryError("Not enough data to read the XMP packet.");
            return;
        }

        metadata.XmpProfile = profile;
        stream.Position = chunkEnd;
    }

    /// <summary>
    /// Finds the next RIFF chunk. The method includes the padding byte after a payload of odd size.
    /// </summary>
    /// <param name="stream">The input positioned at the payload.</param>
    /// <param name="payloadLength">The declared length, without the padding.</param>
    /// <param name="containerEnd">The end of the RIFF payload.</param>
    /// <param name="isComplete">A value that tells if the padded payload is in the container and the stream.</param>
    /// <returns>The next chunk position, limited to the end of the RIFF container or stream.</returns>
    private static long GetChunkEnd(BufferedReadStream stream, uint payloadLength, long containerEnd, out bool isComplete)
    {
        ulong paddedLength = (ulong)payloadLength + (payloadLength & 1);
        long available = Math.Max(0, Math.Min(containerEnd, stream.Length) - stream.Position);
        isComplete = paddedLength <= (ulong)available;
        return stream.Position + (long)Math.Min(paddedLength, (ulong)available);
    }

    private static double GetExifResolutionValue(ExifProfile exifProfile, ExifTag<Rational> tag)
    {
        if (exifProfile.TryGetValue(tag, out IExifValue<Rational>? resolution))
        {
            return resolution.Value.ToDouble();
        }

        return 0;
    }
}
