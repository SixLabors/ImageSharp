// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Text;
using SixLabors.ImageSharp.Formats.Webp.Chunks;

namespace SixLabors.ImageSharp.Formats.Webp;

internal static class RiffHelper
{
    /// <summary>
    /// The header bytes identifying RIFF file.
    /// </summary>
    private const uint RiffFourCc = 0x52_49_46_46;

    public static void WriteRiffFile(Stream stream, string formType, Action<Stream> func) =>
        WriteChunk(stream, RiffFourCc, s =>
        {
            s.Write(Encoding.ASCII.GetBytes(formType));
            func(s);
        });

    public static void WriteChunk(Stream stream, uint fourCc, Action<Stream> func)
    {
        long sizePosition = BeginWriteChunk(stream, fourCc);
        func(stream);
        EndWriteChunk(stream, sizePosition);
    }

    public static void WriteChunk(Stream stream, uint fourCc, ReadOnlySpan<byte> data)
    {
        Span<byte> buffer = stackalloc byte[4];

        // write the fourCC
        BinaryPrimitives.WriteUInt32BigEndian(buffer, fourCc);
        stream.Write(buffer);
        uint size = (uint)data.Length;
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, size);
        stream.Write(buffer);
        stream.Write(data);

        // padding
        if (size % 2 is 1)
        {
            stream.WriteByte(0);
        }
    }

    public static unsafe void WriteChunk<TStruct>(Stream stream, uint fourCc, in TStruct chunk)
        where TStruct : unmanaged
    {
        fixed (TStruct* ptr = &chunk)
        {
            WriteChunk(stream, fourCc, new Span<byte>(ptr, sizeof(TStruct)));
        }
    }

    public static long BeginWriteChunk(Stream stream, uint fourCc)
    {
        Span<byte> buffer = stackalloc byte[4];

        // write the fourCC
        BinaryPrimitives.WriteUInt32BigEndian(buffer, fourCc);
        stream.Write(buffer);

        long sizePosition = stream.Position;
        stream.Position += 4;

        return sizePosition;
    }

    public static void EndWriteChunk(Stream stream, long sizePosition)
    {
        Span<byte> buffer = stackalloc byte[4];

        long position = stream.Position;

        // The RIFF chunk size field is 32 bits. Thus the writer cannot write a larger chunk with a correct size.
        long size = position - sizePosition - 4;
        if (size > uint.MaxValue)
        {
            WebpThrowHelper.ThrowChunkTooLarge(size);
        }

        uint dataSize = (uint)size;

        // padding
        if (dataSize % 2 is 1)
        {
            stream.WriteByte(0);
            position++;
        }

        // Add the size of the encoded file to the Riff header.
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, dataSize);
        stream.Position = sizePosition;
        stream.Write(buffer);
        stream.Position = position;
    }

    public static long BeginWriteRiffFile(Stream stream, string formType)
    {
        long sizePosition = BeginWriteChunk(stream, RiffFourCc);
        stream.Write(Encoding.ASCII.GetBytes(formType));
        return sizePosition;
    }

    public static void EndWriteRiffFile(Stream stream, in WebpVp8X vp8x, bool updateVp8x, long sizePosition)
    {
        // The WebP Container Specification, section "RIFF Header", limits the File Size field to 2^32 - 10.
        // This limit is less than the generic RIFF limit of EndWriteChunk.
        long fileSize = stream.Position - sizePosition - 8;
        if (fileSize > uint.MaxValue - 9)
        {
            WebpThrowHelper.ThrowFileTooLarge(fileSize);
        }

        EndWriteChunk(stream, sizePosition + 4);

        // Write the VP8X chunk if necessary.
        if (updateVp8x)
        {
            long position = stream.Position;

            stream.Position = sizePosition + 12;
            vp8x.WriteTo(stream);
            stream.Position = position;
        }
    }
}
