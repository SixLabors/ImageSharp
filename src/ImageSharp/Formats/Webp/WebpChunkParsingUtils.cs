// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.Buffers.Binary;
using SixLabors.ImageSharp.Formats.Webp.BitReader;
using SixLabors.ImageSharp.Formats.Webp.Lossy;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Metadata.Profiles.Xmp;

namespace SixLabors.ImageSharp.Formats.Webp
{
    internal static class WebpChunkParsingUtils
    {
        /// <summary>
        /// Reads the header of a lossy webp image.
        /// </summary>
        /// <returns>Information about this webp image.</returns>
        public static WebpImageInfo ReadVp8Header(MemoryAllocator memoryAllocator, BufferedReadStream stream, byte[] buffer, WebpFeatures features)
        {
            // VP8 data size (not including this 4 bytes).
            int bytesRead = stream.Read(buffer, 0, 4);
            if (bytesRead != 4)
            {
                WebpThrowHelper.ThrowInvalidImageContentException("Not enough data to read the VP8 header");
            }

            uint dataSize = BinaryPrimitives.ReadUInt32LittleEndian(buffer);

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

            if (!buffer.AsSpan(0, 3).SequenceEqual(WebpConstants.Vp8HeaderMagicBytes))
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
            tmp = BinaryPrimitives.ReadUInt16LittleEndian(buffer.AsSpan(2));
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

            var vp8FrameHeader = new Vp8FrameHeader()
            {
                KeyFrame = true,
                Profile = (sbyte)version,
                PartitionLength = partitionLength
            };

            var bitReader = new Vp8BitReader(
                stream,
                remaining,
                memoryAllocator,
                partitionLength)
            {
                Remaining = remaining
            };

            return new WebpImageInfo()
            {
                Width = width,
                Height = height,
                XScale = xScale,
                YScale = yScale,
                BitsPerPixel = features?.Alpha == true ? WebpBitsPerPixel.Pixel32 : WebpBitsPerPixel.Pixel24,
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
        /// <returns>Information about this image.</returns>
        public static WebpImageInfo ReadVp8LHeader(MemoryAllocator memoryAllocator, BufferedReadStream stream, byte[] buffer, WebpFeatures features)
        {
            // VP8 data size.
            uint imageDataSize = ReadChunkSize(stream, buffer);

            var bitReader = new Vp8LBitReader(stream, imageDataSize, memoryAllocator);

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
            // TODO: this flag value is not used yet
            bool alphaIsUsed = bitReader.ReadBit();

            // The next 3 bits are the version. The version number is a 3 bit code that must be set to 0.
            // Any other value should be treated as an error.
            uint version = bitReader.ReadValue(WebpConstants.Vp8LVersionBits);
            if (version != 0)
            {
                WebpThrowHelper.ThrowNotSupportedException($"Unexpected version number {version} found in VP8L header");
            }

            return new WebpImageInfo()
            {
                Width = width,
                Height = height,
                BitsPerPixel = WebpBitsPerPixel.Pixel32,
                IsLossless = true,
                Features = features,
                Vp8LBitReader = bitReader
            };
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
        /// <returns>Information about this webp image.</returns>
        public static WebpImageInfo ReadVp8XHeader(BufferedReadStream stream, byte[] buffer, WebpFeatures features)
        {
            uint fileSize = ReadChunkSize(stream, buffer);

            // The first byte contains information about the image features used.
            byte imageFeatures = (byte)stream.ReadByte();

            // The first two bit of it are reserved and should be 0.
            if (imageFeatures >> 6 != 0)
            {
                WebpThrowHelper.ThrowImageFormatException("first two bits of the VP8X header are expected to be zero");
            }

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

            // 3 reserved bytes should follow which are supposed to be zero.
            stream.Read(buffer, 0, 3);
            if (buffer[0] != 0 || buffer[1] != 0 || buffer[2] != 0)
            {
                WebpThrowHelper.ThrowImageFormatException("reserved bytes should be zero");
            }

            // 3 bytes for the width.
            uint width = ReadUnsignedInt24Bit(stream, buffer) + 1;

            // 3 bytes for the height.
            uint height = ReadUnsignedInt24Bit(stream, buffer) + 1;

            // Read all the chunks in the order they occur.
            var info = new WebpImageInfo()
            {
                Width = width,
                Height = height,
                Features = features
            };

            return info;
        }

        /// <summary>
        /// Reads a unsigned 24 bit integer.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="buffer">The buffer to store the read data into.</param>
        /// <returns>A unsigned 24 bit integer.</returns>
        public static uint ReadUnsignedInt24Bit(BufferedReadStream stream, byte[] buffer)
        {
            if (stream.Read(buffer, 0, 3) == 3)
            {
                buffer[3] = 0;
                return BinaryPrimitives.ReadUInt32LittleEndian(buffer);
            }

            throw new ImageFormatException("Invalid Webp data, could not read unsigned integer.");
        }

        /// <summary>
        /// Reads the chunk size. If Chunk Size is odd, a single padding byte will be added to the payload,
        /// so the chunk size will be increased by 1 in those cases.
        /// </summary>
        /// <param name="stream">The stream to read the data from.</param>
        /// <param name="buffer">Buffer to store the data read from the stream.</param>
        /// <returns>The chunk size in bytes.</returns>
        public static uint ReadChunkSize(BufferedReadStream stream, byte[] buffer)
        {
            if (stream.Read(buffer, 0, 4) == 4)
            {
                uint chunkSize = BinaryPrimitives.ReadUInt32LittleEndian(buffer);
                return (chunkSize % 2 == 0) ? chunkSize : chunkSize + 1;
            }

            throw new ImageFormatException("Invalid Webp data, could not read chunk size.");
        }

        /// <summary>
        /// Identifies the chunk type from the chunk.
        /// </summary>
        /// <param name="stream">The stream to read the data from.</param>
        /// <param name="buffer">Buffer to store the data read from the stream.</param>
        /// <exception cref="ImageFormatException">
        /// Thrown if the input stream is not valid.
        /// </exception>
        public static WebpChunkType ReadChunkType(BufferedReadStream stream, byte[] buffer)
        {
            if (stream.Read(buffer, 0, 4) == 4)
            {
                var chunkType = (WebpChunkType)BinaryPrimitives.ReadUInt32BigEndian(buffer);
                return chunkType;
            }

            throw new ImageFormatException("Invalid Webp data, could not read chunk type.");
        }

        /// <summary>
        /// Parses optional metadata chunks. There SHOULD be at most one chunk of each type ('EXIF' and 'XMP ').
        /// If there are more such chunks, readers MAY ignore all except the first one.
        /// Also, a file may possibly contain both 'EXIF' and 'XMP ' chunks.
        /// </summary>
        public static void ParseOptionalChunks(BufferedReadStream stream, WebpChunkType chunkType, ImageMetadata metadata, bool ignoreMetaData, byte[] buffer)
        {
            long streamLength = stream.Length;
            while (stream.Position < streamLength)
            {
                uint chunkLength = ReadChunkSize(stream, buffer);

                if (ignoreMetaData)
                {
                    stream.Skip((int)chunkLength);
                }

                int bytesRead;
                switch (chunkType)
                {
                    case WebpChunkType.Exif:
                        byte[] exifData = new byte[chunkLength];
                        bytesRead = stream.Read(exifData, 0, (int)chunkLength);
                        if (bytesRead != chunkLength)
                        {
                            WebpThrowHelper.ThrowImageFormatException("Could not read enough data for the EXIF profile");
                        }

                        if (metadata.ExifProfile != null)
                        {
                            metadata.ExifProfile = new ExifProfile(exifData);
                        }

                        break;
                    case WebpChunkType.Xmp:
                        byte[] xmpData = new byte[chunkLength];
                        bytesRead = stream.Read(xmpData, 0, (int)chunkLength);
                        if (bytesRead != chunkLength)
                        {
                            WebpThrowHelper.ThrowImageFormatException("Could not read enough data for the XMP profile");
                        }

                        if (metadata.XmpProfile != null)
                        {
                            metadata.XmpProfile = new XmpProfile(xmpData);
                        }

                        break;
                    default:
                        stream.Skip((int)chunkLength);
                        break;
                }
            }
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
}
