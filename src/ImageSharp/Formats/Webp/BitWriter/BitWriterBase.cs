// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.Metadata.Profiles.Xmp;

namespace SixLabors.ImageSharp.Formats.Webp.BitWriter;

internal abstract class BitWriterBase
{
    private const uint MaxDimension = 16777215;

    private const ulong MaxCanvasPixels = 4294967295ul;

    protected const uint ExtendedFileChunkSize = WebpConstants.ChunkHeaderSize + WebpConstants.Vp8XChunkSize;

    /// <summary>
    /// Buffer to write to.
    /// </summary>
    private byte[] buffer;

    /// <summary>
    /// A scratch buffer to reduce allocations.
    /// </summary>
    private ScratchBuffer scratchBuffer;  // mutable struct, don't make readonly

    /// <summary>
    /// Initializes a new instance of the <see cref="BitWriterBase"/> class.
    /// </summary>
    /// <param name="expectedSize">The expected size in bytes.</param>
    protected BitWriterBase(int expectedSize) => this.buffer = new byte[expectedSize];

    /// <summary>
    /// Initializes a new instance of the <see cref="BitWriterBase"/> class.
    /// Used internally for cloning.
    /// </summary>
    /// <param name="buffer">The byte buffer.</param>
    private protected BitWriterBase(byte[] buffer) => this.buffer = buffer;

    public byte[] Buffer => this.buffer;

    /// <summary>
    /// Gets the number of bytes of the encoded image data.
    /// </summary>
    /// <returns>The number of bytes of the image data.</returns>
    public abstract int NumBytes { get; }

    /// <summary>
    /// Writes the encoded bytes of the image to the stream. Call Finish() before this.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    public void WriteToStream(Stream stream) => stream.Write(this.Buffer.AsSpan(0, this.NumBytes));

    /// <summary>
    /// Writes the encoded bytes of the image to the given buffer. Call Finish() before this.
    /// </summary>
    /// <param name="dest">The destination buffer.</param>
    public void WriteToBuffer(Span<byte> dest) => this.Buffer.AsSpan(0, this.NumBytes).CopyTo(dest);

    /// <summary>
    /// Resizes the buffer to write to.
    /// </summary>
    /// <param name="extraSize">The extra size in bytes needed.</param>
    public abstract void BitWriterResize(int extraSize);

    /// <summary>
    /// Flush leftover bits.
    /// </summary>
    public abstract void Finish();

    protected void ResizeBuffer(int maxBytes, int sizeRequired)
    {
        int newSize = (3 * maxBytes) >> 1;
        if (newSize < sizeRequired)
        {
            newSize = sizeRequired;
        }

        // Make new size multiple of 1k.
        newSize = ((newSize >> 10) + 1) << 10;
        Array.Resize(ref this.buffer, newSize);
    }

    /// <summary>
    /// Writes the RIFF header to the stream.
    /// </summary>
    /// <remarks>Think of it as a static method — none of the other members are called except for <see cref="scratchBuffer"/></remarks>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="riffSize">The block length.</param>
    protected void WriteRiffHeader(Stream stream, uint riffSize)
    {
        stream.Write(WebpConstants.RiffFourCc);
        BinaryPrimitives.WriteUInt32LittleEndian(this.scratchBuffer.Span, riffSize);
        stream.Write(this.scratchBuffer.Span[..4]);
        stream.Write(WebpConstants.WebpHeader);
    }

    /// <summary>
    /// Calculates the chunk size of EXIF, XMP or ICCP metadata.
    /// </summary>
    /// <remarks>Think of it as a static method — none of the other members are called except for <see cref="scratchBuffer"/></remarks>
    /// <param name="metadataBytes">The metadata profile bytes.</param>
    /// <returns>The metadata chunk size in bytes.</returns>
    protected static uint MetadataChunkSize(byte[] metadataBytes)
    {
        uint metaSize = (uint)metadataBytes.Length;
        return WebpConstants.ChunkHeaderSize + metaSize + (metaSize & 1);
    }

    /// <summary>
    /// Calculates the chunk size of a alpha chunk.
    /// </summary>
    /// <param name="alphaBytes">The alpha chunk bytes.</param>
    /// <returns>The alpha data chunk size in bytes.</returns>
    protected static uint AlphaChunkSize(Span<byte> alphaBytes)
    {
        uint alphaSize = (uint)alphaBytes.Length + 1;
        return WebpConstants.ChunkHeaderSize + alphaSize + (alphaSize & 1);
    }

