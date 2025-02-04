// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Icon;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory.Internals;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;

namespace SixLabors.ImageSharp.Formats.Ani;

internal class AniDecoderCore : ImageDecoderCore
{
    /// <summary>
    /// The general decoder options.
    /// </summary>
    private readonly Configuration configuration;

    /// <summary>
    /// The stream to decode from.
    /// </summary>
    private BufferedReadStream currentStream = null!;

    private AniHeader header;

    public AniDecoderCore(DecoderOptions options)
        : base(options) => this.configuration = options.Configuration;

    protected override Image<TPixel> Decode<TPixel>(BufferedReadStream stream, CancellationToken cancellationToken)
    {
        this.currentStream = stream;
        this.ReadHeader();
        ImageMetadata metadata = new();
        AniMetadata aniMetadata = metadata.GetAniMetadata();
        Image<TPixel>? image = null;

        Span<byte> buffer = stackalloc byte[20];

        try
        {
            while (this.TryReadChunk(buffer, out AniChunk chunk))
            {
                try
                {
                    switch (chunk.Type)
                    {
                        case AniChunkType.Seq:

                            break;
                        case AniChunkType.Rate:

                            break;
                        case AniChunkType.List:

                            break;
                        default:
                            break;
                    }
                }
                finally
                {
                    chunk.Data?.Dispose();
                }
            }
        }
        catch
        {
            image?.Dispose();
            throw;
        }


        throw new NotImplementedException();
    }

    private void ReadSeq()
    {

    }

    private bool TryReadChunk(Span<byte> buffer, out AniChunk chunk)
    {
        if (!this.TryReadChunkLength(buffer, out int length))
        {
            // IEND
            chunk = default;
            return false;
        }

        while (length < 0)
        {
            // Not a valid chunk so try again until we reach a known chunk.
            if (!this.TryReadChunkLength(buffer, out length))
            {
                // IEND
                chunk = default;
                return false;
            }
        }

        AniChunkType type = this.ReadChunkType(buffer);

        // A chunk might report a length that exceeds the length of the stream.
        // Take the minimum of the two values to ensure we don't read past the end of the stream.
        long position = this.currentStream.Position;
        chunk = new AniChunk(
            length: (int)Math.Min(length, this.currentStream.Length - position),
            type: type,
            data: this.ReadChunkData(length));

        return true;
    }


    protected override ImageInfo Identify(BufferedReadStream stream, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    private void ReadHeader()
    {
        // Skip the identifier
        this.currentStream.Skip(12);
        Span<byte> buffer = stackalloc byte[36];
        _ = this.currentStream.Read(buffer);
        this.header = AniHeader.Parse(buffer);
    }

    private void ReadSeq(Stream stream)
    {
        Span<byte> buffer = stackalloc byte[4];
        int length = BinaryPrimitives.ReadInt32BigEndian(buffer);

    }

    /// <summary>
    /// Attempts to read the length of the next chunk.
    /// </summary>
    /// <param name="buffer">Temporary buffer.</param>
    /// <param name="result">The result length. If the return type is <see langword="false"/> this parameter is passed uninitialized.</param>
    /// <returns>
    /// Whether the length was read.
    /// </returns>
    [MethodImpl(InliningOptions.ShortMethod)]
    private bool TryReadChunkLength(Span<byte> buffer, out int result)
    {
        if (this.currentStream.Read(buffer, 0, 4) == 4)
        {
            result = BinaryPrimitives.ReadInt32BigEndian(buffer);

            return true;
        }

        result = 0;
        return false;
    }

    /// <summary>
    /// Identifies the chunk type from the chunk.
    /// </summary>
    /// <param name="buffer">Temporary buffer.</param>
    /// <exception cref="ImageFormatException">
    /// Thrown if the input stream is not valid.
    /// </exception>
    [MethodImpl(InliningOptions.ShortMethod)]
    private AniChunkType ReadChunkType(Span<byte> buffer)
    {
        if (this.currentStream.Read(buffer, 0, 4) == 4)
        {
            return (AniChunkType)BinaryPrimitives.ReadUInt32BigEndian(buffer);
        }

        PngThrowHelper.ThrowInvalidChunkType();

        // The IDE cannot detect the throw here.
        return default;
    }

    /// <summary>
    /// Reads the chunk data from the stream.
    /// </summary>
    /// <param name="length">The length of the chunk data to read.</param>
    [MethodImpl(InliningOptions.ShortMethod)]
    private IMemoryOwner<byte> ReadChunkData(int length)
    {
        if (length == 0)
        {
            return new BasicArrayBuffer<byte>([]);
        }

        // We rent the buffer here to return it afterwards in Decode()
        // We don't want to throw a degenerated memory exception here as we want to allow partial decoding
        // so limit the length.
        length = (int)Math.Min(length, this.currentStream.Length - this.currentStream.Position);
        IMemoryOwner<byte> buffer = this.configuration.MemoryAllocator.Allocate<byte>(length, AllocationOptions.Clean);

        this.currentStream.Read(buffer.GetSpan(), 0, length);

        return buffer;
    }

}