    /// <summary>
    /// Overwrites ides the write file size.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    protected static void OverwriteFileSize(Stream stream)
    {
        uint position = (uint)stream.Position;
        stream.Position = 4;
        byte[] buffer = new byte[4];

        // "RIFF"(4)+uint32 size(4)
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, position - WebpConstants.ChunkHeaderSize);
        stream.Write(buffer);
        stream.Position = position;
    }

    /// <summary>
    /// Writes a metadata profile (EXIF or XMP) to the stream.
    /// </summary>
    /// <remarks>Think of it as a static method — none of the other members are called except for <see cref="scratchBuffer"/></remarks>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="metadataBytes">The metadata profile's bytes.</param>
    /// <param name="chunkType">The chuck type to write.</param>
    protected void WriteMetadataProfile(Stream stream, byte[]? metadataBytes, WebpChunkType chunkType)
    {
        DebugGuard.NotNull(metadataBytes, nameof(metadataBytes));

        uint size = (uint)metadataBytes.Length;
        Span<byte> buf = this.scratchBuffer.Span[..4];
        BinaryPrimitives.WriteUInt32BigEndian(buf, (uint)chunkType);
        stream.Write(buf);
        BinaryPrimitives.WriteUInt32LittleEndian(buf, size);
        stream.Write(buf);
        stream.Write(metadataBytes);

        // Add padding byte if needed.
        if ((size & 1) == 1)
        {
            stream.WriteByte(0);
        }
    }

    /// <summary>
    /// Writes the color profile(<see cref="WebpChunkType.Iccp"/>) to the stream.
    /// </summary>
    /// <remarks>Think of it as a static method — none of the other members are called except for <see cref="scratchBuffer"/></remarks>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="iccProfileBytes">The color profile bytes.</param>
    protected void WriteColorProfile(Stream stream, byte[] iccProfileBytes) => this.WriteMetadataProfile(stream, iccProfileBytes, WebpChunkType.Iccp);

    /// <summary>
    /// Writes the animation parameter(<see cref="WebpChunkType.AnimationParameter"/>) to the stream.
    /// </summary>
    /// <remarks>Think of it as a static method — none of the other members are called except for <see cref="scratchBuffer"/></remarks>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="background">
    /// The default background color of the canvas in [Blue, Green, Red, Alpha] byte order.
    /// This color MAY be used to fill the unused space on the canvas around the frames,
    /// as well as the transparent pixels of the first frame.
    /// The background color is also used when the Disposal method is 1.
    /// </param>
    /// <param name="loopCount">The number of times to loop the animation. If it is 0, this means infinitely.</param>
    protected void WriteAnimationParameter(Stream stream, uint background, ushort loopCount)
    {
        Span<byte> buf = this.scratchBuffer.Span[..4];
        BinaryPrimitives.WriteUInt32BigEndian(buf, (uint)WebpChunkType.AnimationParameter);
        stream.Write(buf);
        BinaryPrimitives.WriteUInt32LittleEndian(buf, sizeof(uint) + sizeof(ushort));
        stream.Write(buf);
        BinaryPrimitives.WriteUInt32LittleEndian(buf, background);
        stream.Write(buf);
        BinaryPrimitives.WriteUInt16LittleEndian(buf[..2], loopCount);
        stream.Write(buf[..2]);
    }

    /// <summary>
    /// Writes the animation frame(<see cref="WebpChunkType.Animation"/>) to the stream.
    /// </summary>
    /// <remarks>Think of it as a static method — none of the other members are called except for <see cref="scratchBuffer"/></remarks>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="animation">Animation frame data.</param>
    /// <param name="data">Frame data.</param>
    protected void WriteAnimationFrame(Stream stream, AnimationFrameData animation, Span<byte> data)
    {
        uint size = AnimationFrameData.HeaderSize + (uint)data.Length;
        Span<byte> buf = this.scratchBuffer.Span[..4];
        BinaryPrimitives.WriteUInt32BigEndian(buf, (uint)WebpChunkType.Animation);
        stream.Write(buf);
        BinaryPrimitives.WriteUInt32BigEndian(buf, size);
        stream.Write(buf);
        WebpChunkParsingUtils.WriteUInt24LittleEndian(stream, animation.X);
        WebpChunkParsingUtils.WriteUInt24LittleEndian(stream, animation.Y);
        WebpChunkParsingUtils.WriteUInt24LittleEndian(stream, animation.Width - 1);
        WebpChunkParsingUtils.WriteUInt24LittleEndian(stream, animation.Height - 1);
        WebpChunkParsingUtils.WriteUInt24LittleEndian(stream, animation.Duration);
        byte flag = (byte)(((int)animation.BlendingMethod << 1) | (int)animation.DisposalMethod);
        stream.WriteByte(flag);
        stream.Write(data);
    }

    /// <summary>
    /// Writes the alpha chunk to the stream.
    /// </summary>
    /// <remarks>Think of it as a static method — none of the other members are called except for <see cref="scratchBuffer"/></remarks>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="dataBytes">The alpha channel data bytes.</param>
    /// <param name="alphaDataIsCompressed">Indicates, if the alpha channel data is compressed.</param>
    protected void WriteAlphaChunk(Stream stream, Span<byte> dataBytes, bool alphaDataIsCompressed)
    {
        uint size = (uint)dataBytes.Length + 1;
        Span<byte> buf = this.scratchBuffer.Span[..4];
        BinaryPrimitives.WriteUInt32BigEndian(buf, (uint)WebpChunkType.Alpha);
        stream.Write(buf);
        BinaryPrimitives.WriteUInt32LittleEndian(buf, size);
        stream.Write(buf);

        byte flags = 0;
        if (alphaDataIsCompressed)
        {
            flags = 1;
        }

        stream.WriteByte(flags);
        stream.Write(dataBytes);

        // Add padding byte if needed.
        if ((size & 1) == 1)
        {
            stream.WriteByte(0);
        }
    }

    /// <summary>
    /// Writes a VP8X header to the stream.
    /// </summary>
    /// <remarks>Think of it as a static method — none of the other members are called except for <see cref="scratchBuffer"/></remarks>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="exifProfile">A exif profile or null, if it does not exist.</param>
    /// <param name="xmpProfile">A XMP profile or null, if it does not exist.</param>
    /// <param name="iccProfile">The color profile.</param>
    /// <param name="width">The width of the image.</param>
    /// <param name="height">The height of the image.</param>
    /// <param name="hasAlpha">Flag indicating, if a alpha channel is present.</param>
    protected void WriteVp8XHeader(Stream stream, ExifProfile? exifProfile, XmpProfile? xmpProfile, IccProfile? iccProfile, uint width, uint height, bool hasAlpha)
    {
        if (width > MaxDimension || height > MaxDimension)
        {
            WebpThrowHelper.ThrowInvalidImageDimensions($"Image width or height exceeds maximum allowed dimension of {MaxDimension}");
        }

        // The spec states that the product of Canvas Width and Canvas Height MUST be at most 2^32 - 1.
        if (width * height > MaxCanvasPixels)
        {
            WebpThrowHelper.ThrowInvalidImageDimensions("The product of image width and height MUST be at most 2^32 - 1");
        }

        uint flags = 0;
        if (exifProfile != null)
        {
            // Set exif bit.
            flags |= 8;
        }

        /*
        if (isAnimated)
        {
            // Set animated flag.
            flags |= 2;
        }
        */

        if (xmpProfile != null)
        {
            // Set xmp bit.
            flags |= 4;
        }

        if (hasAlpha)
        {
            // Set alpha bit.
            flags |= 16;
        }

        if (iccProfile != null)
        {
            // Set iccp flag.
            flags |= 32;
        }

        Span<byte> buf = this.scratchBuffer.Span[..4];
        BinaryPrimitives.WriteUInt32BigEndian(buf, (uint)WebpChunkType.Vp8X);
        stream.Write(buf);
        BinaryPrimitives.WriteUInt32LittleEndian(buf, WebpConstants.Vp8XChunkSize);
        stream.Write(buf);
        BinaryPrimitives.WriteUInt32LittleEndian(buf, flags);
        stream.Write(buf);
        BinaryPrimitives.WriteUInt32LittleEndian(buf, width - 1);
        stream.Write(buf[..3]);
        BinaryPrimitives.WriteUInt32LittleEndian(buf, height - 1);
        stream.Write(buf[..3]);
    }

    private unsafe struct ScratchBuffer
    {
        private const int Size = 4;
        private fixed byte scratch[Size];

        public Span<byte> Span => MemoryMarshal.CreateSpan(ref this.scratch[0], Size);
    }
}
